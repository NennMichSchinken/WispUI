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
    public const string EditModeKeys = "Drag or use the arrows  ·  Shift for 10  ·  Ctrl ignores the guides";
    public const string EditModeDone = "Done";
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
    public const string TabIcons = "Icons";
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
    public const string FieldIcon = "Icon";

    // --- Party Frames --------------------------------------------------------
    public const string GroupHealthBar = "Health bar";
    public const string GroupHealthBarHint = "Fill, colour and opacity.";
    public const string GroupNameText = "Name text";
    public const string GroupNameTextHint = "The player name on the frame.";
    public const string BarStyle = "Bar style";
    public const string BarColour = "Bar colour";
    public const string NamePosition = "Position";
    public const string NameInJobColour = "Name in job colour";
    public const string ShortenNames = "Name length";

    // Written as what they do to a name rather than named in the abstract: "Fri Day" is one
    // look, and "abbreviate surname" is a sentence you have to translate in your head.
    public const string ShorteningFull = "Fri Day";
    public const string ShorteningSurname = "Fri D.";
    public const string ShorteningForename = "F. Day";

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
    public const string HealthTextCurrent = "Health";
    public const string HealthTextPercent = "Percent";
    public const string HealthTextDeficit = "Missing";

    public const string TextSize = "Size";
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

    // --- Party frames: job icon -----------------------------------------------
    public const string GroupJobIcon = "Job icon";
    public const string GroupJobIconHint = "Who is what, before you read a name.";
    public const string IconStyle = "Style";
    public const string IconStyleFramed = "Framed";
    public const string IconStylePlain = "Plain";
    public const string IconSize = "Size";
    public const string IconHideDps = "Hide on damage";
    public const string IconHideDpsTooltip = "Leaves the two tanks and two healers easy to find.";

    // --- Party frames: party number -------------------------------------------
    public const string GroupPartyNumber = "Party number";
    public const string GroupPartyNumberHint = "The 1 to 8 people are called out by.";
    // --- Party frames: the mouse ----------------------------------------------
    public const string GroupMouse = "Mouse";
    public const string GroupMouseHint = "What clicking and pointing do.";
    public const string HighlightHovered = "Ring the frame under the cursor";
    public const string HighlightHoveredTooltip = "The game's own party list marks it too.";
    public const string ClickToTarget = "Click to select";
    public const string ClickToTargetTooltip = "Left-click a frame to target that member.";
    // --- party frames: presence ----------------------------------------------
    // What a frame says instead of a health figure when the game has no numbers for that
    // member. Short, because it sits where a number sits.
    public const string PresenceOutOfRange = "Out of range";
    public const string PresenceAway = "Elsewhere";
    public const string PresenceOffline = "Offline";

    // --- bindings -----------------------------------------------------------
    public const string TabBindings = "Bindings";
    public const string GroupBindings = "Mouse bindings";
    public const string GroupBindingsHint = "What each button does on a frame. Kept per job.";
    public const string BindingJob = "Job";
    public const string BindingJobTooltip = "Bindings are kept per job, because what a button should do depends on what you play.";
    public const string BindingAdd = "Add binding";
    public const string BindingRemove = "Remove";
    public const string BindingAction = "Action";
    public const string BindingTarget = "Select target";
    public const string BindingContextMenu = "Game menu";
    public const string BindingsEmpty = "Nothing bound. This job's frames will not answer the mouse.";
    public const string BindingsReset = "Reset to defaults";
    public const string BindingNoActions = "This job has nothing that can be aimed at a party member.";

    public const string KeybindListening = "Press a button…";
    public const string ModCtrl = "Ctrl";
    public const string ModShift = "Shift";
    public const string ModAlt = "Alt";
    public const string MouseLeft = "Left";
    public const string MouseRight = "Right";
    public const string MouseMiddle = "Middle";
    public const string MouseFour = "Mouse 4";
    public const string MouseFive = "Mouse 5";

    // --- lettering ----------------------------------------------------------
    public const string GroupLettering = "Lettering";
    public const string GroupLetteringHint = "The face every text on a frame is set in, and what carries it over the world.";
    public const string TextFont = "Font";
    public const string TextFontTooltip = "The game's own faces, two that ship with WispUI, and any .ttf you put in the plugin's Fonts folder.";
    public const string TextWeight = "Weight";
    public const string TextWeightTooltip = "Lays the face down more heavily. Not a second font 2014 the letterforms do not change.";
    public const string TextWeightNormal = "Normal";
    public const string TextWeightMedium = "Medium";
    public const string TextWeightBold = "Bold";
    public const string TextEdge = "Edge";
    public const string TextEdgeTooltip = "What sits behind the letters so they read over a bright background.";
    public const string TextEdgeNone = "None";
    public const string TextEdgeShadow = "Shadow";
    public const string TextEdgeOutline = "Outline";

    public const string ContextMenu = "Right-click menu";
    public const string ContextMenuTooltip = "Right-click a frame for the game's own menu, the same one its party list opens.";
    public const string MouseoverTarget = "Mouseover target";

    // Says exactly what it does and no more. A hotbar key still goes to the selected target —
    // the game has no setting that changes that, whatever we assumed (Florian, 2026-09-12).
    public const string MouseoverTargetTooltip = "Makes <mo> macros act on whoever you point at.";
    public const string MouseoverCasting = "Cast on mouseover";
    public const string MouseoverCastingTooltip = "Sends an action to the frame under the cursor, without selecting them first.";

    public const string GroupLeader = "Leader";
    public const string GroupLeaderHint = "Who is in charge of the party.";

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

    // --- Auras: everything lying on a person ---------------------------------
    public const string GroupAuras = "Afflictions";
    public const string GroupAurasHint = "What is on them, worst first.";
    public const string GroupCleanse = "Cleansable";
    public const string GroupCleanseHint = "How a frame says Esuna would help.";
    public const string GroupRescue = "Rescue";
    public const string GroupRescueHint = "A raise on its way, and who cannot die.";

    public const string ShowAuras = "Show icons";
    public const string ShowAurasTooltip =
        "The effects on this person, ranked the way the game's own party list ranks them.";
    public const string AuraCount = "How many";
    public const string AuraCountTooltip =
        "Above this the lowest ranked are dropped. The game decides the ranking, not WispUI.";
    public const string AuraStacks = "Stacks";
    public const string AuraStacksTooltip = "The number on effects that stack. Nothing is drawn on those that do not.";
    public const string AuraSwipe = "Sweep";
    public const string AuraSwipeTooltip = "A dark wedge that sweeps off the icon as the effect runs out.";

    public const string CleanseHow = "Mark";
    public const string CleanseHowTooltip =
        "Cleansable effects always get a bright edge on their own icon. This is the mark on the frame itself.";
    public const string CleanseNone = "None";
    public const string CleanseBorder = "Frame edge";
    public const string CleanseBar = "Health bar";

    public const string ShowRescue = "Show icon";
    public const string ShowRescueTooltip =
        "A raise already cast on them, or a cooldown that is keeping them alive. Its own place, so a busy frame cannot push it out.";

    public const string GroupGameList = "Game's party list";
    public const string GroupGameListHint = "The list these frames stand in for.";

    public const string HideNativeList = "Hide it";
    public const string HideNativeListTooltip =
        "Hides the list the game draws, leaving only these frames. The frames keep its order and its right-click menu either way.";

    // Under the switch, because it is what the switch does not change. There is no sorting
    // setting in WispUI on purpose: the frames read the finished order out of the game.
    public const string NativeListSorting = "Order and numbers follow the game's own settings.";

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
