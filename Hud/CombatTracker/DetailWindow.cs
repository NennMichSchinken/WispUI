using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using WispUI.Core;
using WispUI.Data;
using WispUI.Interface;
using WispUI.Interface.Widgets;
using WispUI.Localization;
using WispUI.Style;

namespace WispUI.Hud.CombatTracker;

/// <summary>
/// The meter's details: one player in the fight the meter shows — rates, every skill, and a
/// chart of the fight with the party and the boss's health. Opened from an opened bar's
/// "Details"; a chip at the top, or a bar in the meter, switches the player.
/// <para>
/// Ported from HamMeter-Aion2 (our own plugin), with its per-frame LINQ and strings moved
/// into <see cref="DetailData"/>, which is built once per fight update (§7.1).
/// </para>
/// <para>
/// 🔴 As wide as its content (Florian, 2026-10-29: "the details are there for details").
/// Every column of the skill table and every tile is measured at build time and the window
/// takes the widest; nothing is clipped. It grows while open and never shrinks under the
/// pointer, so switching players does not make it jump narrower.
/// </para>
/// </summary>
internal sealed class DetailWindow
{
    private const string IdWindow = "##wisp-meter-details";
    private const string IdDrag = "##wisp-details-move";
    private const string IdClose = "##wisp-details-close";
    private const string IdDamage = "##wisp-details-damage";
    private const string IdHeal = "##wisp-details-heal";
    private const string IdParty = "##wisp-details-party";
    private const string IdList = "##wisp-details-list";
    private const int Columns = 7;

    private const ImGuiWindowFlags WindowFlags =
        ImGuiWindowFlags.NoDecoration
        | ImGuiWindowFlags.NoMove
        | ImGuiWindowFlags.NoBackground
        | ImGuiWindowFlags.NoSavedSettings
        | ImGuiWindowFlags.NoFocusOnAppearing
        | ImGuiWindowFlags.NoNav
        | ImGuiWindowFlags.NoScrollbar
        | ImGuiWindowFlags.NoScrollWithMouse;

    private static readonly string[] ChipIds = BuildIds("##wisp-details-chip", 64);
    private static readonly LineIcon CloseIcon = LineIcons.X;
    private static readonly LineIcon CheckIcon = LineIcons.Check;

    private readonly Configuration m_config;
    private readonly DetailData m_data = new();

    private CombatEvent? m_builtFrom;
    private string m_builtPlayer = string.Empty;
    private bool m_builtHeal;
    private bool m_builtShort;
    private float m_builtText = -1f;
    private float m_builtScale = -1f;

    private bool m_showParty = true;
    private Vector2 m_grab;

    // --- the layout, measured at build ------------------------------------------
    private float m_width;
    private float m_nameColumn;
    private readonly float[] m_columns = new float[Columns];
    private float m_tileWidth;
    private float m_axisWidth;
    private string m_axisMaxMine = string.Empty;
    private string m_axisHalfMine = string.Empty;
    private string m_axisMaxAll = string.Empty;
    private string m_axisHalfAll = string.Empty;
    private readonly List<Vector2> m_chipAt = new();
    private readonly List<float> m_chipWidth = new();
    private float m_chipsHeight;
    private int m_legendRows = 1;

    public DetailWindow(Configuration config) => m_config = config;

    public bool Visible { get; private set; }

    /// <summary>The player shown, by IINACT's name ("YOU" for the local player).</summary>
    public string PlayerKey { get; private set; } = string.Empty;

    public bool Heal { get; private set; }

    public void Show(string playerKey, bool heal)
    {
        if (!this.Visible)
        {
            m_width = 0f;
        }

        this.PlayerKey = playerKey;
        this.Heal = heal;
        this.Visible = true;
    }

    /// <summary>Switches the player without opening the window.</summary>
    public void ShowPlayer(string playerKey) => this.PlayerKey = playerKey;

    public void Close() => this.Visible = false;

    /// <summary>Rebuilds when the fight data, the player or a setting changed. Cheap otherwise.</summary>
    public void Collect(CombatEvent? shown, Func<List<CombatEvent>>? overallParts, string? you, CombatLog log)
    {
        if (!this.Visible || shown is null)
        {
            return;
        }

        Configuration.CombatTrackerConfig cfg = m_config.CombatTracker;

        if (ReferenceEquals(shown, m_builtFrom)
            && string.Equals(this.PlayerKey, m_builtPlayer, StringComparison.Ordinal)
            && this.Heal == m_builtHeal
            && cfg.ShortNumbers == m_builtShort
            && cfg.TextSize == m_builtText
            && Tokens.WorldPx(1f) == m_builtScale)
        {
            return;
        }

        m_data.Build(shown, overallParts?.Invoke(), this.PlayerKey, you, this.Heal, cfg.ShortNumbers, log);
        m_builtFrom = shown;
        m_builtPlayer = this.PlayerKey;
        m_builtHeal = this.Heal;
        m_builtShort = cfg.ShortNumbers;
        m_builtText = cfg.TextSize;
        m_builtScale = Tokens.WorldPx(1f);

        this.Measure();
    }

