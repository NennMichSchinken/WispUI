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
    public const string Close = "Close";
    public const string Apply = "Apply";
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
    public const string StateOn = "on";
    public const string StateOff = "off";

    // --- Placeholders while the screens are still empty ----------------------
    public const string NothingHereYet = "Nothing here yet.";
    public const string SkeletonNote = "The window frame is standing. The controls for this screen arrive with the module itself.";
    public const string PatchNotesLine1 = "The window frame is standing.";
    public const string PatchNotesLine2 = "Click for the patch notes.";
    public const string PatchNotesUnavailable = "The patch notes arrive with the first release.";

    // --- Disabled-state explanations -----------------------------------------
    public const string EditModeDisabled = "Edit Mode needs a HUD element to move. The first one arrives with Party Frames.";
    public const string DefaultsDisabled = "There is nothing to reset yet.";
    public const string ApplyDisabled = "Changes are saved on their own, a moment after you stop.";
    public const string ClipboardDisabled = "The appearance clipboard arrives with the first HUD element.";
}
