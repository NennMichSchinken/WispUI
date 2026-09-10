using Dalamud.Game.Config;
using WispUI.Core;

namespace WispUI.Style;

/// <summary>
/// Works out the interface scale. FFXIV has no virtual UI grid to snap to the way WoW does,
/// so the effect is built instead: one factor on every token, and whole-pixel rounding on
/// every value that reaches the screen (see <see cref="Tokens.Px"/>).
/// <para>
/// The factor is the user's, set in Global. Deriving it from the game's own settings was the
/// original plan, but the value the game reports does not mean what it looked like it meant
/// (it read 2 with the game set to 100%), so guessing at it was dropped rather than shipped
/// wrong. <see cref="LogGameScaleReadings"/> writes the candidates to the log so the mapping
/// can be settled from real readings.
/// </para>
/// </summary>
internal static class Scaling
{
    /// <summary>
    /// Applies a scale and rebuilds the font handles for it. Called on release, never per
    /// frame of a drag: rebuilding the font atlas is expensive, and the settings window
    /// resizes with the scale, so applying it live would move it out from under the cursor.
    /// </summary>
    public static void Commit(float scale)
    {
        float previous = Tokens.Scale;
        Tokens.SetScale(scale);

        // Font handles carry a baked pixel size, so they only need rebuilding when the
        // scale actually moved — or the very first time round.
        if (!Fonts.Ready || previous != Tokens.Scale)
        {
            Fonts.Rebuild();
        }
    }

    /// <summary>
    /// Writes every game setting that might describe the UI scale to the log, once at load.
    /// This is diagnosis, not behaviour: nothing reads these values yet.
    /// </summary>
    public static void LogGameScaleReadings()
    {
        Services.Log.Information(
            "Game scale readings — UiBaseScale: {Base}, UiHighScale: {High}, screen: {Width}x{Height}, mode: {Mode}.",
            Read(SystemConfigOption.UiBaseScale),
            Read(SystemConfigOption.UiHighScale),
            Read(SystemConfigOption.ScreenWidth),
            Read(SystemConfigOption.ScreenHeight),
            Read(SystemConfigOption.ScreenMode));
    }

    private static string Read(SystemConfigOption option) =>
        Services.GameConfig.TryGet(option, out uint value) ? value.ToString() : "unreadable";
}
