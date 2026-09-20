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

    /// <summary>
    /// How long it runs in total, as far as anyone has seen. Zero when it does not run out.
    /// See <see cref="AuraDurations"/> for why this has to be watched rather than read.
    /// </summary>
    public float Duration;

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
    /// The member's place in the party list Dalamud hands us, counting from zero — untouched
    /// by any sorting. Nothing uses it any more; kept because it is one of the three numbers
    /// that could be meant by "which member" and the distinction is worth being able to see.
    /// </summary>
    public int PartyIndex;

    /// <summary>
    /// Where this member sits in the HUD agent's own array, which always begins with the
    /// local player. This is what the game's right-click menu is opened with.
    /// <para>
    /// 🔴 SETTLED IN THE GAME (Florian, 2026-09-13), against the reading that had been
    /// inferred from another plugin. Right-clicking the party leader opened the local
    /// player's own profile — only possible if the index goes into this array, because the
    /// leader's place in the party list is zero and this array's zero is always us.
    /// </para>
    /// </summary>
    public int HudIndex;

    public uint JobId;
    public JobRole Role;
    public uint Hp;
    public uint MaxHp;
    public uint Mp;
    public uint MaxMp;

    /// <summary>
    /// The shield on them, as a percentage of maximum health, or zero for none.
    /// <para>
    /// A percentage because that is the shape the game keeps it in — one byte beside their job
    /// and level, and the same one the native party list draws its own segment from. Kept as
    /// the game gives it rather than converted to hit points, which would invent precision.
    /// </para>
    /// </summary>
    public byte Shield;

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

    /// <summary>
    /// Who <see cref="Name"/> was read for. The content id where there is one, because that
    /// is the only thing about a party member that is there while they are not.
    /// </summary>
    public ulong NameKey;

    // --- what is on them, out of the one status pass -------------------------

    /// <summary>How many of this member's afflictions are worth drawing.</summary>
    public int AuraCount;

    /// <summary>How many of the benefits on them are worth drawing.</summary>
    public int BuffCount;

    /// <summary>How many benefits on them came from somebody else.</summary>
    public int OtherCount;

    /// <summary>
    /// Whether anything on them can be cleansed. Kept apart from the icon list because it is
    /// read whether or not the icons are turned on — it is the answer to the question a
    /// healer is actually asking, and it should not depend on a display setting.
    /// </summary>
    public bool HasDispellable;

    /// <summary>
    /// Seconds on the raise that concerns them: how long the effect has left once one has
    /// landed, or how long until one lands while it is still being cast. Zero for neither.
    /// </summary>
    public float RaiseRemaining;

    /// <summary>
    /// Which of the two it is. The frame draws the same icon either way, but the two mean
    /// opposite things to a second healer — one says stop, the other says wait.
    /// </summary>
    public bool RaiseIsLanded;

    /// <summary>
    /// Which effect is keeping them alive no matter what lands, or zero for none. The id
    /// rather than a yes, so the frame can draw the game's own picture for that cooldown
    /// instead of a mark of ours that would have to be kept in step with it.
    /// </summary>
    public uint InvulnerableStatus;
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

    /// <summary>How long a stand-in effect claims to run. Long enough to watch, short enough to see move.</summary>
    private const float PlaceholderDuration = 30f;

    /// <summary>
    /// How much a stand-in effect has left, counted down off a clock instead of written
    /// down.
    /// <para>
    /// 🔴 It used to be a fixed number, so the sweep in the preview band stood perfectly
    /// still and the whole feature looked like something we had not built — Florian asked
    /// for a duration display that had been shipped and switched on for days (2026-09-21).
    /// A preview that does not move is a preview that lies about a moving thing.
    /// </para>
    /// <para>
    /// The offset is what keeps the icons out of step, so one look shows the sweep at
    /// several points rather than all of them at once.
    /// </para>
    /// </summary>
    private static float PlaceholderRemaining(int offset)
    {
        double now = Environment.TickCount64 / 1000d;
        float along = (float)((now + offset) % PlaceholderDuration);
        return PlaceholderDuration - along;
    }

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

    /// <summary>
    /// Shields on the stand-ins, in hundredths of maximum health. Unlike the effects on a
    /// person, a shield belongs with health and mana: it is something the bar says, and the
    /// bar is exactly what edit mode is for placing.
    /// <para>
    /// Chosen to show every case at once rather than to look plausible. Most have none, which
    /// is the common state; one sits inside the missing health, and two are deliberately
    /// bigger than the gap they have to fill — those are the ones that produce the piece
    /// lying over the health, and it cannot be judged without seeing it.
    /// </para>
    /// </summary>
    private static readonly byte[] PlaceholderShield = { 30, 20, 25, 0, 40, 0, 0, 0 };

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

    /// <summary>
    /// The same again for benefits. A second list rather than one mixed one, because the two
    /// answer different questions and are placed in different corners: what is wrong with
    /// this person, and what have I already put on them.
    /// </summary>
    private readonly AuraSnapshot[] m_buffs = new AuraSnapshot[Capacity * MaxAuras];

    /// <summary>
    /// And a third, for benefits somebody else put there — the row a healer reads to see what
    /// is already keeping this person up before adding to it. Its own row because it must not
    /// compete for places with the player's own effects, which is the whole reason those have
    /// a row of their own.
    /// </summary>
    private readonly AuraSnapshot[] m_others = new AuraSnapshot[Capacity * MaxAuras];

    /// <summary>One member's raw effects, reused. Never more than one member at a time.</summary>
    private readonly NativeUi.StatusEntry[] m_statuses =
        new NativeUi.StatusEntry[NativeUi.StatusCapacity];

    /// <summary>How long each effect runs, watched over time because the game never says.</summary>
    private readonly AuraDurations m_durations = new();

    /// <summary>What each member was last seen doing, for when the game stops saying.</summary>
    private readonly KnownJobs m_jobs = new();

    /// <summary>How far each member is, asked of the game on the tick.</summary>
    private readonly RangeWatch m_range = new();

    /// <summary>Raises in flight, which exist before their effect does.</summary>
    private readonly RaiseWatch m_raises = new();

    /// <summary>Whether only the player's own benefits are kept, and whose those are.</summary>
    private bool m_ownBuffsOnly = true;
    private uint m_localEntityId;

    /// <summary>Looks for raises being cast. Called on the game's tick, not while drawing.</summary>
    public void Tick(double now)
    {
        m_raises.Tick(now);
        m_range.Tick(now);
    }

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
    public void Collect(bool ownBuffsOnly)
    {
        IPartyList party = Services.Party;
        uint leader = party.PartyLeaderIndex;
        uint here = Services.ClientState.TerritoryType;
        int count = 0;

        m_ownBuffsOnly = ownBuffsOnly;

        IPlayerCharacter? self = Services.Objects.LocalPlayer;
        m_localEntityId = self?.EntityId ?? 0u;


        // The game's own order, read once. Everything below asks this rather than deciding
        // anything about order itself — see NativeUi.ReadPartyOrder for why there is no
        // sorting option in the settings at all.
        int ordered = NativeUi.ReadPartyOrder(m_order);

        for (int i = 0; i < party.Length && count < Capacity; i++)
        {
            // 🔴 The address, not party[i]. Dalamud's indexer wraps the member in a new
            // object on every single access — eight of them a frame, for numbers that are
            // sitting in a flat structure the whole time. NativeUi.ReadMember takes the same
            // pointer the indexer would have wrapped and reads the fields out of it.
            nint address = party.GetPartyMemberAddress(i);

            if (!NativeUi.ReadMember(address, out NativeUi.MemberFacts member))
            {
                continue;
            }

            ref PartyMemberSnapshot slot = ref m_members[count];
            uint entityId = member.EntityId;

            // 🔴 Who this slot is holding, for the name cache — by content id, and only by
            // entity id when there is no content id to go on.
            //
            // An entity id belongs to a body in the world, so everybody the game has not
            // loaded has the same one: zero. Keyed on that, the second member in another zone
            // matched the first one's cache and wore their name — two frames, one name, and
            // the real third member never appeared (Florian, 2026-09-13, in a party of three
            // with two members elsewhere). A content id belongs to the person and is there
            // whether or not they are.
            ulong nameKey = member.ContentId != 0 ? member.ContentId : entityId;

            // The name is only read when this slot has changed hands. ToString() on the game's
            // string allocates, and doing it eight times a frame is exactly the kind of churn
            // that shows up as a stutter in a fight.
            if (slot.NameKey != nameKey || slot.Name is null)
            {
                slot.NameKey = nameKey;
                slot.Name = NativeUi.MemberName(address);
            }

            slot.EntityId = entityId;

            // Their place in this list, before anything is sorted. Kept as it is: it is what
            // opens the right-click menu, and it must not follow the frames around.
            slot.PartyIndex = i;

            // Where the game puts them, when the game is willing to say. Its own order is the
            // only order the frames have: matched by entity id, and by content id for anyone
            // too far away to be loaded, who has no entity id worth matching on.
            if (!this.Locate(ordered, entityId, member.ContentId, out slot.PartyNumber, out slot.HudIndex))
            {
                slot.PartyNumber = i + 1;
                slot.HudIndex = i;
            }

            // Three places, in order of how much they can be trusted: what the party list
            // reports, what the game's own list is drawing on that row, and what we last saw.
            // The first is empty for anybody not loaded, which is exactly when the other two
            // matter — and a frame that forgets what somebody does is the one that needed to
            // say it most.
            uint job = member.ClassJob;

            if (job == 0)
            {
                job = NativeUi.PartyListJob(slot.PartyNumber - 1);
            }

            slot.JobId = m_jobs.Resolve(nameKey, job);
            slot.Role = Jobs.Role(slot.JobId);
            slot.Hp = member.Hp;
            slot.MaxHp = member.MaxHp;
            slot.Mp = member.Mp;
            slot.MaxMp = member.MaxMp;
            slot.Shield = member.Shield;
            // The one already read above, not a fresh ask. Reading it here walked the object
            // table and type-tested the result once per member, for an answer that cannot
            // change inside the loop — a list already walked is not walked again (§5.2).
            slot.IsLocalPlayer = entityId != 0u && entityId == m_localEntityId;
            slot.IsLeader = i == leader;

            // Zero maximum health is the party list saying it has nothing for this slot.
            // Current health can legitimately be zero, so it is the maximum that is asked.
            slot.HasData = member.MaxHp > 0;
            slot.Presence = this.Presence(in member, here, nameKey);
            slot.Address = address;

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
        m_durations.BeginPass();

        for (int i = 0; i < count; i++)
        {
            this.CollectAuras(i);
        }
    }

    /// <summary>
    /// Fills the array with stand-ins. Without a group there is nothing to lay out against,
    /// and a layout you cannot see while you set it is a layout you set twice — so edit mode
    /// brings its own eight, one per role, in the order a party is sorted.
    /// <para>
    /// The count is asked for, because the settings window preview offers the three party
    /// sizes somebody actually plays: alone, a light party, a full one. Edit mode takes all
    /// eight, which is where there is finally room to see what a row of icons does.
    /// </para>
    /// </summary>
    public void FillPlaceholders(int count = Capacity, bool withAuras = false)
    {
        count = Math.Clamp(count, 1, Capacity);

        for (int i = 0; i < count; i++)
        {
            ref PartyMemberSnapshot slot = ref m_members[i];
            slot.EntityId = PlaceholderId + (uint)i;
            slot.HudIndex = -1;
            slot.PartyIndex = -1;

            // Nobody to open a menu on. Minus one is what the call refuses, so a stand-in
            // cannot reach a real person's menu even if something did ask.
            slot.HudIndex = -1;
            slot.JobId = PlaceholderJobs[i];
            slot.Role = Jobs.Role(slot.JobId);
            slot.Name = Strings.PreviewName;
            slot.NameKey = PlaceholderId + (uint)i;
            slot.MaxHp = 128000u;
            slot.Hp = slot.MaxHp / 100u * PlaceholderHealth[i];
            slot.HasData = true;
            slot.Presence = PartyPresence.Here;
            slot.MaxMp = 10000u;
            slot.Mp = slot.MaxMp / 100u * PlaceholderMana[i];
            slot.Shield = PlaceholderShield[i];
            slot.IsLocalPlayer = i == 0;
            slot.IsLeader = i == 0;

            // Nobody real, so nothing is on them. Edit mode is about placing things, and a
            // stand-in that carried invented afflictions would be placing a fiction.
            slot.Address = 0;
            slot.AuraCount = 0;
            slot.BuffCount = 0;
            slot.OtherCount = 0;
            slot.HasDispellable = false;
            slot.RaiseRemaining = 0f;
            slot.RaiseIsLanded = false;
            slot.InvulnerableStatus = 0u;
        }

        this.IsSolo = count == 1;
        this.Count = count;

        // Edit mode and the preview are the two halves of setting a frame up, and they are
        // most often on together: eight stand-in people is where there is finally room to see
        // what a row of icons does to a layout.
        if (withAuras)
        {
            for (int i = 0; i < count; i++)
            {
                this.PreviewAuras(i);
            }
        }
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
        slot.BuffCount = 0;
        slot.OtherCount = 0;
        slot.HasDispellable = false;
        slot.RaiseRemaining = 0f;
        slot.RaiseIsLanded = false;
        slot.InvulnerableStatus = 0u;

        // A raise still in the air, which has no effect to be found yet. Asked before the
        // status list, so a raise that has since landed overwrites it with the real thing.
        if (slot.Hp == 0 && slot.HasData)
        {
            slot.RaiseRemaining = m_raises.IncomingFor(slot.EntityId);
        }

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

            if (StatusData.IsRaise(id))
            {
                slot.RaiseRemaining = entry.Remaining;
                slot.RaiseIsLanded = true;
                continue;
            }

            if (StatusData.IsInvulnerability(id))
            {
                slot.InvulnerableStatus = id;
                continue;
            }

            StatusFacts facts = StatusData.Of(id);

            // Neither a benefit nor an affliction: the game has a third kind, and it is
            // neither of the two questions a frame is being asked.
            if (facts.Category is not (1 or 2))
            {
                continue;
            }

            float duration = m_durations.Observe(slot.EntityId, id, entry.Remaining);

            if (facts.Category == 1)
            {
                // 🔴 Anything carried around rather than happening in this fight. Food, company
                // buffs, the roulette bonus, rested experience — all of them sit on everybody
                // all day and are never what a party frame is being read for (Florian,
                // 2026-09-13, finding the roulette bonus in the row).
                //
                // Told apart by how long it runs rather than by a list of ids, because the
                // list would need a new entry every time the game adds one of these and would
                // be wrong until somebody noticed. Nothing that matters in a fight lasts a
                // quarter of an hour, and nothing carried about lasts less.
                if (facts.IsUpkeep || entry.Remaining <= 0f || entry.Remaining > Carried)
                {
                    continue;
                }

                // Two rows, split by who cast it. Yours is the one you are checking you have
                // already done; everybody else's is what is keeping this person up without
                // you. Mixed into one row they compete, and in a full party the dozens win.
                bool mine = (uint)entry.SourceId == m_localEntityId;

                if (mine)
                {
                    Insert(m_buffs, start, ref slot.BuffCount, entry, facts, duration);
                }
                else if (!m_ownBuffsOnly)
                {
                    // Without the split, everything lands in the first row the way it did
                    // before there was a second one.
                    Insert(m_buffs, start, ref slot.BuffCount, entry, facts, duration);
                }
                else
                {
                    Insert(m_others, start, ref slot.OtherCount, entry, facts, duration);
                }

                continue;
            }

            if (facts.CanDispel)
            {
                slot.HasDispellable = true;
            }

            Insert(m_auras, start, ref slot.AuraCount, entry, facts, duration);
        }
    }

    /// <summary>
    /// Stand-in effects, so the icons can be placed without waiting for a fight to make some.
    /// <para>
    /// The frames get a full row each, with the sweeps at different points so it is obvious
    /// what the sweep does, and the marks spread across the party rather than on everyone:
    /// a row where every frame says the same thing shows nothing about how one frame stands
    /// out from the others.
    /// </para>
    /// </summary>
    private void PreviewAuras(int index)
    {
        ref PartyMemberSnapshot slot = ref m_members[index];

        slot.AuraCount = 0;
        slot.BuffCount = 0;
        slot.OtherCount = 0;
        slot.HasDispellable = false;
        slot.RaiseRemaining = 0f;
        slot.RaiseIsLanded = false;
        slot.InvulnerableStatus = 0u;

        uint[] preview = StatusData.Preview;
        int start = index * MaxAuras;

        for (int i = 0; i < MaxAuras && i < preview.Length; i++)
        {
            uint id = preview[i];

            if (id == 0)
            {
                continue;
            }

            StatusFacts facts = StatusData.Of(id);

            ref AuraSnapshot aura = ref m_auras[start + slot.AuraCount];
            aura.StatusId = id;
            aura.Icon = facts.Icon;
            aura.CanDispel = facts.CanDispel;
            aura.Priority = facts.Priority;

            // A different point of the sweep on each, so what the sweep is doing can be seen
            // in one look rather than by watching one icon for half a minute.
            aura.Duration = PlaceholderDuration;
            aura.Remaining = PlaceholderRemaining((index * 3) + (i * 7));

            // A few stacks, and not on all of them, so both cases are on screen.
            aura.Stacks = (ushort)(((i + index) % 3 == 0) ? 0 : (i + 2));

            if (facts.CanDispel)
            {
                slot.HasDispellable = true;
            }

            slot.AuraCount++;
        }

        // Two of the eight, so the marks can be judged against frames that do not carry them.
        if (index == 1)
        {
            slot.RaiseRemaining = 6f;
            slot.RaiseIsLanded = true;
        }
        else if (index == 3)
        {
            slot.InvulnerableStatus = StatusData.PreviewInvulnerability;
        }

        // The cleanse mark belongs to a party, not to everyone in it.
        if (index > 4)
        {
            slot.HasDispellable = false;
        }

        // 🔴 Your own row shows what the job being played leaves behind — a White Mage sees
        // Regen and Medica, not four effects out of the sheet nobody recognises (Florian,
        // 2026-09-19). Derived from the job's actions by name; see JobBuffs for why that is
        // the only link the game offers. A job that leaves nothing falls back to the generic
        // set rather than showing an empty row, because the row still has to be placeable.
        uint[] mine = JobBuffs.For(Services.Objects.LocalPlayer?.ClassJob.RowId ?? 0u, MaxAuras);

        if (mine.Length == 0)
        {
            mine = StatusData.PreviewBuffs;
        }

        Fill(m_buffs, start, ref slot.BuffCount, mine, (index * 5) + 11);

        // 🔴 And the third row gets a DIFFERENT set, not the same one shuffled. It used to be
        // the second row's stand-ins in another order, which made the two rows impossible to
        // tell apart while placing them — the whole reason there are two is that one is
        // yours and one is somebody else's (Florian, 2026-09-19: the row read as empty
        // because it was indistinguishable from the one above it). The generic benefits are
        // the honest stand-in for "put there by another player".
        Fill(m_others, start, ref slot.OtherCount, StatusData.PreviewBuffs, (index * 7) + 3);
    }

    /// <summary>
    /// Lays a set of stand-in effects into one member's slice of a row.
    /// </summary>
    /// <param name="scatter">
    /// Shifts where each icon sits in its sweep, so what the countdown is doing can be seen
    /// in one look rather than by watching a single icon for half a minute.
    /// </param>
    private static void Fill(AuraSnapshot[] into, int start, ref int count, uint[] ids, int scatter)
    {
        count = 0;

        for (int i = 0; i < MaxAuras && i < ids.Length; i++)
        {
            uint id = ids[i];

            if (id == 0)
            {
                continue;
            }

            StatusFacts facts = StatusData.Of(id);

            ref AuraSnapshot slot = ref into[start + count];
            slot.StatusId = id;
            slot.Icon = facts.Icon;
            slot.CanDispel = false;
            slot.Priority = facts.Priority;
            slot.Duration = PlaceholderDuration;
            slot.Remaining = PlaceholderRemaining(scatter + (i * 11));
            slot.Stacks = 0;

            count++;
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
        in StatusFacts facts,
        float duration)
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

        // A stacking effect has one picture per stack, in a run from its own id. The game
        // says how many stacks there are and how many there can be, so the right picture is
        // simply the one that far along — no number needed to read it.
        auras[start + at].Icon = facts.MaxStacks > 1 && entry.Param > 0 && entry.Param <= facts.MaxStacks
            ? facts.Icon + entry.Param - 1u
            : facts.Icon;

        auras[start + at].Remaining = entry.Remaining;
        auras[start + at].Duration = duration;
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

    /// <summary>The benefits on them — by default only the ones this player put there.</summary>
    public ReadOnlySpan<AuraSnapshot> Buffs(int index) =>
        new(m_buffs, index * MaxAuras, m_members[index].BuffCount);

    /// <summary>The benefits somebody else put there.</summary>
    public ReadOnlySpan<AuraSnapshot> Others(int index) =>
        new(m_others, index * MaxAuras, m_members[index].OtherCount);

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
    private PartyPresence Presence(in NativeUi.MemberFacts member, uint here, ulong who)
    {
        uint territory = member.Territory;

        if (territory == 0)
        {
            return PartyPresence.Offline;
        }

        if (territory != here)
        {
            return PartyPresence.Away;
        }

        // 🔴 Asked of the game, not worked out from positions. Three attempts went into
        // measuring the gap between two coordinates and not one of them ever dimmed anybody,
        // because a party member the game has not loaded has no position to measure — which
        // is precisely the case the measurement was for.
        //
        // The game counts the distance to everything it has loaded, and has nothing at all
        // for anybody it has not. That "nothing" is the answer: no object means too far to
        // do anything about, which is the only question a frame is asking.
        byte distance = m_range.DistanceTo(who);

        return distance > Reach ? PartyPresence.OutOfRange : PartyPresence.Here;
    }

    /// <summary>
    /// How far away somebody has to be before the frame says so, in yalms.
    /// <para>
    /// Thirty, because that is the range of nearly every heal in the game. The question a
    /// party frame is answering is not "how far is this person" but "can I do anything about
    /// them", and thirty is where the answer turns to no.
    /// </para>
    /// </summary>
    private const byte Reach = 30;

    /// <summary>
    /// Longer than this, in seconds, and a benefit is something the person is carrying about
    /// rather than something that happened in this fight. Fifteen minutes: well above any
    /// combat effect and well below food, company buffs and the roulette bonus.
    /// </summary>
    private const float Carried = 15f * 60f;

    private int CollectLocalPlayer()
    {
        IPlayerCharacter? player = Services.Objects.LocalPlayer;
        if (player is null)
        {
            return 0;
        }

        ref PartyMemberSnapshot slot = ref m_members[0];
        uint entityId = player.EntityId;
        if (slot.NameKey != entityId || slot.Name is null)
        {
            slot.NameKey = entityId;
            slot.Name = player.Name.ToString();
        }

        slot.EntityId = entityId;
        slot.PartyNumber = 1;

        // The agent's array begins with the local player, so alone they are its only entry.
        slot.HudIndex = 0;
        slot.PartyIndex = 0;

        slot.JobId = player.ClassJob.RowId;
        slot.Role = Jobs.Role(slot.JobId);
        slot.Hp = player.CurrentHp;
        slot.MaxHp = player.MaxHp;
        slot.HasData = true;
        slot.Presence = PartyPresence.Here;
        slot.Mp = player.CurrentMp;
        slot.MaxMp = player.MaxMp;
        slot.Shield = NativeUi.CharacterShield(player.Address);
        slot.IsLocalPlayer = true;
        slot.IsLeader = false;
        slot.Address = player.Address;

        return 1;
    }
}
