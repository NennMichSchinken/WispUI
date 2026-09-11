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
    public const int CurrentVersion = 2;

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

        /// <summary>Index into the nine anchor points.</summary>
        public int NamePosition { get; set; } = (int)Hud.Anchor.Left;

        public bool NameInJobColour { get; set; }

        /// <summary>Cut a long name down rather than let it run out of the frame.</summary>
        public bool ShortenNames { get; set; }

        // --- health text --------------------------------------------------------
        // Every text on a frame is described the same way: whether it shows, how big it is,
        // which of the nine points it hangs on, and how far it is nudged from there. One
        // anatomy for all of them, which is also why there is no padding slider (spec §11.2).

        /// <summary>0 off, 1 the current figure, 2 a percentage, 3 what is missing.</summary>
        public int HpTextMode { get; set; } = 2;

        /// <summary>Index into the three text sizes. Axis is sharp at these and nowhere between.</summary>
        public int HpTextSize { get; set; } = 1;

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

        // --- layout: never copied between elements, it belongs to this one (CLAUDE.md §5.3) ---
        // Stated at scale 1.0 and put through the interface scale when drawn, like every other
        // measurement in the suite. Ranges and defaults come from the spec, §4.

        /// <summary>Where the block of frames starts, from the top left of the screen.</summary>
        public float PositionX { get; set; } = 100f;

        public float PositionY { get; set; } = 300f;

        /// <summary>90-400. Wide enough for a name and a number at the default.</summary>
        public float FrameWidth { get; set; } = 168f;

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
    }
}
