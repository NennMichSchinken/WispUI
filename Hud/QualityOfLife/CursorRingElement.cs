using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using WispUI.Core;
using WispUI.Localization;
using WispUI.Style;

namespace WispUI.Hud.QualityOfLife;

/// <summary>
/// A ring round the mouse pointer, so it can be found again when a fight fills the screen
/// (Florian, 2026-10-16, after the WoW addons that do the same).
/// <para>
/// Three settings and nothing else: size, line and colour. It follows the pointer every frame
/// and goes away when the pointer leaves the game. It stays while the camera is turned, which
/// Florian wants kept (2026-10-16).
/// </para>
/// <para>
/// 🔴 Drawn into the foreground list, not the background one every other element shares. The
/// ring belongs to the pointer, and the pointer is over everything — the settings window
/// included, which is also where somebody is when they are choosing its size.
/// </para>
/// </summary>
internal sealed class CursorRingElement : HudElement
{
    private readonly Configuration m_config;

    private bool m_shown;
    private Vector2 m_centre;

    public CursorRingElement(Configuration config)
    {
        m_config = config;
    }

    public override string Name => Strings.GroupCursorRing;

    public override bool Enabled => m_config.QualityOfLife.CursorRingEnabled;

    public override void Collect()
    {
        // ImGui's own word for "the pointer is not over the game", which it gives as a
        // position no screen has.
        m_shown = ImGui.IsMousePosValid() && NativeUi.GameCursorShown();

        // Asked of Windows now rather than read from ImGui, which took it at the start of the
        // game's frame: a ring drawn from that trails the pointer (see NativeUi.CursorNow).
        if (!NativeUi.CursorNow(out m_centre))
        {
            m_centre = ImGui.GetIO().MousePos;
        }
    }

    public override void Draw(ImDrawListPtr dl)
    {
        if (!m_shown)
        {
            return;
        }

        Configuration.QualityOfLifeConfig cfg = m_config.QualityOfLife;

        // Sized across the outer edge, so the number on the slider is the size on screen. A
        // line can be at most half of that, or the ring closes into a disc.
        float size = Tokens.WorldPx(cfg.CursorRingSize);
        float line = MathF.Min(Tokens.WorldLine(cfg.CursorRingThickness), size * 0.5f);
        float radius = (size - line) * 0.5f;

        ImGui.GetForegroundDrawList().AddCircle(m_centre, radius, cfg.CursorRingColour, 0, line);
    }
}
