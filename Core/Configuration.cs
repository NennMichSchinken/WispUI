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
    public const int CurrentVersion = 4;

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
        // Both on by default. A unit frame that cannot be clicked is a picture of a unit
        // frame, and anybody who wanted a picture would not have turned the module on.

        /// <summary>Left-click a frame to select that member.</summary>
        public bool ClickToTarget { get; set; } = true;

        /// <summary>
        /// While the mouse is over a frame, tell the game that member is what it is pointing
        /// at. That is all it takes for the player's own mouseover macros and, with the game's
        /// own mouseover setting on, their hotbar to act on that member.
        /// </summary>
        public bool MouseoverTarget { get; set; } = true;

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
    }
}
