using WispUI.Core;

namespace WispUI.Data;

/// <summary>
/// What the game's own data says about one status effect. Four numbers, read once.
/// </summary>
internal struct StatusFacts
{
    /// <summary>The icon the game draws for it, or zero for a status that has none.</summary>
    public uint Icon;

    /// <summary>
    /// The game's own ranking for its party list. Used instead of a ranking of ours: when
    /// more effects are on somebody than there is room for, the game has already decided
    /// which ones matter, and inventing a second answer would only disagree with the list
    /// beside it. Higher comes first.
    /// </summary>
    public byte Priority;

    /// <summary>
    /// What kind of effect it is, in the game's own numbering: 1 is a benefit, 2 is an
    /// affliction. Anything else is neither and is left alone.
    /// </summary>
    public byte Category;

    /// <summary>
    /// How many times it can stack, or zero for one that does not.
    /// <para>
    /// 🔴 Needed for the picture, not just for the number: a stacking effect has one icon per
    /// stack count, laid out in a run from its own id. Three stacks is <c>Icon + 2</c>, and
    /// drawing the base icon for all of them throws away what the game already says.
    /// </para>
    /// </summary>
    public byte MaxStacks;

    /// <summary>
    /// Something the player carries about with them rather than something that happened in
    /// this fight: a free company buff, or a meal.
    /// <para>
    /// 🔴 Kept out of the benefit rows. Everybody has food on, it lasts half an hour, and it
    /// is never the thing being looked for — it is the clutter that made those rows worth
    /// filtering in the first place (Florian, 2026-09-13, noting it gets blacklisted).
    /// </para>
    /// </summary>
    public bool IsUpkeep;

    /// <summary>
    /// Whether Esuna and its like can take it off.
    /// <para>
    /// 🔴 Straight out of the sheet, not a list somebody keeps up to date. Every curated id
    /// list in a plugin is a list that goes stale on the next patch, and this one does not
    /// have to exist.
    /// </para>
    /// </summary>
    public bool CanDispel;
}

/// <summary>
/// Status effect facts by status id, read from the game's sheet once and kept.
/// <para>
/// A flat array indexed by id rather than a dictionary: the frames ask this for every effect
/// on every member, every frame, and that path may not allocate or hash (CLAUDE.md §7.1,
/// §7.3). The sheet is a few thousand rows of four bytes, so the whole thing is a handful of
/// kilobytes held for the life of the plugin.
/// </para>
/// </summary>
internal static class StatusData
{
    /// <summary>
    /// Effects that make somebody unkillable for a few seconds.
    /// <para>
    /// 🔴 The one curated list here, and it is curated because the sheet has no field for it
    /// — "cannot die right now" is not something the game writes down. Kept deliberately
    /// short: the tank cooldowns that actually stop a healer reacting, not everything that
    /// reduces damage. A mitigation is not invulnerability, and a frame that says it is would
    /// be lying at the worst moment.
    /// </para>
    /// <para>
    /// ⚠️ This list WILL go stale — it is exactly the kind of list the rest of this file
    /// avoids. When a job gets a new one, it is added here and nowhere else.
    /// </para>
    /// <para>
    /// 🔴 NOT VERIFIED IN THE GAME. These ids are written from memory, and a wrong one is
    /// invisible — it simply never lights up, or lights up on the wrong effect. So
    /// <see cref="Prime"/> logs the name the game gives each of them at load, in the client's
    /// own language: the log says what this list actually resolved to, and anything that is
    /// not the cooldown named beside it comes out of the list.
    /// </para>
    /// </summary>
    private static readonly ushort[] Invulnerabilities =
    {
        82,   // Hallowed Ground — Paladin
        409,  // Living Dead — Dark Knight, the window before Walking Dead
        810,  // Living Dead, the other id the effect is seen under
        811,  // Walking Dead — the one that has to be healed through
        1302, // Holmgang — Warrior
        1836, // Superbolide — Gunbreaker
        3255, // Superbolide, the other id the effect is seen under
    };

