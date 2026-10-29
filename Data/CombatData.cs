using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;

namespace WispUI.Data;

// One "CombatData" push from IINACT: encounter totals + one entry per combatant.
public class CombatEvent
{
    [JsonProperty("type")]
    public string Type { get; set; } = string.Empty;

    [JsonProperty("isActive")]
    public string IsActive { get; set; } = string.Empty;

    [JsonProperty("Encounter")]
    public Encounter? Encounter { get; set; }

    [JsonProperty("Combatant")]
    public Dictionary<string, Combatant>? Combatants { get; set; }

    [JsonIgnore]
    public bool Active => bool.TryParse(this.IsActive, out bool a) && a;

    /// <summary>The skills and seconds of this fight, from the log lines. None for Overall or test data.</summary>
    [JsonIgnore]
    internal FightLog? Log { get; set; }

    // Two finished snapshots of the same fight look identical here; used to de-dup history.
    public bool SameAs(CombatEvent? other)
    {
        if (other?.Encounter is null || this.Encounter is null)
        {
            return false;
        }

        return this.Encounter.Title == other.Encounter.Title
            && this.Encounter.Duration == other.Encounter.Duration;
    }

    // Builds a synthetic "Overall" event by summing every finished encounter.
    public static CombatEvent BuildOverall(List<CombatEvent> events)
    {
        double totalSeconds = 0;
        foreach (CombatEvent ev in events)
        {
            totalSeconds += FightSeconds(ev.Encounter);
        }

        if (totalSeconds <= 0)
        {
            totalSeconds = 1;
        }

        Dictionary<string, Acc> agg = new();
        foreach (CombatEvent ev in events)
        {
            if (ev.Combatants is null)
            {
                continue;
            }

            foreach (Combatant c in ev.Combatants.Values)
            {
                if (string.IsNullOrEmpty(c.Name))
                {
                    continue;
                }

                if (!agg.TryGetValue(c.Name, out Acc? a))
                {
                    a = new Acc { Name = c.Name, Job = c.Job };
                    agg[c.Name] = a;
                }

                if (string.IsNullOrEmpty(a.Job) && !string.IsNullOrEmpty(c.Job))
                {
                    a.Job = c.Job;
                }

                a.Damage += c.DamageTotal;
                a.DamageTaken += c.DamageTaken;
                a.Healed += c.HealedTotal;
                a.HealingTaken += c.HealingTaken;
                a.Deaths += c.DeathCount;
                a.Hits += c.Hits;
                a.Swings += c.Swings;
                a.Misses += c.Misses;
                a.Crits += c.CritHits;
                a.DirectHits += c.DirectHits;
                a.CritDirectHits += c.CritDirectHits;
                a.Heals += c.Heals;
                a.CritHeals += c.CritHeals;
                a.OverHeal += c.OverHeal;
                a.Shields += c.Shields;
                a.MaxHit = MathF.Max(a.MaxHit, c.MaxHit);
                a.MaxHeal = MathF.Max(a.MaxHeal, c.MaxHeal);
            }
        }

        string dur = FormatDuration(totalSeconds);
        Dictionary<string, Combatant> combatants = new();
        float encDamage = 0;
        float encHealed = 0;
        foreach (Acc a in agg.Values)
        {
            encDamage += a.Damage;
            encHealed += a.Healed;
            combatants[a.Name] = new Combatant
            {
                Name = a.Name,
                Job = a.Job,
                DamageRaw = Str(a.Damage),
                DamageTakenRaw = Str(a.DamageTaken),
                HealedRaw = Str(a.Healed),
                HealsTakenRaw = Str(a.HealingTaken),
                Deaths = ((int)a.Deaths).ToString(),
                DpsRaw = Str((float)(a.Damage / totalSeconds)),
                HpsRaw = Str((float)(a.Healed / totalSeconds)),
                HitsRaw = Str(a.Hits),
                SwingsRaw = Str(a.Swings),
                MissesRaw = Str(a.Misses),
                CritHitsRaw = Str(a.Crits),
                DirectHitsRaw = Str(a.DirectHits),
                CritDirectHitsRaw = Str(a.CritDirectHits),
                HealsRaw = Str(a.Heals),
                CritHealsRaw = Str(a.CritHeals),
                OverHealRaw = Str(a.OverHeal),
                ShieldsRaw = Str(a.Shields),
                MaxHitRaw = Str(a.MaxHit),
                MaxHealRaw = Str(a.MaxHeal),
            };
        }

        return new CombatEvent
        {
            Type = "CombatData",
            IsActive = "false",
            Encounter = new Encounter
            {
                Title = "Overall",
                Duration = dur,
                DamageRaw = Str(encDamage),
                HealedRaw = Str(encHealed),
                DpsRaw = Str((float)(encDamage / totalSeconds)),
                HpsRaw = Str((float)(encHealed / totalSeconds)),
            },
            Combatants = combatants,
        };
    }

