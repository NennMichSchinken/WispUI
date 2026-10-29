using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json.Linq;

namespace WispUI.Data;

/// <summary>What one line of a skill list counts: an ability, or every tick over time together.</summary>
internal enum SkillKind
{
    Ability,
    DamageOverTime,
    HealOverTime,
}

/// <summary>One skill of one combatant in one fight.</summary>
internal sealed class SkillTotals
{
    public uint ActionId;
    public SkillKind Kind;
    public bool Heal;

    /// <summary>As the log line names it, in the client's language. Empty for a tick row.</summary>
    public string Name = string.Empty;

    public long Amount;
    public int Hits;
    public int Crits;
    public int DirectHits;
    public int CritDirectHits;
    public long MaxHit;
    public long OverHeal;
}

/// <summary>Everything one combatant did in one fight, by skill and by second.</summary>
internal sealed class ActorLog
{
    public uint Id;
    public string Name = string.Empty;
    public readonly Dictionary<uint, SkillTotals> Skills = new();
    public readonly List<float> Damage = new();
    public readonly List<float> Healing = new();

    /// <summary>A battle NPC: a pet, a summon — or an ally, which never resolves to an owner.</summary>
    public bool IsNpc => CombatLog.IsNpc(this.Id);
}

/// <summary>One fight as the log lines told it, matched to one of IINACT's encounters.</summary>
internal sealed class FightLog
{
    public FightLog(long startMs) => this.StartMs = startMs;

    public long StartMs { get; }

    public readonly Dictionary<uint, ActorLog> Actors = new();

    /// <summary>The enemy with the most health that the party hit: the one the boss line follows.</summary>
    public uint BossId;
    public long BossMaxHp;

    /// <summary>The boss's health per second, as a share of its maximum.</summary>
    public readonly List<float> BossHp = new();
}

/// <summary>
/// Reads IINACT's network log lines into fights: per combatant, per skill, per second — what
/// the meter's details show and what IINACT's own fight data does not carry.
/// <para>
/// The line format (checked against a live dump, 2026-10-29, numbers matched against the
/// target's health): 21/22 = ability (2 source id, 3 name, 4 action id, 5 action name, 6
/// target id, 8–23 eight pairs of effect flags and value, 24/25 target HP before the hit and
/// its maximum); 24 = a tick (2 target, 4 "DoT"/"HoT", 6 amount, 7/8 target HP, 17 source);
/// 03 = a combatant appears (6 owner). Ids starting with 1 are players, with 4 battle NPCs.
/// The tick line names no skill — the game sends its status id as 0 — so ticks are one row.
/// </para>
/// <para>
/// ⚠️ Runs on IINACT's thread, many lines a second in a pull. Everything here is touched under
/// <see cref="Sync"/>, which the meter takes when it builds its details (about once a second),
/// never in a draw.
/// </para>
/// </summary>
internal sealed class CombatLog
{
    // Effect types in the low byte of a flag word.
    private const uint EffectDamage = 0x03;
    private const uint EffectHeal = 0x04;
    private const uint EffectBlocked = 0x05;
    private const uint EffectParried = 0x06;

    // Damage: crit and direct hit in the second byte. Heal: crit in the third byte (a crit
    // Cure III came as 200004). All three checked in live dumps, 2026-10-29.
    private const uint DamageCrit = 0x2000;
    private const uint DamageDirect = 0x4000;
    private const uint HealCrit = 0x200000;

    /// <summary>A damage value with this bit in its third byte carries a fourth byte above the first two.</summary>
    private const uint BigDamage = 0x4000;

    /// <summary>Keys for the two tick rows, outside any action id.</summary>
    internal const uint KeyDamageOverTime = 0x7FFF_FFF0;
    internal const uint KeyHealOverTime = 0xFFFF_FFF0;

    /// <summary>Above every action id, to keep a skill's heal apart from its damage.</summary>
    private const uint HealKeyBit = 0x8000_0000;

    /// <summary>Two hours. A fight longer than that keeps its numbers, not its curve.</summary>
    private const int MaxSeconds = 7200;

    /// <summary>How much older than IINACT says a fight is, before the open log is taken as left over.</summary>
    private const long StaleSlackMs = 15_000;

    private FightLog? m_open;

    public object Sync { get; } = new();

    /// <summary>Pet → owner, from the log's own lines or from the game's objects. Kept across fights.</summary>
    public Dictionary<uint, uint> Owners { get; } = new();

