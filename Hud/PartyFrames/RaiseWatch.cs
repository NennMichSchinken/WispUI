using Dalamud.Game.ClientState.Objects.Types;
using WispUI.Core;
using WispUI.Data;

namespace WispUI.Hud.PartyFrames;

/// <summary>
/// Who is currently having a raise cast on them, and how long until it lands.
/// <para>
/// 🔴 The Raise effect only exists once the cast has finished. The eight seconds before that
/// are precisely when a second healer needs to know somebody is already on it — reading the
/// effect alone says nothing for the whole time the answer matters, and two healers raise the
/// same body.
/// </para>
/// <para>
/// Run on the tick rather than in the draw path, and only while somebody is actually down.
/// Finding a caster means walking the object table, which builds an object per entry in
/// Dalamud — far too much for a draw path, and pointless when nobody is dead. A raise is an
/// eight second cast, so four looks a second is more than enough to catch one.
/// </para>
/// </summary>
internal sealed class RaiseWatch
{
    /// <summary>Four times a second. A raise takes eight, so nothing can slip through.</summary>
    private const double Interval = 0.25d;

    /// <summary>Room for one party's worth of people being picked up at once.</summary>
    private const int Capacity = PartySnapshot.Capacity;

    private readonly ulong[] m_targets = new ulong[Capacity];
    private readonly float[] m_remaining = new float[Capacity];

    private int m_count;
    private double m_next;

    /// <summary>
    /// Looks for raises in flight, if it is time and if there is anyone to look for.
    /// </summary>
    public void Tick(double now)
    {
        if (now < m_next)
        {
            return;
        }

        m_next = now + Interval;

        if (!AnyoneDown())
        {
            // Nothing to find, and the walk below is the expensive part. Everything recorded
            // is dropped: a raise that was in flight either landed or was interrupted, and
            // both of those are somebody who is no longer waiting for one.
            m_count = 0;
            return;
        }

        this.Scan();
    }

    /// <summary>
    /// How long until a raise lands on this person, or zero if nobody is casting one at them.
    /// Read from the collect step, which touches only these two small arrays.
    /// </summary>
    public float IncomingFor(uint entityId)
    {
        for (int i = 0; i < m_count; i++)
        {
            if (m_targets[i] == entityId)
            {
                return m_remaining[i];
            }
        }

        return 0f;
    }

    /// <summary>Whether anybody in the party is on the floor. Eight numbers, checked cheaply.</summary>
    private static bool AnyoneDown()
    {
        var party = Services.Party;

        for (int i = 0; i < party.Length; i++)
        {
            var member = party[i];

            if (member is not null && member.MaxHP > 0 && member.CurrentHP == 0)
            {
                return true;
            }
        }

        return false;
    }

    private void Scan()
    {
        m_count = 0;

        foreach (var obj in Services.Objects)
        {
            if (m_count >= Capacity)
            {
                break;
            }

            if (obj is not IBattleChara caster || !caster.IsCasting)
            {
                continue;
            }

            if (!StatusData.IsRaiseAction(caster.CastActionId))
            {
                continue;
            }

            float left = caster.TotalCastTime - caster.CurrentCastTime;

            if (left <= 0f)
            {
                continue;
            }

            // A healer limit break puts the whole party back up, so it is not aimed at anyone
            // in particular. Recorded against nothing, which leaves it out — a mark on every
            // frame at once says less than the cast bar already does.
            if (StatusData.IsRaiseLimitBreak(caster.CastActionId))
            {
                continue;
            }

            ulong target = caster.CastTargetObjectId;

            if (target == 0)
            {
                continue;
            }

            // Two healers on the same body: the sooner one is what matters, because that is
            // when the frame stops needing anybody.
            int at = this.Find(target);

            if (at >= 0)
            {
                if (left < m_remaining[at])
                {
                    m_remaining[at] = left;
                }

                continue;
            }

            m_targets[m_count] = target;
            m_remaining[m_count] = left;
            m_count++;
        }
    }

    private int Find(ulong target)
    {
        for (int i = 0; i < m_count; i++)
        {
            if (m_targets[i] == target)
            {
                return i;
            }
        }

        return -1;
    }
}
