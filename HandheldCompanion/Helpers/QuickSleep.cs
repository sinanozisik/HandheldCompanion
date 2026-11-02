using HandheldCompanion.Controllers;
using HandheldCompanion.Managers;
using HandheldCompanion.Misc;
using HandheldCompanion.Shared;
using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace HandheldCompanion.Helpers;

public static class QuickSleep
{
    private const int PowerSystemSleeping3 = 4;
    private const int SystemPowerPolicyAc = 0;
    private const int SystemPowerPolicyDc = 1;
    private const int SystemPowerCapabilities = 4;
    private const int ExpectedPolicyBytes = 232;

    public static async Task Run()
    {
        if (!ManagerFactory.settingsManager.GetBoolean("QuickSleep"))
        {
            PowerActionsHelper.Sleep(force: false);
            return;
        }

        FrozenGame? frozen = null;
        PolicySnapshot? ac = null;
        PolicySnapshot? dc = null;
        bool slept = false;

        try
        {
            MultimediaManager.TurnOffScreen(false);
            frozen = TryFreezeForeground();
            ac = TryUseS3(SystemPowerPolicyAc);
            dc = TryUseS3(SystemPowerPolicyDc);

            slept = PowerActionsHelper.Sleep(force: true, disableWakeEvent: false);
        }
        catch (Exception ex)
        {
            LogManager.LogError("[QuickSleep] Entry failed: {0}", ex.Message);
        }
        finally
        {
            ac?.Restore();
            dc?.Restore();
        }

        if (!slept)
        {
            MultimediaManager.TurnOnScreen(false);
            Resume(frozen);
            return;
        }

        await ResumeWhenControllerReady(frozen).ConfigureAwait(false);
    }

    private static FrozenGame? TryFreezeForeground()
    {
        try
        {
            ProcessEx? current = ProcessManager.GetCurrent();
            if (current is null || current.ProcessId == 0 || current.ProcessId == Environment.ProcessId)
                return null;

            if (current.Process is null || current.Process.HasExited || current.IsSuspended)
                return null;

            Profile profile = ManagerFactory.profileManager.GetProfileFromPath(current.Path, true);
            if (profile.Default)
                return null;

            if (!ProcessManager.SuspendProcess(current.Handle, current.ProcessId))
            {
                LogManager.LogWarning("[QuickSleep] Could not freeze {0}. Sleep continues.", current.Executable);
                return null;
            }

            LogManager.LogInformation("[QuickSleep] Froze {0} before suspend.", current.Executable);
            return new FrozenGame(current.Handle, current.ProcessId, current.Executable);
        }
        catch (Exception ex)
        {
            LogManager.LogWarning("[QuickSleep] Could not freeze the foreground game: {0}", ex.Message);
            return null;
        }
    }

    private static async Task ResumeWhenControllerReady(FrozenGame? frozen)
    {
        if (frozen is null)
            return;

        DateTime deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            IController? controller = ControllerManager.GetTarget();
            if (controller is not null && controller.IsReady && controller.IsConnected())
                break;

            await Task.Delay(250).ConfigureAwait(false);
        }

        if (frozen is not FrozenGame game)
            return;

        IController? ready = ControllerManager.GetTarget();
        if (ready is null || !ready.IsReady || !ready.IsConnected())
            LogManager.LogWarning("[QuickSleep] Controller was not back after 10s. Thawing {0} anyway.", game.Executable);

