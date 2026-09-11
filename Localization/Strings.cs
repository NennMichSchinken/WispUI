namespace WispUI.Localization;

/// <summary>
/// Every piece of text the user can read, in one place. No literals in the UI code —
/// that is what keeps a language layer a later addition instead of a rewrite.
/// English is both the code default and the shipping language.
/// </summary>
internal static class Strings
{
    // --- Identity -----------------------------------------------------------
    public const string PluginName = "WispUI";
    public const string WindowTitle = "WISPUI";

    /// <summary>The ImGui window id. The part after ### is what keeps the window identity stable.</summary>
    public const string WindowId = "WispUI###WispUIConfig";

    public const string Command = "/wisp";
    public const string CommandHelp = "Open the WispUI settings window.";

    // --- Window chrome ------------------------------------------------------
    public const string Defaults = "Defaults";
    public const string EditMode = "Edit Mode";
    public const string NewBadge = "New";
    public const string Soon = "Soon";

    // --- Navigation ---------------------------------------------------------
    public const string NavGlobal = "Global";
    public const string NavProfile = "Profile";
    public const string NavPartyFrames = "Party Frames";
    public const string NavPlayerBars = "Player Bars";
    public const string NavJobGauges = "Job Gauges";

    // --- Tabs ---------------------------------------------------------------
    public const string TabBase = "Base";
    public const string TabLayout = "Layout";
    public const string TabAuras = "Auras";

    // --- Module header ------------------------------------------------------
    public const string CopyAppearance = "Copy appearance";
    public const string PasteAppearance = "Paste\u2026";
    public const string UndoPaste = "Undo paste";
    public const string StateOn = "on";
    public const string StateOff = "off";

    // --- Arrow selector -----------------------------------------------------
    public const string SearchHint = "Search";
    public const string SelectorEmpty = "Nothing to choose from";
    public const string SearchNoMatch = "Nothing matches.";

    // --- Appearance clipboard ------------------------------------------------
    public const string PasteFrom = "from: ";
    public const string PasteApply = "Apply";
    public const string PasteCancel = "Cancel";

    /// <summary>Beside a field the target element does not have. A few words, not a sentence.</summary>
    public const string FieldUnsupported = "not on this one";

    public const string FieldColours = "Colours";
    public const string FieldTexture = "Texture";
    public const string FieldShape = "Shape";
    public const string FieldText = "Text";
    public const string FieldBackground = "Background";
    public const string FieldOpacity = "Opacity";

    // --- Party Frames --------------------------------------------------------
    public const string GroupHealthBar = "Health bar";
    public const string GroupHealthBarHint = "Fill, colour and opacity.";
    public const string GroupNameText = "Name text";
    public const string GroupNameTextHint = "The player name on the frame.";
    public const string BarStyle = "Bar style";
    public const string BarColour = "Bar colour";
    public const string NamePosition = "Position";
    public const string NameInJobColour = "Name in job colour";
    public const string ShortenNames = "Shorten long names";
    public const string ShortenNamesTooltip = "Cuts the surname to an initial.";

    // The nine points a text can hang on, in reading order. One list for every text on every
    // element, so nobody has to learn the same word twice.
    public const string PositionTopLeft = "Top left";
    public const string PositionTop = "Top";
    public const string PositionTopRight = "Top right";
    public const string PositionLeft = "Left";
    public const string PositionCentre = "Centre";
    public const string PositionRight = "Right";
    public const string PositionBottomLeft = "Bottom left";
    public const string PositionBottom = "Bottom";
    public const string PositionBottomRight = "Bottom right";

    public const string BarOpacity = "Health bar opacity";
    public const string SmoothBars = "Smooth bars";
    public const string SmoothBarsTooltip = "Health slides to its new value instead of jumping.";

    public const string ColourByRole = "By role";
    public const string ColourByJob = "By job";
    public const string ColourFixed = "Fixed colour";

