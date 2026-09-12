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
/// The edge list behind the segments. Which face the HUD writes in lives in
/// <see cref="FontLibrary"/>, because that list is not fixed.
/// </summary>
internal static class HudText
{
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
