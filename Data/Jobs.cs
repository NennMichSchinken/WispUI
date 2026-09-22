using System;
using System.Collections.Generic;
using WispUI.Core;
using WispUI.Style;

namespace WispUI.Data;

/// <summary>
/// Which of the game's two job icon sets a frame wears. The same pictures either way — one
/// set carries the square frame around them, the other does not.
/// </summary>
internal enum JobIconStyle
{
    Framed = 0,
    Plain = 1,
}

/// <summary>What a job does in a party. Everything that is not one of the three is a job we
/// do not colour by role — a crafter, a gatherer, or a class before it takes its job.</summary>
internal enum JobRole
{
    None = 0,
    Tank,
    Healer,
    Dps,
}

/// <summary>
/// Job facts, looked up by <c>ClassJob</c> row id: what role it fills, what colour it wears,
/// and which icon belongs to it.
/// <para>
/// The colours are the palette FF Logs ranks with, taken from the published list at
/// <c>gist.github.com/stocky37/66936a6c3e82909c39f24204c7a4cb67</c> — the convention a raider
/// already has in their eye, and a set tuned for large filled areas on a near-black ground,
/// which is what a health bar is. Chosen over our own earlier set (HamMeter's) for exactly
/// that recognition; the two agree on eight jobs anyway, which is what says both descend from
/// the game's own job colours.
/// </para>
/// <para>
/// Three are not in that list, because it was written before Viper and Pictomancer and Blue
/// Mage is never ranked. The first two were picked off an FF Logs chart instead (Florian,
/// 2026-09-11) and so belong to the same palette; Blue Mage keeps our own value and is the
/// one job to look at once real bars are on screen.
/// </para>
/// <para>The icon rule comes from HamMeter, our own published plugin.</para>
/// <para>
/// Everything is a flat array indexed by row id, filled once at type load: a bar asks for a
/// colour every frame, and neither a dictionary lookup nor a sheet read belongs in that path.
/// </para>
/// </summary>
internal static class Jobs
{
    /// <summary>One past the highest ClassJob row id we know (PCT is 42).</summary>
    private const int Count = 44;

    /// <summary>
    /// Job icons run in sets of a hundred from 62000, and a job's icon is its row id added to
    /// the set. Verified against two independent plugins that resolve them the same way.
    /// <para>
    /// Which of the two sets is the plain one is written down but has not been looked at in
    /// the game — the first person to switch the option settles it.
    /// </para>
    /// </summary>
    private const int IconSetPlain = 62000;

    private const int IconSetFramed = 62100;

    /// <summary>The palette as shipped. Never written after the type loads.</summary>
    private static readonly uint[] Shipped = new uint[Count];

    /// <summary>What is drawn: the shipped palette with the player's own colours laid over it.</summary>
    private static readonly uint[] Colours = new uint[Count];

    private static readonly JobRole[] Roles = new JobRole[Count];

    /// <summary>Each class and the job it grows into, kept so a changed job colour reaches its class.</summary>
    private static readonly (int ClassId, int JobId)[] ClassOf =
    {
        (1, 19),  // Gladiator -> Paladin
        (3, 21),  // Marauder -> Warrior
        (6, 24),  // Conjurer -> White Mage
        (26, 28), // Arcanist -> Scholar
        (2, 20),  // Pugilist -> Monk
        (4, 22),  // Lancer -> Dragoon
        (29, 30), // Rogue -> Ninja
        (5, 23),  // Archer -> Bard
        (7, 25),  // Thaumaturge -> Black Mage
    };

    private static uint s_roleTank = Tokens.Col.RoleTank;
    private static uint s_roleHealer = Tokens.Col.RoleHealer;
    private static uint s_roleDps = Tokens.Col.RoleDps;

