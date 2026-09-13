using WispUI.Core;

namespace WispUI.Data;

/// <summary>
/// What the game's own data says about one status effect. Four numbers, read once.
/// </summary>
internal struct StatusFacts
{
    /// <summary>The icon the game draws for it, or zero for a status that has none.</summary>
    public uint Icon;

    /// <summary>
    /// The game's own ranking for its party list. Used instead of a ranking of ours: when
    /// more effects are on somebody than there is room for, the game has already decided
    /// which ones matter, and inventing a second answer would only disagree with the list
    /// beside it. Higher comes first.
    /// </summary>
    public byte Priority;

    /// <summary>
    /// What kind of effect it is, in the game's own numbering: 1 is a benefit, 2 is an
    /// affliction. Anything else is neither and is left alone.
    /// </summary>
    public byte Category;

    /// <summary>
    /// Whether Esuna and its like can take it off.
    /// <para>
    /// 🔴 Straight out of the sheet, not a list somebody keeps up to date. Every curated id
    /// list in a plugin is a list that goes stale on the next patch, and this one does not
    /// have to exist.
    /// </para>
    /// </summary>
    public bool CanDispel;
}

/// <summary>
/// Status effect facts by status id, read from the game's sheet once and kept.
/// <para>
/// A flat array indexed by id rather than a dictionary: the frames ask this for every effect
/// on every member, every frame, and that path may not allocate or hash (CLAUDE.md §7.1,
/// §7.3). The sheet is a few thousand rows of four bytes, so the whole thing is a handful of
/// kilobytes held for the life of the plugin.
/// </para>
/// </summary>
internal static class StatusData
{
    /// <summary>
    /// Effects that make somebody unkillable for a few seconds.
    /// <para>
    /// 🔴 The one curated list here, and it is curated because the sheet has no field for it
    /// — "cannot die right now" is not something the game writes down. Kept deliberately
    /// short: the tank cooldowns that actually stop a healer reacting, not everything that
    /// reduces damage. A mitigation is not invulnerability, and a frame that says it is would
    /// be lying at the worst moment.
    /// </para>
    /// <para>
    /// ⚠️ This list WILL go stale — it is exactly the kind of list the rest of this file
    /// avoids. When a job gets a new one, it is added here and nowhere else.
    /// </para>
    /// <para>
    /// 🔴 NOT VERIFIED IN THE GAME. These ids are written from memory, and a wrong one is
    /// invisible — it simply never lights up, or lights up on the wrong effect. So
    /// <see cref="Prime"/> logs the name the game gives each of them at load, in the client's
    /// own language: the log says what this list actually resolved to, and anything that is
    /// not the cooldown named beside it comes out of the list.
    /// </para>
    /// </summary>
    private static readonly ushort[] Invulnerabilities =
    {
        82,   // Hallowed Ground — Paladin
        409,  // Living Dead — Dark Knight, the window before Walking Dead
        810,  // Living Dead, the other id the effect is seen under
        811,  // Walking Dead — the one that has to be healed through
        1302, // Holmgang — Warrior
        1836, // Superbolide — Gunbreaker
        3255, // Superbolide, the other id the effect is seen under
    };

    /// <summary>
    /// The Raise effect and the sickness that follows it — the two a healer reads as "somebody
    /// is already on this" and "do not expect much of them yet".
    /// <para>
    /// Two ids for the same thing: the effect is seen under both, and reading only the first
    /// misses whoever was picked up by the other. Confirmed against a published plugin's own
    /// handling of it — a status id is a fact about the game, not somebody's work.
    /// </para>
    /// </summary>
    public const uint Raise = 148u;

    public const uint RaiseAlt = 1140u;

    public const uint Weakness = 43u;

    public const uint BrinkOfDeath = 44u;

    /// <summary>Whether this effect means a raise has already landed on them.</summary>
    public static bool IsRaise(uint statusId) => statusId is Raise or RaiseAlt;

    /// <summary>
    /// The jobs that can take an effect off somebody, so the cleanse mark can be shown to the
    /// people it is an instruction for and nobody else.
    /// <para>
    /// Bard is on this list only from level 35, which is where it learns the ability — under
    /// that it is a job that looks like it can and cannot.
    /// </para>
    /// </summary>
    public static bool CanCleanse(uint jobId, int level) => jobId switch
    {
        6u => true,   // Conjurer
        24u => true,  // White Mage
        28u => true,  // Scholar
        33u => true,  // Astrologian
        40u => true,  // Sage
        36u => true,  // Blue Mage
        23u => level >= 35, // Bard
        _ => false,
    };

    /// <summary>
    /// The actions that put somebody back on their feet, so a raise already on its way can be
    /// shown before its effect exists.
    /// <para>
    /// 🔴 The gap this closes: the Raise <em>effect</em> only appears once the cast lands. The
    /// eight seconds before that are exactly when a second healer needs to know somebody is
    /// already on it, and reading the effect alone says nothing for all eight of them.
    /// </para>
    /// </summary>
    public static bool IsRaiseAction(uint actionId) => actionId switch
    {
        125u => true,   // Raise — Conjurer, White Mage
        173u => true,   // Resurrection — Arcanist, Summoner, Scholar
        3603u => true,  // Ascend — Astrologian
        24287u => true, // Egeiro — Sage
        18317u => true, // Angel Whisper — Blue Mage
        12996u => true, // Raise L — Eureka
        20730u => true, // Lost Arise — Bozja
        22345u => true, // Lost Sacrifice — Bozja
        _ => IsRaiseLimitBreak(actionId),
    };

