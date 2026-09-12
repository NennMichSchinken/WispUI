using System.Numerics;
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

    /// <summary>The window edit mode takes the mouse with, and one element's handle inside it.</summary>
    private const string IdArrange = "##wisp-arrange";
    private const string IdHandle = "##wisp-arrange-handle";

    /// <summary>
    /// Everything off, like the frames' own input window: it paints nothing and exists only so
    /// that ImGui asks for the mouse over the screen while arranging.
    /// </summary>
    private const ImGuiWindowFlags ArrangeWindowFlags =
        ImGuiWindowFlags.NoDecoration
        | ImGuiWindowFlags.NoMove
        | ImGuiWindowFlags.NoBackground
        | ImGuiWindowFlags.NoSavedSettings
        | ImGuiWindowFlags.NoFocusOnAppearing
        | ImGuiWindowFlags.NoBringToFrontOnFocus
        | ImGuiWindowFlags.NoNavFocus;

    private readonly List<HudElement> m_elements = new();

    /// <summary>Which element was last picked up, so its outline stays marked.</summary>
    private int m_selected = -1;

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

        // Before anything else, so every element is arranged on top of the wash rather than
        // under it.
        EditOverlay.Draw(dl);

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

        if (EditMode.IsActive)
        {
            this.Arrange(dl);
            EditOverlay.DrawBar();
        }
    }

    /// <summary>
    /// Lets the elements be dragged, once they have drawn and their bounds are known.
    /// <para>
    /// Here rather than in each element, because the physics are the same for all of them and
    /// only the shape differs — which is what <see cref="HudElement.Bounds"/> is for. It also
    /// means a new element becomes movable by saying so, with no drag code of its own.
    /// </para>
    /// </summary>
    private void Arrange(ImDrawListPtr dl)
    {
        ImGuiIOPtr io = ImGui.GetIO();
        Vector2 screen = io.DisplaySize;

        // One invisible window over the whole screen. Without a window ImGui never asks for
        // the mouse and the click goes to the world behind — the same lesson as the frames
        // themselves (spec §14).
        ImGui.SetNextWindowPos(Vector2.Zero);
        ImGui.SetNextWindowSize(screen);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);

        if (ImGui.Begin(IdArrange, ArrangeWindowFlags))
        {
            for (int i = 0; i < m_elements.Count; i++)
            {
                HudElement element = m_elements[i];

                if (!element.Enabled || !element.Movable)
                {
                    continue;
                }

                element.Bounds(out Vector2 min, out Vector2 max);

                if (max.X <= min.X || max.Y <= min.Y)
                {
                    continue;
                }

                this.ArrangeOne(dl, element, i, min, max, screen, io);
            }

            NativeUi.KeepGameCursor();
        }

        ImGui.End();
        ImGui.PopStyleVar();
    }

    private void ArrangeOne(
        ImDrawListPtr dl,
        HudElement element,
        int index,
        Vector2 min,
        Vector2 max,
        Vector2 screen,
        ImGuiIOPtr io)
    {
        ImGui.SetCursorScreenPos(min);
        ImGui.PushID(index);
        ImGui.InvisibleButton(IdHandle, max - min);

        bool hovered = ImGui.IsItemHovered();
        bool active = ImGui.IsItemActive();

        if (ImGui.IsItemActivated())
        {
            EditMode.BeginDrag(io.MousePos, min);
            m_selected = index;
        }

        ImGui.PopID();

        if (active)
        {
            Vector2 placed = EditMode.DragTo(io.MousePos, max - min, screen, io.KeyCtrl);
            element.MoveTo(placed);
        }
        else if (EditMode.IsDragging && m_selected == index)
        {
            EditMode.EndDrag();
        }

        // 🔴 No arrow keys, and none possible from here. Dalamud only passes a plugin the
        // keyboard while io.WantTextInput is set — while something is genuinely being typed
        // into — so every key goes to the game instead: the arrows turned the camera and
        // Escape opened the game's menu (Florian, 2026-09-12).
        //
        // Fine-tuning to the pixel therefore lives where it always did, on the two position
        // sliders in the Layout tab, which can also be typed into. Dragging is for placing;
        // the numbers are for placing exactly.
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeAll);
        }

        EditOverlay.DrawHandle(dl, min, max, element.Name, hovered || active || m_selected == index);
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
