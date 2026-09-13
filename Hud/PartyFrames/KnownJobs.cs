namespace WispUI.Hud.PartyFrames;

/// <summary>
/// What job each party member was last seen on, kept by who they are.
/// <para>
/// 🔴 The game stops reporting a job for anybody it has not loaded — somebody in another zone
/// comes back as job zero, which is no role, which is the grey a frame falls back to. So a
/// party with two members elsewhere drew two grey-white slabs, and the one thing a frame is
/// read for first — what this person does — was gone for exactly the people whose frames
/// carry the least other information (Florian, 2026-09-13).
/// </para>
/// <para>
/// Remembering it costs nothing and is right whenever we have ever seen them. Somebody invited
/// from another zone and never met stays grey, which is honest: we do not know.
/// </para>
/// <para>
/// Keyed by content id, which belongs to the person rather than to a body in the world — the
/// same lesson the name cache had to learn an hour earlier.
/// </para>
/// </summary>
internal sealed class KnownJobs
{
    /// <summary>Room for more than a party, so somebody rejoining is still remembered.</summary>
    private const int Size = 32;

    private readonly ulong[] m_who = new ulong[Size];
    private readonly uint[] m_jobs = new uint[Size];
    private int m_next;

    /// <summary>
    /// Records a job when the game gives one, and answers with the best job we have.
    /// </summary>
    /// <param name="who">The member's content id. Zero is nobody and is not remembered.</param>
    /// <param name="job">What the game reports right now, which may be zero.</param>
    /// <returns>The reported job, or the last one seen for this person.</returns>
    public uint Resolve(ulong who, uint job)
    {
        if (who == 0)
        {
            return job;
        }

        for (int i = 0; i < Size; i++)
        {
            if (m_who[i] != who)
            {
                continue;
            }

            if (job != 0)
            {
                // They changed job, or we are seeing them properly for the first time.
                m_jobs[i] = job;
                return job;
            }

            return m_jobs[i];
        }

        if (job == 0)
        {
            // Nothing to remember and nothing remembered. Somebody invited from across the
            // world whom we have never met.
            return 0;
        }

        // Round-robin rather than an eviction rule worth reasoning about: the table holds four
        // parties' worth, and the cost of forgetting is one frame drawn grey.
        m_who[m_next] = who;
        m_jobs[m_next] = job;
        m_next = (m_next + 1) % Size;

        return job;
    }
}
