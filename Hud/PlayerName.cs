using System;

namespace WispUI.Hud;

/// <summary>
/// How much of a player name is drawn. A character in this game has exactly two names, so
/// there are exactly three answers: both, or one of them cut to an initial.
/// <para>
/// Which half you keep is not a default anyone can pick for someone else. On a static
/// everybody goes by their first name; in a party finder group the surname is what tells two
/// Alisaies apart (Florian, 2026-09-12).
/// </para>
/// </summary>
internal enum NameShortening
{
    /// <summary>Both names, as the game gives them.</summary>
    Full = 0,

    /// <summary>The surname to an initial: "Fri D."</summary>
    Surname = 1,

    /// <summary>The first name to an initial: "F. Day"</summary>
    Forename = 2,
}

/// <summary>
/// The name as it goes on a frame. One place, so a second element with a name on it shortens
/// the same way.
/// </summary>
internal static class PlayerName
{
    /// <summary>The three, in the order the arrows walk them.</summary>
    public static readonly NameShortening[] All =
    {
        NameShortening.Full,
        NameShortening.Surname,
        NameShortening.Forename,
    };

    public static NameShortening At(int index) =>
        index >= 0 && index < All.Length ? All[index] : NameShortening.Full;

    /// <summary>
    /// Builds the drawn name. This ALLOCATES when it shortens, so no draw path may call it
    /// directly: the caller keeps the string and asks again only when the name or the mode
    /// has actually changed (CLAUDE.md §7.1).
    /// <para>
    /// A name without the two halves — a stand-in, or anything the game hands us that is not
    /// shaped like a character name — comes back whole. There is nothing to cut, and half a
    /// word is worse than a long one.
    /// </para>
    /// </summary>
    public static string Build(NameShortening mode, string name)
    {
        if (mode == NameShortening.Full || name.Length == 0)
        {
            return name;
        }

        int space = name.LastIndexOf(' ');
        if (space <= 0 || space >= name.Length - 1)
        {
            return name;
        }

        return mode == NameShortening.Surname
            ? string.Concat(name.AsSpan(0, space + 1), name.AsSpan(space + 1, 1), ".")
            : string.Concat(name.AsSpan(0, 1), ". ", name.AsSpan(space + 1));
    }
}
