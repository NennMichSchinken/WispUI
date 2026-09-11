using System;
using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using WispUI.Appearance;
using WispUI.Core;
using WispUI.Data;
using WispUI.Hud;
using WispUI.Hud.PartyFrames;
using WispUI.Interface.Widgets;
using WispUI.Localization;
using WispUI.Style;

namespace WispUI.Interface.Screens;

/// <summary>
/// The party frames' own screens. Base says what a frame looks like and what it says; Layout
/// says how big the frames are and how they are arranged. The split is the suite's own
/// appearance/layout boundary (CLAUDE.md §5.3), which is also what copy and paste hangs on.
/// </summary>
internal sealed class PartyFramesScreen : IAppearanceOwner
{
    private const string IdHealthGroup = "##wisp-pf-health";
    private const string IdTextGroup = "##wisp-pf-text";
    private const string IdHealthTextGroup = "##wisp-pf-healthtext";
    private const string IdManaGroup = "##wisp-pf-mana";
    private const string IdStyle = "##wisp-pf-style";
    private const string IdColour = "##wisp-pf-colour";
    private const string IdOpacity = "##wisp-pf-opacity";
    private const string IdSmooth = "##wisp-pf-smooth";
    private const string IdNamePosition = "##wisp-pf-nameposition";
    private const string IdNameSize = "##wisp-pf-namesize";
    private const string IdNameX = "##wisp-pf-namex";
    private const string IdNameY = "##wisp-pf-namey";
    private const string IdNameJobColour = "##wisp-pf-namejobcolour";
    private const string IdShortenNames = "##wisp-pf-shortennames";
    private const string IdHealthMode = "##wisp-pf-healthmode";
    private const string IdHealthSize = "##wisp-pf-healthsize";
    private const string IdHealthPosition = "##wisp-pf-healthposition";
    private const string IdHealthX = "##wisp-pf-healthx";
    private const string IdHealthY = "##wisp-pf-healthy";
    private const string IdManaStyle = "##wisp-pf-manastyle";
    private const string IdManaHeight = "##wisp-pf-manaheight";
    private const string IdManaTanks = "##wisp-pf-manatanks";
    private const string IdManaHealers = "##wisp-pf-manahealers";
    private const string IdManaDps = "##wisp-pf-manadps";
    private const string IdArrangeGroup = "##wisp-pf-arrange";
    private const string IdSizeGroup = "##wisp-pf-size";
    private const string IdDirection = "##wisp-pf-direction";
    private const string IdLines = "##wisp-pf-lines";
    private const string IdWidth = "##wisp-pf-width";
    private const string IdHeight = "##wisp-pf-height";
    private const string IdSpacing = "##wisp-pf-spacing";

    // The ranges from the spec, §4. The useful height is 30-70; the rest is there so a small
    // party can have tall frames.
    private const float MinWidth = 90f;
    private const float MaxWidth = 400f;
    private const float MinHeight = 18f;
    private const float MaxHeight = 150f;
    private const float MaxSpacing = 24f;

    /// <summary>How far a text may be nudged off its anchor, either way.</summary>
    private const float MaxOffset = 40f;

    /// <summary>Mana strip thickness, in pixels and nothing else (spec §3).</summary>
    private const float MinManaHeight = 2f;
    private const float MaxManaHeight = 16f;

    /// <summary>
    /// Every pixel slider steps by a whole pixel. There is no half a pixel to draw, and every
    /// one of these ranges is narrower than the track, so pointing reaches all of them.
    /// </summary>
    private const float PixelStep = 1f;

    /// <summary>
    /// The frame width is the one range wider than the track: 90 to 400 is more sizes than the
    /// track has pixels, so single pixels there cannot be reached by pointing at all. It steps
    /// by five instead, which lands on the round numbers and gives the slider a detent you can
    /// feel (Florian, 2026-09-12). A frame is a block of the screen, not a glyph — five pixels
    /// of width is a decision, not a nuisance.
    /// </summary>
    private const float WidthStep = 5f;

    /// <summary>Opacity steps by a percent, which is what the readout beside it says.</summary>
    private const float OpacityStep = 0.01f;

    // The slider readouts are kept per slot, so each one is only rebuilt when its own number
    // moves. The slots are in this order.
    private const int SlotWidth = 0;
    private const int SlotHeight = 1;
    private const int SlotSpacing = 2;
    private const int SlotHealthX = 3;
    private const int SlotHealthY = 4;
    private const int SlotManaHeight = 5;
    private const int SlotHealthSize = 6;
    private const int SlotNameSize = 7;
    private const int SlotNameX = 8;
    private const int SlotNameY = 9;
    private const int SlotCount = 10;

