namespace WispUI.Hud;

/// <summary>
/// Made-up afflictions on the frames, so the icons can be placed without waiting for a fight
/// to produce one.
/// <para>
/// 🔴 The reason this exists: everything on the Auras tab positions something that is only
/// there some of the time. Setting a size and an anchor for a row of icons that is not on
/// screen is setting it blind, and the natural way to get real ones — stand in front of
/// something that debuffs you — is a poor way to judge a layout, because you are also being
/// hit (Florian, 2026-09-13).
/// </para>
/// <para>
/// Held here rather than in the configuration because it is never written to disk: it is
/// switched on to set something up and off again, the way edit mode is, and a preview that
/// survived a restart would be a plugin that looks broken.
/// </para>
/// </summary>
internal static class AuraPreview
{
    /// <summary>Whether the frames are showing stand-in effects right now.</summary>
    public static bool Active { get; private set; }

    public static void Toggle() => Active = !Active;

    /// <summary>
    /// Called when the settings window closes. The preview belongs to the act of setting
    /// something up, and there is no setting up going on once the window is gone.
    /// </summary>
    public static void Stop() => Active = false;
}
