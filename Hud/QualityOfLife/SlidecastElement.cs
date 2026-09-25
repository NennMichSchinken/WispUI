using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using WispUI.Core;
using WispUI.Localization;
using WispUI.Style;

namespace WispUI.Hud.QualityOfLife;

/// <summary>
/// The slide window on the game's own cast bar: the last stretch of a cast, during which the
/// player can already move and the cast still goes through.
/// <para>
/// Drawn ON the game's bar rather than as a bar of our own. The game's cast bar is fine; what
/// it does not say is when moving stops costing the cast, and that is all this adds.
/// </para>
/// <para>
/// Two states (Florian, 2026-09-25, variant C of three): one colour while the cast is still
/// the player's to lose, another the moment the server has taken it. The switch is the game's
/// own statement (see <see cref="NativeUi.ReadOwnCast"/>), so it comes earlier on a good
/// connection and later on a bad one — which a fixed half-second mark cannot say.
/// </para>
/// <para>
/// 🔴 The window is the game's own gauge art, tinted, not a rectangle of ours. Three drawn
/// rectangles each missed the bar's rounded rim somewhere; the art fits it because it is what
/// the bar itself is made of (Florian, 2026-09-25).
/// </para>
/// </summary>
internal sealed class SlidecastElement : HudElement
{
    private readonly Configuration m_config;

    // What Collect found for this frame.
    private bool m_show;
    private NativeUi.CastBarArt m_art;
    private float m_total;
    private bool m_taken;

    public SlidecastElement(Configuration config)
    {
        m_config = config;
    }

    public override string Name => Strings.GroupSlidecast;

    public override bool Enabled => m_config.QualityOfLifeEnabled && m_config.QualityOfLife.SlidecastEnabled;

    public override void Collect()
    {
        bool casting = NativeUi.ReadOwnCast(out _, out m_total, out m_taken);
        m_show = casting && NativeUi.ReadCastBarArt(true, out m_art);
    }

    public override void Draw(ImDrawListPtr dl)
    {
        if (m_show)
        {
            this.DrawWindow(dl, in m_art, m_art.Origin, m_art.Scale, m_total, m_taken);
        }
    }

    // --- preview -------------------------------------------------------------

    public override bool HasPreview => true;

    public override Vector2 PreviewSize(int count) =>
        Tokens.Metric.SlidePreviewUnits * Tokens.Metric.SlidePreviewScale;

    /// <summary>
    /// A stand-in bar playing a cast on a loop, with the real window drawn over it.
    /// <para>
    /// The bar underneath is ours, because the game's cannot be drawn into a window. The
    /// window on top is <see cref="DrawWindow"/> with the game's own art, the same call the
    /// real one goes through, so a colour changed here is the colour seen in a fight. Without
    /// the art — the game has not built its cast bar yet — the band shows the bar alone
    /// rather than a window that would look different from the real one.
    /// </para>
    /// </summary>
    public override void DrawPreview(ImDrawListPtr dl, Vector2 origin, int count)
    {
        float cast = Tokens.Metric.SlidePreviewCast;
        float loop = cast + Tokens.Metric.SlidePreviewRest;

        // A clock read, not a counter: the preview must not advance anything the live
        // element advances, and it has nothing of its own to advance either.
        float t = (float)(ImGui.GetTime() % loop);

        float scale = Tokens.Metric.SlidePreviewScale;
        Vector2 units = Tokens.Metric.SlidePreviewUnits;
        Vector2 inset = Tokens.Metric.SlidePreviewTrackInset * scale;
        Vector2 trackMin = origin + inset;
        Vector2 trackMax = origin + (units * scale) - inset;
        dl.AddRectFilled(trackMin, trackMax, Tokens.Col.SlideTrack, trackMax.Y - trackMin.Y);

        // Between casts the bar stands empty, the way the game's goes away.
        if (t >= cast)
        {
            return;
        }

        float fill = MathF.Round((trackMax.X - trackMin.X) * (t / cast));
        dl.AddRectFilled(trackMin, new Vector2(trackMin.X + fill, trackMax.Y), Tokens.Col.SlideFill, trackMax.Y - trackMin.Y);

        if (!NativeUi.ReadCastBarArt(false, out NativeUi.CastBarArt art))
        {
            return;
        }

        bool taken = t >= cast - Tokens.Metric.SlideSeconds + Tokens.Metric.SlidePreviewLatency;
        this.DrawWindow(dl, in art, origin, new Vector2(scale, scale), cast, taken);
    }

