using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Text;
using Dalamud.Plugin;
using WispUI.Core;
using WispUI.Data;
using WispUI.Interface;
using WispUI.Interface.Widgets;
using WispUI.Localization;
using WispUI.Style;

namespace WispUI.Hud.CombatTracker;

/// <summary>Where IINACT stands, as far as another plugin can tell.</summary>
internal enum IinactState
{
    /// <summary>Not looked yet.</summary>
    Unknown = 0,

    /// <summary>Not in Dalamud's list of installed plugins.</summary>
    Missing,

    /// <summary>Installed, but Dalamud did not start it — after a game patch, usually waiting for its update.</summary>
    NotRunning,

    /// <summary>Started a moment ago and not answering yet.</summary>
    Starting,

    /// <summary>Started, but its line stays silent.</summary>
    NotAnswering,

    /// <summary>Answering; fights will arrive.</summary>
    Running,
}

/// <summary>What sits between a bar's rank and its name.</summary>
internal enum MeterJobMark
{
    Icon = 0,
    Letters = 1,
    Off = 2,
}

/// <summary>
/// The combat meter: HamMeter, our own published plugin, as a module of the suite (spec §3,
/// step 2). It wears the settings window's look rather than the HUD's — rim, title bar,
/// panel and card (spec §3a) — and reads its colours from the suite's one palette.
/// <para>
/// Unlike the party frames it is a real window: it has buttons, menus, a list that scrolls
/// and a corner to pull. It is still an element like any other — edit mode moves it, the
/// module switch turns it off — it simply takes its input from a window of its own instead of
/// from a block laid over the frames.
/// </para>
/// <para>
/// 🔴 The draw path allocates nothing. IINACT sends a new fight object about once a second;
/// the rows, their texts and the title are rebuilt when one arrives and only then. Every
/// frame in between reads arrays.
/// </para>
/// </summary>
internal sealed class CombatTrackerElement : HudElement, IDisposable
{
    private const string IdWindow = "##wisp-meter";
    private const string IdBars = "##wisp-meter-bars";
    private const string IdTitleDrag = "##wisp-meter-move";
    private const string IdGrip = "##wisp-meter-grip";
    private const string IdLock = "##wisp-meter-lock";
    private const string IdReset = "##wisp-meter-reset";
    private const string IdFights = "##wisp-meter-fights";
    private const string IdMetric = "##wisp-meter-metric";
    private const string IdSettings = "##wisp-meter-settings";
    private const string PopupMetric = "##wisp-meter-metric-pop";
    private const string PopupFights = "##wisp-meter-fights-pop";
    private const string PopupReset = "##wisp-meter-reset-pop";
    private const string IdConfirm = "##wisp-meter-confirm";
    private const string IdCancel = "##wisp-meter-cancel";

    /// <summary>The view that follows the fight in progress, and the one that adds every fight up.</summary>
    private const int ViewCurrent = -1;
    private const int ViewOverall = -2;

    /// <summary>More lines than any party, alliance or trial log will send.</summary>
    private const int MaxRows = 64;

    /// <summary>How often a missing IINACT is asked again. Asking throws when it is not there.</summary>
    private static readonly TimeSpan ConnectEvery = TimeSpan.FromSeconds(3);

    /// <summary>How often the list of installed plugins is looked at. Walking it allocates.</summary>
    private static readonly TimeSpan StatusEvery = TimeSpan.FromSeconds(2);

    /// <summary>Failed calls in a row before a running IINACT counts as not answering (about ten seconds).</summary>
    private const int NotAnsweringAfter = 3;

    /// <summary>IINACT's name in Dalamud's list, as its own repository names it.</summary>
    private const string IinactName = "IINACT";

    /// <summary>How long after combat the fight is closed, when that is switched on (HamMeter's value).</summary>
    private static readonly TimeSpan EndAfterCombat = TimeSpan.FromSeconds(3);

    private const ImGuiWindowFlags WindowFlags =
        ImGuiWindowFlags.NoDecoration
        | ImGuiWindowFlags.NoMove
        | ImGuiWindowFlags.NoBackground
        | ImGuiWindowFlags.NoSavedSettings
        | ImGuiWindowFlags.NoFocusOnAppearing
        | ImGuiWindowFlags.NoBringToFrontOnFocus
        | ImGuiWindowFlags.NoNav
        | ImGuiWindowFlags.NoScrollbar
        | ImGuiWindowFlags.NoScrollWithMouse;

    private const ImGuiWindowFlags BarsFlags =
        ImGuiWindowFlags.NoBackground
        | ImGuiWindowFlags.NoScrollbar
        | ImGuiWindowFlags.NoNav;

    /// <summary>"1." to "64.", built once rather than per bar per frame.</summary>
    private static readonly string[] Ranks = BuildRanks();

    /// <summary>One id per line of each menu, built once.</summary>
    private static readonly string[] MetricIds = BuildIds("##wisp-meter-m", CombatMetrics.All.Length);
    private static readonly string[] FightIds = BuildIds("##wisp-meter-f", IinactClient.MaxHistory + 2);

    private readonly Configuration m_config;
    private readonly IinactClient m_client = new();

    // The rows on show, and the ones before them — kept so a bar that is still there after an
    // update can carry on sliding from where it was rather than jumping back to its start.
    private MeterRow[] m_rows = new MeterRow[MaxRows];
    private MeterRow[] m_previous = new MeterRow[MaxRows];
    private int m_count;

    // What the rows above were built from. Any of these changing is what makes a rebuild.
    private CombatEvent? m_builtFrom;
    private int m_builtMetric = -1;
    private int m_builtView = int.MinValue;
    private bool m_builtShort;
    private bool m_builtTest;

