using System;
using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using WispUI.Appearance;
using WispUI.Core;
using WispUI.Data;
using WispUI.Hud.PartyFrames;
using WispUI.Interface.Widgets;
using WispUI.Localization;
using WispUI.Style;

namespace WispUI.Interface.Screens;

/// <summary>
/// The Base tab of the party frames. It is the first real user of both core widgets: the
/// arrow selector picks the bar style and the colour mode, and the screen implements
/// <see cref="IAppearanceOwner"/>, which is all it takes to get copy and paste.
/// <para>
/// The frames themselves are not drawn yet, so what is set here has no effect on screen —
/// the widgets are infrastructure and were built first on purpose, so that every element
/// after this one inherits them instead of growing its own.
/// </para>
/// </summary>
internal sealed class PartyFramesScreen : IAppearanceOwner
{
    private const string IdHealthGroup = "##wisp-pf-health";
    private const string IdTextGroup = "##wisp-pf-text";
    private const string IdStyle = "##wisp-pf-style";
    private const string IdColour = "##wisp-pf-colour";
    private const string IdOpacity = "##wisp-pf-opacity";
    private const string IdSmooth = "##wisp-pf-smooth";
    private const string IdNamePosition = "##wisp-pf-nameposition";
    private const string IdNameJobColour = "##wisp-pf-namejobcolour";
    private const string IdShortenNames = "##wisp-pf-shortennames";
    private const string IdArrangeGroup = "##wisp-pf-arrange";
    private const string IdSizeGroup = "##wisp-pf-size";
    private const string IdDirection = "##wisp-pf-direction";
    private const string IdLines = "##wisp-pf-lines";
    private const string IdWidth = "##wisp-pf-width";
    private const string IdHeight = "##wisp-pf-height";
    private const string IdSpacing = "##wisp-pf-spacing";

    // The ranges from the spec, 00a74. The useful height is 30-70; the rest is there so a small
    // party can have tall frames.
    private const float MinWidth = 90f;
    private const float MaxWidth = 400f;
    private const float MinHeight = 18f;
    private const float MaxHeight = 150f;
    private const float MaxSpacing = 24f;

    /// <summary>Where a bar takes its colour from.</summary>
    private enum ColourMode
    {
        ByRole,
        ByJob,
        Fixed,
    }

    /// <summary>What a bar takes its colour from. FFXIV's own convention, not one of ours.</summary>
    private static readonly ColourMode[] ColourModes =
    {
        ColourMode.ByRole,
        ColourMode.ByJob,
        ColourMode.Fixed,
    };

    /// <summary>A few jobs standing in for all of them in the "by job" preview.</summary>
    private static readonly uint[] JobSample = { 19u, 24u, 25u, 23u };

    /// <summary>The line counts as text, so the label never builds a string in a draw path.</summary>
    private static readonly System.Collections.Generic.Dictionary<int, string> LineLabels = new() { { 1, "1" }, { 2, "2" }, { 4, "4" } };

    /// <summary>Which way the block of frames runs.</summary>
    private static readonly string[] Directions = { Strings.DirectionVertical, Strings.DirectionHorizontal };

    /// <summary>Where the player name sits on a frame.</summary>
    private static readonly string[] NamePositions =
    {
        Strings.PositionTopLeft,
        Strings.PositionTop,
        Strings.PositionCentre,
        Strings.PositionBottom,
        Strings.PositionBottomLeft,
    };

    private readonly Configuration m_config;
    private readonly ArrowSelector<BarStyle> m_style;
    private readonly ArrowSelector<ColourMode> m_colour;
    private readonly ArrowSelector<string> m_namePosition;

    private string m_opacityText = string.Empty;
    private int m_opacityTextFor = -1;

    private float m_opacityPreview;
    private bool m_draggingOpacity;

    private readonly ArrowSelector<string> m_direction;
    private readonly ArrowSelector<int> m_lines;

    /// <summary>One readout per size slider, rebuilt only when its number changes.</summary>
    private readonly string[] m_sizeText = new string[4];
    private readonly int[] m_sizeTextFor = { -1, -1, -1, -1 };

    private string m_arrangementText = string.Empty;
    private int m_arrangementFor = -1;

    public PartyFramesScreen(Configuration config)
    {
        m_config = config;
        m_opacityPreview = config.PartyFrames.BarOpacity;

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
        m_colour = new ArrowSelector<ColourMode>(
            IdColour,
            ColourModes,
            new ArrowSelectorOptions<ColourMode>
            {
                Label = static mode => mode switch
                {
                    ColourMode.ByRole => Strings.ColourByRole,
                    ColourMode.ByJob => Strings.ColourByJob,
                    _ => Strings.ColourFixed,
                },
                DrawPreview = DrawColourPreview,
                ShowCounter = false,
            });

        m_namePosition = new ArrowSelector<string>(
            IdNamePosition,
            NamePositions,
            new ArrowSelectorOptions<string> { Label = static position => position });

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
    /// what lets the paste panel tell the truth about what will carry over.
    /// </summary>
    public AppearanceFields SupportedFields =>
        AppearanceFields.Colours | AppearanceFields.Texture | AppearanceFields.Opacity | AppearanceFields.Text;

    public AppearanceBlock GetAppearance() => new()
    {
        BarStyle = m_config.PartyFrames.BarStyle,
        ColourMode = m_config.PartyFrames.ColourMode,
        BarOpacity = m_config.PartyFrames.BarOpacity,
        NamePosition = m_config.PartyFrames.NamePosition,
        NameInJobColour = m_config.PartyFrames.NameInJobColour,
        ShortenNames = m_config.PartyFrames.ShortenNames,
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
            m_opacityPreview = source.BarOpacity;
        }

        if ((mask & AppearanceFields.Text) != 0)
        {
            m_config.PartyFrames.NamePosition = source.NamePosition;
            m_config.PartyFrames.NameInJobColour = source.NameInJobColour;
            m_config.PartyFrames.ShortenNames = source.ShortenNames;
        }

        m_config.MarkDirty();
    }

