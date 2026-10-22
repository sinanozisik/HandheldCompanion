using System;

namespace HandheldCompanion.Misc
{
    /// <summary>
    /// Per power-state overrides stored in a game profile.
    /// Inherit means the setting is resolved from the default game profile,
    /// then from the power profile selected for the same power state.
    /// </summary>
    [Serializable]
    public class PerformanceOverrides
    {
        public FramerateLimitMode FramerateMode { get; set; } = FramerateLimitMode.Inherit;
        public int FramerateValue { get; set; } = 0;
    }
}