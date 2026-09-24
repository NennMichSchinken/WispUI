namespace WispUI.Hud.PartyFrames;

/// <summary>
/// How long an effect ran for when it started, remembered so the sweep on its icon has
/// something to be a fraction of.
/// <para>
/// 🔴 The game does not tell us. A status carries the seconds it has left and nothing else —
/// there is no field on the sheet for how long it was meant to last, and there cannot be one,
/// because the same effect lasts differently depending on what applied it. So the longest time
/// we have ever seen on it is taken as its length: the first frame it appears is the fullest it
/// will be, and a refresh shows up as a number larger than the one held, which replaces it.
/// </para>
/// <para>
/// The cost of being wrong is small and visible only once: an effect first seen half spent
/// sweeps from half, then is right for every later cast. That is the honest price of a sweep
/// the game will not give us the numbers for.
/// </para>
/// <para>
/// A fixed table with linear probing, never grown and never allocating — this is written from
/// the draw path's own collect step, and a dictionary there would hash and could rehash
/// (CLAUDE.md §7.1).
/// </para>
/// </summary>
internal sealed class AuraDurations
{
    /// <summary>
    /// Room for far more than a party can carry at once — eight people times a handful of
    /// effects — so probing stays short and an entry rarely has to be pushed out.
    /// </summary>
    private const int Size = 256;

    private const int MaxProbe = 8;

    /// <summary>How many collects an entry may go unseen before its slot can be taken.</summary>
    private const int Stale = 4;

    private readonly ulong[] m_keys = new ulong[Size];
    private readonly float[] m_durations = new float[Size];
    private readonly int[] m_seen = new int[Size];

    /// <summary>The pass each entry was first seen on — its arrival order, nothing more.</summary>
    private readonly int[] m_born = new int[Size];

    private int m_stamp;

    /// <summary>Called once per collect, before anything is recorded.</summary>
    public void BeginPass() => m_stamp++;

    /// <summary>
    /// Records what is left on an effect, and answers how long it runs in total and when it
    /// first turned up.
    /// </summary>
    /// <param name="owner">Whose effect it is. The same status on two people runs its own clock.</param>
    /// <param name="statusId">Which effect.</param>
    /// <param name="remaining">Seconds left, as the game reports them.</param>
    /// <param name="born">
    /// The pass this effect was first seen on. Larger means newer, and that is the whole of
    /// what it is for — a pass counter rather than a clock, because the only question asked of
    /// it is which of two effects arrived later.
    /// </param>
    /// <returns>The longest time seen on it, or zero for one that does not run out.</returns>
    public float Observe(uint owner, uint statusId, float remaining, out int born)
    {
        // Somebody appearing now is the newest thing on the frame until the table says
        // otherwise, which is also the right answer when the table has no room for them.
        born = m_stamp;

        ulong key = ((ulong)owner << 32) | statusId;
        int slot = Hash(key);
        int free = -1;

        for (int i = 0; i < MaxProbe; i++)
        {
            int at = (slot + i) & (Size - 1);

            if (m_keys[at] == key)
            {
                // Larger than what is held means it was refreshed, or that the first sighting
                // caught it already part spent. Either way the new number is the better one.
                if (remaining > m_durations[at])
                {
                    m_durations[at] = remaining;
                }

                m_seen[at] = m_stamp;
                born = m_born[at];

                // 🔴 A refresh does NOT make it new again. Somebody topping up a shield has
                // not given the frame a new thing to read, and a row that reshuffles every
                // time a heal-over-time is renewed is the jumping this ordering exists to
                // stop (Florian, 2026-09-22).
                return m_durations[at];
            }

            if (free < 0 && (m_keys[at] == 0 || m_stamp - m_seen[at] > Stale))
            {
                free = at;
            }
        }

        if (free < 0)
        {
            // Every slot in reach is somebody else's and still warm. The sweep does without
            // rather than evicting a live entry; it comes back on the next cast.
            return remaining > 0f ? remaining : 0f;
        }

        m_keys[free] = key;
        m_durations[free] = remaining > 0f ? remaining : 0f;
        m_seen[free] = m_stamp;
        m_born[free] = m_stamp;

        // An effect that does not run out has no fraction to sweep — but it still has a
        // birthday, which is why this is answered here rather than turned away at the door.
        return remaining > 0f ? remaining : 0f;
    }

    /// <summary>
    /// A cheap spread of the key across the table. The low bits of a status id run in
    /// sequence, so they are mixed with the entity id rather than used as they are.
    /// </summary>
    private static int Hash(ulong key)
    {
        ulong mixed = key * 0x9E3779B97F4A7C15ul;
        return (int)((mixed >> 40) & (Size - 1));
    }
}
