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
    private const string IdStyle = "##wisp-pf-style";
    private const string IdColour = "##wisp-pf-colour";
    private const string IdOpacity = "##wisp-pf-opacity";
    private const string IdSmooth = "##wisp-pf-smooth";

    /// <summary>What a bar takes its colour from. FFXIV's own convention, not one of ours.</summary>
    private static readonly string[] ColourModes =
    {
        Strings.ColourByRole,
        Strings.ColourByJob,
        Strings.ColourFixed,
    };

    private readonly Configuration m_config;
    private readonly ArrowSelector<BarStyle> m_style;
    private readonly ArrowSelector<string> m_colour;

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
    }

    public string DisplayName => Strings.NavPartyFrames;

    /// <summary>
    /// What this element has. Shape, text and background are not among them yet, and saying
    /// so is what lets the paste panel tell the truth about what will carry over.
    /// </summary>
    public AppearanceFields SupportedFields =>
        AppearanceFields.Colours | AppearanceFields.Texture | AppearanceFields.Opacity;

    public AppearanceBlock GetAppearance() => new()
    {
        BarStyle = m_config.PartyFrames.BarStyle,
        ColourMode = m_config.PartyFrames.ColourMode,
        BarOpacity = m_config.PartyFrames.BarOpacity,
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

        m_config.MarkDirty();
    }

    /// <param name="width">The usable width, with the content padding already taken off.</param>
    public void Draw(float width)
    {
        Vector2 origin = ImGui.GetCursorScreenPos();
        float x = origin.X;
        float y = origin.Y;
        float column = Chrome.ColumnWidth(width);
        float left = Chrome.ColumnX(x, width, 0);
        float right = Chrome.ColumnX(x, width, 1);

        y += Chrome.SectionHeader(Strings.SectionAppearance, Strings.SectionAppearanceHint, x, y);

        // --- row one: the two selectors, one per column ---
        // No tooltip on a field label: a label painted into the draw list is not an ImGui
        // item, so a tooltip hung off it would answer to whatever item came before it.
        float drop = Chrome.FieldLabel(Strings.BarStyle, left, y, column);

        int style = m_config.PartyFrames.BarStyle;
        if (m_style.Draw(ref style, left, y + drop, column))
        {
            m_config.PartyFrames.BarStyle = style;
            m_config.MarkDirty();
        }

        Chrome.FieldLabel(Strings.BarColour, right, y, column);

        int colour = m_config.PartyFrames.ColourMode;
        if (m_colour.Draw(ref colour, right, y + drop, column))
        {
            m_config.PartyFrames.ColourMode = colour;
            m_config.MarkDirty();
        }

        y += drop + ArrowSelector<BarStyle>.Height + Tokens.Metric.RowGap;

        // --- row two: opacity beside the smooth-bars option ---
        float opacity = m_draggingOpacity ? m_opacityPreview : m_config.PartyFrames.BarOpacity;
        Chrome.SliderResult result = Chrome.Slider(
            IdOpacity,
            Strings.BarOpacity,
            this.OpacityCaption(opacity),
            left,
            y,
            column,
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

        // The check box sits on the control line of its row, so it lines up with the slider
        // track beside it rather than with the label above it.
        float controlLine = y + Ink.LineHeight(Ink.Role.Body) + Tokens.Space.Sm;
        float checkTop = MathF.Round(controlLine + ((Tokens.Metric.SliderHeight - Chrome.CheckBoxHeight()) * 0.5f));
        if (Chrome.CheckBox(
                IdSmooth,
                Strings.SmoothBars,
                right,
                checkTop,
                m_config.PartyFrames.SmoothBars,
                Strings.SmoothBarsTooltip))
        {
            m_config.PartyFrames.SmoothBars = !m_config.PartyFrames.SmoothBars;
            m_config.MarkDirty();
        }

        y += result.Height;

        // Tells the scroll area how tall the screen turned out, the air under the last row
        // included. Every block above advanced by its own measured height.
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, y - origin.Y + Tokens.Metric.ContentPaddingBottom));
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
