using System;
using System.Collections.Generic;
using Lumina.Excel.Sheets;
using WispUI.Core;

namespace WispUI.Data;

/// <summary>
/// The five groups a player thinks in when picking a job, which is one more than the three
/// roles a party has. Tanks and healers are roles; the rest split by how they are played, and
/// that is the order every job list in the game is written in.
/// </summary>
internal enum JobGroup
{
    Tank = 0,
    Healer = 1,
    Melee = 2,
    PhysicalRanged = 3,
    MagicalRanged = 4,
}

/// <summary>One job, as it appears in a list the player picks from.</summary>
internal readonly struct JobEntry
{
    public JobEntry(uint id, JobGroup group, string name)
    {
        this.Id = id;
        this.Group = group;
        this.Name = name;
    }

    /// <summary>The ClassJob row id, which is what everything else keys on.</summary>
    public uint Id { get; }

    public JobGroup Group { get; }

    /// <summary>The job's own name, read from the game so it is the player's own language.</summary>
    public string Name { get; }
}

/// <summary>
/// The combat jobs in the order a player expects to see them: tanks, healers, melee, physical
/// ranged, casters.
/// <para>
/// Built once at load. The names come out of the game's own sheet — a sheet read must never
/// happen in a draw path (CLAUDE.md §7.3), and the reward for reading it rather than writing
/// the names down is that a German client sees German job names.
/// </para>
/// <para>
/// Only jobs, never the classes they grow out of: nobody sets up party frame bindings for a
/// Gladiator. That also keeps the list at twenty-one, which is short enough to pick from.
/// </para>
/// </summary>
internal static class JobList
{
    private static readonly List<JobEntry> Entries = new();
    private static JobEntry[] s_all = Array.Empty<JobEntry>();

    /// <summary>The jobs, in display order. Empty until <see cref="Load"/> has run.</summary>
    public static JobEntry[] All => s_all;

    /// <summary>
    /// The order the list is written in, by ClassJob row id. Written out rather than sorted
    /// from the sheet, because the sheet's own order is neither this nor anything a player
    /// would recognise — and because within a group the order is the one the game uses, which
    /// is not alphabetical either.
    /// </summary>
    internal static readonly (uint Id, JobGroup Group)[] Order =
    {
        (19, JobGroup.Tank),           // Paladin
        (21, JobGroup.Tank),           // Warrior
        (32, JobGroup.Tank),           // Dark Knight
        (37, JobGroup.Tank),           // Gunbreaker

        (24, JobGroup.Healer),         // White Mage
        (28, JobGroup.Healer),         // Scholar
        (33, JobGroup.Healer),         // Astrologian
        (40, JobGroup.Healer),         // Sage

        (20, JobGroup.Melee),          // Monk
        (22, JobGroup.Melee),          // Dragoon
        (30, JobGroup.Melee),          // Ninja
        (34, JobGroup.Melee),          // Samurai
        (39, JobGroup.Melee),          // Reaper
        (41, JobGroup.Melee),          // Viper

        (23, JobGroup.PhysicalRanged), // Bard
        (31, JobGroup.PhysicalRanged), // Machinist
        (38, JobGroup.PhysicalRanged), // Dancer

        (25, JobGroup.MagicalRanged),  // Black Mage
        (27, JobGroup.MagicalRanged),  // Summoner
        (35, JobGroup.MagicalRanged),  // Red Mage
        (42, JobGroup.MagicalRanged),  // Pictomancer
        (36, JobGroup.MagicalRanged),  // Blue Mage
    };

    /// <summary>
    /// Reads the job names from the game. Called once at load, never from a draw path.
    /// </summary>
    public static void Load()
    {
        Entries.Clear();

        Lumina.Excel.ExcelSheet<ClassJob>? sheet = Services.Data.GetExcelSheet<ClassJob>();

        for (int i = 0; i < Order.Length; i++)
        {
            (uint id, JobGroup group) = Order[i];
            string name = Name(sheet, id);

            if (name.Length == 0)
            {
                // A job this client does not have — an older game version, or a row that moved.
                // Leaving it out is better than an entry nobody can read.
                continue;
            }

            Entries.Add(new JobEntry(id, group, name));
        }

        s_all = Entries.ToArray();
        Services.Log.Information("Job list built with {Count} jobs.", s_all.Length);
    }

    /// <summary>Where this job sits in the list, or -1 when it is not one we offer.</summary>
    public static int IndexOf(uint id)
    {
        for (int i = 0; i < s_all.Length; i++)
        {
            if (s_all[i].Id == id)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>How many jobs there are. Asked of the array, never written down beside it.</summary>
    public static int Count => s_all.Length;

    /// <summary>The job at this position, or the first one when the position is stale.</summary>
    public static JobEntry At(int index) =>
        index >= 0 && index < s_all.Length
            ? s_all[index]
            : (s_all.Length > 0 ? s_all[0] : default);

    private static string Name(Lumina.Excel.ExcelSheet<ClassJob>? sheet, uint id)
    {
        if (sheet is null)
        {
            return string.Empty;
        }

        try
        {
            ClassJob row = sheet.GetRow(id);
            string name = row.Name.ExtractText();

            // The sheet writes job names in lower case; the game shows them capitalised.
            return name.Length > 0 ? char.ToUpperInvariant(name[0]) + name[1..] : string.Empty;
        }
        catch (Exception ex)
        {
            Services.Log.Error(ex, "Job name could not be read for row {Id}.", id);
            return string.Empty;
        }
    }
}