    private sealed class Acc
    {
        public string Name = string.Empty;
        public string Job = string.Empty;
        public float Damage;
        public float DamageTaken;
        public float Healed;
        public float HealingTaken;
        public float Deaths;
        public float Hits;
        public float Swings;
        public float Misses;
        public float Crits;
        public float DirectHits;
        public float CritDirectHits;
        public float Heals;
        public float CritHeals;
        public float OverHeal;
        public float Shields;
        public float MaxHit;
        public float MaxHeal;
    }

    // Precise fight length: the rounded mm:ss text loses sub-second precision
    // (a <1s burst shows "00:00"), so derive it from damage / encdps instead.
    private static double FightSeconds(Encounter? enc)
    {
        if (enc is null)
        {
            return 0;
        }

        float dmg = Num.Parse(enc.DamageRaw);
        float dps = Num.Parse(enc.DpsRaw);
        if (dps > 0 && dmg > 0)
        {
            return dmg / dps;
        }

        float healed = Num.Parse(enc.HealedRaw);
        float hps = Num.Parse(enc.HpsRaw);
        if (hps > 0 && healed > 0)
        {
            return healed / hps;
        }

        return ParseDuration(enc.Duration);
    }

    private static string Str(float v) => v.ToString("0.######", CultureInfo.InvariantCulture);

    public static double ParseDuration(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return 0;
        }

        string[] p = raw.Split(':');
        try
        {
            if (p.Length == 2)
            {
                return (int.Parse(p[0], CultureInfo.InvariantCulture) * 60)
                    + double.Parse(p[1], CultureInfo.InvariantCulture);
            }

            if (p.Length == 3)
            {
                return (int.Parse(p[0], CultureInfo.InvariantCulture) * 3600)
                    + (int.Parse(p[1], CultureInfo.InvariantCulture) * 60)
                    + double.Parse(p[2], CultureInfo.InvariantCulture);
            }
        }
        catch (FormatException)
        {
            return 0;
        }

        return 0;
    }

    public static string FormatDuration(double seconds)
    {
        TimeSpan ts = TimeSpan.FromSeconds(seconds);
        return ts.TotalHours >= 1
            ? $"{(int)ts.TotalHours:00}:{ts.Minutes:00}:{ts.Seconds:00}"
            : $"{ts.Minutes:00}:{ts.Seconds:00}";
    }
}

public class Encounter
{
    [JsonProperty("title")]
    public string Title { get; set; } = string.Empty;

    [JsonProperty("duration")]
    public string Duration { get; set; } = string.Empty;

    [JsonProperty("encdps")]
    public string DpsRaw { get; set; } = string.Empty;

    [JsonProperty("enchps")]
    public string HpsRaw { get; set; } = string.Empty;

    [JsonProperty("damage")]
    public string DamageRaw { get; set; } = string.Empty;

    [JsonProperty("healed")]
    public string HealedRaw { get; set; } = string.Empty;

    // 🔴 IINACT sends most fields twice, once in capitals ("duration" = "01:05" and
    // "DURATION" = "65"). A name declared once is matched case-insensitively, so the capital
    // twin, arriving later, overwrote the real one: the title said "65" and DPS lost its
    // decimals (seen in a live dump, 2026-10-29). Each twin gets a property of its own, which
    // an exact match takes before any case-insensitive one.
    [JsonProperty("DURATION")]
    public string DurationSecondsRaw { get; set; } = string.Empty;

    [JsonProperty("ENCDPS")]
    public string DpsRounded { get; set; } = string.Empty;

    [JsonProperty("ENCHPS")]
    public string HpsRounded { get; set; } = string.Empty;
}

