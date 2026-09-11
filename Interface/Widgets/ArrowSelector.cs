using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using WispUI.Core;
using WispUI.Localization;
using WispUI.Style;

namespace WispUI.Interface.Widgets;

/// <summary>Draws an item's live preview into the face of a selector, or into a popup row.</summary>
internal delegate void ArrowPreview<in T>(ImDrawListPtr dl, T item, Vector2 min, Vector2 max);

/// <summary>
/// How one selector behaves. A settings object rather than a row of boolean parameters,
/// so a call site reads as a description of the control instead of a puzzle.
/// </summary>
internal sealed class ArrowSelectorOptions<T>
{
    /// <summary>
    /// The name of an item. It must return a string that already exists — a formatter that
    /// builds one would allocate on every frame of every selector on screen.
    /// </summary>
    public required Func<T, string> Label { get; init; }

    /// <summary>
    /// Paints the item itself into the face. Where a choice has a visual result — a bar
    /// fill, a font, a shape — this preview is the actual point of the control, and the
    /// name beside it is only the label for what you already see.
    /// </summary>
    public ArrowPreview<T>? DrawPreview { get; init; }

    /// <summary>Clicking the face opens the full list. Worth it for long lists, noise for three entries.</summary>
    public bool EnablePopupList { get; init; }

    /// <summary>A search box above that list. Independent of the list itself.</summary>
    public bool EnableSearch { get; init; }

    /// <summary>The "2 / 9" on the right, which says where in the list you are without opening it.</summary>
    public bool ShowCounter { get; init; } = true;

    /// <summary>
    /// Off by default: an arrow that stops, greyed out, tells you where you are in the list,
    /// which is worth more than saving a few clicks on the way round.
    /// </summary>
    public bool WrapAround { get; init; }
}

/// <summary>
/// The one selection control in the suite: <c>◀ preview + name ▶</c>, with the full list a
/// click away on the face. Bar textures, fonts, shapes, fill directions and colour schemes
/// all use this same class — a second selection widget would be a rule break, not a style
/// choice, because an improvement here has to reach every one of them.
/// <para>
/// One instance per control, held by the screen. Ids and the counter caption are built in
/// the constructor and reused, so drawing allocates nothing; the popup, which only exists
/// while it is open, is the one place that builds strings.
/// </para>
/// </summary>
internal sealed class ArrowSelector<T>
{
    private readonly IReadOnlyList<T> m_items;
    private readonly ArrowSelectorOptions<T> m_options;

    private readonly string m_idPrev;
    private readonly string m_idNext;
    private readonly string m_idFace;
    private readonly string m_idPopup;
    private readonly string m_idSearch;
    private readonly string m_idList;
    private readonly string m_idRow;

    // "3 / 9" is rebuilt only when it actually reads differently.
    private string m_counter = string.Empty;
    private int m_counterFor = -1;

    private bool m_open;
    private bool m_focusSearch;
    private string m_query = string.Empty;

    public ArrowSelector(string id, IReadOnlyList<T> items, ArrowSelectorOptions<T> options)
    {
        m_items = items;
        m_options = options;

        // Built once. The ids have to be stable and unique, and string work in the draw path
        // is exactly what the performance rules forbid.
        m_idPrev = id + "-prev";
        m_idNext = id + "-next";
        m_idFace = id + "-face";
        m_idPopup = id + "-pop";
        m_idSearch = id + "-search";
        m_idList = id + "-list";
        m_idRow = id + "-row";
    }

    /// <summary>How tall the control is. The caller advances by this plus a rhythm token.</summary>
    public static float Height => Tokens.Metric.SelectorHeight;

