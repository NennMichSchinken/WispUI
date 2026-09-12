using System;
using System.Collections.Generic;
using Lumina.Excel.Sheets;
using WispUI.Core;

namespace WispUI.Data;

/// <summary>One action a binding can be set to.</summary>
internal readonly struct ActionEntry
{
    public ActionEntry(uint id, string name, ushort icon, byte level)
    {
        this.Id = id;
        this.Name = name;
        this.Icon = icon;
        this.Level = level;
    }

    /// <summary>The Action row id, which is what is actually used when the binding fires.</summary>
    public uint Id { get; }

    public string Name { get; }

    /// <summary>The action's own icon, for the list to show.</summary>
    public ushort Icon { get; }

    /// <summary>The level it is learned at, which is how the list is ordered.</summary>
    public byte Level { get; }
}

/// <summary>
/// What a job can be bound to on a party frame, read from the game's own action sheet.
/// <para>
/// 🔴 The list filters itself, which is what makes this usable at all. A frame binding is
/// aimed at a party member, so only actions that can target one are offered — for a White Mage
/// that is on the order of fifteen entries, not four hundred, and for a Warrior it is nearly
/// empty, which is the honest answer rather than a bug.
/// </para>
/// <para>
/// Read once per job and kept, because a sheet read must never happen in a draw path
/// (CLAUDE.md §7.3). The cache is keyed by job and holds whatever jobs have been looked at —
/// in practice one or two, since a player sets up the job they are on.
/// </para>
/// </summary>
internal static class ActionList
{
    private static readonly Dictionary<uint, ActionEntry[]> Cache = new();
    private static readonly List<ActionEntry> Building = new();

    /// <summary>
    /// The actions this job can aim at a party member, weakest first. Built on the first ask
    /// for that job and kept afterwards.
    /// </summary>
    public static ActionEntry[] For(uint jobId)
    {
        if (Cache.TryGetValue(jobId, out ActionEntry[]? cached))
        {
            return cached;
        }

        ActionEntry[] built = Build(jobId);
        Cache[jobId] = built;
        return built;
    }

    private static ActionEntry[] Build(uint jobId)
    {
        Building.Clear();

        Lumina.Excel.ExcelSheet<Lumina.Excel.Sheets.Action>? sheet =
            Services.Data.GetExcelSheet<Lumina.Excel.Sheets.Action>();

        if (sheet is null)
        {
            return Array.Empty<ActionEntry>();
        }

        try
        {
            foreach (Lumina.Excel.Sheets.Action row in sheet)
            {
                if (!Fits(row, jobId))
                {
                    continue;
                }

                string name = row.Name.ExtractText();

                if (name.Length == 0)
                {
                    continue;
                }

                Building.Add(new ActionEntry(row.RowId, name, (ushort)row.Icon, row.ClassJobLevel));
            }
        }
        catch (Exception ex)
        {
            Services.Log.Error(ex, "The action list could not be read for job {Job}.", jobId);
            return Array.Empty<ActionEntry>();
        }

        // By level, which is the order they were learned in and therefore the order they sit
        // on a hotbar in most people's heads. Alphabetical would scatter a job's core three
        // across the whole list.
        Building.Sort(static (a, b) => a.Level != b.Level
            ? a.Level.CompareTo(b.Level)
            : string.CompareOrdinal(a.Name, b.Name));

        ActionEntry[] built = Building.ToArray();
        Services.Log.Information("Job {Job}: {Count} bindable actions.", jobId, built.Length);
        return built;
    }

    /// <summary>
    /// Whether this row is an action the given job can aim at a party member.
    /// <para>
    /// The three questions are the game's own fields, not a list of ours to keep current: is
    /// it this job's, can it be aimed at a party member or at yourself, and is it a real
    /// player action rather than a PvP variant or a row with no job at all.
    /// </para>
    /// </summary>
    private static bool Fits(Lumina.Excel.Sheets.Action row, uint jobId)
    {
        if (row.IsPvP || row.ClassJobLevel <= 0)
        {
            return false;
        }

        // Aimed at somebody in the party, or at yourself — your own frame is one of them.
        if (!row.CanTargetParty && !row.CanTargetSelf && !row.CanTargetAlly)
        {
            return false;
        }

        // The job's own actions, plus the role actions its role shares.
        uint owner = row.ClassJob.RowId;

        if (owner == jobId)
        {
            return true;
        }

        // A role action belongs to a category rather than to one job. The category is checked
        // by asking it about this job, which is the sheet's own answer and beats any mapping
        // we could write down.
        return row.IsRoleAction && InCategory(row, jobId);
    }

    private static bool InCategory(Lumina.Excel.Sheets.Action row, uint jobId)
    {
        try
        {
            ClassJobCategory category = row.ClassJobCategory.Value;
            return jobId < 43 && HasJob(category, jobId);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Whether a job category includes this job. The sheet holds one boolean column per job,
    /// named after the abbreviation, so it is asked by row rather than by name.
    /// </summary>
    private static bool HasJob(ClassJobCategory category, uint jobId) => jobId switch
    {
        19 => category.PLD,
        20 => category.MNK,
        21 => category.WAR,
        22 => category.DRG,
        23 => category.BRD,
        24 => category.WHM,
        25 => category.BLM,
        27 => category.SMN,
        28 => category.SCH,
        30 => category.NIN,
        31 => category.MCH,
        32 => category.DRK,
        33 => category.AST,
        34 => category.SAM,
        35 => category.RDM,
        36 => category.BLU,
        37 => category.GNB,
        38 => category.DNC,
        39 => category.RPR,
        40 => category.SGE,
        41 => category.VPR,
        42 => category.PCT,
        _ => false,
    };
}
