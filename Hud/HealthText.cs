using System;
using System.Globalization;

namespace WispUI.Hud;

/// <summary>
/// What the figure on a health bar says.
/// <para>
/// There is no "off" among them. Whether a text shows at all is the group's own switch, the
/// way it is for the name and for mana — a list you have to walk to the end of to turn
/// something off makes switching it off a search (Florian, 2026-09-12).
/// </para>
/// </summary>
internal enum HealthTextMode
{
    /// <summary>The health there is, as a number.</summary>
    Current = 0,

    /// <summary>How full the bar is. What a healer scans for.</summary>
    Percent = 1,

    /// <summary>How much is missing, as a negative. What a healer heals against.</summary>
    Deficit = 2,
}

/// <summary>
/// The figure on a health bar, built from the numbers. One place, so a second element with a
/// health bar says it the same way — and so the three modes stay three.
/// </summary>
internal static class HealthText
{
    /// <summary>The modes, in the order the arrows walk them.</summary>
    public static readonly HealthTextMode[] All =
    {
        HealthTextMode.Current,
        HealthTextMode.Percent,
        HealthTextMode.Deficit,
    };

    public static HealthTextMode At(int index) =>
        index >= 0 && index < All.Length ? All[index] : HealthTextMode.Percent;

    /// <summary>
    /// Builds the figure. This ALLOCATES, which is exactly why no draw path may call it
    /// directly: the caller keeps the string and asks again only when one of the numbers has
    /// actually moved (CLAUDE.md §7.1).
    /// </summary>
    public static string Build(HealthTextMode mode, uint hp, uint maxHp)
    {
        switch (mode)
        {
            case HealthTextMode.Current:
                return hp.ToString(CultureInfo.InvariantCulture);

            // Nothing at all when nothing is missing. A row of "0" down the side of the party
            // is eight figures that only ever matter when they are not there.
            case HealthTextMode.Deficit:
                return hp >= maxHp ? string.Empty : "-" + (maxHp - hp).ToString(CultureInfo.InvariantCulture);

            default:
                uint percent = maxHp > 0 ? (uint)Math.Round(hp * 100d / maxHp) : 0u;
                return percent.ToString(CultureInfo.InvariantCulture) + " %";
        }
    }
}
