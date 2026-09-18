using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using WispUI.Interface.Widgets;
using WispUI.Style;

namespace WispUI.Data;

/// <summary>How the fill inside a bar is painted.</summary>
internal enum BarFill
{
    Flat,
    Gradient,
    Inverse,
    Glass,
    Split,
    Ridge,
    RidgeFine,
    EdgeLit,
    Hollow,
}

/// <summary>
/// One entry of the bar style list: a name, and either a shape to draw or a picture to paint.
/// <para>
/// A style carries a texture OR a fill, never both. The painted ones are our own greyscale
/// files under <c>Textures/bars</c> — grey on purpose, because a grey picture takes any tint,
/// so one file serves every job colour, every role colour and the shield, and adding a colour
/// never means adding a texture.
/// </para>
/// </summary>
internal readonly struct BarStyle
{
    public readonly string Name;
    public readonly BarFill Fill;

    /// <summary>A file under <c>Textures/</c>, or null for a style that is drawn rather than painted.</summary>
    public readonly string? Texture;

    public BarStyle(string name, BarFill fill)
    {
        this.Name = name;
        this.Fill = fill;
        this.Texture = null;
    }

    public BarStyle(string name, string texture)
    {
        this.Name = name;

        // What it falls back to for the frame or two before the file is ready, and if it never
        // is. A flat bar in the right colour is wrong in texture and right in everything else,
        // which beats a bar that is not there.
        this.Fill = BarFill.Flat;
        this.Texture = texture;
    }
}

/// <summary>
/// The bar styles a health bar can be drawn in, and the code that paints one — used both
/// for the live preview in the selector and, later, for the bar itself.
/// <para>
/// Every style is built from colours that already exist as tokens. A style says how the
/// fill is shaped, not what colour it is: the colour comes from the colour mode beside it,
/// which is why the preview is drawn in a single stand-in tone.
/// </para>
/// </summary>
internal static class BarStyles
{
    /// <summary>The list, in the order the arrows walk it.</summary>
    public static readonly BarStyle[] All =
    {
        new("Flat", BarFill.Flat),
        new("Smooth", "bars/gradient.png"),
        new("Smooth soft", "bars/gradient-soft.png"),
        new("Gradient", BarFill.Gradient),
        new("Inverse", BarFill.Inverse),
        new("Glass", BarFill.Glass),
        new("Split", BarFill.Split),
        new("Ridge", BarFill.Ridge),
        new("Ridge fine", BarFill.RidgeFine),
        new("Edge lit", BarFill.EdgeLit),
        new("Hollow", BarFill.Hollow),
    };

    /// <summary>
    /// What a fresh configuration starts on: the painted smooth bar rather than a flat fill.
    /// The drawn styles beside it are placeholders that were never a design decision, so they
    /// are not what a new player should meet first.
    /// </summary>
    public const string DefaultName = "Smooth";

    /// <summary>
    /// Where a style sits in the list, by name. Zero for a name the list no longer has, which
    /// is a style removed between versions rather than an error — the bar falls back to the
    /// first entry and the player picks again, instead of the plugin refusing to draw.
    /// </summary>
    public static int IndexOf(string? name)
    {
        if (name is not null)
        {
            for (int i = 0; i < All.Length; i++)
            {
                if (string.Equals(All[i].Name, name, System.StringComparison.Ordinal))
                {
                    return i;
                }
            }
        }

        return 0;
    }

    /// <summary>The name at a position, for writing a selection back. Guarded against a stale index.</summary>
    public static string NameAt(int index) =>
        All[Math.Clamp(index, 0, All.Length - 1)].Name;

    /// <summary>The name of a style, for the selector. Returns a string that already exists.</summary>
    public static string Label(BarStyle style) => style.Name;

    /// <summary>
    /// Paints a style into a rectangle. The same routine serves the face of the selector and
    /// a row of its popup, which is why it takes the bounds rather than a size.
    /// </summary>
    public static void DrawPreview(ImDrawListPtr dl, BarStyle style, Vector2 min, Vector2 max) =>
        Draw(dl, style, min, max, Tokens.Col.SliderFill, Tokens.Radius.Small);