    public static bool IsPlayer(uint id) => (id >> 28) == 1;

    public static bool IsNpc(uint id) => (id >> 28) == 4;

    /// <summary>The fight in progress, if a line has opened one. Read under <see cref="Sync"/>.</summary>
    public FightLog? Open => m_open;

    /// <summary>Takes one line. Called on IINACT's thread.</summary>
    public void Feed(JArray line)
    {
        if (line.Count == 0)
        {
            return;
        }

        string? type = (string?)line[0];

        lock (this.Sync)
        {
            switch (type)
            {
                case "21":
                case "22":
                    this.Ability(line);
                    break;
                case "24":
                    this.Tick(line);
                    break;
                case "03":
                    this.Appear(line);
                    break;
            }
        }
    }

    /// <summary>
    /// The log for a fight IINACT reports as running, <paramref name="seconds"/> long. Opens one
    /// if no line has yet, and drops one that is clearly older than the fight — ticks running
    /// on after the last fight closed, which IINACT did not take as a fight of their own.
    /// </summary>
    public FightLog ForActive(double seconds)
    {
        lock (this.Sync)
        {
            long now = Environment.TickCount64;
            long start = now - (long)(seconds * 1000d);

            if (m_open is null || m_open.StartMs < start - StaleSlackMs)
            {
                m_open = new FightLog(start);
            }

            return m_open;
        }
    }

    /// <summary>Closes the fight in progress, for IINACT has closed its encounter. The next damage opens a new one.</summary>
    public FightLog? Close()
    {
        lock (this.Sync)
        {
            FightLog? closed = m_open;
            m_open = null;
            return closed;
        }
    }

    public void Clear()
    {
        lock (this.Sync)
        {
            m_open = null;
        }
    }

    private void Ability(JArray line)
    {
        if (line.Count < 26)
        {
            return;
        }

        uint source = Hex(line[2]);
        uint target = Hex(line[6]);

        for (int pair = 0; pair < 8; pair++)
        {
            uint flags = Hex(line[8 + (pair * 2)]);
            uint effect = flags & 0xFF;

            if (effect is not (EffectDamage or EffectHeal or EffectBlocked or EffectParried))
            {
                continue;
            }

            uint value = Hex(line[9 + (pair * 2)]);
            long amount = Amount(value);
            long hp = Dec(line[24]);
            long maxHp = Dec(line[25]);
            uint actionId = Hex(line[4]);

            if (effect == EffectHeal)
            {
                if (!IsPlayer(target) || !(IsPlayer(source) || IsNpc(source)) || m_open is null)
                {
                    return;
                }

                SkillTotals heal = this.Skill(source, (string?)line[3], actionId | HealKeyBit, actionId, SkillKind.Ability, true, (string?)line[5]);
                Count(heal, amount, (flags & HealCrit) != 0, false);
                heal.OverHeal += Overheal(amount, hp, maxHp);
                this.PerSecond(source, amount, true);
                return;
            }

            if (!IsNpc(target) || source == target || !(IsPlayer(source) || IsNpc(source)))
            {
                return;
            }

            m_open ??= new FightLog(Environment.TickCount64);
            SkillTotals hit = this.Skill(source, (string?)line[3], actionId, actionId, SkillKind.Ability, false, (string?)line[5]);
            Count(hit, amount, (flags & DamageCrit) != 0, (flags & DamageDirect) != 0);
            this.PerSecond(source, amount, false);
            this.Boss(target, hp - amount, maxHp);
            return;
        }
    }

    private void Tick(JArray line)
    {
        if (line.Count < 19)
        {
            return;
        }

        uint target = Hex(line[2]);
        uint source = Hex(line[17]);
        long amount = Hex(line[6]);
        long hp = Dec(line[7]);
        long maxHp = Dec(line[8]);
        bool heal = string.Equals((string?)line[4], "HoT", StringComparison.Ordinal);

        if (!(IsPlayer(source) || IsNpc(source)))
        {
            return;
        }

        if (heal)
        {
            if (!IsPlayer(target) || m_open is null)
            {
                return;
            }

            SkillTotals hot = this.Skill(source, (string?)line[18], KeyHealOverTime, 0, SkillKind.HealOverTime, true, null);
            Count(hot, amount, false, false);
            hot.OverHeal += Overheal(amount, hp, maxHp);
            this.PerSecond(source, amount, true);
            return;
        }

        if (!IsNpc(target) || source == target)
        {
            return;
        }

        m_open ??= new FightLog(Environment.TickCount64);
        SkillTotals dot = this.Skill(source, (string?)line[18], KeyDamageOverTime, 0, SkillKind.DamageOverTime, false, null);
        Count(dot, amount, false, false);
        this.PerSecond(source, amount, false);
        this.Boss(target, hp - amount, maxHp);
    }

