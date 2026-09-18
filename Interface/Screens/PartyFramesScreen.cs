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
    private const string IdShieldGroup = "##wisp-pf-shieldgroup";
    private const string IdShieldStyle = "##wisp-pf-shieldstyle";
    private const string IdShieldColour = "##wisp-pf-shieldcolour";
    private const string IdShieldOpacity = "##wisp-pf-shieldopacity";
    private const string IdManaStyle = "##wisp-pf-manastyle";
    private const string IdManaHeight = "##wisp-pf-manaheight";
    private const string IdManaTanks = "##wisp-pf-manatanks";
    private const string IdManaHealers = "##wisp-pf-manahealers";
    private const string IdManaDps = "##wisp-pf-manadps";
    private const string IdMouseGroup = "##wisp-pf-mouse";
    private const string IdMouseover = "##wisp-pf-mouseover";
    private const string IdMouseoverCasting = "##wisp-pf-mocast";
    private const string IdHighlight = "##wisp-pf-highlight";
    private const string IdLeaderGroup = "##wisp-pf-leader";
    private const string IdLeaderSize = "##wisp-pf-leadersize";
    private const string IdLeaderPosition = "##wisp-pf-leaderposition";
    private const string IdLeaderX = "##wisp-pf-leaderx";
    private const string IdLeaderY = "##wisp-pf-leadery";
    private const string IdNumberGroup = "##wisp-pf-number";
    private const string IdNumberSize = "##wisp-pf-numbersize";
    private const string IdNumberPosition = "##wisp-pf-numberposition";
    private const string IdNumberX = "##wisp-pf-numberx";
    private const string IdNumberY = "##wisp-pf-numbery";
    private const string IdIconGroup = "##wisp-pf-icon";
    private const string IdIconStyle = "##wisp-pf-iconstyle";
    private const string IdIconSize = "##wisp-pf-iconsize";
    private const string IdFont = "##wisp-pf-font";
    private const string IdTextEdge = "##wisp-pf-textedge";
    private const string IdTextWeight = "##wisp-pf-textweight";
    private const string IdTextStyleGroup = "##wisp-pf-textstyle";
    private const string IdBindingsGroup = "##wisp-pf-bindings";
    private const string IdBindingKey = "##wisp-pf-bindkey";
    private const string IdBindingJob = "##wisp-pf-bindjob";
    private const string IdBindingAction = "##wisp-pf-bindaction";
    private const string IdBindingAdd = "##wisp-pf-bindadd";
    private const string IdBindingRemoveRow = "##wisp-pf-bindremoverow";
    private const string IdBindingName = "##wisp-pf-bindname";
    private const string IdBindingOn = "##wisp-pf-bindon";
    private const string IdIconPosition = "##wisp-pf-iconposition";
    private const string IdIconX = "##wisp-pf-iconx";
    private const string IdIconY = "##wisp-pf-icony";
    private const string IdIconHideDps = "##wisp-pf-iconhidedps";
    private const string IdArrangeGroup = "##wisp-pf-arrange";
    private const string IdSizeGroup = "##wisp-pf-size";
    private const string IdAuraGroup = "##wisp-pf-auras";
    private const string IdCleanseGroup = "##wisp-pf-cleansegroup";
    private const string IdRescueGroup = "##wisp-pf-rescuegroup";
    private const string IdShowAuras = "##wisp-pf-showauras";
    private const string IdAuraPosition = "##wisp-pf-auraposition";
    private const string IdAuraSize = "##wisp-pf-aurasize";
    private const string IdAuraX = "##wisp-pf-aurax";
    private const string IdAuraY = "##wisp-pf-auray";
    private const string IdAuraMax = "##wisp-pf-auramax";
    private const string IdAuraStacks = "##wisp-pf-aurastacks";
    private const string IdAuraSwipe = "##wisp-pf-auraswipe";
    private const string IdAuraTooltips = "##wisp-pf-auratips";
    private const string IdPreviewAuras = "##wisp-pf-aurapreview";
    private const string IdCleanseWhenAble = "##wisp-pf-cleanseable";
    private const string IdCleanseColour = "##wisp-pf-cleansecolour";
    private const string IdCleanseThickness = "##wisp-pf-cleansethick";
    private const string IdCleanseOpacity = "##wisp-pf-cleanseopacity";
    private const string IdRaiseMarkGroup = "##wisp-pf-raisemarkgroup";
    private const string IdRaiseMark = "##wisp-pf-raisemark";
    private const string IdRaiseColour = "##wisp-pf-raisecolour";
    private const string IdRaiseThickness = "##wisp-pf-raisethick";
    private const string IdRaiseOpacity = "##wisp-pf-raiseopacity";

    private const float MinCleanseThickness = 1f;
    private const float MaxCleanseThickness = 8f;
    private const string IdBuffGroup = "##wisp-pf-buffs";
    private const string IdShowBuffs = "##wisp-pf-showbuffs";
    private const string IdOwnBuffs = "##wisp-pf-ownbuffs";
    private const string IdBuffPosition = "##wisp-pf-buffposition";
    private const string IdBuffSize = "##wisp-pf-buffsize";
    private const string IdBuffX = "##wisp-pf-buffx";
    private const string IdBuffY = "##wisp-pf-buffy";
    private const string IdBuffMax = "##wisp-pf-buffmax";
    private const string IdOtherGroup = "##wisp-pf-others";
    private const string IdShowOther = "##wisp-pf-showother";
    private const string IdOtherPosition = "##wisp-pf-otherposition";
    private const string IdOtherSize = "##wisp-pf-othersize";
    private const string IdOtherX = "##wisp-pf-otherx";
    private const string IdOtherY = "##wisp-pf-othery";
    private const string IdOtherMax = "##wisp-pf-othermax";
    private const string IdCleanse = "##wisp-pf-cleanse";
    private const string IdShowRaise = "##wisp-pf-showraise";
    private const string IdShowInvuln = "##wisp-pf-showinvuln";
    private const string IdRescuePosition = "##wisp-pf-rescueposition";
    private const string IdRescueSize = "##wisp-pf-rescuesize";
    private const string IdRescueX = "##wisp-pf-rescuex";
    private const string IdRescueY = "##wisp-pf-rescuey";

    /// <summary>The three answers to "say that something can be cleansed", in that order.</summary>
    /// <summary>
    /// The shapes a frame mark can take, in the order the arrows walk them. One list for every
    /// mark there is: the cleanse mark and the raise mark offer exactly the same four, because
    /// they are the same drawing asked to say different things.
    /// </summary>
    private static readonly FrameMarkStyle[] MarkStyles =
        { FrameMarkStyle.Border, FrameMarkStyle.Full, FrameMarkStyle.Bar, FrameMarkStyle.None };

    /// <summary>The name of a shape. Shared, so the two marks can never drift apart in wording.</summary>
    private static string MarkLabel(FrameMarkStyle style) => style switch
    {
        FrameMarkStyle.Border => Strings.MarkBorder,
        FrameMarkStyle.Full => Strings.MarkFull,
        FrameMarkStyle.Bar => Strings.MarkBar,
        _ => Strings.MarkNone,
    };

    private const float MinAuraSize = 10f;
    private const float MaxAuraSize = 48f;

    private const string IdGameListGroup = "##wisp-pf-gamelist";
    private const string IdHideNativeList = "##wisp-pf-hidenative";
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

    /// <summary>
    /// How far a text or an icon may be nudged off its anchor, either way. The number lives
    /// with the configuration because the element draws within the same reach — one figure,
    /// not a slider range and a clip that have to be kept in step by hand.
    /// </summary>
    private const float MaxOffset = Configuration.MaxTextOffset;

    /// <summary>Mana strip thickness, in pixels and nothing else (spec §3).</summary>
    private const float MinManaHeight = 2f;
    private const float MaxManaHeight = 16f;

    /// <summary>
    /// Job icon size, square and in pixels. The top end is set by the tallest frame rather
    /// than by the icon: at 150 px a 48 px icon is still a badge on a frame and not the frame
    /// itself.
    /// </summary>
    private const float MinIconSize = 8f;
    private const float MaxIconSize = 48f;

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

    // What the number at the end of a row means, when it is typed into: a pixel slider holds
    // the number it shows, opacity holds a fraction of the percentage it shows.
    private const float PixelEditScale = 1f;
    private const float OpacityEditScale = 100f;

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
    private const int SlotIconSize = 10;
    private const int SlotIconX = 11;
    private const int SlotIconY = 12;
    private const int SlotNumberSize = 13;
    private const int SlotNumberX = 14;
    private const int SlotNumberY = 15;
    private const int SlotLeaderSize = 16;
    private const int SlotLeaderX = 17;
    private const int SlotLeaderY = 18;
    private const int SlotAuraSize = 19;
    private const int SlotAuraX = 20;
    private const int SlotAuraY = 21;
    private const int SlotAuraMax = 22;
    private const int SlotRescueSize = 23;
    private const int SlotRescueX = 24;
    private const int SlotRescueY = 25;
    private const int SlotBuffSize = 26;
    private const int SlotBuffX = 27;
    private const int SlotBuffY = 28;
    private const int SlotBuffMax = 29;
    private const int SlotOtherSize = 30;
    private const int SlotOtherX = 31;
    private const int SlotOtherY = 32;
    private const int SlotOtherMax = 33;
    private const int SlotCleanseThickness = 34;

    /// <summary>🔴 The last slot. Adding one below this means moving the line under it too.</summary>
    private const int SlotRaiseThickness = 35;

    /// <summary>
    /// How many slots there are, derived from the last one rather than written down.
    /// <para>
    /// 🔴 It was a literal, and a literal is a second place to remember. Adding
    /// <see cref="SlotRaiseThickness"/> without touching it put a slider's index one past the
    /// end of every array sized from this, and the settings window threw on the frame the new
    /// group first drew (Florian, 2026-09-18, with the stack trace). Written this way the two
    /// cannot disagree: a new slot takes the next number, moves this line down, and every
    /// array grows with it.
    /// </para>
    /// </summary>
    private const int SlotCount = SlotRaiseThickness + 1;

    /// <summary>The three weights, in the order the segments sit. Built once, not per frame.</summary>
    private static readonly string[] WeightNames =
    {
        Strings.TextWeightNormal,
        Strings.TextWeightMedium,
        Strings.TextWeightBold,
    };

    /// <summary>The three edges, in the order the segments sit. Built once, not per frame.</summary>
    private static readonly string[] EdgeNames =
    {
        Strings.TextEdgeNone,
        Strings.TextEdgeShadow,
        Strings.TextEdgeOutline,
    };

    /// <summary>What a bar takes its colour from. FFXIV's own convention, not one of ours.</summary>
    private static readonly BarColourMode[] ColourModes =
    {
        BarColourMode.Role,
        BarColourMode.Job,
        BarColourMode.Fixed,
    };

    /// <summary>A few jobs standing in for all of them in the "by job" preview.</summary>
    private static readonly uint[] JobSample = { 19u, 24u, 25u, 23u };

    /// <summary>
    /// The two icon sets, in the order <see cref="JobIconStyle"/> numbers them. Both words
    /// stand side by side in the row, which is the whole reason this is not a selector.
    /// </summary>
    private static readonly string[] IconStyleNames = { Strings.IconStyleFramed, Strings.IconStylePlain };

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
    private readonly ArrowSelector<FontChoice> m_font;
    private readonly ArrowSelector<JobEntry> m_jobSelector;
    private readonly ArrowSelector<ActionEntry> m_actionPicker;

    /// <summary>Which job the bindings tab is showing, and which row is waiting for a press.</summary>
    private int m_bindingJob = -1;
    private int m_listening = -1;

    /// <summary>The actions the picker offers, refilled when the job changes.</summary>
    private readonly System.Collections.Generic.List<ActionEntry> m_actionChoices = new();
    private uint m_choicesFor = uint.MaxValue;
    private readonly ArrowSelector<Anchor> m_healthPosition;
    private readonly ArrowSelector<BarStyle> m_shieldStyle;
    private readonly ArrowSelector<string> m_manaStyle;
    private readonly ArrowSelector<Anchor> m_iconPosition;

    private readonly ArrowSelector<Anchor> m_numberPosition;
    private readonly ArrowSelector<Anchor> m_leaderPosition;
    private readonly ArrowSelector<Anchor> m_auraPosition;
    private readonly ArrowSelector<Anchor> m_rescuePosition;
    private readonly ArrowSelector<FrameMarkStyle> m_cleanse;
    private readonly ArrowSelector<FrameMarkStyle> m_raiseMark;
    private readonly ArrowSelector<Anchor> m_buffPosition;
    private readonly ArrowSelector<Anchor> m_otherPosition;
    private readonly ArrowSelector<NameShortening> m_shortening;
    private readonly ArrowSelector<string> m_direction;
    private readonly ArrowSelector<int> m_lines;

    private string m_opacityText = string.Empty;
    private int m_opacityTextFor = -1;

    // 🔴 A second cache rather than a second caller of the first. One slot shared by two
    // sliders rebuilds its string every frame as soon as the two values differ, which is an
    // allocation per frame in the draw path for a caption nobody asked to change (§7.1).
    private string m_shieldOpacityText = string.Empty;
    private int m_shieldOpacityTextFor = -1;

    private string m_cleanseOpacityText = string.Empty;
    private int m_cleanseOpacityTextFor = -1;

    private string m_raiseOpacityText = string.Empty;
    private int m_raiseOpacityTextFor = -1;

    /// <summary>One readout per slider, rebuilt only when its number changes.</summary>
    private readonly string[] m_sizeText = new string[SlotCount];
    /// <summary>
    /// 🔴 Sized from <see cref="SlotCount"/> and filled, never written out by hand. It was a
    /// list of nineteen minus-ones, and adding a twentieth slider walked off the end of it —
    /// a crash that could only happen on the one tab that had just been built
    /// (Florian, 2026-09-13). Anything counted by SlotCount is allocated from SlotCount.
    /// </summary>
    private readonly int[] m_sizeTextFor = NoSizeText();

    private static int[] NoSizeText()
    {
        var slots = new int[SlotCount];
        Array.Fill(slots, -1);
        return slots;
    }

    private string m_arrangementText = string.Empty;
    private int m_arrangementFor = -1;

    public PartyFramesScreen(Configuration config)
    {
        m_config = config;

        // A long list: worth a popup, and long enough that the search box earns its place.
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

        // The shield picks from a list of its own, and a selector of its own — one widget
        // cannot serve two rows, because it carries the state of its popup and its search box.
        //
        // 🔴 The two lists are FILTERED VIEWS of one list, not two lists of files. A style
        // says where it may be offered and the filter does the rest, so a diagonal pattern
        // never turns up as a health-bar fill and a plain gradient stays available to both.
        // Adding a style is one entry with one flag, never an entry in two places.
        m_shieldStyle = new ArrowSelector<BarStyle>(
            IdShieldStyle,
            BarStyles.ForShield,
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

        // The one selector in the suite with a searchable list, because it is the one whose
        // list the player controls: six faces shipped, and however many they drop in their
        // font folder. Walking that with two arrows is not a list, it is a queue.
        m_font = new ArrowSelector<FontChoice>(
            IdFont,
            FontLibrary.All,
            new ArrowSelectorOptions<FontChoice>
            {
                Label = static face => face.Name,
                EnablePopupList = true,
                EnableSearch = true,
                ShowCounter = false,
            });

        // Twenty-one jobs is well past what two arrows are for, so this one carries the popup
        // and its search — the same reasoning as the font list.
        m_jobSelector = new ArrowSelector<JobEntry>(
            IdBindingJob,
            JobList.All,
            new ArrowSelectorOptions<JobEntry>
            {
                Label = static job => job.Name,
                EnablePopupList = true,
                EnableSearch = true,
                ShowCounter = false,
            });

        // The one list that changes while the window is open: it holds whatever the chosen
        // job can aim at a party member, and is refilled in place when that job changes.
        m_actionPicker = new ArrowSelector<ActionEntry>(
            IdBindingAction,
            m_actionChoices,
            new ArrowSelectorOptions<ActionEntry>
            {
                Label = static action => action.Name,
                EnablePopupList = true,
                EnableSearch = true,
                ShowCounter = false,

                // No arrows and no box. Stepping through a job's whole action list one at a
                // time is not a way anybody would use it, and a field drawn round the name
                // would make the row read as two settings rather than one binding (Florian,
                // 2026-09-12).
                HideArrows = true,
                Flat = true,

                // The action's own icon, which is how a spell is recognised before its name is
                // read. Icons.Handle caches the lookup and hands back a null handle for
                // anything not loaded, which the selector simply does not draw.
                // Square, because an action icon is. The default preview strip is wide and
                // short for bar fills, and an icon stretched into it comes out smeared.
                PreviewSize = Tokens.Px(22f, 22f),

                DrawPreview = static (dl, action, min, max) =>
                {
                    ImTextureID icon = Icons.Handle(action.Icon);

                    if (!icon.IsNull)
                    {
                        dl.AddImage(icon, min, max);
                    }
                },
            });

        m_iconPosition = new ArrowSelector<Anchor>(
            IdIconPosition,
            Anchors.All,
            new ArrowSelectorOptions<Anchor> { Label = AnchorLabel, EnablePopupList = true, ShowCounter = false });

        m_numberPosition = new ArrowSelector<Anchor>(
            IdNumberPosition,
            Anchors.All,
            new ArrowSelectorOptions<Anchor> { Label = AnchorLabel, EnablePopupList = true, ShowCounter = false });

        m_leaderPosition = new ArrowSelector<Anchor>(
            IdLeaderPosition,
            Anchors.All,
            new ArrowSelectorOptions<Anchor> { Label = AnchorLabel, EnablePopupList = true, ShowCounter = false });

        m_auraPosition = new ArrowSelector<Anchor>(
            IdAuraPosition,
            Anchors.All,
            new ArrowSelectorOptions<Anchor> { Label = AnchorLabel, EnablePopupList = true, ShowCounter = false, HideArrows = true });

        m_buffPosition = new ArrowSelector<Anchor>(
            IdBuffPosition,
            Anchors.All,
            new ArrowSelectorOptions<Anchor> { Label = AnchorLabel, EnablePopupList = true, ShowCounter = false, HideArrows = true });

        m_otherPosition = new ArrowSelector<Anchor>(
            IdOtherPosition,
            Anchors.All,
            new ArrowSelectorOptions<Anchor> { Label = AnchorLabel, EnablePopupList = true, ShowCounter = false, HideArrows = true });

        m_rescuePosition = new ArrowSelector<Anchor>(
            IdRescuePosition,
            Anchors.All,
            new ArrowSelectorOptions<Anchor> { Label = AnchorLabel, EnablePopupList = true, ShowCounter = false, HideArrows = true });

        // Two selectors over one list of shapes. They cannot be one widget — a selector
        // carries the state of its own popup — but they share the list and the labelling, so
        // a shape added to MarkStyles turns up in both without either being touched.
        m_cleanse = new ArrowSelector<FrameMarkStyle>(
            IdCleanse,
            MarkStyles,
            new ArrowSelectorOptions<FrameMarkStyle>
            {
                Label = MarkLabel,
                ShowCounter = false,
                EnablePopupList = true,
                HideArrows = true,
            });

        m_raiseMark = new ArrowSelector<FrameMarkStyle>(
            IdRaiseMark,
            MarkStyles,
            new ArrowSelectorOptions<FrameMarkStyle>
            {
                Label = MarkLabel,
                ShowCounter = false,
                EnablePopupList = true,
                HideArrows = true,
            });


        m_shortening = new ArrowSelector<NameShortening>(
            IdShortenNames,
            PlayerName.All,
            new ArrowSelectorOptions<NameShortening>
            {
                Label = static mode => mode switch
                {
                    NameShortening.Surname => Strings.ShorteningSurname,
                    NameShortening.Forename => Strings.ShorteningForename,
                    _ => Strings.ShorteningFull,
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
        AppearanceFields.Colours | AppearanceFields.Texture | AppearanceFields.Opacity
        | AppearanceFields.Text | AppearanceFields.Icon;

    public AppearanceBlock GetAppearance() => new()
    {
        BarStyleName = m_config.PartyFrames.BarStyleName,
        ColourMode = m_config.PartyFrames.ColourMode,
        BarOpacity = m_config.PartyFrames.BarOpacity,
        NamePosition = m_config.PartyFrames.NamePosition,
        NameSize = m_config.PartyFrames.NameSize,
        NameX = m_config.PartyFrames.NameX,
        NameY = m_config.PartyFrames.NameY,
        NameInJobColour = m_config.PartyFrames.NameInJobColour,
        NameShortening = m_config.PartyFrames.NameShortening,
        ShowHealthText = m_config.PartyFrames.ShowHealthText,
        HpTextMode = m_config.PartyFrames.HpTextMode,
        HpTextSize = m_config.PartyFrames.HpTextSize,
        HpTextPosition = m_config.PartyFrames.HpTextPosition,
        HpTextX = m_config.PartyFrames.HpTextX,
        HpTextY = m_config.PartyFrames.HpTextY,
        ShowJobIcon = m_config.PartyFrames.ShowJobIcon,
        JobIconStyle = m_config.PartyFrames.JobIconStyle,
        JobIconSize = m_config.PartyFrames.JobIconSize,
        JobIconPosition = m_config.PartyFrames.JobIconPosition,
        JobIconX = m_config.PartyFrames.JobIconX,
        JobIconY = m_config.PartyFrames.JobIconY,
        JobIconHideDps = m_config.PartyFrames.JobIconHideDps,
        ShowPartyNumber = m_config.PartyFrames.ShowPartyNumber,
        PartyNumberSize = m_config.PartyFrames.PartyNumberSize,
        PartyNumberPosition = m_config.PartyFrames.PartyNumberPosition,
        PartyNumberX = m_config.PartyFrames.PartyNumberX,
        PartyNumberY = m_config.PartyFrames.PartyNumberY,
        ShowLeaderIcon = m_config.PartyFrames.ShowLeaderIcon,
        LeaderIconSize = m_config.PartyFrames.LeaderIconSize,
        LeaderIconPosition = m_config.PartyFrames.LeaderIconPosition,
        LeaderIconX = m_config.PartyFrames.LeaderIconX,
        LeaderIconY = m_config.PartyFrames.LeaderIconY,
        ShowAuras = m_config.PartyFrames.ShowAuras,
        AuraSize = m_config.PartyFrames.AuraSize,
        AuraPosition = m_config.PartyFrames.AuraPosition,
        AuraX = m_config.PartyFrames.AuraX,
        AuraY = m_config.PartyFrames.AuraY,
        AuraMaxCount = m_config.PartyFrames.AuraMaxCount,
        AuraShowStacks = m_config.PartyFrames.AuraShowStacks,
        AuraSwipe = m_config.PartyFrames.AuraSwipe,
        ShowBuffs = m_config.PartyFrames.ShowBuffs,
        OwnBuffsOnly = m_config.PartyFrames.OwnBuffsOnly,
        BuffSize = m_config.PartyFrames.BuffSize,
        BuffPosition = m_config.PartyFrames.BuffPosition,
        BuffX = m_config.PartyFrames.BuffX,
        BuffY = m_config.PartyFrames.BuffY,
        BuffMaxCount = m_config.PartyFrames.BuffMaxCount,
        BuffShowStacks = m_config.PartyFrames.BuffShowStacks,
        BuffSwipe = m_config.PartyFrames.BuffSwipe,
        ShowOtherBuffs = m_config.PartyFrames.ShowOtherBuffs,
        OtherSize = m_config.PartyFrames.OtherSize,
        OtherPosition = m_config.PartyFrames.OtherPosition,
        OtherX = m_config.PartyFrames.OtherX,
        OtherY = m_config.PartyFrames.OtherY,
        OtherMaxCount = m_config.PartyFrames.OtherMaxCount,
        ShowRaiseIcon = m_config.PartyFrames.ShowRaiseIcon,
        ShowInvulnIcon = m_config.PartyFrames.ShowInvulnIcon,
        RescueIconSize = m_config.PartyFrames.RescueIconSize,
        RescueIconPosition = m_config.PartyFrames.RescueIconPosition,
        RescueIconX = m_config.PartyFrames.RescueIconX,
        RescueIconY = m_config.PartyFrames.RescueIconY,
        CleanseMark = m_config.PartyFrames.CleanseMark,
        CleanseOnlyWhenAble = m_config.PartyFrames.CleanseOnlyWhenAble,
        CleanseColour = m_config.PartyFrames.CleanseColour,
        CleanseThickness = m_config.PartyFrames.CleanseThickness,
        CleanseOpacity = m_config.PartyFrames.CleanseOpacity,
        RaiseMark = m_config.PartyFrames.RaiseMark,
        RaiseColour = m_config.PartyFrames.RaiseColour,
        RaiseThickness = m_config.PartyFrames.RaiseThickness,
        RaiseOpacity = m_config.PartyFrames.RaiseOpacity,
    };

    public void ApplyAppearance(AppearanceBlock source, AppearanceFields mask)
    {
        if ((mask & AppearanceFields.Texture) != 0)
        {
            m_config.PartyFrames.BarStyleName = source.BarStyleName;
        }

        if ((mask & AppearanceFields.Colours) != 0)
        {
            m_config.PartyFrames.ColourMode = source.ColourMode;
            m_config.PartyFrames.CleanseMark = source.CleanseMark;
            m_config.PartyFrames.CleanseOnlyWhenAble = source.CleanseOnlyWhenAble;
            m_config.PartyFrames.CleanseColour = source.CleanseColour;
            m_config.PartyFrames.CleanseThickness = source.CleanseThickness;
            m_config.PartyFrames.CleanseOpacity = source.CleanseOpacity;
            m_config.PartyFrames.RaiseMark = source.RaiseMark;
            m_config.PartyFrames.RaiseColour = source.RaiseColour;
            m_config.PartyFrames.RaiseThickness = source.RaiseThickness;
            m_config.PartyFrames.RaiseOpacity = source.RaiseOpacity;
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
            m_config.PartyFrames.NameShortening = source.NameShortening;
            m_config.PartyFrames.ShowHealthText = source.ShowHealthText;
            m_config.PartyFrames.HpTextMode = source.HpTextMode;
            m_config.PartyFrames.HpTextSize = source.HpTextSize;
            m_config.PartyFrames.HpTextPosition = source.HpTextPosition;
            m_config.PartyFrames.HpTextX = source.HpTextX;
            m_config.PartyFrames.HpTextY = source.HpTextY;
        }

        if ((mask & AppearanceFields.Icon) != 0)
        {
            m_config.PartyFrames.ShowJobIcon = source.ShowJobIcon;
            m_config.PartyFrames.JobIconStyle = source.JobIconStyle;
            m_config.PartyFrames.JobIconSize = source.JobIconSize;
            m_config.PartyFrames.JobIconPosition = source.JobIconPosition;
            m_config.PartyFrames.JobIconX = source.JobIconX;
            m_config.PartyFrames.JobIconY = source.JobIconY;
            m_config.PartyFrames.JobIconHideDps = source.JobIconHideDps;
            m_config.PartyFrames.ShowPartyNumber = source.ShowPartyNumber;
            m_config.PartyFrames.PartyNumberSize = source.PartyNumberSize;
            m_config.PartyFrames.PartyNumberPosition = source.PartyNumberPosition;
            m_config.PartyFrames.PartyNumberX = source.PartyNumberX;
            m_config.PartyFrames.PartyNumberY = source.PartyNumberY;
            m_config.PartyFrames.ShowLeaderIcon = source.ShowLeaderIcon;
            m_config.PartyFrames.LeaderIconSize = source.LeaderIconSize;
            m_config.PartyFrames.LeaderIconPosition = source.LeaderIconPosition;
            m_config.PartyFrames.LeaderIconX = source.LeaderIconX;
            m_config.PartyFrames.LeaderIconY = source.LeaderIconY;
            m_config.PartyFrames.ShowAuras = source.ShowAuras;
            m_config.PartyFrames.AuraSize = source.AuraSize;
            m_config.PartyFrames.AuraPosition = source.AuraPosition;
            m_config.PartyFrames.AuraX = source.AuraX;
            m_config.PartyFrames.AuraY = source.AuraY;
            m_config.PartyFrames.AuraMaxCount = source.AuraMaxCount;
            m_config.PartyFrames.AuraShowStacks = source.AuraShowStacks;
            m_config.PartyFrames.AuraSwipe = source.AuraSwipe;
            m_config.PartyFrames.ShowBuffs = source.ShowBuffs;
            m_config.PartyFrames.OwnBuffsOnly = source.OwnBuffsOnly;
            m_config.PartyFrames.BuffSize = source.BuffSize;
            m_config.PartyFrames.BuffPosition = source.BuffPosition;
            m_config.PartyFrames.BuffX = source.BuffX;
            m_config.PartyFrames.BuffY = source.BuffY;
            m_config.PartyFrames.BuffMaxCount = source.BuffMaxCount;
            m_config.PartyFrames.BuffShowStacks = source.BuffShowStacks;
            m_config.PartyFrames.BuffSwipe = source.BuffSwipe;
            m_config.PartyFrames.ShowOtherBuffs = source.ShowOtherBuffs;
            m_config.PartyFrames.OtherSize = source.OtherSize;
            m_config.PartyFrames.OtherPosition = source.OtherPosition;
            m_config.PartyFrames.OtherX = source.OtherX;
            m_config.PartyFrames.OtherY = source.OtherY;
            m_config.PartyFrames.OtherMaxCount = source.OtherMaxCount;
            m_config.PartyFrames.ShowRaiseIcon = source.ShowRaiseIcon;
            m_config.PartyFrames.ShowInvulnIcon = source.ShowInvulnIcon;
            m_config.PartyFrames.RescueIconSize = source.RescueIconSize;
            m_config.PartyFrames.RescueIconPosition = source.RescueIconPosition;
            m_config.PartyFrames.RescueIconX = source.RescueIconX;
            m_config.PartyFrames.RescueIconY = source.RescueIconY;
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
        // 🔴 Seven groups, so one of them ends up alone in its row — there is no arrangement
        // of seven into pairs. Which one is alone is the only real choice, and it is the mouse:
        // it is the one group here that is not about what a frame SHOWS, so a row of its own
        // reads as the separate concern it is rather than as a leftover. The other three rows
        // pair by subject — the bar and what is laid on it, the two extra readings taken off
        // it, and the name beside the lettering that draws every text.
        Chrome.BeginGroupRow();
        Chrome.GroupScope bar = this.DrawHealthBar(Chrome.ColumnX(origin.X, width, 0), y, column, out float barHeight);
        Chrome.GroupScope shield = this.DrawShield(Chrome.ColumnX(origin.X, width, 1), y, column, out float shieldHeight);
        y += FrameRow(bar, barHeight, shield, shieldHeight);

        Chrome.BeginGroupRow();
        Chrome.GroupScope figure = this.DrawHealthText(Chrome.ColumnX(origin.X, width, 0), y, column, out float figureHeight);
        Chrome.GroupScope mana = this.DrawMana(Chrome.ColumnX(origin.X, width, 1), y, column, out float manaHeight);
        y += FrameRow(figure, figureHeight, mana, manaHeight);

        Chrome.BeginGroupRow();
        Chrome.GroupScope name = this.DrawNameText(Chrome.ColumnX(origin.X, width, 0), y, column, out float nameHeight);
        Chrome.GroupScope lettering = this.DrawTextStyle(Chrome.ColumnX(origin.X, width, 1), y, column, out float letteringHeight);
        y += FrameRow(name, nameHeight, lettering, letteringHeight);

        // In its own column rather than stretched across both, like the lone groups on Icons
        // and Layout: a group twice as wide reads as a different kind of thing.
        Chrome.BeginGroupRow();
        Chrome.GroupScope mouse = this.DrawMouse(Chrome.ColumnX(origin.X, width, 0), y, column, out float mouseHeight);
        y += Chrome.GroupFrame(mouse, mouseHeight) + Tokens.Metric.ColumnGutter;

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, y - origin.Y + Tokens.Metric.ContentPaddingBottom));
    }

    /// <summary>
    /// The Icons tab: the badges a frame carries. They are together because they are the same
    /// kind of thing and are set the same way — a size, one of the nine points, and the two
    /// nudges off it — not because they happen to be pictures.
    /// </summary>
    public void DrawIcons(float width)
    {
        Vector2 origin = ImGui.GetCursorScreenPos();
        float column = Chrome.ColumnWidth(width);

        Chrome.BeginGroupRow();
        Chrome.GroupScope icon = this.DrawJobIcon(Chrome.ColumnX(origin.X, width, 0), origin.Y, column, out float iconHeight);
        Chrome.GroupScope leader = this.DrawLeaderIcon(Chrome.ColumnX(origin.X, width, 1), origin.Y, column, out float leaderHeight);
        float y = origin.Y + FrameRow(icon, iconHeight, leader, leaderHeight);

        // One group in the second row. It keeps its column rather than stretching across both:
        // a group twice as wide as the one above it reads as a different kind of thing, and
        // this is the same kind of thing with fewer rows.
        Chrome.BeginGroupRow();
        Chrome.GroupScope number = this.DrawPartyNumber(Chrome.ColumnX(origin.X, width, 0), y, column, out float numberHeight);
        y += Chrome.GroupFrame(number, numberHeight) + Tokens.Metric.ColumnGutter;

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

        // One group in the second row, in its own column like the party number on Icons.
        Chrome.BeginGroupRow();
        Chrome.GroupScope list = this.DrawGameList(Chrome.ColumnX(origin.X, width, 0), y, column, out float listHeight);
        y += Chrome.GroupFrame(list, listHeight) + Tokens.Metric.ColumnGutter;

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

        int style = BarStyles.IndexOf(BarStyles.ForBar, m_config.PartyFrames.BarStyleName);
        if (m_style.Draw(
                ref style,
                Chrome.Row(Strings.BarStyle, group.ContentX, rowY, group.ContentWidth, false),
                rowY,
                Chrome.ControlWidth()))
        {
            m_config.PartyFrames.BarStyleName = BarStyles.NameAt(BarStyles.ForBar, style);
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
            OpacityStep,
            OpacityEditScale);

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
    /// Shields: damage that will not land, drawn on the health bar.
    /// <para>
    /// Its own group rather than four more rows under the bar. It started inside that group on
    /// the argument that a shield is part of what the bar says — which is true, and is why it
    /// sits beside the bar rather than on the Icons tab — but eight rows in one group is not a
    /// group any more, it is a list (Florian, 2026-09-18).
    /// </para>
    /// <para>
    /// There is no placement setting. The shield fills the missing health and turns back over
    /// the health when it no longer fits; the alternative was built, found to leave the gap
    /// empty on a wounded person, and removed — see <see cref="Hud.Shield"/>.
    /// </para>
    /// </summary>
    private Chrome.GroupScope DrawShield(float x, float y, float width, out float contentHeight)
    {
        Chrome.GroupScope group = Chrome.BeginGroup(
            IdShieldGroup,
            new Chrome.GroupHead
            {
                Title = Strings.GroupShield,
                Description = Strings.GroupShieldHint,
                Toggle = m_config.PartyFrames.ShowShield,
            },
            x,
            y,
            width);

        if (group.ToggleClicked)
        {
            m_config.PartyFrames.ShowShield = !m_config.PartyFrames.ShowShield;
            m_config.MarkDirty();
        }

        float pitch = Chrome.RowPitch();
        float rowY = group.ContentY;

        int shieldStyle = BarStyles.IndexOf(BarStyles.ForShield, m_config.PartyFrames.ShieldStyleName);
        if (m_shieldStyle.Draw(
                ref shieldStyle,
                Chrome.Row(Strings.ShieldStyle, group.ContentX, rowY, group.ContentWidth, false),
                rowY,
                Chrome.ControlWidth()))
        {
            m_config.PartyFrames.ShieldStyleName = BarStyles.NameAt(BarStyles.ForShield, shieldStyle);
            m_config.MarkDirty();
        }

        rowY += pitch;

        uint shieldColour = m_config.PartyFrames.ShieldColour;
        if (Chrome.ColourRow(
                IdShieldColour,
                Strings.ShieldColour,
                group.ContentX,
                rowY,
                group.ContentWidth,
                ref shieldColour,
                false))
        {
            m_config.PartyFrames.ShieldColour = shieldColour;
            m_config.MarkDirty();
        }

        rowY += pitch;

        float shieldOpacity = m_config.PartyFrames.ShieldOpacity;
        Chrome.SliderResult shieldResult = Chrome.Slider(
            IdShieldOpacity,
            Strings.ShieldOpacity,
            this.ShieldOpacityCaption(shieldOpacity),
            group.ContentX,
            rowY,
            group.ContentWidth,
            shieldOpacity,
            Configuration.MinBarOpacity,
            1f,
            null,
            Strings.ShieldOpacityTooltip,
            true,
            OpacityStep,
            OpacityEditScale);

        if (shieldResult.Changed)
        {
            m_config.PartyFrames.ShieldOpacity = shieldResult.Value;
            m_config.MarkDirty();
        }

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

        int shortening = m_config.PartyFrames.NameShortening;
        if (m_shortening.Draw(
                ref shortening,
                Chrome.Row(Strings.ShortenNames, group.ContentX, rowY, group.ContentWidth, true),
                rowY,
                Chrome.ControlWidth()))
        {
            m_config.PartyFrames.NameShortening = shortening;
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

    /// <summary>
    /// The job icon: the same anatomy every text on a frame has (spec §11.2) — a size, one of
    /// the nine points, and the two nudges off it — plus the one option that only an icon
    /// wants, which is to leave the damage dealers out.
    /// </summary>
    private Chrome.GroupScope DrawJobIcon(float x, float y, float width, out float contentHeight)
    {
        Chrome.GroupScope group = Chrome.BeginGroup(
            IdIconGroup,
            new Chrome.GroupHead
            {
                Title = Strings.GroupJobIcon,
                Description = Strings.GroupJobIconHint,
                Toggle = m_config.PartyFrames.ShowJobIcon,
            },
            x,
            y,
            width);

        if (group.ToggleClicked)
        {
            m_config.PartyFrames.ShowJobIcon = !m_config.PartyFrames.ShowJobIcon;
            m_config.MarkDirty();
        }

        float pitch = Chrome.RowPitch();
        float rowY = group.ContentY;

        int style = m_config.PartyFrames.JobIconStyle;
        if (Chrome.SegmentRow(IdIconStyle, Strings.IconStyle, group.ContentX, rowY, group.ContentWidth, IconStyleNames, ref style))
        {
            m_config.PartyFrames.JobIconStyle = style;
            m_config.MarkDirty();
        }

        rowY += pitch;
        this.PixelSlider(IdIconSize, Strings.IconSize, SlotIconSize, group, rowY, MinIconSize, MaxIconSize, true, null);
        rowY += pitch;

        int position = m_config.PartyFrames.JobIconPosition;
        if (m_iconPosition.Draw(
                ref position,
                Chrome.Row(Strings.TextPosition, group.ContentX, rowY, group.ContentWidth, true),
                rowY,
                Chrome.ControlWidth()))
        {
            m_config.PartyFrames.JobIconPosition = position;
            m_config.MarkDirty();
        }

        rowY += pitch;
        this.PixelSlider(IdIconX, Strings.OffsetX, SlotIconX, group, rowY, -MaxOffset, MaxOffset, true, null);
        rowY += pitch;
        this.PixelSlider(IdIconY, Strings.OffsetY, SlotIconY, group, rowY, -MaxOffset, MaxOffset, true, null);
        rowY += pitch;

        if (Chrome.OptionRow(
                IdIconHideDps,
                Strings.IconHideDps,
                group.ContentX,
                rowY,
                group.ContentWidth,
                m_config.PartyFrames.JobIconHideDps,
                Chrome.OptionControl.Tick,
                Strings.IconHideDpsTooltip,
                true,
                true))
        {
            m_config.PartyFrames.JobIconHideDps = !m_config.PartyFrames.JobIconHideDps;
            m_config.MarkDirty();
        }

        float used = rowY - group.ContentY + Chrome.RowHeight();
        Chrome.EndGroupContent(group, used);
        contentHeight = used;
        return group;
    }

    /// <summary>
    /// What the mouse does over a frame. Two switches and, when it matters, one line saying
    /// that the game has its own setting for the second one.
    /// </summary>
    private Chrome.GroupScope DrawMouse(float x, float y, float width, out float contentHeight)
    {
        Chrome.GroupScope group = Chrome.BeginGroup(
            IdMouseGroup,
            new Chrome.GroupHead
            {
                Title = Strings.GroupMouse,
                Description = Strings.GroupMouseHint,
            },
            x,
            y,
            width);

        float pitch = Chrome.RowPitch();
        float rowY = group.ContentY;

        if (Chrome.OptionRow(
                IdHighlight,
                Strings.HighlightHovered,
                group.ContentX,
                rowY,
                group.ContentWidth,
                m_config.PartyFrames.HighlightHovered,
                Chrome.OptionControl.Tick,
                Strings.HighlightHoveredTooltip,
                true,
                false))
        {
            m_config.PartyFrames.HighlightHovered = !m_config.PartyFrames.HighlightHovered;
            m_config.MarkDirty();
        }

        rowY += pitch;
        rowY += pitch;

        if (Chrome.OptionRow(
                IdMouseover,
                Strings.MouseoverTarget,
                group.ContentX,
                rowY,
                group.ContentWidth,
                m_config.PartyFrames.MouseoverTarget,
                Chrome.OptionControl.Switch,
                Strings.MouseoverTargetTooltip,
                true,
                true))
        {
            m_config.PartyFrames.MouseoverTarget = !m_config.PartyFrames.MouseoverTarget;
            m_config.MarkDirty();
        }

        rowY += pitch;

        if (Chrome.OptionRow(
                IdMouseoverCasting,
                Strings.MouseoverCasting,
                group.ContentX,
                rowY,
                group.ContentWidth,
                m_config.PartyFrames.MouseoverCasting,
                Chrome.OptionControl.Switch,
                Strings.MouseoverCastingTooltip,
                true,
                true))
        {
            m_config.PartyFrames.MouseoverCasting = !m_config.PartyFrames.MouseoverCasting;
            m_config.MarkDirty();
        }

        float used = rowY - group.ContentY + Chrome.RowHeight();
        Chrome.EndGroupContent(group, used);
        contentHeight = used;
        return group;
    }

    /// <summary>
    /// How every text on a frame is lettered: which face, and what carries it over the world.
    /// <para>
    /// Two rows for all of them rather than two rows each. Both questions are about reading a
    /// frame at a glance, not about the name and the figure separately — and a face is one
    /// font atlas entry for the whole HUD, so it could not honestly be offered per text.
    /// </para>
    /// </summary>
    private Chrome.GroupScope DrawTextStyle(float x, float y, float width, out float contentHeight)
    {
        Chrome.GroupScope group = Chrome.BeginGroup(
            IdTextStyleGroup,
            new Chrome.GroupHead
            {
                Title = Strings.GroupLettering,
                Description = Strings.GroupLetteringHint,
            },
            x,
            y,
            width);

        float pitch = Chrome.RowPitch();
        float rowY = group.ContentY;

        // The list is addressed by name, not by position: it grows and shrinks with the
        // player's font folder, and a stored position would mean a different face the moment
        // they added a file.
        int face = FontLibrary.IndexOf(m_config.PartyFrames.FontName);
        if (m_font.Draw(
                ref face,
                Chrome.Row(Strings.TextFont, group.ContentX, rowY, group.ContentWidth, true, Strings.TextFontTooltip),
                rowY,
                Chrome.ControlWidth()))
        {
            m_config.PartyFrames.FontName = FontLibrary.NameAt(face);
            m_config.MarkDirty();
        }

        rowY += pitch;

        int weight = m_config.PartyFrames.TextWeight;
        if (Chrome.SegmentRow(
                IdTextWeight,
                Strings.TextWeight,
                group.ContentX,
                rowY,
                group.ContentWidth,
                WeightNames,
                ref weight,
                true,
                Strings.TextWeightTooltip))
        {
            m_config.PartyFrames.TextWeight = weight;
            m_config.MarkDirty();
        }

        rowY += pitch;

        int edge = m_config.PartyFrames.TextEdge;
        if (Chrome.SegmentRow(
                IdTextEdge,
                Strings.TextEdge,
                group.ContentX,
                rowY,
                group.ContentWidth,
                EdgeNames,
                ref edge,
                false,
                Strings.TextEdgeTooltip))
        {
            m_config.PartyFrames.TextEdge = edge;
            m_config.MarkDirty();
        }

        float used = rowY - group.ContentY + Chrome.RowHeight();

        // Only when the face that is set is not the face being drawn. A shipped font that
        // fails to load falls back to the interface face, and without this line that is
        // indistinguishable from a font that loaded and simply looks thin — which is exactly
        // how a whole test round was spent (Florian, 2026-09-12).
        string? problem = Fonts.FaceProblem;
        if (problem is not null)
        {
            float noteY = rowY + Chrome.RowHeight() + Tokens.Space.Sm;
            Ink.Draw(
                ImGui.GetWindowDrawList(),
                Ink.Role.Small,
                new Vector2(group.ContentX, noteY),
                Tokens.Col.Gold,
                problem);

            used += Tokens.Space.Sm + Ink.LineHeight(Ink.Role.Small);
        }

        Chrome.EndGroupContent(group, used);
        contentHeight = used;
        return group;
    }

    /// <summary>
    /// The Bindings tab: what each mouse button does on a frame, for one job at a time.
    /// <para>
    /// A job at the top and a list under it, because the bindings are per job and there is no
    /// reading of the list that makes sense without knowing which job it belongs to.
    /// </para>
    /// </summary>
    public void DrawBindings(float width)
    {
        Vector2 origin = ImGui.GetCursorScreenPos();
        float y = origin.Y;

        // 🔴 The one group in the suite that takes the full width, and the one row that
        // carries two controls. The grammar everywhere else — one setting, one control, half
        // the width — is what keeps a settings screen readable, and a binding is not a
        // setting: it is a pair, and the pair is the thing. Splitting "this action" from
        // "this button" across two rows would be two halves of one sentence (Florian,
        // 2026-09-12, pointing at LumenUI's own bindings screen).
        Chrome.BeginGroupRow();
        Chrome.GroupScope group = this.DrawBindingList(origin.X, y, width, out float height);
        y += Chrome.GroupFrame(group, height) + Tokens.Metric.ColumnGutter;

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, y - origin.Y + Tokens.Metric.ContentPaddingBottom));
    }

    private Chrome.GroupScope DrawBindingList(float x, float y, float width, out float contentHeight)
    {
        Chrome.GroupScope group = Chrome.BeginGroup(
            IdBindingsGroup,
            new Chrome.GroupHead
            {
                Title = Strings.GroupBindings,
                Description = Strings.GroupBindingsHint,
            },
            x,
            y,
            width);

        float pitch = Chrome.RowPitch();
        float rowY = group.ContentY;

        // Which job is being set up. Defaults to the one being played, so opening the tab
        // mid-session lands on the list that is actually in force.
        if (m_bindingJob < 0)
        {
            m_bindingJob = Math.Max(0, JobList.IndexOf(Services.Objects.LocalPlayer?.ClassJob.RowId ?? 0u));
        }

        int job = m_bindingJob;
        if (m_jobSelector.Draw(
                ref job,
                Chrome.Row(Strings.BindingJob, group.ContentX, rowY, group.ContentWidth, true, Strings.BindingJobTooltip),
                rowY,
                Chrome.ControlWidth()))
        {
            m_bindingJob = job;
            m_listening = -1;
        }

        rowY += pitch;

        JobEntry entry = JobList.At(m_bindingJob);
        this.SyncActionChoices(entry.Id);

        System.Collections.Generic.List<MouseBinding> bindings = m_config.PartyFrames.Bindings.Edit(entry.Id);

        rowY = this.DrawBindingRows(group, bindings, entry, rowY, pitch);

        float used = rowY - group.ContentY + Chrome.RowHeight();
        Chrome.EndGroupContent(group, used);
        contentHeight = used;
        return group;
    }

    /// <summary>
    /// One row per binding, plus the button that adds another.
    /// <para>
    /// A row reads as one thing with a key beside it: what it does on the left — an icon and a
    /// name, clickable when there is a list behind it — then the button that triggers it, a
    /// switch, and, for the ones that were added, a way to take them away.
    /// </para>
    /// <para>
    /// 🔴 The one group in the suite that takes the full width, and the one row that carries
    /// more than one control. Everywhere else that grammar is what keeps a settings screen
    /// readable; a binding is not a setting but a pair, and half of a pair says nothing.
    /// </para>
    /// </summary>
    private float DrawBindingRows(
        Chrome.GroupScope group,
        System.Collections.Generic.List<MouseBinding> bindings,
        JobEntry job,
        float rowY,
        float pitch)
    {
        ActionEntry[] actions = ActionList.For(job.Id);

        float toggleWidth = Tokens.Px(30f);
        float trash = Tokens.Metric.TitleButton;
        float gap = Tokens.Space.Md;

        float trashX = group.ContentX + group.ContentWidth - trash;
        float toggleX = trashX - gap - toggleWidth;
        float keyX = toggleX - gap - Chrome.KeybindWidth();
        float nameWidth = keyX - gap - group.ContentX;

        int remove = -1;

        for (int i = 0; i < bindings.Count; i++)
        {
            MouseBinding binding = bindings[i];
            bool listening = m_listening == i;
            int button = binding.Button;
            int mods = (int)binding.Modifiers;

            ImGui.PushID(i);

            if (i > 0)
            {
                Chrome.RowDivider(group.ContentX, group.ContentX + group.ContentWidth, rowY);
            }

            if (binding.Kind == BindingKind.Action)
            {
                int pick = this.ActionIndex(binding.ActionId);

                if (m_actionPicker.Draw(ref pick, group.ContentX, rowY, nameWidth)
                    && pick >= 0 && pick < m_actionChoices.Count)
                {
                    binding.ActionId = m_actionChoices[pick].Id;
                    m_config.MarkDirty();
                }
            }
            else
            {
                // Nothing to choose: this row does one built-in thing and says so. Drawn the
                // same shape as the picker beside it so the column still lines up.
                Chrome.BindingName(
                    IdBindingName,
                    group.ContentX,
                    rowY,
                    nameWidth,
                    default,
                    binding.Kind == BindingKind.Target ? Strings.BindingTarget : Strings.BindingContextMenu,
                    false,
                    true);
            }

            if (Chrome.KeybindField(IdBindingKey, keyX, rowY, ref listening, ref button, ref mods))
            {
                binding.Button = button;
                binding.Modifiers = (BindingModifiers)mods;
                m_config.MarkDirty();
            }

            if (Chrome.BindingToggle(IdBindingOn, toggleX, rowY, binding.Enabled))
            {
                binding.Enabled = !binding.Enabled;
                m_config.MarkDirty();
            }

            // 🔴 Only a row that was added can be taken away. Selecting and the game's menu
            // stay: a frame with no way to select anybody is not a state somebody arrives at
            // on purpose, and the switch beside it already covers turning one off (Florian,
            // 2026-09-12).
            if (binding.Removable)
            {
                float trashY = MathF.Round(rowY + ((Chrome.RowHeight() - trash) * 0.5f));

                if (Chrome.CloseButton(IdBindingRemoveRow, trashX, trashY))
                {
                    remove = i;
                }
            }

            ImGui.PopID();

            m_listening = listening ? i : (m_listening == i ? -1 : m_listening);
            rowY += pitch;
        }

        // After the loop, never inside it: taking a row out while walking the list is how a
        // row gets skipped and an index ends up pointing at the wrong binding.
        if (remove >= 0)
        {
            bindings.RemoveAt(remove);
            m_listening = -1;
            m_config.MarkDirty();
        }

        return this.DrawAddBinding(group, bindings, actions, rowY);
    }

    /// <summary>
    /// The button that adds a binding. A pill under the list rather than a bar across it: it
    /// is one more thing you can do with the list, not a row of the list.
    /// </summary>
    private float DrawAddBinding(
        Chrome.GroupScope group,
        System.Collections.Generic.List<MouseBinding> bindings,
        ActionEntry[] actions,
        float rowY)
    {
        if (actions.Length == 0)
        {
            // A tank has nothing to aim at a party member. Saying so is better than a button
            // that adds a row with an empty list in it.
            Ink.Draw(
                ImGui.GetWindowDrawList(),
                Ink.Role.Small,
                new Vector2(group.ContentX, rowY + Tokens.Space.Sm),
                Tokens.Col.InkFaint,
                Strings.BindingNoActions);

            return rowY + Chrome.RowPitch();
        }

        Chrome.RowDivider(group.ContentX, group.ContentX + group.ContentWidth, rowY);

        if (Chrome.PillButton(IdBindingAdd, Strings.BindingAdd, group.ContentX, rowY))
        {
            bindings.Add(new MouseBinding
            {
                Kind = BindingKind.Action,
                ActionId = actions[0].Id,

                // Middle by default, because left and right are already spoken for and a new
                // row that silently shadowed one of them would be the worst first impression
                // this tab could make. It is meant to be changed straight away.
                Button = 2,
            });

            m_config.MarkDirty();
        }

        return rowY + Chrome.RowPitch();
    }

    /// <summary>Where an action sits in the picker's list, or the first entry when it is gone.</summary>
    private int ActionIndex(uint actionId)
    {
        for (int i = 0; i < m_actionChoices.Count; i++)
        {
            if (m_actionChoices[i].Id == actionId)
            {
                return i;
            }
        }

        return 0;
    }

    /// <summary>
    /// Refills the action list the picker holds, when the job it is showing has changed.
    /// <para>
    /// The same list object is kept and its contents replaced, because a selector holds the
    /// reference it was built with. Refilled only on a change, so this costs a comparison on
    /// every frame but the one where the job was switched.
    /// </para>
    /// </summary>
    private void SyncActionChoices(uint jobId)
    {
        if (m_choicesFor == jobId)
        {
            return;
        }

        m_choicesFor = jobId;
        m_actionChoices.Clear();
        m_actionChoices.AddRange(ActionList.For(jobId));
    }

    /// <summary>
    /// What a binding is called in its row: the action's own name, or what the two built-in
    /// kinds do. An action the job no longer has falls back to its number rather than to an
    /// empty row, so it can still be seen and removed.
    /// </summary>
    private string BindingLabel(MouseBinding binding, ActionEntry[] actions)
    {
        switch (binding.Kind)
        {
            case BindingKind.Target:
                return Strings.BindingTarget;

            case BindingKind.ContextMenu:
                return Strings.BindingContextMenu;

            default:
                for (int i = 0; i < actions.Length; i++)
                {
                    if (actions[i].Id == binding.ActionId)
                    {
                        return actions[i].Name;
                    }
                }

                return Strings.BindingAction;
        }
    }

    /// <summary>The leader's mark. The same four rows every badge on a frame gets.</summary>
    private Chrome.GroupScope DrawLeaderIcon(float x, float y, float width, out float contentHeight)
    {
        Chrome.GroupScope group = Chrome.BeginGroup(
            IdLeaderGroup,
            new Chrome.GroupHead
            {
                Title = Strings.GroupLeader,
                Description = Strings.GroupLeaderHint,
                Toggle = m_config.PartyFrames.ShowLeaderIcon,
            },
            x,
            y,
            width);

        if (group.ToggleClicked)
        {
            m_config.PartyFrames.ShowLeaderIcon = !m_config.PartyFrames.ShowLeaderIcon;
            m_config.MarkDirty();
        }

        float pitch = Chrome.RowPitch();
        float rowY = group.ContentY;

        this.PixelSlider(IdLeaderSize, Strings.IconSize, SlotLeaderSize, group, rowY, MinIconSize, MaxIconSize, false, null);
        rowY += pitch;

        int position = m_config.PartyFrames.LeaderIconPosition;
        if (m_leaderPosition.Draw(
                ref position,
                Chrome.Row(Strings.TextPosition, group.ContentX, rowY, group.ContentWidth, true),
                rowY,
                Chrome.ControlWidth()))
        {
            m_config.PartyFrames.LeaderIconPosition = position;
            m_config.MarkDirty();
        }

        rowY += pitch;
        this.PixelSlider(IdLeaderX, Strings.OffsetX, SlotLeaderX, group, rowY, -MaxOffset, MaxOffset, true, null);
        rowY += pitch;
        this.PixelSlider(IdLeaderY, Strings.OffsetY, SlotLeaderY, group, rowY, -MaxOffset, MaxOffset, true, null);

        float used = rowY - group.ContentY + Chrome.RowHeight();
        Chrome.EndGroupContent(group, used);
        contentHeight = used;
        return group;
    }

    /// <summary>
    /// The party number — the 1 to 8 the game's own list puts in front of every member. Same
    /// anatomy as every other thing on a frame.
    /// </summary>
    private Chrome.GroupScope DrawPartyNumber(float x, float y, float width, out float contentHeight)
    {
        Chrome.GroupScope group = Chrome.BeginGroup(
            IdNumberGroup,
            new Chrome.GroupHead
            {
                Title = Strings.GroupPartyNumber,
                Description = Strings.GroupPartyNumberHint,
                Toggle = m_config.PartyFrames.ShowPartyNumber,
            },
            x,
            y,
            width);

        if (group.ToggleClicked)
        {
            m_config.PartyFrames.ShowPartyNumber = !m_config.PartyFrames.ShowPartyNumber;
            m_config.MarkDirty();
        }

        float pitch = Chrome.RowPitch();
        float rowY = group.ContentY;

        this.PixelSlider(IdNumberSize, Strings.TextSize, SlotNumberSize, group, rowY, Configuration.MinTextSize, Configuration.MaxTextSize, false, null);
        rowY += pitch;

        int position = m_config.PartyFrames.PartyNumberPosition;
        if (m_numberPosition.Draw(
                ref position,
                Chrome.Row(Strings.TextPosition, group.ContentX, rowY, group.ContentWidth, true),
                rowY,
                Chrome.ControlWidth()))
        {
            m_config.PartyFrames.PartyNumberPosition = position;
            m_config.MarkDirty();
        }

        rowY += pitch;
        this.PixelSlider(IdNumberX, Strings.OffsetX, SlotNumberX, group, rowY, -MaxOffset, MaxOffset, true, null);
        rowY += pitch;
        this.PixelSlider(IdNumberY, Strings.OffsetY, SlotNumberY, group, rowY, -MaxOffset, MaxOffset, true, null);

        float used = rowY - group.ContentY + Chrome.RowHeight();
        Chrome.EndGroupContent(group, used);
        contentHeight = used;
        return group;
    }


    /// <summary>
    /// The Auras tab: everything lying on a person. Not "debuff icons" — the icons are one of
    /// three displays that come out of the same status pass, and the other two answer
    /// questions the icons cannot (spec §13.2).
    /// </summary>
    public void DrawAuras(float width)
    {
        Vector2 origin = ImGui.GetCursorScreenPos();
        float column = Chrome.ColumnWidth(width);

        Chrome.BeginGroupRow();
        Chrome.GroupScope auras = this.DrawAuraIcons(Chrome.ColumnX(origin.X, width, 0), origin.Y, column, out float auraHeight);
        Chrome.GroupScope rescue = this.DrawRescue(Chrome.ColumnX(origin.X, width, 1), origin.Y, column, out float rescueHeight);
        float y = origin.Y + FrameRow(auras, auraHeight, rescue, rescueHeight);

        Chrome.BeginGroupRow();
        Chrome.GroupScope buffs = this.DrawBuffIcons(Chrome.ColumnX(origin.X, width, 0), y, column, out float buffHeight);
        Chrome.GroupScope others = this.DrawOtherIcons(Chrome.ColumnX(origin.X, width, 1), y, column, out float otherHeight);
        y += FrameRow(buffs, buffHeight, others, otherHeight);

        Chrome.BeginGroupRow();
        // 🔴 Six groups on this tab now, and §3.1 asks for three or four. The raise mark landed
        // here because it belongs beside the cleanse mark — they are the same drawing saying
        // opposite things, and putting them on different tabs would hide that. The tab needs
        // splitting; which way is a layout decision, not a code one (Florian, 2026-09-18).
        Chrome.GroupScope cleanse = this.DrawCleanse(Chrome.ColumnX(origin.X, width, 0), y, column, out float cleanseHeight);
        Chrome.GroupScope raiseMark = this.DrawRaiseMark(Chrome.ColumnX(origin.X, width, 1), y, column, out float raiseMarkHeight);
        y += FrameRow(cleanse, cleanseHeight, raiseMark, raiseMarkHeight);

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, y - origin.Y + Tokens.Metric.ContentPaddingBottom));
    }

    private Chrome.GroupScope DrawAuraIcons(float x, float y, float width, out float contentHeight)
    {
        Chrome.GroupScope group = Chrome.BeginGroup(
            IdAuraGroup,
            new Chrome.GroupHead
            {
                Title = Strings.GroupAuras,
                Description = Strings.GroupAurasHint,
            },
            x,
            y,
            width);

        float pitch = Chrome.RowPitch();
        float rowY = group.ContentY;

        if (Chrome.OptionRow(
                IdShowAuras,
                Strings.ShowAuras,
                group.ContentX,
                rowY,
                group.ContentWidth,
                m_config.PartyFrames.ShowAuras,
                Chrome.OptionControl.Tick,
                Strings.ShowAurasTooltip))
        {
            m_config.PartyFrames.ShowAuras = !m_config.PartyFrames.ShowAuras;
            m_config.MarkDirty();
        }

        // Right under the switch it belongs to, and not saved: everything below this row
        // places something that is only on a frame some of the time, and placing it blind is
        // placing it twice (Florian, 2026-09-13).
        rowY += pitch;
        if (Chrome.OptionRow(
                IdPreviewAuras,
                Strings.PreviewAuras,
                group.ContentX,
                rowY,
                group.ContentWidth,
                AuraPreview.Active,
                Chrome.OptionControl.Tick,
                Strings.PreviewAurasTooltip,
                true,
                true))
        {
            AuraPreview.Toggle();
        }

        rowY += pitch;
        this.PixelSlider(IdAuraMax, Strings.AuraCount, SlotAuraMax, group, rowY, 1f, PartySnapshot.MaxAuras, true, Strings.AuraCountTooltip);

        rowY += pitch;
        this.PixelSlider(IdAuraSize, Strings.IconSize, SlotAuraSize, group, rowY, MinAuraSize, MaxAuraSize, true, null);

        rowY += pitch;
        int position = m_config.PartyFrames.AuraPosition;
        if (m_auraPosition.Draw(
                ref position,
                Chrome.Row(Strings.TextPosition, group.ContentX, rowY, group.ContentWidth, true),
                rowY,
                Chrome.ControlWidth()))
        {
            m_config.PartyFrames.AuraPosition = position;
            m_config.MarkDirty();
        }

        rowY += pitch;
        this.PixelSlider(IdAuraX, Strings.OffsetX, SlotAuraX, group, rowY, -MaxOffset, MaxOffset, true, null);

        rowY += pitch;
        this.PixelSlider(IdAuraY, Strings.OffsetY, SlotAuraY, group, rowY, -MaxOffset, MaxOffset, true, null);

        rowY += pitch;
        if (Chrome.OptionRow(
                IdAuraStacks,
                Strings.AuraStacks,
                group.ContentX,
                rowY,
                group.ContentWidth,
                m_config.PartyFrames.AuraShowStacks,
                Chrome.OptionControl.Tick,
                Strings.AuraStacksTooltip,
                true,
                true))
        {
            m_config.PartyFrames.AuraShowStacks = !m_config.PartyFrames.AuraShowStacks;
            m_config.MarkDirty();
        }

        rowY += pitch;
        if (Chrome.OptionRow(
                IdAuraSwipe,
                Strings.AuraSwipe,
                group.ContentX,
                rowY,
                group.ContentWidth,
                m_config.PartyFrames.AuraSwipe,
                Chrome.OptionControl.Tick,
                Strings.AuraSwipeTooltip,
                true,
                true))
        {
            m_config.PartyFrames.AuraSwipe = !m_config.PartyFrames.AuraSwipe;
            m_config.MarkDirty();
        }

        // One switch for all three icon rows. "What is this picture" is the same question
        // whether the picture is a debuff, your own regen or somebody else's work.
        rowY += pitch;
        if (Chrome.OptionRow(
                IdAuraTooltips,
                Strings.AuraTooltips,
                group.ContentX,
                rowY,
                group.ContentWidth,
                m_config.PartyFrames.ShowAuraTooltips,
                Chrome.OptionControl.Tick,
                Strings.AuraTooltipsTooltip,
                true,
                true))
        {
            m_config.PartyFrames.ShowAuraTooltips = !m_config.PartyFrames.ShowAuraTooltips;
            m_config.MarkDirty();
        }

        float used = rowY - group.ContentY + Chrome.RowHeight();
        Chrome.EndGroupContent(group, used);
        contentHeight = used;
        return group;
    }

    /// <summary>
    /// The second row: what is already on this person from the player's own hands. Same
    /// anatomy as the afflictions, which is the point — one kind of thing, set up one way.
    /// </summary>
    private Chrome.GroupScope DrawBuffIcons(float x, float y, float width, out float contentHeight)
    {
        Chrome.GroupScope group = Chrome.BeginGroup(
            IdBuffGroup,
            new Chrome.GroupHead
            {
                Title = Strings.GroupBuffs,
                Description = Strings.GroupBuffsHint,
            },
            x,
            y,
            width);

        float pitch = Chrome.RowPitch();
        float rowY = group.ContentY;

        if (Chrome.OptionRow(
                IdShowBuffs,
                Strings.ShowBuffs,
                group.ContentX,
                rowY,
                group.ContentWidth,
                m_config.PartyFrames.ShowBuffs,
                Chrome.OptionControl.Tick,
                Strings.ShowBuffsTooltip))
        {
            m_config.PartyFrames.ShowBuffs = !m_config.PartyFrames.ShowBuffs;
            m_config.MarkDirty();
        }

        rowY += pitch;
        if (Chrome.OptionRow(
                IdOwnBuffs,
                Strings.OwnBuffsOnly,
                group.ContentX,
                rowY,
                group.ContentWidth,
                m_config.PartyFrames.OwnBuffsOnly,
                Chrome.OptionControl.Tick,
                Strings.OwnBuffsOnlyTooltip,
                true,
                true))
        {
            m_config.PartyFrames.OwnBuffsOnly = !m_config.PartyFrames.OwnBuffsOnly;
            m_config.MarkDirty();
        }

        rowY += pitch;
        this.PixelSlider(IdBuffMax, Strings.AuraCount, SlotBuffMax, group, rowY, 1f, PartySnapshot.MaxAuras, true, Strings.AuraCountTooltip);

        rowY += pitch;
        this.PixelSlider(IdBuffSize, Strings.IconSize, SlotBuffSize, group, rowY, MinAuraSize, MaxAuraSize, true, null);

        rowY += pitch;
        int position = m_config.PartyFrames.BuffPosition;
        if (m_buffPosition.Draw(
                ref position,
                Chrome.Row(Strings.TextPosition, group.ContentX, rowY, group.ContentWidth, true),
                rowY,
                Chrome.ControlWidth()))
        {
            m_config.PartyFrames.BuffPosition = position;
            m_config.MarkDirty();
        }

        rowY += pitch;
        this.PixelSlider(IdBuffX, Strings.OffsetX, SlotBuffX, group, rowY, -MaxOffset, MaxOffset, true, null);

        rowY += pitch;
        this.PixelSlider(IdBuffY, Strings.OffsetY, SlotBuffY, group, rowY, -MaxOffset, MaxOffset, true, null);

        float used = rowY - group.ContentY + Chrome.RowHeight();
        Chrome.EndGroupContent(group, used);
        contentHeight = used;
        return group;
    }

    /// <summary>
    /// The third row: benefits somebody else put there. Same anatomy again — three rows, one
    /// way of setting a row up.
    /// </summary>
    private Chrome.GroupScope DrawOtherIcons(float x, float y, float width, out float contentHeight)
    {
        Chrome.GroupScope group = Chrome.BeginGroup(
            IdOtherGroup,
            new Chrome.GroupHead
            {
                Title = Strings.GroupOthers,
                Description = Strings.GroupOthersHint,
            },
            x,
            y,
            width);

        float pitch = Chrome.RowPitch();
        float rowY = group.ContentY;

        if (Chrome.OptionRow(
                IdShowOther,
                Strings.ShowOthers,
                group.ContentX,
                rowY,
                group.ContentWidth,
                m_config.PartyFrames.ShowOtherBuffs,
                Chrome.OptionControl.Tick,
                Strings.ShowOthersTooltip))
        {
            m_config.PartyFrames.ShowOtherBuffs = !m_config.PartyFrames.ShowOtherBuffs;
            m_config.MarkDirty();
        }

        rowY += pitch;
        this.PixelSlider(IdOtherMax, Strings.AuraCount, SlotOtherMax, group, rowY, 1f, PartySnapshot.MaxAuras, true, Strings.AuraCountTooltip);

        rowY += pitch;
        this.PixelSlider(IdOtherSize, Strings.IconSize, SlotOtherSize, group, rowY, MinAuraSize, MaxAuraSize, true, null);

        rowY += pitch;
        int position = m_config.PartyFrames.OtherPosition;
        if (m_otherPosition.Draw(
                ref position,
                Chrome.Row(Strings.TextPosition, group.ContentX, rowY, group.ContentWidth, true),
                rowY,
                Chrome.ControlWidth()))
        {
            m_config.PartyFrames.OtherPosition = position;
            m_config.MarkDirty();
        }

        rowY += pitch;
        this.PixelSlider(IdOtherX, Strings.OffsetX, SlotOtherX, group, rowY, -MaxOffset, MaxOffset, true, null);

        rowY += pitch;
        this.PixelSlider(IdOtherY, Strings.OffsetY, SlotOtherY, group, rowY, -MaxOffset, MaxOffset, true, null);

        float used = rowY - group.ContentY + Chrome.RowHeight();
        Chrome.EndGroupContent(group, used);
        contentHeight = used;
        return group;
    }

    private Chrome.GroupScope DrawCleanse(float x, float y, float width, out float contentHeight)
    {
        Chrome.GroupScope group = Chrome.BeginGroup(
            IdCleanseGroup,
            new Chrome.GroupHead
            {
                Title = Strings.GroupCleanse,
                Description = Strings.GroupCleanseHint,
            },
            x,
            y,
            width);

        float rowY = group.ContentY;

        int mark = Array.IndexOf(MarkStyles, FrameMark.At(m_config.PartyFrames.CleanseMark));
        mark = mark < 0 ? 0 : mark;

        if (m_cleanse.Draw(
                ref mark,
                Chrome.Row(Strings.CleanseHow, group.ContentX, rowY, group.ContentWidth, false, Strings.CleanseHowTooltip),
                rowY,
                Chrome.ControlWidth()))
        {
            m_config.PartyFrames.CleanseMark = (int)MarkStyles[mark];
            m_config.MarkDirty();
        }

        rowY += Chrome.RowPitch();
        uint colour = m_config.PartyFrames.CleanseColour;
        if (Chrome.ColourRow(
                IdCleanseColour,
                Strings.CleanseColour,
                group.ContentX,
                rowY,
                group.ContentWidth,
                ref colour,
                true,
                null))
        {
            m_config.PartyFrames.CleanseColour = colour;
            m_config.MarkDirty();
        }

        rowY += Chrome.RowPitch();
        this.PixelSlider(
            IdCleanseThickness,
            Strings.CleanseThickness,
            SlotCleanseThickness,
            group,
            rowY,
            MinCleanseThickness,
            MaxCleanseThickness,
            true,
            Strings.CleanseThicknessTooltip);

        rowY += Chrome.RowPitch();
        float cleanseOpacity = m_config.PartyFrames.CleanseOpacity;
        Chrome.SliderResult cleanseFill = Chrome.Slider(
            IdCleanseOpacity,
            Strings.MarkOpacity,
            this.CleanseOpacityCaption(cleanseOpacity),
            group.ContentX,
            rowY,
            group.ContentWidth,
            cleanseOpacity,
            0f,
            1f,
            null,
            Strings.MarkOpacityTooltip,
            true,
            OpacityStep,
            OpacityEditScale);

        if (cleanseFill.Changed)
        {
            m_config.PartyFrames.CleanseOpacity = cleanseFill.Value;
            m_config.MarkDirty();
        }

        rowY += Chrome.RowPitch();
        if (Chrome.OptionRow(
                IdCleanseWhenAble,
                Strings.CleanseWhenAble,
                group.ContentX,
                rowY,
                group.ContentWidth,
                m_config.PartyFrames.CleanseOnlyWhenAble,
                Chrome.OptionControl.Tick,
                Strings.CleanseWhenAbleTooltip,
                true,
                true))
        {
            m_config.PartyFrames.CleanseOnlyWhenAble = !m_config.PartyFrames.CleanseOnlyWhenAble;
            m_config.MarkDirty();
        }

        float used = rowY - group.ContentY + Chrome.RowHeight();
        Chrome.EndGroupContent(group, used);
        contentHeight = used;
        return group;
    }

    /// <summary>
    /// The mark that says somebody is already being picked up — the same four shapes the
    /// cleanse mark uses, saying the opposite thing.
    /// <para>
    /// Its own group rather than four more rows under the rescue icon: the icon answers "what
    /// is on them", the mark answers "can I stop looking at this frame", and a healer turns
    /// them on for different reasons (Florian, 2026-09-18).
    /// </para>
    /// </summary>
    private Chrome.GroupScope DrawRaiseMark(float x, float y, float width, out float contentHeight)
    {
        Chrome.GroupScope group = Chrome.BeginGroup(
            IdRaiseMarkGroup,
            new Chrome.GroupHead
            {
                Title = Strings.GroupRaiseMark,
                Description = Strings.GroupRaiseMarkHint,
            },
            x,
            y,
            width);

        float rowY = group.ContentY;

        int mark = Array.IndexOf(MarkStyles, FrameMark.At(m_config.PartyFrames.RaiseMark));
        mark = mark < 0 ? 0 : mark;

        if (m_raiseMark.Draw(
                ref mark,
                Chrome.Row(Strings.RaiseHow, group.ContentX, rowY, group.ContentWidth, false, Strings.RaiseHowTooltip),
                rowY,
                Chrome.ControlWidth()))
        {
            m_config.PartyFrames.RaiseMark = (int)MarkStyles[mark];
            m_config.MarkDirty();
        }

        rowY += Chrome.RowPitch();
        uint colour = m_config.PartyFrames.RaiseColour;
        if (Chrome.ColourRow(
                IdRaiseColour,
                Strings.RaiseColour,
                group.ContentX,
                rowY,
                group.ContentWidth,
                ref colour,
                true,
                null))
        {
            m_config.PartyFrames.RaiseColour = colour;
            m_config.MarkDirty();
        }

        rowY += Chrome.RowPitch();
        this.PixelSlider(
            IdRaiseThickness,
            Strings.RaiseThickness,
            SlotRaiseThickness,
            group,
            rowY,
            MinCleanseThickness,
            MaxCleanseThickness,
            true,
            Strings.CleanseThicknessTooltip);

        rowY += Chrome.RowPitch();
        float raiseOpacity = m_config.PartyFrames.RaiseOpacity;
        Chrome.SliderResult raiseFill = Chrome.Slider(
            IdRaiseOpacity,
            Strings.MarkOpacity,
            this.RaiseOpacityCaption(raiseOpacity),
            group.ContentX,
            rowY,
            group.ContentWidth,
            raiseOpacity,
            0f,
            1f,
            null,
            Strings.MarkOpacityTooltip,
            true,
            OpacityStep,
            OpacityEditScale);

        if (raiseFill.Changed)
        {
            m_config.PartyFrames.RaiseOpacity = raiseFill.Value;
            m_config.MarkDirty();
        }

        float used = rowY - group.ContentY + Chrome.RowHeight();
        Chrome.EndGroupContent(group, used);
        contentHeight = used;
        return group;
    }

    private Chrome.GroupScope DrawRescue(float x, float y, float width, out float contentHeight)
    {
        Chrome.GroupScope group = Chrome.BeginGroup(
            IdRescueGroup,
            new Chrome.GroupHead
            {
                Title = Strings.GroupRescue,
                Description = Strings.GroupRescueHint,
            },
            x,
            y,
            width);

        float pitch = Chrome.RowPitch();
        float rowY = group.ContentY;

        // Two switches for one place on the frame. They never show at once — invulnerability
        // wins where both apply — but they answer different questions, so one of them being
        // off is a real thing to want (Florian, 2026-09-18).
        if (Chrome.OptionRow(
                IdShowRaise,
                Strings.ShowRaise,
                group.ContentX,
                rowY,
                group.ContentWidth,
                m_config.PartyFrames.ShowRaiseIcon,
                Chrome.OptionControl.Tick,
                Strings.ShowRaiseTooltip))
        {
            m_config.PartyFrames.ShowRaiseIcon = !m_config.PartyFrames.ShowRaiseIcon;
            m_config.MarkDirty();
        }

        rowY += pitch;

        if (Chrome.OptionRow(
                IdShowInvuln,
                Strings.ShowInvuln,
                group.ContentX,
                rowY,
                group.ContentWidth,
                m_config.PartyFrames.ShowInvulnIcon,
                Chrome.OptionControl.Tick,
                Strings.ShowInvulnTooltip))
        {
            m_config.PartyFrames.ShowInvulnIcon = !m_config.PartyFrames.ShowInvulnIcon;
            m_config.MarkDirty();
        }

        rowY += pitch;
        this.PixelSlider(IdRescueSize, Strings.IconSize, SlotRescueSize, group, rowY, MinIconSize, MaxIconSize, true, null);

        rowY += pitch;
        int position = m_config.PartyFrames.RescueIconPosition;
        if (m_rescuePosition.Draw(
                ref position,
                Chrome.Row(Strings.TextPosition, group.ContentX, rowY, group.ContentWidth, true),
                rowY,
                Chrome.ControlWidth()))
        {
            m_config.PartyFrames.RescueIconPosition = position;
            m_config.MarkDirty();
        }

        rowY += pitch;
        this.PixelSlider(IdRescueX, Strings.OffsetX, SlotRescueX, group, rowY, -MaxOffset, MaxOffset, true, null);

        rowY += pitch;
        this.PixelSlider(IdRescueY, Strings.OffsetY, SlotRescueY, group, rowY, -MaxOffset, MaxOffset, true, null);

        float used = rowY - group.ContentY + Chrome.RowHeight();
        Chrome.EndGroupContent(group, used);
        contentHeight = used;
        return group;
    }

    /// <summary>
    /// What becomes of the list the frames replace.
    /// <para>
    /// One switch, and a line saying what is not a switch: WispUI has no sorting of its own.
    /// The frames read the order out of the game, so role sorting and the job order inside
    /// each role are set once, in the game, and hold for both (Florian, 2026-09-13).
    /// </para>
    /// </summary>
    private Chrome.GroupScope DrawGameList(float x, float y, float width, out float contentHeight)
    {
        Chrome.GroupScope group = Chrome.BeginGroup(
            IdGameListGroup,
            new Chrome.GroupHead
            {
                Title = Strings.GroupGameList,
                Description = Strings.GroupGameListHint,
            },
            x,
            y,
            width);

        float rowY = group.ContentY;

        if (Chrome.OptionRow(
                IdHideNativeList,
                Strings.HideNativeList,
                group.ContentX,
                rowY,
                group.ContentWidth,
                m_config.PartyFrames.HideNativePartyList,
                Chrome.OptionControl.Tick,
                Strings.HideNativeListTooltip))
        {
            m_config.PartyFrames.HideNativePartyList = !m_config.PartyFrames.HideNativePartyList;
            m_config.MarkDirty();
        }

        // Flush left rather than under the control: it is not that switch's answer, it is the
        // group's — it holds whether the list is hidden or not.
        Ink.Draw(
            ImGui.GetWindowDrawList(),
            Ink.Role.Small,
            new Vector2(group.ContentX, rowY + Chrome.RowHeight() + Tokens.Space.Sm),
            Tokens.Col.InkFaint,
            Strings.NativeListSorting);

        float used = rowY - group.ContentY + Chrome.RowHeight() + Tokens.Space.Sm + Ink.LineHeight(Ink.Role.Small);
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
            step,
            PixelEditScale);

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
        SlotIconSize => m_config.PartyFrames.JobIconSize,
        SlotIconX => m_config.PartyFrames.JobIconX,
        SlotIconY => m_config.PartyFrames.JobIconY,
        SlotNumberSize => m_config.PartyFrames.PartyNumberSize,
        SlotNumberX => m_config.PartyFrames.PartyNumberX,
        SlotNumberY => m_config.PartyFrames.PartyNumberY,
        SlotLeaderSize => m_config.PartyFrames.LeaderIconSize,
        SlotLeaderX => m_config.PartyFrames.LeaderIconX,
        SlotLeaderY => m_config.PartyFrames.LeaderIconY,
        SlotAuraSize => m_config.PartyFrames.AuraSize,
        SlotAuraX => m_config.PartyFrames.AuraX,
        SlotAuraY => m_config.PartyFrames.AuraY,
        SlotAuraMax => m_config.PartyFrames.AuraMaxCount,
        SlotRescueSize => m_config.PartyFrames.RescueIconSize,
        SlotRescueX => m_config.PartyFrames.RescueIconX,
        SlotRescueY => m_config.PartyFrames.RescueIconY,
        SlotBuffSize => m_config.PartyFrames.BuffSize,
        SlotBuffX => m_config.PartyFrames.BuffX,
        SlotBuffY => m_config.PartyFrames.BuffY,
        SlotBuffMax => m_config.PartyFrames.BuffMaxCount,
        SlotOtherSize => m_config.PartyFrames.OtherSize,
        SlotOtherX => m_config.PartyFrames.OtherX,
        SlotOtherY => m_config.PartyFrames.OtherY,
        SlotOtherMax => m_config.PartyFrames.OtherMaxCount,
        SlotCleanseThickness => m_config.PartyFrames.CleanseThickness,
        SlotRaiseThickness => m_config.PartyFrames.RaiseThickness,
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
            case SlotIconSize: m_config.PartyFrames.JobIconSize = value; break;
            case SlotIconX: m_config.PartyFrames.JobIconX = value; break;
            case SlotIconY: m_config.PartyFrames.JobIconY = value; break;
            case SlotNumberSize: m_config.PartyFrames.PartyNumberSize = value; break;
            case SlotNumberX: m_config.PartyFrames.PartyNumberX = value; break;
            case SlotNumberY: m_config.PartyFrames.PartyNumberY = value; break;
            case SlotLeaderSize: m_config.PartyFrames.LeaderIconSize = value; break;
            case SlotLeaderX: m_config.PartyFrames.LeaderIconX = value; break;
            case SlotLeaderY: m_config.PartyFrames.LeaderIconY = value; break;
            case SlotAuraSize: m_config.PartyFrames.AuraSize = value; break;
            case SlotAuraX: m_config.PartyFrames.AuraX = value; break;
            case SlotAuraY: m_config.PartyFrames.AuraY = value; break;
            case SlotAuraMax: m_config.PartyFrames.AuraMaxCount = (int)value; break;
            case SlotRescueSize: m_config.PartyFrames.RescueIconSize = value; break;
            case SlotRescueX: m_config.PartyFrames.RescueIconX = value; break;
            case SlotRescueY: m_config.PartyFrames.RescueIconY = value; break;
            case SlotBuffSize: m_config.PartyFrames.BuffSize = value; break;
            case SlotBuffX: m_config.PartyFrames.BuffX = value; break;
            case SlotBuffY: m_config.PartyFrames.BuffY = value; break;
            case SlotBuffMax: m_config.PartyFrames.BuffMaxCount = (int)value; break;
            case SlotOtherSize: m_config.PartyFrames.OtherSize = value; break;
            case SlotOtherX: m_config.PartyFrames.OtherX = value; break;
            case SlotOtherY: m_config.PartyFrames.OtherY = value; break;
            case SlotOtherMax: m_config.PartyFrames.OtherMaxCount = (int)value; break;
            case SlotCleanseThickness: m_config.PartyFrames.CleanseThickness = value; break;
            case SlotRaiseThickness: m_config.PartyFrames.RaiseThickness = value; break;
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

            // One slider on these screens counts things rather than measuring them, and
            // "4 px" on a number of icons would be nonsense on the one row that reads it.
            m_sizeText[slot] = slot is SlotAuraMax or SlotBuffMax or SlotOtherMax
                ? pixels.ToString(CultureInfo.InvariantCulture)
                : pixels.ToString(CultureInfo.InvariantCulture) + " px";
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

    /// <summary>
    /// A percentage for a slider's own caption, built only when the number changes.
    /// <para>
    /// The cache is the point, not the formatting: this runs every frame for every opacity
    /// slider on the screen, and <c>ToString</c> allocates every time it is called (CLAUDE.md
    /// §7.1). Each caller keeps its own pair of fields, because two sliders at different values
    /// sharing one cache would rebuild the string on every frame — the exact thing the cache
    /// exists to prevent.
    /// </para>
    /// </summary>
    private static string PercentCaption(float value, ref int cachedFor, ref string cached)
    {
        int percent = (int)MathF.Round(value * 100f);

        if (percent != cachedFor)
        {
            cachedFor = percent;
            cached = percent.ToString(CultureInfo.InvariantCulture) + " %";
        }

        return cached;
    }

    private string OpacityCaption(float opacity) =>
        PercentCaption(opacity, ref m_opacityTextFor, ref m_opacityText);

    private string CleanseOpacityCaption(float opacity) =>
        PercentCaption(opacity, ref m_cleanseOpacityTextFor, ref m_cleanseOpacityText);

    private string RaiseOpacityCaption(float opacity) =>
        PercentCaption(opacity, ref m_raiseOpacityTextFor, ref m_raiseOpacityText);

    private string ShieldOpacityCaption(float opacity) =>
        PercentCaption(opacity, ref m_shieldOpacityTextFor, ref m_shieldOpacityText);
}
