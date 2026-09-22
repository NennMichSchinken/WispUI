using System;
using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using WispUI.Core;
using WispUI.Data;
using WispUI.Hud.CombatTracker;
using WispUI.Interface.Widgets;
using WispUI.Localization;
using WispUI.Style;

namespace WispUI.Interface.Screens;

/// <summary>
/// The combat meter's page. Everything HamMeter let you set, less what the suite now answers
/// for it — colours come from the palette under Global, the look from the suite itself.
/// <para>
/// Two tabs, cut by kind like every other module (§3.1): <c>Base</c> is the bar and what it
/// says, plus when the meter starts over; <c>Layout</c> is where it sits and how it is framed.
/// No preview band — the meter's own test mode is the preview (spec §3a).
/// </para>
/// </summary>
internal sealed class CombatTrackerScreen
{
    private const string IdBarsGroup = "##wisp-ct-bars";
    private const string IdTextGroup = "##wisp-ct-text";
    private const string IdFightsGroup = "##wisp-ct-fights";
    private const string IdSizeGroup = "##wisp-ct-size";
    private const string IdLookGroup = "##wisp-ct-look";
    private const string IdStyle = "##wisp-ct-style";
    private const string IdColour = "##wisp-ct-colour";
    private const string IdOpacity = "##wisp-ct-opacity";
    private const string IdSmooth = "##wisp-ct-smooth";
    private const string IdJobMark = "##wisp-ct-job";
    private const string IdRanks = "##wisp-ct-ranks";
    private const string IdShort = "##wisp-ct-short";
    private const string IdTextSize = "##wisp-ct-textsize";
    private const string IdOnlyInCombat = "##wisp-ct-combatonly";
    private const string IdAutoReset = "##wisp-ct-autoreset";
    private const string IdConfirm = "##wisp-ct-confirm";
    private const string IdEndOnReset = "##wisp-ct-endreset";
    private const string IdEndAfter = "##wisp-ct-endafter";
    private const string IdWidth = "##wisp-ct-width";
    private const string IdHeight = "##wisp-ct-height";
    private const string IdBarHeight = "##wisp-ct-barheight";
    private const string IdBarSpacing = "##wisp-ct-barspacing";
    private const string IdRim = "##wisp-ct-rim";
    private const string IdBackground = "##wisp-ct-background";
    private const string IdLocked = "##wisp-ct-locked";
    private const string IdTest = "##wisp-ct-test";

    private const float PixelStep = 1f;
    private const float OpacityStep = 0.01f;
    private const float PixelEditScale = 1f;
    private const float OpacityEditScale = 100f;

    // One cached caption per slider, rebuilt only when its own number moves.
    private const int SlotOpacity = 0;
    private const int SlotTextSize = 1;
    private const int SlotWidth = 2;
    private const int SlotHeight = 3;
    private const int SlotBarHeight = 4;
    private const int SlotBarSpacing = 5;
    private const int SlotBackground = 6;
    private const int SlotCount = SlotBackground + 1;

    private static readonly string[] ColourModes = { Strings.ColourByJob, Strings.ColourByRole };
    private static readonly string[] JobMarks = { Strings.MeterJobIcon, Strings.MeterJobLetters, Strings.MeterJobOff };

    private readonly Configuration m_config;
    private readonly CombatTrackerElement m_meter;
    private readonly ArrowSelector<BarStyle> m_style;

    private readonly string[] m_caption = new string[SlotCount];
    private readonly int[] m_captionFor = new int[SlotCount];

    public CombatTrackerScreen(Configuration config, CombatTrackerElement meter)
    {
        m_config = config;
        m_meter = meter;
        Array.Fill(m_caption, string.Empty);
        Array.Fill(m_captionFor, int.MinValue);

        // The same selector, over the same list, as the party frames' health bar: one list of
        // styles for the whole suite, so a new texture arrives in both at once.
        m_style = new ArrowSelector<BarStyle>(
            IdStyle,
            BarStyles.ForBar,
            new ArrowSelectorOptions<BarStyle>
            {
                Label = BarStyles.Label,
                DrawPreview = BarStyles.DrawPreview,
                EnablePopupList = true,
                EnableSearch = true,
            });
    }