    /// <summary>
    /// The Raise effect and the sickness that follows it — the two a healer reads as "somebody
    /// is already on this" and "do not expect much of them yet".
    /// <para>
    /// Two ids for the same thing: the effect is seen under both, and reading only the first
    /// misses whoever was picked up by the other. Confirmed against a published plugin's own
    /// handling of it — a status id is a fact about the game, not somebody's work.
    /// </para>
    /// </summary>
    public const uint Raise = 148u;

    public const uint RaiseAlt = 1140u;

    /// <summary>
    /// The meal buff — one id for every dish in the game. ⚠️ NOT VERIFIED in the game; it is
    /// logged by <c>/wisp status</c> along with everything else, so a wrong one shows up as
    /// food still sitting in the benefit row rather than as nothing at all.
    /// </summary>
    public const uint WellFed = 48u;

    public const uint Weakness = 43u;

    public const uint BrinkOfDeath = 44u;

    /// <summary>Whether this effect means a raise has already landed on them.</summary>
    public static bool IsRaise(uint statusId) => statusId is Raise or RaiseAlt;

    /// <summary>
    /// The jobs that can take an effect off somebody, so the cleanse mark can be shown to the
    /// people it is an instruction for and nobody else.
    /// <para>
    /// Bard is on this list only from level 35, which is where it learns the ability — under
    /// that it is a job that looks like it can and cannot.
    /// </para>
    /// </summary>
    public static bool CanCleanse(uint jobId, int level) => jobId switch
    {
        6u => true,   // Conjurer
        24u => true,  // White Mage
        28u => true,  // Scholar
        33u => true,  // Astrologian
        40u => true,  // Sage
        36u => true,  // Blue Mage
        23u => level >= 35, // Bard
        _ => false,
    };

    /// <summary>
    /// The actions that put somebody back on their feet, so a raise already on its way can be
    /// shown before its effect exists.
    /// <para>
    /// 🔴 The gap this closes: the Raise <em>effect</em> only appears once the cast lands. The
    /// eight seconds before that are exactly when a second healer needs to know somebody is
    /// already on it, and reading the effect alone says nothing for all eight of them.
    /// </para>
    /// </summary>
    public static bool IsRaiseAction(uint actionId) => actionId switch
    {
        125u => true,   // Raise — Conjurer, White Mage
        173u => true,   // Resurrection — Arcanist, Summoner, Scholar
        3603u => true,  // Ascend — Astrologian
        24287u => true, // Egeiro — Sage
        18317u => true, // Angel Whisper — Blue Mage
        12996u => true, // Raise L — Eureka
        20730u => true, // Lost Arise — Bozja
        22345u => true, // Lost Sacrifice — Bozja
        _ => IsRaiseLimitBreak(actionId),
    };

    /// <summary>The healer limit breaks, which raise the whole party at once.</summary>
    public static bool IsRaiseLimitBreak(uint actionId) =>
        actionId is 208u or 4247u or 4248u or 24859u;

    private static StatusFacts[] s_facts = System.Array.Empty<StatusFacts>();

    private static bool[] s_invulnerable = System.Array.Empty<bool>();

