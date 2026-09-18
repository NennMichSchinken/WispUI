using System;

namespace WispUI.Hud;

/// <summary>
/// Where a shield sits on the health bar.
/// </summary>
internal enum ShieldStyle
{
    /// <summary>
    /// From the left edge, over the health, so its length alone is the amount.
    /// </summary>
    OverBar = 0,

    /// <summary>
    /// From the health edge into the missing health, wrapping back over the health when it no
    /// longer fits. Health and shield form one run, which reads as "this is how much more this
    /// person can take" in a single glance.
    /// </summary>
    IntoMissing = 1,
}

/// <summary>
/// Where the shield band lies on a bar, as fractions of its width.
/// <para>
/// One band, one colour. It was two pieces for a while — the part over the health drawn darker
/// than the part over empty bar — and that was wrong twice over: in game it read as a heal
/// absorb rather than a shield (Florian, 2026-09-18), and the reasoning behind it did not hold
/// either. The band covers health from its own start, so the job colour stops EARLIER than the
/// real health does and somebody is read as worse off, never better. Under-reading health is
/// the harmless direction, and the mark below finds the edge anyway.
/// </para>
/// </summary>
internal readonly struct ShieldBand
{
    public readonly float Start;

    public readonly float End;

    /// <summary>
    /// Where to put a bright line, or a negative number for nowhere.
    /// <para>
    /// Only ever set where the band starts inside the health fill, which is the one place its
    /// edge has no contrast of its own to be found by. At the bar's own edge, or out in empty
    /// bar, the band is already its own edge and a line would be a second mark for one thing.
    /// </para>
    /// </summary>
    public readonly float MarkAt;

    public ShieldBand(float start, float end, float markAt)
    {
        this.Start = start;
        this.End = end;
        this.MarkAt = markAt;
    }

    public bool HasBand => this.End > this.Start;

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
            return new ShieldBand(0f, 0f, -1f);
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
            // the right edge and runs back over the health. Both stretches are the same
            // shield, which is why the band's length is the amount either way — the property
            // this style would lose if the overflow were simply dropped at the bar's end.
            float spill = Math.Max(0f, health + shield - 1f);
            start = Math.Max(0f, health - spill);
            end = Math.Min(1f, health + shield);
        }

        // Marked only where the band begins inside the fill and has nothing else to be found
        // by. At the bar's own edge, or exactly at the health edge, there is already a line.
        float mark = start > 0f && start < health ? start : -1f;

        return new ShieldBand(start, end, mark);
    }
}
