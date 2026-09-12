using Dalamud.Interface.GameFonts;

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
    /// One copy of the text, offset and dark, under the letters. Keeps their shapes exactly —
    /// a thin face stays thin — and costs one extra draw.
    /// </summary>
    Shadow = 1,

    /// <summary>
    /// A dark line all the way around. Reads on any background, including a bright one that a
    /// shadow to the lower right does not cover, which is why the game's own floating combat
    /// text uses one. Costs four extra draws and thickens a thin face slightly.
    /// </summary>
    Outline = 2,
}

/// <summary>
/// Which of the game's own faces a HUD element writes in.
/// <para>
/// 🔴 All from the game files, none bundled. A suite that is meant to sit inside FFXIV
/// without announcing itself must not letter itself differently from the game — and it keeps
/// the plugin free of a font licence to carry (CLAUDE.md §4).
/// </para>
/// <para>
/// The game ships two more faces that are not here, Meidinger and JupiterNumeric. Both hold
/// digits and nothing else, so a name in either would come out empty and a percentage would
/// lose its sign. They are left out rather than offered with a warning.
/// </para>
/// </summary>
internal enum HudFontFace
{
    /// <summary>
    /// The game's own interface face, and what every window in the suite is set in. Light,
    /// which is exactly the complaint that led to the others being offered (Florian,
    /// 2026-09-12).
    /// </summary>
    Axis = 0,

    /// <summary>Wide and heavier. The game sets its gauge names in this.</summary>
    MiedingerMid = 1,

    /// <summary>Narrow and heavier. The game sets its window titles in this.</summary>
    TrumpGothic = 2,

    /// <summary>Serif. The game sets its job names in this.</summary>
    Jupiter = 3,
}

/// <summary>
/// The two lists behind the arrows, and the one place that turns a face into something
/// Dalamud will load.
/// </summary>
internal static class HudText
{
    /// <summary>The edges, in the order the arrows walk them.</summary>
    public static readonly TextEdge[] Edges =
    {
        TextEdge.None,
        TextEdge.Shadow,
        TextEdge.Outline,
    };

    /// <summary>The faces, in the order the arrows walk them. Axis first: it is the default.</summary>
    public static readonly HudFontFace[] Faces =
    {
        HudFontFace.Axis,
        HudFontFace.MiedingerMid,
        HudFontFace.TrumpGothic,
        HudFontFace.Jupiter,
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

    public static HudFontFace FaceAt(int index) =>
        index >= 0 && index < Faces.Length ? Faces[index] : HudFontFace.Axis;

    public static int IndexOf(HudFontFace face)
    {
        for (int i = 0; i < Faces.Length; i++)
        {
            if (Faces[i] == face)
            {
                return i;
            }
        }

        return 0;
    }

    /// <summary>What Dalamud calls the face. The one place the two vocabularies meet.</summary>
    public static GameFontFamily Family(HudFontFace face) => face switch
    {
        HudFontFace.MiedingerMid => GameFontFamily.MiedingerMid,
        HudFontFace.TrumpGothic => GameFontFamily.TrumpGothic,
        HudFontFace.Jupiter => GameFontFamily.Jupiter,
        _ => GameFontFamily.Axis,
    };
}
