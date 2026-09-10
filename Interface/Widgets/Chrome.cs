using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using WispUI.Localization;
using WispUI.Style;

namespace WispUI.Interface.Widgets;

/// <summary>
/// The window chrome, as reusable pieces: nav rows, tabs, buttons, switches.
/// Every one of them lives here exactly once. A second copy of any of these is a rule
/// break, because an improvement has to reach every place that uses it.
/// <para>
/// They all draw at an absolute screen position and take their hit box from an invisible
/// button, so a screen can lay itself out without fighting the ImGui cursor. Text goes
/// through <see cref="Ink"/>, which keeps the draw path free of allocations.
/// </para>
/// </summary>
internal static class Chrome
{
    /// <summary>Vertically centres one line of the given role in a box of that height.</summary>
    public static float CenterY(float top, float height, Ink.Role role) =>
        MathF.Round(top + ((height - Ink.LineHeight(role)) * 0.5f));

    /// <summary>
    /// Draws a top-to-bottom two-colour fill, the shape FFXIV uses for its own chrome.
    /// <para>
    /// ImGui cannot round the corners of a gradient, so with a radius the fill is built in
    /// three parts: a rounded base in the top colour, the gradient clipped to the straight
    /// middle, and a rounded cap in the bottom colour. The caps meet the gradient at the
    /// exact colour it has reached there, so the seam is invisible — and nothing square
    /// pokes out from under a rounded edge.
    /// </para>
    /// </summary>
    public static void VerticalFill(
        ImDrawListPtr dl,
        Vector2 min,
        Vector2 max,
        uint top,
        uint bottom,
        float rounding = 0f,
        ImDrawFlags flags = ImDrawFlags.RoundCornersAll)
    {
        if (rounding <= 0f || max.Y - min.Y <= rounding * 2f)
        {
            dl.AddRectFilledMultiColor(min, max, top, top, bottom, bottom);
            return;
        }

        dl.AddRectFilled(min, max, top, rounding, flags);

        dl.PushClipRect(new Vector2(min.X, min.Y + rounding), new Vector2(max.X, max.Y - rounding), true);
        dl.AddRectFilledMultiColor(min, max, top, top, bottom, bottom);
        dl.PopClipRect();

        ImDrawFlags capFlags = flags & ImDrawFlags.RoundCornersBottom;
        if (capFlags == 0)
        {
            capFlags = ImDrawFlags.RoundCornersNone;
        }

        dl.AddRectFilled(new Vector2(min.X, max.Y - rounding), max, bottom, rounding, capFlags);
    }

    /// <summary>A one-pixel horizontal rule.</summary>
    public static void Hairline(ImDrawListPtr dl, float x0, float x1, float y, uint colour) =>
        dl.AddRectFilled(new Vector2(x0, y), new Vector2(x1, y + Tokens.Line(1f)), colour);

