using System;
using System.Runtime.Serialization;

namespace HandheldCompanion.Misc
{
    /// <summary>
    /// Per power-state overrides stored in a game profile.
    /// Inherit means the setting is resolved from the default game profile,
    /// then from the power profile selected for the same power state.
    /// Override applies FramerateValue, where 0 means no limit.
    /// </summary>
    [Serializable]
    public class PerformanceOverrides
    {
        public FramerateLimitMode FramerateMode { get; set; } = FramerateLimitMode.Inherit;
        public int FramerateValue { get; set; } = 0;

        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            // early builds stored NoLimit as its own mode
            if (FramerateMode.IsLegacyNoLimit())
                FramerateValue = 0;

            FramerateMode = FramerateMode.Normalize();
        }
    }
}