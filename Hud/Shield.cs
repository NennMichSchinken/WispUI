using System;

namespace WispUI.Hud;

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
/// so the shape can be reasoned about — and argued about — without running the game.
/// <para>
/// 🔴 There is ONE placement and no setting for it, which is the answer rather than an omission.
/// A second style anchored at the bar's left edge was built and thrown away: it filled the
/// missing health only once the shield grew larger than the health itself, so a half-dead
/// person with a shield showed a stripe at the far left and nothing in the gap — the one place
/// "this damage will not land" belongs. Corrected to fill the gap first it was letter for
/// letter this one, and two settings that do the same thing are one setting too many (Florian,
/// 2026-09-18, who spotted it against the game's own party list).
/// </para>
/// </summary>
internal static class Shield
{
    /// <summary>
    /// Works out where the band lies. Everything in and out is a fraction of the bar's width.
    /// <para>
    /// The shield fills the missing health from the health edge, and whatever does not fit
    /// turns round at the right edge and runs back over the health. Both stretches are the
    /// same shield, which is why the band's length is always the amount — the property that
    /// would be lost by simply dropping the overflow at the bar's end.
    /// </para>
    /// </summary>
    /// <param name="health">Current health, 0 to 1.</param>
    /// <param name="shield">
    /// The shield as a share of maximum health. May exceed 1 — the game keeps a byte of
    /// percent and nothing promises it stops at a hundred — and is clamped here rather than
    /// at the call site, so no caller has to remember.
    /// </param>
    public static ShieldBand Band(float health, float shield)
    {
        health = Math.Clamp(health, 0f, 1f);
        shield = Math.Clamp(shield, 0f, 1f);

        if (shield <= 0f)
        {
            return new ShieldBand(0f, 0f, -1f);
        }

        float spill = Math.Max(0f, health + shield - 1f);
        float start = Math.Max(0f, health - spill);
        float end = Math.Min(1f, health + shield);

        // Marked only where the band begins inside the fill and has nothing else to be found
        // by. At the bar's own edge, or exactly at the health edge, there is already a line.
        float mark = start > 0f && start < health ? start : -1f;

        return new ShieldBand(start, end, mark);
    }
}