    /// <summary>What a bar takes its colour from. FFXIV's own convention, not one of ours.</summary>
    private static readonly BarColourMode[] ColourModes =
    {
        BarColourMode.Role,
        BarColourMode.Job,
        BarColourMode.Fixed,
    };

    /// <summary>A few jobs standing in for all of them in the "by job" preview.</summary>
    private static readonly uint[] JobSample = { 19u, 24u, 25u, 23u };

    /// <summary>The line counts as text, so the label never builds a string in a draw path.</summary>
    private static readonly System.Collections.Generic.Dictionary<int, string> LineLabels = new() { { 1, "1" }, { 2, "2" }, { 4, "4" } };

    /// <summary>Which way the block of frames runs.</summary>
    private static readonly string[] Directions = { Strings.DirectionVertical, Strings.DirectionHorizontal };

    /// <summary>The nine anchor points, in the order <see cref="Anchors.All"/> lists them.</summary>
    private static readonly string[] AnchorNames =
    {
        Strings.PositionTopLeft,
        Strings.PositionTop,
        Strings.PositionTopRight,
        Strings.PositionLeft,
        Strings.PositionCentre,
        Strings.PositionRight,
        Strings.PositionBottomLeft,
        Strings.PositionBottom,
        Strings.PositionBottomRight,
    };

    private static readonly string[] ManaStyles = { Strings.ManaStyleStrip, Strings.ManaStyleBar };

    private readonly Configuration m_config;
    private readonly ArrowSelector<BarStyle> m_style;
    private readonly ArrowSelector<BarColourMode> m_colour;
    private readonly ArrowSelector<Anchor> m_namePosition;
    private readonly ArrowSelector<HealthTextMode> m_healthMode;
    private readonly ArrowSelector<Anchor> m_healthPosition;
    private readonly ArrowSelector<string> m_manaStyle;
    private readonly ArrowSelector<string> m_direction;
    private readonly ArrowSelector<int> m_lines;

    private string m_opacityText = string.Empty;
    private int m_opacityTextFor = -1;

    /// <summary>One readout per slider, rebuilt only when its number changes.</summary>
    private readonly string[] m_sizeText = new string[SlotCount];
    private readonly int[] m_sizeTextFor = { -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 };

    private string m_arrangementText = string.Empty;
    private int m_arrangementFor = -1;

    public PartyFramesScreen(Configuration config)
    {
        m_config = config;

        // A long list: worth a popup, and long enough that the search box earns its place.
        m_style = new ArrowSelector<BarStyle>(
            IdStyle,
            BarStyles.All,
            new ArrowSelectorOptions<BarStyle>
            {
                Label = BarStyles.Label,
                DrawPreview = BarStyles.DrawPreview,
                EnablePopupList = true,
                EnableSearch = true,
            });

        // Three entries: no popup, no search, no counter. The same widget, told to be small.
        m_colour = new ArrowSelector<BarColourMode>(
            IdColour,
            ColourModes,
            new ArrowSelectorOptions<BarColourMode>
            {
                Label = static mode => mode switch
                {
                    BarColourMode.Role => Strings.ColourByRole,
                    BarColourMode.Job => Strings.ColourByJob,
                    _ => Strings.ColourFixed,
                },
                DrawPreview = DrawColourPreview,
                ShowCounter = false,
            });

        // Nine points is more than anyone wants to walk through one arrow at a time, so both
        // anchor selectors carry the popup. No search: nine lines are read, not searched.
        m_namePosition = new ArrowSelector<Anchor>(
            IdNamePosition,
            Anchors.All,
            new ArrowSelectorOptions<Anchor> { Label = AnchorLabel, EnablePopupList = true, ShowCounter = false });

        m_healthPosition = new ArrowSelector<Anchor>(
            IdHealthPosition,
            Anchors.All,
            new ArrowSelectorOptions<Anchor> { Label = AnchorLabel, EnablePopupList = true, ShowCounter = false });

        m_healthMode = new ArrowSelector<HealthTextMode>(
            IdHealthMode,
            HealthText.All,
            new ArrowSelectorOptions<HealthTextMode>
            {
                Label = static mode => mode switch
                {
                    HealthTextMode.Current => Strings.HealthTextCurrent,
                    HealthTextMode.Deficit => Strings.HealthTextDeficit,
                    _ => Strings.HealthTextPercent,
                },
                ShowCounter = false,
            });

        m_manaStyle = new ArrowSelector<string>(
            IdManaStyle,
            ManaStyles,
            new ArrowSelectorOptions<string> { Label = static style => style, ShowCounter = false });

        m_direction = new ArrowSelector<string>(
            IdDirection,
            Directions,
            new ArrowSelectorOptions<string> { Label = static name => name, ShowCounter = false });

        m_lines = new ArrowSelector<int>(
            IdLines,
            FrameLayout.LineChoices,
            new ArrowSelectorOptions<int> { Label = static lines => LineLabels[lines], ShowCounter = false });
    }

