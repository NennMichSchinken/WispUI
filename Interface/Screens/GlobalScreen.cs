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

    public void Draw(float width)
    {
        Vector2 origin = ImGui.GetCursorScreenPos();
        float x = origin.X;
        float y = origin.Y;
        float contentWidth = width - (Tokens.Metric.SectionPaddingX * 2f);

        y += Chrome.SectionHeader(Strings.SectionInterface, Strings.SectionInterfaceHint, x, y);

        float scale = m_dragging ? m_livePreview : m_config.Scale;
        Chrome.SliderResult result = Chrome.Slider(
            IdScale,
            Strings.InterfaceScale,
            this.ScaleCaption(scale),
            x,
            y,
            contentWidth,
            scale,
            Configuration.MinScale,
            Configuration.MaxScale);

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

        y += result.Height + Tokens.Space.Sm;
        y += Chrome.Hint(Strings.InterfaceScaleHint, x, y, contentWidth);

        y += Chrome.SectionRule(x, x + contentWidth, y);

        y += Chrome.SectionHeader(Strings.SectionAccess, Strings.SectionAccessHint, x, y);

        if (Chrome.CheckBox(IdInfoBar, Strings.ShowInfoBarEntry, x, y, m_config.ShowInfoBarEntry))
        {
            m_config.ShowInfoBarEntry = !m_config.ShowInfoBarEntry;
            m_config.MarkDirty();
            this.InfoBarPreferenceChanged?.Invoke();
        }

        y += Tokens.Metric.RowHeight;
        y += Chrome.Hint(Strings.ShowInfoBarEntryHint, x, y, contentWidth);

        // Tell the scroll area how tall the screen actually is.
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(contentWidth, y - origin.Y));
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
