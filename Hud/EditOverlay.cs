using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using WispUI.Core;
using WispUI.Interface.Widgets;
using WispUI.Localization;
using WispUI.Style;

namespace WispUI.Hud;

/// <summary>
/// What edit mode puts on the screen behind the elements being arranged: a wash over the
/// world, the two centre lines, and the guide that appears when a drag is being held.
/// <para>
/// The wash is the whole point of it being visible at all. Arranging a HUD over a busy world
/// means judging edges against grass and stone; quieting the world for the duration is what
/// makes an alignment readable — and it is also the thing that says, unmistakably, that the
/// game is not being played right now (Florian, 2026-09-12).
/// </para>
/// </summary>
internal static class EditOverlay
{
    /// <summary>
    /// Drawn before any element, so everything being arranged sits on top of it. Into the
    /// background list, like the elements themselves.
    /// </summary>
    public static void Draw(ImDrawListPtr dl)
    {
        if (!EditMode.IsActive)
        {
            return;
        }

        Vector2 screen = ImGui.GetIO().DisplaySize;

        // Gentle. It has to calm the world without hiding what the frames will sit against —
        // the whole reason to arrange them in place rather than on a blank sheet.
        dl.AddRectFilled(Vector2.Zero, screen, Tokens.Col.EditWash);

        float centreX = MathF.Round(screen.X * 0.5f);
        float centreY = MathF.Round(screen.Y * 0.5f);
        float line = Tokens.Line(1f);

        // The two axes, always there. Not guides that appear on approach: knowing where the
        // middle is before you start moving is what lets you aim for it.
        dl.AddRectFilled(
            new Vector2(centreX, 0f),
            new Vector2(centreX + line, screen.Y),
            Tokens.Col.EditAxis);

        dl.AddRectFilled(
            new Vector2(0f, centreY),
            new Vector2(screen.X, centreY + line),
            Tokens.Col.EditAxis);

        // And the bright guide, only while a drag is actually being held by one. It is the
        // answer to "why did it stop there", which is otherwise an unexplained stutter.
        if (EditMode.HeldX is float heldX)
        {
            dl.AddRectFilled(
                new Vector2(MathF.Round(heldX), 0f),
                new Vector2(MathF.Round(heldX) + line, screen.Y),
                Tokens.Col.EditGuide);
        }

        if (EditMode.HeldY is float heldY)
        {
            dl.AddRectFilled(
                new Vector2(0f, MathF.Round(heldY)),
                new Vector2(screen.X, MathF.Round(heldY) + line),
                Tokens.Col.EditGuide);
        }
    }

    /// <summary>
    /// The label over one element being arranged, and its outline. Drawn after the element so
    /// it is never buried under what it names.
    /// </summary>
    public static void DrawHandle(ImDrawListPtr dl, Vector2 min, Vector2 max, string name, bool hovered)
    {
        float line = Tokens.Line(hovered ? 2f : 1f);
        uint edge = hovered ? Tokens.Col.EditGuide : Tokens.Col.EditAxis;

        // Filled bars rather than a stroke: ImGui centres a stroke on the path, which puts
        // half of it inside the element and leaves the edge looking soft (session 5).
        dl.AddRectFilled(new Vector2(min.X, min.Y), new Vector2(max.X, min.Y + line), edge);
        dl.AddRectFilled(new Vector2(min.X, max.Y - line), new Vector2(max.X, max.Y), edge);
        dl.AddRectFilled(new Vector2(min.X, min.Y), new Vector2(min.X + line, max.Y), edge);
        dl.AddRectFilled(new Vector2(max.X - line, min.Y), new Vector2(max.X, max.Y), edge);

        float size = Tokens.Px(Configuration.DefaultTextSize);
        float width = Ink.MeasureWidth(size, name);
        float pad = Tokens.Space.Sm;

        // Above the element when there is room, inside its top when there is not — an element
        // dragged to the top of the screen must not lose its own name off the edge.
        float labelY = min.Y - size - (pad * 2f);
        if (labelY < 0f)
        {
            labelY = min.Y + pad;
        }

        Vector2 plateMin = new(min.X, MathF.Round(labelY));
        Vector2 plateMax = new(plateMin.X + width + (pad * 2f), plateMin.Y + size + pad);

        dl.AddRectFilled(plateMin, plateMax, Tokens.Col.EditLabelBg, Tokens.Radius.Control);
        Ink.DrawScaledEdged(
            dl,
            size,
            new Vector2(plateMin.X + pad, plateMin.Y + (pad * 0.5f)),
            Tokens.Col.HudInk,
            name,
            TextEdge.None);
    }

