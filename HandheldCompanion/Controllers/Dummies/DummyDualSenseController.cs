using HandheldCompanion.Controllers.SDL;
using HandheldCompanion.Inputs;

namespace HandheldCompanion.Controllers.Dummies
{
    public class DummyDualSenseController : DualSenseController
    {
        public DummyDualSenseController()
        {
            // The dummy describes mapping destinations on the emulated DualSense;
            // it is not an input device and must not advertise source touchpads.
            TargetButtons.Add(ButtonFlags.TouchpadClick);
            TargetButtons.Add(ButtonFlags.TouchpadTouch);
            TargetButtons.Add(ButtonFlags.B5);

            // DualSense Edge back paddles and function buttons
            TargetButtons.Add(ButtonFlags.L4);
            TargetButtons.Add(ButtonFlags.R4);
            TargetButtons.Add(ButtonFlags.LFn);
            TargetButtons.Add(ButtonFlags.RFn);
            TargetAxis.Add(AxisLayoutFlags.LeftPad);
            TargetAxis.Add(AxisLayoutFlags.RightPad);
        }

        public override bool IsVirtual() => true;
        public override bool IsDummy() => true;
        protected override int GetTouchpads() => 0;
        protected override int GetTouchpadFingers(int touchpad) => 0;

        public override void Tick(long ticks, float delta, bool commit = false)
        {
            ButtonState.Overwrite(InjectedButtons, Inputs.ButtonState);
        }
    }
}
