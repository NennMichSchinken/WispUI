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
/// The Quick Dispel page: one tab, two cards, the five settings agreed with the mockup
/// (Florian, 2026-09-22). What the squares look like on the left, when they are there at all
/// on the right.
/// <para>
/// The cleanse colour is not offered here: a square lights in the same colour as the mark on
/// a party frame, because both say the same thing, and two pickers for one statement are two
/// things to keep in step by hand.
/// </para>
/// </summary>
internal sealed class QuickDispelScreen
{
    private const string IdSquaresGroup = "##wisp-qd-squares";
    private const string IdShownGroup = "##wisp-qd-shown";
    private const string IdSize = "##wisp-qd-size";
    private const string IdLayout = "##wisp-qd-layout";
    private const string IdNumber = "##wisp-qd-number";
    private const string IdOnlyWhenAble = "##wisp-qd-able";
    private const string IdHideWhenClear = "##wisp-qd-clear";

    private const float PixelStep = 1f;
    private const float PixelEditScale = 1f;

    /// <summary>In the order of <see cref="Hud.QuickDispel.DispelLayout"/>.</summary>
    private static readonly string[] Layouts = { Strings.DispelLayoutRow, Strings.DispelLayoutGrid };

    private readonly Configuration m_config;

    /// <summary>The size caption, rebuilt only when the number under it moves.</summary>
    private string m_sizeText = string.Empty;
    private int m_sizeTextFor = -1;

    public QuickDispelScreen(Configuration config)
    {
        m_config = config;
    }

    public void Draw(float width)
    {
        Vector2 origin = ImGui.GetCursorScreenPos();
        float column = Chrome.ColumnWidth(width);

        Chrome.BeginGroupRow();
        Chrome.GroupScope squares = this.DrawSquares(Chrome.ColumnX(origin.X, width, 0), origin.Y, column, out float squaresHeight);
        Chrome.GroupScope shown = this.DrawShown(Chrome.ColumnX(origin.X, width, 1), origin.Y, column, out float shownHeight);
        float y = origin.Y + Chrome.GroupFrameRow(squares, squaresHeight, shown, shownHeight) + Tokens.Metric.ColumnGutter;

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, y - origin.Y + Tokens.Metric.ContentPaddingBottom));
    }

    private Chrome.GroupScope DrawSquares(float x, float y, float width, out float contentHeight)
    {
        Configuration.QuickDispelConfig cfg = m_config.QuickDispel;
        Chrome.GroupScope group = Chrome.BeginGroup(
            IdSquaresGroup,
            new Chrome.GroupHead
            {
                Title = Strings.GroupDispelSquares,
                Description = Strings.GroupDispelSquaresHint,
            },
            x,
            y,
            width);

        float rowY = group.ContentY;
        Chrome.SliderResult size = Chrome.Slider(
            IdSize,
            Strings.DispelSquareSize,
            this.SizeText(cfg.SquareSize),
            group.ContentX,
            rowY,
            group.ContentWidth,
            cfg.SquareSize,
            Configuration.MinDispelSquare,
            Configuration.MaxDispelSquare,
            null,
            null,
            false,
            PixelStep,
            PixelEditScale);

        if (size.Changed)
        {
            cfg.SquareSize = MathF.Round(size.Value);
            m_config.MarkDirty();
        }

        rowY += Chrome.RowPitch();
        int layout = cfg.Layout;
        if (Chrome.SegmentRow(IdLayout, Strings.DispelLayout, group.ContentX, rowY, group.ContentWidth, Layouts, ref layout, true))
        {
            cfg.Layout = layout;
            m_config.MarkDirty();
        }

        rowY += Chrome.RowPitch();
        if (Chrome.OptionRow(
                IdNumber,
                Strings.DispelPartyNumber,
                group.ContentX,
                rowY,
                group.ContentWidth,
                cfg.ShowPartyNumber,
                Chrome.OptionControl.Tick,
                Strings.DispelPartyNumberTooltip,
                true,
                true))
        {
            cfg.ShowPartyNumber = !cfg.ShowPartyNumber;
            m_config.MarkDirty();
        }

        contentHeight = rowY - group.ContentY + Chrome.RowHeight();
        Chrome.EndGroupContent(group, contentHeight);
        return group;
    }

    private Chrome.GroupScope DrawShown(float x, float y, float width, out float contentHeight)
    {
        Configuration.QuickDispelConfig cfg = m_config.QuickDispel;
        Chrome.GroupScope group = Chrome.BeginGroup(
            IdShownGroup,
            new Chrome.GroupHead
            {
                Title = Strings.GroupDispelShown,
                Description = Strings.GroupDispelShownHint,
            },
            x,
            y,
            width);

        float rowY = group.ContentY;
        if (Chrome.OptionRow(
                IdOnlyWhenAble,
                Strings.DispelOnlyWhenAble,
                group.ContentX,
                rowY,
                group.ContentWidth,
                cfg.OnlyWhenAble,
                Chrome.OptionControl.Tick,
                Strings.DispelOnlyWhenAbleTooltip,
                true,
                false))
        {
            cfg.OnlyWhenAble = !cfg.OnlyWhenAble;
            m_config.MarkDirty();
        }

        rowY += Chrome.RowPitch();
        if (Chrome.OptionRow(
                IdHideWhenClear,
                Strings.DispelHideWhenClear,
                group.ContentX,
                rowY,
                group.ContentWidth,
                cfg.HideWhenClear,
                Chrome.OptionControl.Tick,
                Strings.DispelHideWhenClearTooltip,
                true,
                true))
        {
            cfg.HideWhenClear = !cfg.HideWhenClear;
            m_config.MarkDirty();
        }

        contentHeight = rowY - group.ContentY + Chrome.RowHeight();
        Chrome.EndGroupContent(group, contentHeight);
        return group;
    }

    private string SizeText(float value)
    {
        int whole = (int)MathF.Round(value);
        if (m_sizeTextFor != whole)
        {
            m_sizeTextFor = whole;
            m_sizeText = whole.ToString(CultureInfo.InvariantCulture) + " px";
        }

        return m_sizeText;
    }
}
