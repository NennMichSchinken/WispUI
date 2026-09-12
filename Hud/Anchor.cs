using System;
using System.Numerics;

namespace WispUI.Hud;

/// <summary>
/// Where a piece of text sits inside a frame. Nine points, the way a designer says it: the
/// three horizontals crossed with the three verticals.
/// <para>
/// One list for every text on every element. A name and a health figure that each brought
/// their own idea of "top left" would be two places to fix the same off-by-a-pixel, and the
/// user would have to learn the word twice.
/// </para>
/// </summary>
internal enum Anchor
{
    TopLeft = 0,
    Top,
    TopRight,
    Left,
    Centre,
    Right,
    BottomLeft,
    Bottom,
    BottomRight,
}

internal static class Anchors
{
    /// <summary>The nine, in reading order, which is the order the arrows walk them.</summary>
    public static readonly Anchor[] All =
    {
        Anchor.TopLeft,
        Anchor.Top,
        Anchor.TopRight,
        Anchor.Left,
        Anchor.Centre,
        Anchor.Right,
        Anchor.BottomLeft,
        Anchor.Bottom,
        Anchor.BottomRight,
    };

    /// <summary>Reads a stored index as an anchor, treating anything unknown as the top left.</summary>
    public static Anchor At(int index) =>
        index >= 0 && index < All.Length ? All[index] : Anchor.TopLeft;

    /// <summary>
    /// Where to start drawing so the text lands on its anchor.
    /// <para>
    /// The padding only applies to the edges: a centred text is centred on the frame, not on
    /// the frame less its padding, because the two would differ by half a pixel and the eye
    /// reads a centred label against the frame it sits in.
    /// </para>
    /// </summary>
    /// <param name="size">The width of the text and the height of its line, already measured.</param>
    public static Vector2 Place(Anchor anchor, Vector2 min, Vector2 max, Vector2 size, float padding)
    {
        int column = (int)anchor % 3;
        int row = (int)anchor / 3;

        float x = column switch
        {
            0 => min.X + padding,
            1 => (min.X + max.X - size.X) * 0.5f,
            _ => max.X - padding - size.X,
        };

        float y = row switch
        {
            0 => min.Y + padding,
            1 => (min.Y + max.Y - size.Y) * 0.5f,
            _ => max.Y - padding - size.Y,
        };

        return new Vector2(MathF.Round(x), MathF.Round(y));
    }
}