    /// <summary>Shows a tooltip while the last item is hovered. Used to say why something is off.</summary>
    public static void TooltipOnHover(string text)
    {
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(text);
        }
    }

    /// <summary>
    /// One row of the navigation tree. Selected rows carry the gold edge on the left.
    /// Rows that are not built yet are dimmed and inert but stay visible, so the tree
    /// still tells you the module exists.
    /// </summary>
    public static bool NavItem(string id, string label, float x, float y, float width, bool selected, bool soon)
    {
        float height = Tokens.Metric.NavItemHeight;
        Vector2 min = new(x, y);
        Vector2 max = new(x + width, y + height);

        ImGui.SetCursorScreenPos(min);
        ImGui.InvisibleButton(id, new Vector2(width, height));
        bool hovered = ImGui.IsItemHovered() && !soon;
        bool clicked = ImGui.IsItemClicked() && !soon;

        ImDrawListPtr dl = ImGui.GetWindowDrawList();
        if (selected)
        {
            dl.AddRectFilled(min, max, Tokens.Col.NavSelected);
            dl.AddRectFilled(min, new Vector2(x + Tokens.Metric.NavAccent, max.Y), Tokens.Col.Gold);
        }
        else if (hovered)
        {
            dl.AddRectFilled(min, max, Tokens.Col.NavHover);
        }

        uint ink = soon ? Tokens.Col.InkFaint
            : selected ? Tokens.Col.GoldHi
            : hovered ? Tokens.Col.Ink
            : Tokens.Col.InkDim;
        Ink.Draw(dl, Ink.Role.Body, new Vector2(x + Tokens.Metric.NavIndent, CenterY(y, height, Ink.Role.Body)), ink, label);

        if (soon)
        {
            SoonChip(dl, max.X - Tokens.Space.Lg, y + (height * 0.5f));
        }

        return clicked;
    }

    /// <summary>The muted "Soon" pill on a module that is not built yet.</summary>
    private static void SoonChip(ImDrawListPtr dl, float right, float middleY)
    {
        Vector2 text = Ink.Measure(Ink.Role.Small, Strings.Soon);
        float height = Tokens.Metric.BadgeHeight;
        float width = text.X + (Tokens.Metric.BadgePaddingX * 2f);
        Vector2 min = new(MathF.Round(right - width), MathF.Round(middleY - (height * 0.5f)));
        Vector2 max = new(min.X + width, min.Y + height);

        dl.AddRect(min, max, Tokens.Col.EdgeDim, height * 0.5f, ImDrawFlags.RoundCornersAll, Tokens.Line(1f));
        Ink.Draw(
            dl,
            Ink.Role.Small,
            new Vector2(min.X + Tokens.Metric.BadgePaddingX, MathF.Round(min.Y + ((height - text.Y) * 0.5f))),
            Tokens.Col.InkFaint,
            Strings.Soon);
    }

    /// <summary>Measures how wide a tab needs to be, so a row of them can be laid out first.</summary>
    public static float MeasureTab(string label) =>
        MathF.Round(Ink.Measure(Ink.Role.Body, label).X + (Tokens.Metric.TabPaddingX * 2f));

    /// <summary>One tab at the head of a module screen.</summary>
    public static bool Tab(string id, string label, float x, float y, float width, bool selected)
    {
        float height = Tokens.Metric.TabHeight;
        Vector2 min = new(x, y);
        Vector2 max = new(x + width, y + height);

        ImGui.SetCursorScreenPos(min);
        ImGui.InvisibleButton(id, new Vector2(width, height));
        bool hovered = ImGui.IsItemHovered();
        bool clicked = ImGui.IsItemClicked();

        ImDrawListPtr dl = ImGui.GetWindowDrawList();
        float radius = Tokens.Radius.Control;

        // Drawn a little taller than the hit box and clipped back to it, so only the top
        // corners round off and the tab sits on the divider line instead of floating above it.
        dl.PushClipRect(min, max, true);
        VerticalFill(
            dl,
            min,
            new Vector2(max.X, max.Y + radius),
            selected ? Tokens.Col.ControlHover : Tokens.Col.TitleBar,
            selected ? Tokens.Col.PanelSoft : Tokens.Col.Control2,
            radius);
        dl.AddRect(
            min,
            new Vector2(max.X, max.Y + radius),
            selected ? Tokens.Col.GoldDim : Tokens.Col.EdgeDim,
            radius,
            ImDrawFlags.RoundCornersAll,
            Tokens.Line(1f));
        dl.PopClipRect();

        uint ink = selected ? Tokens.Col.GoldHi : hovered ? Tokens.Col.Ink : Tokens.Col.InkDim;
        float textX = MathF.Round(x + ((width - Ink.Measure(Ink.Role.Body, label).X) * 0.5f));
        Ink.Draw(dl, Ink.Role.Body, new Vector2(textX, CenterY(y, height, Ink.Role.Body)), ink, label);

        return clicked;
    }

    /// <summary>Measures a button, so a right-aligned row can be laid out before drawing.</summary>
    public static float MeasureButton(string label) =>
        MathF.Round(Ink.Measure(Ink.Role.Body, label).X + (Tokens.Metric.ButtonPaddingX * 2f));

    /// <summary>
    /// A standard button. A disabled one still answers a hover, because a disabled control
    /// owes the user a reason. Pass it in <paramref name="disabledReason"/>.
    /// </summary>
    public static bool Button(string id, string label, float x, float y, bool enabled, string? disabledReason = null)
    {
        float width = MeasureButton(label);
        float height = Tokens.Metric.ButtonHeight;
        Vector2 min = new(x, y);
        Vector2 max = new(x + width, y + height);

        ImGui.SetCursorScreenPos(min);
        ImGui.InvisibleButton(id, new Vector2(width, height));
        bool hovered = ImGui.IsItemHovered();
        bool clicked = enabled && ImGui.IsItemClicked();

        float alpha = enabled ? 1f : Tokens.Col.DisabledAlpha;
        ImDrawListPtr dl = ImGui.GetWindowDrawList();
        VerticalFill(
            dl,
            min,
            max,
            Tokens.Col.Faded(Tokens.Col.ButtonTop, alpha),
            Tokens.Col.Faded(Tokens.Col.ButtonBottom, alpha),
            Tokens.Radius.Control);

        uint edge = enabled && hovered ? Tokens.Col.GoldDim : Tokens.Col.ControlEdge;
        dl.AddRect(min, max, Tokens.Col.Faded(edge, alpha), Tokens.Radius.Control, ImDrawFlags.RoundCornersAll, Tokens.Line(1f));

        uint ink = enabled && hovered ? Tokens.Col.GoldHi : Tokens.Col.Ink;
        Ink.Draw(
            dl,
            Ink.Role.Body,
            new Vector2(x + Tokens.Metric.ButtonPaddingX, CenterY(y, height, Ink.Role.Body)),
            Tokens.Col.Faded(ink, alpha),
            label);

        if (!enabled && disabledReason is not null)
        {
            TooltipOnHover(disabledReason);
        }

        return clicked;
    }

    /// <summary>The per-module on/off switch that sits at the left of every module header.</summary>
    public static bool Switch(string id, float x, float y, bool value, bool enabled, string? disabledReason = null)
    {
        float width = Tokens.Metric.SwitchWidth;
        float height = Tokens.Metric.SwitchHeight;
        Vector2 min = new(x, y);
        Vector2 max = new(x + width, y + height);

        ImGui.SetCursorScreenPos(min);
        ImGui.InvisibleButton(id, new Vector2(width, height));
        bool clicked = enabled && ImGui.IsItemClicked();

        float alpha = enabled ? 1f : Tokens.Col.DisabledAlpha;
        float radius = height * 0.5f;
        ImDrawListPtr dl = ImGui.GetWindowDrawList();

        dl.AddRectFilled(min, max, Tokens.Col.Faded(value ? Tokens.Col.GoldSwitchTrack : Tokens.Col.Input, alpha), radius);
        dl.AddRect(
            min,
            max,
            Tokens.Col.Faded(value ? Tokens.Col.GoldDim : Tokens.Col.ControlEdge, alpha),
            radius,
            ImDrawFlags.RoundCornersAll,
            Tokens.Line(1f));

        float knob = Tokens.Metric.SwitchKnob;
        float inset = MathF.Round((height - knob) * 0.5f);
        float knobX = value ? max.X - inset - knob : min.X + inset;
        dl.AddRectFilled(
            new Vector2(knobX, min.Y + inset),
            new Vector2(knobX + knob, min.Y + inset + knob),
            Tokens.Col.Faded(value ? Tokens.Col.GoldHi : Tokens.Col.InkFaint, alpha),
            knob * 0.5f);

        if (!enabled && disabledReason is not null)
        {
            TooltipOnHover(disabledReason);
        }

        return clicked;
    }

    /// <summary>
    /// The head of a settings section: a title and one line saying what it covers.
    /// Returns the height it used, so the caller can carry on below it.
    /// </summary>
    public static float SectionHeader(string title, string description, float x, float y)
    {
        ImDrawListPtr dl = ImGui.GetWindowDrawList();
        Ink.Draw(dl, Ink.Role.Body, new Vector2(x, y), Tokens.Col.Ink, title);

        float used = Ink.LineHeight(Ink.Role.Body) + Tokens.Space.Xs;
        Ink.Draw(dl, Ink.Role.Small, new Vector2(x, MathF.Round(y + used)), Tokens.Col.InkDim, description);

        return used + Ink.LineHeight(Ink.Role.Small) + Tokens.Space.Lg;
    }

    /// <summary>The label side of a settings row, vertically centred against its control.</summary>
    public static void RowLabel(string label, float x, float y, float height)
    {
        ImDrawListPtr dl = ImGui.GetWindowDrawList();
        Ink.Draw(dl, Ink.Role.Body, new Vector2(x, CenterY(y, height, Ink.Role.Body)), Tokens.Col.Ink, label);
    }

    /// <summary>A quiet line of explanation under a control.</summary>
    public static float Hint(string text, float x, float y, float wrapWidth)
    {
        ImGui.SetCursorScreenPos(new Vector2(x, y));
        Ink.Push(Ink.Role.Small);
        ImGui.PushStyleColor(ImGuiCol.Text, Tokens.Col.InkFaint);
        ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + wrapWidth);
        ImGui.TextUnformatted(text);
        ImGui.PopTextWrapPos();
        ImGui.PopStyleColor();
        Ink.Pop(Ink.Role.Small);
        return ImGui.GetItemRectSize().Y;
    }

    /// <summary>A check box with its label to the right of it.</summary>
    public static bool CheckBox(string id, string label, float x, float y, bool value)
    {
        float box = Tokens.Metric.CheckBox;
        float labelWidth = Ink.Measure(Ink.Role.Body, label).X;
        float height = MathF.Max(box, Ink.LineHeight(Ink.Role.Body));
        float width = box + Tokens.Space.Md + labelWidth;

        ImGui.SetCursorScreenPos(new Vector2(x, y));
        ImGui.InvisibleButton(id, new Vector2(width, height));
        bool hovered = ImGui.IsItemHovered();
        bool clicked = ImGui.IsItemClicked();

        Vector2 min = new(x, MathF.Round(y + ((height - box) * 0.5f)));
        Vector2 max = new(min.X + box, min.Y + box);

        ImDrawListPtr dl = ImGui.GetWindowDrawList();
        dl.AddRectFilled(min, max, value ? Tokens.Col.Gold : Tokens.Col.Input, Tokens.Radius.Small);
        dl.AddRect(
            min,
            max,
            value ? Tokens.Col.GoldHi : hovered ? Tokens.Col.GoldDim : Tokens.Col.ControlEdge,
            Tokens.Radius.Small,
            ImDrawFlags.RoundCornersAll,
            Tokens.Line(1f));

        if (value)
        {
            // A tick drawn as two strokes, so it does not depend on a glyph.
            float thickness = Tokens.Line(2f);
            dl.AddLine(
                new Vector2(min.X + (box * 0.24f), min.Y + (box * 0.52f)),
                new Vector2(min.X + (box * 0.44f), min.Y + (box * 0.72f)),
                Tokens.Col.InkOnGold,
                thickness);
            dl.AddLine(
                new Vector2(min.X + (box * 0.44f), min.Y + (box * 0.72f)),
                new Vector2(min.X + (box * 0.78f), min.Y + (box * 0.28f)),
                Tokens.Col.InkOnGold,
                thickness);
        }

        Ink.Draw(
            dl,
            Ink.Role.Body,
            new Vector2(max.X + Tokens.Space.Md, CenterY(y, height, Ink.Role.Body)),
            hovered ? Tokens.Col.Ink : Tokens.Col.InkDim,
            label);

        return clicked;
    }

    /// <summary>What a <see cref="Slider"/> reports back after one frame.</summary>
    public readonly struct SliderResult
    {
        public readonly float Value;

        /// <summary>The value moved this frame. Show it, but do not save it yet.</summary>
        public readonly bool Changed;

        /// <summary>The drag ended this frame. This is the moment to save and to do the expensive work.</summary>
        public readonly bool Released;

        public SliderResult(float value, bool changed, bool released)
        {
            this.Value = value;
            this.Changed = changed;
            this.Released = released;
        }
    }

    /// <summary>
    /// A slider with its value in a box on the right. It reports moving and releasing
    /// separately, because the rule is that a change shows at once but is written on release.
    /// </summary>
    public static SliderResult Slider(string id, float x, float y, float width, float value, float min, float max, string valueText)
    {
        float height = Tokens.Metric.SliderHeight;
        float valueWidth = Tokens.Metric.SliderValueWidth;
        float trackWidth = width - valueWidth - Tokens.Space.Md;
        float grabWidth = Tokens.Metric.SliderGrabWidth;

        ImGui.SetCursorScreenPos(new Vector2(x, y));
        ImGui.InvisibleButton(id, new Vector2(trackWidth, height));
        bool active = ImGui.IsItemActive();
        bool released = ImGui.IsItemDeactivated();

        float travel = trackWidth - grabWidth;
        float result = value;
        bool changed = false;
        if (active && travel > 0f)
        {
            float local = ImGui.GetIO().MousePos.X - x - (grabWidth * 0.5f);
            float t = Math.Clamp(local / travel, 0f, 1f);
            result = min + (t * (max - min));
            changed = result != value;
        }

        float fraction = max > min ? Math.Clamp((result - min) / (max - min), 0f, 1f) : 0f;

        ImDrawListPtr dl = ImGui.GetWindowDrawList();
        float trackHeight = Tokens.Metric.SliderTrack;
        float trackTop = MathF.Round(y + ((height - trackHeight) * 0.5f));
        Vector2 trackMin = new(x, trackTop);
        Vector2 trackMax = new(x + trackWidth, trackTop + trackHeight);
        dl.AddRectFilled(trackMin, trackMax, Tokens.Col.Input, Tokens.Radius.Small);
        dl.AddRect(trackMin, trackMax, Tokens.Col.ControlEdge, Tokens.Radius.Small, ImDrawFlags.RoundCornersAll, Tokens.Line(1f));

        float grabHeight = Tokens.Metric.SliderGrabHeight;
        float grabX = MathF.Round(x + (fraction * travel));
        float grabTop = MathF.Round(y + ((height - grabHeight) * 0.5f));
        Vector2 grabMin = new(grabX, grabTop);
        Vector2 grabMax = new(grabX + grabWidth, grabTop + grabHeight);
        VerticalFill(dl, grabMin, grabMax, Tokens.Col.Ink, Tokens.Col.InkDim, Tokens.Radius.Small);
        dl.AddRect(grabMin, grabMax, Tokens.Col.Edge, Tokens.Radius.Small, ImDrawFlags.RoundCornersAll, Tokens.Line(1f));

        float valueX = x + trackWidth + Tokens.Space.Md;
        Vector2 valueMin = new(valueX, MathF.Round(y + ((height - Tokens.Metric.ButtonHeight) * 0.5f)));
        Vector2 valueMax = new(valueX + valueWidth, valueMin.Y + Tokens.Metric.ButtonHeight);
        dl.AddRectFilled(valueMin, valueMax, Tokens.Col.Input, Tokens.Radius.Small);
        dl.AddRect(valueMin, valueMax, Tokens.Col.ControlEdge, Tokens.Radius.Small, ImDrawFlags.RoundCornersAll, Tokens.Line(1f));

        float textX = MathF.Round(valueMax.X - Tokens.Space.Md - Ink.Measure(Ink.Role.Body, valueText).X);
        Ink.Draw(
            dl,
            Ink.Role.Body,
            new Vector2(textX, CenterY(valueMin.Y, Tokens.Metric.ButtonHeight, Ink.Role.Body)),
            Tokens.Col.Ink,
            valueText);

        return new SliderResult(result, changed, released);
    }

    /// <summary>
    /// The close glyph in the title bar. Drawn as two lines rather than typed as a character,
    /// so it never depends on a glyph being present in the game font.
    /// </summary>
    public static bool CloseButton(string id, float x, float y)
    {
        float size = Tokens.Metric.TitleButton;
        Vector2 min = new(x, y);
        Vector2 max = new(x + size, y + size);

        ImGui.SetCursorScreenPos(min);
        ImGui.InvisibleButton(id, new Vector2(size, size));
        bool hovered = ImGui.IsItemHovered();
        bool clicked = ImGui.IsItemClicked();

        ImDrawListPtr dl = ImGui.GetWindowDrawList();
        dl.AddRectFilled(min, max, Tokens.Col.Control, Tokens.Radius.Control);
        dl.AddRect(
            min,
            max,
            hovered ? Tokens.Col.Gold : Tokens.Col.EdgeDim,
            Tokens.Radius.Control,
            ImDrawFlags.RoundCornersAll,
            Tokens.Line(1f));

        uint ink = hovered ? Tokens.Col.GoldHi : Tokens.Col.InkDim;
        float pad = MathF.Round(size * 0.3f);
        float thickness = Tokens.Line(1f);
        dl.AddLine(new Vector2(min.X + pad, min.Y + pad), new Vector2(max.X - pad, max.Y - pad), ink, thickness);
        dl.AddLine(new Vector2(max.X - pad, min.Y + pad), new Vector2(min.X + pad, max.Y - pad), ink, thickness);

        return clicked;
    }
}
