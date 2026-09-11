using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin.Services;
using WispUI.Core;
using WispUI.Data;
using WispUI.Localization;

namespace WispUI.Hud.PartyFrames;

/// <summary>One party member as the renderer needs them, and nothing more.</summary>
internal struct PartyMemberSnapshot
{
    public uint EntityId;

    /// <summary>
    /// The member's place in the party, counted from one — the number the game's own party
    /// list shows and the number people are called out by. It is the party's, not ours: once
    /// the frames can be sorted, member three has to stay the three they answer to.
    /// </summary>
    public int PartyNumber;

    public uint JobId;
    public JobRole Role;
    public uint Hp;
    public uint MaxHp;
    public uint Mp;
    public uint MaxMp;
    public bool IsLocalPlayer;

    /// <summary>
    /// Taken from the game's own string once, when this slot starts holding someone else.
    /// A name is the one field here that cannot be a number, and reading it allocates.
    /// </summary>
    public string Name;
}

/// <summary>
/// The party, read once per frame into a reused array. Everything that draws reads from here
/// and never asks the game again — that is the rule the whole HUD hangs on (CLAUDE.md §7.2),
/// because a second pass over the party list is a second chance to allocate or to trip over a
/// member who vanished between the two.
/// <para>
/// The array is allocated once at the size of a full party and never grows: eight is what the
/// game gives, and a light party simply leaves the tail unused.
/// </para>
/// </summary>
internal sealed class PartySnapshot
{
    /// <summary>A full party. An alliance is a later module and brings its own snapshot.</summary>
    public const int Capacity = 8;

    /// <summary>Far above any real entity id, so a stand-in can never be mistaken for a player.</summary>
    private const uint PlaceholderId = 0xF0000000u;

    /// <summary>One party's worth of jobs for edit mode: two tanks, two healers, four damage.</summary>
    private static readonly uint[] PlaceholderJobs = { 19u, 32u, 24u, 33u, 22u, 30u, 23u, 25u };

    /// <summary>
    /// How full the stand-ins are, in hundredths. Not all full: a row of untouched bars says
    /// nothing about where the health text sits or how a half-empty bar reads, which is the
    /// whole reason edit mode exists. One of them is left whole, because that case has to be
    /// looked at too.
    /// </summary>
    private static readonly uint[] PlaceholderHealth = { 100u, 64u, 92u, 38u, 100u, 71u, 17u, 85u };

    private static readonly uint[] PlaceholderMana = { 100u, 88u, 46u, 73u, 100u, 95u, 60u, 29u };

    private readonly PartyMemberSnapshot[] m_members = new PartyMemberSnapshot[Capacity];

    /// <summary>How many entries of <see cref="Members"/> hold someone this frame.</summary>
    public int Count { get; private set; }

    /// <summary>True while the player is alone — the party list is empty, but there is still a you.</summary>
    public bool IsSolo { get; private set; }

    public PartyMemberSnapshot[] Members => m_members;

    /// <summary>
    /// Fills the array from the game. Called once per frame, before anything draws.
    /// <para>
    /// Every slot of the party list is nullable and can empty out mid-zone, so each one is
    /// checked rather than trusted: a member who leaves between two frames must cost a skipped
    /// slot, not an exception in a draw path (CLAUDE.md §7.6).
    /// </para>
    /// </summary>
    public void Collect()
    {
        IPartyList party = Services.Party;
        int count = 0;

        for (int i = 0; i < party.Length && count < Capacity; i++)
        {
            var member = party[i];
            if (member is null)
            {
                continue;
            }

            ref PartyMemberSnapshot slot = ref m_members[count];
            uint entityId = member.EntityId;

            // The name is only read when this slot has changed hands. ToString() on the game's
            // string allocates, and doing it eight times a frame is exactly the kind of churn
            // that shows up as a stutter in a fight.
            if (slot.EntityId != entityId || slot.Name is null)
            {
                slot.Name = member.Name.ToString();
            }

            slot.EntityId = entityId;
            slot.PartyNumber = i + 1;
            slot.JobId = member.ClassJob.RowId;
            slot.Role = Jobs.Role(slot.JobId);
            slot.Hp = member.CurrentHP;
            slot.MaxHp = member.MaxHP;
            slot.Mp = member.CurrentMP;
            slot.MaxMp = member.MaxMP;
            slot.IsLocalPlayer = entityId == Services.Objects.LocalPlayer?.EntityId;

            count++;
        }

        // Alone, the party list is empty and the player is not in it. They are still the one
        // person a party frame would be about, so they take the first slot.
        this.IsSolo = count == 0;
        if (this.IsSolo)
        {
            count = this.CollectLocalPlayer();
        }

        this.Count = count;
    }

    /// <summary>
    /// Fills the array with a full party of stand-ins. Without a group there is nothing to lay
    /// out against, and a layout you cannot see while you set it is a layout you set twice —
    /// so edit mode brings its own eight, one per role, in the order a party is sorted.
    /// </summary>
    public void FillPlaceholders()
    {
        for (int i = 0; i < Capacity; i++)
        {
            ref PartyMemberSnapshot slot = ref m_members[i];
            slot.EntityId = PlaceholderId + (uint)i;
            slot.PartyNumber = i + 1;
            slot.JobId = PlaceholderJobs[i];
            slot.Role = Jobs.Role(slot.JobId);
            slot.Name = Strings.PreviewName;
            slot.MaxHp = 128000u;
            slot.Hp = slot.MaxHp / 100u * PlaceholderHealth[i];
            slot.MaxMp = 10000u;
            slot.Mp = slot.MaxMp / 100u * PlaceholderMana[i];
            slot.IsLocalPlayer = i == 0;
        }

        this.IsSolo = false;
        this.Count = Capacity;
    }

    private int CollectLocalPlayer()
    {
        IPlayerCharacter? player = Services.Objects.LocalPlayer;
        if (player is null)
        {
            return 0;
        }

        ref PartyMemberSnapshot slot = ref m_members[0];
        uint entityId = player.EntityId;
        if (slot.EntityId != entityId || slot.Name is null)
        {
            slot.Name = player.Name.ToString();
        }

        slot.EntityId = entityId;
        slot.PartyNumber = 1;
        slot.JobId = player.ClassJob.RowId;
        slot.Role = Jobs.Role(slot.JobId);
        slot.Hp = player.CurrentHp;
        slot.MaxHp = player.MaxHp;
        slot.Mp = player.CurrentMp;
        slot.MaxMp = player.MaxMp;
        slot.IsLocalPlayer = true;

        return 1;
    }
}