    private string m_titleMain = string.Empty;
    private string m_titleSub = string.Empty;

    /// <summary>The earlier fights, as the list shows them. Built when the list is opened.</summary>
    private readonly List<string> m_fightLabels = new();

    private CombatEvent? m_testEvent;
    private int m_view = ViewCurrent;

    private DateTime m_nextConnect = DateTime.MinValue;
    private DateTime m_nextStatus = DateTime.MinValue;
    private int m_failedConnects;
    private DateTime m_endAt = DateTime.MaxValue;
    private bool m_wasInDuty;
    private bool m_wasInCombat;

    // Where the meter was drawn this frame, for edit mode.
    private Vector2 m_min;
    private Vector2 m_max;

    // A move or a resize in progress.
    private Vector2 m_grab;
    private Vector2 m_resizeFrom;
    private Vector2 m_resizeSize;
    private int m_resizeAxis;

    public CombatTrackerElement(Configuration config)
    {
        m_config = config;
    }

    /// <summary>The meter's gear button: open the suite on this module's page.</summary>
    public event Action? SettingsRequested;

    /// <summary>
    /// A made-up fight in place of the real one, for setting the meter up without a pull.
    /// <para>
    /// 🔴 Only in memory, and switched off when the settings window closes (spec §3a). A test
    /// mode left on is exactly the case where somebody looks at invented numbers in the middle
    /// of a real fight and believes them.
    /// </para>
    /// </summary>
    public bool TestMode { get; set; }

    /// <summary>Where IINACT stands, as of the last look. Kept current by <see cref="RefreshStatus"/>.</summary>
    public IinactState Iinact { get; private set; } = IinactState.Unknown;

    /// <summary>
    /// Looks at IINACT: installed, started, answering (§5.1a — the list of installed plugins,
    /// then a call that has to come back). Throttled here, so the tick and the settings page
    /// can both ask every frame and the list is still walked only every couple of seconds.
    /// <para>
    /// Only knocks when there is somebody to answer. Asking a plugin that is not there
    /// throws inside Dalamud, and doing that every few seconds for somebody who has no
    /// IINACT and never will is noise for nothing.
    /// </para>
    /// </summary>
    public void RefreshStatus()
    {
        DateTime now = DateTime.UtcNow;

        if (now < m_nextStatus)
        {
            return;
        }

        m_nextStatus = now + StatusEvery;

        bool installed = false;
        bool loaded = false;

        foreach (IExposedPlugin plugin in Services.PluginInterface.InstalledPlugins)
        {
            if (string.Equals(plugin.InternalName, IinactName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(plugin.Name, IinactName, StringComparison.OrdinalIgnoreCase))
            {
                installed = true;
                loaded = plugin.IsLoaded;
                break;
            }
        }

        if (!installed || !loaded)
        {
            // Gone or stopped: whatever line we had to it is gone with it.
            m_client.Forget();
            m_failedConnects = 0;
            this.Iinact = installed ? IinactState.NotRunning : IinactState.Missing;
            return;
        }

        if (!m_client.Connected && now >= m_nextConnect)
        {
            m_nextConnect = now + ConnectEvery;
            m_client.Connect();

            if (!m_client.Connected)
            {
                m_failedConnects++;
            }
        }

        // A plugin that has just started takes a moment to open its line. Only after a few
        // tries in a row is it not answering rather than still starting.
        this.Iinact = m_client.Connected ? IinactState.Running
            : m_failedConnects >= NotAnsweringAfter ? IinactState.NotAnswering
            : IinactState.Starting;
    }

    public override string Name => Strings.NavCombatTracker;

    public override bool Enabled => m_config.CombatTrackerEnabled;

    public override bool Movable => true;

    public override bool HasAnythingToDraw
    {
        get
        {
            if (!Services.ClientState.IsLoggedIn)
            {
                return false;
            }

            if (EditMode.IsActive || this.TestMode || !m_config.CombatTracker.OnlyInCombat)
            {
                return true;
            }

            CombatEvent? current = m_client.Current;
            return current is not null && current.Active;
        }
    }

    public override void Bounds(out Vector2 min, out Vector2 max)
    {
        min = m_min;
        max = m_max;
    }

    public override void MoveTo(Vector2 topLeft)
    {
        m_config.CombatTracker.PositionX = topLeft.X;
        m_config.CombatTracker.PositionY = topLeft.Y;
        m_config.MarkDirty();
    }

    /// <summary>
    /// Keeps the line to IINACT open and does what HamMeter did between fights: start over on
    /// entering a duty, and close a fight shortly after combat when asked to.
    /// </summary>
    public override void Tick()
    {
        DateTime now = DateTime.UtcNow;
        Configuration.CombatTrackerConfig cfg = m_config.CombatTracker;

        this.RefreshStatus();

        // Entering a duty, never leaving one. Read as a change of state on every tick, so
        // the flags settling a moment after the zone change is simply the tick they change on.
        bool inDuty = Services.Condition[ConditionFlag.BoundByDuty]
            || Services.Condition[ConditionFlag.BoundByDuty56]
            || Services.Condition[ConditionFlag.BoundByDuty95];

        if (cfg.AutoResetInDuty && inDuty && !m_wasInDuty)
        {
            this.Reset();
        }

        m_wasInDuty = inDuty;

        bool inCombat = Services.Condition[ConditionFlag.InCombat];

        if (inCombat)
        {
            m_endAt = DateTime.MaxValue;
        }
        else if (m_wasInCombat && cfg.AutoEndCombat)
        {
            m_endAt = now + EndAfterCombat;
        }

        if (now >= m_endAt)
        {
            m_endAt = DateTime.MaxValue;
            EndFight();
        }

        m_wasInCombat = inCombat;
    }

    /// <summary>Starts over: every fight is dropped, and the meter goes back to the current one.</summary>
    public void Reset()
    {
        if (m_config.CombatTracker.EndEncounterOnReset)
        {
            EndFight();
        }

        m_client.ClearData();
        m_view = ViewCurrent;
        m_count = 0;
        m_builtFrom = null;
    }

    public override void Collect()
    {
        Configuration.CombatTrackerConfig cfg = m_config.CombatTracker;
        CombatEvent? shown = this.Shown();

        if (ReferenceEquals(shown, m_builtFrom)
            && cfg.Metric == m_builtMetric
            && m_view == m_builtView
            && cfg.ShortNumbers == m_builtShort
            && this.TestMode == m_builtTest)
        {
            return;
        }

        this.Rebuild(shown, (CombatMetric)cfg.Metric, cfg.ShortNumbers);
        m_builtFrom = shown;
        m_builtMetric = cfg.Metric;
        m_builtView = m_view;
        m_builtShort = cfg.ShortNumbers;
        m_builtTest = this.TestMode;
    }

    public override void Draw(ImDrawListPtr background)
    {
        Configuration.CombatTrackerConfig cfg = m_config.CombatTracker;
        Vector2 pos = new(Tokens.WorldPx(cfg.PositionX), Tokens.WorldPx(cfg.PositionY));
        Vector2 size = new(Tokens.WorldPx(cfg.Width), Tokens.WorldPx(cfg.Height));

        m_min = pos;
        m_max = pos + size;

        ImGui.SetNextWindowPos(pos);
        ImGui.SetNextWindowSize(size);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0f);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, Vector2.Zero);