    /// <summary>
    /// Draws the control and reports whether the selection changed this frame. A change is
    /// live at once; writing it to disk is the caller's job, and is debounced.
    /// </summary>
    public bool Draw(ref int index, float x, float y, float width)
    {
        float height = Tokens.Metric.SelectorHeight;
        float arrow = Tokens.Metric.SelectorArrow;
        ImDrawListPtr dl = ImGui.GetWindowDrawList();

        int count = m_items.Count;
        if (count == 0)
        {
            DrawEmpty(dl, x, y, width, height);
            return false;
        }

        // A stored index can outlive the list it pointed into — a texture removed, a list
        // shortened by an update. Fall back to the first entry and say so, rather than throw
        // in the draw path.
        if (index < 0 || index >= count)
        {
            Services.Log.Warning($"Selector {m_idFace} had index {index} for {count} items; fell back to the first.");
            index = 0;
        }

        bool wrap = m_options.WrapAround;
        bool atStart = !wrap && index == 0;
        bool atEnd = !wrap && index == count - 1;

        bool changed = false;
        if (this.Arrow(dl, m_idPrev, x, y, arrow, height, true, !atStart, Strings.SelectorAtStart))
        {
            index = Step(index, -1, count, wrap);
            changed = true;
        }

        if (this.Arrow(dl, m_idNext, x + width - arrow, y, arrow, height, false, !atEnd, Strings.SelectorAtEnd))
        {
            index = Step(index, 1, count, wrap);
            changed = true;
        }

        bool faceClicked = this.DrawFace(dl, index, count, x + arrow, y, width - (arrow * 2f), height, out bool focused);

        // The whole group is outlined as one object, with a hairline where the arrows meet
        // the face. Three separate boxes with gaps between them would be three targets to
        // hit instead of one strip to click along.
        Vector2 groupMin = new(x, y);
        Vector2 groupMax = new(x + width, y + height);
        float line = Tokens.Line(1f);
        dl.AddRect(groupMin, groupMax, Tokens.Col.ControlEdge, Tokens.Radius.Control, ImDrawFlags.RoundCornersAll, line);
        dl.AddRectFilled(new Vector2(x + arrow - line, y), new Vector2(x + arrow, y + height), Tokens.Col.ControlEdge);
        dl.AddRectFilled(new Vector2(x + width - arrow, y), new Vector2(x + width - arrow + line, y + height), Tokens.Col.ControlEdge);

        // The arrow keys do what the arrow buttons do, as long as the control has the focus.
        if (focused)
        {
            if (ImGui.IsKeyPressed(ImGuiKey.LeftArrow) && !atStart)
            {
                index = Step(index, -1, count, wrap);
                changed = true;
            }
            else if (ImGui.IsKeyPressed(ImGuiKey.RightArrow) && !atEnd)
            {
                index = Step(index, 1, count, wrap);
                changed = true;
            }
        }

        if (faceClicked && m_options.EnablePopupList)
        {
            m_open = true;
            m_focusSearch = m_options.EnableSearch;
            m_query = string.Empty;
            ImGui.OpenPopup(m_idPopup);
        }

        if (m_open)
        {
            changed |= this.DrawPopup(ref index, x, y + height + Tokens.Metric.PopupGap, width);
        }

        return changed;
    }

    private static int Step(int index, int by, int count, bool wrap) =>
        wrap ? ((index + by) % count + count) % count : Math.Clamp(index + by, 0, count - 1);

    /// <summary>
    /// An empty list draws a disabled control and says so. The list being empty is a state,
    /// not a failure, and it must never take the window down with it.
    /// </summary>
    private static void DrawEmpty(ImDrawListPtr dl, float x, float y, float width, float height)
    {
        Vector2 min = new(x, y);
        Vector2 max = new(x + width, y + height);
        float alpha = Tokens.Col.DisabledAlpha;

        dl.AddRectFilled(min, max, Tokens.Col.Faded(Tokens.Col.Input, alpha), Tokens.Radius.Control);
        dl.AddRect(
            min,
            max,
            Tokens.Col.Faded(Tokens.Col.ControlEdge, alpha),
            Tokens.Radius.Control,
            ImDrawFlags.RoundCornersAll,
            Tokens.Line(1f));

        Ink.Draw(
            dl,
            Ink.Role.Body,
            new Vector2(x + Tokens.Metric.SelectorPaddingX, Chrome.CenterY(y, height, Ink.Role.Body)),
            Tokens.Col.InkFaint,
            Strings.SelectorEmpty);
    }

    /// <summary>
    /// One arrow button. The triangle is drawn rather than typed, so it never depends on a
    /// glyph being in the game font — the same reason the close cross is two lines.
    /// </summary>
    private bool Arrow(
        ImDrawListPtr dl,
        string id,
        float x,
        float y,
        float width,
        float height,
        bool pointsLeft,
        bool enabled,
        string disabledReason)
    {
        Vector2 min = new(x, y);
        Vector2 max = new(x + width, y + height);

        ImGui.SetCursorScreenPos(min);
        ImGui.InvisibleButton(id, new Vector2(width, height));
        bool hovered = ImGui.IsItemHovered();
        bool clicked = enabled && ImGui.IsItemClicked();

        ImDrawFlags corners = pointsLeft ? ImDrawFlags.RoundCornersLeft : ImDrawFlags.RoundCornersRight;
        uint top = enabled && hovered ? Tokens.Col.ButtonTop : Tokens.Col.Control;
        uint bottom = enabled && hovered ? Tokens.Col.Control : Tokens.Col.Control2;
        Chrome.VerticalFill(dl, min, max, top, bottom, Tokens.Radius.Control, corners);

        uint ink = !enabled ? Tokens.Col.EdgeDim : hovered ? Tokens.Col.GoldHi : Tokens.Col.Gold;
        float glyph = Tokens.Metric.SelectorGlyph;
        float cx = MathF.Round(x + (width * 0.5f));
        float cy = MathF.Round(y + (height * 0.5f));
        float half = MathF.Round(glyph * 0.5f);

        if (pointsLeft)
        {
            dl.AddTriangleFilled(
                new Vector2(cx - half, cy),
                new Vector2(cx + half, cy - glyph),
                new Vector2(cx + half, cy + glyph),
                ink);
        }
        else
        {
            dl.AddTriangleFilled(
                new Vector2(cx + half, cy),
                new Vector2(cx - half, cy + glyph),
                new Vector2(cx - half, cy - glyph),
                ink);
        }

        if (!enabled)
        {
            Chrome.TooltipOnHover(disabledReason);
        }

        return clicked;
    }

