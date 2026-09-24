using Dalamud.Game.ClientState.Conditions;

namespace WispUI.Core;

/// <summary>
/// Puts the right profile on when the job changes, and not a moment sooner than it should.
/// <para>
/// 🔴 Never during a fight (Florian, 2026-09-20). A job cannot be changed mid-fight, but the
/// switch can still come due in one: a profile edited to cover another job, a pull that
/// starts in the seconds after a change, or a zone change into a duty. Frames that rebuild
/// themselves while somebody is healing is the one thing this feature must never do, so a
/// switch that falls due in combat is remembered and made when the fight ends.
/// </para>
/// <para>
/// On the tick, and only ever looking at two numbers when nothing has happened. There is no
/// event for a job change that is cheaper than reading the one the player is on.
/// </para>
/// </summary>
internal sealed class ProfileWatch
{
    private readonly Configuration m_config;

    /// <summary>The job this last acted on, so an unchanged job costs one comparison.</summary>
    private uint m_job;

    /// <summary>
    /// A switch that fell due while a fight was on, or -1 for none. Held rather than
    /// dropped: the job is already the new one, so forgetting would leave the wrong profile
    /// on until the next change.
    /// </summary>
    private int m_waiting = -1;

    private bool m_wasInCombat;

    public ProfileWatch(Configuration config) => m_config = config;

    public void Tick()
    {
        uint job = Services.Objects.LocalPlayer?.ClassJob.RowId ?? 0u;

        // Logged out, loading, or between areas. Nothing is decided without a job, and the
        // remembered one is left alone so logging back in on the same job is not a change.
        if (job == 0u)
        {
            return;
        }

        bool inCombat = Services.Condition[ConditionFlag.InCombat];

        if (job != m_job)
        {
            m_job = job;
            this.Decide(job, inCombat);
        }
        else if (m_wasInCombat && !inCombat && m_waiting >= 0)
        {
            // The fight the switch was waiting on is over.
            this.Put(m_waiting);
            m_waiting = -1;
        }

        m_wasInCombat = inCombat;
    }

    /// <summary>
    /// Works out which profile this job wants and either puts it on or queues it. The
    /// fallback is a real answer here: a job no profile claims belongs to the profile that
    /// catches everything, and switching back to it is as much a switch as switching away.
    /// </summary>
    private void Decide(uint job, bool inCombat)
    {
        int wanted = m_config.Profiles.IndexFor(job);

        // Nobody claimed it. The fallback takes it — but only if the profile on screen was
        // itself put on automatically. Somebody who reached for a profile by hand meant it,
        // and having it taken away by a job change would be the plugin overruling them.
        if (wanted < 0)
        {
            wanted = m_config.Profiles.Current.Automatic ? 0 : m_config.Profiles.Active;
        }

        if (wanted == m_config.Profiles.Active)
        {
            m_waiting = -1;
            return;
        }

        if (inCombat)
        {
            m_waiting = wanted;
            return;
        }

        this.Put(wanted);
    }

    private void Put(int index)
    {
        if (index < 0 || index >= m_config.Profiles.Items.Count)
        {
            return;
        }

        m_config.UseProfile(index);
        Services.Log.Debug("Profile {Name} is on.", m_config.Profiles.Current.Name);
    }
}
