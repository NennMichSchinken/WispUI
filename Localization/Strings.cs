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
    public const string SectionAppearance = "Appearance";
    public const string SectionAppearanceHint = "Colour and style. Shared by every party frame.";
    public const string BarStyle = "Bar style";
    public const string BarColour = "Bar colour";
    public const string BarOpacity = "Health bar opacity";
    public const string SmoothBars = "Smooth bars";
    public const string SmoothBarsTooltip = "Health slides to its new value instead of jumping.";

    public const string ColourByRole = "By role";
    public const string ColourByJob = "By job";
    public const string ColourFixed = "Fixed colour";

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
}
