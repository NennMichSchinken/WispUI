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

    public bool NameInJobColour { get; init; }

    public bool ShortenNames { get; init; }

    // The health text, described the same way every text on an element is: what it says, how
    // big it is, which point it hangs on, and how far it is nudged from there. All of it is
    // appearance and travels together — where the figure sits on a frame is a look, not a
    // size, and an element that copies the look and leaves the figure behind copied nothing.

    /// <summary>Index into the health text modes — see <see cref="Hud.HealthTextMode"/>.</summary>
    public int HpTextMode { get; init; }

    /// <summary>Index into the three text sizes.</summary>
    public int HpTextSize { get; init; }

    /// <summary>Index into the nine anchor points.</summary>
    public int HpTextPosition { get; init; }

    public float HpTextX { get; init; }

    public float HpTextY { get; init; }
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
