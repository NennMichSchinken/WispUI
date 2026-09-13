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
    public const int CurrentVersion = 6;

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

    public bool PartyFramesEnabled { get; set; } = true;

    public PartyFramesConfig PartyFrames { get; set; } = new();

    /// <summary>
    /// The party frames' own settings. Split the way the whole suite is split: what you see
    /// here is appearance, which the clipboard can carry between elements — size, position
    /// and growth direction will live beside it and are never copied.
    /// </summary>
    [Serializable]
    public sealed class PartyFramesConfig
    {
        /// <summary>Index into the bar style list.</summary>
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

        /// <summary>The master switch. The three role switches decide who it then applies to.</summary>
        public bool ShowMana { get; set; } = true;

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
        /// Send an action to whoever the mouse is over instead of to the selected target.
        /// <para>
        /// Off until asked for, and the only setting in the suite that deserves to be. The two
        /// above change what is shown or what is selected; this changes what a key press does,
        /// and nobody should find that out by surprise.
        /// </para>
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

        // --- everybody else's: the third row -------------------------------------
        // What is already keeping this person up without you — mitigation, somebody else's
        // regen, a shield. Its own row so it can never take a place from the row above it.
        //
        // ⚠️ It is "not yours", not "mitigation". Whether the game's own data can tell a
        // mitigation from any other benefit is an open question (/wisp status writes what the
        // sheet says). Calling this row mitigation before that is answered would be naming it
        // after something it does not know.

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

        /// <summary>One of <see cref="Hud.CleanseMark"/>.</summary>
        public int CleanseMark { get; set; } = (int)Hud.CleanseMark.Border;

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

        // --- rescue: a raise on its way, and somebody who cannot be killed -------
        // Their own place on the frame rather than a slot in the icon row, because the row
        // is ranked and can drop things, and these two are exactly what must never be
        // dropped (Florian, 2026-09-13).

        /// <summary>
        /// The raise-is-coming and cannot-be-killed marks. On: both change what you would do
        /// next, which is a higher bar than most things on a frame clear.
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
    }

    internal static Configuration Load()
    {
        Configuration config = Services.PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        if (config.Version < CurrentVersion)
        {
            Migrate(config);
            config.Version = CurrentVersion;
            config.Write();
        }

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
            if (config.PartyFrames.ClickToTarget && config.PartyFrames.ContextMenu)
            {
                return;
            }

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