    /// <summary>
    /// Reads the sheet. Called once while the plugin is loading, never from a draw path.
    /// <para>
    /// Failing here is not fatal: everything falls back to a status nobody knows anything
    /// about, which draws its own icon and nothing else. That is a worse party frame, not a
    /// broken one.
    /// </para>
    /// </summary>
    public static void Prime()
    {
        Lumina.Excel.ExcelSheet<Lumina.Excel.Sheets.Status>? sheet =
            Services.Data.GetExcelSheet<Lumina.Excel.Sheets.Status>();

        if (sheet is null)
        {
            return;
        }

        int size = (int)sheet.Count + 1;

        // Row ids are not contiguous and the count is not the highest id, so the highest id is
        // asked for rather than assumed. One pass, at load.
        foreach (Lumina.Excel.Sheets.Status row in sheet)
        {
            if (row.RowId >= size)
            {
                size = (int)row.RowId + 1;
            }
        }

        var facts = new StatusFacts[size];
        var invulnerable = new bool[size];

        foreach (Lumina.Excel.Sheets.Status row in sheet)
        {
            ref StatusFacts entry = ref facts[row.RowId];

            entry.Icon = row.Icon;
            entry.Priority = row.PartyListPriority;
            entry.Category = row.StatusCategory;
            entry.CanDispel = row.CanDispel;
            entry.MaxStacks = row.MaxStacks;

            // A free company buff says so in the sheet. Food does not, so it is named: it is
            // the one effect everybody wears all the time, and there is no field for "this is
            // a meal". One id rather than a list, because one meal buff covers every dish.
            entry.IsUpkeep = row.IsFcBuff || row.RowId == WellFed;
        }

        for (int i = 0; i < Invulnerabilities.Length; i++)
        {
            ushort id = Invulnerabilities[i];

            if (id >= size)
            {
                Services.Log.Warning("Invulnerability {Id} is not a status the game knows.", id);
                continue;
            }

            invulnerable[id] = true;

            // Written out so the guesswork above can be checked rather than trusted. A wrong
            // id here is silent otherwise, and a frame that never says "unkillable" looks the
            // same as one that has nothing to say (session 8: a silent fallback reads as a
            // decision).
            Services.Log.Information(
                "Invulnerability {Id} is \"{Name}\".",
                id,
                sheet.GetRowOrDefault(id)?.Name.ExtractText() ?? "?");
        }

        s_facts = facts;
        s_invulnerable = invulnerable;
        BuildPreview(sheet);
    }

    /// <summary>
    /// A handful of real afflictions to stand in while somebody is placing the icons, half of
    /// them cleansable so both looks can be judged.
    /// <para>
    /// Taken out of the sheet rather than written down: any four afflictions with pictures
    /// will do, and picking them from the data means there is nothing here to go stale and no
    /// id guessed. They are highly ranked ones, so the preview looks like a frame in trouble
    /// rather than a frame with four odd things on it.
    /// </para>
    /// </summary>
    public static uint[] Preview { get; private set; } = System.Array.Empty<uint>();

    /// <summary>The same for benefits, so the second row can be placed too.</summary>
    public static uint[] PreviewBuffs { get; private set; } = System.Array.Empty<uint>();

    /// <summary>An invulnerability the preview can show, or zero if none resolved.</summary>
    public static uint PreviewInvulnerability { get; private set; }

    private static void BuildPreview(Lumina.Excel.ExcelSheet<Lumina.Excel.Sheets.Status> sheet)
    {
        // 🔴 The highest ranked, not the first found. The sheet's early rows are leftovers and
        // oddities, and a preview built from them showed four effects nobody recognises — the
        // point of a preview is that it looks like a frame in a real fight (Florian,
        // 2026-09-13). PartyListPriority is the game saying which effects it would actually
        // put on a party list, which is exactly the question.
        var picked = new uint[4];
        var benefits = new uint[4];
        var pickedRank = new byte[picked.Length];
        var benefitRank = new byte[benefits.Length];

        foreach (Lumina.Excel.Sheets.Status row in sheet)
        {
            if (row.Icon == 0 || row.PartyListPriority == 0)
            {
                continue;
            }

            switch (row.StatusCategory)
            {
                case 1:
                    Rank(benefits, benefitRank, 0, benefits.Length, row.RowId, row.PartyListPriority);
                    break;

                case 2:
                    // Half the row cleansable and half not, so both looks are on screen: the
                    // first two slots are kept for effects Esuna takes off.
                    if (row.CanDispel)
                    {
                        Rank(picked, pickedRank, 0, 2, row.RowId, row.PartyListPriority);
                    }
                    else
                    {
                        Rank(picked, pickedRank, 2, 4, row.RowId, row.PartyListPriority);
                    }

                    break;
            }
        }

        Preview = picked;
        PreviewBuffs = benefits;

        for (int i = 0; i < Invulnerabilities.Length && PreviewInvulnerability == 0; i++)
        {
            if (Of(Invulnerabilities[i]).Icon != 0)
            {
                PreviewInvulnerability = Invulnerabilities[i];
            }
        }
    }

