using System;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Party;
using Dalamud.Plugin.Services;
using WispUI.Core;
using WispUI.Data;
using WispUI.Localization;

namespace WispUI.Hud.PartyFrames;

/// <summary>
/// Where a party member is, as far as the frames need to care.
/// <para>
/// 🔴 Told apart by the zone the party list reports for them, not by reading the game's own
/// party list for the word "Offline" — that is a localised string and would have to be kept
/// for every client language (Florian, 2026-09-12, asking how other plugins do it; they do it
/// that way, and we are not going to).
/// </para>
/// </summary>
internal enum PartyPresence
{
    /// <summary>Loaded and reporting: everything on the frame is real.</summary>
    Here = 0,

    /// <summary>In this zone but too far away for the game to load them. They will be back.</summary>
    OutOfRange = 1,

    /// <summary>In a different zone or instance. Not coming back this pull.</summary>
    Away = 2,

    /// <summary>No zone at all, which is what a member who has logged out looks like.</summary>
    Offline = 3,
}

/// <summary>
/// One status effect worth drawing on a frame. Flattened out of the game's array so the
/// renderer never touches a status again.
/// </summary>
internal struct AuraSnapshot
{
    public uint StatusId;

    /// <summary>The game's own icon for it. Zero means there is nothing to draw.</summary>
    public uint Icon;

    /// <summary>Seconds left, or zero for an effect that does not run out.</summary>
    public float Remaining;

    /// <summary>Stacks, where the effect has them.</summary>
    public ushort Stacks;

    /// <summary>Whether Esuna takes it off — the one thing a healer scans a party for.</summary>
    public bool CanDispel;

    /// <summary>The game's ranking, kept so the order can be seen to come from somewhere.</summary>
    public byte Priority;
}

/// <summary>One party member as the renderer needs them, and nothing more.</summary>
internal struct PartyMemberSnapshot
{
    public uint EntityId;

    /// <summary>
    /// Where the game keeps this member. Held so the effects on them can be read after the
    /// frames have been put in order, rather than read once and then shuffled about.
    /// </summary>
    public nint Address;

    /// <summary>
    /// The member's row in the game's own party list, counted from one — the number people
    /// are called out by. It is the game's number, not a count of our frames: the frames are
    /// laid out in this order, so the two agree, and when the game has nothing to say the
    /// party list's own order stands in.
    /// </summary>
    public int PartyNumber;

    /// <summary>
    /// Where this member sits in the HUD agent's own array — the one number the game's
    /// right-click menu can be opened with.
    /// <para>
    /// 🔴 Kept apart from <see cref="PartyNumber"/> on purpose. The agent's array always
    /// begins with the local player, whatever the list looks like on screen, so the two are
    /// the same number only in a party nobody has sorted. Passing the wrong one opens the
    /// menu on the wrong person, and it looks right until somebody turns on role sorting.
    /// </para>
    /// </summary>
    public int HudIndex;

    public uint JobId;
    public JobRole Role;
    public uint Hp;
    public uint MaxHp;
    public uint Mp;
    public uint MaxMp;
    public bool IsLocalPlayer;

    /// <summary>Whether this member leads the party. Alone, nobody does.</summary>
    public bool IsLeader;

    /// <summary>
    /// Whether the game is telling us anything real about this member right now.
    /// <para>
    /// 🔴 It stops when they are too far away, in another zone, or offline: the party list
    /// keeps the slot but reports zero for health, which drew a black frame with no fill at
    /// all until somebody walked back into range (Florian, 2026-09-12, in a real party). A
    /// frame that says "this person has no health" about somebody who is merely elsewhere is
    /// worse than saying nothing.
    /// </para>
    /// </summary>
    public bool HasData;

    /// <summary>Why there are no numbers, when there are none.</summary>
    public PartyPresence Presence;

    /// <summary>
    /// Taken from the game's own string once, when this slot starts holding someone else.
    /// A name is the one field here that cannot be a number, and reading it allocates.
    /// </summary>
    public string Name;

