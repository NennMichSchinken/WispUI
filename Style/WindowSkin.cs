using WispUI.Core;

namespace WispUI.Style;

/// <summary>The choice under Global, in the order its segments sit.</summary>
internal enum WindowStyle
{
    /// <summary>Whatever colour theme is set in the game's own options.</summary>
    Game = 0,

    Grey = 1,

    ClearBlue = 2,
}

/// <summary>
/// Which look the windows wear right now. Settled once a tick, never in the draw path: the
/// palette swap rewrites every window colour, and a swap halfway through a frame would draw
/// one half of the window in each style.
/// <para>
/// Only the windows — the settings window and the combat meter. Colours on the world (bars,
/// marks, HUD text) never follow it.
/// </para>
/// </summary>
internal static class WindowSkin
{
    /// <summary>
    /// The game's own number for Clear Blue, MEASURED with <c>/wisp status</c> (2026-10-27).
    /// It is also the Clear Blue column of the UIColor sheet.
    /// </summary>
    private const int GameClearBlue = 3;

    /// <summary>Where the game keeps the Clear Blue art, MEASURED off the running window.</summary>
    public const string ArtFolder = "ui/uld/img03/";

    private static bool s_settled;

    /// <summary>True while the windows wear Clear Blue.</summary>
    public static bool ClearBlue { get; private set; }

    /// <summary>
    /// Works out the look from the setting (and, when it says so, the game's theme) and swaps
    /// the palette if it changed. Cheap enough for every tick: one byte read, one compare.
    /// <para>
    /// Every theme other than Clear Blue reads as grey — Light, Classic FF, Clear White and
    /// Clear Green have no style of ours, and grey is the one that was measured against the
    /// game (Florian, 2026-10-27).
    /// </para>
    /// </summary>
    public static void Settle(int setting)
    {
        bool blue = (WindowStyle)setting switch
        {
            WindowStyle.Grey => false,
            WindowStyle.ClearBlue => true,
            _ => NativeUi.ColourTheme() == GameClearBlue,
        };

        if (s_settled && blue == ClearBlue)
        {
            return;
        }

        s_settled = true;
        ClearBlue = blue;
        Tokens.Col.ApplySkin(blue);
    }
}