    /// <summary>
    /// The middle of the control: preview, name, counter. It is filled in the input colour
    /// rather than the button colour, so it reads as "a value sits here" even though it is
    /// clickable.
    /// </summary>
    private bool DrawFace(
        ImDrawListPtr dl,
        int index,
        int count,
        float x,
        float y,
        float width,
        float height,
        out bool focused)
    {
        Vector2 min = new(x, y);
        Vector2 max = new(x + width, y + height);

        ImGui.SetCursorScreenPos(min);
        ImGui.InvisibleButton(m_idFace, new Vector2(width, height));
        bool hovered = ImGui.IsItemHovered();
        bool clicked = ImGui.IsItemClicked();
        focused = ImGui.IsItemFocused();

        bool openable = m_options.EnablePopupList;
        dl.AddRectFilled(min, max, hovered && openable ? Tokens.Col.Panel : Tokens.Col.Input);

        T item = m_items[index];
        float pad = Tokens.Metric.SelectorPaddingX;
        float left = x + pad;
        float right = max.X - pad;

        if (m_options.DrawPreview is not null)
        {
            Vector2 swatch = Tokens.Metric.SelectorSwatch;
            float swatchTop = MathF.Round(y + ((height - swatch.Y) * 0.5f));
            Vector2 swatchMin = new(left, swatchTop);
            Vector2 swatchMax = new(left + swatch.X, swatchTop + swatch.Y);
            m_options.DrawPreview(dl, item, swatchMin, swatchMax);
            left = swatchMax.X + Tokens.Space.Md;
        }

        if (m_options.ShowCounter)
        {
            string counter = this.Counter(index, count);
            float counterWidth = Ink.Measure(Ink.Role.Small, counter).X;
            Ink.Draw(
                dl,
                Ink.Role.Small,
                new Vector2(MathF.Round(right - counterWidth), Chrome.CenterY(y, height, Ink.Role.Small)),
                Tokens.Col.InkFaint,
                counter);
            right -= counterWidth + Tokens.Space.Md;
        }

        // Clipped rather than shortened with an ellipsis: building a shortened string would
        // mean allocating one per frame for a case that barely comes up.
        if (right > left)
        {
            dl.PushClipRect(new Vector2(left, min.Y), new Vector2(right, max.Y), true);
            Ink.Draw(
                dl,
                Ink.Role.Body,
                new Vector2(left, Chrome.CenterY(y, height, Ink.Role.Body)),
                Tokens.Col.Ink,
                m_options.Label(item));
            dl.PopClipRect();
        }

        return clicked;
    }

    private string Counter(int index, int count)
    {
        if (index != m_counterFor)
        {
            m_counterFor = index;
            m_counter = (index + 1).ToString(CultureInfo.InvariantCulture)
                + " / "
                + count.ToString(CultureInfo.InvariantCulture);
        }

        return m_counter;
    }

