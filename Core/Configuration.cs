using System;
using Dalamud.Configuration;

namespace WispUI.Core;

/// <summary>
/// The saved state of the whole suite. The <see cref="Version"/> field and the migration
/// chain exist from the first commit on, so a later format change never needs a reset.
/// </summary>
[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    /// <summary>Bump this whenever the stored shape changes, and add a step to <see cref="Migrate"/>.</summary>
    public const int CurrentVersion = 14;

    /// <summary>How long the configuration may sit unsaved before it is written to disk.</summary>
    private static readonly TimeSpan SaveDelay = TimeSpan.FromSeconds(1.5);

    private DateTime m_dirtySince = DateTime.MaxValue;

    public int Version { get; set; } = CurrentVersion;

    // --- Global -------------------------------------------------------------
    /// <summary>Smallest and largest interface scale the slider offers.</summary>
    public const float MinScale = 0.75f;
    public const float MaxScale = 1.75f;

    /// <summary>The interface scale. The window has a fixed size, and this is what sets it.</summary>
    public float Scale { get; set; } = 1f;

    /// <summary>Show an entry in the server info bar that opens the settings window.</summary>
    public bool ShowInfoBarEntry { get; set; } = true;

    /// <summary>
    /// Whether the preview band under the module header is open.
    /// <para>
    /// Saved, unlike the eye switches that hide parts of what it shows. Those are a way of
    /// looking at one thing for a moment; this is whether you want the band at all, and
    /// having to open it every session would make the answer "no" for everybody.
    /// </para>
    /// </summary>
    public bool PreviewOpen { get; set; } = true;

    /// <summary>How many stand-ins the preview shows: one, four or a full party.</summary>
    public int PreviewCount { get; set; } = 4;

    /// <summary>
    /// The newest release whose notes have been opened, or empty for somebody who has never
    /// looked. Compared as a name rather than parsed as a number: a version is what it is
    /// called, and a release that never went out cannot be "greater" than one that did.
    /// </summary>
    public string NewsSeenVersion { get; set; } = string.Empty;

    // --- Modules ------------------------------------------------------------

    /// <summary>The quietest a health bar may be drawn. Below this it stops being readable.</summary>
    public const float MinBarOpacity = 0.2f;

    /// <summary>
    /// What a text on a HUD element starts at, in pixels: Axis's own body size, the one place
    /// the face is exactly sharp rather than scaled to fit.
    /// </summary>
    public const float DefaultTextSize = 16f;

    /// <summary>Smaller than this is not readable in a fight; larger does not belong on a frame.</summary>
    public const float MinTextSize = 8f;

    public const float MaxTextSize = 40f;

    /// <summary>
    /// How far a name, a figure or an icon may be nudged off its anchor, either way. Far
    /// enough to leave the frame entirely on every side, because that is a layout people
    /// build on purpose — the name above the frame with the icons beside it — and not a way
    /// to get something wrong (Florian, at the first job icon).
    /// <para>
    /// It is also the reach an element is drawn within: past this, something has run away
    /// rather than been placed.
    /// </para>
    /// </summary>
    public const float MaxTextOffset = 60f;

    // --- what a number may be -----------------------------------------------
    // 🔴 These live with the data, not with the slider that shows them.
    //
    // They used to live as private constants in the settings screen, which was fine for
    // exactly as long as the settings screen was the only way a number could get in here.
    // A profile arriving from somebody else is the other way, and a bound that only the
    // slider knows is a bound an import walks straight past. Sanitise is what reads them,
    // and it runs on load and on every import.
    //
    // The ranges themselves are unchanged, and the screen now takes them from here so the
    // two cannot drift.
    public const float MinFrameWidth = 90f;
    public const float MaxFrameWidth = 400f;
    public const float MinFrameHeight = 18f;
    public const float MaxFrameHeight = 150f;
    public const float MaxFrameSpacing = 24f;
    public const float MinManaHeight = 2f;
    public const float MaxManaHeight = 16f;
    public const float MinIconSize = 8f;
    public const float MaxIconSize = 48f;

    /// <summary>
    /// How far off screen an element may be put. Generous on purpose — a second monitor and
    /// a resolution change both leave things at coordinates that look wrong and are not —
    /// but not unbounded, because a position no screen contains is an element nobody can
    /// find again.
    /// </summary>
    public const float MaxPosition = 10000f;

    /// <summary>The thickest the edge on a removable affliction may be drawn.</summary>
    public const float MaxDispelThickness = 4f;

    /// <summary>
    /// How small and how large the numbers on an effect icon may be set, as a share of the
    /// icon's height.
    /// <para>
    /// Not up to a whole icon: a number the full height of its square has its outline
    /// hanging off every side, and past that the icon is a number with a picture behind it.
    /// </para>
    /// </summary>
    public const float MinAuraNumberSize = 0.35f;

    /// <inheritdoc cref="MinAuraNumberSize"/>
    public const float MaxAuraNumberSize = 0.95f;

    public bool PartyFramesEnabled { get; set; } = true;

    public PartyFramesConfig PartyFrames { get; set; } = new();

    /// <summary>
    /// The saved profiles and which one is on.
    /// <para>
    /// 🔴 The blocks above are still what everything reads. A profile is written into them
    /// when it is put on and read back out before it is replaced — see <see cref="ProfileSet"/>
    /// for why the renderer never goes through a profile to find its own settings.
    /// </para>
    /// </summary>
    public ProfileSet Profiles { get; set; } = new();

    /// <summary>
    /// The party frames' own settings. Split the way the whole suite is split: what you see
    /// here is appearance, which the clipboard can carry between elements — size, position
    /// and growth direction will live beside it and are never copied.
    /// </summary>
    [Serializable]
    public sealed class PartyFramesConfig
    {
        /// <summary>
        /// The bar style, by name.
        /// <para>
        /// 🔴 By NAME and not by position, the same lesson the face learned in version 5: the
        /// list grows and shrinks. Two painted styles were added in the middle and the
        /// placeholder ones will be thinned out once they have been looked at — either of
        /// those silently hands a stored index to a different style, and the player finds
        /// their bar changed by an update they did not ask for.
        /// </para>
        /// </summary>
        public string BarStyleName { get; set; } = Data.BarStyles.DefaultName;

        /// <summary>
        /// The old position in the style list. Nothing writes it any more; it is here so the
        /// migration to version 7 can read what the user had. Droppable once no stored
        /// configuration is older than that.
        /// </summary>
        public int BarStyle { get; set; }

        /// <summary>Index into the colour modes: by role, by job, or a fixed colour.</summary>
        public int ColourMode { get; set; }

        public float BarOpacity { get; set; } = 1f;

        /// <summary>
        /// Health slides to its new value instead of jumping. Off by default: movement is
        /// information, and a bar that is still catching up is lying about the current state.
        /// </summary>
        public bool SmoothBars { get; set; }

        /// <summary>Draw the player name on the frame at all — the switch in the group head.</summary>
        public bool ShowName { get; set; } = true;

        // --- how every text on a frame is carried ---------------------------------
        // Two settings for all of them rather than two per text. Which face and what is
        // behind it are questions about reading a frame, not about the name or the numbers
        // separately, and answering them once is what keeps the tab to four groups (§3.1).

        /// <summary>
        /// What is drawn behind every text on a frame so it reads over the world.
        /// <para>
        /// Shadow by default, which is what the frames shipped with. Outline is the game's own
        /// floating-text look, and what a bright background needs; None is for anyone who
        /// finds both noisy (Florian, 2026-09-12).
        /// </para>
        /// </summary>
        public int TextEdge { get; set; } = 1;

        /// <summary>
        /// The chosen edge, as the value the drawing code wants. Internal, which is also what
        /// keeps it out of the saved file: the stored shape is the index above, and a second
        /// spelling of the same setting in the JSON would be one to keep in step for nothing.
        /// </summary>
        internal Style.TextEdge Edge => Style.HudText.EdgeAt(this.TextEdge);

        /// <summary>
        /// Which of the game's own faces the frames are lettered in. Axis is the game's
        /// interface face and the suite's own; it is also light, which is what prompted the
        /// choice (Florian, 2026-09-12).
        /// <para>
        /// ⚠️ One face for the whole HUD, not one per element: a face is a font atlas entry
        /// and a lock per frame, so a second element asking for a second face would cost real
        /// work every frame. It lives here because the frames are the only HUD element there
        /// is; a second one means moving this to Global rather than copying it.
        /// </para>
        /// </summary>
        public string FontName { get; set; } = Style.FontLibrary.DefaultName;

        /// <summary>
        /// How heavily the face is laid down. Medium by default, not Normal: against a black
        /// edge every face reads thinner than it is, and the first build shipped at Normal was
        /// called thin for every face including the one brought in to compare against
        /// (Florian, 2026-09-12).
        /// </summary>
        public int TextWeight { get; set; } = 1;

        /// <summary>
        /// What each mouse button does on a frame, per job. See <see cref="BindingSet"/> for
        /// why it is per job and what a job answers to before anybody has set it up.
        /// </summary>
        public BindingSet Bindings { get; set; } = new();

        /// <summary>
        /// Which spells go to whoever the mouse is over, per job. See
        /// <see cref="MouseoverSet"/> for why this is a list and not a switch.
        /// </summary>
        public MouseoverSet Mouseover { get; set; } = new();

        /// <summary>
        /// The old position in a fixed list of six faces. Nothing writes it any more; it is
        /// here so the migration to version 5 can read what the user had. Droppable once no
        /// stored configuration is older than that.
        /// </summary>
        public int Font { get; set; }

        // --- name text ----------------------------------------------------------
        // Every text on a frame is described the same way: whether it shows, how big it is,
        // which of the nine points it hangs on, and how far it is nudged from there. One
        // anatomy for all of them, which is also why there is no padding slider (spec §11.2).

        /// <summary>Index into the nine anchor points.</summary>
        public int NamePosition { get; set; } = (int)Hud.Anchor.Left;

        /// <summary>
        /// In pixels. A frame can be anywhere from 18 to 150 tall, and what size a name wants
        /// to be follows from that, so it is a number rather than one of three steps.
        /// </summary>
        public float NameSize { get; set; } = DefaultTextSize;

        /// <summary>
        /// Starts clear of the job icon rather than under it: both hang on the left edge by
        /// default, and a name printed across an icon is not a default worth shipping. With
        /// the icon moved or switched off the name is simply indented, which is a look rather
        /// than a fault.
        /// </summary>
        public float NameX { get; set; } = 22f;

        public float NameY { get; set; }

        public bool NameInJobColour { get; set; }

        /// <summary>
        /// How much of the name is drawn: all of it, the surname cut to an initial, or the
        /// first name cut to one. Which half you keep is a matter of who you play with —
        /// on a static everyone is known by their first name, in a party finder group the
        /// surname is what tells two Alisaies apart (Florian, 2026-09-12).
        /// </summary>
        public int NameShortening { get; set; }

        /// <summary>
        /// The old yes-or-no shortening. Nothing writes it any more; it is here so the
        /// migration to version 4 can read what the user had. Droppable once no stored
        /// configuration is older than that.
        /// </summary>
        public bool ShortenNames { get; set; }

        // --- health text --------------------------------------------------------

        /// <summary>The figure's own switch. Whether it shows is not one of the things it says.</summary>
        public bool ShowHealthText { get; set; } = true;

        /// <summary>0 the current figure, 1 a percentage, 2 what is missing.</summary>
        public int HpTextMode { get; set; } = 1;

        /// <summary>In pixels, like every other text size on a frame.</summary>
        public float HpTextSize { get; set; } = DefaultTextSize;

        /// <summary>Index into the nine anchor points.</summary>
        public int HpTextPosition { get; set; } = (int)Hud.Anchor.Right;

        public float HpTextX { get; set; }

        public float HpTextY { get; set; }

        // --- mana ---------------------------------------------------------------

        /// <summary>
        /// The master switch. The three role switches decide who it then applies to.
        /// <para>
        /// Off out of the box (Florian, 2026-09-19). A second bar on every frame is a lot of
        /// height and a lot of ink for something most people never look at, and the ones who
        /// do — a healer watching their own, a Summoner — know they want it.
        /// </para>
        /// </summary>
        public bool ShowMana { get; set; }

        /// <summary>0 a thin strip along the bottom edge, 1 a bar of its own.</summary>
        public int ManaStyle { get; set; }

        /// <summary>
        /// 2-16 px, and a pixel count rather than a share of the frame: a strip that grew with
        /// the frame was absurd at 150 px tall (Florian, at the prototype). Thin trim follows
        /// the interface scale and nothing else.
        /// </summary>
        public float ManaHeight { get; set; } = 3f;

        /// <summary>
        /// Who gets a mana bar. Three switches rather than one "healers only": in a light
        /// party every caster's mana is worth a glance, in a full one it is mostly noise.
        /// </summary>
        public bool ManaForTanks { get; set; }

        public bool ManaForHealers { get; set; } = true;

        public bool ManaForDps { get; set; }

        // --- shields ------------------------------------------------------------
        // What the bar says about damage that will not land. Read straight off the byte the
        // game keeps beside a member's job and level, which is the same number its own party
        // list draws from, so ours can never disagree with the native one.

        /// <summary>The group's own switch. On: a shield nobody sees is a shield nobody uses.</summary>
        public bool ShowShield { get; set; } = true;

        /// <summary>
        /// The shield's colour. A pale warm white by default, which is neither a role colour
        /// nor a job colour and so cannot be mistaken for one.
        /// <para>
        /// ⚠️ Drawn at full strength and dimmed by colour where it must step back, never by
        /// opacity: the band over empty bar has the world behind it, and a faded band there
        /// picks up grass and stone and stops being a colour at all (CLAUDE.md, session 9).
        /// </para>
        /// </summary>
        /// <summary>
        /// The shield's own texture, by name, out of the same list the bar picks from.
        /// <para>
        /// Its own setting rather than the bar's, for the same reason its opacity is: a shield
        /// is something laid ON the bar, and what reads well as a bar fill is not what reads
        /// well as an overlay (Florian, 2026-09-18). A pattern that would be noise across a
        /// whole bar — stripes, say — is exactly what says "shield" on a short stretch of one.
        /// </para>
        /// </summary>
        public string ShieldStyleName { get; set; } = Data.BarStyles.ShieldDefaultName;

        public uint ShieldColour { get; set; } = Style.Tokens.Col.Shield;

        /// <summary>
        /// How strongly the shield is drawn, on its own and not tied to the health bar's.
        /// <para>
        /// 🔴 Deliberately separate. The bar's opacity answers "how much should this frame
        /// assert itself over the world"; this one answers "how much should the shield read as
        /// something laid ON the bar" — which is a different question with a different answer,
        /// and tying them made turning the frames down turn the shield down with them
        /// (Florian, 2026-09-18).
        /// </para>
        /// <para>
        /// ⚠️ Low values cost more than they look like they do wherever the band lies over
        /// EMPTY bar, because there the world is behind it and grass and stone drag it towards
        /// grey — the same trap as everywhere else on a HUD. Over the health fill it is only
        /// ever the overlay it was asked to be.
        /// </para>
        /// </summary>
        public float ShieldOpacity { get; set; } = 0.8f;

        // --- job icon -----------------------------------------------------------
        // Described exactly like a text is (spec §11.2): a switch, a size, one of the nine
        // anchors, and the two nudges off it. An icon is not a text, but where something sits
        // on a frame is the same question whatever it is, and answering it twice in two
        // vocabularies is how a settings screen stops being learnable.

        /// <summary>The group's own switch.</summary>
        public bool ShowJobIcon { get; set; } = true;

        /// <summary>
        /// In pixels, and square. Big enough at the default to read a job off at a glance in
        /// a 38 px frame without crowding the name beside it.
        /// </summary>
        public float JobIconSize { get; set; } = 20f;

        /// <summary>
        /// 0 the framed set, 1 the plain one. Two sets of the same icons the game ships;
        /// which one reads better depends on how busy the frame beside it is.
        /// </summary>
        public int JobIconStyle { get; set; }

        /// <summary>Index into the nine anchor points.</summary>
        public int JobIconPosition { get; set; } = (int)Hud.Anchor.Left;

        public float JobIconX { get; set; }

        public float JobIconY { get; set; }

        /// <summary>
        /// Hide it on damage dealers. In a light party every icon is a landmark; in a full one
        /// the six damage icons are the ones you never look at, and leaving them out is what
        /// makes the two tanks and two healers findable.
        /// </summary>
        public bool JobIconHideDps { get; set; }

        // --- the mouse ----------------------------------------------------------

        /// <summary>
        /// The old switch for selecting on left-click. Nothing reads it any more — it is a
        /// binding now, and the default binding set does exactly what it did. Kept so the
        /// migration to version 6 can see whether somebody had turned it off; droppable once
        /// no stored configuration is older than that.
        /// </summary>
        public bool ClickToTarget { get; set; } = true;

        /// <summary>
        /// The old switch for the right-click menu, in the same position as
        /// <see cref="ClickToTarget"/> and kept for the same reason.
        /// </summary>
        public bool ContextMenu { get; set; } = true;

        /// <summary>
        /// While the mouse is over a frame, tell the game that member is what it is pointing
        /// at. That is all it takes for the player's own mouseover macros and, with the game's
        /// own mouseover setting on, their hotbar to act on that member.
        /// </summary>
        public bool MouseoverTarget { get; set; } = true;

        /// <summary>
        /// The old blanket switch for mouseover casting, in the same position as
        /// <see cref="ClickToTarget"/> and kept for the same reason: the migration to version
        /// 10 reads it to tell whether anybody was relying on it. Nothing writes it any more
        /// — the spells are picked one at a time in <see cref="Mouseover"/>.
        /// </summary>
        public bool MouseoverCasting { get; set; }

        /// <summary>
        /// Ring the frame the mouse is over. On, because the game's own party list does it and
        /// a frame that answers the mouse without saying so is a frame you aim at twice.
        /// </summary>
        public bool HighlightHovered { get; set; } = true;

        // --- leader mark --------------------------------------------------------
        // The same anatomy again. No "hide on" switch: there is exactly one leader, and one
        // mark is never the thing that makes a party unreadable.

        /// <summary>
        /// Off by default. Who leads matters when it matters — pulling, ready checks, loot —
        /// and the rest of the time it is a mark on somebody's frame for no reason.
        /// </summary>
        public bool ShowLeaderIcon { get; set; }

        /// <summary>In pixels, and square.</summary>
        public float LeaderIconSize { get; set; } = 16f;

        /// <summary>Index into the nine anchor points.</summary>
        public int LeaderIconPosition { get; set; } = (int)Hud.Anchor.TopRight;

        public float LeaderIconX { get; set; }

        public float LeaderIconY { get; set; }

        // --- party number -------------------------------------------------------
        // The 1 to 8 the game's own party list puts in front of every member. Same anatomy
        // as every other thing on a frame, and it is the party's numbering, not ours: once
        // the frames can be sorted, member three stays the three they are called out as.

        /// <summary>
        /// Off by default. It is a real aid in a raid where people are called by number, and
        /// eight numbers nobody uses is eight pieces of furniture on the screen.
        /// </summary>
        public bool ShowPartyNumber { get; set; }

        /// <summary>
        /// In pixels, like every other text on a frame, but a step above the body size the
        /// rest starts at. A single digit has no word around it to be read from, so it needs
        /// to carry on its own — and 19 px is the next size Axis is drawn at rather than
        /// scaled to (Florian, 2026-09-12).
        /// </summary>
        public float PartyNumberSize { get; set; } = 19f;

        /// <summary>Index into the nine anchor points.</summary>
        public int PartyNumberPosition { get; set; } = (int)Hud.Anchor.TopLeft;

        public float PartyNumberX { get; set; }

        public float PartyNumberY { get; set; }

        // --- what is on the person ----------------------------------------------
        // The Auras tab. Not "debuff icons": everything lying on somebody, of which the
        // icons are one display and the cleanse mark and the rescue icon are two more, all
        // out of the same status pass (spec §13.2).

        /// <summary>
        /// The row of affliction icons. On, because it is the reason the tab exists.
        /// </summary>
        public bool ShowAuras { get; set; } = true;

        /// <summary>In pixels, and square, like every other icon on a frame.</summary>
        public float AuraSize { get; set; } = 20f;

        /// <summary>Index into the nine anchor points.</summary>
        public int AuraPosition { get; set; } = (int)Hud.Anchor.TopRight;

        public float AuraX { get; set; }

        public float AuraY { get; set; }

        /// <summary>
        /// How many fit before the game's own ranking starts dropping them. Four, because a
        /// frame is not a debuff list — the ones that matter rank highest, and a row of
        /// twelve tiny squares is unreadable at the moment it would be needed.
        /// </summary>
        public int AuraMaxCount { get; set; } = 4;

        /// <summary>
        /// The stack count on effects that carry one. On: a stacking debuff is a different
        /// thing at one stack than at five, and the number is the only thing that says so.
        /// </summary>
        public bool AuraShowStacks { get; set; } = true;

        /// <summary>
        /// The dark wedge that sweeps off an icon as it runs out — the game's own way of
        /// showing a duration, read without a number.
        /// </summary>
        public bool AuraSwipe { get; set; } = true;

        /// <summary>
        /// The seconds left, written across the icon.
        /// <para>
        /// Off by default. The sweep already says how much is left without asking anybody to
        /// read anything, and it does it at every icon size; a number is for somebody who
        /// wants to know whether it is four seconds or two.
        /// </para>
        /// <para>
        /// 🔴 No size of its own, the way the stack count has none: it is a share of the icon
        /// it sits on. A number sized independently is a number that runs out of its icon the
        /// moment somebody moves the icon slider (Florian asked, 2026-09-21; the answer is
        /// the same one the stack count already gives).
        /// </para>
        /// </summary>
        public bool AuraShowDuration { get; set; }

        /// <summary>
        /// A coloured edge on the afflictions that can be taken off.
        /// <para>
        /// 🔴 The cleanse mark, brought down from the frame onto the single icon. The mark
        /// says this person has something removable; with four afflictions on them it does
        /// not say WHICH, and that is the question somebody is asking while they look
        /// (Florian, 2026-09-21: the debuffs are hard to tell apart).
        /// </para>
        /// <para>
        /// 🔴 It takes the CLEANSE COLOUR and has no picker of its own. The mark on the frame
        /// and the edge on the icon are one statement made twice — "this can be taken off" —
        /// and two colours for one statement is two things to keep in step by hand, plus a
        /// row on a group that is already long. Whoever wants it red sets the cleanse colour
        /// red and both follow (Florian asked about red, 2026-09-21).
        /// </para>
        /// </summary>
        public bool AuraDispelBorder { get; set; } = true;

        /// <summary>
        /// Its own thickness, though — three pixels around a twenty-pixel icon and three
        /// pixels around a whole frame are not the same weight at all.
        /// </summary>
        public float AuraDispelThickness { get; set; } = 2f;

        /// <summary>
        /// How much of an icon's height the numbers on it take — the seconds left, and the
        /// stack count, which keeps a fixed ratio below this one.
        /// <para>
        /// 🔴 There was deliberately no setting for this, on the reasoning that a number
        /// with a size of its own runs out of its icon the moment somebody moves the icon
        /// slider, while a SHARE of the icon never can. The reasoning was sound and the
        /// conclusion was still wrong: the share is of the font's em box, not of the icon,
        /// and how much of that box a digit actually fills is a property of the typeface.
        /// Axis leaves room above and below; a condensed face fills it to the edges, and the
        /// same 0.7 came out enormous the moment Florian switched to one (2026-09-21).
        /// <b>It was one of the four fonts the game itself ships and we already offer</b> —
        /// so this is not an edge case reached by loading something exotic, and on top of
        /// that we hand out a folder for the player's own fonts. No single share can be
        /// right for all of them, and a number that cannot be sized is one a player has to
        /// solve by giving up their font.
        /// </para>
        /// <para>
        /// One slider for both numbers rather than one each: they are two readings of the
        /// same typeface at the same place, and nobody wants the seconds large while the
        /// stack count stays small. It is <b>not</b> a pixel value, so the interface-scale
        /// migration must leave it alone.
        /// </para>
        /// </summary>
        public float AuraNumberSize { get; set; } = 0.7f;

        /// <summary>
        /// Point at an affliction and the game's own name and description for it come up.
        /// <para>
        /// 🔴 OFF by default, and it is the one setting here where the default is the opposite
        /// of the feature being good. The cursor is over these frames constantly and on
        /// purpose — click-to-target, mouseover healing — so a panel that opens on hover would
        /// open while somebody is healing through it, over the very frames they are reading.
        /// Whoever wants it is somebody learning what an effect does, and they will go and
        /// find the switch (Florian asked for it as an option, 2026-09-18).
        /// </para>
        /// <para>
        /// 🔴 One switch per row, where stacks and the sweep have one for the afflictions and
        /// one for both benefit rows. The two are different questions: how an icon is DRAWN
        /// is the same question for both benefit rows, but "what is this picture" is not —
        /// a stranger's affliction is the one nobody recognises, and your own regen is the
        /// one everybody does (Florian, 2026-09-21).
        /// </para>
        /// </summary>
        public bool ShowAuraTooltips { get; set; }

        /// <summary>The same, for the row of effects you put on somebody.</summary>
        public bool ShowBuffTooltips { get; set; }

        /// <summary>The same, for the row of effects somebody else put on them.</summary>
        public bool ShowOtherTooltips { get; set; }

        // --- benefits: a second row, in the other corner ------------------------
        // Its own row rather than a mix with the afflictions, because the two answer
        // different questions: what is wrong with this person, and what have I already put on
        // them. Mixed, a regen would push a debuff out of a full row.

        /// <summary>
        /// The row of benefits — a healer's own regens, mostly. On by default, and by default
        /// only the player's own.
        /// </summary>
        public bool ShowBuffs { get; set; } = true;

        /// <summary>
        /// Only what this player put there.
        /// <para>
        /// On, and this is the setting that makes the row worth having at all: in a full party
        /// somebody carries dozens of benefits, and the one a healer is looking for is the
        /// regen they cast themselves. Off, the row is a wall of food and raid buffs.
        /// </para>
        /// </summary>
        public bool OwnBuffsOnly { get; set; } = true;

        /// <summary>In pixels, and square.</summary>
        public float BuffSize { get; set; } = 18f;

        /// <summary>Index into the nine anchor points. The opposite corner to the afflictions.</summary>
        public int BuffPosition { get; set; } = (int)Hud.Anchor.BottomLeft;

        public float BuffX { get; set; }

        public float BuffY { get; set; }

        /// <summary>Three: a healer rarely has more than that of their own on one person.</summary>
        public int BuffMaxCount { get; set; } = 3;

        public bool BuffShowStacks { get; set; } = true;

        public bool BuffSwipe { get; set; } = true;

        /// <summary>
        /// The seconds left on a benefit icon. Shared by both benefit rows, like the two
        /// above it: how an icon is drawn is one question, and answering it twice would put
        /// six switches on the tab for a distinction nobody makes.
        /// </summary>
        public bool BuffShowDuration { get; set; }

        // --- everybody else's: the third row -------------------------------------
        // What is already keeping this person up without you — mitigation, somebody else's
        // regen, a shield. Its own row so it can never take a place from the row above it.
        //
        // 🔴 It is "not yours", and it stays that way — the question is SETTLED, not open
        // (measured 2026-09-18, five Gunbreaker defensives at once). The sheet cannot tell a
        // mitigation from any other benefit: ParamModifier reads -10 for a 15%, a 20% and a
        // 30% reduction alike, yet +10 for Camouflage and 0 for Superbolide, so it carries
        // neither the kind nor the amount. ParamEffect is 0 throughout and the Status sheet
        // has no defensive flag at all. Florian decided the same day NOT to filter this row
        // by hand: a list of ids would go stale at every job patch, and a row that filters
        // wrongly is worse than one that does not filter, because people believe it.

        /// <summary>
        /// Off by default. It is the busiest of the three and the least often needed, and a
        /// third block of icons on a frame is a real cost — it has to be asked for.
        /// </summary>
        public bool ShowOtherBuffs { get; set; }

        /// <summary>In pixels, and square.</summary>
        public float OtherSize { get; set; } = 18f;

        /// <summary>Index into the nine anchor points.</summary>
        public int OtherPosition { get; set; } = (int)Hud.Anchor.BottomRight;

        public float OtherX { get; set; }

        public float OtherY { get; set; }

        public int OtherMaxCount { get; set; } = 3;

        /// <summary>
        /// One of <see cref="Hud.FrameMarkStyle"/>. None out of the box.
        /// <para>
        /// 🔴 The marks are the loudest thing a frame can do — a border or a wash across the
        /// whole of it — and a newcomer meeting one has no way to know what it is telling
        /// them. Loud belongs to somebody who asked for it (Florian, 2026-09-19). The icons
        /// still say what is on a person either way; this is only about shouting it.
        /// </para>
        /// </summary>
        public int CleanseMark { get; set; } = (int)Hud.FrameMarkStyle.Border;

        /// <summary>
        /// Whether the cleanse mark appears at all. Off out of the box.
        /// <para>
        /// 🔴 The marks are the loudest thing a frame can do — a border or a wash across the
        /// whole of it — and somebody meeting one on their first login has no way to know
        /// what it is telling them. Loud belongs to whoever asked for it (Florian,
        /// 2026-09-19). The icons still say what is on a person; this is only about shouting.
        /// </para>
        /// <para>
        /// Separate from the shape above, so switching it off and on again gives back the
        /// shape that was chosen rather than the first one in the list.
        /// </para>
        /// </summary>
        public bool ShowCleanseMark { get; set; }

        /// <summary>
        /// Show the cleanse mark only while on a job that can actually cleanse.
        /// <para>
        /// On. The mark is an instruction, and an instruction to somebody who cannot carry it
        /// out is noise — a Dragoon does not need to know that the tank has something Esuna
        /// would take off. The icons keep showing the effect either way; this is only about
        /// the mark on the frame.
        /// </para>
        /// </summary>
        public bool CleanseOnlyWhenAble { get; set; } = true;

        /// <summary>
        /// How thick the cleanse edge is, in pixels.
        /// <para>
        /// Three, not the frame's own hairline. A one pixel edge in a colour the frame does
        /// not otherwise use was still missed at a glance, and the whole job of this mark is
        /// to be caught without looking for it (Florian, 2026-09-13).
        /// </para>
        /// </summary>
        public float CleanseThickness { get; set; } = 3f;

        /// <summary>
        /// The cleanse colour, packed the way ImGui packs one. Settable, because which colour
        /// carries against a blue, a green and a red bar is a matter of eyes and of monitor,
        /// and our own value was never pipetted.
        /// </summary>
        public uint CleanseColour { get; set; } = Style.Tokens.Col.Cleanse;

        /// <summary>
        /// How solid the cleanse fill is at its strongest. Was a constant until the full-frame
        /// wash arrived beside the band — a band that fades out at the top can afford to be
        /// strong where it starts, and a wash over the whole frame cannot, so the number had
        /// to stop being one number for everybody (Florian, 2026-09-18).
        /// </summary>
        public float CleanseOpacity { get; set; } = 0.55f;

        // --- the raise mark: the same marking, saying the opposite thing -------------------
        // Cleanse says "you have to do something". This says "somebody already is" — which is
        // why it is worth a mark of its own rather than a second meaning for the first one.

        /// <summary>One of <see cref="Hud.FrameMarkStyle"/>. None out of the box, with the
        /// cleanse mark and for the same reason.</summary>
        public int RaiseMark { get; set; } = (int)Hud.FrameMarkStyle.Border;

        /// <summary>Whether the raise mark appears, with the cleanse mark and for the same
        /// reason.</summary>
        public bool ShowRaiseMark { get; set; }

        /// <summary>
        /// A spring green, and deliberately NOT the healer role colour <c>#6EF54D</c>: the mark
        /// washes over a bar that may already be that exact green, and a mark you cannot see on
        /// the very job most likely to be raising is no mark. Placeholder in the sense that
        /// every colour here is — the picker is right beside it.
        /// </summary>
        public uint RaiseColour { get; set; } = Style.Tokens.Col.Raise;

        public float RaiseThickness { get; set; } = 3f;

        public float RaiseOpacity { get; set; } = 0.55f;

        // --- rescue: a raise on its way, and somebody who cannot be killed -------
        // Their own place on the frame rather than a slot in the icon row, because the row
        // is ranked and can drop things, and these two are exactly what must never be
        // dropped (Florian, 2026-09-13).

        /// <summary>
        /// The raise-is-coming mark. On: it changes what you would do next, which is a higher
        /// bar than most things on a frame clear.
        /// </summary>
        public bool ShowRaiseIcon { get; set; } = true;

        /// <summary>
        /// The cannot-be-killed mark.
        /// <para>
        /// A switch of its own rather than a share of the raise's (Florian, 2026-09-18). The
        /// two ride in the same place on the frame and never appear together, which is what
        /// made one switch look reasonable — but they are read by different people at
        /// different moments. A healer turns the raise mark on to see who somebody else has
        /// already picked up; the invulnerability mark answers "stop healing, this is not
        /// damage you can lose them to", and a group may well want one without the other.
        /// </para>
        /// </summary>
        public bool ShowInvulnIcon { get; set; } = true;

        /// <summary>
        /// What the two above used to be, kept only so a configuration written before version 9
        /// still has something to read into and the migration has something to read out of.
        /// Nothing draws from it.
        /// </summary>
        public bool ShowRescueIcon { get; set; } = true;

        /// <summary>In pixels, and square. Larger than the affliction icons on purpose.</summary>
        public float RescueIconSize { get; set; } = 24f;

        /// <summary>Index into the nine anchor points.</summary>
        public int RescueIconPosition { get; set; } = (int)Hud.Anchor.Centre;

        public float RescueIconX { get; set; }

        public float RescueIconY { get; set; }

        // --- layout: never copied between elements, it belongs to this one (CLAUDE.md §5.3) ---
        // Stated at scale 1.0 and put through the interface scale when drawn, like every other
        // measurement in the suite. Ranges and defaults come from the spec, §4.

        /// <summary>Where the block of frames starts, from the top left of the screen.</summary>
        public float PositionX { get; set; } = 100f;

        public float PositionY { get; set; } = 300f;

        /// <summary>
        /// 90-400, in steps of five. Wide enough for a name and a number at the default; the
        /// spec's 168 became 170 so that the default sits on a stop of its own slider.
        /// </summary>
        public float FrameWidth { get; set; } = 170f;

        /// <summary>18-150. The useful range is 30-70; the rest is there for small parties.</summary>
        public float FrameHeight { get; set; } = 38f;

        /// <summary>Between two frames. Never mixed with padding.</summary>
        public float Spacing { get; set; } = 4f;

        /// <summary>0 = vertical (the game's own shape), 1 = horizontal.</summary>
        public int Direction { get; set; }

        /// <summary>How many lines the frames break into: 1, 2 or 4.</summary>
        public int Lines { get; set; } = 1;

        /// <summary>
        /// Hides the game's own party list while WispUI's frames are on.
        /// <para>
        /// Off by default, and deliberately. Hiding a piece of somebody's game interface the
        /// first time a plugin loads is the kind of thing that gets found out rather than
        /// chosen, and the frames are worth looking at beside the list they replace before the
        /// list goes. It is one tick, both ways.
        /// </para>
        /// <para>
        /// The frames keep their order and their right-click menu from the list even while it
        /// is hidden — the game still keeps it, it simply is not drawn.
        /// </para>
        /// </summary>
        public bool HideNativePartyList { get; set; }

        /// <summary>
        /// Puts every number back inside the range it is allowed to hold.
        /// <para>
        /// 🔴 On the module rather than on the suite, because an import can bring one module
        /// on its own and a check that only exists for the whole thing would not run for it.
        /// </para>
        /// <para>
        /// Silent on purpose. A value out of range is not something the player did — it is a
        /// file edited by hand, a profile from somebody running a different version, or a
        /// field this build has never heard of. Telling them about it would be reporting our
        /// own housekeeping; putting it right and drawing something sensible is the answer.
        /// </para>
        /// </summary>
        internal void Sanitise()
        {
            this.BarOpacity = Bounded(this.BarOpacity, MinBarOpacity, 1f, 1f);
            this.ShieldOpacity = Bounded(this.ShieldOpacity, 0f, 1f, 0.8f);
            this.CleanseOpacity = Bounded(this.CleanseOpacity, 0f, 1f, 0.55f);
            this.RaiseOpacity = Bounded(this.RaiseOpacity, 0f, 1f, 0.55f);

            this.FrameWidth = Bounded(this.FrameWidth, MinFrameWidth, MaxFrameWidth, 170f);
            this.FrameHeight = Bounded(this.FrameHeight, MinFrameHeight, MaxFrameHeight, 38f);
            this.Spacing = Bounded(this.Spacing, 0f, MaxFrameSpacing, 4f);
            this.ManaHeight = Bounded(this.ManaHeight, MinManaHeight, MaxManaHeight, 3f);

            this.PositionX = Bounded(this.PositionX, -MaxPosition, MaxPosition, 100f);
            this.PositionY = Bounded(this.PositionY, -MaxPosition, MaxPosition, 300f);

            this.NameSize = Bounded(this.NameSize, MinTextSize, MaxTextSize, DefaultTextSize);
            this.HpTextSize = Bounded(this.HpTextSize, MinTextSize, MaxTextSize, DefaultTextSize);
            this.PartyNumberSize = Bounded(this.PartyNumberSize, MinTextSize, MaxTextSize, 19f);

            this.JobIconSize = Bounded(this.JobIconSize, MinIconSize, MaxIconSize, 20f);
            this.LeaderIconSize = Bounded(this.LeaderIconSize, MinIconSize, MaxIconSize, 16f);
            this.RescueIconSize = Bounded(this.RescueIconSize, MinIconSize, MaxIconSize, 24f);
            this.AuraSize = Bounded(this.AuraSize, MinIconSize, MaxIconSize, 20f);
            this.BuffSize = Bounded(this.BuffSize, MinIconSize, MaxIconSize, 18f);
            this.OtherSize = Bounded(this.OtherSize, MinIconSize, MaxIconSize, 18f);

            this.NameX = Offset(this.NameX, 22f);
            this.NameY = Offset(this.NameY, 0f);
            this.HpTextX = Offset(this.HpTextX, 0f);
            this.HpTextY = Offset(this.HpTextY, 0f);
            this.JobIconX = Offset(this.JobIconX, 0f);
            this.JobIconY = Offset(this.JobIconY, 0f);
            this.LeaderIconX = Offset(this.LeaderIconX, 0f);
            this.LeaderIconY = Offset(this.LeaderIconY, 0f);
            this.PartyNumberX = Offset(this.PartyNumberX, 0f);
            this.PartyNumberY = Offset(this.PartyNumberY, 0f);
            this.RescueIconX = Offset(this.RescueIconX, 0f);
            this.RescueIconY = Offset(this.RescueIconY, 0f);
            this.AuraX = Offset(this.AuraX, 0f);
            this.AuraY = Offset(this.AuraY, 0f);
            this.BuffX = Offset(this.BuffX, 0f);
            this.BuffY = Offset(this.BuffY, 0f);
            this.OtherX = Offset(this.OtherX, 0f);
            this.OtherY = Offset(this.OtherY, 0f);

            this.AuraDispelThickness = Bounded(this.AuraDispelThickness, 1f, MaxDispelThickness, 2f);
            this.AuraNumberSize = Bounded(this.AuraNumberSize, MinAuraNumberSize, MaxAuraNumberSize, 0.7f);

            this.AuraMaxCount = Math.Clamp(this.AuraMaxCount, 1, MaxAurasPerRow);
            this.BuffMaxCount = Math.Clamp(this.BuffMaxCount, 1, MaxAurasPerRow);
            this.OtherMaxCount = Math.Clamp(this.OtherMaxCount, 1, MaxAurasPerRow);

            // The ones standing in for a list. Anything the build does not know goes back to
            // the first entry rather than to whatever that number would have indexed.
            this.ColourMode = Known<Appearance.BarColourMode>(this.ColourMode);
            this.TextEdge = Known<Style.TextEdge>(this.TextEdge);
            this.ManaStyle = Known<Hud.PartyFrames.ManaStyle>(this.ManaStyle);
            this.NameShortening = Known<Hud.NameShortening>(this.NameShortening);
            this.HpTextMode = Known<Hud.HealthTextMode>(this.HpTextMode);
            this.JobIconStyle = Known<Data.JobIconStyle>(this.JobIconStyle);
            this.Direction = Known<Hud.PartyFrames.FrameDirection>(this.Direction);
            this.CleanseMark = Known<Hud.FrameMarkStyle>(this.CleanseMark);
            this.RaiseMark = Known<Hud.FrameMarkStyle>(this.RaiseMark);

            this.NamePosition = Known<Hud.Anchor>(this.NamePosition);
            this.HpTextPosition = Known<Hud.Anchor>(this.HpTextPosition);
            this.JobIconPosition = Known<Hud.Anchor>(this.JobIconPosition);
            this.LeaderIconPosition = Known<Hud.Anchor>(this.LeaderIconPosition);
            this.PartyNumberPosition = Known<Hud.Anchor>(this.PartyNumberPosition);
            this.RescueIconPosition = Known<Hud.Anchor>(this.RescueIconPosition);
            this.AuraPosition = Known<Hud.Anchor>(this.AuraPosition);
            this.BuffPosition = Known<Hud.Anchor>(this.BuffPosition);
            this.OtherPosition = Known<Hud.Anchor>(this.OtherPosition);

            // A count of lines, not a list: it has to be one the layout actually lays out.
            if (Array.IndexOf(Hud.PartyFrames.FrameLayout.LineChoices, this.Lines) < 0)
            {
                this.Lines = 1;
            }

            this.TextWeight = Math.Clamp(this.TextWeight, 0, 2);

            // A style is stored by name and resolved against a list, so an unknown one
            // already falls back on its own. What cannot be allowed through is null, which
            // would be a name nothing can even be compared against.
            this.BarStyleName ??= Data.BarStyles.DefaultName;
            this.ShieldStyleName ??= Data.BarStyles.ShieldDefaultName;
            this.FontName ??= Style.FontLibrary.DefaultName;
            this.Bindings ??= new BindingSet();
            this.Mouseover ??= new MouseoverSet();
        }

        /// <summary>
        /// The most icons a row will draw, asked of the snapshot that holds them rather than
        /// written down again. A number larger than the array behind it is the SlotCount trap
        /// from session 11, and it is worth exactly one reference to avoid.
        /// </summary>
        private const int MaxAurasPerRow = Hud.PartyFrames.PartySnapshot.MaxAuras;

        /// <summary>
        /// A number inside its range, or the default when it is not a number at all. The
        /// second case is the one that matters: NaN fails every comparison, so it slips
        /// through a clamp untouched and then quietly poisons every size computed from it.
        /// </summary>
        private static float Bounded(float value, float low, float high, float fallback) =>
            float.IsFinite(value) ? Math.Clamp(value, low, high) : fallback;

        private static float Offset(float value, float fallback) =>
            Bounded(value, -MaxTextOffset, MaxTextOffset, fallback);

        /// <summary>
        /// A stored number that stands for one of a list. Asked of the enum itself rather
        /// than counted here, so adding an entry never leaves a bound behind.
        /// </summary>
        private static int Known<TEnum>(int value)
            where TEnum : struct, Enum =>
            Enum.IsDefined((TEnum)(object)value) ? value : 0;
    }

    /// <summary>
    /// Puts every number in the whole suite back inside its range. See the module's own
    /// <see cref="PartyFramesConfig.Sanitise"/> for why this exists at all.
    /// </summary>
    internal void Sanitise()
    {
        this.Scale = float.IsFinite(this.Scale) ? Math.Clamp(this.Scale, MinScale, MaxScale) : 1f;
        this.PreviewCount = this.PreviewCount is 1 or 4 or 8 ? this.PreviewCount : 4;
        this.PartyFrames ??= new PartyFramesConfig();
        this.PartyFrames.Sanitise();
        this.Profiles ??= new ProfileSet();
        this.Profiles.Sanitise();
    }

    /// <summary>
    /// Writes what is on screen back into the profile it came from. Called before another
    /// profile is put on, and on the way out — otherwise everything since the last switch
    /// belongs to nobody.
    /// </summary>
    internal void StoreIntoActiveProfile()
    {
        Profile active = this.Profiles.Current;
        active.PartyFramesEnabled = this.PartyFramesEnabled;
        Profile.Copy(this.PartyFrames, active.PartyFrames);
    }

    /// <summary>
    /// Puts a profile on: its settings become the live ones. What was on screen is stored
    /// first, so switching never loses the last thing somebody changed.
    /// </summary>
    internal void UseProfile(int index)
    {
        if (index < 0 || index >= this.Profiles.Items.Count || index == this.Profiles.Active)
        {
            return;
        }

        this.StoreIntoActiveProfile();
        this.Profiles.Active = index;
        this.ApplyActiveProfile();
    }

    /// <summary>
    /// Makes the profile at <see cref="ProfileSet.Active"/> the live settings, WITHOUT
    /// storing what was there first.
    /// <para>
    /// 🔴 Its own method because of one case: the profile that was on has just been
    /// removed. Going through <see cref="UseProfile"/> there would store the removed
    /// profile's settings into whichever profile takes over — deleting one profile would
    /// quietly overwrite another, which is the worst thing a delete button can do.
    /// </para>
    /// </summary>
    internal void ApplyActiveProfile()
    {
        Profile now = this.Profiles.Current;
        this.PartyFramesEnabled = now.PartyFramesEnabled;
        Profile.Copy(now.PartyFrames, this.PartyFrames);
        this.MarkDirty();
    }

    internal static Configuration Load()
    {
        Configuration config = Services.PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        if (config.Version < CurrentVersion)
        {
            Migrate(config);
            config.Version = CurrentVersion;

            // Before the write, not after: a migrated file is written once here and then
            // not again until something changes, so a value put right afterwards would
            // stay wrong on disk for as long as nobody touched a setting.
            config.Sanitise();
            config.Write();
            return config;
        }

        if (config.Version > CurrentVersion)
        {
            // Written by a build newer than this one. There is no migrating backwards, and
            // the version is NOT corrected: saying it is version 11 when it holds whatever
            // 12 wrote would hide the problem from the step that will one day read it
            // properly. Sanitise below is what makes it safe to draw meanwhile — a field
            // this build has never heard of falls back rather than indexing something.
            Services.Log.Warning(
                "The saved configuration is version {Found}, newer than this build's {Known}. "
                + "Anything it does not recognise will fall back to a default.",
                config.Version,
                CurrentVersion);
        }

        // Whatever was in the file, whoever wrote it. Bounds used to live only in the
        // sliders, which held for as long as the sliders were the only way a number could
        // get in — and stopped holding the moment a profile could arrive from somebody else.
        config.Sanitise();

        return config;
    }

    /// <summary>
    /// Marks the configuration as changed. The write itself is debounced, so dragging a
    /// slider or clicking through an arrow selector never touches the disk per frame.
    /// </summary>
    /// <summary>
    /// Whether a change is still waiting to be written. Read by anything that should not act
    /// while the player is still moving a slider — the font handles above all, since building
    /// those is far more expensive than saving a file.
    /// </summary>
    internal bool HasPendingChanges => m_dirtySince != DateTime.MaxValue;

    internal void MarkDirty()
    {
        if (m_dirtySince == DateTime.MaxValue)
        {
            m_dirtySince = DateTime.UtcNow;
        }
    }

    /// <summary>Called once per framework tick. Writes only after the changes have gone quiet.</summary>
    internal void Tick()
    {
        if (m_dirtySince != DateTime.MaxValue && DateTime.UtcNow - m_dirtySince >= SaveDelay)
        {
            this.Write();
        }
    }

    /// <summary>Writes a pending change right now, ignoring the debounce. Used on unload.</summary>
    internal void FlushPending()
    {
        if (m_dirtySince != DateTime.MaxValue)
        {
            this.Write();
        }
    }

    /// <summary>Writes immediately, whether or not the debounce has elapsed.</summary>
    internal void Write()
    {
        m_dirtySince = DateTime.MaxValue;

        // 🔴 Every write, not only when a profile is switched. The live blocks are the truth
        // while the game runs and the profile is a copy, so a file written between those two
        // moments holds a profile that disagrees with the settings beside it — and the
        // disagreement only shows up later, as a switch away and back quietly undoing an
        // afternoon's work. The copy is a few dozen assignments after a second and a half of
        // quiet; being certain is worth more than that.
        this.StoreIntoActiveProfile();

        Services.PluginInterface.SavePluginConfig(this);
    }

    private static void Migrate(Configuration config)
    {
        // Steps go here one at a time and in order, each one leaving the configuration in the
        // shape the next step expects.
        if (config.Version < 2)
        {
            // The name used to sit on a list of five positions of its own. Text now hangs on
            // the nine anchors every element shares, so the five are mapped onto their new
            // neighbours rather than silently meaning something else.
            config.PartyFrames.NamePosition = config.PartyFrames.NamePosition switch
            {
                1 => (int)Hud.Anchor.Top,
                2 => (int)Hud.Anchor.Centre,
                3 => (int)Hud.Anchor.Bottom,
                4 => (int)Hud.Anchor.BottomLeft,
                _ => (int)Hud.Anchor.TopLeft,
            };
        }

        if (config.Version < 3)
        {
            // Text sizes were three named steps and are now a pixel count. The three are
            // mapped onto the sizes they actually were, so a frame keeps the look it had.
            config.PartyFrames.HpTextSize = config.PartyFrames.HpTextSize switch
            {
                0f => 13f,
                2f => 19f,
                _ => DefaultTextSize,
            };

            // "Nothing" left the list of what the figure can say and became the group's own
            // switch. Whoever had it off keeps it off, and the mode under it is the sensible
            // one to come back to rather than whatever index happened to sit at zero.
            config.PartyFrames.ShowHealthText = config.PartyFrames.HpTextMode != 0;
            config.PartyFrames.HpTextMode = config.PartyFrames.HpTextMode switch
            {
                1 => (int)Hud.HealthTextMode.Current,
                3 => (int)Hud.HealthTextMode.Deficit,
                _ => (int)Hud.HealthTextMode.Percent,
            };
        }

        if (config.Version < 4)
        {
            // Shortening was a yes or no and is now a choice of which half to keep. Whoever
            // had it on keeps exactly what they had: the surname cut to an initial.
            config.PartyFrames.NameShortening = config.PartyFrames.ShortenNames
                ? (int)Hud.NameShortening.Surname
                : (int)Hud.NameShortening.Full;
        }

        if (config.Version < 5)
        {
            // The face was a position in a fixed list of six. The list is no longer fixed —
            // it grows with whatever the player puts in their font folder — so the face is
            // stored by name, and a position now maps to the name it used to mean.
            config.PartyFrames.FontName = config.PartyFrames.Font switch
            {
                1 => "Miedinger",
                2 => "Trump Gothic",
                3 => "Jupiter",
                4 => "Figtree",
                5 => "DM Sans",
                _ => Style.FontLibrary.DefaultName,
            };
        }

        if (config.Version < 6)
        {
            // Selecting and the right-click menu were two switches and are now bindings. A
            // job with no entry answers to the defaults, which do exactly what the two
            // switches did when both were on — so only somebody who had turned one off needs
            // anything written down, and for them it is written down for every job at once.
            //
            // 🔴 This block used to leave the whole method with a `return` when there was
            // nothing to write. That was invisible while it was the last step and a trap the
            // moment anything followed it: every later migration would have been skipped for
            // exactly the people who had changed nothing. A step declines by doing nothing,
            // never by ending the chain.
            if (!config.PartyFrames.ClickToTarget || !config.PartyFrames.ContextMenu)
            {
                System.Collections.Generic.List<MouseBinding> kept = new();

                if (config.PartyFrames.ClickToTarget)
                {
                    kept.Add(new MouseBinding { Button = 0, Kind = BindingKind.Target });
                }

                if (config.PartyFrames.ContextMenu)
                {
                    kept.Add(new MouseBinding { Button = 1, Kind = BindingKind.ContextMenu });
                }

                foreach ((uint id, _) in Data.JobList.Order)
                {
                    config.PartyFrames.Bindings.ByJob[id] = Clone(kept);
                }
            }
        }

        if (config.Version < 7)
        {
            // The style was a position in the style list. The list is no longer fixed — two
            // painted styles joined it and the placeholder ones are on their way out — so the
            // style is stored by name, and a position maps to the name it used to mean.
            //
            // Read against the list as it was at version 6, NOT against the list as it is
            // now: the point of the step is that those two are no longer the same.
            config.PartyFrames.BarStyleName = config.PartyFrames.BarStyle switch
            {
                1 => "Gradient",
                2 => "Inverse",
                3 => "Glass",
                4 => "Split",
                5 => "Ridge",
                6 => "Ridge fine",
                7 => "Edge lit",
                8 => "Hollow",
                _ => "Flat",
            };
        }

        if (config.Version < 8)
        {
            // The style list was rebuilt on WispUI's own textures. Seven files carried over
            // from LumenUI went out — measured at under 10 % contrast once tinted, which is
            // to say invisible — and with them eight of the nine drawn placeholder shapes,
            // which were never a design decision in the first place.
            //
            // Read against the list as it was at version 7. Everything whose name survives
            // keeps it; everything else lands on the nearest thing that still exists, which
            // for most of the placeholders is the quiet default. Falling through to the
            // list's first entry would work — that is what IndexOf does for an unknown name —
            // but it would silently move somebody who had chosen a shaded bar onto a flat one.
            config.PartyFrames.BarStyleName = config.PartyFrames.BarStyleName switch
            {
                "Flat" => "Flat",
                "Hollow" => "Flat",

                // Both old "Smooth" entries were the carried-over gradients, and both of them
                // were nearly flat once tinted. Our own quiet gradient is what they were
                // trying to be.
                "Smooth" => "Smooth",
                "Smooth soft" => "Smooth",

                "Gradient" => "Gradient",
                "Inverse" => "Gradient",

                // The three that put a light edge or a step on the bar all land on the one
                // texture that still does that.
                "Glass" => "Bevel",
                "Split" => "Bevel",
                "Edge lit" => "Bevel",

                _ => Data.BarStyles.DefaultName,
            };

            // The shield's own style arrived with the bar's default, because at the time there
            // was nothing better to point it at. There is now: a pattern, which is the whole
            // reason the shield picks separately. Nobody chose "Smooth" here — it is the value
            // it was born with — so moving it is finishing the setting rather than overriding
            // a decision.
            if (config.PartyFrames.ShieldStyleName is "Smooth" or "Smooth soft")
            {
                config.PartyFrames.ShieldStyleName = Data.BarStyles.ShieldDefaultName;
            }
        }

        if (config.Version < 9)
        {
            // One switch covered the raise mark and the invulnerability mark together; they
            // are two now. Whoever had turned the pair off gets both off, which is the only
            // reading of the old value that keeps a frame looking the way it looked — the
            // alternative, defaulting both to on, would switch something back on for exactly
            // the person who went looking for the switch.
            config.PartyFrames.ShowRaiseIcon = config.PartyFrames.ShowRescueIcon;
            config.PartyFrames.ShowInvulnIcon = config.PartyFrames.ShowRescueIcon;
        }

        if (config.Version < 10)
        {
            // Mouseover casting was one switch for every action a job owns and is now a list
            // of spells. Nothing is written: the list starts empty on purpose (Florian,
            // 2026-09-19).
            //
            // 🔴 The tempting step is the one not taken here — filling the list with every
            // heal the job has, so that whoever had the switch on keeps what they had. It
            // would be the wrong reading of the old value. Turning the switch on was a bet
            // that redirecting everything was better than redirecting nothing, made when
            // those were the only two offers; it was never a statement about any particular
            // spell. Writing a dozen decisions somebody never made, into the one feature that
            // changes what a key press does, is worse than an empty list they fill in a
            // minute.
            //
            // Said out loud rather than done silently, because for that person the feature
            // has stopped working and the reason is not on screen anywhere.
            if (config.PartyFrames.MouseoverCasting)
            {
                Services.Log.Information(
                    "Mouseover casting is now chosen per spell. The old switch was on; the new list starts empty — "
                    + "pick the spells you want on the pointer under Party frames, Bindings.");
            }
        }

        if (config.Version < 11)
        {
            // The two marks had "None" in their list of shapes, which made turning one off a
            // search through a list of what it can look like. They have a switch of their own
            // now, and the shape list is only shapes (CLAUDE.md, version 3: whether something
            // is shown does not belong in the list of what it can show).
            //
            // None meant off, so that is what it becomes — and the shape goes back to the
            // default, so switching it on afterwards gives a mark rather than nothing. Any
            // other shape was somebody choosing to have the mark, switch on and shape kept.
            config.PartyFrames.ShowCleanseMark = config.PartyFrames.CleanseMark != (int)Hud.FrameMarkStyle.None;
            config.PartyFrames.ShowRaiseMark = config.PartyFrames.RaiseMark != (int)Hud.FrameMarkStyle.None;

            if (!config.PartyFrames.ShowCleanseMark)
            {
                config.PartyFrames.CleanseMark = (int)Hud.FrameMarkStyle.Border;
            }

            if (!config.PartyFrames.ShowRaiseMark)
            {
                config.PartyFrames.RaiseMark = (int)Hud.FrameMarkStyle.Border;
            }
        }

        if (config.Version < 12)
        {
            // Everything somebody already had becomes the profile that catches every job.
            // Nothing they see changes: the fallback holds exactly what was live, it is
            // what is on, and a player who never opens the Profile screen carries on with
            // one profile they were never asked about.
            config.Profiles ??= new ProfileSet();

            if (config.Profiles.Items.Count == 0)
            {
                config.Profiles.Items.Add(ProfileSet.NewFallback());
            }

            config.Profiles.Active = 0;
            config.StoreIntoActiveProfile();
        }

        if (config.Version < 13)
        {
            // 🔴 The interface scale used to reach the party frames as well as the window.
            // Every pixel a player set was multiplied on its way to the screen and divided
            // on its way back, so the two halves agreed with each other while disagreeing
            // with the slider label: "170 px" drew 212 wide at 125 %.
            //
            // The drawing is fixed (see Tokens.WorldPx). This converts what is stored, so
            // nobody's frames change size or move because of an update — the numbers now
            // mean what they always claimed to. At 100 % it does nothing at all, which is
            // every player who never touched the slider.
            float was = config.Scale;

            if (was > 0f && MathF.Abs(was - 1f) > 0.001f)
            {
                Rescale(config.PartyFrames, was);

                for (int i = 0; i < config.Profiles.Items.Count; i++)
                {
                    // Every profile, not only the one that is on. A profile nobody has
                    // switched to yet still holds pixels written under the old arithmetic.
                    Rescale(config.Profiles.Items[i].PartyFrames, was);
                }
            }
        }

        if (config.Version < 14)
        {
            // The tooltip was one switch for all three icon rows and is three now. Whoever
            // had it on keeps it on everywhere: the point of splitting it is that they can
            // now switch two OFF, not that we switch two off for them.
            SplitTooltips(config.PartyFrames);

            for (int i = 0; i < config.Profiles.Items.Count; i++)
            {
                SplitTooltips(config.Profiles.Items[i].PartyFrames);
            }
        }
    }

    private static void SplitTooltips(PartyFramesConfig cfg)
    {
        cfg.ShowBuffTooltips = cfg.ShowAuraTooltips;
        cfg.ShowOtherTooltips = cfg.ShowAuraTooltips;
    }

    /// <summary>
    /// Multiplies every stored pixel in one party frames block. Used once, by the migration
    /// to version 13; see there for why.
    /// <para>
    /// Opacities, colours, counts and anything naming a choice are left alone — only the
    /// values that were going through the scale on their way to the screen.
    /// </para>
    /// </summary>
    private static void Rescale(PartyFramesConfig cfg, float by)
    {
        cfg.PositionX *= by;
        cfg.PositionY *= by;
        cfg.FrameWidth *= by;
        cfg.FrameHeight *= by;
        cfg.Spacing *= by;
        cfg.ManaHeight *= by;

        cfg.NameSize *= by;
        cfg.NameX *= by;
        cfg.NameY *= by;
        cfg.HpTextSize *= by;
        cfg.HpTextX *= by;
        cfg.HpTextY *= by;
        cfg.PartyNumberSize *= by;
        cfg.PartyNumberX *= by;
        cfg.PartyNumberY *= by;

        cfg.JobIconSize *= by;
        cfg.JobIconX *= by;
        cfg.JobIconY *= by;
        cfg.LeaderIconSize *= by;
        cfg.LeaderIconX *= by;
        cfg.LeaderIconY *= by;
        cfg.RescueIconSize *= by;
        cfg.RescueIconX *= by;
        cfg.RescueIconY *= by;

        cfg.AuraSize *= by;
        cfg.AuraX *= by;
        cfg.AuraY *= by;
        cfg.BuffSize *= by;
        cfg.BuffX *= by;
        cfg.BuffY *= by;
        cfg.OtherSize *= by;
        cfg.OtherX *= by;
        cfg.OtherY *= by;

        cfg.AuraDispelThickness *= by;
        cfg.CleanseThickness *= by;
        cfg.RaiseThickness *= by;
    }

    /// <summary>A fresh copy per job, so editing one job's bindings never moves another's.</summary>
    private static System.Collections.Generic.List<MouseBinding> Clone(
        System.Collections.Generic.List<MouseBinding> source)
    {
        System.Collections.Generic.List<MouseBinding> copy = new(source.Count);

        for (int i = 0; i < source.Count; i++)
        {
            copy.Add(source[i].Clone());
        }

        return copy;
    }
}
