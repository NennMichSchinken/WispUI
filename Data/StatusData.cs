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
    /// </summary>
    public const uint Raise = 148u;

    public const uint Weakness = 43u;

    public const uint BrinkOfDeath = 44u;

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
