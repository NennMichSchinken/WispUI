using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using WispUI.Style;

namespace WispUI.Data;

/// <summary>Where a style is offered. A style may be offered in both places.</summary>
[Flags]
internal enum BarStyleUse
{
    None = 0,

    /// <summary>The health bar itself.</summary>
    Bar = 1,

    /// <summary>The shield band laid on it.</summary>
    Shield = 2,

    Both = Bar | Shield,
}

/// <summary>
/// One entry of the bar style list: a name, and how to paint it.
/// <para>
/// A style is built from up to two layers, and the split is not decoration — it is the whole
/// reason the glow styles exist:
/// </para>
/// <list type="bullet">
/// <item><b>Base</b> — greyscale, opaque, MULTIPLIED by the bar colour. 255 gives the colour
/// itself, so a base can only ever make the bar <em>darker</em>. There is no value it can hold
/// that comes out brighter than the colour.</item>
/// <item><b>Light</b> — white, with its shape in the ALPHA channel, drawn over the base and
/// tinted with a lightened version of the same colour. This is the only way to get brighter
/// than the colour, which is what "the bar glows in the job colour" means (Florian,
/// 2026-09-18).</item>
/// </list>
/// <para>
/// Everything under <c>Textures/bars</c> is greyscale on purpose: a grey picture takes any
/// tint, so one file serves every job colour, every role colour and the shield, and adding a
/// colour never means adding a texture. The aurora's light layer is the one exception and
/// says why in its own comment below.
/// </para>
/// </summary>
internal readonly struct BarStyle
{
    public readonly string Name;

    /// <summary>A file under <c>Textures/</c>, or null for a style that is drawn rather than painted.</summary>
    public readonly string? Texture;

    /// <summary>The light layer over the base, or null for a style that does not glow.</summary>
    public readonly string? Light;

    /// <summary>
    /// How far the lit tone is carried toward white, 0 to 1.
    /// <para>
    /// 🔴 A colour whose brightest channel already sits at 255 — <c>#006EFF</c>, most job
    /// colours — cannot be brightened without its other channels rising, and that IS moving
    /// toward white. "A lighter blue" and "a little toward white" are the same movement; only
    /// the amount decides whether it reads as the job colour or as a white overlay. Measured
    /// against real bars at four amounts and settled at 0.35 (Florian, 2026-09-18).
    /// </para>
    /// </summary>
    public readonly float LitAmount;

    /// <summary>How opaque the light layer gets at its brightest, 0 to 1.</summary>
    public readonly float LightStrength;

    /// <summary>
    /// Whether the picture repeats at its own size instead of stretching over the bar.
    /// <para>
    /// A gradient must stretch — it describes the bar's height and nothing else. A pattern
    /// must not: stretched, its angle would change with the frame's width, so a diagonal at
    /// one setting is a different diagonal at another. Tiling keeps the angle fixed, and the
    /// sampler Dalamud sets up makes it free — VERIFIED in the Dalamud source
    /// (<c>Dx11Renderer.cs</c>, the shared sampler is <c>TEXTURE_ADDRESS_WRAP</c> on both
    /// axes), so UVs past 1 repeat rather than clamping.
    /// </para>
    /// </summary>
    public readonly bool Tiles;

    public readonly BarStyleUse Uses;

    /// <summary>A style that is drawn rather than painted: a plain fill in the bar colour.</summary>
    public BarStyle(string name, BarStyleUse uses)
    {
        this.Name = name;
        this.Texture = null;
        this.Light = null;
        this.LitAmount = 0f;
        this.LightStrength = 0f;
        this.Tiles = false;
        this.Uses = uses;
    }

    /// <summary>A style painted from one base picture.</summary>
    public BarStyle(string name, string texture, BarStyleUse uses, bool tiles = false)
    {
        this.Name = name;
        this.Texture = texture;
        this.Light = null;
        this.LitAmount = 0f;
        this.LightStrength = 0f;
        this.Tiles = tiles;
        this.Uses = uses;
    }

    /// <summary>A style painted from a base and a light layer over it.</summary>
    public BarStyle(string name, string texture, string light, float litAmount, float lightStrength, BarStyleUse uses)
    {
        this.Name = name;
        this.Texture = texture;
        this.Light = light;
        this.LitAmount = litAmount;
        this.LightStrength = lightStrength;
        this.Tiles = false;
        this.Uses = uses;
    }
}