    // --- Party frames: health text -------------------------------------------
    public const string GroupHealthText = "Health text";
    public const string GroupHealthTextHint = "The figure on the bar.";
    public const string HealthTextMode = "Shows";
    public const string HealthTextOff = "Nothing";
    public const string HealthTextCurrent = "Health";
    public const string HealthTextPercent = "Percent";
    public const string HealthTextDeficit = "Missing";
    public const string HealthTextDeficitTooltip = "How much is gone, and nothing at all while nothing is.";

    public const string TextSize = "Size";
    public const string TextSizeSmall = "Small";
    public const string TextSizeNormal = "Normal";
    public const string TextSizeLarge = "Large";
    public const string TextPosition = "Position";
    public const string OffsetX = "Offset X";
    public const string OffsetY = "Offset Y";

    // --- Party frames: mana ---------------------------------------------------
    public const string GroupMana = "Mana";
    public const string GroupManaHint = "A second reading, for who needs one.";
    public const string ManaStyle = "Style";
    public const string ManaStyleStrip = "Strip";
    public const string ManaStyleBar = "Bar";
    public const string ManaHeight = "Height";
    public const string ManaHeightHint = "pixels, whatever the frame height";
    public const string ManaForTanks = "On tanks";
    public const string ManaForHealers = "On healers";
    public const string ManaForDps = "On damage";

    // --- Global screen ------------------------------------------------------
    public const string SectionInterface = "Interface";
    public const string SectionInterfaceHint = "How large WispUI draws, on this screen.";
    public const string InterfaceScale = "Scale";

    /// <summary>A few words beside the label. The rest is in the tooltip, not in the flow.</summary>
    public const string InterfaceScaleNote = "100 % is the sharp one";

    public const string InterfaceScaleTooltip = "Sizes everything WispUI draws, this window included.";

    public const string SectionAccess = "Access";
    public const string SectionAccessHint = "How to reach these settings.";
    public const string ShowInfoBarEntry = "Server info bar entry";
    public const string ShowInfoBarEntryTooltip = "Opens this window from next to the clock.";

    public const string InfoBarTooltip = "Open the WispUI settings.";

    // --- Placeholders while the screens are still empty ----------------------
    public const string NothingHereYet = "Nothing here yet";
    public const string SkeletonNote = "The controls arrive with the module itself.";
    public const string PatchNotesLine1 = "The window frame is standing.";
    public const string PatchNotesLine2 = "Click for the patch notes.";
    public const string PatchNotesUnavailable = "Arrives with the first release.";

    // --- Disabled-state explanations -----------------------------------------
    // One short line each. A disabled control owes a reason, not a paragraph.
    public const string EditModeDisabled = "Needs a HUD element to move.";
    public const string DefaultsDisabled = "Nothing to reset yet.";
    public const string PasteDisabled = "Nothing copied yet.";
    public const string ApplyDisabled = "Nothing ticked that this one has.";
    public const string SelectorAtStart = "Start of the list.";
    public const string SelectorAtEnd = "End of the list.";

    // --- Party frames: layout ------------------------------------------------
    public const string GroupArrangement = "Arrangement";
    public const string GroupArrangementHint = "How the frames are laid out.";
    public const string GroupSize = "Size";
    public const string GroupSizeHint = "How big each frame is, and how far apart.";

    public const string Direction = "Direction";
    public const string DirectionVertical = "Vertical";
    public const string DirectionHorizontal = "Horizontal";
    public const string Lines = "Lines";

    public const string FrameWidth = "Frame width";
    public const string FrameHeight = "Frame height";
    public const string Spacing = "Spacing";
    public const string SpacingHint = "between frames";

    // The arrangement written out, so nobody has to picture it. Filled with the numbers.
    public const string ArrangementColumns = "{0} columns of {1}";
    public const string ArrangementRows = "{0} rows of {1}";
    public const string ArrangementOneColumn = "one column of 8";
    public const string ArrangementOneRow = "one row of 8";

    // --- Edit mode -----------------------------------------------------------
    public const string EditModeOn = "Edit Mode is on";
    public const string EditModeHint = "Shows eight placeholder frames so the layout can be set without a party.";
    public const string PreviewName = "Placeholder";
}