        // 🔴 Deaf to the mouse while arranging. Edit mode lays one window over the whole
        // screen to catch the drag, but this one can sit in front of it — and then the meter
        // took the press itself and could not be picked up (Florian, 2026-09-22).
        ImGuiWindowFlags flags = EditMode.IsActive ? WindowFlags | ImGuiWindowFlags.NoInputs : WindowFlags;

        if (ImGui.Begin(IdWindow, flags))
        {
            this.DrawMeter(pos, size);
        }

        ImGui.End();
        ImGui.PopStyleVar(3);
    }

    public void Dispose() => m_client.Dispose();

    // --- drawing --------------------------------------------------------------

    private void DrawMeter(Vector2 pos, Vector2 size)
    {
        Configuration.CombatTrackerConfig cfg = m_config.CombatTracker;
        ImDrawListPtr dl = ImGui.GetWindowDrawList();
        float surface = cfg.BackgroundOpacity;
        Vector2 max = pos + size;

        // The pointer over the meter stays the game's own, the same as over the frames.
        if (ImGui.IsWindowHovered(
                ImGuiHoveredFlags.RootAndChildWindows
                | ImGuiHoveredFlags.AllowWhenBlockedByActiveItem
                | ImGuiHoveredFlags.AllowWhenBlockedByPopup))
        {
            NativeUi.KeepGameCursor();
        }

        float radius = Tokens.Metric.MeterRadius;
        dl.AddRectFilled(pos, max, Tokens.Col.Faded(Tokens.Col.Panel, surface), radius, ImDrawFlags.RoundCornersAll);

        float titleBottom = pos.Y + Tokens.WorldPx(cfg.TitleHeight);
        this.DrawTitle(dl, pos, max.X, titleBottom, surface);

        float pad = Tokens.Metric.MeterPad;
        Vector2 cardMin = new(pos.X + pad, titleBottom + pad);
        Vector2 cardMax = new(max.X - pad, max.Y - pad);

        if (cardMax.X > cardMin.X && cardMax.Y > cardMin.Y)
        {
            float cardRadius = Tokens.Metric.MeterCardRadius;
            dl.AddRectFilled(cardMin, cardMax, Tokens.Col.Faded(Tokens.Col.GroupBg, surface), cardRadius);
            dl.AddRect(cardMin, cardMax, Tokens.Col.Faded(Tokens.Col.Hairline, surface), cardRadius, ImDrawFlags.RoundCornersAll, Tokens.WorldLine(1f));

            float inset = Tokens.Metric.MeterCardPad;
            this.DrawBars(new Vector2(cardMin.X + inset, cardMin.Y + inset), new Vector2(cardMax.X - inset, cardMax.Y - inset));
        }

        if (cfg.ShowRim)
        {
            ConfigWindow.DrawWindowEdge(dl, pos, size, Tokens.WorldLine(1f), radius);
        }

        this.DrawCorner(dl, pos, max);
        this.DrawPopups();
    }

    private void DrawTitle(ImDrawListPtr dl, Vector2 pos, float right, float bottom, float surface)
    {
        Configuration.CombatTrackerConfig cfg = m_config.CombatTracker;
        float radius = Tokens.Metric.MeterRadius;

        Chrome.VerticalFill(
            dl,
            pos,
            new Vector2(right, bottom),
            Tokens.Col.Faded(Tokens.Col.TitleBarTop, surface),
            Tokens.Col.Faded(Tokens.Col.TitleBar, surface),
            radius,
            ImDrawFlags.RoundCornersTop,
            Tokens.Metric.MeterTitleFade);

        float line = Tokens.WorldLine(1f);
        float pad = Tokens.Metric.MeterPad;
        Chrome.Rule(dl, pos.X + pad, right - pad, bottom - (line * Tokens.Col.TitleRule.Length), line, Tokens.Metric.MeterTitleFade);

        // The four buttons, laid out from the right edge inwards: settings outermost, so it
        // stays put whatever the title says. Reset, fights, reading, settings from the left.
        float titleHeight = bottom - pos.Y;

        // The buttons keep their size, and only shrink when the bar gets too low to hold them.
        float icon = MathF.Min(Tokens.Metric.MeterIcon, titleHeight - (Tokens.Metric.MeterCardPad * 2f));
        float gap = Tokens.Metric.MeterIconGap;
        float iconY = MathF.Round(pos.Y + ((titleHeight - icon) * 0.5f));
        float x = right - pad - icon;

        if (IconButton(IdSettings, LineIcons.Settings, x, iconY, icon, Strings.MeterSettingsTooltip))
        {
            this.SettingsRequested?.Invoke();
        }

        x -= icon + gap;
        if (IconButton(IdMetric, LineIcons.ArrowRightLeft, x, iconY, icon, Strings.MeterMetricTooltip))
        {
            ImGui.OpenPopup(PopupMetric);
        }

        x -= icon + gap;
        if (IconButton(IdFights, LineIcons.List, x, iconY, icon, Strings.MeterFightsTooltip))
        {
            this.BuildFightLabels();
            ImGui.OpenPopup(PopupFights);
        }

        x -= icon + gap;
        if (IconButton(IdReset, LineIcons.RotateCcw, x, iconY, icon, Strings.MeterResetTooltip))
        {
            if (cfg.ConfirmReset)
            {
                ImGui.OpenPopup(PopupReset);
            }
            else
            {
                this.Reset();
            }
        }

        float buttonsLeft = x - gap;

        // What is on show, clipped short of the buttons rather than running under them.
        float textSize = Tokens.WorldPx(cfg.TitleTextSize);
        float textY = MathF.Round(pos.Y + ((titleHeight - textSize) * 0.5f));
        float textX = pos.X + pad + Tokens.Metric.MeterBarInset;

        dl.PushClipRect(pos, new Vector2(buttonsLeft, bottom), true);
        Ink.DrawNote(dl, textSize, new Vector2(textX, textY), Tokens.Col.Heading, m_titleMain, TextEdge.None);
        textX += Ink.MeasureNote(textSize, m_titleMain) + Tokens.Metric.MeterBarInset;
        Ink.DrawNote(dl, textSize, new Vector2(textX, textY), Tokens.Col.Ink, m_titleSub, TextEdge.None);
        dl.PopClipRect();

        // The title bar is also the handle, except over the buttons and while locked. Edit
        // mode has its own handle over the whole meter and does not need this one.
        if (cfg.Locked || EditMode.IsActive || buttonsLeft <= pos.X)
        {
            return;
        }

        ImGui.SetCursorScreenPos(pos);
        ImGui.InvisibleButton(IdTitleDrag, new Vector2(buttonsLeft - pos.X, bottom - pos.Y));
        Vector2 mouse = ImGui.GetIO().MousePos;

        if (ImGui.IsItemActivated())
        {
            m_grab = mouse - pos;
        }

        if (ImGui.IsItemActive())
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeAll);
            this.MoveTo(mouse - m_grab);
        }
    }

    private void DrawBars(Vector2 min, Vector2 max)
    {
        Configuration.CombatTrackerConfig cfg = m_config.CombatTracker;
        Vector2 area = max - min;

        if (area.X < 1f || area.Y < 1f)
        {
            return;
        }

        ImGui.SetCursorScreenPos(min);

        if (ImGui.BeginChild(IdBars, area, false, BarsFlags))
        {
            ImDrawListPtr dl = ImGui.GetWindowDrawList();
            Vector2 origin = ImGui.GetCursorScreenPos();

            if (m_count == 0)
            {
                this.DrawNotice(dl, origin, area.X);
            }
            else
            {
                float barHeight = Tokens.WorldPx(cfg.BarHeight);
                float pitch = barHeight + Tokens.WorldPx(cfg.BarSpacing);
                float delta = ImGui.GetIO().DeltaTime;

                for (int i = 0; i < m_count; i++)
                {
                    float top = origin.Y + (i * pitch);

                    // Only what is inside the card is drawn; the rest is still counted so the
                    // list scrolls to the right length.
                    if (top + barHeight >= min.Y && top <= max.Y)
                    {
                        this.DrawBar(dl, ref m_rows[i], new Vector2(origin.X, top), new Vector2(origin.X + area.X, top + barHeight), delta);
                    }
                }

                ImGui.Dummy(new Vector2(area.X, (m_count * pitch) - Tokens.WorldPx(cfg.BarSpacing)));
            }
        }

        ImGui.EndChild();
    }

    /// <summary>
    /// What stands where the bars would, when there are none: waiting for a fight, or what is
    /// wrong with IINACT and what to do about it. Said here, in the meter the player is
    /// looking at, and nowhere else — no popup, no chat line (§5.1a).
    /// <para>
    /// "Not running" cannot honestly say "an update is out": Dalamud does not tell another
    /// plugin whether one is. What it does tell is that IINACT is installed and did not start,
    /// and after a game patch that nearly always means it is waiting for its update.
    /// </para>
    /// </summary>
    private void DrawNotice(ImDrawListPtr dl, Vector2 origin, float width)
    {
        float inset = Tokens.Metric.MeterBarInset;
        Vector2 at = origin + new Vector2(inset, inset);
        float size = Tokens.WorldPx(m_config.CombatTracker.TitleTextSize);

        (string title, string? body) = this.TestMode || this.Iinact == IinactState.Running
            ? (Strings.MeterNoData, (string?)null)
            : this.Iinact switch
            {
                IinactState.Missing => (Strings.MeterIinactMissing, Strings.MeterIinactMissingBody),
                IinactState.NotRunning => (Strings.MeterIinactStopped, Strings.MeterIinactStoppedBody),
                IinactState.NotAnswering => (Strings.MeterIinactSilent, Strings.MeterIinactSilentBody),
                _ => (Strings.MeterNotConnected, (string?)null),
            };

        Ink.DrawNote(dl, size, at, body is null ? Tokens.Col.InkDim : Tokens.Col.GoldHi, title, TextEdge.None);

        if (body is not null)
        {
            Ink.DrawWrapped(Ink.Role.Small, new Vector2(at.X, at.Y + size + Tokens.Space.Sm), width - (inset * 2f), Tokens.Col.InkDim, body);
        }
    }

    private void DrawBar(ImDrawListPtr dl, ref MeterRow row, Vector2 min, Vector2 max, float delta)
    {
        Configuration.CombatTrackerConfig cfg = m_config.CombatTracker;
        float radius = Tokens.Metric.MeterBarRadius;
        float width = max.X - min.X;
        float height = max.Y - min.Y;

        // The empty part of the bar: the panel colour, a step darker than the card it sits on.
        dl.AddRectFilled(min, max, Tokens.Col.Faded(Tokens.Col.Panel, cfg.BackgroundOpacity), radius);

        // Sliding to the new length rather than jumping, at HamMeter's rate.
        row.Shown = cfg.SmoothBars
            ? row.Shown + ((row.Fraction - row.Shown) * Math.Clamp(delta * 10f, 0f, 1f))
            : row.Fraction;

        float fillRight = MathF.Round(min.X + (width * Math.Clamp(row.Shown, 0f, 1f)));

        if (fillRight > min.X)
        {
            uint colour = this.BarColour(row.JobId);
            BarStyle style = BarStyles.At(BarStyles.ForBar, cfg.BarStyleName);

            // Clipped rather than squeezed, like the health bar: a style has a shape along its
            // length, and drawing it into a shorter box would change that shape with the value.
            dl.PushClipRect(min, new Vector2(fillRight, max.Y), true);
            BarStyles.Draw(dl, style, min, max, Tokens.Col.Faded(colour, cfg.BarOpacity), radius);
            dl.PopClipRect();
        }

        float px = Tokens.WorldPx(cfg.TextSize);
        float inset = Tokens.Metric.MeterBarInset;
        float textTop = Ink.DigitTop(px, min.Y + (height * 0.5f));
        TextEdge edge = m_config.Edge;
        float x = min.X + inset;

        if (cfg.ShowRanks)
        {
            Ink.DrawScaledEdged(dl, px, new Vector2(x, textTop), Tokens.Col.HudInk, row.Rank, edge);
            x += Ink.MeasureWidth(px, row.Rank) + inset;
        }

        x = this.DrawJobMark(dl, ref row, x, min.Y, height, px, textTop, edge);

        float valueWidth = Ink.MeasureWidth(px, row.Value);
        float valueX = max.X - inset - valueWidth;
        Ink.DrawScaledEdged(dl, px, new Vector2(valueX, textTop), Tokens.Col.HudInk, row.Value, edge);

        // The name gives way to the number: a long name is cut, never written under it.
        dl.PushClipRect(min, new Vector2(MathF.Max(min.X, valueX - inset), max.Y), true);
        Ink.DrawScaledEdged(dl, px, new Vector2(x, textTop), Tokens.Col.HudInk, row.Name, edge);
        dl.PopClipRect();
    }

    private float DrawJobMark(ImDrawListPtr dl, ref MeterRow row, float x, float top, float height, float px, float textTop, TextEdge edge)
    {
        var mark = (MeterJobMark)m_config.CombatTracker.JobMark;

        // Limit Break and anything else without a job gets no mark, only its name.
        if (mark == MeterJobMark.Off || row.JobId == 0)
        {
            return x;
        }

        float inset = Tokens.Metric.MeterBarInset;

        if (mark == MeterJobMark.Icon)
        {
            float margin = Tokens.WorldPx(2f);
            float side = MathF.Max(1f, height - (margin * 2f));
            bool framed = m_config.CombatTracker.JobIconStyle == (int)JobIconStyle.Framed;
            ImTextureID icon = Icons.Handle(Jobs.IconId(row.JobId, framed));

            if (icon != default)
            {
                dl.AddImage(icon, new Vector2(x, top + margin), new Vector2(x + side, top + margin + side));
            }

            return x + side + inset;
        }

        Ink.DrawScaledEdged(dl, px, new Vector2(x, textTop), Tokens.Col.HudInk, row.Letters, edge);

        // A fixed column, so the names line up whatever the three letters are.
        return x + Ink.MeasureWidth(px, LettersColumn) + inset;
    }

    /// <summary>
    /// The corner a mouse can pull, and the lock beside it — both only while the pointer is on
    /// the meter, so neither sits in view during a fight. A locked meter shows the lock alone,
    /// which is the way back.
    /// </summary>
    private void DrawCorner(ImDrawListPtr dl, Vector2 pos, Vector2 max)
    {
        Configuration.CombatTrackerConfig cfg = m_config.CombatTracker;
        bool resizing = m_resizeAxis != 0;

        if (EditMode.IsActive || (!ImGui.IsMouseHoveringRect(pos, max, false) && !resizing))
        {
            return;
        }

        float grip = Tokens.Metric.MeterGrip;
        float edge = Tokens.WorldPx(3f);
        Vector2 gripMin = new(max.X - edge - grip, max.Y - edge - grip);
        Vector2 lockMin = cfg.Locked ? gripMin : new Vector2(gripMin.X - grip - Tokens.WorldPx(4f), gripMin.Y);

        ImGui.SetCursorScreenPos(lockMin);
        ImGui.InvisibleButton(IdLock, new Vector2(grip, grip));
        bool lockHovered = ImGui.IsItemHovered();
        Chrome.ShowHand(lockHovered);

        if (ImGui.IsItemClicked())
        {
            cfg.Locked = !cfg.Locked;
            m_config.MarkDirty();
        }

        if (lockHovered)
        {
            Chrome.Tooltip(null, cfg.Locked ? Strings.MeterLocked : Strings.MeterUnlocked);
        }

        LineIcons.Draw(dl, cfg.Locked ? LineIcons.Lock : LineIcons.LockOpen, lockMin, grip, lockHovered ? Tokens.Col.GoldHi : Tokens.Col.InkDim);

        if (cfg.Locked)
        {
            return;
        }

        ImGui.SetCursorScreenPos(gripMin);
        ImGui.InvisibleButton(IdGrip, new Vector2(grip, grip));
        bool gripHovered = ImGui.IsItemHovered();
        ImGuiIOPtr io = ImGui.GetIO();

        if (ImGui.IsItemActivated())
        {
            m_resizeFrom = io.MousePos;
            m_resizeSize = max - pos;
            m_resizeAxis = 3;
        }

        if (ImGui.IsItemActive())
        {
            this.Resize(io, pos);
        }
        else
        {
            m_resizeAxis = 0;
        }

        if (gripHovered || ImGui.IsItemActive())
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeNwse);

            if (!ImGui.IsItemActive())
            {
                Chrome.Tooltip(null, Strings.MeterGripTooltip);
            }
        }

        // Three short diagonals in the corner, the usual sign for "pull here".
        uint ink = gripHovered || ImGui.IsItemActive() ? Tokens.Col.GoldHi : Tokens.Col.InkDim;
        float line = Tokens.WorldLine(1f);
        Vector2 corner = gripMin + new Vector2(grip, grip);

        for (int i = 1; i <= 3; i++)
        {
            float reach = grip * i / 3f;
            dl.AddLine(new Vector2(corner.X - reach, corner.Y), new Vector2(corner.X, corner.Y - reach), ink, line);
        }
    }

    /// <summary>
    /// The corner under the mouse: the top left stays where it is, the meter never gets
    /// smaller than it can hold or bigger than the screen, and Shift keeps one side as it was.
    /// </summary>
    private void Resize(ImGuiIOPtr io, Vector2 pos)
    {
        Configuration.CombatTrackerConfig cfg = m_config.CombatTracker;
        Vector2 moved = io.MousePos - m_resizeFrom;

        // Shift picks the side the pointer has moved along furthest the moment it goes down,
        // and keeps it until it is let go.
        if (io.KeyShift)
        {
            if (m_resizeAxis == 3)
            {
                m_resizeAxis = MathF.Abs(moved.X) >= MathF.Abs(moved.Y) ? 1 : 2;
            }
        }
        else
        {
            m_resizeAxis = 3;
        }

        float width = (m_resizeAxis & 1) != 0 ? m_resizeSize.X + moved.X : m_resizeSize.X;
        float height = (m_resizeAxis & 2) != 0 ? m_resizeSize.Y + moved.Y : m_resizeSize.Y;

        Vector2 screen = io.DisplaySize;
        width = Math.Clamp(width, Configuration.MinMeterWidth, MathF.Max(Configuration.MinMeterWidth, screen.X - pos.X));
        height = Math.Clamp(height, Configuration.MinMeterHeight, MathF.Max(Configuration.MinMeterHeight, screen.Y - pos.Y));

        cfg.Width = MathF.Round(width);
        cfg.Height = MathF.Round(height);
        m_config.MarkDirty();
    }

    // --- the three menus ------------------------------------------------------

    private void DrawPopups()
    {
        PushPopupStyle();

        if (ImGui.BeginPopup(PopupMetric))
        {
            CombatMetric[] all = CombatMetrics.All;

            for (int i = 0; i < all.Length; i++)
            {
                if (MenuRow(MetricIds[i], CombatMetrics.Name(all[i]), m_config.CombatTracker.Metric == (int)all[i]))
                {
                    m_config.CombatTracker.Metric = (int)all[i];
                    m_config.MarkDirty();
                    ImGui.CloseCurrentPopup();
                }
            }

            ImGui.EndPopup();
        }

        if (ImGui.BeginPopup(PopupFights))
        {
            if (MenuRow(FightIds[0], Strings.MeterViewCurrent, m_view == ViewCurrent))
            {
                m_view = ViewCurrent;
                ImGui.CloseCurrentPopup();
            }

            if (MenuRow(FightIds[1], Strings.MeterViewOverall, m_view == ViewOverall))
            {
                m_view = ViewOverall;
                ImGui.CloseCurrentPopup();
            }

            // Newest first, the way anybody looks for the pull they just had.
            for (int i = m_fightLabels.Count - 1; i >= 0; i--)
            {
                if (MenuRow(FightIds[i + 2], m_fightLabels[i], m_view == i))
                {
                    m_view = i;
                    ImGui.CloseCurrentPopup();
                }
            }

            ImGui.EndPopup();
        }

        if (ImGui.BeginPopup(PopupReset))
        {
            Vector2 at = ImGui.GetCursorScreenPos();
            Ink.Draw(ImGui.GetWindowDrawList(), Ink.Role.Body, at, Tokens.Col.Ink, Strings.MeterResetQuestion);

            float buttonY = at.Y + Ink.LineHeight(Ink.Role.Body) + Tokens.Space.Md;
            float confirmWidth = Chrome.MeasureButton(Strings.MeterResetConfirm);

            if (Chrome.Button(IdConfirm, Strings.MeterResetConfirm, at.X, buttonY, true, null, true))
            {
                this.Reset();
                ImGui.CloseCurrentPopup();
            }

            if (Chrome.Button(IdCancel, Strings.MeterResetCancel, at.X + confirmWidth + Tokens.Space.Md, buttonY, true))
            {
                ImGui.CloseCurrentPopup();
            }

            // Room for what was drawn by hand, so the popup sizes itself around it.
            float wide = MathF.Max(
                Ink.Measure(Ink.Role.Body, Strings.MeterResetQuestion).X,
                confirmWidth + Tokens.Space.Md + Chrome.MeasureButton(Strings.MeterResetCancel));
            ImGui.SetCursorScreenPos(at);
            ImGui.Dummy(new Vector2(wide, buttonY + Tokens.Metric.ButtonHeight - at.Y));

            ImGui.EndPopup();
        }

        PopPopupStyle();
    }

    /// <summary>One line of a menu: its label, a hover, and the gold of the one that is on.</summary>
    private static bool MenuRow(string id, string label, bool selected)
    {
        float height = Chrome.RowHeight();
        float width = MathF.Max(Tokens.Metric.ControlWidth, Ink.Measure(Ink.Role.Body, label).X + (Tokens.Space.Lg * 2f));
        Vector2 min = ImGui.GetCursorScreenPos();

        ImGui.InvisibleButton(id, new Vector2(width, height));
        bool hovered = ImGui.IsItemHovered();
        bool clicked = ImGui.IsItemClicked();
        Chrome.ShowHand(hovered);

        ImDrawListPtr dl = ImGui.GetWindowDrawList();

        if (hovered)
        {
            dl.AddRectFilled(min, min + new Vector2(width, height), Tokens.Col.RowHover, Tokens.Radius.Small);
        }

        Ink.Draw(
            dl,
            Ink.Role.Body,
            new Vector2(min.X + Tokens.Space.Md, Chrome.CenterY(min.Y, height, Ink.Role.Body)),
            selected ? Tokens.Col.GoldHi : Tokens.Col.Ink,
            label);

        return clicked;
    }

    private static void PushPopupStyle()
    {
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(Tokens.Space.Md, Tokens.Space.Md));
        ImGui.PushStyleVar(ImGuiStyleVar.PopupRounding, Tokens.Radius.Control);
        ImGui.PushStyleVar(ImGuiStyleVar.PopupBorderSize, Tokens.Line(1f));
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(0f, Tokens.Space.Xs));
        ImGui.PushStyleColor(ImGuiCol.PopupBg, Tokens.Col.PopupBg);
        ImGui.PushStyleColor(ImGuiCol.Border, Tokens.Col.PopupEdge);
    }

    private static void PopPopupStyle()
    {
        ImGui.PopStyleColor(2);
        ImGui.PopStyleVar(4);
    }

    private static bool IconButton(string id, LineIcon icon, float x, float y, float size, string tooltip)
    {
        Vector2 min = new(x, y);
        ImGui.SetCursorScreenPos(min);
        ImGui.InvisibleButton(id, new Vector2(size, size));
        bool hovered = ImGui.IsItemHovered();
        bool clicked = ImGui.IsItemClicked();
        Chrome.ShowHand(hovered);

        if (hovered)
        {
            Chrome.Tooltip(null, tooltip);
        }

        LineIcons.Draw(ImGui.GetWindowDrawList(), icon, min, size, hovered ? Tokens.Col.GoldHi : Tokens.Col.Ink);
        return clicked;
    }

    // --- the data -------------------------------------------------------------

    /// <summary>The fight the meter is set to show: the made-up one, every fight together, one from the list, or the current one.</summary>
    private CombatEvent? Shown()
    {
        if (this.TestMode)
        {
            return m_testEvent ??= CombatTestData.Build();
        }

        if (m_view == ViewOverall)
        {
            return m_client.GetOverall();
        }

        if (m_view >= 0)
        {
            CombatEvent? past = m_client.GetPast(m_view);

            if (past is not null)
            {
                return past;
            }

            // The fight the list pointed at is gone — a reset, or history running over.
            m_view = ViewCurrent;
        }

        return m_client.Current;
    }

    /// <summary>
    /// Everything the bars and the title say, worked out once per fight update. Sorting,
    /// formatting and the job lookup all happen here; the draw only reads the result.
    /// </summary>
    private void Rebuild(CombatEvent? shown, CombatMetric metric, bool shortNumbers)
    {
        // The rows on show become the ones before, so a bar that stays can keep its motion.
        (m_previous, m_rows) = (m_rows, m_previous);
        int previousCount = m_count;
        m_count = 0;

        string view = this.TestMode || m_view == ViewCurrent
            ? Strings.MeterViewCurrent
            : m_view == ViewOverall ? Strings.MeterViewOverall : Strings.MeterViewFight;
        string duration = shown?.Encounter?.Duration ?? "00:00";
        m_titleMain = CombatMetrics.Name(metric);
        m_titleSub = view + " · " + duration;

        if (shown?.Combatants is null)
        {
            return;
        }

        string? you = Services.Objects.LocalPlayer?.Name.TextValue;

        foreach (Combatant c in shown.Combatants.Values)
        {
            if (m_count >= MaxRows || string.IsNullOrEmpty(c.Name))
            {
                continue;
            }

            uint jobId = Jobs.FromAbbreviation(c.Job);
            bool limitBreak = string.Equals(c.Job, LimitBreak, StringComparison.OrdinalIgnoreCase)
                || string.Equals(c.Name, LimitBreak, StringComparison.OrdinalIgnoreCase);

            // Players and the limit break. Enemies and pets come through with no job.
            if (jobId == 0 && !limitBreak)
            {
                continue;
            }

            float value = CombatMetrics.Value(c, metric);

            // Sorted as they go in: a party is eight lines, and an insertion is cheaper than
            // anything that would sort a list of them afterwards.
            int at = m_count;
            while (at > 0 && m_rows[at - 1].Amount < value)
            {
                m_rows[at] = m_rows[at - 1];
                at--;
            }

            // IINACT calls the local player "YOU"; the meter shows their name.
            string name = string.Equals(c.Name, "YOU", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(you)
                ? you
                : c.Name;

            m_rows[at] = new MeterRow
            {
                Name = name,
                JobId = jobId,
                Letters = c.Job.ToUpperInvariant(),
                Amount = value,
                Value = Format(c, metric, value, shortNumbers),
            };

            m_count++;
        }

        float top = m_count > 0 ? MathF.Max(1f, m_rows[0].Amount) : 1f;

        for (int i = 0; i < m_count; i++)
        {
            ref MeterRow row = ref m_rows[i];
            row.Rank = Ranks[i];
            row.Fraction = Math.Clamp(row.Amount / top, 0f, 1f);

            // Carried over by name: the same person keeps sliding from where their bar was.
            // A new line starts at its value, which is what HamMeter did.
            row.Shown = row.Fraction;
            for (int p = 0; p < previousCount; p++)
            {
                if (string.Equals(m_previous[p].Name, row.Name, StringComparison.Ordinal))
                {
                    row.Shown = m_previous[p].Shown;
                    break;
                }
            }
        }
    }

    private void BuildFightLabels()
    {
        m_fightLabels.Clear();
        List<CombatEvent> past = m_client.SnapshotPast();

        for (int i = 0; i < past.Count && i < IinactClient.MaxHistory; i++)
        {
            Encounter? encounter = past[i].Encounter;
            m_fightLabels.Add((encounter?.Title ?? string.Empty) + " (" + (encounter?.Duration ?? "00:00") + ")");
        }
    }

    private uint BarColour(uint jobId)
    {
        if (jobId == 0)
        {
            return Tokens.Col.InkDim;
        }

        return m_config.CombatTracker.ColourMode == 1
            ? Jobs.RoleColour(Jobs.Role(jobId))
            : Jobs.Colour(jobId);
    }

    private static string Format(Combatant c, CombatMetric metric, float value, bool shortNumbers)
    {
        if (CombatMetrics.IsCount(metric))
        {
            return ((int)value).ToString(CultureInfo.InvariantCulture);
        }

        string main = Number(value, shortNumbers);
        return CombatMetrics.HasRate(metric)
            ? main + "  (" + Number(CombatMetrics.Rate(c, metric), shortNumbers) + ")"
            : main;
    }

    private static string Number(float value, bool shortNumbers)
    {
        if (!shortNumbers)
        {
            return ((long)value).ToString("N0", CultureInfo.InvariantCulture);
        }

        if (value >= 1_000_000f)
        {
            return (value / 1_000_000f).ToString("0.0", CultureInfo.InvariantCulture) + "M";
        }

        return value >= 1000f
            ? (value / 1000f).ToString("0.0", CultureInfo.InvariantCulture) + "K"
            : value.ToString("0", CultureInfo.InvariantCulture);
    }

    /// <summary>The echo line IINACT reads as "close this fight". IINACT does not know "clear", only "end".</summary>
    private static void EndFight()
    {
        try
        {
            Services.Chat.Print(new XivChatEntry { Message = "end", Type = XivChatType.Echo });
        }
        catch (Exception ex)
        {
            Services.Log.Error(ex, "Combat tracker: could not ask IINACT to end the fight.");
        }
    }

    private static string[] BuildRanks()
    {
        var ranks = new string[MaxRows];
        for (int i = 0; i < ranks.Length; i++)
        {
            ranks[i] = (i + 1).ToString(CultureInfo.InvariantCulture) + ".";
        }

        return ranks;
    }

    private static string[] BuildIds(string prefix, int count)
    {
        var ids = new string[count];
        for (int i = 0; i < ids.Length; i++)
        {
            ids[i] = prefix + i.ToString(CultureInfo.InvariantCulture);
        }

        return ids;
    }

    /// <summary>What IINACT calls the limit break, which is a line on the meter without a job.</summary>
    private const string LimitBreak = "Limit Break";

    /// <summary>The widest three letters, so the letters column is one width for every job.</summary>
    private const string LettersColumn = "WWW";

    /// <summary>One line of the meter, with everything it says already written out.</summary>
    private struct MeterRow
    {
        public string Name;
        public string Rank;
        public string Letters;
        public string Value;
        public uint JobId;

        /// <summary>The raw figure the rows are sorted by.</summary>
        public float Amount;

        /// <summary>How long the bar should be, against the top line.</summary>
        public float Fraction;

        /// <summary>How long it is drawn right now, while it slides.</summary>
        public float Shown;
    }
}
