using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using WispUI.Core;
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

    /// <summary>The one line of instruction, along the bottom of the screen.</summary>
    public static void DrawHint(ImDrawListPtr dl)
    {
        if (!EditMode.IsActive)
        {
            return;
        }

        Vector2 screen = ImGui.GetIO().DisplaySize;
        float size = Tokens.Px(Configuration.DefaultTextSize);
        float width = Ink.MeasureWidth(size, Strings.EditModeKeys);

        Ink.DrawScaledEdged(
            dl,
            size,
            new Vector2(
                MathF.Round((screen.X - width) * 0.5f),
                MathF.Round(screen.Y - (size * 3f))),
            Tokens.Col.HudInkQuiet,
            Strings.EditModeKeys,
            TextEdge.Shadow);
    }
}