    public string DisplayName => Strings.NavPartyFrames;

    /// <summary>
    /// What this element has. Shape and background are not among them yet, and saying so is
    /// what lets the paste panel tell the truth about what will carry over. Mana is not in
    /// here either: a thickness in pixels is a size, and sizes are never copied.
    /// </summary>
    public AppearanceFields SupportedFields =>
        AppearanceFields.Colours | AppearanceFields.Texture | AppearanceFields.Opacity | AppearanceFields.Text;

    public AppearanceBlock GetAppearance() => new()
    {
        BarStyle = m_config.PartyFrames.BarStyle,
        ColourMode = m_config.PartyFrames.ColourMode,
        BarOpacity = m_config.PartyFrames.BarOpacity,
        NamePosition = m_config.PartyFrames.NamePosition,
        NameSize = m_config.PartyFrames.NameSize,
        NameX = m_config.PartyFrames.NameX,
        NameY = m_config.PartyFrames.NameY,
        NameInJobColour = m_config.PartyFrames.NameInJobColour,
        ShortenNames = m_config.PartyFrames.ShortenNames,
        ShowHealthText = m_config.PartyFrames.ShowHealthText,
        HpTextMode = m_config.PartyFrames.HpTextMode,
        HpTextSize = m_config.PartyFrames.HpTextSize,
        HpTextPosition = m_config.PartyFrames.HpTextPosition,
        HpTextX = m_config.PartyFrames.HpTextX,
        HpTextY = m_config.PartyFrames.HpTextY,
    };

    public void ApplyAppearance(AppearanceBlock source, AppearanceFields mask)
    {
        if ((mask & AppearanceFields.Texture) != 0)
        {
            m_config.PartyFrames.BarStyle = source.BarStyle;
        }

        if ((mask & AppearanceFields.Colours) != 0)
        {
            m_config.PartyFrames.ColourMode = source.ColourMode;
        }

        if ((mask & AppearanceFields.Opacity) != 0)
        {
            m_config.PartyFrames.BarOpacity = source.BarOpacity;
        }

        if ((mask & AppearanceFields.Text) != 0)
        {
            m_config.PartyFrames.NamePosition = source.NamePosition;
            m_config.PartyFrames.NameSize = source.NameSize;
            m_config.PartyFrames.NameX = source.NameX;
            m_config.PartyFrames.NameY = source.NameY;
            m_config.PartyFrames.NameInJobColour = source.NameInJobColour;
            m_config.PartyFrames.ShortenNames = source.ShortenNames;
            m_config.PartyFrames.ShowHealthText = source.ShowHealthText;
            m_config.PartyFrames.HpTextMode = source.HpTextMode;
            m_config.PartyFrames.HpTextSize = source.HpTextSize;
            m_config.PartyFrames.HpTextPosition = source.HpTextPosition;
            m_config.PartyFrames.HpTextX = source.HpTextX;
            m_config.PartyFrames.HpTextY = source.HpTextY;
        }

        m_config.MarkDirty();
    }

