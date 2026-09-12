using System;
using System.Collections.Generic;

namespace WispUI.Core;

/// <summary>
/// Every mouse binding the party frames answer to, kept per job.
/// <para>
/// A job that has never been set up gets the two defaults — left selects, right opens the
/// game's menu — rather than nothing. Those two are what the frames did before bindings
/// existed, and a frame that stops selecting because somebody switched job would be a
/// regression dressed as a feature.
/// </para>
/// </summary>
[Serializable]
public sealed class BindingSet
{
    /// <summary>
    /// Bindings by ClassJob row id. A job with no entry has never been touched and answers to
    /// <see cref="Defaults"/>; a job with an empty list has been emptied on purpose and
    /// answers to nothing, which is a choice somebody is allowed to make.
    /// </summary>
    public Dictionary<uint, List<MouseBinding>> ByJob { get; set; } = new();

    /// <summary>
    /// What a job answers to before anybody has changed it. Left selects, right opens the
    /// game's menu — exactly what the frames did before this existed.
    /// </summary>
    public static List<MouseBinding> Defaults() => new()
    {
        new MouseBinding { Button = 0, Kind = BindingKind.Target },
        new MouseBinding { Button = 1, Kind = BindingKind.ContextMenu },
    };

    /// <summary>
    /// The bindings in force for a job, without creating an entry for it. Reading must not
    /// write: this is called from the draw path, and a lookup that fills the dictionary would
    /// grow the saved file every time somebody changed job.
    /// </summary>
    public List<MouseBinding> For(uint jobId) =>
        this.ByJob.TryGetValue(jobId, out List<MouseBinding>? list) ? list : DefaultList;

    /// <summary>
    /// The bindings for a job, creating the entry if this is the first change to it. Only
    /// called from the settings window.
    /// </summary>
    public List<MouseBinding> Edit(uint jobId)
    {
        if (!this.ByJob.TryGetValue(jobId, out List<MouseBinding>? list))
        {
            list = Defaults();
            this.ByJob[jobId] = list;
        }

        return list;
    }

    /// <summary>Throws away a job's bindings so it goes back to answering to the defaults.</summary>
    public void Reset(uint jobId) => this.ByJob.Remove(jobId);

    /// <summary>
    /// One shared instance of the defaults, handed out for reading only. Built once because
    /// <see cref="For"/> runs in the draw path and must not allocate.
    /// </summary>
    private static readonly List<MouseBinding> DefaultList = Defaults();
}
