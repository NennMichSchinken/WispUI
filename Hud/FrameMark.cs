using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using WispUI.Style;

namespace WispUI.Hud;

/// <summary>
/// How a frame says that this person needs something from you.
/// <para>
/// One set of shapes, used by every such statement rather than one per statement. It started
/// as the cleanse mark alone and became shared when the raise mark asked for the same thing
/// (Florian, 2026-09-18) — which is the rule from CLAUDE.md §5.2: an improvement here reaches
/// every mark at once, and a second mark is a group of settings, not a second piece of drawing
/// code.
/// </para>
/// <para>
/// 🔴 The numbering is fixed. These land in the saved configuration as integers, so an entry
/// may be added at the end and none may ever be renumbered.
/// </para>
/// </summary>
internal enum FrameMarkStyle
{
    /// <summary>Nothing. The icons carry it alone, for somebody who wants a quiet frame.</summary>
    None = 0,

    /// <summary>
    /// A coloured edge around the frame, with a band rising from its bottom.
    /// <para>
    /// The default, and the band is not decoration: an edge alone was overlooked in the game.
    /// Area catches the eye, a line does not (Florian, 2026-09-13). The bar underneath keeps
    /// saying role and job, which is what is read first.
    /// </para>
    /// </summary>
    Border = 1,

    /// <summary>
    /// The health bar itself takes the colour. Impossible to miss, at the price of the one
    /// piece of colour a frame already had something to say with.
    /// </summary>
    Bar = 2,

    /// <summary>
    /// The edge, and a wash over the whole frame instead of a band at its foot.
    /// <para>
    /// Added 2026-09-18 (Florian). The loudest of the three without spending the bar's colour:
    /// the job tone still reads through the wash, which is what separates this from
    /// <see cref="Bar"/> — and why the opacity beside it is the setting that matters. Far
    /// enough down it is a tint; far enough up it is a curtain.
    /// </para>
    /// </summary>
    Full = 3,
}

/// <summary>
/// Draws a <see cref="FrameMarkStyle"/>. Geometry only — it is handed a colour that has
/// already been stepped back for an absent member, so nothing here knows about presence,
/// settings or what the mark happens to mean.
/// </summary>
internal static class FrameMark
{
    /// <summary>
    /// How far up the frame the band reaches, as a share of its height. Far enough to be an
    /// area, not so far that it washes the whole bar and takes the role colour with it.
    /// </summary>
    private const float BandRise = 0.45f;

    /// <summary>
    /// Paints the edge and whatever fill the style asks for.
    /// <para>
    /// Drawn OUTSIDE the frame and over everything. Inside it, a thick edge eats the bar it is
    /// framing; underneath, the next frame's ground paints across it.
    /// </para>
    /// </summary>
    /// <param name="opacity">How solid the fill is at its strongest, 0 to 1.</param>
    public static void Draw(
        ImDrawListPtr dl,
        FrameMarkStyle style,
        Vector2 min,
        Vector2 max,
        uint colour,
        float thickness,
        float opacity)
    {
        if (style is FrameMarkStyle.None or FrameMarkStyle.Bar)
        {
            // Bar is not drawn here at all: it is the bar's own colour being swapped out
            // before the bar is painted, which happens where the bar is painted.
            return;
        }

        uint clear = colour & 0x00FFFFFFu;
        uint strong = Fade(colour, opacity);

        if (style == FrameMarkStyle.Full)
        {
            // Top to bottom, fading in downwards, so it keeps the direction the band had and
            // the name stays readable where it usually sits. A flat wash was the obvious first
            // try and made the frame look switched off rather than marked.
            dl.AddRectFilledMultiColor(min, max, Fade(colour, opacity * 0.35f), Fade(colour, opacity * 0.35f), strong, strong);
        }
        else
        {
            // The band, from the bottom of the frame to somewhere below halfway. Fades to
            // nothing, so it has no edge of its own to be mistaken for one.
            float height = MathF.Round((max.Y - min.Y) * BandRise);

            dl.AddRectFilledMultiColor(
                new Vector2(min.X, max.Y - height),
                max,
                clear,
                clear,
                strong,
                strong);
        }

        // AddRect puts half the thickness either side of the path, so the path is pushed out
        // by half to leave the frame all of its own room.
        float out2 = thickness * 0.5f;
        dl.AddRect(
            new Vector2(min.X - out2, min.Y - out2),
            new Vector2(max.X + out2, max.Y + out2),
            colour,
            0f,
            ImDrawFlags.None,
            thickness);
    }

    /// <summary>The style at a stored value, guarded against a number no longer in the list.</summary>
    public static FrameMarkStyle At(int stored) =>
        stored is >= (int)FrameMarkStyle.None and <= (int)FrameMarkStyle.Full
            ? (FrameMarkStyle)stored
            : FrameMarkStyle.Border;

    /// <summary>The same colour at a share of its own alpha.</summary>
    private static uint Fade(uint colour, float amount)
    {
        uint alpha = (colour >> 24) & 0xFFu;
        return (colour & 0x00FFFFFFu) | ((uint)Math.Clamp(alpha * amount, 0f, 255f) << 24);
    }

    /// <summary>The thickness of a mark's edge in screen pixels, never thinner than a line.</summary>
    public static float Thickness(float configured) =>
        MathF.Max(Tokens.Line(1f), Tokens.Px(configured));
}
