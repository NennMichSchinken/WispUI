using System;

namespace WispUI.Hud;

/// <summary>
/// The parts of an element that can be taken out of the preview for a moment.
/// <para>
/// One bit per group card in the settings, because that is the unit somebody thinks in: the
/// eye on a card and the entry in the menu turn off the same thing, and neither has to know
/// about the other.
/// </para>
/// </summary>
[Flags]
internal enum PreviewPart
{
    None = 0,
    Name = 1 << 0,
    HealthText = 1 << 1,
    Mana = 1 << 2,
    Shield = 1 << 3,
    JobIcon = 1 << 4,
    Leader = 1 << 5,
    PartyNumber = 1 << 6,
    Debuffs = 1 << 7,
    OwnBuffs = 1 << 8,
    OtherBuffs = 1 << 9,
    CleanseMark = 1 << 10,
    RaiseMark = 1 << 11,
    RescueIcon = 1 << 12,
}

/// <summary>
/// What the preview is leaving out right now.
/// <para>
/// 🔴 These are not settings and must never become any. They say what you want to LOOK at
/// while you set something up — turn the icons off, place the name, turn them back on. A
/// switch that hid a name in the preview and also in the game would be a second place "name
/// off" can be written down, and the day somebody's names go missing they would have two
/// places to search (Florian, 2026-09-19, describing LumenUI's: it resets when the window
/// closes and everything is back on when you open it again).
/// </para>
/// <para>
/// So: held here, never written to disk, and cleared when the settings window closes. The
/// same reasoning edit mode is held under.
/// </para>
/// </summary>
internal static class PreviewMask
{
    /// <summary>Everything the preview can be asked to leave out, in menu order.</summary>
    public static readonly PreviewPart[] All =
    {
        PreviewPart.Name,
        PreviewPart.HealthText,
        PreviewPart.Mana,
        PreviewPart.Shield,
        PreviewPart.JobIcon,
        PreviewPart.Leader,
        PreviewPart.PartyNumber,
        PreviewPart.Debuffs,
        PreviewPart.OwnBuffs,
        PreviewPart.OtherBuffs,
        PreviewPart.CleanseMark,
        PreviewPart.RaiseMark,
        PreviewPart.RescueIcon,
    };

    /// <summary>What is being left out. Nothing, unless somebody has asked this session.</summary>
    public static PreviewPart Hidden { get; private set; }

    /// <summary>Whether anything at all is hidden — the menu says so on its own button.</summary>
    public static bool AnyHidden => Hidden != PreviewPart.None;

    public static bool Shows(PreviewPart part) => (Hidden & part) == 0;

    public static void Toggle(PreviewPart part) => Hidden ^= part;

    /// <summary>
    /// Back to showing everything. Called when the settings window closes, so the next time
    /// it opens there is nothing to wonder about.
    /// </summary>
    public static void ShowAll() => Hidden = PreviewPart.None;
}
