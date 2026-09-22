using System;
using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
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
    private const string IdIconStyle = "##wisp-ct-iconstyle";
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
    private const string IdTitleHeight = "##wisp-ct-titleheight";
    private const string IdTitleText = "##wisp-ct-titletext";

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
    private const int SlotTitleHeight = 7;
    private const int SlotTitleText = 8;
    private const int SlotCount = SlotTitleText + 1;

    private static readonly string[] ColourModes = { Strings.ColourByJob, Strings.ColourByRole };
    private static readonly string[] JobMarks = { Strings.MeterJobIcon, Strings.MeterJobLetters, Strings.MeterJobOff };
    private static readonly string[] IconStyles = { Strings.IconStyleFramed, Strings.IconStylePlain };

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

    /// <summary>
    /// Whether the page is the IINACT wizard rather than the settings. Asked by the window
    /// before it draws the tab chips, so it takes the fresh look at IINACT itself.
    /// <para>
    /// Once the wizard has started it stays until IINACT actually answers — installing it
    /// moves it from "missing" to "not running" for a moment, and the wizard vanishing in the
    /// middle of step 3 would take away the one line saying it worked.
    /// </para>
    /// </summary>
    public bool ShowsWizard
    {
        get
        {
            m_meter.RefreshStatus();
            IinactState state = m_meter.Iinact;

            if (state == IinactState.Running)
            {
                m_wizardOpen = false;
                m_step = 0;
            }

            return state == IinactState.Missing || (m_wizardOpen && state != IinactState.Unknown);
        }
    }

    /// <summary>The Base tab: the bar, what it says, and when the meter starts over.</summary>
    public void Draw(float width)
    {
        if (this.ShowsWizard)
        {
            this.DrawWizard(width);
            return;
        }

        Vector2 origin = ImGui.GetCursorScreenPos();
        float column = Chrome.ColumnWidth(width);
        float top = origin.Y + this.DrawTrouble(origin.X, origin.Y, width);

        Chrome.BeginGroupRow();
        Chrome.GroupScope bars = this.DrawBarsGroup(Chrome.ColumnX(origin.X, width, 0), top, column, out float barsHeight);
        Chrome.GroupScope text = this.DrawTextGroup(Chrome.ColumnX(origin.X, width, 1), top, column, out float textHeight);
        float y = top + Chrome.GroupFrameRow(bars, barsHeight, text, textHeight) + Tokens.Metric.ColumnGutter;

        Chrome.BeginGroupRow();
        Chrome.GroupScope fights = this.DrawFightsGroup(Chrome.ColumnX(origin.X, width, 0), y, column, out float fightsHeight);
        y += Chrome.GroupFrame(fights, fightsHeight) + Tokens.Metric.ColumnGutter;

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, y - origin.Y + Tokens.Metric.ContentPaddingBottom));
    }

    /// <summary>The Layout tab: how big the meter is, and how it sits on the screen.</summary>
    public void DrawLayout(float width)
    {
        if (this.ShowsWizard)
        {
            this.DrawWizard(width);
            return;
        }

        Vector2 origin = ImGui.GetCursorScreenPos();
        float column = Chrome.ColumnWidth(width);
        float top = origin.Y + this.DrawTrouble(origin.X, origin.Y, width);

        Chrome.BeginGroupRow();
        Chrome.GroupScope size = this.DrawSizeGroup(Chrome.ColumnX(origin.X, width, 0), top, column, out float sizeHeight);
        Chrome.GroupScope look = this.DrawLookGroup(Chrome.ColumnX(origin.X, width, 1), top, column, out float lookHeight);
        float y = top + Chrome.GroupFrameRow(size, sizeHeight, look, lookHeight) + Tokens.Metric.ColumnGutter;

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

        // The same two sets, in the same words, as the party frames' job icon.
        rowY += pitch;
        int iconStyle = cfg.JobIconStyle;
        if (Chrome.SegmentRow(IdIconStyle, Strings.IconStyle, group.ContentX, rowY, group.ContentWidth, IconStyles, ref iconStyle, true))
        {
            cfg.JobIconStyle = iconStyle;
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
        rowY += pitch;
        this.PixelSlider(IdTitleHeight, Strings.MeterTitleHeight, SlotTitleHeight, group, rowY, Configuration.MinMeterTitle, Configuration.MaxMeterTitle);

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
        this.PixelSlider(IdTitleText, Strings.MeterTitleText, SlotTitleText, group, rowY, Configuration.MinTextSize, Configuration.MaxTextSize);

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
        Chrome.EndGroupContent(group, contentHeight);
        return group;
    }

    // --- IINACT: the wizard, and the card for when it is there but not working --------------

    /// <summary>
    /// The card above the settings when IINACT is installed but not doing its job. Returns the
    /// height it took, gap included, or nothing when all is well.
    /// </summary>
    private float DrawTrouble(float x, float y, float width)
    {
        IinactState state = m_meter.Iinact;

        if (state is not (IinactState.NotRunning or IinactState.NotAnswering))
        {
            return 0f;
        }

        bool stopped = state == IinactState.NotRunning;

        Chrome.BeginGroupRow();
        Chrome.GroupScope group = Chrome.BeginGroup(
            IdTroubleGroup,
            new Chrome.GroupHead
            {
                Title = stopped ? Strings.MeterIinactStopped : Strings.MeterIinactSilent,
                Description = stopped ? Strings.MeterIinactStoppedBody : Strings.MeterIinactSilentBody,
            },
            x,
            y,
            width);

        string label = stopped ? Strings.WizardOpenUpdates : Strings.WizardOpenInstalled;
        if (Chrome.Button(IdTroubleButton, label, group.ContentX, group.ContentY, true))
        {
            Services.PluginInterface.OpenPluginInstallerTo(
                stopped ? PluginInstallerOpenKind.UpdateablePlugins : PluginInstallerOpenKind.InstalledPlugins,
                stopped ? null : IinactSearch);
        }

        float used = Tokens.Metric.ButtonHeight;
        Chrome.EndGroupContent(group, used);
        return Chrome.GroupFrame(group, used) + Tokens.Metric.ColumnGutter;
    }

    /// <summary>
    /// The three steps to IINACT, standing where the settings would (Florian, 2026-09-22):
    /// a bar across the top that fills as the steps are done, one step at a time below it.
    /// <para>
    /// Only three, not the spec's four. "Is the repository added?" is not something a plugin
    /// can see — Dalamud keeps its repository list to itself — so it is folded into the step
    /// that can be checked: whether IINACT is installed and running.
    /// </para>
    /// <para>
    /// A plugin cannot add a repository or install anything (API notes §3.5). What it can do
    /// is put the address on the clipboard and open the two pages where the player does it.
    /// </para>
    /// </summary>
    private void DrawWizard(float width)
    {
        m_wizardOpen = true;

        Vector2 origin = ImGui.GetCursorScreenPos();
        ImDrawListPtr dl = ImGui.GetWindowDrawList();
        IinactState state = m_meter.Iinact;
        float y = origin.Y;

        // The bar: one segment per step — done, on it, still to come.
        float gap = Tokens.Space.Sm;
        float segment = MathF.Floor((width - (gap * (WizardSteps - 1))) / WizardSteps);
        float barHeight = Tokens.Line(5f);

        for (int i = 0; i < WizardSteps; i++)
        {
            float left = origin.X + (i * (segment + gap));
            uint fill = i < m_step ? Tokens.Col.Gold
                : i == m_step ? Tokens.Col.Faded(Tokens.Col.Gold, 0.55f)
                : Tokens.Col.Control;
            dl.AddRectFilled(new Vector2(left, y), new Vector2(left + segment, y + barHeight), fill, barHeight * 0.5f);
            Ink.Draw(dl, Ink.Role.Small, new Vector2(left, y + barHeight + Tokens.Space.Sm), i == m_step ? Tokens.Col.GoldHi : Tokens.Col.InkFaint, StepLabels[i]);
        }

        y += barHeight + Tokens.Space.Sm + Ink.LineHeight(Ink.Role.Small) + Tokens.Space.Xl;

        Ink.Draw(dl, Ink.Role.Small, new Vector2(origin.X, y), Tokens.Col.InkFaint, StepKickers[m_step]);
        y += Ink.LineHeight(Ink.Role.Small) + Tokens.Space.Sm;

        Ink.Draw(dl, Ink.Role.ScreenTitle, new Vector2(origin.X, y), Tokens.Col.Heading, StepTitles[m_step]);
        y += Ink.LineHeight(Ink.Role.ScreenTitle) + Tokens.Space.Md;

        float textWidth = MathF.Min(width, Tokens.Px(WizardTextWidth));
        string body = StepBodies[m_step];
        Ink.DrawWrapped(Ink.Role.Body, new Vector2(origin.X, y), textWidth, Tokens.Col.Ink, body);
        y += Ink.MeasureWrapped(Ink.Role.Body, body, textWidth).Y + Tokens.Space.Lg;

        // The box: what the step is about, in a form that can be read off at a glance.
        string boxText = m_step switch
        {
            0 => RepoUrl,
            1 => Strings.WizardPath,
            _ => state switch
            {
                IinactState.NotRunning => Strings.WizardStatusStopped,
                IinactState.Starting => Strings.WizardStatusStarting,
                IinactState.NotAnswering => Strings.WizardStatusSilent,
                _ => Strings.WizardStatusMissing,
            },
        };

        float pad = Tokens.Metric.GroupPadding;
        float boxHeight = Ink.MeasureWrapped(Ink.Role.Body, boxText, textWidth - (pad * 2f)).Y + (pad * 2f);
        Vector2 boxMin = new(origin.X, y);
        Vector2 boxMax = new(origin.X + textWidth, y + boxHeight);
        dl.AddRectFilled(boxMin, boxMax, Tokens.Col.GroupBg, Tokens.Radius.Group);
        dl.AddRect(boxMin, boxMax, Tokens.Col.Hairline, Tokens.Radius.Group, ImDrawFlags.RoundCornersAll, Tokens.Line(1f));
        Ink.DrawWrapped(Ink.Role.Body, new Vector2(boxMin.X + pad, boxMin.Y + pad), textWidth - (pad * 2f), m_step == 1 ? Tokens.Col.GoldHi : Tokens.Col.Ink, boxText);
        y += boxHeight + Tokens.Space.Lg;

        // Back, the step's own action, and Next — the last step has no Next: it moves on by
        // itself once IINACT answers.
        float x = origin.X;
        float spacing = Tokens.Space.Md;

        if (m_step > 0)
        {
            if (Chrome.Button(IdBack, Strings.WizardBack, x, y, true))
            {
                m_step--;
            }

            x += Chrome.MeasureButton(Strings.WizardBack) + spacing;
        }

        string action = m_step switch
        {
            0 => m_copied ? Strings.WizardCopied : Strings.WizardCopy,
            1 => Strings.WizardOpenSettings,
            _ => Strings.WizardOpenInstaller,
        };

        if (Chrome.Button(IdAction, action, x, y, true, null, true))
        {
            this.RunStep();
        }

        x += Chrome.MeasureButton(action) + spacing;

        if (m_step < WizardSteps - 1 && Chrome.Button(IdNext, Strings.WizardNext, x, y, true))
        {
            m_step++;
        }

        y += Tokens.Metric.ButtonHeight + Tokens.Space.Xl;

        // The long way round, for anybody who wants it in pictures.
        Vector2 linkSize = Ink.Measure(Ink.Role.Small, Strings.WizardGuide);
        ImGui.SetCursorScreenPos(new Vector2(origin.X, y));
        ImGui.InvisibleButton(IdGuide, linkSize);
        bool linkHovered = ImGui.IsItemHovered();
        Chrome.ShowHand(linkHovered);

        if (ImGui.IsItemClicked())
        {
            Dalamud.Utility.Util.OpenLink(GuideUrl);
        }

        Ink.Draw(dl, Ink.Role.Small, new Vector2(origin.X, y), linkHovered ? Tokens.Col.GoldHi : Tokens.Col.Gold, Strings.WizardGuide);
        y += linkSize.Y;

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, y - origin.Y + Tokens.Metric.ContentPaddingBottom));
    }

    private void RunStep()
    {
        switch (m_step)
        {
            case 0:
                ImGui.SetClipboardText(RepoUrl);
                m_copied = true;
                break;

            // The Experimental page is where the custom repositories are listed.
            case 1:
                Services.PluginInterface.OpenDalamudSettingsTo(SettingsOpenKind.Experimental);
                break;

            // Opened already searching for it, so there is one entry to click.
            default:
                Services.PluginInterface.OpenPluginInstallerTo(PluginInstallerOpenKind.AllPlugins, IinactSearch);
                break;
        }
    }

    /// <summary>
    /// IINACT's repository, as its own installation guide gives it (iinact.com/installation,
    /// checked 2026-09-22). IINACT is not in Dalamud's main list — damage meters are not
    /// allowed there — so this is the only way to it.
    /// </summary>
    private const string RepoUrl = "https://raw.githubusercontent.com/marzent/IINACT/main/repo.json";

    private const string GuideUrl = "https://www.iinact.com/installation/";

    /// <summary>What the installer's search box is filled with.</summary>
    private const string IinactSearch = "IINACT";

    private const int WizardSteps = 3;

    /// <summary>How wide the wizard's text runs before it wraps, so a line stays readable in a wide window.</summary>
    private const float WizardTextWidth = 560f;

    private const string IdTroubleGroup = "##wisp-ct-trouble";
    private const string IdTroubleButton = "##wisp-ct-trouble-btn";
    private const string IdBack = "##wisp-ct-wiz-back";
    private const string IdNext = "##wisp-ct-wiz-next";
    private const string IdAction = "##wisp-ct-wiz-action";
    private const string IdGuide = "##wisp-ct-wiz-guide";

    private static readonly string[] StepLabels = { Strings.WizardStep1, Strings.WizardStep2, Strings.WizardStep3 };
    private static readonly string[] StepKickers = { Strings.WizardKicker1, Strings.WizardKicker2, Strings.WizardKicker3 };
    private static readonly string[] StepTitles = { Strings.WizardTitle1, Strings.WizardTitle2, Strings.WizardTitle3 };
    private static readonly string[] StepBodies = { Strings.WizardBody1, Strings.WizardBody2, Strings.WizardBody3 };

    // The wizard's place, in memory only: coming back to the page later starts it where the
    // player left it, and a restart starts it over, which is what anybody would expect.
    private int m_step;
    private bool m_copied;
    private bool m_wizardOpen;

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
        SlotTitleHeight => m_config.CombatTracker.TitleHeight,
        SlotTitleText => m_config.CombatTracker.TitleTextSize,
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
            case SlotTitleHeight: cfg.TitleHeight = value; break;
            case SlotTitleText: cfg.TitleTextSize = value; break;
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
