using System.Numerics;
using Dalamud.Bindings.ImGui;
using WispUI.Core;
using WispUI.Interface.Widgets;
using WispUI.Localization;
using WispUI.Style;

namespace WispUI.Interface.Screens;

/// <summary>
/// The Quality of Life page: small helpers on the game's own interface.
/// <para>
/// One tab, <c>Base</c>, with one group per helper and a switch in each head (Florian,
/// 2026-09-25). A helper is a group, not a tab — the page grows by cards, and only the day a
/// helper needs more than a card does it earn a tab of its own.
/// </para>
/// </summary>
internal sealed class QualityOfLifeScreen
{
    private const string IdSlidecastGroup = "##wisp-qol-slidecast";
    private const string IdSlidecastWait = "##wisp-qol-slidewait";
    private const string IdSlidecastReady = "##wisp-qol-slideready";

    private readonly Configuration m_config;

    public QualityOfLifeScreen(Configuration config)
    {
        m_config = config;
    }

    public void Draw(float width)
    {
        Vector2 origin = ImGui.GetCursorScreenPos();
        float column = Chrome.ColumnWidth(width);

        Chrome.BeginGroupRow();
        Chrome.GroupScope slidecast = this.DrawSlidecast(Chrome.ColumnX(origin.X, width, 0), origin.Y, column, out float slidecastHeight);
        float y = origin.Y + Chrome.GroupFrame(slidecast, slidecastHeight) + Tokens.Metric.ColumnGutter;

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, y - origin.Y + Tokens.Metric.ContentPaddingBottom));
    }

    private Chrome.GroupScope DrawSlidecast(float x, float y, float width, out float contentHeight)
    {
        Configuration.QualityOfLifeConfig cfg = m_config.QualityOfLife;
        Chrome.GroupScope group = Chrome.BeginGroup(
            IdSlidecastGroup,
            new Chrome.GroupHead
            {
                Title = Strings.GroupSlidecast,
                Description = Strings.GroupSlidecastHint,
                Toggle = cfg.SlidecastEnabled,
            },
            x,
            y,
            width);

        if (group.ToggleClicked)
        {
            cfg.SlidecastEnabled = !cfg.SlidecastEnabled;
            m_config.MarkDirty();
        }

        float rowY = group.ContentY;

        uint wait = cfg.SlidecastWaitColour;
        if (Chrome.ColourRow(
                IdSlidecastWait,
                Strings.SlidecastWaitColour,
                group.ContentX,
                rowY,
                group.ContentWidth,
                ref wait,
                false,
                Strings.SlidecastWaitTooltip,
                Tokens.Col.SlideWait))
        {
            cfg.SlidecastWaitColour = wait;
            m_config.MarkDirty();
        }

        rowY += Chrome.RowPitch();
        uint ready = cfg.SlidecastReadyColour;
        if (Chrome.ColourRow(
                IdSlidecastReady,
                Strings.SlidecastReadyColour,
                group.ContentX,
                rowY,
                group.ContentWidth,
                ref ready,
                true,
                Strings.SlidecastReadyTooltip,
                Tokens.Col.SlideReady))
        {
            cfg.SlidecastReadyColour = ready;
            m_config.MarkDirty();
        }

        contentHeight = rowY - group.ContentY + Chrome.RowHeight();
        Chrome.EndGroupContent(group, contentHeight);
        return group;
    }
}