    /// <summary>
    /// Writes what the sheet holds about every effect currently on the player, to the log.
    /// <para>
    /// A diagnostic, reached by <c>/wisp status</c>. It was written to answer one question —
    /// can the game's own data tell a mitigation from any other benefit? — and that question is
    /// now answered: <b>no</b>. Measured 2026-09-18 on five Gunbreaker defensives at once,
    /// <c>ParamModifier</c> reads -10 for a 15%, a 20% and a 30% reduction alike, +10 for
    /// Camouflage and 0 for Superbolide, so it carries neither the kind nor the amount, and
    /// <c>ParamEffect</c> is 0 throughout. The <c>Status</c> sheet has no defensive flag.
    /// Florian chose not to keep a hand list, so there is no mitigation row and will not be one.
    /// </para>
    /// <para>
    /// It stays because it earned its keep on other questions three times over: it reads the
    /// whole party, every effect with its sheet fields, and the target's effects too. Extend it
    /// when the next question needs live data rather than writing a throwaway dump.
    /// </para>
    /// </summary>
    public static void DumpPlayerStatuses()
    {
        var player = Services.Objects.LocalPlayer;

        if (player is null)
        {
            Services.Log.Information("No player to read.");
            return;
        }

        Lumina.Excel.ExcelSheet<Lumina.Excel.Sheets.Status>? sheet =
            Services.Data.GetExcelSheet<Lumina.Excel.Sheets.Status>();

        if (sheet is null)
        {
            Services.Log.Information("No status sheet.");
            return;
        }

        DumpParty(player);

        var buffer = new NativeUi.StatusEntry[NativeUi.StatusCapacity];
        int ours = NativeUi.ReadCharacterStatuses(player.Address, buffer);

        // 🔴 Both counts, always. "Nothing on you" and "our read is broken" look identical
        // from one number, and a diagnostic that cannot tell them apart is worse than none —
        // it invites a conclusion from a zero (Florian, 2026-09-13, reporting exactly that
        // zero while standing about with no buffs).
        // 🔴 Against Dalamud's FILLED entries, not its Length. Length is NumValidStatuses,
        // which turns out to be the size of the slot table rather than a count of what is in
        // it — so comparing against it reported a disagreement on a character with three
        // effects and cost a round chasing a bug that was not there (2026-09-13). A
        // diagnostic that cries wolf is worse than no diagnostic.
        int slots = player.StatusList.Length;
        int filled = 0;

        for (int i = 0; i < slots; i++)
        {
            if (player.StatusList[i] is { StatusId: not 0 })
            {
                filled++;
            }
        }

        Services.Log.Information(
            "--- effects on {Name}: {Ours} read directly, {Filled} through Dalamud ({Slots} slots) ---",
            player.Name.TextValue,
            ours,
            filled,
            slots);

        if (ours != filled)
        {
            Services.Log.Warning(
                "The two disagree, so the direct read the frames use is wrong — not the effects.");
        }

        for (int i = 0; i < ours; i++)
        {
            uint id = buffer[i].StatusId;

            if (!sheet.TryGetRow(id, out Lumina.Excel.Sheets.Status row))
            {
                Services.Log.Information("{Id} — not in the sheet.", id);
                continue;
            }

            Services.Log.Information(
                "{Id} {Name} | cat {Category} | prio {Priority} | paramEffect {ParamEffect} | paramMod {ParamModifier} | param {Param} | fc {Fc} | permanent {Permanent} | canRemove {CanRemove} | dispel {Dispel} | left {Left:0.0}s",
                id,
                row.Name.ExtractText(),
                row.StatusCategory,
                row.PartyListPriority,
                row.ParamEffect,
                row.ParamModifier,
                buffer[i].Param,
                row.IsFcBuff,
                row.IsPermanent,
                row.CanStatusOff,
                row.CanDispel,
                buffer[i].Remaining);
        }

        // The target too, when there is one — a tank with cooldowns up is the case this was
        // written for, and standing on your own tells us nothing about mitigation.
        if (Services.Targets.Target is Dalamud.Game.ClientState.Objects.Types.IBattleChara target)
        {
            int onTarget = NativeUi.ReadCharacterStatuses(target.Address, buffer);

            Services.Log.Information(
                "--- effects on target {Name}: {Count} ---",
                target.Name.TextValue,
                onTarget);

            for (int i = 0; i < onTarget; i++)
            {
                uint id = buffer[i].StatusId;

                if (!sheet.TryGetRow(id, out Lumina.Excel.Sheets.Status row))
                {
                    continue;
                }

                Services.Log.Information(
                    "{Id} {Name} | cat {Category} | prio {Priority} | paramEffect {ParamEffect} | paramMod {ParamModifier} | param {Param}",
                    id,
                    row.Name.ExtractText(),
                    row.StatusCategory,
                    row.PartyListPriority,
                    row.ParamEffect,
                    row.ParamModifier,
                    buffer[i].Param);
            }
        }
    }

