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
        float contentWidth = width;

        // Controls sit on the two-column grid and fill one column, never the whole width.
        // Every block below advances by its own measured height plus one rhythm token —
        // there is no per-control row constant to drift out of step with what was drawn.
        float columnWidth = Chrome.ColumnWidth(contentWidth);

        y += Chrome.SectionHeader(Strings.SectionInterface, Strings.SectionInterfaceHint, x, y);

        float scale = m_dragging ? m_livePreview : m_config.Scale;
        Chrome.SliderResult result = Chrome.Slider(
            IdScale,
            Strings.InterfaceScale,
            this.ScaleCaption(scale),
            Chrome.ColumnX(x, contentWidth, 0),
            y,
            columnWidth,
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

        y += result.Height;

        y += Chrome.SectionRule(x, x + contentWidth, y);

        y += Chrome.SectionHeader(Strings.SectionAccess, Strings.SectionAccessHint, x, y);

        if (Chrome.CheckBox(
                IdInfoBar,
                Strings.ShowInfoBarEntry,
                Chrome.ColumnX(x, contentWidth, 0),
                y,
                m_config.ShowInfoBarEntry,
                Strings.ShowInfoBarEntryTooltip))
        {
            m_config.ShowInfoBarEntry = !m_config.ShowInfoBarEntry;
            m_config.MarkDirty();
            this.InfoBarPreferenceChanged?.Invoke();
        }

        y += Chrome.CheckBoxHeight();

        // Tell the scroll area how tall the screen is, the air under the last row included —
        // without it a scrolled screen ends flush with the window edge.
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(contentWidth, y - origin.Y + Tokens.Metric.ContentPaddingBottom));
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
