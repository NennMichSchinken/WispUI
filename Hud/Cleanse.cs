namespace WispUI.Hud;

/// <summary>
/// How a frame says that something on this person can be cleansed.
/// <para>
/// The one question a healer scans a party for, so it gets its own answer rather than being
/// left to whether the icons happen to be on and happen to be looked at. The icons say
/// <em>which</em> effect; this says <em>that there is one</em>, from across the screen.
/// </para>
/// </summary>
internal enum CleanseMark
{
    /// <summary>Nothing. The icons carry it alone, for somebody who wants a quiet frame.</summary>
    None = 0,

    /// <summary>
    /// The frame's edge takes the cleanse colour. The default: it is read in the corner of the
    /// eye, and the bar underneath keeps saying role and job, which is what is read first
    /// (Florian, 2026-09-13, picking it over colouring the bar).
    /// </summary>
    Border = 1,

    /// <summary>
    /// The health bar itself takes the colour. Impossible to miss, at the price of the one
    /// piece of colour a frame already had something to say with.
    /// </summary>
    Bar = 2,
}
