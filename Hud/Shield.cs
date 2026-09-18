using System;

namespace WispUI.Hud;

/// <summary>
/// Where a shield sits on the health bar.
/// <para>
/// Only two, and the difference is where the band is anchored — not how it is drawn. Both
/// styles split the band the same way and both keep the whole shield visible; picking one is a
/// question of which reading you want, not of which one tells the truth.
/// </para>
/// </summary>
internal enum ShieldStyle
{
    /// <summary>
    /// From the left edge, over the health. The default, and what FFXIV's own party list does:
    /// the shield is read as its own quantity, always starting from the same place, so its
    /// length alone says how big it is without first finding the health edge.
    /// </summary>
    OverBar = 0,

    /// <summary>
    /// From the health edge into the missing health, wrapping back over the health when it no
    /// longer fits. What raid frames in other games do: health and shield together form one
    /// run, which reads as "this is how much more this person can take" in a single glance.
    /// </summary>
    IntoMissing = 1,
}

/// <summary>
/// Where the shield band lies on a bar, as fractions of its width.
/// <para>
/// Two pieces rather than one, split at the health edge: the part lying over empty bar and the
/// part lying over the health fill. 🔴 They are split because they must not be drawn alike.
/// The piece over the fill covers the health edge, and if it looked the same as the rest you
/// would read the edge in the wrong place and think somebody is healthier than they are
/// (Florian, 2026-09-18). A shield is never allowed to lie about health.
/// </para>
/// </summary>
internal readonly struct ShieldBand
{
    /// <summary>The piece over empty bar. Drawn solid, because the world is behind it.</summary>
    public readonly float MainStart;

    public readonly float MainEnd;

    /// <summary>The piece lying over the health fill. Drawn dimmer, never more transparent.</summary>
    public readonly float OverStart;

    public readonly float OverEnd;

    /// <summary>
    /// Where to put a bright line, or a negative number for nowhere.
    /// <para>
    /// Only ever set where the band starts inside the health fill, which is the one place its
    /// edge has no contrast of its own to be found by. Everywhere else the change of shade is
    /// the edge, and a line would be a second mark for the same thing.
    /// </para>
    /// </summary>
    public readonly float MarkAt;

    public ShieldBand(float mainStart, float mainEnd, float overStart, float overEnd, float markAt)
    {
        this.MainStart = mainStart;
        this.MainEnd = mainEnd;
        this.OverStart = overStart;
        this.OverEnd = overEnd;
        this.MarkAt = markAt;
    }

    public bool HasMain => this.MainEnd > this.MainStart;

    public bool HasOver => this.OverEnd > this.OverStart;

    public bool HasMark => this.MarkAt >= 0f;
}

/// <summary>
/// Turns a health fraction and a shield fraction into the band to draw. Pure arithmetic, like
/// <see cref="PartyFrames.FrameLayout"/>: nothing here knows about ImGui, colours or settings,
/// so the two styles can be reasoned about — and argued about — without running the game.
/// </summary>
internal static class Shield
{
    /// <summary>
    /// Works out where the band lies. Everything in and out is a fraction of the bar's width.
    /// </summary>
    /// <param name="health">Current health, 0 to 1.</param>
    /// <param name="shield">
    /// The shield as a share of maximum health. May exceed 1 — the game keeps a byte of
    /// percent and nothing promises it stops at a hundred — and is clamped here rather than
    /// at the call site, so no caller has to remember.
    /// </param>
    public static ShieldBand Band(ShieldStyle style, float health, float shield)
    {
        health = Math.Clamp(health, 0f, 1f);
        shield = Math.Clamp(shield, 0f, 1f);

        if (shield <= 0f)
        {
            return new ShieldBand(0f, 0f, 0f, 0f, -1f);
        }

        float start;
        float end;

        if (style == ShieldStyle.OverBar)
        {
            // Anchored at the bar's left edge, so its length reads as the amount directly.
            start = 0f;
            end = shield;
        }
        else
        {
            // From the health edge into what is missing; whatever does not fit turns round at
            // the right edge and runs back over the health. Both pieces are the same shield,
            // which is why the length of the whole band is the amount either way — that is the
            // property the style would lose if the overflow were simply dropped.
            float spill = Math.Max(0f, health + shield - 1f);
            start = Math.Max(0f, health - spill);
            end = Math.Min(1f, health + shield);
        }

        // Split at the health edge. Below it the band covers the fill, above it bare bar.
        float overStart = Math.Min(start, health);
        float overEnd = Math.Min(end, health);
        float mainStart = Math.Max(start, health);
        float mainEnd = Math.Max(end, health);

        // Marked only where the band begins inside the fill and has nothing else to be found
        // by. At the bar's own edge, or exactly at the health edge, there is already a line.
        float mark = start > 0f && start < health ? start : -1f;

        return new ShieldBand(mainStart, mainEnd, overStart, overEnd, mark);
    }
}