public class Combatant
{
    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("Job")]
    public string Job { get; set; } = string.Empty;

    [JsonProperty("encdps")]
    public string DpsRaw { get; set; } = string.Empty;

    [JsonProperty("enchps")]
    public string HpsRaw { get; set; } = string.Empty;

    [JsonProperty("damage")]
    public string DamageRaw { get; set; } = string.Empty;

    [JsonProperty("damagetaken")]
    public string DamageTakenRaw { get; set; } = string.Empty;

    [JsonProperty("healed")]
    public string HealedRaw { get; set; } = string.Empty;

    [JsonProperty("healstaken")]
    public string HealsTakenRaw { get; set; } = string.Empty;

    [JsonProperty("deaths")]
    public string Deaths { get; set; } = string.Empty;

    // The capital twins, so they do not overwrite the real fields (see Encounter).
    [JsonProperty("DURATION")]
    public string DurationSecondsRaw { get; set; } = string.Empty;

    [JsonProperty("ENCDPS")]
    public string DpsRounded { get; set; } = string.Empty;

    [JsonProperty("ENCHPS")]
    public string HpsRounded { get; set; } = string.Empty;

    // What the details read: counts rather than IINACT's own percent texts, so they add up
    // into Overall and are formatted once, our way.
    [JsonProperty("hits")]
    public string HitsRaw { get; set; } = string.Empty;

    [JsonProperty("swings")]
    public string SwingsRaw { get; set; } = string.Empty;

    [JsonProperty("misses")]
    public string MissesRaw { get; set; } = string.Empty;

    [JsonProperty("crithits")]
    public string CritHitsRaw { get; set; } = string.Empty;

    [JsonProperty("DirectHitCount")]
    public string DirectHitsRaw { get; set; } = string.Empty;

    [JsonProperty("CritDirectHitCount")]
    public string CritDirectHitsRaw { get; set; } = string.Empty;

    [JsonProperty("heals")]
    public string HealsRaw { get; set; } = string.Empty;

    [JsonProperty("critheals")]
    public string CritHealsRaw { get; set; } = string.Empty;

    [JsonProperty("overHeal")]
    public string OverHealRaw { get; set; } = string.Empty;

    [JsonProperty("damageShield")]
    public string ShieldsRaw { get; set; } = string.Empty;

    /// <summary>"22620" — the biggest hit alone; its lower-case twin carries the skill's name too.</summary>
    [JsonProperty("MAXHIT")]
    public string MaxHitRaw { get; set; } = string.Empty;

    [JsonProperty("maxhit")]
    public string MaxHitNamed { get; set; } = string.Empty;

    [JsonProperty("MAXHEAL")]
    public string MaxHealRaw { get; set; } = string.Empty;

    [JsonProperty("maxheal")]
    public string MaxHealNamed { get; set; } = string.Empty;

    /// <summary>"0%" — only as a share, and only for a single fight (Overall has none).</summary>
    [JsonProperty("ParryPct")]
    public string ParryPct { get; set; } = string.Empty;

    [JsonProperty("BlockPct")]
    public string BlockPct { get; set; } = string.Empty;

    // Parsed values are cached on first access: IINACT sends them as strings, and the
    // render loop reads them many times per frame, so we parse each one only once.
    [JsonIgnore]
    private float? m_dps;
    [JsonIgnore]
    private float? m_hps;
    [JsonIgnore]
    private float? m_damageTotal;
    [JsonIgnore]
    private float? m_damageTaken;
    [JsonIgnore]
    private float? m_healedTotal;
    [JsonIgnore]
    private float? m_healingTaken;
    [JsonIgnore]
    private int? m_deathCount;

    [JsonIgnore]
    public float Dps => m_dps ??= Num.Parse(this.DpsRaw);

    [JsonIgnore]
    public float Hps => m_hps ??= Num.Parse(this.HpsRaw);

    [JsonIgnore]
    public float DamageTotal => m_damageTotal ??= Num.Parse(this.DamageRaw);

    [JsonIgnore]
    public float DamageTaken => m_damageTaken ??= Num.Parse(this.DamageTakenRaw);

    [JsonIgnore]
    public float HealedTotal => m_healedTotal ??= Num.Parse(this.HealedRaw);

    [JsonIgnore]
    public float HealingTaken => m_healingTaken ??= Num.Parse(this.HealsTakenRaw);

    [JsonIgnore]
    public int DeathCount => m_deathCount ??= (int.TryParse(this.Deaths, out int d) ? d : 0);

    // Read by the details only, which build once per fight update: parsed on demand.
    [JsonIgnore]
    public float Hits => Num.Parse(this.HitsRaw);

    [JsonIgnore]
    public float Swings => Num.Parse(this.SwingsRaw);

    [JsonIgnore]
    public float Misses => Num.Parse(this.MissesRaw);

    [JsonIgnore]
    public float CritHits => Num.Parse(this.CritHitsRaw);

    [JsonIgnore]
    public float DirectHits => Num.Parse(this.DirectHitsRaw);

    [JsonIgnore]
    public float CritDirectHits => Num.Parse(this.CritDirectHitsRaw);

    [JsonIgnore]
    public float Heals => Num.Parse(this.HealsRaw);

    [JsonIgnore]
    public float CritHeals => Num.Parse(this.CritHealsRaw);

    [JsonIgnore]
    public float OverHeal => Num.Parse(this.OverHealRaw);

    [JsonIgnore]
    public float Shields => Num.Parse(this.ShieldsRaw);

    [JsonIgnore]
    public float MaxHit => Num.Parse(this.MaxHitRaw);

    [JsonIgnore]
    public float MaxHeal => Num.Parse(this.MaxHealRaw);
}

internal static class Num
{
    public static float Parse(string? s)
    {
        if (float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float v))
        {
            return v;
        }

        // 🔴 IINACT formats decimals in the culture of the player's Windows: a German system
        // sends "3964,79" (seen in a live dump, 2026-10-29), which the invariant parse rejects
        // outright. Whole numbers ("damage") are unaffected. Only reached on the rare value
        // with a comma, and every combatant caches what it parsed, so the copy is once a push.
        if (s is null || s.IndexOf(',') < 0)
        {
            return 0f;
        }

        string fixedUp = s.IndexOf('.') < 0 ? s.Replace(',', '.') : s.Replace(",", string.Empty);
        return float.TryParse(fixedUp, NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? v : 0f;
    }
}
