using System;
using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using WispUI.Appearance;
using WispUI.Core;
using WispUI.Data;
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

    /// <summary>What a bar takes its colour from. FFXIV's own convention, not one of ours.</summary>
    private static readonly string[] ColourModes =
    {
        Strings.ColourByRole,
        Strings.ColourByJob,
        Strings.ColourFixed,
    };

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
    private readonly ArrowSelector<string> m_colour;
    private readonly ArrowSelector<string> m_namePosition;

    private string m_opacityText = string.Empty;
    private int m_opacityTextFor = -1;

    private float m_opacityPreview;
    private bool m_draggingOpacity;

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
        m_colour = new ArrowSelector<string>(
            IdColour,
            ColourModes,
            new ArrowSelectorOptions<string>
            {
                Label = static mode => mode,
                ShowCounter = false,
            });

        m_namePosition = new ArrowSelector<string>(
            IdNamePosition,
            NamePositions,
            new ArrowSelectorOptions<string> { Label = static position => position });
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

        // Field cells first, the compact options after them. Every row takes one step of the
        // ladder whatever sits in it, which is what keeps this column level with the one
        // beside it; putting the fields first means the first label sits right under the head
        // rule instead of a tick floating in the middle of the step below it.
        float pitch = Chrome.RowPitch();
        float rowY = group.ContentY;

        float drop = Chrome.FieldLabel(Strings.BarStyle, group.ContentX, rowY, group.ContentWidth);
        int style = m_config.PartyFrames.BarStyle;
        if (m_style.Draw(ref style, group.ContentX, rowY + drop, group.ContentWidth))
        {
            m_config.PartyFrames.BarStyle = style;
            m_config.MarkDirty();
        }

        rowY += pitch;

        Chrome.FieldLabel(Strings.BarColour, group.ContentX, rowY, group.ContentWidth);
        int colour = m_config.PartyFrames.ColourMode;
        if (m_colour.Draw(ref colour, group.ContentX, rowY + drop, group.ContentWidth))
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
            1f);

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

        // The run of compact options closes the group. It carries a divider on its top edge:
        // that line is where the fields end and the switches begin.
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

        // The content ends at the last row's own height, not at the end of its step: the step
        // exists so the NEXT row starts level, and there is no next row here.
        float used = Chrome.OptionRowEnd(rowY) - group.ContentY;
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

        // The field cell first, the run of compact options under it — the same order every
        // group uses, which is what puts row two of this column level with row two of its
        // neighbour whether either of them holds a dropdown or a tick.
        float pitch = Chrome.RowPitch();
        float rowY = group.ContentY;

        float drop = Chrome.FieldLabel(Strings.NamePosition, group.ContentX, rowY, group.ContentWidth);
        int position = m_config.PartyFrames.NamePosition;
        if (m_namePosition.Draw(ref position, group.ContentX, rowY + drop, group.ContentWidth))
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

        float used = Chrome.OptionRowEnd(rowY) - group.ContentY;
        Chrome.EndGroupContent(group, used);
        contentHeight = used;
        return group;
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