    static Jobs()
    {
        // Tanks
        Set(19, JobRole.Tank, 0xA8D2E6); // Paladin
        Set(21, JobRole.Tank, 0xCF2621); // Warrior
        Set(32, JobRole.Tank, 0xD126CC); // Dark Knight
        Set(37, JobRole.Tank, 0x998D50); // Gunbreaker

        // Healers
        Set(24, JobRole.Healer, 0xFFF0DC); // White Mage
        Set(28, JobRole.Healer, 0x8657FF); // Scholar
        Set(33, JobRole.Healer, 0xFFE74A); // Astrologian
        Set(40, JobRole.Healer, 0x80A0F0); // Sage

        // Melee
        Set(20, JobRole.Dps, 0xD69C00); // Monk
        Set(22, JobRole.Dps, 0x4164CD); // Dragoon
        Set(30, JobRole.Dps, 0xAF1964); // Ninja
        Set(34, JobRole.Dps, 0xE46D04); // Samurai
        Set(39, JobRole.Dps, 0x965A90); // Reaper
        Set(41, JobRole.Dps, 0x108210); // Viper — measured off an FF Logs chart, see the note above

        // Physical ranged
        Set(23, JobRole.Dps, 0x91BA5E); // Bard
        Set(31, JobRole.Dps, 0x6EE1D6); // Machinist
        Set(38, JobRole.Dps, 0xE2B0AF); // Dancer

        // Casters
        Set(25, JobRole.Dps, 0xA579D6); // Black Mage
        Set(27, JobRole.Dps, 0x2D9B78); // Summoner
        Set(35, JobRole.Dps, 0xE87B7B); // Red Mage
        Set(42, JobRole.Dps, 0xFC92E1); // Pictomancer — measured off an FF Logs chart
        Set(36, JobRole.Dps, 0x4B48FF); // Blue Mage — chosen by hand, never ranked, so nothing to take

        // Beastmaster is a limited job and is never ranked, so there is no FF Logs value to
        // take; this is the orange of its own job icon, picked by eye. ACCEPTED as it stands,
        // looked at on real frames on a full party of Beastmasters (Florian, 2026-09-13) —
        // the one colour in this table that is a judgement rather than a measurement, and it
        // has now had the only test that could settle it.
        Set(43, JobRole.Dps, 0xA8732B); // Beastmaster

        // The classes a job grows out of. They keep the job's colour and role, so a party
        // member below level 30 is not suddenly uncoloured.
        for (int i = 0; i < ClassOf.Length; i++)
        {
            Roles[ClassOf[i].ClassId] = Roles[ClassOf[i].JobId];
            Shipped[ClassOf[i].ClassId] = Shipped[ClassOf[i].JobId];
        }

        Array.Copy(Shipped, Colours, Count);
    }

    /// <summary>
    /// Lays the player's own colours over the shipped palette. Called on load and whenever one
    /// of them changes — never per frame: a bar reads the result out of a flat array.
    /// <para>
    /// Only what the player changed is stored, so a colour they never touched keeps following
    /// the shipped palette if that palette is ever corrected.
    /// </para>
    /// </summary>
    public static void Apply(IReadOnlyDictionary<uint, uint> jobs, IReadOnlyDictionary<int, uint> roles)
    {
        Array.Copy(Shipped, Colours, Count);

        foreach (KeyValuePair<uint, uint> pair in jobs)
        {
            if (IsColoured(pair.Key))
            {
                Colours[pair.Key] = pair.Value;
            }
        }

        // A class wears whatever its job wears now, not what the job wore when shipped.
        for (int i = 0; i < ClassOf.Length; i++)
        {
            Colours[ClassOf[i].ClassId] = Colours[ClassOf[i].JobId];
        }

        s_roleTank = roles.TryGetValue((int)JobRole.Tank, out uint tank) ? tank : Tokens.Col.RoleTank;
        s_roleHealer = roles.TryGetValue((int)JobRole.Healer, out uint healer) ? healer : Tokens.Col.RoleHealer;
        s_roleDps = roles.TryGetValue((int)JobRole.Dps, out uint dps) ? dps : Tokens.Col.RoleDps;
    }