    /// <summary>The healer limit breaks, which raise the whole party at once.</summary>
    public static bool IsRaiseLimitBreak(uint actionId) =>
        actionId is 208u or 4247u or 4248u or 24859u;

    private static StatusFacts[] s_facts = System.Array.Empty<StatusFacts>();

    private static bool[] s_invulnerable = System.Array.Empty<bool>();

    /// <summary>
    /// Reads the sheet. Called once while the plugin is loading, never from a draw path.
    /// <para>
    /// Failing here is not fatal: everything falls back to a status nobody knows anything
    /// about, which draws its own icon and nothing else. That is a worse party frame, not a
    /// broken one.
    /// </para>
    /// </summary>
    public static void Prime()
    {
        Lumina.Excel.ExcelSheet<Lumina.Excel.Sheets.Status>? sheet =
            Services.Data.GetExcelSheet<Lumina.Excel.Sheets.Status>();

        if (sheet is null)
        {
            return;
        }

        int size = (int)sheet.Count + 1;

        // Row ids are not contiguous and the count is not the highest id, so the highest id is
        // asked for rather than assumed. One pass, at load.
        foreach (Lumina.Excel.Sheets.Status row in sheet)
        {
            if (row.RowId >= size)
            {
                size = (int)row.RowId + 1;
            }
        }

        var facts = new StatusFacts[size];
        var invulnerable = new bool[size];

        foreach (Lumina.Excel.Sheets.Status row in sheet)
        {
            ref StatusFacts entry = ref facts[row.RowId];

            entry.Icon = row.Icon;
            entry.Priority = row.PartyListPriority;
            entry.Category = row.StatusCategory;
            entry.CanDispel = row.CanDispel;
        }

        for (int i = 0; i < Invulnerabilities.Length; i++)
        {
            ushort id = Invulnerabilities[i];

            if (id >= size)
            {
                Services.Log.Warning("Invulnerability {Id} is not a status the game knows.", id);
                continue;
            }

            invulnerable[id] = true;

            // Written out so the guesswork above can be checked rather than trusted. A wrong
            // id here is silent otherwise, and a frame that never says "unkillable" looks the
            // same as one that has nothing to say (session 8: a silent fallback reads as a
            // decision).
            Services.Log.Information(
                "Invulnerability {Id} is \"{Name}\".",
                id,
                sheet.GetRowOrDefault(id)?.Name.ExtractText() ?? "?");
        }

        s_facts = facts;
        s_invulnerable = invulnerable;
        BuildPreview(sheet);
    }

    /// <summary>
    /// A handful of real afflictions to stand in while somebody is placing the icons, half of
    /// them cleansable so both looks can be judged.
    /// <para>
    /// Taken out of the sheet rather than written down: any four afflictions with pictures
    /// will do, and picking them from the data means there is nothing here to go stale and no
    /// id guessed. They are highly ranked ones, so the preview looks like a frame in trouble
    /// rather than a frame with four odd things on it.
    /// </para>
    /// </summary>
    public static uint[] Preview { get; private set; } = System.Array.Empty<uint>();

    /// <summary>An invulnerability the preview can show, or zero if none resolved.</summary>
    public static uint PreviewInvulnerability { get; private set; }

    private static void BuildPreview(Lumina.Excel.ExcelSheet<Lumina.Excel.Sheets.Status> sheet)
    {
        var picked = new uint[4];
        int dispellable = 0;
        int plain = 0;

        foreach (Lumina.Excel.Sheets.Status row in sheet)
        {
            if (dispellable >= 2 && plain >= 2)
            {
                break;
            }

            if (row.StatusCategory != 2 || row.Icon == 0 || row.PartyListPriority == 0)
            {
                continue;
            }

            if (row.CanDispel)
            {
                if (dispellable < 2)
                {
                    picked[dispellable] = row.RowId;
                    dispellable++;
                }
            }
            else if (plain < 2)
            {
                picked[2 + plain] = row.RowId;
                plain++;
            }
        }

        Preview = picked;

        for (int i = 0; i < Invulnerabilities.Length && PreviewInvulnerability == 0; i++)
        {
            if (Of(Invulnerabilities[i]).Icon != 0)
            {
                PreviewInvulnerability = Invulnerabilities[i];
            }
        }
    }

    /// <summary>
    /// What is known about a status id. An id past the end of the sheet — which a patch can
    /// hand us before the data catches up — comes back empty rather than throwing.
    /// </summary>
    public static StatusFacts Of(uint statusId) =>
        statusId < (uint)s_facts.Length ? s_facts[statusId] : default;

    /// <summary>Whether this effect means the person cannot be killed while it lasts.</summary>
    public static bool IsInvulnerability(uint statusId) =>
        statusId < (uint)s_invulnerable.Length && s_invulnerable[statusId];

    /// <summary>An affliction, in the game's own numbering. Anything else is not one.</summary>
    public static bool IsAffliction(uint statusId) => Of(statusId).Category == 2;
}
