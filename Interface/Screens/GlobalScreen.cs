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

    private readonly Configuration m_config;

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
            Scaling.Commit(m_config.Scale);
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
