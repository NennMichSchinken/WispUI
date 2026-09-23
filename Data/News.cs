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
/// <item>🔴 <b>An entry says what the PLAYER can now do, never what we built.</b> No file
/// name, no "renderer", no "allocation", no "draw path". Somebody reads this screen once,
/// quickly, while wanting to get back into the game — a sentence they have to decode is a
/// sentence they skip, and then the release might as well not have said anything (Florian,
/// 2026-09-20). <c>Changed</c> and <c>Fixed</c> describe what used to be annoying, not what
/// was wrong in the code.</item>
/// <item>One plain sentence per entry. A line that needs two sentences is two entries, or
/// it is a thing nobody needed to be told.</item>
/// <item>A jump target only where there is a setting behind the line. Bug fixes usually
/// carry none.</item>
/// <item>English, like every other string. The locale layer picks them up with the rest.</item>
/// </list>
/// </summary>
internal static class News
{
    /// <summary>Tab numbers on the party frames screen, named so a note is readable.</summary>
    private const int GlobalColours = 1;

    private const int TrackerBase = 0;

    private const int PartyBase = 0;
    private const int PartyText = 1; // No note leads here yet; the tab numbers are kept whole so one can.
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
                    "Party frames you can size yourself, instead of the game's thin bars.",
                    Screen.PartyFrames,
                    PartyLayout,
                    "##wisp-pf-size"),
                new(
                    NewsKind.New,
                    "Different settings for every job, switched over when you change job.",
                    Screen.Profile,
                    0,
                    "##wisp-profile-list"),
                new(
                    NewsKind.New,
                    "Send your whole setup to a friend as one line of text.",
                    Screen.Profile,
                    0,
                    "##wisp-profile-share"),
                new(
                    NewsKind.New,
                    "See your frames while you change them, without needing a party.",
                    Screen.PartyFrames,
                    PartyBase,
                    "##wisp-pf-health"),
                new(
                    NewsKind.New,
                    "A damage meter in the same look, fed by IINACT.",
                    Screen.CombatTracker,
                    TrackerBase,
                    "##wisp-ct-bars"),
                new(
                    NewsKind.New,
                    "Pick your own colour for every job and role.",
                    Screen.Global,
                    GlobalColours,
                    "##wisp-global-roles"),
                new(
                    NewsKind.New,
                    "Nine looks for the health bar and five for shields.",
                    Screen.PartyFrames,
                    PartyBase,
                    "##wisp-pf-health"),
                new(
                    NewsKind.New,
                    "Choose which spells go to whoever you point at, one spell at a time.",
                    Screen.PartyFrames,
                    PartyBindings,
                    "##wisp-pf-mogroup"),
                new(
                    NewsKind.New,
                    "A mark on anyone who needs cleansing, and on anyone being raised.",
                    Screen.PartyFrames,
                    PartyMarks,
                    "##wisp-pf-cleansegroup"),
                new(
                    NewsKind.New,
                    "Effect icons in three rows: debuffs, yours, and everyone else's.",
                    Screen.PartyFrames,
                    PartyAuras,
                    "##wisp-pf-auras"),
                new(
                    NewsKind.New,
                    "The frames use the party order you already set in the game.",
                    Screen.PartyFrames,
                    PartyLayout,
                    "##wisp-pf-arrange"),
                // 🔴 This line pointed at a group on the party frames until the lettering
                // moved to Global, and then it pointed at nothing — the jump just did
                // nothing, silently, because a target that is never drawn is
                // indistinguishable from one that has not been drawn YET (the wait has a
                // frame budget and then gives up). Caught by reading the notes before a
                // pull request, not by the build. **Moving a group means checking whether
                // a note leads to it.**
                new(
                    NewsKind.New,
                    "Six fonts, three weights, and a folder for your own.",
                    Screen.Global,
                    -1,
                    "##wisp-global-lettering"),
                new(
                    NewsKind.Changed,
                    "The frames are lighter on your frame rate."),
                new(
                    NewsKind.Fixed,
                    "A rare crash when somebody left the party while you pointed at them."),
            }),
    };

    /// <summary>
    /// Sorts every release's lines so that, inside one kind, the lines about the same
    /// module and tab stand together.
    /// <para>
    /// 🔴 Once, at load, never per frame — and by sorting rather than by asking whoever
    /// writes the notes to keep them in order. A note is written at release time by
    /// somebody thinking about the release, not about the sort order, and a rule nobody
    /// is reminded of is a rule that lasts one release (Florian, 2026-09-20, wanting all
    /// the party frame lines together).
    /// </para>
    /// <para>
    /// Written out rather than handed to Array.Sort: that one is not stable, so two lines
    /// about the same tab would swap places for no reason anybody could see. At a dozen
    /// entries an insertion sort is both shorter and correct.
    /// </para>
    /// </summary>
    static News()
    {
        for (int r = 0; r < Releases.Length; r++)
        {
            NewsEntry[] entries = Releases[r].Entries;

            for (int i = 1; i < entries.Length; i++)
            {
                NewsEntry moving = entries[i];
                int at = i;

                while (at > 0 && Before(moving, entries[at - 1]))
                {
                    entries[at] = entries[at - 1];
                    at--;
                }

                entries[at] = moving;
            }
        }
    }

    /// <summary>
    /// Whether one line sorts ahead of another. Kind first, because that is the heading it
    /// lands under; then the module, then the tab. Equal on all three means neither is
    /// before the other, and the order they were written in stands.
    /// </summary>
    private static bool Before(NewsEntry a, NewsEntry b)
    {
        if (a.Kind != b.Kind)
        {
            return a.Kind < b.Kind;
        }

        // A line that goes nowhere sorts last within its kind: it has no module to stand
        // with, and leading with one would break up the runs that have.
        if (a.Jumps != b.Jumps)
        {
            return a.Jumps;
        }

        // By the screen's own order, which is the order of the navigation rail. A reader who
        // has the window open already knows that order, and a second one invented here would
        // only be a second thing to learn.
        if (a.Screen != b.Screen)
        {
            return a.Screen < b.Screen;
        }

        return a.Tab < b.Tab;
    }

    /// <summary>The release at the top of the list, which is the one the card talks about.</summary>
    public static NewsRelease Latest => Releases[0];

    /// <summary>
    /// Whether there is a release the player has not looked at. Compared by string rather
    /// than parsed: a version is a name here, and a name either matches or it does not.
    /// </summary>
    public static bool HasUnseen(string? seenVersion) =>
        Releases.Length > 0 && !string.Equals(seenVersion, Latest.Version, System.StringComparison.Ordinal);
}
