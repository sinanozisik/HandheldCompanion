using HandheldCompanion.Managers;
using HandheldCompanion.Utils;
using System;
using System.Runtime.Serialization;

namespace HandheldCompanion.Misc
{
    [Serializable]
    public class PowerProfile
    {
        public string Name = string.Empty;
        public string Description = string.Empty;

        public string FileName { get; set; } = string.Empty;
        public bool Default { get; set; }
        public bool DeviceDefault { get; set; }

        public Version Version { get; set; } = new();
        public Guid Guid { get; set; } = Guid.NewGuid();

        public bool TDPOverrideEnabled { get; set; }
        public double[]? TDPOverrideValues { get; set; }

        public bool CPUOverrideEnabled { get; set; }
        public double CPUOverrideValue { get; set; }

        public bool GPUOverrideEnabled { get; set; }
        public double GPUOverrideValue { get; set; }

        public bool AutoTDPEnabled { get; set; }
        public float AutoTDPRequestedFPS { get; set; } = 30.0f;
        public bool? FramerateLimitEnabled { get; set; }
        public int FramerateValue { get; set; } = 0; // default RTSS value

        // null on power profiles saved before FramerateLimitMode existed, see GetFramerateMode()
        public FramerateLimitMode? FramerateMode { get; set; }

        /// <summary>
        /// Inherit leaves RTSS alone, Override writes FramerateValue to RTSS (0 = no limit).
        /// </summary>
        public FramerateLimitMode GetFramerateMode()
        {
            if (FramerateMode.HasValue)
                return FramerateMode.Value.Normalize();

            // legacy: a disabled limiter never touched RTSS, an enabled one always wrote its value, 0 included
            return (FramerateLimitEnabled ?? FramerateValue != 0) ? FramerateLimitMode.Override : FramerateLimitMode.Inherit;
        }

        public void SetFramerateMode(FramerateLimitMode mode)
        {
            FramerateMode = mode.Normalize();

            // keep the legacy flag meaningful for older versions
            FramerateLimitEnabled = FramerateMode == FramerateLimitMode.Override;
        }

        [OnDeserialized]
        private void OnFramerateDeserialized(StreamingContext context)
        {
            // early builds stored NoLimit as its own mode
            if (FramerateMode.HasValue && FramerateMode.Value.IsLegacyNoLimit())
            {
                FramerateValue = 0;
                FramerateMode = FramerateLimitMode.Override;
            }
        }

        public bool EPPOverrideEnabled { get; set; }
        public uint EPPOverrideValue { get; set; } = 50;

        public bool CPUCoreEnabled { get; set; }
        public int CPUCoreCount { get; set; } = MotherboardInfo.NumberOfCores;

        public CoreParkingMode CPUParkingMode { get; set; } = CoreParkingMode.AllCoresAuto;

        public CPUBoostLevel CPUBoostLevel { get; set; } = CPUBoostLevel.Enabled;

        public FanProfile FanProfile { get; set; } = new();

        public bool IntelEnduranceGamingEnabled { get; set; } = false;
        public int IntelEnduranceGamingPreset { get; set; } = 0;

        public int OEMPowerMode { get; set; } = 0xFF;
        public Guid OSPowerMode { get; set; } = Managers.OSPowerMode.BetterPerformance;

        public PowerProfile()
        { }

        public PowerProfile(string name, string description)
        {
            Name = name;
            Description = description;
        }

        public string GetFileName()
        {
            string safe = FileUtils.MakeValidFileName(Name);
            if (string.IsNullOrWhiteSpace(safe))
                safe = "PowerProfile";

            return $"{safe}.json";
        }

        public bool IsDefault()
        {
            return Default && Guid == Guid.Empty;
        }

        public bool IsDeviceDefault()
        {
            return DeviceDefault;
        }

        public override string ToString()
        {
            return $"{Name} - {Description}";
        }
    }
}