    /// <summary>
    /// What the party list says about where everybody is.
    /// <para>
    /// 🔴 Written because "out of range" was guessed at three times running. The frames tell
    /// somebody too far to help from somebody standing beside you by measuring the distance —
    /// and a party of eight none of whom could be targeted came back undimmed anyway. Whether
    /// that is because the positions are empty, or because they are real and near, or because
    /// the zone check catches it first, is not answerable by reading the code again.
    /// </para>
    /// </summary>
    private static void DumpParty(Dalamud.Game.ClientState.Objects.SubKinds.IPlayerCharacter player)
    {
        var party = Services.Party;
        System.Numerics.Vector3 from = player.Position;

        Services.Log.Information(
            "--- party: {Count} slot(s), you at {X:0.0}/{Y:0.0}/{Z:0.0} in territory {Here} ---",
            party.Length,
            from.X,
            from.Y,
            from.Z,
            Services.ClientState.TerritoryType);

        for (int i = 0; i < party.Length; i++)
        {
            var member = party[i];

            if (member is null)
            {
                Services.Log.Information("  [{Slot}] empty", i);
                continue;
            }

            System.Numerics.Vector3 at = member.Position;
            float dx = at.X - from.X;
            float dy = at.Y - from.Y;
            float dz = at.Z - from.Z;
            double distance = System.Math.Sqrt((dx * dx) + (dy * dy) + (dz * dz));

            Services.Log.Information(
                "  [{Slot}] {Name} | job {Job} | territory {Territory} | hp {Hp}/{MaxHp} | pos {X:0.0}/{Y:0.0}/{Z:0.0} | distance {Distance:0.0} | object {Object}",
                i,
                member.Name.TextValue,
                member.ClassJob.RowId,
                member.Territory.RowId,
                member.CurrentHP,
                member.MaxHP,
                at.X,
                at.Y,
                at.Z,
                distance,
                member.GameObject is null ? "none" : "loaded");
        }
    }

    /// <summary>
    /// Keeps the highest ranked few in a small run of slots, in order. Called once per sheet
    /// row at load, over four slots — a full sort would be more code for the same answer.
    /// </summary>
    private static void Rank(uint[] into, byte[] ranks, int from, int to, uint id, byte priority)
    {
        for (int i = from; i < to; i++)
        {
            if (into[i] != 0 && ranks[i] >= priority)
            {
                continue;
            }

            // Push the rest along and drop whatever fell off the end.
            for (int j = to - 1; j > i; j--)
            {
                into[j] = into[j - 1];
                ranks[j] = ranks[j - 1];
            }

            into[i] = id;
            ranks[i] = priority;
            return;
        }
    }

    /// <summary>
    /// What is known about a status id. An id past the end of the sheet — which a patch can
    /// hand us before the data catches up — comes back empty rather than throwing.
    /// </summary>
    public static StatusFacts Of(uint statusId) =>
        statusId < (uint)s_facts.Length ? s_facts[statusId] : default;

    /// <summary>Whether this effect means the person cannot be killed while it lasts.</summary>
    public static bool IsInvulnerability(uint statusId) =>
        statusId < (uint)s_invulnerable.Length && s_invulnerable[statusId];

    /// <summary>An affliction, in the game's own numbering. Anything else is not one.</summary>
    public static bool IsAffliction(uint statusId) => Of(statusId).Category == 2;
}