    /// <summary>
    /// The four nudge arrows around an element, one per side.
    /// <para>
    /// 🔴 These exist because the arrow keys cannot. Dalamud only passes a plugin a key while
    /// something is being typed into, so the keyboard belongs to the game the whole time edit
    /// mode is up — the keys turned the camera instead (Florian, 2026-09-12, who asked for
    /// these instead).
    /// </para>
    /// <para>
    /// One pixel a click, ten with Shift held. Modifiers do arrive, unlike keys: they come in
    /// with the mouse state, which is why Ctrl works for ignoring the guides.
    /// </para>
    /// </summary>
    /// <returns>How far the element should move this frame, or zero.</returns>
    public static Vector2 DrawNudges(Vector2 min, Vector2 max, float step)
    {
        float size = Tokens.Px(18f);
        float gap = Tokens.Px(6f);

        Vector2 centre = new(
            MathF.Round((min.X + max.X) * 0.5f),
            MathF.Round((min.Y + max.Y) * 0.5f));

        Vector2 move = Vector2.Zero;

        if (Arrow(IdNudgeUp, new Vector2(centre.X - (size * 0.5f), min.Y - gap - size), size, Direction.Up))
        {
            move.Y -= step;
        }

        if (Arrow(IdNudgeDown, new Vector2(centre.X - (size * 0.5f), max.Y + gap), size, Direction.Down))
        {
            move.Y += step;
        }

        if (Arrow(IdNudgeLeft, new Vector2(min.X - gap - size, centre.Y - (size * 0.5f)), size, Direction.Left))
        {
            move.X -= step;
        }

        if (Arrow(IdNudgeRight, new Vector2(max.X + gap, centre.Y - (size * 0.5f)), size, Direction.Right))
        {
            move.X += step;
        }

        return move;
    }

    private enum Direction
    {
        Up,
        Down,
        Left,
        Right,
    }

    /// <summary>
    /// One nudge arrow. Repeats while held, so moving twenty pixels is one press rather than
    /// twenty — the same behaviour the arrow keys would have had.
    /// </summary>
    private static bool Arrow(string id, Vector2 at, float size, Direction direction)
    {
        Vector2 min = new(MathF.Round(at.X), MathF.Round(at.Y));
        Vector2 max = new(min.X + size, min.Y + size);

        ImGui.SetCursorScreenPos(min);
        ImGui.PushButtonRepeat(true);
        ImGui.InvisibleButton(id, new Vector2(size, size));
        bool clicked = ImGui.IsItemClicked();
        ImGui.PopButtonRepeat();

        bool hovered = ImGui.IsItemHovered();
        Chrome.ShowHand(hovered);

        ImDrawListPtr dl = ImGui.GetWindowDrawList();
        dl.AddRectFilled(min, max, hovered ? Tokens.Col.Control : Tokens.Col.EditLabelBg, Tokens.Radius.Control);
        dl.AddRect(
            min,
            max,
            hovered ? Tokens.Col.EditGuide : Tokens.Col.EditAxis,
            Tokens.Radius.Control,
            ImDrawFlags.RoundCornersAll,
            Tokens.Line(1f));

        // A filled triangle rather than a glyph: the face a HUD is lettered in is the
        // player's choice, and an arrow drawn from it would change shape with that choice.
        float inset = MathF.Round(size * 0.3f);
        uint ink = hovered ? Tokens.Col.GoldHi : Tokens.Col.HudInkQuiet;

        Vector2 a, b, c;

        switch (direction)
        {
            case Direction.Up:
                a = new Vector2(min.X + (size * 0.5f), min.Y + inset);
                b = new Vector2(max.X - inset, max.Y - inset);
                c = new Vector2(min.X + inset, max.Y - inset);
                break;

            case Direction.Down:
                a = new Vector2(min.X + (size * 0.5f), max.Y - inset);
                b = new Vector2(min.X + inset, min.Y + inset);
                c = new Vector2(max.X - inset, min.Y + inset);
                break;

            case Direction.Left:
                a = new Vector2(min.X + inset, min.Y + (size * 0.5f));
                b = new Vector2(max.X - inset, min.Y + inset);
                c = new Vector2(max.X - inset, max.Y - inset);
                break;

            default:
                a = new Vector2(max.X - inset, min.Y + (size * 0.5f));
                b = new Vector2(min.X + inset, max.Y - inset);
                c = new Vector2(min.X + inset, min.Y + inset);
                break;
        }

        dl.AddTriangleFilled(a, b, c, ink);
        return clicked;
    }