    /// <summary>
    /// Paints a style in a colour of its own — what a health bar actually does. The style
    /// says how the fill is shaped, the colour comes from the mode beside it, and the lit
    /// tone is derived rather than stored, so a job colour and a role colour are shaded the
    /// same way without either of them needing a second entry in a table.
    /// </summary>
    /// <param name="radius">Zero for a bar inside a frame, whose corners the frame already has.</param>
    public static void Draw(ImDrawListPtr dl, BarStyle style, Vector2 min, Vector2 max, uint colour, float radius)
    {
        // A painted style short-circuits the drawn ones. Tinted by the bar colour, which is
        // what the greyscale buys: the picture carries the shading, the colour carries the
        // meaning, and neither has to know about the other.
        if (style.Texture is not null && Icons.Bundled(style.Texture, out ImTextureID handle))
        {
            if (radius > 0f)
            {
                dl.AddImageRounded(handle, min, max, Vector2.Zero, Vector2.One, colour, radius);
            }
            else
            {
                dl.AddImage(handle, min, max, Vector2.Zero, Vector2.One, colour);
            }

            return;
        }

        uint plain = colour;
        uint bright = Lit(colour);
        float height = max.Y - min.Y;
        float line = Tokens.Line(1f);

        switch (style.Fill)
        {
            case BarFill.Flat:
                dl.AddRectFilled(min, max, plain, radius);
                break;

            case BarFill.Gradient:
                Chrome.VerticalFill(dl, min, max, bright, plain, radius);
                break;

            case BarFill.Inverse:
                Chrome.VerticalFill(dl, min, max, plain, bright, radius);
                break;

            case BarFill.Glass:
                dl.AddRectFilled(min, max, plain, radius);
                dl.AddRectFilled(min, new Vector2(max.X, min.Y + (height * 0.4f)), bright, radius, ImDrawFlags.RoundCornersTop);
                break;

            case BarFill.Split:
                dl.AddRectFilled(min, max, plain, radius);
                dl.AddRectFilled(min, new Vector2(max.X, MathF.Round(min.Y + (height * 0.5f))), bright, radius, ImDrawFlags.RoundCornersTop);
                break;

            case BarFill.Ridge:
                Bands(dl, min, max, plain, bright, Tokens.Px(3f), radius);
                break;

            case BarFill.RidgeFine:
                Bands(dl, min, max, plain, bright, Tokens.Px(2f), radius);
                break;

            case BarFill.EdgeLit:
                dl.AddRectFilled(min, max, plain, radius);
                dl.AddRectFilled(min, new Vector2(max.X, min.Y + line), bright);
                dl.AddRectFilled(new Vector2(min.X, max.Y - line), max, bright);
                break;

            case BarFill.Hollow:
                dl.AddRectFilled(min, max, Tokens.Col.Input, radius);
                dl.AddRect(min, max, plain, radius, ImDrawFlags.RoundCornersAll, line);
                break;
        }
    }

    /// <summary>
    /// The lit tone of a colour: the same colour carried towards white, with its opacity left
    /// alone. Derived rather than listed, because a bar can be painted in any of twenty job
    /// colours and a second table of highlights is twenty more chances to be one shade off.
    /// </summary>
    private static uint Lit(uint colour) =>
        Tokens.Col.Mix(colour, (colour & 0xFF000000u) | 0x00FFFFFFu, 0.28f);

    private static void Bands(ImDrawListPtr dl, Vector2 min, Vector2 max, uint plain, uint bright, float band, float radius)
    {
        dl.AddRectFilled(min, max, plain, radius);
        dl.PushClipRect(min, max, true);
        for (float y = min.Y; y < max.Y; y += band * 2f)
        {
            dl.AddRectFilled(new Vector2(min.X, y), new Vector2(max.X, y + band), bright);
        }

        dl.PopClipRect();
    }
}