/// <summary>
/// The styles a bar or a shield can be painted in, and the code that paints one — used both
/// for the live preview in the selector and for the bar itself.
/// <para>
/// A style says how the fill is shaped, never what colour it is: the colour comes from the
/// colour mode beside it, which is why the preview is drawn in a single stand-in tone.
/// </para>
/// </summary>
internal static class BarStyles
{
    /// <summary>
    /// How far the glow styles carry the lit tone toward white, and how opaque their light
    /// layer gets. One pair for the whole family, so the four never drift apart — picked off a
    /// ladder of five strengths shown on real bars in four job colours (Florian, 2026-09-18).
    /// </summary>
    private const float GlowLit = 0.35f;

    private const float GlowStrength = 0.68f;

    /// <summary>
    /// The aurora carries its own, stronger pair.
    /// <para>
    /// Not a second opinion about how bright a glow should be — the same numbers genuinely
    /// read weaker here. The three lit shapes put their brightest value across the whole
    /// width, so the peak is most of what you see. The aurora's peak is a curtain a few pixels
    /// wide and everything between the curtains sits far below it, so at the shared strength
    /// the bar averages out dimmer than the others even though the peaks match (Florian,
    /// 2026-09-18, seeing it beside them in game).
    /// </para>
    /// </summary>
    private const float AuroraLit = 0.45f;

    private const float AuroraStrength = 0.88f;

    /// <summary>The whole list. The two selectors each show a filtered view of it.</summary>
    public static readonly BarStyle[] All =
    {
        // Drawn, not painted: the one honest fallback, and the only survivor of the nine
        // placeholder shapes that stood here before there were textures. It is also what any
        // painted style falls back to for the frame or two before its file is ready — a bar in
        // the right colour and the wrong texture beats a bar that is not there.
        new("Flat", BarStyleUse.Both),

        new("Smooth", "bars/smooth.png", BarStyleUse.Both),
        new("Gradient", "bars/gradient.png", BarStyleUse.Bar),
        new("Bevel", "bars/bevel.png", BarStyleUse.Bar),
        new("Sheen", "bars/sheen.png", BarStyleUse.Bar),

        // The glow family: one quiet base, four light layers over it.
        new("Glow", "bars/body.png", "bars/lit-core.png", GlowLit, GlowStrength, BarStyleUse.Bar),
        new("Glow top", "bars/body.png", "bars/lit-top.png", GlowLit, GlowStrength, BarStyleUse.Bar),
        new("Glow bottom", "bars/body.png", "bars/lit-bottom.png", GlowLit, GlowStrength, BarStyleUse.Bar),
        new("Aurora", "bars/body.png", "bars/aurora.png", AuroraLit, AuroraStrength, BarStyleUse.Bar),

        // Patterns, for the shield only. A diagonal across a whole health bar is noise; across
        // the short stretch a shield covers it is the thing that says "this is laid on top".
        new("Stripe", "bars/stripe.png", BarStyleUse.Shield, tiles: true),
        new("Stripe fine", "bars/stripe-fine.png", BarStyleUse.Shield, tiles: true),
        new("Hatch", "bars/hatch.png", BarStyleUse.Shield, tiles: true),
    };

    /// <summary>What the health bar offers.</summary>
    public static readonly BarStyle[] ForBar = Filter(BarStyleUse.Bar);

    /// <summary>What the shield offers.</summary>
    public static readonly BarStyle[] ForShield = Filter(BarStyleUse.Shield);

    /// <summary>What a fresh configuration paints a health bar with.</summary>
    public const string DefaultName = "Smooth";

    /// <summary>
    /// What a fresh configuration paints a shield with. Not the bar's default: a shield is
    /// read as an overlay or it is read as a brighter piece of bar, and only a pattern makes
    /// that difference on a stretch that is often a dozen pixels wide.
    /// </summary>
    public const string ShieldDefaultName = "Stripe";

    /// <summary>
    /// Where a style sits in one of the lists, by name. Zero for a name the list no longer
    /// has, which is a style removed between versions rather than an error — the bar falls
    /// back to the first entry and the player picks again, instead of the plugin refusing to
    /// draw.
    /// </summary>
    public static int IndexOf(BarStyle[] list, string? name)
    {
        if (name is not null)
        {
            for (int i = 0; i < list.Length; i++)
            {
                if (string.Equals(list[i].Name, name, StringComparison.Ordinal))
                {
                    return i;
                }
            }
        }

        return 0;
    }

    /// <summary>The name at a position, for writing a selection back. Guarded against a stale index.</summary>
    public static string NameAt(BarStyle[] list, int index) =>
        list[Math.Clamp(index, 0, list.Length - 1)].Name;

