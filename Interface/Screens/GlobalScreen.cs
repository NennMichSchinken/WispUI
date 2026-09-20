using System;
using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using WispUI.Core;
using WispUI.Interface.Widgets;
using WispUI.Localization;
using WispUI.Style;

namespace WispUI.Interface.Screens;

/// <summary>
/// The suite-wide screen. It holds the interface scale, which is what sets the size of
/// everything WispUI draws — the window included, since that has a fixed size on purpose.
/// </summary>
internal sealed class GlobalScreen
{
    private const string IdInterfaceGroup = "##wisp-global-interface";
    private const string IdAccessGroup = "##wisp-global-access";
    private const string IdScale = "##wisp-global-scale";
    private const string IdInfoBar = "##wisp-global-infobar";
    private const string IdLetteringGroup = "##wisp-global-lettering";
    private const string IdFont = "##wisp-global-font";
    private const string IdTextWeight = "##wisp-global-textweight";
    private const string IdTextEdge = "##wisp-global-textedge";

    private readonly Configuration m_config;

    /// <summary>
    /// The one selector in the suite with a searchable list, because it is the one whose list
    /// the player controls: six faces shipped, and however many they drop in their font
    /// folder. Walking that with two arrows is not a list, it is a queue.
    /// </summary>
    private readonly ArrowSelector<FontChoice> m_font;

    // The percentage caption would allocate a string per frame if it were rebuilt every time.
    // It only changes when the value does.
    private string m_scaleText = string.Empty;
    private int m_scaleTextFor = -1;

    /// <summary>The scale while a drag is in progress. Written to the configuration on release.</summary>
    private float m_livePreview;
    private bool m_dragging;

    public GlobalScreen(Configuration config)
    {
        m_config = config;
        m_livePreview = config.Scale;

        m_font = new ArrowSelector<FontChoice>(
            IdFont,
            FontLibrary.All,
            new ArrowSelectorOptions<FontChoice>
            {
                Label = static face => face.Name,
                EnablePopupList = true,
                EnableSearch = true,
                ShowCounter = false,
            });
    }

    /// <summary>Raised when the info bar entry is switched on or off.</summary>
    public event Action? InfoBarPreferenceChanged;