    /// <summary>The Base tab: the bar, what it says, and when the meter starts over.</summary>
    public void Draw(float width)
    {
        Vector2 origin = ImGui.GetCursorScreenPos();
        float column = Chrome.ColumnWidth(width);

        Chrome.BeginGroupRow();
        Chrome.GroupScope bars = this.DrawBarsGroup(Chrome.ColumnX(origin.X, width, 0), origin.Y, column, out float barsHeight);
        Chrome.GroupScope text = this.DrawTextGroup(Chrome.ColumnX(origin.X, width, 1), origin.Y, column, out float textHeight);
        float y = origin.Y + Chrome.GroupFrameRow(bars, barsHeight, text, textHeight) + Tokens.Metric.ColumnGutter;

        Chrome.BeginGroupRow();
        Chrome.GroupScope fights = this.DrawFightsGroup(Chrome.ColumnX(origin.X, width, 0), y, column, out float fightsHeight);
        y += Chrome.GroupFrame(fights, fightsHeight) + Tokens.Metric.ColumnGutter;

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, y - origin.Y + Tokens.Metric.ContentPaddingBottom));
    }

    /// <summary>The Layout tab: how big the meter is, and how it sits on the screen.</summary>
    public void DrawLayout(float width)
    {
        Vector2 origin = ImGui.GetCursorScreenPos();
        float column = Chrome.ColumnWidth(width);

        Chrome.BeginGroupRow();
        Chrome.GroupScope size = this.DrawSizeGroup(Chrome.ColumnX(origin.X, width, 0), origin.Y, column, out float sizeHeight);
        Chrome.GroupScope look = this.DrawLookGroup(Chrome.ColumnX(origin.X, width, 1), origin.Y, column, out float lookHeight);
        float y = origin.Y + Chrome.GroupFrameRow(size, sizeHeight, look, lookHeight) + Tokens.Metric.ColumnGutter;

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, y - origin.Y + Tokens.Metric.ContentPaddingBottom));
    }

    private Chrome.GroupScope DrawBarsGroup(float x, float y, float width, out float contentHeight)
    {
        Configuration.CombatTrackerConfig cfg = m_config.CombatTracker;
        Chrome.GroupScope group = Chrome.BeginGroup(
            IdBarsGroup,
            new Chrome.GroupHead { Title = Strings.GroupMeterBars, Description = Strings.GroupMeterBarsHint },
            x,
            y,
            width);

        float pitch = Chrome.RowPitch();
        float rowY = group.ContentY;

        int style = BarStyles.IndexOf(BarStyles.ForBar, cfg.BarStyleName);
        if (m_style.Draw(ref style, Chrome.Row(Strings.BarStyle, group.ContentX, rowY, group.ContentWidth, false), rowY, Chrome.ControlWidth()))
        {
            cfg.BarStyleName = BarStyles.NameAt(BarStyles.ForBar, style);
            m_config.MarkDirty();
        }

        rowY += pitch;
        int colour = cfg.ColourMode;
        if (Chrome.SegmentRow(IdColour, Strings.BarColour, group.ContentX, rowY, group.ContentWidth, ColourModes, ref colour, true))
        {
            cfg.ColourMode = colour;
            m_config.MarkDirty();
        }

        rowY += pitch;
        Chrome.SliderResult opacity = Chrome.Slider(
            IdOpacity,
            Strings.MeterBarOpacity,
            this.Percent(SlotOpacity, cfg.BarOpacity),
            group.ContentX,
            rowY,
            group.ContentWidth,
            cfg.BarOpacity,
            Configuration.MinBarOpacity,
            1f,
            null,
            null,
            true,
            OpacityStep,
            OpacityEditScale);

        if (opacity.Changed)
        {
            cfg.BarOpacity = opacity.Value;
            m_config.MarkDirty();
        }

        rowY += pitch;
        if (Chrome.OptionRow(IdSmooth, Strings.SmoothBars, group.ContentX, rowY, group.ContentWidth, cfg.SmoothBars, Chrome.OptionControl.Tick, Strings.MeterSmoothTooltip, true, true))
        {
            cfg.SmoothBars = !cfg.SmoothBars;
            m_config.MarkDirty();
        }

        contentHeight = rowY - group.ContentY + Chrome.RowHeight();
        Chrome.EndGroupContent(group, contentHeight);
        return group;
    }

    private Chrome.GroupScope DrawTextGroup(float x, float y, float width, out float contentHeight)
    {
        Configuration.CombatTrackerConfig cfg = m_config.CombatTracker;
        Chrome.GroupScope group = Chrome.BeginGroup(
            IdTextGroup,
            new Chrome.GroupHead { Title = Strings.GroupMeterText, Description = Strings.GroupMeterTextHint },
            x,
            y,
            width);

        float pitch = Chrome.RowPitch();
        float rowY = group.ContentY;

        int mark = cfg.JobMark;
        if (Chrome.SegmentRow(IdJobMark, Strings.MeterJobMark, group.ContentX, rowY, group.ContentWidth, JobMarks, ref mark, false))
        {
            cfg.JobMark = mark;
            m_config.MarkDirty();
        }

        rowY += pitch;
        if (Chrome.OptionRow(IdRanks, Strings.MeterRanks, group.ContentX, rowY, group.ContentWidth, cfg.ShowRanks, Chrome.OptionControl.Tick, null, true, true))
        {
            cfg.ShowRanks = !cfg.ShowRanks;
            m_config.MarkDirty();
        }

        rowY += pitch;
        if (Chrome.OptionRow(IdShort, Strings.MeterShortNumbers, group.ContentX, rowY, group.ContentWidth, cfg.ShortNumbers, Chrome.OptionControl.Tick, Strings.MeterShortNumbersTooltip, true, true))
        {
            cfg.ShortNumbers = !cfg.ShortNumbers;
            m_config.MarkDirty();
        }

        rowY += pitch;
        this.PixelSlider(IdTextSize, Strings.MeterTextSize, SlotTextSize, group, rowY, Configuration.MinTextSize, Configuration.MaxTextSize);

        contentHeight = rowY - group.ContentY + Chrome.RowHeight();
        Chrome.EndGroupContent(group, contentHeight);
        return group;
    }

    private Chrome.GroupScope DrawFightsGroup(float x, float y, float width, out float contentHeight)
    {
        Configuration.CombatTrackerConfig cfg = m_config.CombatTracker;
        Chrome.GroupScope group = Chrome.BeginGroup(
            IdFightsGroup,
            new Chrome.GroupHead { Title = Strings.GroupMeterFights, Description = Strings.GroupMeterFightsHint },
            x,
            y,
            width);

        float pitch = Chrome.RowPitch();
        float rowY = group.ContentY;

        if (this.Tick(IdOnlyInCombat, Strings.MeterOnlyInCombat, group, rowY, cfg.OnlyInCombat, null, false))
        {
            cfg.OnlyInCombat = !cfg.OnlyInCombat;
        }

        rowY += pitch;
        if (this.Tick(IdAutoReset, Strings.MeterAutoReset, group, rowY, cfg.AutoResetInDuty, null, true))
        {
            cfg.AutoResetInDuty = !cfg.AutoResetInDuty;
        }

        rowY += pitch;
        if (this.Tick(IdConfirm, Strings.MeterConfirmReset, group, rowY, cfg.ConfirmReset, null, true))
        {
            cfg.ConfirmReset = !cfg.ConfirmReset;
        }

        rowY += pitch;
        if (this.Tick(IdEndOnReset, Strings.MeterEndOnReset, group, rowY, cfg.EndEncounterOnReset, Strings.MeterEndOnResetTooltip, true))
        {
            cfg.EndEncounterOnReset = !cfg.EndEncounterOnReset;
        }

        rowY += pitch;
        if (this.Tick(IdEndAfter, Strings.MeterEndAfterCombat, group, rowY, cfg.AutoEndCombat, Strings.MeterEndAfterCombatTooltip, true))
        {
            cfg.AutoEndCombat = !cfg.AutoEndCombat;
        }

        contentHeight = rowY - group.ContentY + Chrome.RowHeight();
        Chrome.EndGroupContent(group, contentHeight);
        return group;
    }

    private Chrome.GroupScope DrawSizeGroup(float x, float y, float width, out float contentHeight)
    {
        Configuration.CombatTrackerConfig cfg = m_config.CombatTracker;
        Chrome.GroupScope group = Chrome.BeginGroup(
            IdSizeGroup,
            new Chrome.GroupHead { Title = Strings.GroupMeterSize, Description = Strings.GroupMeterSizeHint },
            x,
            y,
            width);

        float pitch = Chrome.RowPitch();
        float rowY = group.ContentY;

        // The sliders stop at a sensible size; the corner goes as far as the screen does, and
        // the number here then simply reads what the corner made.
        this.PixelSlider(IdWidth, Strings.MeterWidth, SlotWidth, group, rowY, Configuration.MinMeterWidth, MeterSliderMax, false);
        rowY += pitch;
        this.PixelSlider(IdHeight, Strings.MeterHeight, SlotHeight, group, rowY, Configuration.MinMeterHeight, MeterSliderMax);
        rowY += pitch;
        this.PixelSlider(IdBarHeight, Strings.MeterBarHeight, SlotBarHeight, group, rowY, Configuration.MinMeterBarHeight, Configuration.MaxMeterBarHeight);
        rowY += pitch;
        this.PixelSlider(IdBarSpacing, Strings.MeterBarSpacing, SlotBarSpacing, group, rowY, 0f, Configuration.MaxFrameSpacing);

        contentHeight = rowY - group.ContentY + Chrome.RowHeight();
        Chrome.EndGroupContent(group, contentHeight);
        return group;
    }

    private Chrome.GroupScope DrawLookGroup(float x, float y, float width, out float contentHeight)
    {
        Configuration.CombatTrackerConfig cfg = m_config.CombatTracker;
        Chrome.GroupScope group = Chrome.BeginGroup(
            IdLookGroup,
            new Chrome.GroupHead { Title = Strings.GroupMeterLook, Description = Strings.GroupMeterLookHint },
            x,
            y,
            width);

        float pitch = Chrome.RowPitch();
        float rowY = group.ContentY;

        if (this.Tick(IdRim, Strings.MeterRim, group, rowY, cfg.ShowRim, null, false))
        {
            cfg.ShowRim = !cfg.ShowRim;
        }

        rowY += pitch;
        Chrome.SliderResult background = Chrome.Slider(
            IdBackground,
            Strings.MeterBackground,
            this.Percent(SlotBackground, cfg.BackgroundOpacity),
            group.ContentX,
            rowY,
            group.ContentWidth,
            cfg.BackgroundOpacity,
            0f,
            1f,
            null,
            null,
            true,
            OpacityStep,
            OpacityEditScale);

        if (background.Changed)
        {
            cfg.BackgroundOpacity = background.Value;
            m_config.MarkDirty();
        }

        rowY += pitch;
        if (this.Tick(IdLocked, Strings.MeterLockedRow, group, rowY, cfg.Locked, Strings.MeterLockedTooltip, true))
        {
            cfg.Locked = !cfg.Locked;
        }

        // A way of looking, not a setting: it lives on the meter, in memory, and goes when
        // this window closes (spec §3a).
        rowY += pitch;
        if (Chrome.OptionRow(IdTest, Strings.MeterTestMode, group.ContentX, rowY, group.ContentWidth, m_meter.TestMode, Chrome.OptionControl.Tick, Strings.MeterTestModeTooltip, true, true))
        {
            m_meter.TestMode = !m_meter.TestMode;
        }

        contentHeight = rowY - group.ContentY + Chrome.RowHeight();

        // Said where the switch is, and nowhere else — the suite never nags (§5.1a).
        if (m_config.CombatTrackerEnabled && !m_meter.Connected)
        {
            float noteY = rowY + Chrome.RowHeight() + Tokens.Space.Sm;
            Ink.Draw(ImGui.GetWindowDrawList(), Ink.Role.Small, new Vector2(group.ContentX, noteY), Tokens.Col.InkFaint, Strings.MeterNotConnectedNote);
            contentHeight = noteY + Ink.LineHeight(Ink.Role.Small) - group.ContentY;
        }

        Chrome.EndGroupContent(group, contentHeight);
        return group;
    }

    /// <summary>The largest the size sliders go. The corner can go further.</summary>
    private const float MeterSliderMax = 1200f;

    private bool Tick(string id, string label, in Chrome.GroupScope group, float rowY, bool value, string? tooltip, bool divider)
    {
        if (!Chrome.OptionRow(id, label, group.ContentX, rowY, group.ContentWidth, value, Chrome.OptionControl.Tick, tooltip, true, divider))
        {
            return false;
        }

        m_config.MarkDirty();
        return true;
    }

    private void PixelSlider(
        string id,
        string label,
        int slot,
        in Chrome.GroupScope group,
        float rowY,
        float min,
        float max,
        bool divider = true)
    {
        float value = this.SizeValue(slot);
        Chrome.SliderResult result = Chrome.Slider(
            id,
            label,
            this.Pixels(slot, value),
            group.ContentX,
            rowY,
            group.ContentWidth,
            value,
            min,
            max,
            null,
            null,
            divider,
            PixelStep,
            PixelEditScale);

        if (result.Changed)
        {
            this.SetSizeValue(slot, MathF.Round(result.Value));
            m_config.MarkDirty();
        }
    }

    private float SizeValue(int slot) => slot switch
    {
        SlotTextSize => m_config.CombatTracker.TextSize,
        SlotWidth => m_config.CombatTracker.Width,
        SlotHeight => m_config.CombatTracker.Height,
        SlotBarHeight => m_config.CombatTracker.BarHeight,
        _ => m_config.CombatTracker.BarSpacing,
    };

    private void SetSizeValue(int slot, float value)
    {
        Configuration.CombatTrackerConfig cfg = m_config.CombatTracker;

        switch (slot)
        {
            case SlotTextSize: cfg.TextSize = value; break;
            case SlotWidth: cfg.Width = value; break;
            case SlotHeight: cfg.Height = value; break;
            case SlotBarHeight: cfg.BarHeight = value; break;
            default: cfg.BarSpacing = value; break;
        }
    }

    private string Pixels(int slot, float value)
    {
        int whole = (int)MathF.Round(value);
        if (m_captionFor[slot] != whole)
        {
            m_captionFor[slot] = whole;
            m_caption[slot] = whole.ToString(CultureInfo.InvariantCulture) + " px";
        }

        return m_caption[slot];
    }

    private string Percent(int slot, float fraction)
    {
        int whole = (int)MathF.Round(fraction * 100f);
        if (m_captionFor[slot] != whole)
        {
            m_captionFor[slot] = whole;
            m_caption[slot] = whole.ToString(CultureInfo.InvariantCulture) + " %";
        }

        return m_caption[slot];
    }
}