    /// <param name="width">The usable width, with the content padding already taken off.</param>
    public void Draw(float width)
    {
        Vector2 origin = ImGui.GetCursorScreenPos();
        float column = Chrome.ColumnWidth(width);
        float rowTop = origin.Y;

        Chrome.BeginGroupRow();

        // Drawn first and framed afterwards, so the shorter column can be carried down to the
        // taller one's bottom edge. Two groups that each stop where their own rows end leave a
        // step, and every group added later adds another one. Nothing else has to be squared
        // up between the columns: both stack on the same ladder (Chrome.RowPitch).
        Chrome.GroupScope left = this.DrawHealthBar(Chrome.ColumnX(origin.X, width, 0), rowTop, column, out float leftHeight);
        Chrome.GroupScope right = this.DrawNameText(Chrome.ColumnX(origin.X, width, 1), rowTop, column, out float rightHeight);

        float y = rowTop + Chrome.GroupFrameRow(left, leftHeight, right, rightHeight);

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, y - origin.Y + Tokens.Metric.ContentPaddingBottom));
    }

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

        float opacity = m_draggingOpacity ? m_opacityPreview : m_config.PartyFrames.BarOpacity;
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
            true);

        if (result.Changed)
        {
            m_draggingOpacity = true;
            m_opacityPreview = result.Value;
        }

        if (result.Released && m_draggingOpacity)
        {
            m_draggingOpacity = false;
            m_config.PartyFrames.BarOpacity = m_opacityPreview;
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

        int position = m_config.PartyFrames.NamePosition;
        if (m_namePosition.Draw(
                ref position,
                Chrome.Row(Strings.NamePosition, group.ContentX, rowY, group.ContentWidth, false),
                rowY,
                Chrome.ControlWidth()))
        {
            m_config.PartyFrames.NamePosition = position;
            m_config.MarkDirty();
        }

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
    /// The swatch beside a colour mode: what the bars would actually be coloured with. The
    /// roles are three stripes in the game's own blue, green and red; "by job" shows four jobs
    /// standing in for all of them. "Fixed colour" draws nothing — it has no colour until the
    /// picker arrives with the module, and an invented one would be a promise we cannot keep.
    /// </summary>
    private static void DrawColourPreview(ImDrawListPtr dl, ColourMode mode, Vector2 min, Vector2 max)
    {
        switch (mode)
        {
            case ColourMode.ByRole:
                Stripes(dl, min, max, Tokens.Col.RoleTank, Tokens.Col.RoleHealer, Tokens.Col.RoleDps);
                break;

            case ColourMode.ByJob:
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

    /// <summary>
    /// The Layout tab: how the frames are arranged, and how big they are. Kept apart from Base
    /// on purpose — colour and style are shared between elements, size and arrangement belong
    /// to this one and are never copied (CLAUDE.md §5.3).
    /// </summary>
    public void DrawLayout(float width)
    {
        Vector2 origin = ImGui.GetCursorScreenPos();
        float column = Chrome.ColumnWidth(width);
        float rowTop = origin.Y;

        Chrome.BeginGroupRow();
        Chrome.GroupScope left = this.DrawArrangement(Chrome.ColumnX(origin.X, width, 0), rowTop, column, out float leftHeight);
        Chrome.GroupScope right = this.DrawSize(Chrome.ColumnX(origin.X, width, 1), rowTop, column, out float rightHeight);

        float y = rowTop + Chrome.GroupFrameRow(left, leftHeight, right, rightHeight);

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, y - origin.Y + Tokens.Metric.ContentPaddingBottom));
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

        int lines = System.Array.IndexOf(FrameLayout.LineChoices, m_config.PartyFrames.Lines);
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

        this.PixelSlider(IdWidth, Strings.FrameWidth, 0, group, rowY, MinWidth, MaxWidth, false, null);
        rowY += pitch;
        this.PixelSlider(IdHeight, Strings.FrameHeight, 1, group, rowY, MinHeight, MaxHeight, true, null);
        rowY += pitch;
        this.PixelSlider(IdSpacing, Strings.Spacing, 2, group, rowY, 0f, MaxSpacing, true, Strings.SpacingHint);

        float used = rowY - group.ContentY + Chrome.RowHeight();
        Chrome.EndGroupContent(group, used);
        contentHeight = used;
        return group;
    }

    /// <summary>
    /// One row of the Size group. The four differ only in which number they move, so they are
    /// written once: a second copy of a slider row is a second place to fix a spacing bug.
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
        string? hint)
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
            divider);

        // Applied while the hand is still on it, not on release: the frames are on screen
        // right now, and a size you only see once you let go is a size you set twice. The
        // interface scale is the one slider that waits, because it resizes the window under
        // the cursor — this one changes something you are looking at.
        if (result.Changed)
        {
            this.SetSizeValue(slot, MathF.Round(result.Value));
            m_config.MarkDirty();
        }
    }

    private float SizeValue(int slot) => slot switch
    {
        0 => m_config.PartyFrames.FrameWidth,
        1 => m_config.PartyFrames.FrameHeight,
        _ => m_config.PartyFrames.Spacing,
    };

    private void SetSizeValue(int slot, float value)
    {
        switch (slot)
        {
            case 0: m_config.PartyFrames.FrameWidth = value; break;
            case 1: m_config.PartyFrames.FrameHeight = value; break;
            default: m_config.PartyFrames.Spacing = value; break;
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