    /// <summary>Whether this row id is a job with a colour of its own — what a stored colour may name.</summary>
    public static bool IsColoured(uint jobId) => jobId < Count && Shipped[jobId] != 0;

    /// <summary>The colour a job ships with, whatever the player has made of it.</summary>
    public static uint ShippedColour(uint jobId) => IsColoured(jobId) ? Shipped[jobId] : Tokens.Col.Ink;

    /// <summary>The colour a role ships with: the measured one in the tokens.</summary>
    public static uint ShippedRoleColour(JobRole role) => role switch
    {
        JobRole.Tank => Tokens.Col.RoleTank,
        JobRole.Healer => Tokens.Col.RoleHealer,
        JobRole.Dps => Tokens.Col.RoleDps,
        _ => Tokens.Col.InkDim,
    };

    /// <summary>The colour of a job, or the body text colour for anything we do not know.</summary>
    public static uint Colour(uint jobId) =>
        jobId < Count && Colours[jobId] != 0 ? Colours[jobId] : Tokens.Col.Ink;

    public static JobRole Role(uint jobId) => jobId < Count ? Roles[jobId] : JobRole.None;

    /// <summary>The colour of a role — the three a player reads before they read a name.</summary>
    public static uint RoleColour(JobRole role) => role switch
    {
        JobRole.Tank => s_roleTank,
        JobRole.Healer => s_roleHealer,
        JobRole.Dps => s_roleDps,
        _ => Tokens.Col.InkDim,
    };

    /// <summary>The job icon in the chosen set, or zero if we have no job.</summary>
    public static uint IconId(uint jobId, bool framed) =>
        jobId == 0 ? 0u : (uint)(framed ? IconSetFramed : IconSetPlain) + jobId;


    /// <summary>
    /// Job abbreviation to row id, for the one place a job arrives as text rather than as a
    /// number: the combat tracker, where the fight data names a job "WHM".
    /// <para>
    /// 🔴 Read out of the game's own <c>ClassJob</c> sheet rather than typed out. A typed list
    /// is correct until the next expansion adds a job, and then it is silently wrong for
    /// whoever plays that job first — the same reasoning that put JobBuffs on the sheet
    /// instead of on a list (session 12).
    /// </para>
    /// <para>
    /// ⚠️ Not yet checked against a non-English client. Job abbreviations are the same three
    /// letters in every language the game ships, which is why this is safe to derive; if one
    /// of them ever is not, a job simply shows the body text colour instead of its own.
    /// </para>
    /// <para>
    /// Built once, on demand, never from a draw path — a sheet read there is one of the things
    /// the performance rules forbid outright (§7.3).
    /// </para>
    /// </summary>
    private static Dictionary<string, uint>? s_byAbbreviation;

    /// <summary>The row id behind an abbreviation like "WHM", or zero for one we cannot place.</summary>
    public static uint FromAbbreviation(string? abbreviation)
    {
        if (string.IsNullOrEmpty(abbreviation))
        {
            return 0u;
        }

        if (s_byAbbreviation is null)
        {
            var built = new Dictionary<string, uint>(64, StringComparer.OrdinalIgnoreCase);

            var sheet = Services.Data.GetExcelSheet<Lumina.Excel.Sheets.ClassJob>();

            if (sheet is not null)
            {
                foreach (var row in sheet)
                {
                    string abbr = row.Abbreviation.ExtractText();

                    if (!string.IsNullOrEmpty(abbr))
                    {
                        built[abbr] = row.RowId;
                    }
                }
            }

            s_byAbbreviation = built;
        }

        return s_byAbbreviation.TryGetValue(abbreviation, out uint id) ? id : 0u;
    }

    private static void Set(int jobId, JobRole role, uint hex)
    {
        Roles[jobId] = role;
        Shipped[jobId] = 0xFF000000u | ((hex & 0x0000FFu) << 16) | (hex & 0x00FF00u) | ((hex & 0xFF0000u) >> 16);
    }
}
