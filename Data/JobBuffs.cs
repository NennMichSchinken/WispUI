using System;
using System.Collections.Generic;
using WispUI.Core;

namespace WispUI.Data;

/// <summary>
/// The effects a job leaves on the people it looks after, worked out from the game's own data
/// so the preview shows the frames somebody actually plays with.
/// <para>
/// 🔴 There is no field for this. Action carries StatusGainSelf, which is the effect an action
/// gives the <em>caster</em>; nothing in the sheets says which job puts which effect on a
/// party member. What is true is that the two are almost always called the same thing — Regen
/// the spell leaves Regen the effect, Medica II leaves Medica II, Aspected Benefic leaves
/// Aspected Benefic — so the job's own actions are looked up by name among the benefits
/// (<see cref="StatusData.NamedBenefit"/>).
/// </para>
/// <para>
/// That is a derivation rather than a list of ours, which is the whole point: nothing here
/// goes stale on a patch. Actions that leave nothing behind simply fail to match, and that
/// failure is doing useful work — Cure has no effect of its own and drops out, while every
/// heal over time and every shield stays. The result is close to the list somebody would have
/// written by hand, without anybody having to keep writing it.
/// </para>
/// <para>
/// ⚠️ It will not be exactly that list. A few effects are named differently from the action
/// that applies them, and a few names collide across jobs. For a preview — where the question
/// is how a row of icons sits on a frame — near enough is the right amount of effort, and the
/// alternative was four effects nobody recognises.
/// </para>
/// </summary>
internal static class JobBuffs
{
    private static readonly Dictionary<uint, uint[]> Cache = new();
    private static readonly List<uint> Building = new();

    /// <summary>
    /// The effects this job leaves on others, at most <paramref name="most"/> of them, built
    /// on the first ask and kept. Empty for a job that leaves nothing — a Dragoon, say.
    /// </summary>
    public static uint[] For(uint jobId, int most)
    {
        if (Cache.TryGetValue(jobId, out uint[]? known))
        {
            return known;
        }

        Building.Clear();
        ActionEntry[] actions = ActionList.For(jobId);

        for (int i = 0; i < actions.Length && Building.Count < most; i++)
        {
            uint status = StatusData.NamedBenefit(actions[i].Name);

            if (status == 0u || Building.Contains(status))
            {
                continue;
            }

            Building.Add(status);
        }

        uint[] built = Building.ToArray();
        Cache[jobId] = built;

        Services.Log.Debug(
            "Job {Job}: {Count} of {Actions} actions leave an effect that shares their name.",
            jobId,
            built.Length,
            actions.Length);

        return built;
    }
}
