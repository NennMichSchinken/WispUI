using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using WispUI.Appearance;
using WispUI.Localization;
using WispUI.Style;

namespace WispUI.Interface.Widgets;

/// <summary>
/// The clipboard half of the screen header: copy, paste, and the one step back out of a
/// paste. Built once and handed every module — Party Frames today, Player Bars, Target and
/// Target-of-Target later — because the buffer belongs to a module, not to a section, and
/// this strip is where that boundary shows.
/// </summary>
internal sealed class AppearanceBar
{
    private const string IdCopy = "##wisp-app-copy";
    private const string IdPaste = "##wisp-app-paste";
    private const string IdUndo = "##wisp-app-undo";
    private const string IdPanel = "##wisp-app-panel";
    private const string IdApply = "##wisp-app-apply";
    private const string IdCancel = "##wisp-app-cancel";

    /// <summary>
    /// The parts, in the order the panel lists them. Ids are held alongside the labels so no
    /// string is built while drawing.
    /// </summary>
    private static readonly FieldRow[] FieldRows =
    {
        new(AppearanceFields.Colours, Strings.FieldColours, "##wisp-app-f0"),
        new(AppearanceFields.Texture, Strings.FieldTexture, "##wisp-app-f1"),
        new(AppearanceFields.Shape, Strings.FieldShape, "##wisp-app-f2"),
        new(AppearanceFields.Text, Strings.FieldText, "##wisp-app-f3"),
        new(AppearanceFields.Background, Strings.FieldBackground, "##wisp-app-f4"),
        new(AppearanceFields.Opacity, Strings.FieldOpacity, "##wisp-app-f5"),
        new(AppearanceFields.Icon, Strings.FieldIcon, "##wisp-app-f6"),
    };

    private readonly AppearanceClipboard m_clipboard;

    /// <summary>What the panel has ticked. Everything, until the user says otherwise.</summary>
    private AppearanceFields m_mask = AppearanceFields.All;

    private bool m_open;
    private IAppearanceOwner? m_lastOwner;

    public AppearanceBar(AppearanceClipboard clipboard)
    {
        m_clipboard = clipboard;
    }

    /// <summary>
    /// Says which element is on screen — <c>null</c> for a screen that has no appearance at
    /// all. Leaving the element takes the step back with it, and so does closing the window:
    /// an undo button that outlives the moment it belonged to is worse than none.
    /// <para>
    /// This has to be called on every frame, including the frames where the strip itself is
    /// not drawn. That was the bug behind an undo button that sat there through a screen
    /// change: a bar that only notices a change while it is on screen never notices it.
    /// </para>
    /// </summary>
    public void NoteOwner(IAppearanceOwner? owner)
    {
        if (ReferenceEquals(owner, m_lastOwner))
        {
            return;
        }

        m_lastOwner = owner;
        m_clipboard.ForgetUndo();
        m_open = false;
    }

    /// <summary>
    /// Draws the buttons right to left from <paramref name="right"/> and returns the left
    /// edge of the leftmost one, so the caller knows what room is left.
    /// </summary>
    public float Draw(IAppearanceOwner owner, float right, float y)
    {
        this.NoteOwner(owner);

        float cursor = right - Chrome.MeasureButton(Strings.PasteAppearance);
        if (Chrome.Button(IdPaste, Strings.PasteAppearance, cursor, y, m_clipboard.HasContent, Strings.PasteDisabled))
        {
            m_mask = AppearanceFields.All;
            m_open = true;
            ImGui.OpenPopup(IdPanel);
        }

        cursor -= Tokens.Space.Sm + Chrome.MeasureButton(Strings.CopyAppearance);
        if (Chrome.Button(IdCopy, Strings.CopyAppearance, cursor, y, true))
        {
            m_clipboard.Copy(owner);
        }

        if (m_clipboard.CanUndo(owner))
        {
            cursor -= Tokens.Space.Md + Chrome.MeasureButton(Strings.UndoPaste);
            if (Chrome.Button(IdUndo, Strings.UndoPaste, cursor, y, true, null, true))
            {
                m_clipboard.Undo(owner);
            }
        }

        if (m_open)
        {
            this.DrawPanel(owner, right, y + Tokens.Metric.ButtonHeight + Tokens.Metric.PopupGap);
        }

        return cursor;
    }

