using System.Collections.Generic;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Conditions;
using WispUI.Hud;

namespace WispUI.Core;

/// <summary>
/// Every element WispUI draws in the world, and the one place that decides whether anything
/// is drawn at all.
/// <para>
/// The elements paint into ImGui's background draw list rather than into windows of their
/// own: a HUD element has no chrome, no title bar and no input, and a window would only bring
/// an id stack that reshuffles whenever a layout changes. The list also sits behind every
/// WispUI window, so the settings window is never covered by the thing it configures.
/// </para>
/// </summary>
internal sealed class HudManager
{
    /// <summary>
    /// When the game is not showing its own interface, WispUI shows none either. Reading a
    /// flag costs nothing and saves a whole frame's work.
    /// </summary>
    private static readonly ConditionFlag[] Hidden =
    {
        ConditionFlag.BetweenAreas,
        ConditionFlag.BetweenAreas51,
        ConditionFlag.WatchingCutscene,
        ConditionFlag.WatchingCutscene78,
        ConditionFlag.OccupiedInCutSceneEvent,
    };

    private readonly List<HudElement> m_elements = new();

    /// <summary>Adds an element. Draw order is the order they are added in (spec §5).</summary>
    public void Add(HudElement element) => m_elements.Add(element);

    /// <summary>
    /// Called once per frame. Collects first and draws afterwards, so an element reads the
    /// game exactly once and then only paints (CLAUDE.md §7.2).
    /// </summary>
    public void Draw()
    {
        if (!ShouldDraw())
        {
            return;
        }

        ImDrawListPtr dl = ImGui.GetBackgroundDrawList();

        for (int i = 0; i < m_elements.Count; i++)
        {
            HudElement element = m_elements[i];
            if (!element.Enabled || !element.HasAnythingToDraw)
            {
                continue;
            }

            element.Collect();
            element.Draw(dl);
        }
    }

    private static bool ShouldDraw()
    {
        if (!Services.ClientState.IsLoggedIn || Services.GameGui.GameUiHidden)
        {
            return false;
        }

        for (int i = 0; i < Hidden.Length; i++)
        {
            if (Services.Condition[Hidden[i]])
            {
                return false;
            }
        }

        return true;
    }
}
