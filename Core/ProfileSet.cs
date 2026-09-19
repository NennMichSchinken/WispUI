using System;
using System.Collections.Generic;

namespace WispUI.Core;

/// <summary>
/// Every profile, which one is on, and the rule that picks one for a job.
/// <para>
/// 🔴 The live configuration is NOT one of the entries in this list. The module blocks under
/// <see cref="Configuration"/> stay where they are and go on being what everything draws
/// from; a profile is written into them when it is put on, and read back out of them when
/// something is about to replace it. The alternative — every reader going through the
/// active profile — would mean a null check in the draw path for a case that cannot happen,
/// and would put a lookup between the renderer and its own settings.
/// </para>
/// </summary>
[Serializable]
public sealed class ProfileSet
{
    /// <summary>
    /// The profiles, in the order the list shows them. The first is the fallback and cannot
    /// be removed — a job nothing claims still has to land somewhere, and "no profile" is a
    /// state that would have to be explained on screen.
    /// </summary>
    public List<Profile> Items { get; set; } = new();

    /// <summary>
    /// Which one is on. An index into <see cref="Items"/>; out of range means the fallback,
    /// which is what makes a hand-edited file harmless here.
    /// </summary>
    public int Active { get; set; }

    /// <summary>The most profiles somebody may keep. Far past any real setup.</summary>
    public const int MaxProfiles = 20;

    /// <summary>The fallback, made on the spot if the list has somehow been emptied.</summary>
    public Profile Fallback
    {
        get
        {
            if (this.Items.Count == 0)
            {
                this.Items.Add(NewFallback());
            }

            return this.Items[0];
        }
    }

    public Profile Current
    {
        get
        {
            Profile fallback = this.Fallback;
            return this.Active > 0 && this.Active < this.Items.Count ? this.Items[this.Active] : fallback;
        }
    }

    /// <summary>
    /// Which profile belongs to this job, or -1 when none does and the fallback stands.
    /// <para>
    /// The most specific claim wins: a profile that names the job beats one that names its
    /// role. Two profiles claiming a job the same way is a setup somebody built and the
    /// earlier one holds, because a list has an order and the top of it is where the eye
    /// goes first.
    /// </para>
    /// <para>
    /// A profile with automatic switching off is skipped entirely. It can still be put on by
    /// hand; it just never reaches for the screen on its own.
    /// </para>
    /// </summary>
    public int IndexFor(uint jobId)
    {
        int best = -1;
        int bestRank = 0;

        for (int i = 1; i < this.Items.Count; i++)
        {
            Profile profile = this.Items[i];

            if (!profile.Automatic || !profile.Covers(jobId))
            {
                continue;
            }

            int rank = profile.Specificity;

            if (rank > bestRank)
            {
                best = i;
                bestRank = rank;
            }
        }

        return best;
    }

    /// <summary>A name nothing else in the list is using, built from the one asked for.</summary>
    public string FreeName(string wanted)
    {
        if (!this.Taken(wanted))
        {
            return wanted;
        }

        // Two is where a copy starts, because the first one is the original.
        for (int n = 2; n < 100; n++)
        {
            string tried = wanted + " " + n.ToString(System.Globalization.CultureInfo.InvariantCulture);

            if (!this.Taken(tried))
            {
                return tried;
            }
        }

        return wanted;
    }

    private bool Taken(string name)
    {
        for (int i = 0; i < this.Items.Count; i++)
        {
            if (string.Equals(this.Items[i].Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Puts the list back into a shape the screen can draw. Runs on load and after an
    /// import, for the same reason every other Sanitise does.
    /// </summary>
    internal void Sanitise()
    {
        this.Items ??= new List<Profile>();

        if (this.Items.Count == 0)
        {
            this.Items.Add(NewFallback());
        }

        // Whatever the first entry claimed to be, it is the fallback: the list is ordered
        // and position zero is what "nothing else answered" means. A file that says
        // otherwise would leave every unclaimed job pointing at a profile meant for one.
        this.Items[0].ScopeKind = (int)ProfileScopeKind.Fallback;
        this.Items[0].Automatic = true;

        if (this.Items.Count > MaxProfiles)
        {
            this.Items.RemoveRange(MaxProfiles, this.Items.Count - MaxProfiles);
        }

        for (int i = 0; i < this.Items.Count; i++)
        {
            this.Items[i] ??= NewFallback();
            this.Items[i].Sanitise();
        }

        // Past the end or negative both mean the fallback, which is also what an empty
        // Active field deserialises to.
        if (this.Active < 0 || this.Active >= this.Items.Count)
        {
            this.Active = 0;
        }
    }

    internal static Profile NewFallback() => new()
    {
        Name = Localization.Strings.ProfileDefaultName,
        ScopeKind = (int)ProfileScopeKind.Fallback,
    };
}
