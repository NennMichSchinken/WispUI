namespace WispUI.Style;

/// <summary>
/// What is put behind a piece of HUD text so it can be read over the world.
/// <para>
/// The frames lie over grass, stone, sky and a health bar, sometimes all within one name. On
/// a panel a colour alone carries text; over the world nothing does, and this is the choice
/// of what carries it instead.
/// </para>
/// </summary>
internal enum TextEdge
{
    /// <summary>
    /// Nothing behind the letters. Cleanest where the frames sit over a dark, quiet part of
    /// the screen, and the honest option for anyone who finds the other two noisy.
    /// </summary>
    None = 0,

    /// <summary>
    /// Two dark copies of the text under the letters, a near one and a fainter far one.
    /// Keeps their shapes exactly — a thin face stays thin.
    /// </summary>
    Shadow = 1,

    /// <summary>
    /// A dark line all the way around. Reads on any background, including a bright one above
    /// and left that a shadow does not cover, which is why the game's own floating combat
    /// text uses one.
    /// </summary>
    Outline = 2,
}

/// <summary>
/// How heavily a face is laid down when it is rasterised.
/// <para>
/// Not a second font. It multiplies the coverage of every glyph pixel as the face is turned
/// into an image, so the strokes become denser rather than wider — the letterforms are
/// untouched. That distinction matters: the other way to fake weight is to draw the glyph
/// twice, slightly offset, and that is a smear, not bold (session 7).
/// </para>
/// <para>
/// It exists because the right answer otherwise depends on the file. A face has whatever
/// weights its maker cut, a player's own font may only exist as Regular, and against a black
/// outline every one of them reads thinner than it is (Florian, 2026-09-12). This lets any
/// face be pulled up to the frame without hunting for another file.
/// </para>
/// </summary>
internal enum TextWeight
{
    /// <summary>The face exactly as it was drawn.</summary>
    Normal = 0,

    /// <summary>A little denser. The default, because against an outline nothing reads as heavy as it is.</summary>
    Medium = 1,

    /// <summary>As heavy as it goes before the counters inside letters start to close up.</summary>
    Bold = 2,
}

/// <summary>
/// The edge and weight lists behind the segments. Which face the HUD writes in lives in
/// <see cref="FontLibrary"/>, because that list is not fixed.
/// </summary>
internal static class HudText
{
    /// <summary>The weights, in the order the segments sit.</summary>
    public static readonly TextWeight[] Weights =
    {
        TextWeight.Normal,
        TextWeight.Medium,
        TextWeight.Bold,
    };

    public static TextWeight WeightAt(int index) =>
        index >= 0 && index < Weights.Length ? Weights[index] : TextWeight.Medium;

    /// <summary>
    /// What to multiply glyph coverage by. Held here rather than at the call site so the three
    /// steps mean one thing everywhere.
    /// <para>
    /// The top end is deliberately short of where it could go. Past about 1.6 the inside of an
    /// 'e' at sixteen pixels starts filling in, and a face that has lost its counters is less
    /// readable than the thin one it replaced — which is the whole point of the setting.
    /// </para>
    /// </summary>
    public static float Multiplier(TextWeight weight) => weight switch
    {
        TextWeight.Medium => 1.25f,
        TextWeight.Bold => 1.5f,
        _ => 1f,
    };

    /// <summary>The edges, in the order the segments sit.</summary>
    public static readonly TextEdge[] Edges =
    {
        TextEdge.None,
        TextEdge.Shadow,
        TextEdge.Outline,
    };

    public static TextEdge EdgeAt(int index) =>
        index >= 0 && index < Edges.Length ? Edges[index] : TextEdge.Shadow;

    public static int IndexOf(TextEdge edge)
    {
        for (int i = 0; i < Edges.Length; i++)
        {
            if (Edges[i] == edge)
            {
                return i;
            }
        }

        return 1;
    }
}