    public void Draw(Vector2 meterPos, Vector2 meterSize)
    {
        if (!this.Visible || m_builtFrom is null || EditMode.IsActive)
        {
            return;
        }

        Configuration.CombatTrackerConfig cfg = m_config.CombatTracker;
        float height = this.Height();
        Vector2 size = new(m_width, height);
        Vector2 pos;

        if (cfg.DetailsPlaced)
        {
            pos = new Vector2(Tokens.WorldPx(cfg.DetailsX), Tokens.WorldPx(cfg.DetailsY));
        }
        else
        {
            // Beside the meter, on whichever side has room.
            Vector2 screen = ImGui.GetIO().DisplaySize;
            float gap = Tokens.Metric.DetailGap;
            float x = meterPos.X + meterSize.X + gap;

            if (x + m_width > screen.X)
            {
                x = MathF.Max(0f, meterPos.X - gap - m_width);
            }

            pos = new Vector2(x, MathF.Max(0f, MathF.Min(meterPos.Y, screen.Y - height)));
        }

        ImGui.SetNextWindowPos(pos);
        ImGui.SetNextWindowSize(size);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0f);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, Vector2.Zero);

        if (ImGui.Begin(IdWindow, WindowFlags))
        {
            this.DrawWindow(pos, size);
        }

        ImGui.End();
        ImGui.PopStyleVar(3);
    }

    // --- measuring -------------------------------------------------------------

    private float Body => Tokens.WorldPx(m_config.CombatTracker.TextSize);

    private float Small => Tokens.WorldPx(MathF.Max(Configuration.MinTextSize, m_config.CombatTracker.TextSize - 2f));

    private float Big => Tokens.WorldPx(m_config.CombatTracker.TextSize + 4f);

    private float TitlePx => Tokens.WorldPx(m_config.CombatTracker.TitleTextSize);

    private float TitleHeight => MathF.Max(Tokens.WorldPx(m_config.CombatTracker.TitleHeight), this.TitlePx + (Tokens.Metric.DetailPad * 2f));

    private float RowHeight => this.Body + Tokens.WorldPx(10f);

    private float ChipHeight => this.Body + Tokens.WorldPx(12f);

    private float TileHeight => this.Small + this.Big + (Tokens.Metric.DetailPad * 2f) + Tokens.WorldPx(4f);

    private float ControlHeight => this.Body + Tokens.WorldPx(10f);

    private string SecondHeader => this.Heal ? Strings.DetailColOverheal : Strings.DetailColDirect;

    private string ColumnHeader(int i) => i switch
    {
        0 => Strings.DetailColAmount,
        1 => Strings.DetailColShare,
        2 => Strings.DetailColHits,
        3 => Strings.DetailColCrit,
        4 => this.SecondHeader,
        5 => Strings.DetailColAverage,
        _ => Strings.DetailColBiggest,
    };

    private static string ColumnValue(in SkillLine line, int i) => i switch
    {
        0 => line.AmountText,
        1 => line.Share,
        2 => line.Hits,
        3 => line.Crit,
        4 => line.Second,
        5 => line.Average,
        _ => line.Biggest,
    };

    /// <summary>
    /// Every width the window needs, from the texts it will draw. Here and not in the draw:
    /// a measure is a font lookup per string, and these only change with the data.
    /// </summary>
    private void Measure()
    {
        float body = this.Body;
        float small = this.Small;
        float big = this.Big;
        float pad = Tokens.Metric.DetailPad;
        float gap = Tokens.Metric.DetailGap;
        float colGap = Tokens.Metric.DetailColumnGap;
        float smallGap = Tokens.Metric.DetailSmallGap;

        // The title: icon, name, what and where, the two totals, the close button.
        float iconSide = this.TitleHeight - (pad * 2f);
        float header = pad + iconSide + smallGap
            + Ink.MeasureNote(this.TitlePx, m_data.Name) + smallGap
            + Ink.MeasureNote(body, m_data.Subtitle) + (colGap * 2f)
            + Ink.MeasureNote(small, this.Heal ? Strings.DetailTotalHeal : Strings.DetailTotalDamage) + smallGap
            + Ink.MeasureNote(body, m_data.TotalValue) + colGap
            + Ink.MeasureNote(small, this.Heal ? Strings.DetailRateHeal : Strings.DetailRateDamage) + smallGap
            + Ink.MeasureNote(body, m_data.RateValue) + colGap + iconSide + pad;

        // The tiles: one width for all eight, the widest label or value.
        float tile = 0f;
        foreach (RateTile t in m_data.Tiles)
        {
            tile = MathF.Max(tile, MathF.Max(Ink.MeasureNote(small, t.Label ?? string.Empty), Ink.MeasureNote(big, t.Value ?? string.Empty)));
        }

        m_tileWidth = tile + (pad * 2f);
        float tiles = (m_tileWidth * 4f) + (gap * 3f);

        // The table: each column as wide as its widest entry, the header included.
        float names = Ink.MeasureNote(small, Strings.DetailColSkill);
        for (int i = 0; i < Columns; i++)
        {
            m_columns[i] = Ink.MeasureNote(small, this.ColumnHeader(i));
        }

        foreach (SkillLine line in m_data.Skills)
        {
            names = MathF.Max(names, Ink.MeasureNote(body, line.Name));

            for (int i = 0; i < Columns; i++)
            {
                m_columns[i] = MathF.Max(m_columns[i], Ink.MeasureNote(body, ColumnValue(line, i)));
            }
        }

        float rowIcon = this.RowHeight - Tokens.WorldPx(4f);
        m_nameColumn = rowIcon + smallGap + names;
        float table = pad + m_nameColumn + colGap + Tokens.Metric.DetailBarWidth + pad + Tokens.Metric.DetailScrollbar;
        for (int i = 0; i < Columns; i++)
        {
            table += colGap + m_columns[i];
        }

        // The chart's controls and its axis.
        float controls = Ink.MeasureNote(small, Strings.DetailOverTheFight) + gap
            + this.SegmentWidth(Strings.DetailDamage) + this.SegmentWidth(Strings.DetailHealing) + gap
            + this.ControlHeight + smallGap + Ink.MeasureNote(body, Strings.DetailShowParty);

        m_axisMaxMine = CombatTrackerElement.Number(m_data.MaxMine, true);
        m_axisHalfMine = CombatTrackerElement.Number(m_data.MaxMine * 0.5f, true);
        m_axisMaxAll = CombatTrackerElement.Number(m_data.MaxAll, true);
        m_axisHalfAll = CombatTrackerElement.Number(m_data.MaxAll * 0.5f, true);
        m_axisWidth = MathF.Max(Ink.MeasureNote(small, m_axisMaxAll), Ink.MeasureNote(small, "0")) + smallGap;

        float content = MathF.Max(MathF.Max(tiles, table - (pad * 2f)), controls);
        float width = MathF.Max(Tokens.Metric.DetailMinWidth, MathF.Max(header, content + (pad * 2f)));
        m_width = MathF.Ceiling(MathF.Max(m_width, width));

        // The chips, wrapped to that width.
        m_chipAt.Clear();
        m_chipWidth.Clear();
        float inner = m_width - (pad * 2f);
        float x = 0f;
        float y = 0f;
        float chipH = this.ChipHeight;

        foreach (PlayerChip chip in m_data.Chips)
        {
            float w = Tokens.WorldPx(20f) + Ink.MeasureNote(body, chip.Name) + smallGap + Ink.MeasureNote(small, chip.Amount) + pad;

            if (x > 0f && x + w > inner)
            {
                x = 0f;
                y += chipH + smallGap;
            }

            m_chipAt.Add(new Vector2(x, y));
            m_chipWidth.Add(w);
            x += w + smallGap;
        }

        m_chipsHeight = m_data.Chips.Count > 0 ? y + chipH : 0f;
        m_legendRows = this.LegendRows();
    }

    private float SegmentWidth(string label) => Ink.MeasureNote(this.Body, label) + (Tokens.Metric.DetailPad * 2f);

    private int VisibleRows => Math.Clamp(m_data.Skills.Count, 1, Tokens.Metric.DetailVisibleRows);

    private float Height()
    {
        float pad = Tokens.Metric.DetailPad;
        float gap = Tokens.Metric.DetailGap;
        float h = this.TitleHeight + pad;

        if (m_chipsHeight > 0f)
        {
            h += m_chipsHeight + gap;
        }

        h += (this.TileHeight * 2f) + Tokens.Metric.DetailSmallGap + gap;
        h += this.Small + Tokens.WorldPx(6f) + (this.VisibleRows * this.RowHeight) + gap;
        h += this.ControlHeight + Tokens.Metric.DetailSmallGap + Tokens.Metric.DetailChartHeight + Tokens.Metric.DetailSmallGap;
        h += (m_legendRows * this.Small) + ((m_legendRows - 1) * Tokens.Metric.DetailSmallGap) + pad;
        return MathF.Ceiling(h);
    }

    // --- drawing -----------------------------------------------------------------

    private void DrawWindow(Vector2 pos, Vector2 size)
    {
        Configuration.CombatTrackerConfig cfg = m_config.CombatTracker;
        ImDrawListPtr dl = ImGui.GetWindowDrawList();
        float radius = Tokens.Metric.MeterRadius;
        Vector2 max = pos + size;

        if (ImGui.IsWindowHovered(ImGuiHoveredFlags.RootAndChildWindows | ImGuiHoveredFlags.AllowWhenBlockedByActiveItem))
        {
            NativeUi.KeepGameCursor();
        }

        dl.AddRectFilled(pos, max, Tokens.Col.Panel, radius, ImDrawFlags.RoundCornersAll);

        float titleBottom = pos.Y + this.TitleHeight;
        this.DrawTitle(dl, pos, max.X, titleBottom);

        float pad = Tokens.Metric.DetailPad;
        float gap = Tokens.Metric.DetailGap;
        float left = pos.X + pad;
        float width = size.X - (pad * 2f);
        float y = titleBottom + pad;

        if (m_chipsHeight > 0f)
        {
            this.DrawChips(dl, new Vector2(left, y));
            y += m_chipsHeight + gap;
        }

        this.DrawTiles(dl, new Vector2(left, y), width);
        y += (this.TileHeight * 2f) + Tokens.Metric.DetailSmallGap + gap;

        y = this.DrawSkills(dl, new Vector2(left, y), width) + gap;
        this.DrawChart(dl, new Vector2(left, y), width);

        if (cfg.ShowRim)
        {
            ConfigWindow.DrawWindowEdge(dl, pos, size, Tokens.WorldLine(1f), radius);
        }
    }

    private void DrawTitle(ImDrawListPtr dl, Vector2 pos, float right, float bottom)
    {
        Configuration.CombatTrackerConfig cfg = m_config.CombatTracker;
        float pad = Tokens.Metric.DetailPad;
        float smallGap = Tokens.Metric.DetailSmallGap;
        float colGap = Tokens.Metric.DetailColumnGap;
        float body = this.Body;
        float small = this.Small;

        Chrome.VerticalFill(
            dl,
            pos,
            new Vector2(right, bottom),
            Tokens.Col.TitleBarTop,
            Tokens.Col.TitleBar,
            Tokens.Metric.MeterRadius,
            ImDrawFlags.RoundCornersTop,
            Tokens.Metric.MeterTitleFade);

        float line = Tokens.WorldLine(1f);
        Chrome.Rule(dl, pos.X + pad, right - pad, bottom - (line * Tokens.Col.TitleRule.Length), line, Tokens.Metric.MeterTitleFade);

        float side = this.TitleHeight - (pad * 2f);
        float x = pos.X + pad;
        float top = pos.Y + pad;

        bool framed = cfg.JobIconStyle == (int)JobIconStyle.Framed;
        ImTextureID icon = Icons.Handle(Jobs.IconId(m_data.JobId, framed));
        if (icon != default)
        {
            dl.AddImage(icon, new Vector2(x, top), new Vector2(x + side, top + side));
        }

        x += side + smallGap;
        float mid = pos.Y + ((bottom - pos.Y) * 0.5f);
        Ink.DrawNote(dl, this.TitlePx, new Vector2(x, MathF.Round(mid - (this.TitlePx * 0.5f))), Tokens.Col.Heading, m_data.Name, TextEdge.None);
        x += Ink.MeasureNote(this.TitlePx, m_data.Name) + smallGap;
        Ink.DrawNote(dl, body, new Vector2(x, MathF.Round(mid - (body * 0.5f))), Tokens.Col.InkDim, m_data.Subtitle, TextEdge.None);

        // Close, then the two totals right to left.
        float closeX = right - pad - side;
        ImGui.SetCursorScreenPos(new Vector2(closeX, top));
        ImGui.InvisibleButton(IdClose, new Vector2(side, side));
        bool closeHovered = ImGui.IsItemHovered();
        Chrome.ShowHand(closeHovered);
        if (ImGui.IsItemClicked())
        {
            this.Close();
        }

        LineIcons.Draw(dl, CloseIcon, new Vector2(closeX, top), side, closeHovered ? Tokens.Col.GoldHi : Tokens.Col.Ink);

        float rx = closeX - colGap;
        rx = DrawTotal(dl, rx, mid, body, small, this.Heal ? Strings.DetailRateHeal : Strings.DetailRateDamage, m_data.RateValue);
        rx = DrawTotal(dl, rx - colGap, mid, body, small, this.Heal ? Strings.DetailTotalHeal : Strings.DetailTotalDamage, m_data.TotalValue);

        // The title bar is the handle, short of the close button.
        ImGui.SetCursorScreenPos(pos);
        ImGui.InvisibleButton(IdDrag, new Vector2(MathF.Max(1f, closeX - pos.X - smallGap), bottom - pos.Y));
        Vector2 mouse = ImGui.GetIO().MousePos;

        if (ImGui.IsItemActivated())
        {
            m_grab = mouse - pos;
        }

        if (ImGui.IsItemActive())
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeAll);
            Vector2 to = mouse - m_grab;
            float unit = Tokens.WorldPx(1f);
            cfg.DetailsPlaced = true;
            cfg.DetailsX = MathF.Round(to.X / unit);
            cfg.DetailsY = MathF.Round(to.Y / unit);
            m_config.MarkDirty();
        }
    }

    /// <summary>"Damage 2.94M", right-aligned at <paramref name="right"/>; returns where it starts.</summary>
    private static float DrawTotal(ImDrawListPtr dl, float right, float mid, float body, float small, string label, string value)
    {
        float valueWidth = Ink.MeasureNote(body, value);
        float x = right - valueWidth;
        Ink.DrawNote(dl, body, new Vector2(x, MathF.Round(mid - (body * 0.5f))), Tokens.Col.Ink, value, TextEdge.None);
        x -= Tokens.Metric.DetailSmallGap + Ink.MeasureNote(small, label);
        Ink.DrawNote(dl, small, new Vector2(x, MathF.Round(mid - (small * 0.5f))), Tokens.Col.InkDim, label, TextEdge.None);
        return x;
    }

    private void DrawChips(ImDrawListPtr dl, Vector2 origin)
    {
        float body = this.Body;
        float small = this.Small;
        float h = this.ChipHeight;
        float radius = Tokens.Radius.Small;
        float smallGap = Tokens.Metric.DetailSmallGap;

        for (int i = 0; i < m_data.Chips.Count && i < m_chipAt.Count && i < ChipIds.Length; i++)
        {
            PlayerChip chip = m_data.Chips[i];
            Vector2 min = origin + m_chipAt[i];
            Vector2 max = min + new Vector2(m_chipWidth[i], h);

            ImGui.SetCursorScreenPos(min);
            ImGui.InvisibleButton(ChipIds[i], max - min);
            bool hovered = ImGui.IsItemHovered();
            Chrome.ShowHand(hovered);

            if (ImGui.IsItemClicked())
            {
                this.PlayerKey = chip.Key;
            }

            bool selected = string.Equals(chip.Name, m_data.Name, StringComparison.Ordinal);
            dl.AddRectFilled(min, max, selected ? Tokens.Col.NavSelected : hovered ? Tokens.Col.RowHover : Tokens.Col.GroupBg, radius);
            dl.AddRect(min, max, selected ? Tokens.Col.Gold : Tokens.Col.Hairline, radius, ImDrawFlags.RoundCornersAll, Tokens.WorldLine(1f));

            float mid = min.Y + (h * 0.5f);
            dl.AddCircleFilled(new Vector2(min.X + Tokens.WorldPx(10f), mid), Tokens.WorldPx(4f), Jobs.Colour(chip.JobId));
            float x = min.X + Tokens.WorldPx(20f);
            Ink.DrawNote(dl, body, new Vector2(x, MathF.Round(mid - (body * 0.5f))), Tokens.Col.Ink, chip.Name, TextEdge.None);
            x += Ink.MeasureNote(body, chip.Name) + smallGap;
            Ink.DrawNote(dl, small, new Vector2(x, MathF.Round(mid - (small * 0.5f))), Tokens.Col.InkDim, chip.Amount, TextEdge.None);
        }
    }

    private void DrawTiles(ImDrawListPtr dl, Vector2 origin, float width)
    {
        float gap = Tokens.Metric.DetailGap;
        float pad = Tokens.Metric.DetailPad;
        float h = this.TileHeight;
        float small = this.Small;
        float big = this.Big;

        // Stretched to the full width: the tiles are at least as wide as their widest text.
        float w = MathF.Max(m_tileWidth, (width - (gap * 3f)) / 4f);

        for (int i = 0; i < DetailData.TileCount; i++)
        {
            RateTile tile = m_data.Tiles[i];
            Vector2 min = origin + new Vector2((i % 4) * (w + gap), (i / 4) * (h + Tokens.Metric.DetailSmallGap));
            dl.AddRectFilled(min, min + new Vector2(w, h), Tokens.Col.GroupBg, Tokens.Radius.Small);
            dl.AddRect(min, min + new Vector2(w, h), Tokens.Col.Hairline, Tokens.Radius.Small, ImDrawFlags.RoundCornersAll, Tokens.WorldLine(1f));
            Ink.DrawNote(dl, small, min + new Vector2(pad, pad), Tokens.Col.InkDim, tile.Label ?? string.Empty, TextEdge.None);
            Ink.DrawNote(dl, big, min + new Vector2(pad, pad + small + Tokens.WorldPx(4f)), tile.Known ? Tokens.Col.Ink : Tokens.Col.InkFaint, tile.Value ?? string.Empty, TextEdge.None);
        }
    }

    /// <summary>The skill table; returns its bottom.</summary>
    private float DrawSkills(ImDrawListPtr dl, Vector2 origin, float width)
    {
        float body = this.Body;
        float small = this.Small;
        float pad = Tokens.Metric.DetailPad;
        float colGap = Tokens.Metric.DetailColumnGap;
        float rowH = this.RowHeight;

        // The numbers sit right-aligned at the right edge of their column.
        float listWidth = width - Tokens.Metric.DetailScrollbar;
        float barLeft = origin.X + pad + m_nameColumn + colGap;

        Ink.DrawNote(dl, small, new Vector2(origin.X + pad, origin.Y), Tokens.Col.InkDim, Strings.DetailColSkill, TextEdge.None);
        Ink.DrawNote(dl, small, new Vector2(barLeft, origin.Y), Tokens.Col.InkDim, this.Heal ? Strings.DetailHealing : Strings.DetailDamage, TextEdge.None);

        float right = origin.X + listWidth - pad;
        for (int i = Columns - 1; i >= 0; i--)
        {
            string header = this.ColumnHeader(i);
            Ink.DrawNote(dl, small, new Vector2(right - Ink.MeasureNote(small, header), origin.Y), Tokens.Col.InkDim, header, TextEdge.None);
            right -= m_columns[i] + colGap;
        }

        float top = origin.Y + small + Tokens.WorldPx(6f);
        float listHeight = this.VisibleRows * rowH;

        if (m_data.Skills.Count == 0)
        {
            string note = m_data.NoLog ? Strings.DetailNoLog : this.Heal ? Strings.DetailNoHealing : Strings.DetailNoDamage;
            Ink.DrawNote(dl, body, new Vector2(origin.X + pad, top + ((rowH - body) * 0.5f)), Tokens.Col.InkDim, note, TextEdge.None);
            return top + listHeight;
        }

        ImGui.SetCursorScreenPos(new Vector2(origin.X, top));
        ImGui.PushStyleVar(ImGuiStyleVar.ScrollbarSize, Tokens.Metric.DetailScrollbar);
        ImGui.PushStyleVar(ImGuiStyleVar.ScrollbarRounding, Tokens.Metric.DetailScrollbar * 0.5f);
        ImGui.PushStyleColor(ImGuiCol.ChildBg, 0u);
        ImGui.PushStyleColor(ImGuiCol.ScrollbarBg, 0u);
        ImGui.PushStyleColor(ImGuiCol.ScrollbarGrab, Tokens.Col.ControlEdge);
        ImGui.PushStyleColor(ImGuiCol.ScrollbarGrabHovered, Tokens.Col.ControlEdgeHover);
        ImGui.PushStyleColor(ImGuiCol.ScrollbarGrabActive, Tokens.Col.Gold);

        if (ImGui.BeginChild(IdList, new Vector2(width, listHeight), false, ImGuiWindowFlags.NoNav))
        {
            ImDrawListPtr cl = ImGui.GetWindowDrawList();
            Vector2 start = ImGui.GetCursorScreenPos();
            uint colour = Jobs.Colour(m_data.JobId);
            float icon = rowH - Tokens.WorldPx(4f);
            float barW = Tokens.Metric.DetailBarWidth;
            float barH = Tokens.Metric.DetailBarHeight;
            Vector2 window = ImGui.GetWindowPos();
            float clipTop = window.Y;
            float clipBottom = window.Y + listHeight;

            for (int i = 0; i < m_data.Skills.Count; i++)
            {
                float rowTop = start.Y + (i * rowH);

                if (rowTop + rowH < clipTop || rowTop > clipBottom)
                {
                    continue;
                }

                SkillLine line = m_data.Skills[i];
                Vector2 min = new(start.X, rowTop);
                Vector2 max = new(start.X + listWidth, rowTop + rowH - Tokens.WorldPx(2f));
                bool hovered = ImGui.IsMouseHoveringRect(min, max) && ImGui.IsWindowHovered();

                if (hovered || i == 0)
                {
                    cl.AddRectFilled(min, max, hovered ? Tokens.Col.RowHover : Tokens.Col.GroupBg, Tokens.Radius.Small);
                }

                if (hovered)
                {
                    SkillTooltip(line);
                }

                float mid = min.Y + ((max.Y - min.Y) * 0.5f);
                float textY = MathF.Round(mid - (body * 0.5f));
                float x = min.X + pad;

                ImTextureID tex = Icons.Handle(line.IconId);
                if (tex != default)
                {
                    cl.AddImage(tex, new Vector2(x, mid - (icon * 0.5f)), new Vector2(x + icon, mid + (icon * 0.5f)));
                }

                Ink.DrawNote(cl, body, new Vector2(x + icon + Tokens.Metric.DetailSmallGap, textY), Tokens.Col.Ink, line.Name, TextEdge.None);

                float bx = min.X + pad + m_nameColumn + colGap;
                float by = mid - (barH * 0.5f);
                cl.AddRectFilled(new Vector2(bx, by), new Vector2(bx + barW, by + barH), Tokens.Col.Input, barH * 0.5f);
                cl.AddRectFilled(new Vector2(bx, by), new Vector2(bx + MathF.Max(barH, barW * line.Fraction), by + barH), colour, barH * 0.5f);

                float rx = max.X - pad;
                for (int c = Columns - 1; c >= 0; c--)
                {
                    string value = ColumnValue(line, c);
                    uint ink = c is 1 or 5 ? Tokens.Col.InkDim : Tokens.Col.Ink;
                    Ink.DrawNote(cl, body, new Vector2(rx - Ink.MeasureNote(body, value), textY), ink, value, TextEdge.None);
                    rx -= m_columns[c] + colGap;
                }
            }

            ImGui.Dummy(new Vector2(listWidth, m_data.Skills.Count * rowH));
        }

        ImGui.EndChild();
        ImGui.PopStyleColor(5);
        ImGui.PopStyleVar(2);
        return top + listHeight;
    }

    /// <summary>
    /// What a skill does, in the game's own words: its name and the description the game shows
    /// on its hotbar tooltip (Florian, 2026-10-29). The text comes from the sheet on the first
    /// hover and is kept (<see cref="ActionList.Describe"/>). A tick row is no one skill — the
    /// game does not say which effect ticked — so it says that instead.
    /// <para>
    /// Our tooltip in the suite's look, not the game's own window: the game draws its UI
    /// under every ImGui window, so its tooltip would open behind this one.
    /// </para>
    /// </summary>
    internal static void SkillTooltip(in SkillLine line)
    {
        string text = line.Kind switch
        {
            SkillKind.DamageOverTime => Strings.DetailDotTooltip,
            SkillKind.HealOverTime => Strings.DetailHotTooltip,
            _ => line.ActionId != 0 ? ActionList.Describe(line.ActionId) : string.Empty,
        };

        Chrome.Tooltip(line.Name, text);
    }

    private void DrawChart(ImDrawListPtr dl, Vector2 origin, float width)
    {
        float body = this.Body;
        float small = this.Small;
        float pad = Tokens.Metric.DetailPad;
        float gap = Tokens.Metric.DetailGap;
        float smallGap = Tokens.Metric.DetailSmallGap;
        float controlH = this.ControlHeight;

        Ink.DrawNote(dl, small, new Vector2(origin.X, MathF.Round(origin.Y + ((controlH - small) * 0.5f))), Tokens.Col.InkDim, Strings.DetailOverTheFight, TextEdge.None);

        // Right to left: "Show party", then Healing, then Damage.
        float right = origin.X + width;
        float partyText = Ink.MeasureNote(body, Strings.DetailShowParty);
        float box = controlH - Tokens.WorldPx(8f);
        float partyLeft = right - partyText - smallGap - box;
        ImGui.SetCursorScreenPos(new Vector2(partyLeft, origin.Y));
        ImGui.InvisibleButton(IdParty, new Vector2(right - partyLeft, controlH));
        bool partyHovered = ImGui.IsItemHovered();
        Chrome.ShowHand(partyHovered);
        if (ImGui.IsItemClicked())
        {
            m_showParty = !m_showParty;
        }

        Vector2 boxMin = new(partyLeft, origin.Y + ((controlH - box) * 0.5f));
        dl.AddRectFilled(boxMin, boxMin + new Vector2(box, box), Tokens.Col.Input, Tokens.Radius.Small);
        dl.AddRect(boxMin, boxMin + new Vector2(box, box), partyHovered ? Tokens.Col.ControlEdgeHover : Tokens.Col.ControlEdge, Tokens.Radius.Small, ImDrawFlags.RoundCornersAll, Tokens.WorldLine(1f));
        if (m_showParty)
        {
            LineIcons.Draw(dl, CheckIcon, boxMin, box, Tokens.Col.GoldHi);
        }

        Ink.DrawNote(dl, body, new Vector2(boxMin.X + box + smallGap, MathF.Round(origin.Y + ((controlH - body) * 0.5f))), Tokens.Col.Ink, Strings.DetailShowParty, TextEdge.None);

        float healW = this.SegmentWidth(Strings.DetailHealing);
        float damageW = this.SegmentWidth(Strings.DetailDamage);
        float healLeft = partyLeft - gap - healW;
        float damageLeft = healLeft - damageW;

        if (this.Segment(dl, IdDamage, Strings.DetailDamage, new Vector2(damageLeft, origin.Y), damageW, controlH, !this.Heal))
        {
            this.Heal = false;
        }

        if (this.Segment(dl, IdHeal, Strings.DetailHealing, new Vector2(healLeft, origin.Y), healW, controlH, this.Heal))
        {
            this.Heal = true;
        }

        // The plot.
        float top = origin.Y + controlH + smallGap;
        float chartH = Tokens.Metric.DetailChartHeight;
        Vector2 plot = new(origin.X + m_axisWidth, top);
        float plotW = width - m_axisWidth;
        dl.AddRectFilled(plot, plot + new Vector2(plotW, chartH), Tokens.Col.Input, Tokens.Radius.Small);
        dl.AddLine(plot + new Vector2(0f, chartH * 0.5f), plot + new Vector2(plotW, chartH * 0.5f), Tokens.Col.Hairline, Tokens.WorldLine(1f));

        float legendY = top + chartH + smallGap;

        if (m_data.Overall || m_data.NoLog || m_data.Length < 2)
        {
            string note = m_data.Overall ? Strings.DetailChartOverall : m_data.NoLog ? Strings.DetailNoLog : Strings.DetailChartWaiting;
            float noteW = Ink.MeasureNote(body, note);
            Ink.DrawNote(dl, body, plot + new Vector2((plotW - noteW) * 0.5f, (chartH - body) * 0.5f), Tokens.Col.InkDim, note, TextEdge.None);
            return;
        }

        float max = m_showParty ? m_data.MaxAll : m_data.MaxMine;
        max = MathF.Max(1f, max);
        string maxText = m_showParty ? m_axisMaxAll : m_axisMaxMine;
        string halfText = m_showParty ? m_axisHalfAll : m_axisHalfMine;
        float axisRight = plot.X - smallGap;
        Ink.DrawNote(dl, small, new Vector2(axisRight - Ink.MeasureNote(small, maxText), plot.Y), Tokens.Col.InkDim, maxText, TextEdge.None);
        Ink.DrawNote(dl, small, new Vector2(axisRight - Ink.MeasureNote(small, halfText), plot.Y + ((chartH - small) * 0.5f)), Tokens.Col.InkDim, halfText, TextEdge.None);
        Ink.DrawNote(dl, small, new Vector2(axisRight - Ink.MeasureNote(small, "0"), plot.Y + chartH - small), Tokens.Col.InkDim, "0", TextEdge.None);

        float inset = Tokens.WorldPx(4f);
        Vector2 area = plot + new Vector2(0f, inset);
        Vector2 areaSize = new(plotW, chartH - (inset * 2f));
        int length = m_data.Length;

        dl.PushClipRect(plot, plot + new Vector2(plotW, chartH), true);

        if (m_data.Boss.Length > 1)
        {
            Dashed(dl, m_data.Boss, 1f, length, area, areaSize, Tokens.Col.DetailBoss, Tokens.Metric.DetailLineOther, Tokens.Metric.DetailDot, Tokens.Metric.DetailDotGap);
        }

        foreach (ChartLine line in m_data.Lines)
        {
            if (!line.Mine && m_showParty)
            {
                Dashed(dl, line.Values, max, length, area, areaSize, Jobs.Colour(line.JobId), Tokens.Metric.DetailLineOther, Tokens.Metric.DetailDash, Tokens.Metric.DetailDashGap);
            }
        }

        foreach (ChartLine line in m_data.Lines)
        {
            if (line.Mine)
            {
                Dashed(dl, line.Values, max, length, area, areaSize, Jobs.Colour(line.JobId), Tokens.Metric.DetailLineMine, 0f, 0f);
            }
        }

        dl.PopClipRect();

        // Time and legend under the plot.
        Ink.DrawNote(dl, small, new Vector2(plot.X, legendY), Tokens.Col.InkDim, Strings.DetailTimeZero, TextEdge.None);
        float endW = Ink.MeasureNote(small, m_data.EndTime);
        float endX = plot.X + plotW - endW;
        Ink.DrawNote(dl, small, new Vector2(endX, legendY), Tokens.Col.InkDim, m_data.EndTime, TextEdge.None);

        var legend = new Legend(plot.X + Ink.MeasureNote(small, Strings.DetailTimeZero) + gap, endX - gap, legendY, small);

        foreach (ChartLine line in m_data.Lines)
        {
            if (line.Mine)
            {
                legend.Entry(dl, line.Name, Jobs.Colour(line.JobId), 0f, 0f, Tokens.Metric.DetailLineMine);
            }
        }

        if (m_showParty)
        {
            foreach (ChartLine line in m_data.Lines)
            {
                if (!line.Mine)
                {
                    legend.Entry(dl, line.Name, Jobs.Colour(line.JobId), Tokens.Metric.DetailDash, Tokens.Metric.DetailDashGap, Tokens.Metric.DetailLineOther);
                }
            }
        }

        if (m_data.Boss.Length > 1)
        {
            legend.Entry(dl, Strings.DetailBossHp, Tokens.Col.DetailBoss, Tokens.Metric.DetailDot, Tokens.Metric.DetailDotGap, Tokens.Metric.DetailLineOther);
        }
    }

    /// <summary>How many lines the legend takes at this width, with the whole party shown.</summary>
    private int LegendRows()
    {
        if (m_data.Overall || m_data.NoLog)
        {
            return 1;
        }

        float small = this.Small;
        float gap = Tokens.Metric.DetailGap;
        float plotLeft = Tokens.Metric.DetailPad + m_axisWidth;
        float plotRight = m_width - Tokens.Metric.DetailPad;
        var legend = new Legend(
            plotLeft + Ink.MeasureNote(small, Strings.DetailTimeZero) + gap,
            plotRight - Ink.MeasureNote(small, m_data.EndTime) - gap,
            0f,
            small);

        foreach (ChartLine line in m_data.Lines)
        {
            legend.Entry(null, line.Name, 0u, 0f, 0f, 0f);
        }

        if (m_data.Boss.Length > 1)
        {
            legend.Entry(null, Strings.DetailBossHp, 0u, 0f, 0f, 0f);
        }

        return legend.Rows;
    }

    /// <summary>The legend's entries between the two times, wrapping onto more lines rather than leaving any out.</summary>
    private struct Legend
    {
        private readonly float m_start;
        private readonly float m_limit;
        private readonly float m_small;
        private float m_x;
        private float m_y;

        public Legend(float start, float limit, float y, float small)
        {
            m_start = start;
            m_limit = limit;
            m_small = small;
            m_x = start;
            m_y = y;
            this.Rows = 1;
        }

        public int Rows { get; private set; }

        /// <summary>Lays one entry out, and draws it when there is a draw list (null only measures).</summary>
        public void Entry(ImDrawListPtr? dl, string name, uint colour, float dash, float gap, float thickness)
        {
            float swatch = Tokens.WorldPx(14f);
            float w = swatch + Tokens.Metric.DetailSmallGap + Ink.MeasureNote(m_small, name);

            if (m_x > m_start && m_x + w > m_limit)
            {
                m_x = m_start;
                m_y += m_small + Tokens.Metric.DetailSmallGap;
                this.Rows++;
            }

            if (dl is ImDrawListPtr list)
            {
                float mid = m_y + (m_small * 0.5f);
                DashedSegment(list, new Vector2(m_x, mid), new Vector2(m_x + swatch, mid), colour, thickness, dash, gap, 0f);
                Ink.DrawNote(list, m_small, new Vector2(m_x + swatch + Tokens.Metric.DetailSmallGap, m_y), Tokens.Col.Ink, name, TextEdge.None);
            }

            m_x += w + Tokens.Metric.DetailGap;
        }
    }

    private bool Segment(ImDrawListPtr dl, string id, string label, Vector2 min, float w, float h, bool on)
    {
        ImGui.SetCursorScreenPos(min);
        ImGui.InvisibleButton(id, new Vector2(w, h));
        bool hovered = ImGui.IsItemHovered();
        Chrome.ShowHand(hovered);
        bool clicked = ImGui.IsItemClicked();

        Vector2 max = min + new Vector2(w, h);
        dl.AddRectFilled(min, max, on ? Tokens.Col.Gold : hovered ? Tokens.Col.RowHover : Tokens.Col.Control2, Tokens.Radius.Small);
        float body = this.Body;
        float textW = Ink.MeasureNote(body, label);
        Ink.DrawNote(dl, body, new Vector2(MathF.Round(min.X + ((w - textW) * 0.5f)), MathF.Round(min.Y + ((h - body) * 0.5f))), on ? Tokens.Col.InkOnGold : Tokens.Col.Ink, label, TextEdge.None);
        return clicked;
    }

    /// <summary>
    /// A curve across the plot, solid when <paramref name="dash"/> is zero. Wider than the
    /// plot has pixels for, it steps over seconds rather than drawing segments on top of each
    /// other. Nothing is allocated: each segment is drawn as it is walked.
    /// </summary>
    private static void Dashed(ImDrawListPtr dl, float[] values, float max, int length, Vector2 area, Vector2 size, uint colour, float thickness, float dash, float gap)
    {
        if (values.Length < 2 || length < 2)
        {
            return;
        }

        int step = Math.Max(1, (int)(length / MathF.Max(1f, size.X * 0.5f)));
        float phase = 0f;
        Vector2 previous = Point(0, values[0]);

        for (int i = step; i < values.Length; i += step)
        {
            Vector2 next = Point(i, values[i]);
            phase = DashedSegment(dl, previous, next, colour, thickness, dash, gap, phase);
            previous = next;
        }

        Vector2 Point(int i, float v) => new(
            area.X + (size.X * i / (length - 1)),
            area.Y + size.Y - (Math.Clamp(v / max, 0f, 1f) * size.Y));
    }

    /// <summary>One segment of a dashed line, continuing the pattern from <paramref name="phase"/>; returns the phase after it.</summary>
    private static float DashedSegment(ImDrawListPtr dl, Vector2 from, Vector2 to, uint colour, float thickness, float dash, float gap, float phase)
    {
        if (dash <= 0f)
        {
            dl.AddLine(from, to, colour, thickness);
            return 0f;
        }

        float period = dash + gap;
        float length = Vector2.Distance(from, to);

        if (length <= 0f)
        {
            return phase;
        }

        Vector2 direction = (to - from) / length;
        float walked = 0f;

        while (walked < length)
        {
            float inPeriod = phase % period;
            bool on = inPeriod < dash;
            float left = on ? dash - inPeriod : period - inPeriod;
            float take = MathF.Min(left, length - walked);

            if (on)
            {
                dl.AddLine(from + (direction * walked), from + (direction * (walked + take)), colour, thickness);
            }

            walked += take;
            phase += take;
        }

        return phase % period;
    }

    private static string[] BuildIds(string prefix, int count)
    {
        var ids = new string[count];
        for (int i = 0; i < count; i++)
        {
            ids[i] = prefix + i.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        return ids;
    }
}