    // --- what is on them, out of the one status pass -------------------------

    /// <summary>How many of this member's afflictions are worth drawing.</summary>
    public int AuraCount;

    /// <summary>
    /// Whether anything on them can be cleansed. Kept apart from the icon list because it is
    /// read whether or not the icons are turned on — it is the answer to the question a
    /// healer is actually asking, and it should not depend on a display setting.
    /// </summary>
    public bool HasDispellable;

    /// <summary>Seconds left on a raise already cast on them, or zero for none.</summary>
    public float RaiseRemaining;

    /// <summary>Whether something is keeping them alive no matter what lands.</summary>
    public bool IsInvulnerable;
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

    /// <summary>
    /// The most afflictions a frame will ever hold. Above this the game's own ranking decides
    /// what is dropped, which is the whole reason to read that ranking — a frame with room for
    /// four icons and twelve effects on it has to choose, and the game has already chosen.
    /// </summary>
    public const int MaxAuras = 8;

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

    /// <summary>
    /// The game's own party list order, read once a frame into a reused array. Ten, because
    /// that is what the agent keeps room for; a party of four simply leaves the tail unused.
    /// </summary>
    private readonly NativeUi.HudPartyMemberInfo[] m_order =
        new NativeUi.HudPartyMemberInfo[NativeUi.HudPartyCapacity];

    /// <summary>
    /// Every member's afflictions, laid end to end: member <c>i</c> owns
    /// <c>MaxAuras</c> entries starting at <c>i * MaxAuras</c>, of which
    /// <see cref="PartyMemberSnapshot.AuraCount"/> hold anything.
    /// <para>
    /// Flat rather than an array per member, because a member is a struct and cannot carry one
    /// without allocating. Filled after the order is settled, so a slice always belongs to the
    /// frame drawn above it.
    /// </para>
    /// </summary>
    private readonly AuraSnapshot[] m_auras = new AuraSnapshot[Capacity * MaxAuras];

    /// <summary>One member's raw effects, reused. Never more than one member at a time.</summary>
    private readonly NativeUi.StatusEntry[] m_statuses =
        new NativeUi.StatusEntry[NativeUi.StatusCapacity];

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
        uint leader = party.PartyLeaderIndex;
        uint here = Services.ClientState.TerritoryType;
        int count = 0;

        // The game's own order, read once. Everything below asks this rather than deciding
        // anything about order itself — see NativeUi.ReadPartyOrder for why there is no
        // sorting option in the settings at all.
        int ordered = NativeUi.ReadPartyOrder(m_order);

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

            // Where the game puts them, when the game is willing to say. Its own order is the
            // only order the frames have: matched by entity id, and by content id for anyone
            // too far away to be loaded, who has no entity id worth matching on.
            if (!this.Locate(ordered, entityId, member.ContentId, out slot.PartyNumber, out slot.HudIndex))
            {
                slot.PartyNumber = i + 1;
                slot.HudIndex = i;
            }

            slot.JobId = member.ClassJob.RowId;
            slot.Role = Jobs.Role(slot.JobId);
            slot.Hp = member.CurrentHP;
            slot.MaxHp = member.MaxHP;
            slot.Mp = member.CurrentMP;
            slot.MaxMp = member.MaxMP;
            slot.IsLocalPlayer = entityId == Services.Objects.LocalPlayer?.EntityId;
            slot.IsLeader = i == leader;

            // Zero maximum health is the party list saying it has nothing for this slot.
            // Current health can legitimately be zero, so it is the maximum that is asked.
            slot.HasData = member.MaxHP > 0;
            slot.Presence = slot.HasData ? PartyPresence.Here : Presence(member, here);
            slot.Address = member.Address;

