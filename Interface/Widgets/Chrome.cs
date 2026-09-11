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
    // Fixed ids for the parts of a group head. The group pushes its own id first, so these
    // stay unique without a string being built on every frame.
    private const string IdGroupToggle = "##wisp-group-toggle";
    private const string IdGroupAction = "##wisp-group-action";
    private const string IdGroupCollapse = "##wisp-group-collapse";

    /// <summary>Only ever mixed towards, never painted: the step a control takes under the hand.</summary>
    private const uint White = 0xFFFFFFFFu;

    private static bool s_closePopups;
    private static bool s_rowSplit;

    /// <summary>
    /// How much slower a slider moves while shift is held — a quarter speed, which puts four
    /// mouse pixels behind every step and makes one pixel of a frame width a deliberate act.
    /// </summary>
    private const float FineDragFactor = 0.25f;

    // Where a slider drag started. Only one control can hold the mouse at a time, which is
    // ImGui's own guarantee, so one anchor serves every slider in the suite. This is drag
    // state, not settings state: it belongs to the hand that is moving, and it is gone the
    // moment the button comes up.
    private static bool s_dragging;
    private static bool s_dragFine;
    private static bool s_dragRelative;
    private static float s_dragValue;
    private static float s_dragMouseX;

    /// <summary>
    /// Set for one frame when escape was pressed with a list or panel open. Whoever is drawing
    /// a popup this frame reads it and closes itself: only the popup's own body may call
    /// ImGui's close, so the key cannot act on it from the outside.
    /// </summary>
    public static bool ClosePopupRequested => s_closePopups;

    /// <summary>Asks every popup drawn this frame to close. Cleared again by <see cref="EndFrame"/>.</summary>
    public static void RequestClosePopups() => s_closePopups = true;

    /// <summary>Called once at the end of the window's frame, after every popup has had its turn.</summary>
    public static void EndFrame() => s_closePopups = false;

    /// <summary>Vertically centres one line of the given role in a box of that height.</summary>
    public static float CenterY(float top, float height, Ink.Role role) =>
        MathF.Round(top + ((height - Ink.LineHeight(role)) * 0.5f));

    /// <summary>
    /// Draws a top-to-bottom two-colour fill, the shape FFXIV uses for its own chrome.
    /// <para>
    /// ImGui cannot round the corners of a gradient, so with a radius the fill is built in
    /// three parts: a rounded cap in the top colour, the gradient between the caps, and a
    /// rounded cap in the bottom colour. The gradient starts and ends exactly where the caps
    /// do, so each cap holds the colour its neighbour begins with and nothing square pokes
    /// out from under a rounded edge.
    /// </para>
    /// <para>
    /// <paramref name="fade"/> ends the gradient early and fills the rest in the bottom
    /// colour — a lit edge that runs out near the top rather than a wash over the whole box.
    /// </para>
    /// </summary>
    public static void VerticalFill(
        ImDrawListPtr dl,
        Vector2 min,
        Vector2 max,
        uint top,
        uint bottom,
        float rounding = 0f,
        ImDrawFlags flags = ImDrawFlags.RoundCornersAll,
        float fade = 0f)
    {
        float fadeTop = min.Y;
        float fadeBottom = max.Y;

        if (rounding > 0f && max.Y - min.Y > rounding * 2f)
        {
            // The rounded caps can only hold one colour each, so the gradient runs between
            // them rather than under them. Drawing it across the full height instead left the
            // caps sitting on a colour the gradient had already moved past — a straight line
            // across the bar at exactly the cap's edge, which is what this used to do.
            fadeTop = min.Y + rounding;
            fadeBottom = max.Y - rounding;

            dl.AddRectFilled(min, new Vector2(max.X, fadeTop), top, rounding, flags & ImDrawFlags.RoundCornersTop);

            ImDrawFlags capFlags = flags & ImDrawFlags.RoundCornersBottom;
            dl.AddRectFilled(
                new Vector2(min.X, fadeBottom),
                max,
                bottom,
                rounding,
                capFlags == 0 ? ImDrawFlags.RoundCornersNone : capFlags);
        }

        // A fade shorter than the box: the light runs out near the lit edge and the rest of
        // the box is the surface colour, the way the game's own title bars are lit. The flat
        // remainder is painted in the colour the gradient ends on, so there is no seam.
        if (fade > 0f && fade < fadeBottom - fadeTop)
        {
            float fadeEnd = fadeTop + fade;
            dl.AddRectFilledMultiColor(
                new Vector2(min.X, fadeTop),
                new Vector2(max.X, fadeEnd),
                top,
                top,
                bottom,
                bottom);

            dl.AddRectFilled(new Vector2(min.X, fadeEnd), new Vector2(max.X, fadeBottom), bottom);
            return;
        }

        dl.AddRectFilledMultiColor(
            new Vector2(min.X, fadeTop),
            new Vector2(max.X, fadeBottom),
            top,
            top,
            bottom,
            bottom);
    }

    /// <summary>A one-pixel horizontal rule.</summary>
    public static void Hairline(ImDrawListPtr dl, float x0, float x1, float y, uint colour) =>
        dl.AddRectFilled(new Vector2(x0, y), new Vector2(x1, y + Tokens.Line(1f)), colour);

    /// <summary>
    /// The three-pixel divider measured off the game — dark, surface, light — fading out at
    /// both ends. This is the only rule that separates the window's big regions; anything
    /// finer uses <see cref="Hairline"/>. Returns the height it used.
    /// </summary>
    public static float Rule(ImDrawListPtr dl, float x0, float x1, float y)
    {
        float step = Tokens.Line(1f);
        for (int i = 0; i < Tokens.Col.TitleRule.Length; i++)
        {
            FadingHairline(dl, x0, x1, y + (i * step), Tokens.Col.TitleRule[i], Tokens.Metric.TitleRuleFade);
        }

        return Tokens.Metric.TitleRuleHeight;
    }

    /// <summary>
    /// A one-pixel rule that fades to nothing at both ends instead of butting into the frame,
    /// the way the game's own dividers run out towards the corners.
    /// </summary>
    public static void FadingHairline(ImDrawListPtr dl, float x0, float x1, float y, uint colour, float fade)
    {
        float thickness = Tokens.Line(1f);
        float width = x1 - x0;
        if (width <= 0f)
        {
            return;
        }

        // Two fades cannot take more than the line has to give.
        fade = MathF.Min(fade, width * 0.5f);
        uint clear = Tokens.Col.Faded(colour, 0f);

        dl.AddRectFilledMultiColor(
            new Vector2(x0, y),
            new Vector2(x0 + fade, y + thickness),
            clear,
            colour,
            colour,
            clear);

        dl.AddRectFilled(new Vector2(x0 + fade, y), new Vector2(x1 - fade, y + thickness), colour);

        dl.AddRectFilledMultiColor(
            new Vector2(x1 - fade, y),
            new Vector2(x1, y + thickness),
            colour,
            clear,
            clear,
            colour);
    }

    /// <summary>
    /// Shows a tooltip while the last item is hovered — the one place a longer explanation
    /// belongs, since it is not in the flow of the screen.
    /// <para>
    /// Built by hand rather than through <c>SetTooltip</c> for two reasons: ImGui sets a
    /// tooltip as a single unbroken line, which on a wide screen runs right across the game,
    /// and the default font is not the one the rest of the window is written in.
    /// </para>
    /// </summary>
    public static void TooltipOnHover(string text)
    {
        if (!ImGui.IsItemHovered())
        {
            return;
        }

        Ink.Push(Ink.Role.Body);
        ImGui.BeginTooltip();
        ImGui.PushTextWrapPos(Tokens.Metric.TooltipWrap);
        ImGui.PushStyleColor(ImGuiCol.Text, Tokens.Col.Ink);
        ImGui.TextUnformatted(text);
        ImGui.PopStyleColor();
        ImGui.PopTextWrapPos();
        ImGui.EndTooltip();
        Ink.Pop(Ink.Role.Body);
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

    /// <summary>
    /// One tab, drawn as a free-standing chip rather than a folder tab attached to a line.
    /// The selected chip is filled; the others are just text until you hover them.
    /// </summary>
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

        // Lighter at the top than at the bottom, the way the game shades its own tabs. That
        // one gradient is what makes them read as raised rather than as flat rectangles.
        if (selected)
        {
            VerticalFill(dl, min, max, Tokens.Col.ButtonTop, Tokens.Col.ButtonBottom, radius);
            dl.AddRect(min, max, Tokens.Col.GoldDim, radius, ImDrawFlags.RoundCornersAll, Tokens.Line(1f));
        }
        else if (hovered)
        {
            VerticalFill(dl, min, max, Tokens.Col.Control, Tokens.Col.Control2, radius);
        }

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
    /// <param name="primary">
    /// Marks the one button in a group that finishes the job — it carries the gold edge that
    /// a hovered button otherwise gets. At most one per group, or the emphasis says nothing.
    /// </param>
    public static bool Button(
        string id,
        string label,
        float x,
        float y,
        bool enabled,
        string? disabledReason = null,
        bool primary = false)
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

        uint edge = enabled && (hovered || primary) ? Tokens.Col.GoldDim : Tokens.Col.ControlEdge;
        dl.AddRect(min, max, Tokens.Col.Faded(edge, alpha), Tokens.Radius.Control, ImDrawFlags.RoundCornersAll, Tokens.Line(1f));

        uint ink = enabled && (hovered || primary) ? Tokens.Col.GoldHi : Tokens.Col.Ink;
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
        ImGui.SetCursorScreenPos(new Vector2(x, y));
        ImGui.InvisibleButton(id, new Vector2(Tokens.Metric.SwitchWidth, Tokens.Metric.SwitchHeight));
        bool clicked = enabled && ImGui.IsItemClicked();

        PaintSwitch(ImGui.GetWindowDrawList(), x, y, value, enabled ? 1f : Tokens.Col.DisabledAlpha);

        if (!enabled && disabledReason is not null)
        {
            TooltipOnHover(disabledReason);
        }

        return clicked;
    }

    /// <summary>
    /// Paints a switch without claiming a hit box, so a row that already owns its hit box can
    /// put one at its edge. The drawing lives here once; whoever wants a switch calls this.
    /// </summary>
    private static void PaintSwitch(ImDrawListPtr dl, float x, float y, bool value, float alpha)
    {
        float width = Tokens.Metric.SwitchWidth;
        float height = Tokens.Metric.SwitchHeight;
        Vector2 min = new(x, y);
        Vector2 max = new(x + width, y + height);
        float radius = height * 0.5f;

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
    }

    /// <summary>Paints a tick box without claiming a hit box. The one tick in the suite.</summary>
    private static void PaintTick(ImDrawListPtr dl, Vector2 min, bool value, bool hovered, float alpha)
    {
        float box = Tokens.Metric.CheckBox;
        Vector2 max = new(min.X + box, min.Y + box);

        dl.AddRectFilled(min, max, Tokens.Col.Faded(value ? Tokens.Col.Gold : Tokens.Col.Input, alpha), Tokens.Radius.Small);
        dl.AddRect(
            min,
            max,
            Tokens.Col.Faded(value ? Tokens.Col.GoldHi : hovered ? Tokens.Col.GoldDim : Tokens.Col.ControlEdge, alpha),
            Tokens.Radius.Small,
            ImDrawFlags.RoundCornersAll,
            Tokens.Line(1f));

        if (!value)
        {
            return;
        }

        // Two strokes rather than a glyph, so it never depends on the game font having one.
        float thickness = Tokens.Line(2f);
        uint ink = Tokens.Col.Faded(Tokens.Col.InkOnGold, alpha);
        dl.AddLine(
            new Vector2(min.X + (box * 0.24f), min.Y + (box * 0.52f)),
            new Vector2(min.X + (box * 0.44f), min.Y + (box * 0.72f)),
            ink,
            thickness);
        dl.AddLine(
            new Vector2(min.X + (box * 0.44f), min.Y + (box * 0.72f)),
            new Vector2(min.X + (box * 0.78f), min.Y + (box * 0.28f)),
            ink,
            thickness);
    }

    /// <summary>What a compact option puts at the right edge of its row.</summary>
    internal enum OptionControl
    {
        Tick,
        Switch,
    }

    /// <summary>
    /// A compact option: the label on the left, the control hard against the right edge of
    /// the column. The whole row is the hit box, so it can be clicked anywhere along it.
    /// <para>
    /// That right edge is the point. A tick box parked next to its label leaves the column
    /// with nothing to align to, and a screen full of them reads as scattered controls rather
    /// than as a list of settings — the value of a slider, a switch and a tick all end on the
    /// same line instead.
    /// </para>
    /// </summary>
    /// <param name="divider">
    /// Draws a faint line in the gap ABOVE the row — pass it on every option of a run except
    /// the first, whose top line is the group's own head rule.
    /// <para>
    /// Above rather than below is LumenUI's rule, and it is the one that composes: a line
    /// under a row has to be suppressed on the last one, and a subheading dropped into the
    /// middle of a run would need its own special case. Belonging to the row beneath it, the
    /// line simply never appears where a run begins.
    /// </para>
    /// </param>
    public static bool OptionRow(
        string id,
        string label,
        float x,
        float y,
        float width,
        bool value,
        OptionControl control = OptionControl.Tick,
        string? tooltip = null,
        bool enabled = true,
        bool divider = false)
    {
        // The same row every other setting gets: label left, one control against the right
        // edge, one height. The whole row is the hit box.
        float height = RowHeight();

        ImGui.SetCursorScreenPos(new Vector2(x, y));
        ImGui.InvisibleButton(id, new Vector2(width, height));
        bool hovered = ImGui.IsItemHovered() && enabled;
        bool clicked = ImGui.IsItemClicked() && enabled;

        float alpha = enabled ? 1f : Tokens.Col.DisabledAlpha;
        ImDrawListPtr dl = ImGui.GetWindowDrawList();

        Ink.Draw(
            dl,
            Ink.Role.Body,
            new Vector2(x, CenterY(y, height, Ink.Role.Body)),
            Tokens.Col.Faded(Tokens.Col.Ink, alpha),
            label);

        if (control == OptionControl.Switch)
        {
            float switchWidth = Tokens.Metric.SwitchWidth;
            PaintSwitch(
                dl,
                MathF.Round(x + width - switchWidth),
                MathF.Round(y + ((height - Tokens.Metric.SwitchHeight) * 0.5f)),
                value,
                alpha);
        }
        else
        {
            float box = Tokens.Metric.CheckBox;
            PaintTick(
                dl,
                new Vector2(MathF.Round(x + width - box), MathF.Round(y + ((height - box) * 0.5f))),
                value,
                hovered,
                alpha);
        }

        if (divider)
        {
            // Centred in the air above the row, so it sits between two rows, not on either.
            Hairline(dl, x, x + width, MathF.Round(y - (Tokens.Metric.RowGap * 0.5f)), Tokens.Col.RowDivider);
        }

        if (tooltip is not null)
        {
            TooltipOnHover(tooltip);
        }

        return clicked;
    }

    /// <summary>
    /// The head of a settings section: a title and one line saying what it covers.
    /// Returns the height it used, the gap down to the first row included.
    /// <para>
    /// The description is set in the smallest role rather than in body copy. At body size it
    /// was as loud as a control label, which left the head reading as another row instead of
    /// as the thing the rows below belong to.
    /// </para>
    /// </summary>
    public static float SectionHeader(string title, string description, float x, float y)
    {
        ImDrawListPtr dl = ImGui.GetWindowDrawList();
        Ink.Draw(dl, Ink.Role.Title, new Vector2(x, y), Tokens.Col.Heading, title);

        float used = Ink.LineHeight(Ink.Role.Title) + Tokens.Space.Xs;
        Ink.Draw(dl, Ink.Role.Small, new Vector2(x, MathF.Round(y + used)), Tokens.Col.InkFaint, description);

        return used + Ink.LineHeight(Ink.Role.Small) + Tokens.Metric.SectionHeadGap;
    }

    /// <summary>What goes in the head of a group, beyond its name.</summary>
    internal readonly struct GroupHead
    {
        public string Title { get; init; }

        /// <summary>One line saying what the group covers. May be empty.</summary>
        public string Description { get; init; }

        /// <summary>An on/off switch at the right of the head, or null for a group that is always on.</summary>
        public bool? Toggle { get; init; }

        /// <summary>A button at the right of the head — "Defaults", say. Null for none.</summary>
        public string? Action { get; init; }

        /// <summary>A small count beside the head, the way a list says how long it is.</summary>
        public string? Badge { get; init; }

        public bool Collapsible { get; init; }

        public bool Collapsed { get; init; }
    }

    /// <summary>What a group reports back, and where its rows go.</summary>
    internal readonly struct GroupScope
    {
        public readonly float X;
        public readonly float Y;
        public readonly float Width;

        /// <summary>Where the first row goes, and how wide the rows are.</summary>
        public readonly float ContentX;

        public readonly float ContentY;
        public readonly float ContentWidth;

        /// <summary>False while the group's switch is off: rows are inert and veiled.</summary>
        public readonly bool Enabled;

        /// <summary>True while the group is folded away — the caller draws no rows at all.</summary>
        public readonly bool Collapsed;

        public readonly bool ToggleClicked;
        public readonly bool ActionClicked;
        public readonly bool CollapseClicked;

        public GroupScope(
            float x,
            float y,
            float width,
            float contentX,
            float contentY,
            float contentWidth,
            bool enabled,
            bool collapsed,
            bool toggleClicked,
            bool actionClicked,
            bool collapseClicked)
        {
            this.X = x;
            this.Y = y;
            this.Width = width;
            this.ContentX = contentX;
            this.ContentY = contentY;
            this.ContentWidth = contentWidth;
            this.Enabled = enabled;
            this.Collapsed = collapsed;
            this.ToggleClicked = toggleClicked;
            this.ActionClicked = actionClicked;
            this.CollapseClicked = collapseClicked;
        }
    }

    /// <summary>
    /// Opens a settings group: a framed block with a head of its own. Always paired with
    /// <see cref="EndGroup"/>, which draws the frame once the rows have said how tall they are.
    /// <para>
    /// The frame is the hairline in a second shape — as a stroke it separates two sections, as
    /// a rectangle it gathers one. That keeps the window at the two kinds of divider it has
    /// always had, and it needs no surface colour of its own, which matters because the
    /// measured palette has exactly one surface: the game separates with lines, not shades.
    /// </para>
    /// <para>
    /// A group is what a per-group switch, a per-group "Defaults" and a fold-away hang on.
    /// None of those had anywhere to live while a section was just a heading and some space.
    /// </para>
    /// </summary>
    public static GroupScope BeginGroup(string id, in GroupHead head, float x, float y, float width)
    {
        // Pushed so the head's parts can use fixed ids instead of building one per frame.
        ImGui.PushID(id);

        float pad = Tokens.Metric.GroupPadding;
        float left = x + pad;
        float right = x + width - pad;
        float top = y + pad;
        ImDrawListPtr dl = ImGui.GetWindowDrawList();

        bool enabled = head.Toggle ?? true;
        float alpha = enabled ? 1f : Tokens.Col.DisabledAlpha;

        // --- right of the head, laid out from the edge inwards ---
        float cursor = right;
        bool toggleClicked = false;
        if (head.Toggle is not null)
        {
            cursor -= Tokens.Metric.SwitchWidth;
            toggleClicked = Switch(IdGroupToggle, cursor, top, head.Toggle.Value, true);
            cursor -= Tokens.Space.Md;
        }

        bool collapseClicked = false;
        if (head.Collapsible)
        {
            float size = Tokens.Metric.CollapseGlyph;
            cursor -= size;
            collapseClicked = CollapseArrow(dl, IdGroupCollapse, cursor, top, size, head.Collapsed);
            cursor -= Tokens.Space.Md;
        }

        if (head.Badge is not null)
        {
            Vector2 text = Ink.Measure(Ink.Role.Small, head.Badge);
            float badgeHeight = Tokens.Metric.BadgeHeight;
            float badgeWidth = text.X + (Tokens.Metric.BadgePaddingX * 2f);
            cursor -= badgeWidth;
            Vector2 min = new(MathF.Round(cursor), MathF.Round(top + ((Ink.LineHeight(Ink.Role.Title) - badgeHeight) * 0.5f)));
            dl.AddRect(
                min,
                new Vector2(min.X + badgeWidth, min.Y + badgeHeight),
                Tokens.Col.Faded(Tokens.Col.EdgeDim, alpha),
                Tokens.Radius.Small,
                ImDrawFlags.RoundCornersAll,
                Tokens.Line(1f));
            Ink.Draw(
                dl,
                Ink.Role.Small,
                new Vector2(min.X + Tokens.Metric.BadgePaddingX, MathF.Round(min.Y + ((badgeHeight - text.Y) * 0.5f))),
                Tokens.Col.Faded(Tokens.Col.InkFaint, alpha),
                head.Badge);
            cursor -= Tokens.Space.Md;
        }

        bool actionClicked = false;
        if (head.Action is not null)
        {
            cursor -= MeasureButton(head.Action);
            actionClicked = Button(
                IdGroupAction,
                head.Action,
                cursor,
                MathF.Round(top + ((Ink.LineHeight(Ink.Role.Title) - Tokens.Metric.ButtonHeight) * 0.5f)),
                enabled);
        }

        // --- the name, and the line under it ---
        Ink.Draw(dl, Ink.Role.Title, new Vector2(left, top), Tokens.Col.Faded(Tokens.Col.Heading, alpha), head.Title);
        float headHeight = Ink.LineHeight(Ink.Role.Title);

        if (head.Description.Length > 0)
        {
            float descY = MathF.Round(top + headHeight + Tokens.Space.Xs);
            Ink.Draw(dl, Ink.Role.Small, new Vector2(left, descY), Tokens.Col.Faded(Tokens.Col.InkFaint, alpha), head.Description);
            headHeight += Tokens.Space.Xs + Ink.LineHeight(Ink.Role.Small);
        }

        float contentY = top + headHeight;
        if (!head.Collapsed)
        {
            // Held back from the frame on both sides. A rule that runs into the border cuts
            // the group into two stacked boxes; inset, it divides one object.
            float ruleY = MathF.Round(contentY + Tokens.Metric.GroupHeadGap);
            Hairline(dl, left, right, ruleY, Tokens.Col.Faded(Tokens.Col.Hairline, alpha));
            contentY = ruleY + Tokens.Line(1f) + Tokens.Metric.GroupRuleGap;
        }

        // Blocks input to everything the caller draws next. The veil that goes with it is
        // drawn in EndGroup, once the rows have been laid out.
        if (!enabled && !head.Collapsed)
        {
            ImGui.BeginDisabled();
        }

        return new GroupScope(
            x,
            y,
            width,
            left,
            contentY,
            right - left,
            enabled,
            head.Collapsed,
            toggleClicked,
            actionClicked,
            collapseClicked);
    }

    /// <summary>
    /// Closes a group and draws its frame. Takes the height the rows turned out to be, which
    /// is why the frame is drawn last: a group is exactly as tall as what is in it.
    /// Returns the whole height, so the screen can move on.
    /// </summary>
    public static float EndGroup(in GroupScope scope, float contentHeight)
    {
        EndGroupContent(scope, contentHeight);
        return GroupFrame(scope, contentHeight);
    }

    /// <summary>
    /// Closes what a group contains without drawing its frame: lifts the disabled block and
    /// the id, and veils the rows if the group is off. Split out from <see cref="EndGroup"/>
    /// for the side-by-side case, where neither frame can be drawn until both columns have
    /// said how tall they are — see <see cref="GroupFrameRow"/>.
    /// </summary>
    public static void EndGroupContent(in GroupScope scope, float contentHeight)
    {
        if (!scope.Enabled && !scope.Collapsed)
        {
            ImGui.EndDisabled();

            // Veiled by painting the surface back over the rows at part strength. Cheaper and
            // more honest than dimming every colour on the way out: what is there stays
            // readable, it just stops asking for attention.
            ImGui.GetWindowDrawList().AddRectFilled(
                new Vector2(scope.ContentX, scope.ContentY),
                new Vector2(scope.ContentX + scope.ContentWidth, scope.ContentY + contentHeight),
                Tokens.Col.Faded(Tokens.Col.GroupBg, 1f - Tokens.Col.DisabledAlpha));
        }

        ImGui.PopID();
    }

    /// <summary>
    /// One row of a settings group: the label on the left, one control hard against the right
    /// edge. EVERY row is built this way — a dropdown, a slider and a tick all take the same
    /// height, so nothing in one column can come out level with a gap in another.
    /// <para>
    /// This is FFXIV's own row, and it is what the two-line version (label above control) cost
    /// us: with two row heights in play, a compact row could be aligned to a field's label, to
    /// its control, or to the middle of its step, and each of the three left the other two
    /// looking wrong. One height, one question, no answer needed.
    /// </para>
    /// </summary>
    public static float RowHeight() => Tokens.Metric.FieldControlHeight;

    /// <summary>Row to row, the height plus the air between two of them.</summary>
    public static float RowPitch() => RowHeight() + Tokens.Metric.RowGap;

    /// <summary>
    /// How wide every control is, whatever it is. Taken as a fixed column off the right edge
    /// rather than as a share of the row, so controls line up down the screen AND across the
    /// two columns; the label takes what is left.
    /// </summary>
    public static float ControlWidth() => Tokens.Metric.ControlWidth;

    /// <summary>Where a row's control starts — the right-hand column of the row.</summary>
    public static float ControlX(float rowX, float rowWidth) => rowX + rowWidth - ControlWidth();

    /// <summary>
    /// Draws a row's divider and its label, and hands back where the control goes. The divider
    /// is centred in the air above the row, so it sits between two rows rather than on either.
    /// </summary>
    public static float Row(string label, float x, float y, float width, bool divider, string? hint = null)
    {
        ImDrawListPtr dl = ImGui.GetWindowDrawList();

        if (divider)
        {
            Hairline(dl, x, x + width, MathF.Round(y - (Tokens.Metric.RowGap * 0.5f)), Tokens.Col.RowDivider);
        }

        float labelY = CenterY(y, RowHeight(), Ink.Role.Body);
        Ink.Draw(dl, Ink.Role.Body, new Vector2(x, labelY), Tokens.Col.Ink, label);

        if (hint is not null)
        {
            // On the label's baseline, in the smallest role, and dropped rather than crowded
            // if the control column has taken the room.
            float hintX = MathF.Round(x + Ink.Measure(Ink.Role.Body, label).X + Tokens.Space.Md);
            float hintY = MathF.Round(labelY + Ink.LineHeight(Ink.Role.Body) - Ink.LineHeight(Ink.Role.Small));
            if (hintX + Ink.Measure(Ink.Role.Small, hint).X + Tokens.Space.Md <= ControlX(x, width))
            {
                Ink.Draw(dl, Ink.Role.Small, new Vector2(hintX, hintY), Tokens.Col.InkFaint, hint);
            }
        }

        return ControlX(x, width);
    }

    /// <summary>
    /// Opens a row of groups. Called before the first <see cref="BeginGroup"/> of the row,
    /// because a group's surface has to go down before its rows and the height is only known
    /// afterwards: the draw list is split in two, the rows go on the upper channel, and the
    /// surfaces are painted onto the lower one when the row is framed.
    /// </summary>
    public static void BeginGroupRow()
    {
        ImDrawListPtr list = ImGui.GetWindowDrawList();
        list.ChannelsSplit(2);
        list.ChannelsSetCurrent(1);
        s_rowSplit = true;
    }

    /// <summary>
    /// Draws one group's surface and frame around the height its rows turned out to be, and
    /// returns the whole height so the screen can move on.
    /// </summary>
    public static float GroupFrame(in GroupScope scope, float contentHeight)
    {
        float bottom = GroupBottom(scope, contentHeight);
        ImDrawListPtr dl = BeginBacks();
        Surface(dl, scope, bottom);
        EndBacks(dl);
        return FrameTo(scope, bottom);
    }

    /// <summary>
    /// Frames two groups that sit side by side, both down to the lower of the two bottom
    /// edges. Two columns of different length would otherwise end in a step, and every group
    /// added later would add another one; a shared bottom edge keeps the row one object.
    /// A folded-away group keeps its own small height — stretching it would undo the fold.
    /// </summary>
    public static float GroupFrameRow(in GroupScope left, float leftHeight, in GroupScope right, float rightHeight)
    {
        float bottom = MathF.Max(GroupBottom(left, leftHeight), GroupBottom(right, rightHeight));
        float leftBottom = left.Collapsed ? GroupBottom(left, leftHeight) : bottom;
        float rightBottom = right.Collapsed ? GroupBottom(right, rightHeight) : bottom;

        ImDrawListPtr dl = BeginBacks();
        Surface(dl, left, leftBottom);
        Surface(dl, right, rightBottom);
        EndBacks(dl);

        return MathF.Max(FrameTo(left, leftBottom), FrameTo(right, rightBottom));
    }

    /// <summary>Switches to the channel the group surfaces are painted on, if the row split it.</summary>
    private static ImDrawListPtr BeginBacks()
    {
        ImDrawListPtr list = ImGui.GetWindowDrawList();
        if (s_rowSplit)
        {
            list.ChannelsSetCurrent(0);
        }

        return list;
    }

    private static void EndBacks(ImDrawListPtr list)
    {
        if (!s_rowSplit)
        {
            return;
        }

        list.ChannelsMerge();
        s_rowSplit = false;
    }

    private static void Surface(ImDrawListPtr dl, in GroupScope scope, float bottom) => dl.AddRectFilled(
        new Vector2(scope.X, scope.Y),
        new Vector2(scope.X + scope.Width, bottom),
        Tokens.Col.GroupBg,
        Tokens.Radius.Group,
        ImDrawFlags.RoundCornersAll);

    private static float GroupBottom(in GroupScope scope, float contentHeight) => scope.Collapsed
        ? scope.ContentY + Tokens.Metric.GroupPadding
        : scope.ContentY + contentHeight + Tokens.Metric.GroupPadding;

    /// <summary>
    /// Drawn after the rows on purpose: it is an outline with nothing behind it, so covering
    /// the content is impossible, and this way it can wrap whatever height came out.
    /// </summary>
    private static float FrameTo(in GroupScope scope, float bottom)
    {
        ImGui.GetWindowDrawList().AddRect(
            new Vector2(scope.X, scope.Y),
            new Vector2(scope.X + scope.Width, bottom),
            Tokens.Col.Hairline,
            Tokens.Radius.Group,
            ImDrawFlags.RoundCornersAll,
            Tokens.Line(1f));

        return bottom - scope.Y;
    }

    /// <summary>The fold-away arrow in a group head, drawn as a triangle rather than typed.</summary>
    private static bool CollapseArrow(ImDrawListPtr dl, string id, float x, float y, float size, bool collapsed)
    {
        float line = Ink.LineHeight(Ink.Role.Title);
        ImGui.SetCursorScreenPos(new Vector2(x, y));
        ImGui.InvisibleButton(id, new Vector2(size, line));
        bool hovered = ImGui.IsItemHovered();
        bool clicked = ImGui.IsItemClicked();

        uint ink = hovered ? Tokens.Col.GoldHi : Tokens.Col.InkDim;
        float cx = MathF.Round(x + (size * 0.5f));
        float cy = MathF.Round(y + (line * 0.5f));
        float half = MathF.Round(size * 0.35f);

        if (collapsed)
        {
            dl.AddTriangleFilled(
                new Vector2(cx + half, cy),
                new Vector2(cx - half, cy - half),
                new Vector2(cx - half, cy + half),
                ink);
        }
        else
        {
            dl.AddTriangleFilled(
                new Vector2(cx, cy + half),
                new Vector2(cx - half, cy - half),
                new Vector2(cx + half, cy - half),
                ink);
        }

        return clicked;
    }

    /// <summary>
    /// The width of one column of the settings grid. Controls are sized to this, never to
    /// the full content width.
    /// </summary>
    public static float ColumnWidth(float contentWidth)
    {
        float gutters = Tokens.Metric.ColumnGutter * (Tokens.Metric.Columns - 1);
        return MathF.Floor((contentWidth - gutters) / Tokens.Metric.Columns);
    }

    /// <summary>The left edge of the given column, counted from zero.</summary>
    public static float ColumnX(float contentLeft, float contentWidth, int column) =>
        contentLeft + (column * (ColumnWidth(contentWidth) + Tokens.Metric.ColumnGutter));

    /// <summary>
    /// How tall a check box row is. The caller advances by this rather than by a row token of
    /// its own, so the gap under a check box is the same gap as under everything else.
    /// </summary>
    public static float CheckBoxHeight() =>
        MathF.Max(Tokens.Metric.CheckBox, Ink.LineHeight(Ink.Role.Body));

    /// <summary>A check box with its label to the right of it, the way the game writes its own.</summary>
    /// <param name="enabled">
    /// A disabled box is still drawn and still answers a hover — the paste panel uses this to
    /// show a part the target element does not have, rather than leaving it off the list and
    /// letting the user wonder where it went.
    /// </param>
    public static bool CheckBox(
        string id,
        string label,
        float x,
        float y,
        bool value,
        string? tooltip = null,
        bool enabled = true)
    {
        float box = Tokens.Metric.CheckBox;
        float labelWidth = Ink.Measure(Ink.Role.Body, label).X;
        float height = CheckBoxHeight();
        float width = box + Tokens.Space.Md + labelWidth;

        ImGui.SetCursorScreenPos(new Vector2(x, y));
        ImGui.InvisibleButton(id, new Vector2(width, height));
        bool hovered = ImGui.IsItemHovered() && enabled;
        bool clicked = ImGui.IsItemClicked() && enabled;

        Vector2 min = new(x, MathF.Round(y + ((height - box) * 0.5f)));
        Vector2 max = new(min.X + box, min.Y + box);

        float alpha = enabled ? 1f : Tokens.Col.DisabledAlpha;
        ImDrawListPtr dl = ImGui.GetWindowDrawList();
        PaintTick(dl, min, value, hovered, alpha);

        Ink.Draw(
            dl,
            Ink.Role.Body,
            new Vector2(max.X + Tokens.Space.Md, CenterY(y, height, Ink.Role.Body)),
            Tokens.Col.Faded(hovered ? Tokens.Col.Ink : Tokens.Col.InkDim, alpha),
            label);

        if (tooltip is not null)
        {
            TooltipOnHover(tooltip);
        }

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

        /// <summary>How tall the whole control turned out, caption row included.</summary>
        public readonly float Height;

        public SliderResult(float value, bool changed, bool released, float height)
        {
            this.Value = value;
            this.Changed = changed;
            this.Released = released;
            this.Height = height;
        }
    }

    /// <summary>
    /// A slider laid out over two rows: the label top left, the value top right, and the
    /// track across the width of its column beneath them. The track fills up to the grab, the
    /// way the game's own sliders do, and the grab is a circle.
    /// <para>
    /// Moving and releasing are reported separately, because the rule is that a change shows
    /// at once but is written on release.
    /// </para>
    /// </summary>
    /// <param name="hint">A few words beside the label, for what a label alone cannot say.</param>
    /// <param name="tooltip">The longer explanation, which belongs here and not in the flow.</param>
    public static SliderResult Slider(
        string id,
        string label,
        string valueText,
        float x,
        float y,
        float width,
        float value,
        float min,
        float max,
        string? hint = null,
        string? tooltip = null,
        bool divider = false,
        float step = 0f)
    {
        ImDrawListPtr dl = ImGui.GetWindowDrawList();

        // The row's own label and note, and the value at the very right — the number belongs
        // to the reader, so it keeps the edge and the track gives up the room for it.
        float valueWidth = Tokens.Metric.ValueWidth;
        float rowX = x;
        float rowWidth = width;
        Row(label, rowX, y, rowWidth, divider, hint);

        float valueX = MathF.Round(rowX + rowWidth - Ink.Measure(Ink.Role.Body, valueText).X);
        Ink.Draw(dl, Ink.Role.Body, new Vector2(valueX, CenterY(y, RowHeight(), Ink.Role.Body)), Tokens.Col.GoldHi, valueText);

        // The track sits in the control column, less the room the value took.
        x = ControlX(rowX, rowWidth);
        width = ControlWidth() - valueWidth - Tokens.Space.Md;

        float boxTop = y;
        float boxHeight = RowHeight();
        float rowHeight = Tokens.Metric.SliderHeight;
        float trackRowTop = MathF.Round(boxTop + ((boxHeight - rowHeight) * 0.5f));
        float radius = Tokens.Metric.SliderGrabRadius;

        // The hit box is the whole cell, not just the track: an easier grab, and it costs
        // nothing, since the value only ever comes from the horizontal position.
        ImGui.SetCursorScreenPos(new Vector2(x, boxTop));
        ImGui.InvisibleButton(id, new Vector2(width, boxHeight));
        bool active = ImGui.IsItemActive();
        bool hovered = ImGui.IsItemHovered();
        bool released = ImGui.IsItemDeactivated();

        if (tooltip is not null && !active)
        {
            TooltipOnHover(tooltip);
        }

        // The grab is a circle, so its centre travels between one radius from each end.
        float travel = width - (radius * 2f);
        float result = value;
        bool changed = false;
        if (active && travel > 0f)
        {
            result = Drag(x, radius, travel, value, min, max, step, ImGui.IsItemActivated());
            changed = result != value;
        }

        if (released)
        {
            s_dragging = false;
        }

        float fraction = max > min ? Math.Clamp((result - min) / (max - min), 0f, 1f) : 0f;
        float grabCenterX = MathF.Round(x + radius + (fraction * travel));

        float trackHeight = Tokens.Metric.SliderTrack;
        float trackTop = MathF.Round(trackRowTop + ((rowHeight - trackHeight) * 0.5f));
        Vector2 trackMin = new(x, trackTop);
        Vector2 trackMax = new(x + width, trackTop + trackHeight);
        float trackRadius = trackHeight * 0.5f;

        dl.AddRectFilled(trackMin, trackMax, Tokens.Col.SliderTrackBg, trackRadius);
        if (fraction > 0f)
        {
            dl.PushClipRect(trackMin, new Vector2(grabCenterX, trackMax.Y), true);
            dl.AddRectFilled(
                trackMin,
                trackMax,
                active || hovered ? Tokens.Col.SliderFillHi : Tokens.Col.SliderFill,
                trackRadius);
            dl.PopClipRect();
        }

        // No outline on the track. The rounded ends are the shape; a border around them only
        // made the bar look boxed in.
        Vector2 grabCenter = new(grabCenterX, MathF.Round(trackTop + (trackHeight * 0.5f)));
        MilledKnob(dl, grabCenter, radius, active || hovered ? 0.16f : 0f);

        return new SliderResult(result, changed, released, RowHeight());
    }

    /// <summary>
    /// What the slider is worth after this frame's mouse movement.
    /// <para>
    /// The knob stays under the cursor. That is not a nicety — a knob that lags behind the
    /// hand reads as a broken control, whatever it is doing underneath (Florian, 2026-09-12).
    /// So the value comes from where the mouse is on the track, and it lands on whole steps,
    /// which is what makes a size settle on a pixel instead of between two.
    /// </para>
    /// <para>
    /// Where that is not enough is a range wider than the track has pixels: 90 to 400 across
    /// 162 pixels puts nearly two sizes behind every one of them, and the values in between
    /// cannot be reached by pointing at all. Holding shift drags at a quarter speed to reach
    /// them, and a drag that has gone fine stays measured from where it was rather than
    /// snapping back to the cursor when shift is let go.
    /// </para>
    /// <para>
    /// Every other slider in the suite has a range narrower than the track, so pointing alone
    /// already reaches every step and shift is never needed.
    /// </para>
    /// </summary>
    /// <param name="step">The smallest move the value may make, or zero for a smooth one.</param>
    private static float Drag(
        float x,
        float radius,
        float travel,
        float value,
        float min,
        float max,
        float step,
        bool activated)
    {
        float mouseX = ImGui.GetIO().MousePos.X;
        bool fine = ImGui.GetIO().KeyShift;
        float range = max - min;

        if (activated)
        {
            s_dragging = true;
            s_dragFine = fine;

            // Shift held before the press starts the drag off fine straight away; otherwise
            // the knob simply follows the mouse, which is what a slider is.
            s_dragRelative = fine;
            s_dragValue = value;
            s_dragMouseX = mouseX;
        }
        else if (!s_dragging)
        {
            return value;
        }
        else if (fine != s_dragFine)
        {
            // Shift taken or let go mid-drag. Measuring starts again from here, and once a
            // drag has gone relative it stays relative until the button comes up: switching
            // back would snap the knob to the cursor, and a jump is worse than an offset.
            s_dragFine = fine;
            s_dragRelative = true;
            s_dragValue = value;
            s_dragMouseX = mouseX;
        }

        float result;

        if (!s_dragRelative)
        {
            float t = Math.Clamp((mouseX - x - radius) / travel, 0f, 1f);
            result = min + (t * range);
        }
        else
        {
            float unitsPerPixel = range / travel * (fine ? FineDragFactor : 1f);
            result = s_dragValue + ((mouseX - s_dragMouseX) * unitsPerPixel);
        }

        if (step > 0f)
        {
            result = min + (MathF.Round((result - min) / step) * step);
        }

        return Math.Clamp(result, min, max);
    }

    /// <summary>
    /// The slider knob: a milled metal disc, bright at the rim, darker towards the middle,
    /// with radial grooves across the face. Built from wedges rather than a gradient, because
    /// a draw list has no radial fill and the grooves are what make it read as metal at all.
    /// <paramref name="lift"/> raises every tone towards white while the knob is in hand.
    /// </summary>
    private static void MilledKnob(ImDrawListPtr dl, Vector2 centre, float radius, float lift)
    {
        // Fine enough that the wedges disappear into a sheen rather than reading as slices.
        const int Wedges = 32;

        // Four bright quarters, the pattern a spun disc shows under one light. Turned an
        // eighth so the bright ones sit on the diagonals — square-on they read as a cross.
        const float Lobes = 4f;
        const float Phase = MathF.PI * 0.25f;

        uint light = Tokens.Col.Mix(Tokens.Col.SliderGrab, White, lift);
        uint dark = Tokens.Col.Mix(Tokens.Col.SliderGrabMill, White, lift);

        // A round silhouette under the wedges: thirty-two straight chords make a polygon, and
        // its flat sides showed at the rim. The wedges stop a hair short of this circle, so
        // what defines the outline is the circle's own antialiased edge.
        dl.AddCircleFilled(centre, radius, dark);

        float step = MathF.PI * 2f / Wedges;
        radius -= Tokens.Line(1f);
        Vector2 previous = new(centre.X + radius, centre.Y);

        for (int i = 1; i <= Wedges; i++)
        {
            float angle = i * step;
            Vector2 point = new(
                centre.X + (MathF.Cos(angle) * radius),
                centre.Y + (MathF.Sin(angle) * radius));

            float sheen = 0.5f + (0.5f * MathF.Cos(((angle - (step * 0.5f)) * Lobes) + Phase));
            dl.AddTriangleFilled(centre, previous, point, Tokens.Col.Mix(dark, light, sheen));
            previous = point;
        }

        // Where the grind converges. No drawn rim: at sixteen pixels across, a ring dark
        // enough to see reads as an outline round a sticker rather than as the edge of a
        // disc — the dark tone of the grind at the silhouette does that job on its own.
        dl.AddCircleFilled(centre, radius * 0.18f, Tokens.Col.Mix(Tokens.Col.SliderGrabCore, White, lift));
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