    /// <summary>
    /// The window itself: the gauge art from where the slide window starts to the end of the
    /// bar, tinted. <paramref name="origin"/> and <paramref name="scale"/> place the gauge on
    /// the screen, so the same call draws over the game's bar and over the preview's.
    /// Allocates nothing.
    /// </summary>
    private void DrawWindow(ImDrawListPtr dl, in NativeUi.CastBarArt art, Vector2 origin, Vector2 scale, float total, bool taken)
    {
        // Where the window starts, as a share of the bar. A cast shorter than the window is
        // all window, and that is true: it can be moved out of from the start.
        float start = MathF.Max(0f, 1f - (Tokens.Metric.SlideSeconds / total));

        // The art carries a see-through lead-in on its left, so it is placed that far before
        // the point it should visibly start at. The narrowest it may get is its two ends.
        float left = (art.Width * start) - Tokens.Metric.SlideArtLeadIn;
        left = MathF.Min(left, art.Width - art.SliceLeft - art.SliceRight);

        Vector2 min = new(MathF.Round(origin.X + (left * scale.X)), MathF.Round(origin.Y));
        Vector2 max = new(MathF.Round(origin.X + (art.Width * scale.X)), MathF.Round(origin.Y + (art.Height * scale.Y)));

        Configuration.QualityOfLifeConfig cfg = m_config.QualityOfLife;
        uint colour = taken ? cfg.SlidecastReadyColour : cfg.SlidecastWaitColour;

        // 🔴 Two layers. Tinting a picture multiplies it, so the art alone can only come out
        // darker than the colour — it did, too dark to read (Florian, 2026-09-25). The colour
        // itself goes underneath, as a capsule that stops inside the rim; the art on top
        // covers its edges, so the shape is still the game's and the colour is the colour.
        Vector2 fillMin = new(
            MathF.Round(origin.X + (art.Width * start * scale.X)),
            MathF.Round(origin.Y + (Tokens.Metric.SlideFillInsetTop * scale.Y)));
        Vector2 fillMax = new(
            MathF.Round(origin.X + ((art.Width - Tokens.Metric.SlideFillInsetRight) * scale.X)),
            MathF.Round(origin.Y + ((art.Height - Tokens.Metric.SlideFillInsetBottom) * scale.Y)));

        if (fillMax.X > fillMin.X && fillMax.Y > fillMin.Y)
        {
            dl.AddRectFilled(
                fillMin,
                fillMax,
                Tokens.Col.Faded(colour, taken ? Tokens.Metric.SlideReadyAlpha : Tokens.Metric.SlideWaitAlpha),
                (fillMax.Y - fillMin.Y) * 0.5f);
        }

        DrawNineSlice(dl, in art, min, max, scale, colour);
    }

    /// <summary>
    /// Draws the art stretched to a rectangle the way the game stretches a nine-grid: the
    /// corners at their own size, the edges stretched one way, the middle both.
    /// </summary>
    private static void DrawNineSlice(ImDrawListPtr dl, in NativeUi.CastBarArt art, Vector2 min, Vector2 max, Vector2 scale, uint tint)
    {
        Span<float> x = stackalloc float[4];
        Span<float> y = stackalloc float[4];
        Span<float> u = stackalloc float[4];
        Span<float> v = stackalloc float[4];

        x[0] = min.X;
        x[1] = min.X + (art.SliceLeft * scale.X);
        x[2] = max.X - (art.SliceRight * scale.X);
        x[3] = max.X;
        y[0] = min.Y;
        y[1] = min.Y + (art.SliceTop * scale.Y);
        y[2] = max.Y - (art.SliceBottom * scale.Y);
        y[3] = max.Y;

        u[0] = art.Uv0.X;
        u[1] = art.Uv0.X + (art.SliceLeft * art.UvPerUnit.X);
        u[2] = art.Uv1.X - (art.SliceRight * art.UvPerUnit.X);
        u[3] = art.Uv1.X;
        v[0] = art.Uv0.Y;
        v[1] = art.Uv0.Y + (art.SliceTop * art.UvPerUnit.Y);
        v[2] = art.Uv1.Y - (art.SliceBottom * art.UvPerUnit.Y);
        v[3] = art.Uv1.Y;

        for (int row = 0; row < 3; row++)
        {
            if (y[row + 1] <= y[row])
            {
                continue;
            }

            for (int col = 0; col < 3; col++)
            {
                if (x[col + 1] <= x[col])
                {
                    continue;
                }

                dl.AddImage(
                    art.Texture,
                    new Vector2(x[col], y[row]),
                    new Vector2(x[col + 1], y[row + 1]),
                    new Vector2(u[col], v[row]),
                    new Vector2(u[col + 1], v[row + 1]),
                    tint);
            }
        }
    }
}