            count++;
        }

        Order(m_members, count);

        // Alone, the party list is empty and the player is not in it. They are still the one
        // person a party frame would be about, so they take the first slot.
        this.IsSolo = count == 0;
        if (this.IsSolo)
        {
            count = this.CollectLocalPlayer();
        }

        this.Count = count;

        // Last, and only once the order is settled: a member's effects are stored beside their
        // place in the block, so reading them before the sort would file them under a frame
        // that is about to move.
        for (int i = 0; i < count; i++)
        {
            this.CollectAuras(i);
        }
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

            // Nobody to open a menu on. Minus one is what the call refuses, so a stand-in
            // cannot reach a real person's menu even if something did ask.
            slot.HudIndex = -1;
            slot.JobId = PlaceholderJobs[i];
            slot.Role = Jobs.Role(slot.JobId);
            slot.Name = Strings.PreviewName;
            slot.MaxHp = 128000u;
            slot.Hp = slot.MaxHp / 100u * PlaceholderHealth[i];
            slot.HasData = true;
            slot.MaxMp = 10000u;
            slot.Mp = slot.MaxMp / 100u * PlaceholderMana[i];
            slot.IsLocalPlayer = i == 0;
            slot.IsLeader = i == 0;

            // Nobody real, so nothing is on them. Edit mode is about placing things, and a
            // stand-in that carried invented afflictions would be placing a fiction.
            slot.Address = 0;
            slot.AuraCount = 0;
            slot.HasDispellable = false;
            slot.RaiseRemaining = 0f;
            slot.IsInvulnerable = false;
        }

        this.IsSolo = false;
        this.Count = Capacity;
    }

    /// <summary>
    /// The afflictions on one member, in the order the game ranks them.
    /// <para>
    /// 🔴 One pass over the status array, and everything the frames say about what is on a
    /// person comes out of it: the icons, whether anything can be cleansed, whether a raise
    /// is already up, whether they cannot be killed. Four features, one walk — building them
    /// one at a time would walk it four times (spec §13.2).
    /// </para>
    /// </summary>
    private void CollectAuras(int index)
    {
        ref PartyMemberSnapshot slot = ref m_members[index];

        slot.AuraCount = 0;
        slot.HasDispellable = false;
        slot.RaiseRemaining = 0f;
        slot.IsInvulnerable = false;

        // Nobody loaded, nobody to read. A member across the map has no effects we can see,
        // which is what the game's own list shows too.
        if (!slot.HasData || slot.Address == 0)
        {
            return;
        }

        int count = slot.IsLocalPlayer && this.IsSolo
            ? NativeUi.ReadCharacterStatuses(slot.Address, m_statuses)
            : NativeUi.ReadMemberStatuses(slot.Address, m_statuses);

        int start = index * MaxAuras;

        for (int i = 0; i < count; i++)
        {
            ref NativeUi.StatusEntry entry = ref m_statuses[i];
            uint id = entry.StatusId;

            if (id == StatusData.Raise)
            {
                slot.RaiseRemaining = entry.Remaining;
                continue;
            }

            if (StatusData.IsInvulnerability(id))
            {
                slot.IsInvulnerable = true;
                continue;
            }

            StatusFacts facts = StatusData.Of(id);

            // Benefits are not drawn on a party frame. A healer reads this block for what is
            // wrong, and forty buffs would bury the one debuff that matters.
            if (facts.Category != 2)
            {
                continue;
            }

            if (facts.CanDispel)
            {
                slot.HasDispellable = true;
            }

            Insert(m_auras, start, ref slot.AuraCount, entry, facts);
        }
    }

    /// <summary>
    /// Puts one affliction in its place, highest ranked first, keeping at most
    /// <see cref="MaxAuras"/>.
    /// <para>
    /// An insertion into a list of eight, which is cheaper than collecting everything and
    /// sorting afterwards and never allocates. Something ranked below a full list is dropped
    /// where it stands.
    /// </para>
    /// </summary>
    private static void Insert(
        AuraSnapshot[] auras,
        int start,
        ref int count,
        in NativeUi.StatusEntry entry,
        in StatusFacts facts)
    {
        int at = count;

        while (at > 0 && auras[start + at - 1].Priority < facts.Priority)
        {
            if (at < MaxAuras)
            {
                auras[start + at] = auras[start + at - 1];
            }

            at--;
        }

        if (at >= MaxAuras)
        {
            return;
        }

        auras[start + at].StatusId = entry.StatusId;
        auras[start + at].Icon = facts.Icon;
        auras[start + at].Remaining = entry.Remaining;
        auras[start + at].Stacks = entry.Param;
        auras[start + at].CanDispel = facts.CanDispel;
        auras[start + at].Priority = facts.Priority;

        if (count < MaxAuras)
        {
            count++;
        }
    }

    /// <summary>The afflictions on the member drawn at <paramref name="index"/>.</summary>
    public ReadOnlySpan<AuraSnapshot> Auras(int index) =>
        new(m_auras, index * MaxAuras, m_members[index].AuraCount);

    /// <summary>
    /// Finds a member in the game's own party list order.
    /// <para>
    /// By entity id first, and by content id for anyone the game has not loaded: somebody
    /// across the map or in another duty still holds their row in the list, but their entity
    /// id is not worth matching on. Content id survives all of it.
    /// </para>
    /// <para>
    /// A search inside a loop, which reads like the wrong shape — but both sides are a party,
    /// so the worst case is sixty-four comparisons of a number, once a frame, against building
    /// and clearing a lookup that would allocate. The rule is no allocations in the draw path,
    /// not no arithmetic (CLAUDE.md §7.1).
    /// </para>
    /// </summary>
    private bool Locate(int ordered, uint entityId, ulong contentId, out int row, out int hudIndex)
    {
        for (int i = 0; i < ordered; i++)
        {
            ref NativeUi.HudPartyMemberInfo entry = ref m_order[i];

            bool same = (entityId != 0 && entry.EntityId == entityId)
                || (contentId != 0 && entry.ContentId == contentId);

            if (!same)
            {
                continue;
            }

            row = entry.Row + 1;
            hudIndex = entry.HudIndex;
            return true;
        }

        row = 0;
        hudIndex = -1;
        return false;
    }

    /// <summary>
    /// Puts the members in the order the game draws them.
    /// <para>
    /// An insertion sort, because a party is eight at most and an almost-sorted eight is the
    /// case it is fastest at — which is every frame, since the order only ever changes when
    /// somebody joins, leaves or swaps job. It moves whole entries, cached name and all, so
    /// nothing is re-read for having been shifted along one place.
    /// </para>
    /// </summary>
    private static void Order(PartyMemberSnapshot[] members, int count)
    {
        for (int i = 1; i < count; i++)
        {
            PartyMemberSnapshot moving = members[i];
            int j = i - 1;

            while (j >= 0 && members[j].PartyNumber > moving.PartyNumber)
            {
                members[j + 1] = members[j];
                j--;
            }

            members[j + 1] = moving;
        }
    }

    /// <summary>
    /// Why a member has no numbers: the zone the party list still reports for them.
    /// <para>
    /// The zone survives the member themselves not being loaded, which is what makes this
    /// work — somebody across the map still has one, somebody in a different duty has a
    /// different one, and somebody who has logged out has none at all.
    /// </para>
    /// </summary>
    private static PartyPresence Presence(IPartyMember member, uint here)
    {
        uint territory = member.Territory.RowId;

        if (territory == 0)
        {
            return PartyPresence.Offline;
        }

        return territory == here ? PartyPresence.OutOfRange : PartyPresence.Away;
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

        // The agent's array begins with the local player, so alone they are its only entry.
        slot.HudIndex = 0;

        slot.JobId = player.ClassJob.RowId;
        slot.Role = Jobs.Role(slot.JobId);
        slot.Hp = player.CurrentHp;
        slot.MaxHp = player.MaxHp;
        slot.HasData = true;
        slot.Mp = player.CurrentMp;
        slot.MaxMp = player.MaxMp;
        slot.IsLocalPlayer = true;
        slot.IsLeader = false;
        slot.Address = player.Address;

        return 1;
    }
}