    /// <param name="width">The usable width, with the content padding already taken off.</param>
    public void Draw(float width)
    {
        Vector2 origin = ImGui.GetCursorScreenPos();
        float x = origin.X;
        float y = origin.Y;

        // Groups sit on the same two-column grid the controls used to sit on directly, both
        // drawn before either is framed: they share a bottom edge, and their surfaces are
        // painted under the rows on the lower channel of the row split.
        float column = Chrome.ColumnWidth(width);
        float rowTop = y;

        Chrome.BeginGroupRow();
        Chrome.GroupScope left = this.DrawInterface(Chrome.ColumnX(x, width, 0), rowTop, column, out float leftHeight);
        Chrome.GroupScope right = this.DrawAccess(Chrome.ColumnX(x, width, 1), rowTop, column, out float rightHeight);

        y = rowTop + Chrome.GroupFrameRow(left, leftHeight, right, rightHeight);

        // The lettering came up from the party frames at version 16. It belongs to the suite
        // for the same reason the scale above it does — and for a hard one as well: one face
        // is one font atlas entry and one lock per frame, so it could never have been per
        // module without costing real work every frame (§7).
        Chrome.BeginGroupRow();
        Chrome.GroupScope lettering = this.DrawLettering(Chrome.ColumnX(x, width, 0), y, column, out float letteringHeight);
        y += Chrome.GroupFrame(lettering, letteringHeight) + Tokens.Metric.ColumnGutter;

        // Tells the scroll area how tall the screen is, the air under the last group included.
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, y - origin.Y + Tokens.Metric.ContentPaddingBottom));
    }

    private Chrome.GroupScope DrawInterface(float x, float y, float width, out float contentHeight)
    {
        Chrome.GroupScope group = Chrome.BeginGroup(
            IdInterfaceGroup,
            new Chrome.GroupHead
            {
                Title = Strings.SectionInterface,
                Description = Strings.SectionInterfaceHint,
            },
            x,
            y,
            width);

        float scale = m_dragging ? m_livePreview : m_config.Scale;
        Chrome.SliderResult result = Chrome.Slider(
            IdScale,
            Strings.InterfaceScale,
            this.ScaleCaption(scale),
            group.ContentX,
            group.ContentY,
            group.ContentWidth,
            scale,
            Configuration.MinScale,
            Configuration.MaxScale,
            Strings.InterfaceScaleNote,
            Strings.InterfaceScaleTooltip);

        if (result.Changed)
        {
            // Deliberately NOT applied live. This slider resizes the window itself, so a live
            // apply would grow the window out from under the cursor while dragging — the same
            // defect the pinned preview had. The number under your hand is the feedback; the
            // change lands once, on release.
            m_dragging = true;
            m_livePreview = result.Value;
        }

        if (result.Released && m_dragging)
        {
            m_dragging = false;
            m_config.Scale = m_livePreview;
            m_config.MarkDirty();
            Scaling.Request(m_config.Scale);
        }

        Chrome.EndGroupContent(group, result.Height);
        contentHeight = result.Height;
        return group;
    }

    private Chrome.GroupScope DrawAccess(float x, float y, float width, out float contentHeight)
    {
        Chrome.GroupScope group = Chrome.BeginGroup(
            IdAccessGroup,
            new Chrome.GroupHead
            {
                Title = Strings.SectionAccess,
                Description = Strings.SectionAccessHint,
            },
            x,
            y,
            width);

        if (Chrome.OptionRow(
                IdInfoBar,
                Strings.ShowInfoBarEntry,
                group.ContentX,
                group.ContentY,
                group.ContentWidth,
                m_config.ShowInfoBarEntry,
                Chrome.OptionControl.Tick,
                Strings.ShowInfoBarEntryTooltip))
        {
            m_config.ShowInfoBarEntry = !m_config.ShowInfoBarEntry;
            m_config.MarkDirty();
            this.InfoBarPreferenceChanged?.Invoke();
        }

        // One row, and it is the whole group.
        contentHeight = Chrome.RowHeight();
        Chrome.EndGroupContent(group, contentHeight);
        return group;
    }

    /// <summary>
    /// How every text WispUI draws on the world is lettered: which face, how heavy, and what
    /// carries it over whatever is behind it.
    /// </summary>
    private Chrome.GroupScope DrawLettering(float x, float y, float width, out float contentHeight)
    {
        Chrome.GroupScope group = Chrome.BeginGroup(
            IdLetteringGroup,
            new Chrome.GroupHead
            {
                Title = Strings.GroupLettering,
                Description = Strings.GroupLetteringHint,
            },
            x,
            y,
            width);

        float pitch = Chrome.RowPitch();
        float rowY = group.ContentY;

        // The list is addressed by name, not by position: it grows and shrinks with the
        // player's font folder, and a stored position would mean a different face the moment
        // they added a file.
        int face = FontLibrary.IndexOf(m_config.FontName);
        if (m_font.Draw(
                ref face,
                Chrome.Row(Strings.TextFont, group.ContentX, rowY, group.ContentWidth, false, Strings.TextFontTooltip),
                rowY,
                Chrome.ControlWidth()))
        {
            m_config.FontName = FontLibrary.NameAt(face);
            m_config.MarkDirty();
        }

        rowY += pitch;

        int weight = m_config.TextWeight;
        if (Chrome.SegmentRow(
                IdTextWeight,
                Strings.TextWeight,
                group.ContentX,
                rowY,
                group.ContentWidth,
                WeightNames,
                ref weight,
                true,
                Strings.TextWeightTooltip))
        {
            m_config.TextWeight = weight;
            m_config.MarkDirty();
        }

        rowY += pitch;

        int edge = m_config.TextEdge;
        if (Chrome.SegmentRow(
                IdTextEdge,
                Strings.TextEdge,
                group.ContentX,
                rowY,
                group.ContentWidth,
                EdgeNames,
                ref edge,
                false,
                Strings.TextEdgeTooltip))
        {
            m_config.TextEdge = edge;
            m_config.MarkDirty();
        }

        float used = rowY - group.ContentY + Chrome.RowHeight();

        // Only when the face that is set is not the face being drawn. A shipped font that
        // fails to load falls back to the interface face, and without this line that is
        // indistinguishable from a font that loaded and simply looks thin — which is exactly
        // how a whole test round was spent (Florian, 2026-09-12).
        string? problem = Fonts.FaceProblem;
        if (problem is not null)
        {
            float noteY = rowY + Chrome.RowHeight() + Tokens.Space.Sm;
            Ink.Draw(
                ImGui.GetWindowDrawList(),
                Ink.Role.Small,
                new Vector2(group.ContentX, noteY),
                Tokens.Col.Gold,
                problem);

            used += Tokens.Space.Sm + Ink.LineHeight(Ink.Role.Small);
        }

        Chrome.EndGroupContent(group, used);
        contentHeight = used;
        return group;
    }

    /// <summary>The three weights, in the order the segments sit. Built once, not per frame.</summary>
    private static readonly string[] WeightNames =
    {
        Strings.TextWeightNormal,
        Strings.TextWeightMedium,
        Strings.TextWeightBold,
    };

    /// <summary>The three edges, in the order the segments sit. Built once, not per frame.</summary>
    private static readonly string[] EdgeNames =
    {
        Strings.TextEdgeNone,
        Strings.TextEdgeShadow,
        Strings.TextEdgeOutline,
    };

    private string ScaleCaption(float scale)
    {
        int percent = (int)MathF.Round(scale * 100f);
        if (percent != m_scaleTextFor)
        {
            m_scaleTextFor = percent;
            m_scaleText = percent.ToString(CultureInfo.InvariantCulture) + " %";
        }

        return m_scaleText;
    }
}