    /// <summary>The style at a position in a list, guarded against a stale index.</summary>
    public static BarStyle At(BarStyle[] list, string? name) => list[IndexOf(list, name)];

    /// <summary>The name of a style, for the selector. Returns a string that already exists.</summary>
    public static string Label(BarStyle style) => style.Name;

    /// <summary>
    /// Paints a style into a rectangle. The same routine serves the face of the selector and
    /// a row of its popup, which is why it takes the bounds rather than a size.
    /// </summary>
    public static void DrawPreview(ImDrawListPtr dl, BarStyle style, Vector2 min, Vector2 max) =>
        Draw(dl, style, min, max, Tokens.Col.SliderFill, Tokens.Radius.Small);

    /// <summary>
    /// Paints a style in a colour of its own — what a health bar actually does. The style says
    /// how the fill is shaped, the colour comes from the mode beside it, and the lit tone is
    /// derived rather than stored, so a job colour and a role colour are shaded the same way
    /// without either of them needing a second entry in a table.
    /// </summary>
    /// <param name="radius">Zero for a bar inside a frame, whose corners the frame already has.</param>
    public static void Draw(ImDrawListPtr dl, BarStyle style, Vector2 min, Vector2 max, uint colour, float radius)
    {
        if (style.Texture is not null && Icons.Bundled(style.Texture, out ImTextureID handle, out Vector2 texSize))
        {
            Vector2 uv1 = style.Tiles ? Repeats(min, max, texSize) : Vector2.One;
            Paint(dl, handle, min, max, uv1, colour, radius);

            // The light layer, over the base and under everything else. Tinted with the lit
            // tone rather than white, and carrying the bar's own opacity multiplied by the
            // style's strength — so a half-transparent bar glows half as hard rather than
            // keeping a solid highlight on a bar you can see through.
            if (style.Light is not null && Icons.Bundled(style.Light, out ImTextureID lit, out _))
            {
                uint tint = Alpha(Lit(colour, style.LitAmount), Opacity(colour) * style.LightStrength);
                Paint(dl, lit, min, max, Vector2.One, tint, radius);
            }

            return;
        }

        dl.AddRectFilled(min, max, colour, radius);
    }

    private static void Paint(
        ImDrawListPtr dl,
        ImTextureID handle,
        Vector2 min,
        Vector2 max,
        Vector2 uv1,
        uint tint,
        float radius)
    {
        if (radius > 0f)
        {
            // AddImageRounded takes no UV pair worth tiling through, and nothing that tiles is
            // ever drawn rounded — a pattern belongs to the shield, which sits inside a frame
            // whose corners are already cut.
            dl.AddImageRounded(handle, min, max, Vector2.Zero, uv1, tint, radius);
        }
        else
        {
            dl.AddImage(handle, min, max, Vector2.Zero, uv1, tint);
        }
    }

    /// <summary>
    /// How many times a tiling picture repeats across a rectangle: its own pixels, scaled with
    /// the interface, divided into the space it covers. The result is the far UV corner.
    /// </summary>
    private static Vector2 Repeats(Vector2 min, Vector2 max, Vector2 texSize)
    {
        float w = MathF.Max(1f, Tokens.Px(texSize.X));
        float h = MathF.Max(1f, Tokens.Px(texSize.Y));
        return new Vector2((max.X - min.X) / w, (max.Y - min.Y) / h);
    }

    /// <summary>
    /// The lit tone of a colour: the same colour carried toward white, with its opacity left
    /// alone. Derived rather than listed, because a bar can be painted in any of twenty job
    /// colours and a second table of highlights is twenty more chances to be one shade off.
    /// </summary>
    private static uint Lit(uint colour, float amount) =>
        Tokens.Col.Mix(colour, (colour & 0xFF000000u) | 0x00FFFFFFu, amount);

    /// <summary>A colour's own opacity, 0 to 1.</summary>
    private static float Opacity(uint colour) => ((colour >> 24) & 0xFFu) / 255f;

    /// <summary>The same colour at a given opacity.</summary>
    private static uint Alpha(uint colour, float alpha) =>
        (colour & 0x00FFFFFFu) | ((uint)Math.Clamp(alpha * 255f, 0f, 255f) << 24);

    private static BarStyle[] Filter(BarStyleUse use)
    {
        int count = 0;
        foreach (BarStyle style in All)
        {
            if ((style.Uses & use) != 0)
            {
                count++;
            }
        }

        var kept = new BarStyle[count];
        int at = 0;
        foreach (BarStyle style in All)
        {
            if ((style.Uses & use) != 0)
            {
                kept[at++] = style;
            }
        }

        return kept;
    }
}
