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

    private int m_stamp;

    /// <summary>Called once per collect, before anything is recorded.</summary>
    public void BeginPass() => m_stamp++;

    /// <summary>
    /// Records what is left on an effect and answers how long it runs in total.
    /// </summary>
    /// <param name="owner">Whose effect it is. The same status on two people runs its own clock.</param>
    /// <param name="statusId">Which effect.</param>
    /// <param name="remaining">Seconds left, as the game reports them.</param>
    /// <returns>The longest time seen on it, which is never less than <paramref name="remaining"/>.</returns>
    public float Observe(uint owner, uint statusId, float remaining)
    {
        if (remaining <= 0f)
        {
            // Nothing to sweep: an effect that does not run out has no fraction to show.
            return 0f;
        }

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
            return remaining;
        }

        m_keys[free] = key;
        m_durations[free] = remaining;
        m_seen[free] = m_stamp;
        return remaining;
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