    /// <param name="width">The usable width, with the content padding already taken off.</param>
    public void Draw(float width)
    {
        Vector2 origin = ImGui.GetCursorScreenPos();
        float column = Chrome.ColumnWidth(width);
        float y = origin.Y;

        // Drawn first and framed afterwards, so the shorter column can be carried down to the
        // taller one's bottom edge. Two groups that each stop where their own rows end leave a
        // step, and every group added later adds another one. Nothing else has to be squared
        // up between the columns: both stack on the same ladder (Chrome.RowPitch).
        Chrome.BeginGroupRow();
        Chrome.GroupScope bar = this.DrawHealthBar(Chrome.ColumnX(origin.X, width, 0), y, column, out float barHeight);
        Chrome.GroupScope name = this.DrawNameText(Chrome.ColumnX(origin.X, width, 1), y, column, out float nameHeight);
        y += FrameRow(bar, barHeight, name, nameHeight);

        Chrome.BeginGroupRow();
        Chrome.GroupScope figure = this.DrawHealthText(Chrome.ColumnX(origin.X, width, 0), y, column, out float figureHeight);
        Chrome.GroupScope mana = this.DrawMana(Chrome.ColumnX(origin.X, width, 1), y, column, out float manaHeight);
        y += FrameRow(figure, figureHeight, mana, manaHeight);

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, y - origin.Y + Tokens.Metric.ContentPaddingBottom));
    }

    /// <summary>
    /// The Layout tab: how the frames are arranged, and how big they are. Kept apart from Base
    /// on purpose — colour and style are shared between elements, size and arrangement belong
    /// to this one and are never copied (CLAUDE.md §5.3).
    /// </summary>
    public void DrawLayout(float width)
    {
        Vector2 origin = ImGui.GetCursorScreenPos();
        float column = Chrome.ColumnWidth(width);

        Chrome.BeginGroupRow();
        Chrome.GroupScope arrange = this.DrawArrangement(Chrome.ColumnX(origin.X, width, 0), origin.Y, column, out float arrangeHeight);
        Chrome.GroupScope size = this.DrawSize(Chrome.ColumnX(origin.X, width, 1), origin.Y, column, out float sizeHeight);
        float y = origin.Y + FrameRow(arrange, arrangeHeight, size, sizeHeight);

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, y - origin.Y + Tokens.Metric.ContentPaddingBottom));
    }

    /// <summary>
    /// Frames a row of two groups down to a shared bottom edge and says where the next row
    /// starts.
    /// <para>
    /// The air under a row is the same gutter that sits between the two columns. One measure
    /// used both ways is what makes a screen read as a grid rather than as stacked pairs.
    /// </para>
    /// </summary>
    private static float FrameRow(in Chrome.GroupScope left, float leftHeight, in Chrome.GroupScope right, float rightHeight) =>
        Chrome.GroupFrameRow(left, leftHeight, right, rightHeight) + Tokens.Metric.ColumnGutter;

    private Chrome.GroupScope DrawHealthBar(float x, float y, float width, out float contentHeight)
    {
        Chrome.GroupScope group = Chrome.BeginGroup(
            IdHealthGroup,
            new Chrome.GroupHead
            {
                Title = Strings.GroupHealthBar,
                Description = Strings.GroupHealthBarHint,
            },
            x,
            y,
            width);

        // One row per setting, all built the same: label left, control right. Nothing has to
        // be squared up with the column beside it — every row is the same height, so row three
        // is row three over there too, whatever either of them holds.
        float pitch = Chrome.RowPitch();
        float rowY = group.ContentY;

        int style = m_config.PartyFrames.BarStyle;
        if (m_style.Draw(
                ref style,
                Chrome.Row(Strings.BarStyle, group.ContentX, rowY, group.ContentWidth, false),
                rowY,
                Chrome.ControlWidth()))
        {
            m_config.PartyFrames.BarStyle = style;
            m_config.MarkDirty();
        }

        rowY += pitch;

        int colour = m_config.PartyFrames.ColourMode;
        if (m_colour.Draw(
                ref colour,
                Chrome.Row(Strings.BarColour, group.ContentX, rowY, group.ContentWidth, true),
                rowY,
                Chrome.ControlWidth()))
        {
            m_config.PartyFrames.ColourMode = colour;
            m_config.MarkDirty();
        }

        rowY += pitch;

        float opacity = m_config.PartyFrames.BarOpacity;
        Chrome.SliderResult result = Chrome.Slider(
            IdOpacity,
            Strings.BarOpacity,
            this.OpacityCaption(opacity),
            group.ContentX,
            rowY,
            group.ContentWidth,
            opacity,
            Configuration.MinBarOpacity,
            1f,
            null,
            null,
            true,
            OpacityStep);

        // Applied while the hand is still on it, like every other slider that changes
        // something already on screen. Opacity is the setting you most want to judge by
        // looking, so holding it back until release was exactly the wrong one to hold back.
        if (result.Changed)
        {
            m_config.PartyFrames.BarOpacity = result.Value;
            m_config.MarkDirty();
        }

        rowY += pitch;

        if (Chrome.OptionRow(
                IdSmooth,
                Strings.SmoothBars,
                group.ContentX,
                rowY,
                group.ContentWidth,
                m_config.PartyFrames.SmoothBars,
                Chrome.OptionControl.Tick,
                Strings.SmoothBarsTooltip,
                true,
                true))
        {
            m_config.PartyFrames.SmoothBars = !m_config.PartyFrames.SmoothBars;
            m_config.MarkDirty();
        }

        // The group ends with its last row, not with the gap that would follow it.
        float used = rowY - group.ContentY + Chrome.RowHeight();
        Chrome.EndGroupContent(group, used);
        contentHeight = used;
        return group;
    }

    /// <summary>
    /// The name text, with a switch of its own in the group head. With it off the rows stay
    /// visible but go quiet and stop answering — you can still see what the group would give
    /// you, which is the point of dimming rather than hiding.
    /// </summary>
    private Chrome.GroupScope DrawNameText(float x, float y, float width, out float contentHeight)
    {
        Chrome.GroupScope group = Chrome.BeginGroup(
            IdTextGroup,
            new Chrome.GroupHead
            {
                Title = Strings.GroupNameText,
                Description = Strings.GroupNameTextHint,
                Toggle = m_config.PartyFrames.ShowName,
            },
            x,
            y,
            width);

        if (group.ToggleClicked)
        {
            m_config.PartyFrames.ShowName = !m_config.PartyFrames.ShowName;
            m_config.MarkDirty();
        }

        // One row per setting, same as everywhere: label left, control right, one height.
        float pitch = Chrome.RowPitch();
        float rowY = group.ContentY;

        this.PixelSlider(IdNameSize, Strings.TextSize, SlotNameSize, group, rowY, Configuration.MinTextSize, Configuration.MaxTextSize, false, null);
        rowY += pitch;

        int position = m_config.PartyFrames.NamePosition;
        if (m_namePosition.Draw(
                ref position,
                Chrome.Row(Strings.NamePosition, group.ContentX, rowY, group.ContentWidth, true),
                rowY,
                Chrome.ControlWidth()))
        {
            m_config.PartyFrames.NamePosition = position;
            m_config.MarkDirty();
        }

        rowY += pitch;
        this.PixelSlider(IdNameX, Strings.OffsetX, SlotNameX, group, rowY, -MaxOffset, MaxOffset, true, null);
        rowY += pitch;
        this.PixelSlider(IdNameY, Strings.OffsetY, SlotNameY, group, rowY, -MaxOffset, MaxOffset, true, null);
        rowY += pitch;

        if (Chrome.OptionRow(
                IdNameJobColour,
                Strings.NameInJobColour,
                group.ContentX,
                rowY,
                group.ContentWidth,
                m_config.PartyFrames.NameInJobColour,
                Chrome.OptionControl.Switch,
                null,
                true,
                true))
        {
            m_config.PartyFrames.NameInJobColour = !m_config.PartyFrames.NameInJobColour;
            m_config.MarkDirty();
        }

        rowY += pitch;

        if (Chrome.OptionRow(
                IdShortenNames,
                Strings.ShortenNames,
                group.ContentX,
                rowY,
                group.ContentWidth,
                m_config.PartyFrames.ShortenNames,
                Chrome.OptionControl.Tick,
                Strings.ShortenNamesTooltip,
                true,
                true))
        {
            m_config.PartyFrames.ShortenNames = !m_config.PartyFrames.ShortenNames;
            m_config.MarkDirty();
        }

        float used = rowY - group.ContentY + Chrome.RowHeight();
        Chrome.EndGroupContent(group, used);
        contentHeight = used;
        return group;
    }

    /// <summary>
    /// The figure on the bar. The same rows the name gets: how big it is, which point it hangs
    /// on, and the two nudges off that point (spec §11.2), plus what it says. That anatomy is
    /// also why there is no padding slider — padding would be a second, vaguer way of saying
    /// the same thing.
    /// <para>
    /// Whether it shows is the group's own switch and not an entry in the list of what it can
    /// say: turning something off should not mean walking a list to find the word for off.
    /// </para>
    /// </summary>
    private Chrome.GroupScope DrawHealthText(float x, float y, float width, out float contentHeight)
    {
        Chrome.GroupScope group = Chrome.BeginGroup(
            IdHealthTextGroup,
            new Chrome.GroupHead
            {
                Title = Strings.GroupHealthText,
                Description = Strings.GroupHealthTextHint,
                Toggle = m_config.PartyFrames.ShowHealthText,
            },
            x,
            y,
            width);

        if (group.ToggleClicked)
        {
            m_config.PartyFrames.ShowHealthText = !m_config.PartyFrames.ShowHealthText;
            m_config.MarkDirty();
        }

        float pitch = Chrome.RowPitch();
        float rowY = group.ContentY;

        int mode = m_config.PartyFrames.HpTextMode;
        if (m_healthMode.Draw(
                ref mode,
                Chrome.Row(Strings.HealthTextMode, group.ContentX, rowY, group.ContentWidth, false),
                rowY,
                Chrome.ControlWidth()))
        {
            m_config.PartyFrames.HpTextMode = mode;
            m_config.MarkDirty();
        }

        rowY += pitch;
        this.PixelSlider(IdHealthSize, Strings.TextSize, SlotHealthSize, group, rowY, Configuration.MinTextSize, Configuration.MaxTextSize, true, null);
        rowY += pitch;

        int position = m_config.PartyFrames.HpTextPosition;
        if (m_healthPosition.Draw(
                ref position,
                Chrome.Row(Strings.TextPosition, group.ContentX, rowY, group.ContentWidth, true),
                rowY,
                Chrome.ControlWidth()))
        {
            m_config.PartyFrames.HpTextPosition = position;
            m_config.MarkDirty();
        }

        rowY += pitch;
        this.PixelSlider(IdHealthX, Strings.OffsetX, SlotHealthX, group, rowY, -MaxOffset, MaxOffset, true, null);
        rowY += pitch;
        this.PixelSlider(IdHealthY, Strings.OffsetY, SlotHealthY, group, rowY, -MaxOffset, MaxOffset, true, null);

        float used = rowY - group.ContentY + Chrome.RowHeight();
        Chrome.EndGroupContent(group, used);
        contentHeight = used;
        return group;
    }

    /// <summary>
    /// Mana. Three switches rather than one "healers only": in a light party a caster's mana
    /// is worth a glance, in a full one eight of them are noise, and which is which is the
    /// player's call, not ours.
    /// </summary>
    private Chrome.GroupScope DrawMana(float x, float y, float width, out float contentHeight)
    {
        Chrome.GroupScope group = Chrome.BeginGroup(
            IdManaGroup,
            new Chrome.GroupHead
            {
                Title = Strings.GroupMana,
                Description = Strings.GroupManaHint,
                Toggle = m_config.PartyFrames.ShowMana,
            },
            x,
            y,
            width);

        if (group.ToggleClicked)
        {
            m_config.PartyFrames.ShowMana = !m_config.PartyFrames.ShowMana;
            m_config.MarkDirty();
        }

        float pitch = Chrome.RowPitch();
        float rowY = group.ContentY;

        int style = m_config.PartyFrames.ManaStyle;
        if (m_manaStyle.Draw(
                ref style,
                Chrome.Row(Strings.ManaStyle, group.ContentX, rowY, group.ContentWidth, false),
                rowY,
                Chrome.ControlWidth()))
        {
            m_config.PartyFrames.ManaStyle = style;
            m_config.MarkDirty();
        }

        rowY += pitch;
        this.PixelSlider(
            IdManaHeight,
            Strings.ManaHeight,
            SlotManaHeight,
            group,
            rowY,
            MinManaHeight,
            MaxManaHeight,
            true,
            Strings.ManaHeightHint);

        rowY += pitch;
        if (Chrome.OptionRow(
                IdManaTanks,
                Strings.ManaForTanks,
                group.ContentX,
                rowY,
                group.ContentWidth,
                m_config.PartyFrames.ManaForTanks,
                Chrome.OptionControl.Tick,
                null,
                true,
                true))
        {
            m_config.PartyFrames.ManaForTanks = !m_config.PartyFrames.ManaForTanks;
            m_config.MarkDirty();
        }

        rowY += pitch;
        if (Chrome.OptionRow(
                IdManaHealers,
                Strings.ManaForHealers,
                group.ContentX,
                rowY,
                group.ContentWidth,
                m_config.PartyFrames.ManaForHealers,
                Chrome.OptionControl.Tick,
                null,
                true,
                true))
        {
            m_config.PartyFrames.ManaForHealers = !m_config.PartyFrames.ManaForHealers;
            m_config.MarkDirty();
        }

        rowY += pitch;
        if (Chrome.OptionRow(
                IdManaDps,
                Strings.ManaForDps,
                group.ContentX,
                rowY,
                group.ContentWidth,
                m_config.PartyFrames.ManaForDps,
                Chrome.OptionControl.Tick,
                null,
                true,
                true))
        {
            m_config.PartyFrames.ManaForDps = !m_config.PartyFrames.ManaForDps;
            m_config.MarkDirty();
        }

        float used = rowY - group.ContentY + Chrome.RowHeight();
        Chrome.EndGroupContent(group, used);
        contentHeight = used;
        return group;
    }

    private Chrome.GroupScope DrawArrangement(float x, float y, float width, out float contentHeight)
    {
        Chrome.GroupScope group = Chrome.BeginGroup(
            IdArrangeGroup,
            new Chrome.GroupHead
            {
                Title = Strings.GroupArrangement,
                Description = Strings.GroupArrangementHint,
            },
            x,
            y,
            width);

        float pitch = Chrome.RowPitch();
        float rowY = group.ContentY;

        int direction = m_config.PartyFrames.Direction;
        if (m_direction.Draw(
                ref direction,
                Chrome.Row(Strings.Direction, group.ContentX, rowY, group.ContentWidth, false),
                rowY,
                Chrome.ControlWidth()))
        {
            m_config.PartyFrames.Direction = direction;
            m_config.MarkDirty();
        }

        rowY += pitch;

        int lines = Array.IndexOf(FrameLayout.LineChoices, m_config.PartyFrames.Lines);
        lines = lines < 0 ? 0 : lines;
        if (m_lines.Draw(
                ref lines,
                Chrome.Row(Strings.Lines, group.ContentX, rowY, group.ContentWidth, true),
                rowY,
                Chrome.ControlWidth()))
        {
            m_config.PartyFrames.Lines = FrameLayout.LineChoices[lines];
            m_config.MarkDirty();
        }

        // The arrangement written out, under the control it belongs to and lined up with it.
        // Flush left it read as a stray remark in the middle of the group; under the lines
        // selector it is plainly that selector's answer.
        string caption = this.ArrangementCaption();
        float captionX = Chrome.ControlX(group.ContentX, group.ContentWidth);
        Ink.Draw(
            ImGui.GetWindowDrawList(),
            Ink.Role.Small,
            new Vector2(captionX, rowY + Chrome.RowHeight() + Tokens.Space.Sm),
            Tokens.Col.InkFaint,
            caption);

        float used = rowY - group.ContentY + Chrome.RowHeight() + Tokens.Space.Sm + Ink.LineHeight(Ink.Role.Small);
        Chrome.EndGroupContent(group, used);
        contentHeight = used;
        return group;
    }

    private Chrome.GroupScope DrawSize(float x, float y, float width, out float contentHeight)
    {
        Chrome.GroupScope group = Chrome.BeginGroup(
            IdSizeGroup,
            new Chrome.GroupHead
            {
                Title = Strings.GroupSize,
                Description = Strings.GroupSizeHint,
            },
            x,
            y,
            width);

        float pitch = Chrome.RowPitch();
        float rowY = group.ContentY;

        this.PixelSlider(IdWidth, Strings.FrameWidth, SlotWidth, group, rowY, MinWidth, MaxWidth, false, null, WidthStep);
        rowY += pitch;
        this.PixelSlider(IdHeight, Strings.FrameHeight, SlotHeight, group, rowY, MinHeight, MaxHeight, true, null);
        rowY += pitch;
        this.PixelSlider(IdSpacing, Strings.Spacing, SlotSpacing, group, rowY, 0f, MaxSpacing, true, Strings.SpacingHint);

        float used = rowY - group.ContentY + Chrome.RowHeight();
        Chrome.EndGroupContent(group, used);
        contentHeight = used;
        return group;
    }

    /// <summary>
    /// The swatch beside a colour mode: what the bars would actually be coloured with. The
    /// roles are three stripes in the game's own blue, green and red; "by job" shows four jobs
    /// standing in for all of them. "Fixed colour" draws nothing — it has no colour until the
    /// picker arrives, and an invented one would be a promise we cannot keep.
    /// </summary>
    private static void DrawColourPreview(ImDrawListPtr dl, BarColourMode mode, Vector2 min, Vector2 max)
    {
        switch (mode)
        {
            case BarColourMode.Role:
                Stripes(dl, min, max, Tokens.Col.RoleTank, Tokens.Col.RoleHealer, Tokens.Col.RoleDps);
                break;

            case BarColourMode.Job:
                float width = (max.X - min.X) / JobSample.Length;
                for (int i = 0; i < JobSample.Length; i++)
                {
                    float left = min.X + (i * width);
                    dl.AddRectFilled(
                        new Vector2(left, min.Y),
                        new Vector2(i == JobSample.Length - 1 ? max.X : left + width, max.Y),
                        Jobs.Colour(JobSample[i]));
                }

                break;
        }
    }

    private static void Stripes(ImDrawListPtr dl, Vector2 min, Vector2 max, uint first, uint second, uint third)
    {
        float width = (max.X - min.X) / 3f;
        dl.AddRectFilled(min, new Vector2(min.X + width, max.Y), first);
        dl.AddRectFilled(new Vector2(min.X + width, min.Y), new Vector2(max.X - width, max.Y), second);
        dl.AddRectFilled(new Vector2(max.X - width, min.Y), max, third);
    }

    private static string AnchorLabel(Anchor anchor) => AnchorNames[(int)anchor];

    /// <summary>
    /// One slider row. They differ only in which number they move, so they are written once:
    /// a second copy of a slider row is a second place to fix a spacing bug.
    /// </summary>
    private void PixelSlider(
        string id,
        string label,
        int slot,
        in Chrome.GroupScope group,
        float rowY,
        float min,
        float max,
        bool divider,
        string? hint,
        float step = PixelStep)
    {
        float value = this.SizeValue(slot);
        Chrome.SliderResult result = Chrome.Slider(
            id,
            label,
            this.PixelCaption(slot, value),
            group.ContentX,
            rowY,
            group.ContentWidth,
            value,
            min,
            max,
            hint,
            null,
            divider,
            step);

        // Applied while the hand is still on it, not on release: the frames are on screen
        // right now, and a size you only see once you let go is a size you set twice. The
        // interface scale is the one slider that waits, because it resizes the window under
        // the cursor — these change something you are looking at.
        if (result.Changed)
        {
            this.SetSizeValue(slot, MathF.Round(result.Value));
            m_config.MarkDirty();
        }
    }

    private float SizeValue(int slot) => slot switch
    {
        SlotWidth => m_config.PartyFrames.FrameWidth,
        SlotHeight => m_config.PartyFrames.FrameHeight,
        SlotSpacing => m_config.PartyFrames.Spacing,
        SlotHealthX => m_config.PartyFrames.HpTextX,
        SlotHealthY => m_config.PartyFrames.HpTextY,
        SlotHealthSize => m_config.PartyFrames.HpTextSize,
        SlotNameSize => m_config.PartyFrames.NameSize,
        SlotNameX => m_config.PartyFrames.NameX,
        SlotNameY => m_config.PartyFrames.NameY,
        _ => m_config.PartyFrames.ManaHeight,
    };

    private void SetSizeValue(int slot, float value)
    {
        switch (slot)
        {
            case SlotWidth: m_config.PartyFrames.FrameWidth = value; break;
            case SlotHeight: m_config.PartyFrames.FrameHeight = value; break;
            case SlotSpacing: m_config.PartyFrames.Spacing = value; break;
            case SlotHealthX: m_config.PartyFrames.HpTextX = value; break;
            case SlotHealthY: m_config.PartyFrames.HpTextY = value; break;
            case SlotHealthSize: m_config.PartyFrames.HpTextSize = value; break;
            case SlotNameSize: m_config.PartyFrames.NameSize = value; break;
            case SlotNameX: m_config.PartyFrames.NameX = value; break;
            case SlotNameY: m_config.PartyFrames.NameY = value; break;
            default: m_config.PartyFrames.ManaHeight = value; break;
        }
    }

    /// <summary>A pixel readout, rebuilt only when the number actually changes.</summary>
    private string PixelCaption(int slot, float value)
    {
        int pixels = (int)MathF.Round(value);
        if (m_sizeTextFor[slot] != pixels || m_sizeText[slot] is null)
        {
            m_sizeTextFor[slot] = pixels;
            m_sizeText[slot] = pixels.ToString(CultureInfo.InvariantCulture) + " px";
        }

        return m_sizeText[slot];
    }

    /// <summary>The arrangement in words, rebuilt only when one of the two controls moves.</summary>
    private string ArrangementCaption()
    {
        int lines = m_config.PartyFrames.Lines;
        bool vertical = m_config.PartyFrames.Direction == (int)FrameDirection.Vertical;
        int key = (lines * 2) + (vertical ? 1 : 0);

        if (m_arrangementFor != key || m_arrangementText is null)
        {
            m_arrangementFor = key;
            int perLine = FrameLayout.PerLine(PartySnapshot.Capacity, lines);
            m_arrangementText = lines <= 1
                ? vertical ? Strings.ArrangementOneColumn : Strings.ArrangementOneRow
                : string.Format(
                    CultureInfo.InvariantCulture,
                    vertical ? Strings.ArrangementColumns : Strings.ArrangementRows,
                    lines,
                    perLine);
        }

        return m_arrangementText;
    }

    private string OpacityCaption(float opacity)
    {
        int percent = (int)MathF.Round(opacity * 100f);
        if (percent != m_opacityTextFor)
        {
            m_opacityTextFor = percent;
            m_opacityText = percent.ToString(CultureInfo.InvariantCulture) + " %";
        }

        return m_opacityText;
    }
}
