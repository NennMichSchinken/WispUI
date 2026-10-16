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
    private const string IdSlidecastFill = "##wisp-qol-slidefill";
    private const string IdRingGroup = "##wisp-qol-ring";
    private const string IdRingSize = "##wisp-qol-ringsize";
    private const string IdRingThickness = "##wisp-qol-ringline";
    private const string IdRingColour = "##wisp-qol-ringcolour";

    private const float PixelStep = 1f;
    private const float PixelEditScale = 1f;

    private readonly Configuration m_config;

    /// <summary>The two slider captions, rebuilt only when the number under them moves.</summary>
    private string m_sizeText = string.Empty;
    private int m_sizeTextFor = -1;
    private string m_lineText = string.Empty;
    private int m_lineTextFor = -1;

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
        Chrome.GroupScope ring = this.DrawCursorRing(Chrome.ColumnX(origin.X, width, 1), origin.Y, column, out float ringHeight);
        float y = origin.Y + Chrome.GroupFrameRow(slidecast, slidecastHeight, ring, ringHeight) + Tokens.Metric.ColumnGutter;

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

        rowY += Chrome.RowPitch();
        if (Chrome.OptionRow(
                IdSlidecastFill,
                Strings.SlidecastFill,
                group.ContentX,
                rowY,
                group.ContentWidth,
                cfg.SlidecastFill,
                Chrome.OptionControl.Tick,
                Strings.SlidecastFillTooltip,
                true,
                true))
        {
            cfg.SlidecastFill = !cfg.SlidecastFill;
            m_config.MarkDirty();
        }

        contentHeight = rowY - group.ContentY + Chrome.RowHeight();
        Chrome.EndGroupContent(group, contentHeight);
        return group;
    }

    /// <summary>
    /// The ring round the mouse. No preview band: while this page is open the ring is round
    /// the pointer already, so every change shows on the very pointer moving the slider.
    /// </summary>
    private Chrome.GroupScope DrawCursorRing(float x, float y, float width, out float contentHeight)
    {
        Configuration.QualityOfLifeConfig cfg = m_config.QualityOfLife;
        Chrome.GroupScope group = Chrome.BeginGroup(
            IdRingGroup,
            new Chrome.GroupHead
            {
                Title = Strings.GroupCursorRing,
                Description = Strings.GroupCursorRingHint,
                Toggle = cfg.CursorRingEnabled,
            },
            x,
            y,
            width);

        if (group.ToggleClicked)
        {
            cfg.CursorRingEnabled = !cfg.CursorRingEnabled;
            m_config.MarkDirty();
        }

        float rowY = group.ContentY;
        Chrome.SliderResult size = Chrome.Slider(
            IdRingSize,
            Strings.CursorRingSize,
            Pixels(cfg.CursorRingSize, ref m_sizeTextFor, ref m_sizeText),
            group.ContentX,
            rowY,
            group.ContentWidth,
            cfg.CursorRingSize,
            Configuration.MinCursorRing,
            Configuration.MaxCursorRing,
            null,
            null,
            false,
            PixelStep,
            PixelEditScale);

        if (size.Changed)
        {
            cfg.CursorRingSize = MathF.Round(size.Value);
            m_config.MarkDirty();
        }

        rowY += Chrome.RowPitch();
        Chrome.SliderResult line = Chrome.Slider(
            IdRingThickness,
            Strings.CursorRingThickness,
            Pixels(cfg.CursorRingThickness, ref m_lineTextFor, ref m_lineText),
            group.ContentX,
            rowY,
            group.ContentWidth,
            cfg.CursorRingThickness,
            Configuration.MinCursorRingLine,
            Configuration.MaxCursorRingLine,
            null,
            null,
            true,
            PixelStep,
            PixelEditScale);

        if (line.Changed)
        {
            cfg.CursorRingThickness = MathF.Round(line.Value);
            m_config.MarkDirty();
        }

        rowY += Chrome.RowPitch();
        uint colour = cfg.CursorRingColour;
        if (Chrome.ColourRow(
                IdRingColour,
                Strings.CursorRingColour,
                group.ContentX,
                rowY,
                group.ContentWidth,
                ref colour,
                true,
                null,
                Tokens.Col.CursorRing))
        {
            cfg.CursorRingColour = colour;
            m_config.MarkDirty();
        }

        contentHeight = rowY - group.ContentY + Chrome.RowHeight();
        Chrome.EndGroupContent(group, contentHeight);
        return group;
    }

    private static string Pixels(float value, ref int cachedFor, ref string cached)
    {
        int whole = (int)MathF.Round(value);
        if (cachedFor != whole)
        {
            cachedFor = whole;
            cached = whole.ToString(CultureInfo.InvariantCulture) + " px";
        }

        return cached;
    }
}
