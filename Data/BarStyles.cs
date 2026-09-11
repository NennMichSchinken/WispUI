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

/// <summary>One entry of the bar style list: a name and the fill it stands for.</summary>
internal readonly struct BarStyle
{
    public readonly string Name;
    public readonly BarFill Fill;

    public BarStyle(string name, BarFill fill)
    {
        this.Name = name;
        this.Fill = fill;
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
        new("Gradient", BarFill.Gradient),
        new("Inverse", BarFill.Inverse),
        new("Glass", BarFill.Glass),
        new("Split", BarFill.Split),
        new("Ridge", BarFill.Ridge),
        new("Ridge fine", BarFill.RidgeFine),
        new("Edge lit", BarFill.EdgeLit),
        new("Hollow", BarFill.Hollow),
    };

    /// <summary>The name of a style, for the selector. Returns a string that already exists.</summary>
    public static string Label(BarStyle style) => style.Name;

    /// <summary>
    /// Paints a style into a rectangle. The same routine serves the face of the selector and
    /// a row of its popup, which is why it takes the bounds rather than a size.
    /// </summary>
    public static void DrawPreview(ImDrawListPtr dl, BarStyle style, Vector2 min, Vector2 max)
    {
        uint bright = Tokens.Col.SliderFillHi;
        uint plain = Tokens.Col.SliderFill;
        float radius = Tokens.Radius.Small;
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
