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
/// Two states (Florian, 2026-09-25, variant C of three): an outline over the last half second
/// while the cast is still the player's to lose, and a fill the moment the server has taken
/// it. The fill is the game's own statement (see <see cref="NativeUi.ReadOwnCast"/>), so it
/// arrives earlier on a good connection and later on a bad one — which is exactly what a
/// fixed half-second line cannot say.
/// </para>
/// </summary>
internal sealed class SlidecastElement : HudElement
{
    private readonly Configuration m_config;

    // What Collect found for this frame.
    private bool m_show;
    private Vector2 m_min;
    private Vector2 m_max;
    private float m_total;
    private bool m_taken;

    // --- measuring (⚠️ comes out once the track node and "taken" are confirmed) ---
    private bool m_dumped;
    private bool m_wasTaken;
    private int m_takenLogged;

    /// <summary>How many casts get their "taken" moment written to the log. A handful is a measurement; every cast is noise.</summary>
    private const int TakenLogLimit = 5;

    public SlidecastElement(Configuration config)
    {
        m_config = config;
    }

    public override string Name => Strings.GroupSlidecast;

    public override bool Enabled => m_config.QualityOfLifeEnabled && m_config.QualityOfLife.SlidecastEnabled;

    public override void Collect()
    {
        bool casting = NativeUi.ReadOwnCast(out float elapsed, out m_total, out m_taken);
        m_show = casting && NativeUi.ReadCastBar(out m_min, out m_max);

        this.Measure(casting, elapsed);
    }

    public override void Draw(ImDrawListPtr dl)
    {
        if (m_show)
        {
            this.DrawWindow(dl, m_min, m_max, m_total, m_taken);
        }
    }

    // --- preview -------------------------------------------------------------

    public override bool HasPreview => true;

    public override Vector2 PreviewSize(int count) =>
        new(Tokens.Metric.SlidePreviewWidth, Tokens.Metric.SlidePreviewHeight);

    /// <summary>
    /// A stand-in bar playing a cast on a loop, with the real window drawn over it.
    /// <para>
    /// The bar underneath is ours, because the game's cannot be drawn into a window. The
    /// window on top is <see cref="DrawWindow"/>, the same call the real one goes through, so
    /// a colour changed here is the colour seen in a fight.
    /// </para>
    /// </summary>
    public override void DrawPreview(ImDrawListPtr dl, Vector2 origin, int count)
    {
        float cast = Tokens.Metric.SlidePreviewCast;
        float loop = cast + Tokens.Metric.SlidePreviewRest;

        // A clock read, not a counter: the preview must not advance anything the live
        // element advances, and it has nothing of its own to advance either.
        float t = (float)(ImGui.GetTime() % loop);

        Vector2 min = origin;
        Vector2 max = origin + this.PreviewSize(count);
        dl.AddRectFilled(min, max, Tokens.Col.SlideTrack);

        // Between casts the bar stands empty, the way the game's goes away.
        if (t >= cast)
        {
            return;
        }

        float fill = MathF.Round((max.X - min.X) * (t / cast));
        dl.AddRectFilled(min, new Vector2(min.X + fill, max.Y), Tokens.Col.SlideFill);

        bool taken = t >= cast - Tokens.Metric.SlideSeconds + Tokens.Metric.SlidePreviewLatency;
        this.DrawWindow(dl, min, max, cast, taken);
    }

    /// <summary>
    /// The window itself, over a track that runs from <paramref name="min"/> to
    /// <paramref name="max"/>. Allocates nothing.
    /// </summary>
    private void DrawWindow(ImDrawListPtr dl, Vector2 min, Vector2 max, float total, bool taken)
    {
        float width = max.X - min.X;

        // The last half second of the cast, as a share of the bar. A cast shorter than that
        // is all window, and that is true: it can be moved out of from the start.
        float share = MathF.Min(1f, Tokens.Metric.SlideSeconds / total);
        float left = MathF.Round(max.X - (width * share));
        Vector2 from = new(left, min.Y);

        Configuration.QualityOfLifeConfig cfg = m_config.QualityOfLife;
        uint edge = taken ? cfg.SlidecastReadyColour : cfg.SlidecastWaitColour;

        if (taken)
        {
            dl.AddRectFilled(from, max, Tokens.Col.Faded(cfg.SlidecastReadyColour, Tokens.Metric.SlideFillAlpha));
        }

        dl.AddRect(from, max, edge, 0f, ImDrawFlags.None, Tokens.Metric.SlideEdge);
    }

    /// <summary>
    /// ⚠️ MEASURING ONLY. Writes the cast bar's node list on the first cast after loading,
    /// and how much of the cast was left when the server took it, for the first few casts.
    /// Builds strings, but only on those few frames.
    /// </summary>
    private void Measure(bool casting, float elapsed)
    {
        if (!casting)
        {
            m_wasTaken = false;
            return;
        }

        if (!m_dumped)
        {
            m_dumped = true;
            NativeUi.DumpCastBar();
        }

        if (m_taken && !m_wasTaken && m_takenLogged < TakenLogLimit)
        {
            m_takenLogged++;
            Services.Log.Information(
                "[cast] taken with {0:0.000}s of {1:0.000}s left",
                m_total - elapsed,
                m_total);
        }

        m_wasTaken = m_taken;
    }
}
