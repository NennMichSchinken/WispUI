namespace WispUI.Appearance;

/// <summary>
/// The copyable half of an element's configuration: how it looks, never where it is or how
/// big. That split is the whole architecture of the clipboard — there is no per-element
/// "which fields may I transfer?" logic anywhere, because layout is simply not in here.
/// <para>
/// Deliberately flat and free of runtime objects: only ids and values, no textures, no
/// delegates, no references to a live element. That costs nothing today and is what keeps
/// a shareable text code or a named style preset a later addition rather than a rewrite.
/// </para>
/// </summary>
internal sealed record AppearanceBlock
{
    /// <summary>Index into the bar style list. An id, not the style itself.</summary>
    public int BarStyle { get; init; }

    /// <summary>
    /// What decides a bar's colour — see <see cref="BarColourMode"/>. The colour that
    /// <see cref="BarColourMode.Fixed"/> uses is not here yet: it arrives together with the
    /// colour picker, in the module that needs it. A field nothing can set is dead weight.
    /// </summary>
    public int ColourMode { get; init; }

    public float BarOpacity { get; init; } = 1f;

    /// <summary>Index into the nine anchor points a text hangs on.</summary>
    public int NamePosition { get; init; }

    /// <summary>In pixels.</summary>
    public float NameSize { get; init; }

    public float NameX { get; init; }

    public float NameY { get; init; }

    public bool NameInJobColour { get; init; }

    /// <summary>Index into <see cref="Hud.NameShortening"/>.</summary>
    public int NameShortening { get; init; }

    // The health text, described the same way every text on an element is: what it says, how
    // big it is, which point it hangs on, and how far it is nudged from there. All of it is
    // appearance and travels together — where the figure sits on a frame is a look, not a
    // size, and an element that copies the look and leaves the figure behind copied nothing.

    /// <summary>Whether the figure shows at all.</summary>
    public bool ShowHealthText { get; init; }

    /// <summary>Index into the health text modes — see <see cref="Hud.HealthTextMode"/>.</summary>
    public int HpTextMode { get; init; }

    /// <summary>In pixels.</summary>
    public float HpTextSize { get; init; }

    /// <summary>Index into the nine anchor points.</summary>
    public int HpTextPosition { get; init; }

    public float HpTextX { get; init; }

    public float HpTextY { get; init; }

    // The job icon, described the same way: whether it shows, how big it is, which point it
    // hangs on, and how far it is nudged from there. Size is in here rather than out with the
    // layout because an icon's size is what it looks like, the way a text size is — the frame
    // it sits in is the thing whose size belongs to the element alone.

    public bool ShowJobIcon { get; init; }

    /// <summary>Index into <see cref="Data.JobIconStyle"/>.</summary>
    public int JobIconStyle { get; init; }

    /// <summary>In pixels, and square.</summary>
    public float JobIconSize { get; init; }

    /// <summary>Index into the nine anchor points.</summary>
    public int JobIconPosition { get; init; }

    public float JobIconX { get; init; }

    public float JobIconY { get; init; }

    public bool JobIconHideDps { get; init; }

    // The party number. It travels with the icon rather than with the text: it is a badge on
    // a frame, not something the frame says about the person on it.

    public bool ShowPartyNumber { get; init; }

    /// <summary>In pixels.</summary>
    public float PartyNumberSize { get; init; }

    /// <summary>Index into the nine anchor points.</summary>
    public int PartyNumberPosition { get; init; }

    public float PartyNumberX { get; init; }

    public float PartyNumberY { get; init; }

    public bool ShowLeaderIcon { get; init; }

    /// <summary>In pixels, and square.</summary>
    public float LeaderIconSize { get; init; }

    /// <summary>Index into the nine anchor points.</summary>
    public int LeaderIconPosition { get; init; }

    public float LeaderIconX { get; init; }

    public float LeaderIconY { get; init; }

    // --- afflictions and the rescue mark, carried with the other badges -------

    public bool ShowAuras { get; init; }

    public float AuraSize { get; init; }

    public int AuraPosition { get; init; }

    public float AuraX { get; init; }

    public float AuraY { get; init; }

    public int AuraMaxCount { get; init; }

    public bool AuraShowStacks { get; init; }

    public bool AuraSwipe { get; init; }

    public bool ShowRescueIcon { get; init; }

    public float RescueIconSize { get; init; }

    public int RescueIconPosition { get; init; }

    public float RescueIconX { get; init; }

    public float RescueIconY { get; init; }

    /// <summary>
    /// Carried with the colours rather than the icons: it is not a picture, it is what the
    /// frame's own edge or bar does — see <see cref="Hud.CleanseMark"/>.
    /// </summary>
    public int CleanseMark { get; init; }
}

/// <summary>
/// What decides the colour of a health bar. Role and job follow FFXIV's own convention:
/// familiarity beats having a scheme of our own.
/// </summary>
internal enum BarColourMode
{
    Role = 0,
    Job = 1,
    Fixed = 2,
}
