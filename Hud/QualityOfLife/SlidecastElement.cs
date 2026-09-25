using System;
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

    public override bool Enabled => m_config.QualityOfLife.SlidecastEnabled;

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
}
