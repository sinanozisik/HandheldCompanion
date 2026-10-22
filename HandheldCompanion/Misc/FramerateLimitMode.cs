namespace HandheldCompanion.Misc
{
    /// <summary>
    /// How a settings layer defines the framerate limit.
    /// Inherit defers to the next layer: game profile, default game profile, power profile, then RTSS itself.
    /// </summary>
    public enum FramerateLimitMode
    {
        Inherit = 0,
        NoLimit = 1,
        Limit = 2,
    }

    /// <summary>
    /// The layer a resolved framerate limit comes from.
    /// </summary>
    public enum FramerateLimitSource
    {
        GameProfile = 0,
        DefaultProfile = 1,
        PowerProfile = 2,
    }
}