    /// <summary>
    /// The panel behind <c>Paste…</c>: where the look came from, what to carry over, and the
    /// one button that does it.
    /// <para>
    /// It lists exactly what the buffer holds. A part the target element does not have is
    /// shown disabled with the reason beside it rather than left out — being told what will
    /// not come across is the difference between a paste you understand and a paste that
    /// appears to have done nothing.
    /// </para>
    /// </summary>
    private void DrawPanel(IAppearanceOwner owner, float right, float y)
    {
        float width = Tokens.Metric.PastePanelWidth;
        float pad = Tokens.Metric.PastePanelPadding;
        float rowHeight = Chrome.CheckBoxHeight();
        float line = Tokens.Line(1f);

        int rows = 0;
        foreach (FieldRow row in FieldRows)
        {
            if ((m_clipboard.SourceFields & row.Flag) != 0)
            {
                rows++;
            }
        }

        float height = (pad * 2f)
            + Ink.LineHeight(Ink.Role.Small) + Tokens.Space.Md
            + (rows * rowHeight) + (Math.Max(0, rows - 1) * Tokens.Metric.FieldGap)
            + Tokens.Space.Lg + line + Tokens.Space.Lg
            + Tokens.Metric.ButtonHeight;

        ImGui.SetNextWindowPos(new Vector2(right - width, y));
        ImGui.SetNextWindowSize(new Vector2(width, height));

        // Popup style vars, not window ones: a popup ignores WindowBorderSize entirely, which
        // is why this panel first went out with no edge at all.
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.PopupRounding, Tokens.Radius.Control);
        ImGui.PushStyleVar(ImGuiStyleVar.PopupBorderSize, line);
        ImGui.PushStyleColor(ImGuiCol.PopupBg, Tokens.Col.PopupBg);
        ImGui.PushStyleColor(ImGuiCol.Border, Tokens.Col.PopupEdge);

        if (ImGui.BeginPopup(IdPanel))
        {
            // Escape is handled by the window, which cannot close a popup from the outside.
            if (Chrome.ClosePopupRequested)
            {
                ImGui.CloseCurrentPopup();
            }

            ImDrawListPtr dl = ImGui.GetWindowDrawList();
            Vector2 origin = ImGui.GetWindowPos();
            float x = origin.X + pad;
            float cursorY = origin.Y + pad;
            float contentRight = origin.X + width - pad;

            // "from: Party Frames" — the label quiet, the source named in the heading colour,
            // because the name is the part you read.
            Ink.Draw(dl, Ink.Role.Small, new Vector2(x, cursorY), Tokens.Col.InkFaint, Strings.PasteFrom);
            Ink.Draw(
                dl,
                Ink.Role.Small,
                new Vector2(MathF.Round(x + Ink.Measure(Ink.Role.Small, Strings.PasteFrom).X), cursorY),
                Tokens.Col.Heading,
                m_clipboard.SourceName);
            cursorY += Ink.LineHeight(Ink.Role.Small) + Tokens.Space.Md;

            bool first = true;
            foreach (FieldRow row in FieldRows)
            {
                if ((m_clipboard.SourceFields & row.Flag) == 0)
                {
                    continue;
                }

                if (!first)
                {
                    cursorY += Tokens.Metric.FieldGap;
                }

                first = false;
                bool supported = (owner.SupportedFields & row.Flag) != 0;
                bool ticked = (m_mask & row.Flag) != 0;

                if (Chrome.CheckBox(row.Id, row.Label, x, cursorY, ticked && supported, null, supported))
                {
                    m_mask ^= row.Flag;
                }

                if (!supported)
                {
                    float noteWidth = Ink.Measure(Ink.Role.Small, Strings.FieldUnsupported).X;
                    Ink.Draw(
                        dl,
                        Ink.Role.Small,
                        new Vector2(
                            MathF.Round(contentRight - noteWidth),
                            Chrome.CenterY(cursorY, rowHeight, Ink.Role.Small)),
                        Tokens.Col.InkFaint,
                        Strings.FieldUnsupported);
                }

                cursorY += rowHeight;
            }

            cursorY += Tokens.Space.Lg;
            Chrome.Hairline(dl, x, contentRight, MathF.Round(cursorY), Tokens.Col.Hairline);
            cursorY += line + Tokens.Space.Lg;

            bool canApply = m_clipboard.EffectiveMask(owner, m_mask) != AppearanceFields.None;
            float buttonX = contentRight - Chrome.MeasureButton(Strings.PasteApply);
            if (Chrome.Button(IdApply, Strings.PasteApply, buttonX, cursorY, canApply, Strings.ApplyDisabled, true))
            {
                m_clipboard.Paste(owner, m_mask);
                m_open = false;
                ImGui.CloseCurrentPopup();
            }

            buttonX -= Tokens.Space.Md + Chrome.MeasureButton(Strings.PasteCancel);
            if (Chrome.Button(IdCancel, Strings.PasteCancel, buttonX, cursorY, true))
            {
                m_open = false;
                ImGui.CloseCurrentPopup();
            }

            ImGui.EndPopup();
        }
        else
        {
            // Clicked away or dismissed with escape; ImGui has closed it already.
            m_open = false;
        }

        ImGui.PopStyleColor(2);
        ImGui.PopStyleVar(3);
    }

    /// <summary>One line of the paste panel: which part, what it is called, and its hit box id.</summary>
    private readonly struct FieldRow
    {
        public readonly AppearanceFields Flag;
        public readonly string Label;
        public readonly string Id;

        public FieldRow(AppearanceFields flag, string label, string id)
        {
            this.Flag = flag;
            this.Label = label;
            this.Id = id;
        }
    }
}
