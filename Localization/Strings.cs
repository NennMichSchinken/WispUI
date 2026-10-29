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

    // --- the preview band ---------------------------------------------------
    // The counts are written as numbers, because that is what they are. "Light party" would
    // be the game's word for one of them and have no counterpart for the other two.
    public const string Preview = "Preview";
    public const string PreviewSolo = "1";
    public const string PreviewLight = "4";
    public const string PreviewFull = "8";
    public const string EditMode = "Edit Mode";
    public const string EditModeKeys = "Drag or use the arrows  ·  Shift for 10  ·  Ctrl ignores the guides";
    public const string EditModeDone = "Done";
    public const string NewBadge = "New";

    // --- Navigation ---------------------------------------------------------
    public const string NavGlobal = "Global";
    public const string NavProfile = "Profile";
    public const string NavPartyFrames = "Party List";

    // --- Tabs ---------------------------------------------------------------
    public const string TabBase = "Base";
    public const string TabLayout = "Layout";
    public const string TabColours = "Colours";
    // --- Subheadings inside a card. Rare on purpose — see Chrome.Subhead.
    // --- Module header ------------------------------------------------------
    public const string StateOn = "on";
    public const string StateOff = "off";

    // --- Arrow selector -----------------------------------------------------
    public const string SearchHint = "Search";
    public const string SelectorEmpty = "Nothing to choose from";
    public const string SearchNoMatch = "Nothing matches.";

    // --- Bars and job icons, as the meter draws them --------------------------
    public const string BarStyle = "Bar style";
    public const string BarColour = "Bar colour";
    public const string SmoothBars = "Smooth bars";
    public const string ColourByRole = "By role";
    public const string ColourByJob = "By job";
    public const string IconStyle = "Style";
    public const string IconStyleFramed = "Framed";
    public const string IconStylePlain = "Plain";
    // --- bindings -----------------------------------------------------------
    public const string TabBindings = "Bindings";
    public const string GroupJob = "Job";
    public const string GroupJobHint = "The list below belongs to the job picked here.";
    public const string BindingJob = "Set up for";
    public const string BindingJobTooltip = "Kept per job, because which spells go to the pointer depends on what you play.";
    public const string BindingNoActions = "This job has nothing that can be aimed at a party member.";

    // --- colours -----------------------------------------------------------
    public const string GroupRoles = "Roles";
    public const string GroupRolesHint = "Used wherever something is coloured by role rather than by job.";
    public const string GroupTanks = "Tanks";
    public const string GroupHealers = "Healers";
    public const string GroupMelee = "Melee";
    public const string GroupRangedCasters = "Ranged and casters";
    public const string ResetColour = "Back to the colour WispUI ships with.";

    // --- lettering ----------------------------------------------------------
    public const string GroupLettering = "Lettering";
    public const string GroupLetteringHint = "The face of the numbers on the meter and on Quick Dispel, and what carries them over the world.";
    public const string TextFont = "Font";
    public const string TextFontTooltip = "The game's own faces, two that ship with WispUI, and any .ttf you put in the plugin's Fonts folder.";
    public const string TextWeight = "Weight";
    public const string TextWeightTooltip = "Lays the face down more heavily. Not a second font — the letterforms do not change.";
    public const string TextWeightNormal = "Normal";
    public const string TextWeightMedium = "Medium";
    public const string TextWeightBold = "Bold";
    public const string TextEdge = "Edge";
    public const string TextEdgeTooltip = "What sits behind the letters so they read over a bright background.";
    public const string TextEdgeNone = "None";
    public const string TextEdgeShadow = "Shadow";
    public const string TextEdgeOutline = "Outline";

    // Says exactly what it does and no more. A hotbar key still goes to the selected target —
    // the game has no setting that changes that, whatever we assumed (Florian, 2026-09-12).
    // The group is named after what it does, not after a switch, because it no longer is one:
    // the list of spells is the setting (Florian, 2026-09-19).
    public const string GroupMouseover = "Mouseover casting";
    public const string GroupMouseoverHint = "Spells that go to whoever is under the pointer instead of your target. Off in PvP.";
    public const string MouseoverAdd = "Add spell";
    public const string MouseoverPick = "Pick a spell…";

    // --- Global screen ------------------------------------------------------
    public const string SectionInterface = "Interface";
    public const string SectionInterfaceHint = "How large WispUI draws, on this screen.";
    public const string InterfaceScale = "Scale";

    /// <summary>A few words beside the label. The rest is in the tooltip, not in the flow.</summary>
    public const string InterfaceScaleNote = "100 % is the sharp one";

    public const string InterfaceScaleTooltip = "Sizes everything WispUI draws, this window included.";
    public const string WindowStyle = "Window style";
    public const string WindowStyleTooltip = "Game matches the colour theme set in the game's own options: Clear Blue turns these windows blue, every other theme keeps them grey.";
    public const string WindowStyleGame = "Game";
    public const string WindowStyleGrey = "Grey";
    public const string WindowStyleClearBlue = "Clear Blue";

    public const string SectionAccess = "Access";
    public const string SectionAccessHint = "How to reach these settings.";
    public const string ShowInfoBarEntry = "Server info bar entry";
    public const string ShowInfoBarEntryTooltip = "Opens this window from next to the clock.";

    public const string InfoBarTooltip = "Open the WispUI settings.";

    // --- Placeholders while the screens are still empty ----------------------
    public const string NothingHereYet = "Nothing here yet";
    public const string SkeletonNote = "The controls arrive with the module itself.";
    public const string NewsNew = "New";
    public const string NewsChanged = "Changed";
    public const string NewsFixed = "Fixed";
    public const string NewsTitle = "What's new";
    public const string NewsOpenHint = "What changed in this release.";

    // --- Disabled-state explanations -----------------------------------------
    // One short line each. A disabled control owes a reason, not a paragraph.
    public const string DefaultsDisabled = "Nothing to reset yet.";
    public const string SelectorAtStart = "Start of the list.";
    public const string SelectorAtEnd = "End of the list.";

    // --- The party list: the marks on the game's own list ----------------------
    public const string GroupCleanse = "Cleansable";
    public const string GroupCleanseHint = "A mark on the row of anyone your cleanse would help.";
    public const string MarkOpacity = "Fill strength";
    public const string MarkOpacityTooltip =
        "How solid the mark is.";

    public const string CleanseColour = "Colour";
    public const string GroupRaiseMark = "Being raised";
    public const string GroupRaiseMarkHint = "A mark on the row of anyone a raise is already on its way to.";
    public const string RaiseColour = "Colour";
    public const string CleanseWhenAble = "Only on cleanse jobs";
    public const string CleanseWhenAbleTooltip =
        "The mark is an instruction. Off, it also appears on jobs that cannot act on it.";

    // --- Edit mode -----------------------------------------------------------
    public const string EditModeOn = "Edit Mode is on";
    public const string EditModeHint = "Shows placeholders so everything can be placed without a party.";
    public const string PreviewName = "Placeholder";

    // Role names, said the way a player says them. Used where a profile names a role.
    public const string RoleTank = "Tanks";
    public const string RoleHealer = "Healers";
    public const string RoleDps = "Damage";

    // --- Profiles ------------------------------------------------------------
    public const string GroupProfiles = "Profiles";
    public const string GroupProfilesHint = "One set of settings per job, and the one at the top for everything else.";
    public const string ProfileDefaultName = "Default";
    public const string ProfileUnnamed = "Unnamed";
    public const string ProfileAdd = "Add a profile";
    public const string ProfileNewName = "New profile";
    public const string ProfileRename = "Rename";
    public const string ProfileRemove = "Remove";
    public const string ProfileFallbackScope = "everything not spoken for";
    public const string ProfileFallbackKeep = "The top profile catches every job no other one claims, so it stays.";
    public const string ProfileFull = "Twenty profiles is the most there is room for.";
    public const string ProfileActiveTooltip = "Put this profile on.";
    public const string ProfileManualOnly = "only by hand";

    public const string GroupProfileScope = "Where it applies";
    public const string ProfileAutomatic = "Switch automatically";
    public const string ProfileAutomaticTooltip =
        "Changing to a job this profile covers puts it on. Never during a fight — the switch waits until the fight is over.";
    public const string ProfileAppliesTo = "Applies to";
    public const string ProfileScopeRole = "A role";
    public const string ProfileScopeJobs = "Named jobs";
    public const string ProfileRole = "Role";
    public const string ProfileJobsNone = "No job picked yet.";
    public const string ProfileJobAdd = "Add a job";
    public const string ProfileScopeBeaten = "A profile naming this job on its own wins over one naming its role.";

    public const string GroupProfileShare = "Share";
    public const string GroupProfileShareHint = "Send this profile to somebody, or take theirs.";
    public const string ProfileThisOne = "This profile";
    public const string ProfileCopyCode = "Copy code";
    public const string ProfileCopied = "Copied. Paste it wherever you like.";
    public const string ProfileFromCode = "From a code";
    public const string ProfilePaste = "Paste";
    public const string ProfilePasteHint = "Pasting asks what to take.";

    public const string ProfilePasteTitle = "A profile from a code";
    public const string ProfilePasteInto = "Put it";
    public const string ProfilePasteAsNew = "In a new profile";
    public const string ProfilePasteOverCurrent = "Over the one on screen";
    public const string ProfilePasteApply = "Take it";
    public const string ProfilePasteNothing = "Nothing ticked.";
    public const string ProfilePartPartyFrames = "Party list";
    public const string ProfilePartScope = "Which jobs it is for";

    public const string ProfileCodeEmpty = "There is nothing on the clipboard.";
    public const string ProfileCodeNotOurs = "That is not a WispUI profile code.";
    public const string ProfileCodeNewer = "That code is from a newer version of WispUI.";
    public const string ProfileCodeUnreadable = "That code is damaged. Ask for it again — chat windows sometimes cut them.";

    // --- Combat tracker ---

    /// <summary>
    /// What a meter counts. Sentence case like every other label in the suite — the game's own
    /// tools title-case these, and matching them would be the one place WispUI does.
    /// </summary>
    public const string MetricDamageDone = "Damage done";

    public const string MetricDamageTaken = "Damage taken";

    public const string MetricHealingDone = "Healing done";

    public const string MetricHealingTaken = "Healing taken";

    public const string MetricDeaths = "Deaths";

    public const string NavCombatTracker = "Combat Tracker";

    // --- Quick Dispel ---------------------------------------------------------
    public const string NavQuickDispel = "Quick Dispel";
    public const string GroupDispelSquares = "Squares";
    public const string GroupDispelSquaresHint = "One per party member. Hold Alt over them to move them.";
    public const string DispelSquareSize = "Square size";
    public const string DispelLayout = "Layout";
    public const string DispelLayoutRow = "Row";
    public const string DispelLayoutGrid = "4 across";
    public const string DispelPartyNumber = "Party number";
    public const string DispelPartyNumberTooltip = "Writes each member's place in the party in their square while nothing is on them.";
    public const string GroupDispelShown = "When it shows";
    public const string GroupDispelShownHint = "A click always casts on that member; a lit square needs it. No clicks in PvP.";
    public const string DispelOnlyWhenAble = "Only on jobs that can cleanse";
    public const string DispelOnlyWhenAbleTooltip = "Hides the squares on any job without Esuna, the Warden's Paean or Exuviation.";
    public const string DispelHideWhenClear = "Hide when nobody needs one";
    public const string DispelHideWhenClearTooltip = "Hides the squares until somebody has an effect you can take off. Off, they stay where your eye expects them.";

    // --- Quality of Life ------------------------------------------------------
    public const string NavQualityOfLife = "Quality of Life";
    public const string GroupSlidecast = "Cast bar";
    public const string GroupSlidecastHint = "Marks the end of a cast you can already move out of.";
    public const string SlidecastWaitColour = "Not yet";
    public const string SlidecastWaitTooltip = "The outline over the last half second of a cast, before the server has taken it. Moving now still cancels the cast.";
    public const string SlidecastReadyColour = "Safe to move";
    public const string SlidecastReadyTooltip = "The colour the moment the server has taken the cast. From here on you can move and the cast still goes through.";
    public const string SlidecastFill = "Fill when safe";
    public const string SlidecastFillTooltip = "Fills the window with the safe colour, over the cast bar's own fill. Off, only the frame changes colour.";
    public const string GroupCursorRing = "Mouse ring";
    public const string GroupCursorRingHint = "A ring round the pointer, so you find it again in a busy fight.";
    public const string CursorRingSize = "Size";
    public const string CursorRingThickness = "Thickness";
    public const string CursorRingColour = "Colour";

    // On the meter itself.
    public const string MeterViewCurrent = "Current";
    public const string MeterViewOverall = "Overall";
    public const string MeterViewFight = "Earlier fight";
    public const string MeterNotConnected = "Waiting for IINACT";
    public const string MeterNoData = "Waiting for combat data";
    public const string MeterResetTooltip = "Start over";
    public const string MeterFightsTooltip = "Earlier fights";
    public const string MeterMetricTooltip = "What to show";
    public const string MeterSettingsTooltip = "Settings";
    public const string MeterLocked = "Locked. Click to unlock.";
    public const string MeterUnlocked = "Click to lock in place.";
    public const string MeterGripTooltip = "Drag to resize. Hold Shift to change one side only.";
    public const string MeterResetQuestion = "Reset all combat data?";
    public const string MeterResetConfirm = "Reset";
    public const string MeterResetCancel = "Cancel";

    // The meter's details: the panel under an opened bar, and the window.
    public const string DetailOpen = "Details";
    public const string DetailCritShort = "Crit";
    public const string DetailDirectShort = "DH";
    public const string DetailCritDirectShort = "CDH";
    public const string DetailOverhealShort = "Overheal";
    public const string DetailTotalDamage = "Damage";
    public const string DetailRateDamage = "DPS";
    public const string DetailTotalHeal = "Healing";
    public const string DetailRateHeal = "HPS";
    public const string DetailCrit = "Crit";
    public const string DetailDirectHit = "Direct hit";
    public const string DetailCritDirectHit = "Crit direct hit";
    public const string DetailBiggestHit = "Biggest hit";
    public const string DetailMissed = "Missed";
    public const string DetailParryBlock = "Taken: parry / block";
    public const string DetailDamageTaken = "Damage taken";
    public const string DetailDeaths = "Deaths";
    public const string DetailCritHeal = "Crit heal";
    public const string DetailOverheal = "Overheal";
    public const string DetailShields = "Shields";
    public const string DetailBiggestHeal = "Biggest heal";
    public const string DetailHeals = "Heals";
    public const string DetailHealingTaken = "Healing taken";
    public const string DetailColSkill = "SKILL";
    public const string DetailColAmount = "AMOUNT";
    public const string DetailColShare = "SHARE";
    public const string DetailColHits = "HITS";
    public const string DetailColCrit = "CRIT";
    public const string DetailColDirect = "DH";
    public const string DetailColOverheal = "OVERHEAL";
    public const string DetailColAverage = "AVG";
    public const string DetailColBiggest = "MAX";
    public const string DetailDamage = "Damage";
    public const string DetailHealing = "Healing";
    public const string DetailDamageOverTime = "Damage over time";
    public const string DetailHealOverTime = "Healing over time";
    public const string DetailDotTooltip = "Every tick of your damage over time effects together. The game does not say which effect ticked.";
    public const string DetailHotTooltip = "Every tick of your healing over time effects together. The game does not say which effect ticked.";
    public const string DetailOverTheFight = "OVER THE FIGHT";
    public const string DetailShowParty = "Show party";
    public const string DetailBossHp = "Boss HP";
    public const string DetailTimeZero = "0:00";
    public const string DetailNoLog = "No skill data for this fight. Skills are recorded while WispUI is loaded.";
    public const string DetailNoLogShort = "No skill data for this fight.";
    public const string DetailNoDamage = "No damage in this fight.";
    public const string DetailNoHealing = "No healing in this fight.";
    public const string DetailChartOverall = "No chart for Overall. Pick a single fight.";
    public const string DetailChartWaiting = "Nothing to draw yet.";

    // The settings page.
    public const string GroupMeterBars = "Bars";
    public const string GroupMeterBarsHint = "Fill, colour and opacity.";
    public const string MeterBarOpacity = "Bar opacity";
    public const string MeterSmoothTooltip = "Bars slide to their new length instead of jumping.";
    public const string GroupMeterText = "On each bar";
    public const string GroupMeterTextHint = "What every line says.";
    public const string MeterJobMark = "Job";
    public const string MeterJobIcon = "Icon";
    public const string MeterJobLetters = "Letters";
    public const string MeterJobOff = "Off";
    public const string MeterRanks = "Rank numbers";
    public const string MeterShortNumbers = "Short numbers";
    public const string MeterShortNumbersTooltip = "1.2M instead of 1,234,567.";
    public const string MeterTextSize = "Text size";
    public const string GroupMeterFights = "Fights";
    public const string GroupMeterFightsHint = "When the meter starts over.";
    public const string MeterOnlyInCombat = "Only in combat";
    public const string MeterAutoReset = "Reset on entering a duty";
    public const string MeterConfirmReset = "Ask before resetting";
    public const string MeterEndOnReset = "End the fight on reset";
    public const string MeterEndOnResetTooltip = "Tells IINACT the fight is over whenever you reset, so the next pull starts clean.";
    public const string MeterEndAfterCombat = "End the fight after combat";
    public const string MeterEndAfterCombatTooltip = "Tells IINACT the fight is over a few seconds after combat ends.";
    public const string GroupMeterSize = "Size";
    public const string GroupMeterSizeHint = "Or drag the meter's bottom right corner.";
    public const string MeterWidth = "Width";
    public const string MeterHeight = "Height";
    public const string MeterBarHeight = "Bar height";
    public const string MeterBarSpacing = "Bar spacing";
    public const string MeterTitleHeight = "Title height";
    public const string MeterTitleText = "Title text size";
    public const string GroupMeterLook = "Meter";
    public const string GroupMeterLookHint = "How the meter sits on the screen.";
    public const string MeterRim = "Gold rim";
    public const string MeterBackground = "Background opacity";
    public const string MeterLockedRow = "Locked";
    public const string MeterLockedTooltip = "A locked meter cannot be moved or resized with the mouse. Edit Mode still moves it.";
    public const string MeterTestMode = "Show test data";
    public const string MeterTestModeTooltip = "Fills the meter with a made-up fight so you can set it up. Switches off when this window closes.";

    // IINACT, where it stands — said in the meter and on its page, never anywhere else.
    public const string MeterIinactMissing = "IINACT is not installed";
    public const string MeterIinactMissingBody = "The meter reads its numbers from IINACT. Open the settings to set it up.";
    public const string MeterIinactStopped = "IINACT is not running";
    public const string MeterIinactStoppedBody = "It is installed, but Dalamud did not start it. After a game patch it usually needs an update.";
    public const string MeterIinactSilent = "IINACT is not answering";
    public const string MeterIinactSilentBody = "It is running, but not sending anything. Restarting the game usually helps.";

    // The wizard.
    public const string WizardStep1 = "1 · Copy the address";
    public const string WizardStep2 = "2 · Add the repository";
    public const string WizardStep3 = "3 · Install IINACT";
    public const string WizardKicker1 = "Combat Tracker needs IINACT · step 1 of 3";
    public const string WizardKicker2 = "Combat Tracker needs IINACT · step 2 of 3";
    public const string WizardKicker3 = "Combat Tracker needs IINACT · step 3 of 3";
    public const string WizardTitle1 = "Copy IINACT's repository address";
    public const string WizardTitle2 = "Add the repository to Dalamud";
    public const string WizardTitle3 = "Install IINACT";
    public const string WizardBody1 = "The meter reads its numbers from IINACT, a free plugin that lives in its own repository rather than in Dalamud's main list. Copy its address first.";
    public const string WizardBody2 = "Open Dalamud's settings and paste the address into the empty line of Custom Plugin Repositories, the lower of the two lists. The Dev Plugin Locations list above it wants a folder and will say the path is not valid. If the address is already in the list, skip this step.";
    public const string WizardBody3 = "Search the plugin installer for IINACT and install it. This page moves on by itself once IINACT is running.";
    public const string WizardPath = "Experimental  ›  Custom Plugin Repositories (the lower list)  ›  paste  ›  +  ›  Save";
    public const string WizardStatusMissing = "Not installed yet.";
    public const string WizardStatusStopped = "Installed, but not running yet.";
    public const string WizardStatusStarting = "Installed. Waiting for it to start.";
    public const string WizardStatusSilent = "Installed and started, but not answering. Restarting the game usually helps.";
    public const string WizardCopy = "Copy address";
    public const string WizardCopied = "Copied. Copy again";
    public const string WizardOpenSettings = "Open Dalamud settings";
    public const string WizardOpenInstaller = "Open plugin installer";
    public const string WizardOpenUpdates = "Open plugin updates";
    public const string WizardOpenInstalled = "Open installed plugins";
    public const string WizardBack = "Back";
    public const string WizardNext = "Next";
    public const string WizardGuide = "Step by step guide on iinact.com";
}