    /// <summary>
    /// The full list, for when you know what you want and the arrows are the long way round.
    /// It is an ImGui popup rather than something drawn in place, so it is not clipped by
    /// the scrolling settings area and closes on a click outside or on escape by itself.
    /// </summary>
    private bool DrawPopup(ref int index, float x, float y, float width)
    {
        float pad = Tokens.Metric.PopupPadding;
        float rowHeight = Tokens.Metric.PopupRowHeight;

        ImGui.SetNextWindowPos(new Vector2(x, y));
        ImGui.SetNextWindowSize(new Vector2(width, 0f));

        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(pad, pad));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, Tokens.Radius.Control);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, Tokens.Line(1f));
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, Tokens.Radius.Control);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, Tokens.Line(1f));
        ImGui.PushStyleVar(
            ImGuiStyleVar.FramePadding,
            new Vector2(Tokens.Space.Md, MathF.Round((Tokens.Metric.PopupSearchHeight - Ink.LineHeight(Ink.Role.Body)) * 0.5f)));
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(0f, Tokens.Space.Sm));
        ImGui.PushStyleColor(ImGuiCol.PopupBg, Tokens.Col.Panel);
        ImGui.PushStyleColor(ImGuiCol.Border, Tokens.Col.EdgeDim);
        ImGui.PushStyleColor(ImGuiCol.FrameBg, Tokens.Col.Input);
        ImGui.PushStyleColor(ImGuiCol.ChildBg, Tokens.Col.Panel);
        ImGui.PushStyleColor(ImGuiCol.Text, Tokens.Col.Ink);

        bool changed = false;
        Ink.Push(Ink.Role.Body);

        if (ImGui.BeginPopup(m_idPopup))
        {
            if (m_options.EnableSearch)
            {
                if (m_focusSearch)
                {
                    ImGui.SetKeyboardFocusHere();
                    m_focusSearch = false;
                }

                ImGui.SetNextItemWidth(-1f);
                ImGui.InputTextWithHint(m_idSearch, Strings.SearchHint, ref m_query, 64);
            }

            int matches = this.CountMatches();
            float listHeight = Math.Max(1, Math.Min(matches, Tokens.Metric.PopupRows)) * rowHeight;

            if (ImGui.BeginChild(m_idList, new Vector2(0f, listHeight)))
            {
                if (matches == 0)
                {
                    ImGui.SetCursorPos(new Vector2(Tokens.Space.Sm, Tokens.Space.Sm));
                    Ink.Push(Ink.Role.Small);
                    ImGui.PushStyleColor(ImGuiCol.Text, Tokens.Col.InkFaint);
                    ImGui.TextUnformatted(Strings.SearchNoMatch);
                    ImGui.PopStyleColor();
                    Ink.Pop(Ink.Role.Small);
                }
                else
                {
                    for (int i = 0; i < m_items.Count; i++)
                    {
                        if (!this.Matches(m_items[i]))
                        {
                            continue;
                        }

                        if (this.DrawRow(i, index, rowHeight))
                        {
                            index = i;
                            changed = true;
                            m_open = false;
                            ImGui.CloseCurrentPopup();
                        }
                    }
                }
            }

            ImGui.EndChild();
            ImGui.EndPopup();
        }
        else
        {
            // Clicked away or dismissed with escape — ImGui has already closed it.
            m_open = false;
        }

        Ink.Pop(Ink.Role.Body);
        ImGui.PopStyleColor(5);
        ImGui.PopStyleVar(7);
        return changed;
    }

    private bool DrawRow(int i, int selected, float rowHeight)
    {
        ImDrawListPtr dl = ImGui.GetWindowDrawList();
        Vector2 min = ImGui.GetCursorScreenPos();
        float width = ImGui.GetContentRegionAvail().X;
        Vector2 max = new(min.X + width, min.Y + rowHeight);

        ImGui.PushID(i);
        ImGui.InvisibleButton(m_idRow, new Vector2(width, rowHeight));
        bool hovered = ImGui.IsItemHovered();
        bool clicked = ImGui.IsItemClicked();
        ImGui.PopID();

        bool isSelected = i == selected;
        if (isSelected)
        {
            dl.AddRectFilled(min, max, Tokens.Col.NavSelected);
        }
        else if (hovered)
        {
            dl.AddRectFilled(min, max, Tokens.Col.NavHover);
        }

        T item = m_items[i];
        float left = min.X + Tokens.Space.Md;
        if (m_options.DrawPreview is not null)
        {
            Vector2 swatch = Tokens.Metric.PopupSwatch;
            float top = MathF.Round(min.Y + ((rowHeight - swatch.Y) * 0.5f));
            m_options.DrawPreview(dl, item, new Vector2(left, top), new Vector2(left + swatch.X, top + swatch.Y));
            left += swatch.X + Tokens.Space.Md;
        }

        uint ink = isSelected ? Tokens.Col.GoldHi : hovered ? Tokens.Col.Ink : Tokens.Col.InkDim;
        Ink.Draw(
            dl,
            Ink.Role.Body,
            new Vector2(left, Chrome.CenterY(min.Y, rowHeight, Ink.Role.Body)),
            ink,
            m_options.Label(item));

        return clicked;
    }

    private int CountMatches()
    {
        if (m_query.Length == 0)
        {
            return m_items.Count;
        }

        int matches = 0;
        for (int i = 0; i < m_items.Count; i++)
        {
            if (this.Matches(m_items[i]))
            {
                matches++;
            }
        }

        return matches;
    }

    private bool Matches(T item) =>
        m_query.Length == 0
        || m_options.Label(item).Contains(m_query, StringComparison.OrdinalIgnoreCase);
}
