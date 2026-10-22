using Newtonsoft.Json;
using System;

namespace HandheldCompanion.Misc
{
    /// <summary>
    /// Per power-state overrides stored in a game profile.
    /// A null value means "inherit": the setting is resolved from the default game profile,
    /// then from the power profile selected for the same power state.
    /// </summary>
    [Serializable]
    public class PerformanceOverrides
    {
        // Framerate limit. null = inherit, true = limit to FramerateValue, false = no limit.
        public bool? FramerateLimitEnabled { get; set; }
        public int FramerateValue { get; set; } = 0;

        [JsonIgnore]
        public bool HasFramerateOverride => FramerateLimitEnabled.HasValue;
    }
}