using Dalamud.Game.ClientState.Objects.Types;
using WispUI.Core;

namespace WispUI.Hud.PartyFrames;

/// <summary>
/// How far away each party member is, as the game itself counts it.
/// <para>
/// 🔴 Not worked out from two positions. Three attempts went into that and none of them ever
/// dimmed anybody: the position on a party member is not filled in for somebody the game has
/// not loaded, which is exactly the case the measurement was for (Florian, 2026-09-13, with
/// screenshots of a party none of whom could be targeted, all at full brightness).
/// </para>
/// <para>
/// The game keeps a distance on every object it has loaded, in yalms, and updates it itself.
/// Somebody too far to be loaded has no object at all — so "no object" IS the answer, and the
/// two cases that were being handled separately, out of range and in another zone, become one
/// thing asked one way.
/// </para>
/// <para>
/// On the tick at four looks a second, because finding the object walks the object table.
/// People do not cross thirty yalms in a quarter of a second.
/// </para>
/// </summary>
internal sealed class RangeWatch
{
    private const double Interval = 0.25d;

    private const int Capacity = PartySnapshot.Capacity;

    /// <summary>What the game reports for somebody it cannot see at all.</summary>
    public const byte Unreachable = byte.MaxValue;

    private readonly ulong[] m_who = new ulong[Capacity];
    private readonly byte[] m_distance = new byte[Capacity];

    private int m_count;
    private double m_next;

    /// <summary>Measures the party, if it is time.</summary>
    public void Tick(double now)
    {
        if (now < m_next)
        {
            return;
        }

        m_next = now + Interval;
        m_count = 0;

        var party = Services.Party;

        for (int i = 0; i < party.Length && m_count < Capacity; i++)
        {
            var member = party[i];

            if (member is null)
            {
                continue;
            }

            ulong who = member.ContentId != 0 ? member.ContentId : member.EntityId;

            if (who == 0)
            {
                continue;
            }

            // ⚠️ This searches the object table, which is why it is here and not in a frame.
            // A null object is not a failure — it is the game saying it has not loaded this
            // person, which is the whole answer we came for.
            //
            // 🔴 Asked by ENTITY id, not through IPartyMember.GameObject. That property hands
            // the member's entity id to SearchById, which compares it against each object's
            // GameObjectId — a different field. SearchByEntityId compares the field the
            // number actually came from. A lookup that answers "not loaded" for somebody
            // standing next to you makes the whole party look out of range, which is what it
            // did (Florian, 2026-09-21: everybody dimmed except himself).
            IGameObject? obj = Services.Objects.SearchByEntityId(member.EntityId);

            m_who[m_count] = who;
            m_distance[m_count] = obj is null ? Unreachable : obj.CurrentDistance;
            m_count++;
        }
    }

    /// <summary>
    /// How many yalms away somebody was at the last look, or <see cref="Unreachable"/> for
    /// anybody the game has not loaded — including anybody we have not measured yet.
    /// </summary>
    public byte DistanceTo(ulong who)
    {
        for (int i = 0; i < m_count; i++)
        {
            if (m_who[i] == who)
            {
                return m_distance[i];
            }
        }

        return Unreachable;
    }
}