        Resume(game);
    }

    private static void Resume(FrozenGame? frozen)
    {
        if (frozen is not FrozenGame game)
            return;

        try
        {
            if (!ProcessManager.ResumeProcess(game.Handle, game.ProcessId))
                LogManager.LogWarning("[QuickSleep] Could not thaw {0}.", game.Executable);
            else
                LogManager.LogInformation("[QuickSleep] Thawed {0}.", game.Executable);
        }
        catch (Exception ex)
        {
            LogManager.LogWarning("[QuickSleep] Could not thaw {0}: {1}", game.Executable, ex.Message);
        }
    }

    private static PolicySnapshot? TryUseS3(int level)
    {
        if (!FirmwareSupportsS3())
            return null;

        int policyBytes = Marshal.SizeOf<SystemPowerPolicy>();
        if (policyBytes != ExpectedPolicyBytes)
        {
            LogManager.LogWarning("[QuickSleep] Power policy layout is {0} bytes, expected {1}. S3 was not applied.", policyBytes, ExpectedPolicyBytes);
            return null;
        }

        byte[]? original = ReadPolicy(level, policyBytes);
        if (original is null)
            return null;

        int minSleep = BitConverter.ToInt32(original, MinSleepOffset);
        int maxSleep = BitConverter.ToInt32(original, MaxSleepOffset);
        if (minSleep == PowerSystemSleeping3 && maxSleep == PowerSystemSleeping3)
            return null;

        byte[] updated = (byte[])original.Clone();
        BitConverter.TryWriteBytes(updated.AsSpan(MinSleepOffset), PowerSystemSleeping3);
        BitConverter.TryWriteBytes(updated.AsSpan(MaxSleepOffset), PowerSystemSleeping3);

        if (!WritePolicy(level, updated))
            return null;

        LogManager.LogInformation("[QuickSleep] Sleep range for policy {0} set to S3.", level);
        return new PolicySnapshot(level, original);
    }

    private static bool? s3Supported;

    private static bool FirmwareSupportsS3()
    {
        if (s3Supported.HasValue)
            return s3Supported.Value;

        byte[] caps = new byte[16];
        uint status = CallNtPowerInformation(SystemPowerCapabilities, null, 0, caps, caps.Length);
        if (status != 0)
        {
            LogManager.LogWarning("[QuickSleep] Could not read sleep capabilities: 0x{0:X8}", status);
            s3Supported = false;
            return false;
        }

        s3Supported = caps[5] != 0;
        if (!s3Supported.Value)
            LogManager.LogInformation("[QuickSleep] Firmware has no S3. Critical suspend stays on the current sleep state.");
        return s3Supported.Value;
    }

    private static byte[]? ReadPolicy(int level, int bytes)
    {
        byte[] buffer = new byte[bytes];
        uint status = CallNtPowerInformation(level, null, 0, buffer, buffer.Length);
        if (status != 0)
        {
            LogManager.LogWarning("[QuickSleep] Could not read power policy {0}: 0x{1:X8}", level, status);
            return null;
        }

        uint revision = BitConverter.ToUInt32(buffer, 0);
        if (revision != 1)
        {
            LogManager.LogWarning("[QuickSleep] Power policy {0} revision is {1}. S3 was not applied.", level, revision);
            return null;
        }

        return buffer;
    }

    private static bool WritePolicy(int level, byte[] buffer)
    {
        uint status = CallNtPowerInformation(level, buffer, buffer.Length, null, 0);
        if (status == 0)
            return true;

        LogManager.LogWarning("[QuickSleep] Could not write power policy {0}: 0x{1:X8}", level, status);
        return false;
    }

    private static readonly int MinSleepOffset = Marshal.OffsetOf<SystemPowerPolicy>(nameof(SystemPowerPolicy.MinSleep)).ToInt32();
    private static readonly int MaxSleepOffset = Marshal.OffsetOf<SystemPowerPolicy>(nameof(SystemPowerPolicy.MaxSleep)).ToInt32();

    [DllImport("powrprof.dll")]
    private static extern uint CallNtPowerInformation(
        int informationLevel,
        byte[]? inputBuffer,
        int inputBufferLength,
        byte[]? outputBuffer,
        int outputBufferLength);

    private readonly record struct FrozenGame(nint Handle, int ProcessId, string Executable);

    private sealed class PolicySnapshot
    {
        private readonly int level;
        private readonly byte[] original;

        public PolicySnapshot(int level, byte[] original)
        {
            this.level = level;
            this.original = original;
        }

        public void Restore()
        {
            if (WritePolicy(level, original))
                LogManager.LogInformation("[QuickSleep] Restored power policy {0}.", level);
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct PowerActionPolicy
    {
        public int Action;
        public uint Flags;
        public uint EventCode;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct SystemPowerLevel
    {
        public byte Enable;
        public byte Spare0;
        public byte Spare1;
        public byte Spare2;
        public uint BatteryLevel;
        public PowerActionPolicy PowerPolicy;
        public int MinSystemState;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct SystemPowerPolicy
    {
        public uint Revision;
        public PowerActionPolicy PowerButton;
        public PowerActionPolicy SleepButton;
        public PowerActionPolicy LidClose;
        public int LidOpenWake;
        public uint Reserved;
        public PowerActionPolicy Idle;
        public uint IdleTimeout;
        public byte IdleSensitivity;
        public byte DynamicThrottle;
        public byte Spare2_0;
        public byte Spare2_1;
        public int MinSleep;
        public int MaxSleep;
        public int ReducedLatencySleep;
        public uint WinLogonFlags;
        public uint Spare3;
        public uint DozeS4Timeout;
        public uint BroadcastCapacityResolution;
        public SystemPowerLevel Discharge0;
        public SystemPowerLevel Discharge1;
        public SystemPowerLevel Discharge2;
        public SystemPowerLevel Discharge3;
        public uint VideoTimeout;
        public byte VideoDimDisplay;
        public byte VideoPad0;
        public byte VideoPad1;
        public byte VideoPad2;
        public uint VideoReserved0;
        public uint VideoReserved1;
        public uint VideoReserved2;
        public uint SpindownTimeout;
        public byte OptimizeForPower;
        public byte FanThrottleTolerance;
        public byte ForcedThrottle;
        public byte MinThrottle;
        public PowerActionPolicy OverThrottled;
    }
}
