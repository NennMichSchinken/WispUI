using WispUI.Interface;

namespace WispUI.Data;

/// <summary>What a line of release notes is about, which decides the word in front of it.</summary>
internal enum NewsKind
{
    New = 0,
    Changed = 1,
    Fixed = 2,
}

/// <summary>
/// One line of a release's notes, and optionally where the setting behind it lives.
/// <para>
/// 🔴 A jump target is OPTIONAL and most fixes have none. A row that looks clickable and
/// lands nowhere reads as broken, so a row without a target draws no path, no chevron and
/// no hover — it is a sentence, and it says so.
/// </para>
/// </summary>
internal readonly struct NewsEntry
{
    public NewsEntry(NewsKind kind, string text, Screen screen = Screen.Global, int tab = -1, string? group = null)
    {
        this.Kind = kind;
        this.Text = text;
        this.Screen = screen;
        this.Tab = tab;
        this.Group = group;
    }

    public NewsKind Kind { get; }

    /// <summary>One plain sentence. No marketing, and never two.</summary>
    public string Text { get; }

    public Screen Screen { get; }

    /// <summary>Which tab of that screen, or -1 for a line that jumps nowhere.</summary>
    public int Tab { get; }

    /// <summary>
    /// The settings group to land on, as its own id — the same string the screen passes to
    /// <c>Chrome.BeginGroup</c>. Null lands on the tab and lights nothing up.
    /// </summary>
    public string? Group { get; }

    public bool Jumps => this.Tab >= 0;
}

/// <summary>One release, and the lines it brought.</summary>
internal readonly struct NewsRelease
{
    public NewsRelease(string version, string date, string summary, NewsEntry[] entries)
    {
        this.Version = version;
        this.Date = date;
        this.Summary = summary;
        this.Entries = entries;
    }

    /// <summary>The released tag, without a leading v.</summary>
    public string Version { get; }

    /// <summary>When it went out, as the player's calendar reads it.</summary>
    public string Date { get; }

    /// <summary>The one line the card in the navigation shows. Short — the card does not grow.</summary>
    public string Summary { get; }

    public NewsEntry[] Entries { get; }
}

/// <summary>
/// What is new, as the window shows it.
/// <para>
/// 🔴 WRITTEN BY HAND FOR EVERY RELEASE, and it is a permanent release step rather than a
/// one-off — see CLAUDE.md §2.1. Two files carry a release's notes and both need the new
/// version: this one for the window, and <c>CHANGELOG.md</c> for the published changelog
/// that goes with the submission. Missing one of them is not a build error, which is
/// exactly why it is written down here.
/// </para>
/// <para>
/// Rules that keep the screen honest, learned from the sister project:
/// </para>
/// <list type="bullet">
/// <item>Newest release first. Nothing sorts this; the order it is written in is the order
/// it is shown in.</item>
/// <item>One plain sentence per entry, in the past tense of what changed. A line that needs
/// two sentences is two entries, or it is a thing nobody needed to be told.</item>
/// <item>A jump target only where there is a setting behind the line. Bug fixes usually
/// carry none.</item>
/// <item>English, like every other string. The locale layer picks them up with the rest.</item>
/// </list>
/// </summary>
internal static class News
{
    /// <summary>Tab numbers on the party frames screen, named so a note is readable.</summary>
    private const int PartyBase = 0;
    private const int PartyText = 1;
    private const int PartyIcons = 2;
    private const int PartyLayout = 3;
    private const int PartyAuras = 4;
    private const int PartyMarks = 5;
    private const int PartyBindings = 6;

    public static readonly NewsRelease[] Releases =
    {
        new(
            "0.1.0",
            "2026-09-20",
            // 🔴 Two lines in the navigation card, and the card clips rather than grows.
            // Roughly sixty characters fit — past that a sentence loses its end, which
            // reads as a defect even though it is the intended cap.
            "Party frames, profiles per job, and a live preview.",
            new NewsEntry[]
            {
                new(
                    NewsKind.New,
                    "Party frames: real rectangles with a width, a height and a spacing you set, in place of the game's narrow bars.",
                    Screen.PartyFrames,
                    PartyLayout,
                    "##wisp-pf-size"),
                new(
                    NewsKind.New,
                    "Profiles: one set of settings per job or role, switched automatically and never during a fight.",
                    Screen.Profile,
                    0,
                    "##wisp-profile-list"),
                new(
                    NewsKind.New,
                    "A profile travels as one line of text, and pasting one asks what to take of it.",
                    Screen.Profile,
                    0,
                    "##wisp-profile-share"),
                new(
                    NewsKind.New,
                    "A preview band under the module header shows one, four or eight frames drawn by the real drawing code.",
                    Screen.PartyFrames,
                    PartyBase,
                    "##wisp-pf-health"),
                new(
                    NewsKind.New,
                    "Nine bar textures and five shield textures, all drawn for this plugin.",
                    Screen.PartyFrames,
                    PartyBase,
                    "##wisp-pf-health"),
                new(
                    NewsKind.New,
                    "Mouseover casting is a list of spells you pick per job, so a regen can follow the pointer while a raise does not.",
                    Screen.PartyFrames,
                    PartyBindings,
                    "##wisp-pf-mogroup"),
                new(
                    NewsKind.New,
                    "Cleanse and raise marks share four shapes and a fill strength each, and can be switched off separately.",
                    Screen.PartyFrames,
                    PartyMarks,
                    "##wisp-pf-cleansegroup"),
                new(
                    NewsKind.New,
                    "Three rows of effect icons: afflictions, what you put on somebody, and what everybody else did.",
                    Screen.PartyFrames,
                    PartyAuras,
                    "##wisp-pf-auras"),
                new(
                    NewsKind.New,
                    "The frames follow the game's own party order, so every sorting setting you already have keeps working.",
                    Screen.PartyFrames,
                    PartyLayout,
                    "##wisp-pf-arrange"),
                new(
                    NewsKind.New,
                    "Six faces to choose from, your own font folder, and three weights that work on all of them.",
                    Screen.PartyFrames,
                    PartyText,
                    "##wisp-pf-textstyle"),
                new(
                    NewsKind.Changed,
                    "The party is read straight out of the game's own structure now, which takes several hundred allocations a second out of the draw path."),
                new(
                    NewsKind.Fixed,
                    "Mouseover casting kept a pointer to a party member between frames; a member who left in between could take the game down with them."),
            }),
    };

    /// <summary>The release at the top of the list, which is the one the card talks about.</summary>
    public static NewsRelease Latest => Releases[0];

    /// <summary>
    /// Whether there is a release the player has not looked at. Compared by string rather
    /// than parsed: a version is a name here, and a name either matches or it does not.
    /// </summary>
    public static bool HasUnseen(string? seenVersion) =>
        Releases.Length > 0 && !string.Equals(seenVersion, Latest.Version, System.StringComparison.Ordinal);
}
