using System;
using System.Collections.Generic;

namespace WispUI.Core;

/// <summary>
/// One action that goes to whoever the mouse is over instead of to the selected target.
/// <para>
/// A pair of an action and a switch, rather than a bare id, for the same reason a binding has
/// one: putting a spell aside for one fight is not the same as throwing it out of the list.
/// </para>
/// </summary>
[Serializable]
public sealed class MouseoverSpell
{
    /// <summary>The Action row id. Zero is a row that has lost its action and answers to nothing.</summary>
    public uint ActionId { get; set; }

    public bool Enabled { get; set; } = true;
}

/// <summary>
/// Which spells are cast on whoever the mouse is over, kept per job.
/// <para>
/// 🔴 This is a list and not a switch, and the difference is the whole point (Florian,
/// 2026-09-19). One switch for every action meant a choice about a heal was also a choice
/// about every other key on the bar; a list is the same question asked once per spell, which
/// is how the question actually comes up.
/// </para>
/// <para>
/// Unlike <see cref="BindingSet"/> there are no defaults. A job nobody has set up redirects
/// nothing, because redirecting an action is the one thing in the suite that changes what a
/// key press does and it may never happen by arriving.
/// </para>
/// </summary>
[Serializable]
public sealed class MouseoverSet
{
    /// <summary>
    /// Spells by ClassJob row id. A job with no entry and a job with an empty list mean the
    /// same thing here — nothing is redirected — so unlike the bindings there is nothing to
    /// tell apart.
    /// </summary>
    public Dictionary<uint, List<MouseoverSpell>> ByJob { get; set; } = new();

    /// <summary>
    /// The spells in force for a job, without creating an entry for it. Reading must not
    /// write: this is asked on every tick, and a lookup that filled the dictionary would grow
    /// the saved file every time somebody changed job.
    /// </summary>
    public List<MouseoverSpell> For(uint jobId) =>
        this.ByJob.TryGetValue(jobId, out List<MouseoverSpell>? list) ? list : EmptyList;

    /// <summary>
    /// A job's spells, creating the entry on the first change to it. Only called from the
    /// settings window.
    /// </summary>
    public List<MouseoverSpell> Edit(uint jobId)
    {
        if (!this.ByJob.TryGetValue(jobId, out List<MouseoverSpell>? list))
        {
            list = new List<MouseoverSpell>();
            this.ByJob[jobId] = list;
        }

        return list;
    }

    /// <summary>
    /// One shared empty list, handed out for reading only. Built once because
    /// <see cref="For"/> runs on every tick and must not allocate.
    /// </summary>
    private static readonly List<MouseoverSpell> EmptyList = new();
}
