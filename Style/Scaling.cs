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
    /// Applies a scale and rebuilds the font handles for it. <b>At load only</b> — see
    /// <see cref="Request"/> for why nothing in a drawing path may call this.
    /// </summary>
    public static void CommitAtLoad(float scale)
    {
        Tokens.SetScale(scale);
        Fonts.Rebuild();
    }

    /// <summary>
    /// Asks for a new scale. It lands on the next tick, not now.
    /// <para>
    /// 🔴 THIS MUST NOT HAPPEN INSIDE A FRAME. Rebuilding throws the font atlas away and
    /// makes a new one, and <see cref="Ink"/> took this frame's font pointers at the top of
    /// the frame — every piece of text drawn after the rebuild would carry a pointer into a
    /// freed atlas, and the draw list would reach Dalamud's renderer holding it. That is not
    /// a wrong-looking frame, it is an unhandled exception inside Present: the game closes
    /// (Florian, 2026-09-20, crashing on the click after moving the slider).
    /// </para>
    /// <para>
    /// The same trap the HUD's own font sync was moved to the tick for in session 2. The
    /// window's rebuild was left behind in the draw path, and the comment on
    /// <c>Plugin.SyncHudFonts</c> has been describing this exact crash ever since.
    /// </para>
    /// <para>
    /// Deferring the SCALE too, not only the rebuild: a frame that changed scale halfway
    /// would lay its second half out at the new size with the old faces. The frame that
    /// releases the slider finishes as it started, and the next one is wholly the new size.
    /// </para>
    /// </summary>
    public static void Request(float scale) => s_wanted = scale;

    /// <summary>
    /// Applies a scale asked for while drawing. Called from the framework tick, where
    /// nothing is holding a font pointer.
    /// </summary>
    public static void Settle()
    {
        if (float.IsNaN(s_wanted))
        {
            return;
        }

        float previous = Tokens.Scale;
        Tokens.SetScale(s_wanted);
        s_wanted = float.NaN;

        // Font handles carry a baked pixel size, so they only need rebuilding when the
        // scale actually moved.
        if (!Fonts.Ready || previous != Tokens.Scale)
        {
            Fonts.Rebuild();
        }
    }

    /// <summary>The scale waiting to be applied, or NaN for none.</summary>
    private static float s_wanted = float.NaN;

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
