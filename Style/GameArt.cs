using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace WispUI.Style;

/// <summary>
/// The game's own Clear Blue art for tabs, buttons, tick boxes, sliders and drop-downs, drawn
/// from the game's files rather than rebuilt.
/// <para>
/// 🔴 Why not drawn: three drawn versions of the tabs missed their shape (Florian,
/// 2026-10-27), and the rule from the cast bar holds — after three tries at a shape, take the
/// game's graphic. The art is opaque and drawn untinted, so nothing here runs into ImGui's
/// darken-only tinting.
/// </para>
/// <para>
/// Every cut below was MEASURED off the running game with <c>/wisp status</c>, in the 1×
/// coordinates the game's parts lists use; the files are the <c>_hr1</c> versions at twice
/// that, which <see cref="HiRes"/> accounts for. Paths, cuts and slice widths: API notes,
/// addendum 2026-10-27.
/// </para>
/// </summary>
internal static class GameArt
{
    /// <summary>The <c>_hr1</c> files are twice the size the parts lists are written in.</summary>
    private const float HiRes = 2f;

    private const string TabSheet = WindowSkin.ArtFolder + "TabButtonA_hr1.tex";
    private const string ButtonSheet = WindowSkin.ArtFolder + "ButtonA_hr1.tex";
    private const string CheckSheet = WindowSkin.ArtFolder + "CheckBoxA_hr1.tex";
    private const string SliderSheet = WindowSkin.ArtFolder + "SliderGaugeHorizontalA_hr1.tex";
    private const string DropDownSheet = WindowSkin.ArtFolder + "DropDownA_hr1.tex";

    /// <summary>One cut of one sheet, and how much of each end must not stretch.</summary>
    internal readonly record struct Part(string Sheet, float U, float V, float W, float H, float Cap);

    public static readonly Part TabOff = new(TabSheet, 0f, 0f, 88f, 26f, 16f);
    public static readonly Part TabOn = new(TabSheet, 0f, 26f, 88f, 26f, 16f);
    public static readonly Part Button = new(ButtonSheet, 0f, 0f, 100f, 28f, 16f);
    public static readonly Part CheckBox = new(CheckSheet, 0f, 0f, 16f, 16f, 0f);
    public static readonly Part CheckMark = new(CheckSheet, 16f, 0f, 16f, 16f, 0f);
    public static readonly Part SliderKnob = new(SliderSheet, 1f, 1f, 14f, 15f, 0f);
    public static readonly Part SliderTrack = new(SliderSheet, 16f, 0f, 40f, 8f, 8f);
    public static readonly Part SliderFill = new(SliderSheet, 16f, 8f, 40f, 7f, 8f);
    public static readonly Part DropDown = new(DropDownSheet, 0f, 0f, 44f, 23f, 16f);

    /// <summary>Draws a cut whole, scaled to the box. False when the file is not loaded yet.</summary>
    public static bool Image(ImDrawListPtr dl, in Part part, Vector2 min, Vector2 max, uint tint = 0xFFFFFFFFu)
    {
        if (!Data.Icons.Game(part.Sheet, out ImTextureID handle, out Vector2 size))
        {
            return false;
        }

        dl.AddImage(handle, min, max, Uv(part.U, part.V, size), Uv(part.U + part.W, part.V + part.H, size), tint);
        return true;
    }

    /// <summary>
    /// Draws a cut stretched to any width, the way the game stretches it: both ends at their
    /// own width (scaled to the height the box is drawn at), only the middle pulled long.
    /// False when the file is not loaded yet, so the caller can fall back to drawing.
    /// </summary>
    public static bool Stretched(ImDrawListPtr dl, in Part part, Vector2 min, Vector2 max, uint tint = 0xFFFFFFFFu)
    {
        if (!Data.Icons.Game(part.Sheet, out ImTextureID handle, out Vector2 size))
        {
            return false;
        }

        float height = max.Y - min.Y;
        float cap = part.Cap * (height / part.H);

        // A box too narrow for both ends gives each end half of it rather than overlapping.
        cap = MathF.Min(MathF.Round(cap), MathF.Floor((max.X - min.X) * 0.5f));

        float top = part.V;
        float bottom = part.V + part.H;
        float leftEnd = part.U + part.Cap;
        float rightStart = part.U + part.W - part.Cap;

        float x1 = min.X + cap;
        float x2 = max.X - cap;

        dl.AddImage(handle, min, new Vector2(x1, max.Y), Uv(part.U, top, size), Uv(leftEnd, bottom, size), tint);
        dl.AddImage(handle, new Vector2(x1, min.Y), new Vector2(x2, max.Y), Uv(leftEnd, top, size), Uv(rightStart, bottom, size), tint);
        dl.AddImage(handle, new Vector2(x2, min.Y), max, Uv(rightStart, top, size), Uv(part.U + part.W, bottom, size), tint);
        return true;
    }

    /// <summary>A 1× part coordinate as a texture coordinate of the high-resolution file.</summary>
    private static Vector2 Uv(float u, float v, Vector2 size) => new(u * HiRes / size.X, v * HiRes / size.Y);
}