    private const string IdNudgeUp = "##wisp-nudge-up";
    private const string IdNudgeDown = "##wisp-nudge-down";
    private const string IdNudgeLeft = "##wisp-nudge-left";
    private const string IdNudgeRight = "##wisp-nudge-right";

    /// <summary>
    /// The bar that says arranging is happening and ends it.
    /// <para>
    /// 🔴 A button rather than a key, because a key was not available. Dalamud only hands a
    /// plugin the keyboard while <c>io.WantTextInput</c> is set — that is, while something is
    /// genuinely being typed into — so Escape and the arrows went to the game and edit mode
    /// had no way out at all (Florian, 2026-09-12, stuck in it, who then suggested this).
    /// </para>
    /// <para>
    /// It is also the better answer regardless: a mode that covers the screen should say so
    /// in words and offer the way out in the same place, rather than rely on a key nobody was
    /// told about.
    /// </para>
    /// </summary>
    public static void DrawBar()
    {
        if (!EditMode.IsActive)
        {
            return;
        }

        Vector2 screen = ImGui.GetIO().DisplaySize;

        // Tight. It is a strip that says what is happening and offers the way out, not a
        // panel — and it is sitting on top of the thing being arranged (Florian, 2026-09-12).
        float pad = Tokens.Space.Md;
        float rowHeight = Tokens.Px(26f);

        // Placed at the top the first time it appears, and left wherever it is pushed after
        // that — the thing being arranged may well be at the top of the screen.
        ImGui.SetNextWindowPos(
            new Vector2(MathF.Round(screen.X * 0.5f), Tokens.Px(48f)),
            ImGuiCond.Appearing,
            new Vector2(0.5f, 0f));

        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(pad, pad));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, Tokens.Line(1f));
        ImGui.PushStyleColor(ImGuiCol.WindowBg, Tokens.Col.Panel);
        ImGui.PushStyleColor(ImGuiCol.Border, Tokens.Col.EdgeDim);

        if (ImGui.Begin(IdBar, BarFlags))
        {
            Vector2 at = ImGui.GetCursorScreenPos();
            ImDrawListPtr dl = ImGui.GetWindowDrawList();

            Ink.Draw(
                dl,
                Ink.Role.Body,
                new Vector2(at.X, Chrome.CenterY(at.Y, rowHeight, Ink.Role.Body)),
                Tokens.Col.Heading,
                Strings.EditMode);

            float titleWidth = Ink.Measure(Ink.Role.Body, Strings.EditMode).X;
            float hintX = at.X + titleWidth + Tokens.Space.Md;

            Ink.Draw(
                dl,
                Ink.Role.Small,
                new Vector2(hintX, Chrome.CenterY(at.Y, rowHeight, Ink.Role.Small)),
                Tokens.Col.InkFaint,
                Strings.EditModeKeys);

            float hintWidth = Ink.Measure(Ink.Role.Small, Strings.EditModeKeys).X;
            float buttonX = hintX + hintWidth + Tokens.Space.Lg;

            if (Chrome.PillButton(IdBarDone, Strings.EditModeDone, buttonX, at.Y, rowHeight))
            {
                EditMode.Stop();
            }

            // The window sizes itself to what was drawn into it, which nothing here does
            // through ImGui's own cursor — so it is told.
            float width = buttonX - at.X + Ink.Measure(Ink.Role.Body, Strings.EditModeDone).X + (Tokens.Space.Lg * 2f);
            ImGui.Dummy(new Vector2(width, rowHeight));
        }

        ImGui.End();
        ImGui.PopStyleColor(2);
        ImGui.PopStyleVar(2);
    }

    private const string IdBar = "##wisp-editbar";
    private const string IdBarDone = "##wisp-editbar-done";

    /// <summary>
    /// Movable, unlike everything else the suite puts on screen while arranging: the bar may
    /// well be sitting exactly where an element needs to go.
    /// </summary>
    private const ImGuiWindowFlags BarFlags =
        ImGuiWindowFlags.NoTitleBar
        | ImGuiWindowFlags.NoResize
        | ImGuiWindowFlags.NoScrollbar
        | ImGuiWindowFlags.NoCollapse
        | ImGuiWindowFlags.NoSavedSettings
        | ImGuiWindowFlags.AlwaysAutoResize;
}
