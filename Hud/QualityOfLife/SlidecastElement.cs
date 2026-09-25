using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
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
/// 🔴 The window is a node inside the game's own cast bar, wearing the bar's own gauge art
/// (Florian, 2026-09-25). Three drawn rectangles each missed the rounded rim; the art drawn
/// by ImGui fitted but could only be tinted darker than its colour. A node is drawn by the
/// game's renderer, which can also add colour, and it sits in the game's layers like the bar.
/// All of that lives in <see cref="NativeUi.UpdateSlideWindow"/>; this class only decides
/// what the node should say, right before the cast bar draws.
/// </para>
/// </summary>
internal sealed class SlidecastElement : HudElement, IDisposable
{
    private const string CastBarAddon = "_CastBar";

    private readonly Configuration m_config;

    public SlidecastElement(Configuration config)
    {
        m_config = config;

        // The game's thread, right before the cast bar draws — the only time the node may be
        // touched — and right before the cast bar is torn down, when it must come out.
        Services.AddonLifecycle.RegisterListener(AddonEvent.PreDraw, CastBarAddon, this.OnCastBarDraw);
        Services.AddonLifecycle.RegisterListener(AddonEvent.PreFinalize, CastBarAddon, this.OnCastBarGone);
    }

    public override string Name => Strings.GroupSlidecast;

    public override bool Enabled => m_config.QualityOfLifeEnabled && m_config.QualityOfLife.SlidecastEnabled;

    /// <summary>Nothing for the HUD pass: the game draws the window as part of its own bar.</summary>
    public override bool HasAnythingToDraw => false;

    public override void Collect()
    {
    }

    public override void Draw(ImDrawListPtr dl)
    {
    }

    /// <summary>
    /// Unhooks and takes the node out. Dalamud disposes a plugin on the game's thread, which
    /// is the thread the node may be touched on; anywhere else it is left to the game.
    /// </summary>
    public void Dispose()
    {
        Services.AddonLifecycle.UnregisterListener(AddonEvent.PreDraw, CastBarAddon, this.OnCastBarDraw);
        Services.AddonLifecycle.UnregisterListener(AddonEvent.PreFinalize, CastBarAddon, this.OnCastBarGone);

        if (Services.Framework.IsInFrameworkUpdateThread)
        {
            NativeUi.RemoveSlideWindow();
        }
    }

    private void OnCastBarDraw(AddonEvent type, AddonArgs args)
    {
        // Switched off: the node comes out rather than hiding, so a player who never wants
        // it has nothing of ours in the game's tree.
        if (!this.Enabled)
        {
            NativeUi.RemoveSlideWindow();
            return;
        }

        bool casting = NativeUi.ReadOwnCast(out _, out float total, out bool taken);
        Configuration.QualityOfLifeConfig cfg = m_config.QualityOfLife;

        NativeUi.UpdateSlideWindow(
            args.Addon.Address,
            casting,
            SlideStart(total),
            taken && cfg.SlidecastFill,
            taken ? cfg.SlidecastReadyColour : cfg.SlidecastWaitColour,
            Tokens.Col.Faded(cfg.SlidecastReadyColour, Tokens.Metric.SlideReadyAlpha));
    }

    private void OnCastBarGone(AddonEvent type, AddonArgs args) => NativeUi.ForgetSlideWindow(args.Addon.Address);

    /// <summary>
    /// Where the window starts, as a share of the bar. A cast shorter than the window is all
    /// window, and that is true: it can be moved out of from the start.
    /// </summary>
    private static float SlideStart(float total) =>
        total > 0f ? MathF.Max(0f, 1f - (Tokens.Metric.SlideSeconds / total)) : 1f;

    // --- preview -------------------------------------------------------------

    public override bool HasPreview => true;

    public override Vector2 PreviewSize(int count) =>
        Tokens.Metric.SlidePreviewUnits * Tokens.Metric.SlidePreviewScale;

    /// <summary>
    /// A stand-in bar playing a cast on a loop, with the window drawn over it.
    /// <para>
    /// ⚠️ The one preview in the suite NOT drawn by the real drawing code, and it cannot be:
    /// the real window is a node the game draws inside its own cast bar, and a game node
    /// cannot be drawn into a settings window. This is the same art, cut the same way, with
    /// the colour laid under it because ImGui can only tint a picture darker. Shape, place and
    /// colours match; the brightness is close rather than exact. Without the art — the game
    /// has not built its cast bar yet — the band shows the bar alone.
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
    /// The preview's window: the gauge art from where the slide window starts to the end of
    /// the bar, over a capsule of its colour. Allocates nothing.
    /// </summary>
    private void DrawWindow(ImDrawListPtr dl, in NativeUi.CastBarArt art, Vector2 origin, Vector2 scale, float total, bool taken)
    {
        float start = SlideStart(total);

        // The art carries a see-through lead-in on its left, so it is placed that far before
        // the point it should visibly start at. The narrowest it may get is its two ends.
        float left = (art.Width * start) - Tokens.Metric.SlideArtLeadIn;
        left = MathF.Min(left, art.Width - art.SliceLeft - art.SliceRight);

        Vector2 min = new(MathF.Round(origin.X + (left * scale.X)), MathF.Round(origin.Y));
        Vector2 max = new(MathF.Round(origin.X + (art.Width * scale.X)), MathF.Round(origin.Y + (art.Height * scale.Y)));

        Configuration.QualityOfLifeConfig cfg = m_config.QualityOfLife;
        uint colour = taken ? cfg.SlidecastReadyColour : cfg.SlidecastWaitColour;

        // Like the real one: the frame alone while waiting, a green fill under it once the
        // cast is taken. The fill here is a capsule of ours inside the rim rather than the
        // gauge's fill art, because ImGui can only tint a picture darker; the frame on top
        // covers its edges, so the shape is still the game's.
        if (!taken || !cfg.SlidecastFill)
        {
            DrawNineSlice(dl, in art, min, max, scale, colour);
            return;
        }

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
                Tokens.Col.Faded(colour, Tokens.Metric.SlideReadyAlpha),
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