    private void Appear(JArray line)
    {
        if (line.Count < 7)
        {
            return;
        }

        uint id = Hex(line[2]);
        uint owner = Hex(line[6]);

        if (IsNpc(id) && IsPlayer(owner))
        {
            this.Owners[id] = owner;
        }
    }

    private SkillTotals Skill(uint source, string? sourceName, uint key, uint actionId, SkillKind kind, bool heal, string? actionName)
    {
        FightLog fight = m_open!;

        if (!fight.Actors.TryGetValue(source, out ActorLog? actor))
        {
            actor = new ActorLog { Id = source, Name = sourceName ?? string.Empty };
            fight.Actors[source] = actor;
        }

        if (!actor.Skills.TryGetValue(key, out SkillTotals? skill))
        {
            skill = new SkillTotals { ActionId = actionId, Kind = kind, Heal = heal, Name = actionName ?? string.Empty };
            actor.Skills[key] = skill;
        }

        return skill;
    }

    private static void Count(SkillTotals skill, long amount, bool crit, bool direct)
    {
        skill.Amount += amount;
        skill.Hits++;
        skill.MaxHit = Math.Max(skill.MaxHit, amount);

        if (crit && direct)
        {
            skill.CritDirectHits++;
        }

        if (crit)
        {
            skill.Crits++;
        }

        if (direct)
        {
            skill.DirectHits++;
        }
    }

    private void PerSecond(uint source, long amount, bool heal)
    {
        FightLog fight = m_open!;
        long second = (Environment.TickCount64 - fight.StartMs) / 1000;

        if (second < 0 || second >= MaxSeconds || !fight.Actors.TryGetValue(source, out ActorLog? actor))
        {
            return;
        }

        List<float> series = heal ? actor.Healing : actor.Damage;
        Grow(series, (int)second, 0f);
        series[(int)second] += amount;
    }

    private void Boss(uint target, long hpAfter, long maxHp)
    {
        FightLog fight = m_open!;

        if (maxHp <= 0)
        {
            return;
        }

        // A bigger enemy takes the line over, and its curve starts from where it is now.
        if (maxHp > fight.BossMaxHp)
        {
            fight.BossId = target;
            fight.BossMaxHp = maxHp;
            fight.BossHp.Clear();
        }

        if (target != fight.BossId)
        {
            return;
        }

        long second = (Environment.TickCount64 - fight.StartMs) / 1000;

        if (second < 0 || second >= MaxSeconds)
        {
            return;
        }

        float share = Math.Clamp((float)hpAfter / maxHp, 0f, 1f);

        // Seconds without a hit on it keep the last reading, so the line steps rather than drops.
        float last = fight.BossHp.Count > 0 ? fight.BossHp[^1] : share;
        Grow(fight.BossHp, (int)second, last);
        fight.BossHp[(int)second] = share;
    }

    private static void Grow(List<float> series, int index, float fill)
    {
        while (series.Count <= index)
        {
            series.Add(fill);
        }
    }

    private static long Overheal(long amount, long hp, long maxHp) =>
        maxHp > 0 ? Math.Max(0, amount - Math.Max(0, maxHp - hp)) : 0;

    /// <summary>
    /// The amount in an effect's value word AABBCCDD: AABB, or DDAABB when CC carries the big
    /// flag. The small form checked against the target's health, the big one against a crit
    /// Glare III: 4F3C4001 = 0x014F3C = 85,820 (both 2026-10-29).
    /// </summary>
    private static long Amount(uint value)
    {
        uint a = (value >> 24) & 0xFF;
        uint b = (value >> 16) & 0xFF;
        uint d = value & 0xFF;

        return (value & BigDamage) != 0 ? (d << 16) | (a << 8) | b : (a << 8) | b;
    }

    private static uint Hex(JToken token) =>
        uint.TryParse((string?)token, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint v) ? v : 0u;

    private static long Dec(JToken token) =>
        long.TryParse((string?)token, NumberStyles.Integer, CultureInfo.InvariantCulture, out long v) ? v : 0L;
}
