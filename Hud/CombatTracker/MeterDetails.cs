using System;
using System.Collections.Generic;
using System.Globalization;
using WispUI.Core;
using WispUI.Data;
using WispUI.Localization;

namespace WispUI.Hud.CombatTracker;

/// <summary>One line of a skill list, with everything it says already written out.</summary>
internal struct SkillLine
{
    /// <summary>The action, for its description on hover; zero for a tick row.</summary>
    public uint ActionId;
    public SkillKind Kind;
    public string Name;
    public uint IconId;
    public long Amount;

    /// <summary>The bar's length, against the biggest skill.</summary>
    public float Fraction;

    public string AmountText;
    public string Share;
    public string Hits;
    public string Crit;

    /// <summary>Direct hits when it is damage, overheal when it is healing.</summary>
    public string Second;

    public string Average;
    public string Biggest;
}

/// <summary>One of the eight rate tiles.</summary>
internal struct RateTile
{
    public string Label;
    public string Value;
    public bool Known;
}

/// <summary>One player of the fight, as a chip at the top of the details.</summary>
internal struct PlayerChip
{
    /// <summary>The name IINACT gives: "YOU" for the local player.</summary>
    public string Key;
    public string Name;
    public string Amount;
    public uint JobId;
}

/// <summary>One curve of the chart, smoothed and ready to draw.</summary>
internal sealed class ChartLine
{
    public float[] Values = Array.Empty<float>();
    public uint JobId;
    public string Name = string.Empty;
    public bool Mine;
}

/// <summary>
/// Everything the details say about one player in one fight: worked out from IINACT's totals
/// and from the log lines, once each time the fight data changes, never per frame (§7.1).
/// The drawing only reads these fields.
/// </summary>
internal sealed class DetailData
{
    public const int TileCount = 8;

    /// <summary>The seconds the chart's line is averaged over, so it follows the fight without jumping with every hit.</summary>
    private const int SmoothSeconds = 5;

    private static readonly Dictionary<uint, uint> ActionIcons = new();

    public string Name = string.Empty;
    public string Subtitle = string.Empty;
    public uint JobId;
    public bool Heal;
    public string TotalValue = string.Empty;
    public string RateValue = string.Empty;

    public readonly RateTile[] Tiles = new RateTile[TileCount];
    public readonly List<PlayerChip> Chips = new();
    public readonly List<SkillLine> Skills = new();

    /// <summary>"Overall", where the log lines of every fight are added up; there is no chart then.</summary>
    public bool Overall;

    /// <summary>The fight has no log lines at all — test data, or a fight from before the meter listened.</summary>
    public bool NoLog;

    public readonly List<ChartLine> Lines = new();
    public float[] Boss = Array.Empty<float>();
    public int Length;
    public float MaxMine;
    public float MaxAll;
    public string EndTime = string.Empty;

    /// <summary>
    /// Fills this from a fight. <paramref name="you"/> is the local player's name, which IINACT
    /// writes as "YOU" and the log lines write as the name.
    /// </summary>
    public void Build(
        CombatEvent fight,
        List<CombatEvent>? overallParts,
        string playerKey,
        string? you,
        bool heal,
        bool shortNumbers,
        CombatLog log)
    {
        this.Heal = heal;
        this.Overall = overallParts is not null;
        this.Chips.Clear();
        this.Skills.Clear();
        this.Lines.Clear();
        this.Boss = Array.Empty<float>();
        this.Length = 0;
        this.MaxMine = 0f;
        this.MaxAll = 0f;

        Combatant? me = null;

        if (fight.Combatants is not null)
        {
            foreach (Combatant c in fight.Combatants.Values)
            {
                uint jobId = Jobs.FromAbbreviation(c.Job);

                if (jobId == 0)
                {
                    continue;
                }

                if (string.Equals(c.Name, playerKey, StringComparison.Ordinal))
                {
                    me = c;
                }

                this.Chips.Add(new PlayerChip
                {
                    Key = c.Name,
                    Name = Display(c.Name, you),
                    Amount = CombatTrackerElement.Number(heal ? c.HealedTotal : c.DamageTotal, shortNumbers),
                    JobId = jobId,
                });
            }
        }

        // Biggest first, by insertion: a party is eight chips.
        for (int i = 1; i < this.Chips.Count; i++)
        {
            PlayerChip chip = this.Chips[i];
            float amount = Amount(fight, chip.Name, you, heal);
            int at = i;

            while (at > 0 && Amount(fight, this.Chips[at - 1].Name, you, heal) < amount)
            {
                this.Chips[at] = this.Chips[at - 1];
                at--;
            }

            this.Chips[at] = chip;
        }

        string name = Display(playerKey, you);
        this.Name = name;
        this.JobId = me is null ? 0u : Jobs.FromAbbreviation(me.Job);

        int jobIndex = JobList.IndexOf(this.JobId);
        string jobName = jobIndex >= 0 ? JobList.At(jobIndex).Name : string.Empty;
        string title = fight.Encounter?.Title ?? string.Empty;
        string duration = fight.Encounter?.Duration ?? string.Empty;
        this.Subtitle = jobName.Length > 0
            ? jobName + " · " + title + " · " + duration
            : title + " · " + duration;

        this.TotalValue = me is null ? "—" : CombatTrackerElement.Number(heal ? me.HealedTotal : me.DamageTotal, shortNumbers);
        this.RateValue = me is null ? "—" : CombatTrackerElement.Number(heal ? me.Hps : me.Dps, shortNumbers);

        this.BuildTiles(me, heal, shortNumbers);

        lock (log.Sync)
        {
            this.NoLog = true;

            if (overallParts is not null)
            {
                var skills = new Dictionary<uint, SkillTotals>();

                foreach (CombatEvent part in overallParts)
                {
                    if (part.Log is not null)
                    {
                        this.NoLog = false;
                        Gather(part.Log, log, name, heal, skills, null);
                    }
                }

                this.BuildSkills(skills, heal, shortNumbers);
                return;
            }

            if (fight.Log is null)
            {
                return;
            }

            this.NoLog = false;
            var mine = new Dictionary<uint, SkillTotals>();
            Gather(fight.Log, log, name, heal, mine, null);
            this.BuildSkills(mine, heal, shortNumbers);
            this.BuildChart(fight.Log, log, name, heal);
        }
    }

    private void BuildTiles(Combatant? c, bool heal, bool shortNumbers)
    {
        if (heal)
        {
            this.Tiles[0] = Share(Strings.DetailCritHeal, c?.CritHeals, c?.Heals);
            this.Tiles[1] = Share(Strings.DetailOverheal, c?.OverHeal, c?.HealedTotal);
            this.Tiles[2] = Amount(Strings.DetailShields, c?.Shields, shortNumbers);
            this.Tiles[3] = Amount(Strings.DetailBiggestHeal, c?.MaxHeal, shortNumbers);
            this.Tiles[4] = Count(Strings.DetailHeals, c?.Heals);
            this.Tiles[5] = Amount(Strings.DetailHealingTaken, c?.HealingTaken, shortNumbers);
        }
        else
        {
            this.Tiles[0] = Share(Strings.DetailCrit, c?.CritHits, c?.Hits);
            this.Tiles[1] = Share(Strings.DetailDirectHit, c?.DirectHits, c?.Hits);
            this.Tiles[2] = Share(Strings.DetailCritDirectHit, c?.CritDirectHits, c?.Hits);
            this.Tiles[3] = Amount(Strings.DetailBiggestHit, c?.MaxHit, shortNumbers);
            this.Tiles[4] = Share(Strings.DetailMissed, c?.Misses, c?.Swings);

            // Only as IINACT's own share, and only for one fight: Overall adds up counts.
            bool known = c is not null && c.ParryPct.Length > 0;
            this.Tiles[5] = new RateTile
            {
                Label = Strings.DetailParryBlock,
                Value = known ? c!.ParryPct + " / " + c.BlockPct : "—",
                Known = known,
            };
        }

        this.Tiles[6] = Amount(Strings.DetailDamageTaken, c?.DamageTaken, shortNumbers);
        this.Tiles[7] = Count(Strings.DetailDeaths, c?.DeathCount);
    }

    private void BuildSkills(Dictionary<uint, SkillTotals> skills, bool heal, bool shortNumbers)
    {
        long total = 0;

        foreach (SkillTotals s in skills.Values)
        {
            total += s.Amount;
        }

        total = Math.Max(1, total);

        foreach (SkillTotals s in skills.Values)
        {
            if (s.Amount <= 0 && s.Hits == 0)
            {
                continue;
            }

            bool tick = s.Kind != SkillKind.Ability;
            string name = s.Kind switch
            {
                SkillKind.DamageOverTime => Strings.DetailDamageOverTime,
                SkillKind.HealOverTime => Strings.DetailHealOverTime,
                _ => s.Name.Length > 0 ? s.Name : s.ActionId.ToString(CultureInfo.InvariantCulture),
            };

            string second = heal
                ? Percent(s.OverHeal, s.Amount)
                : tick ? "—" : Percent(s.DirectHits, s.Hits);

            var line = new SkillLine
            {
                ActionId = s.ActionId,
                Kind = s.Kind,
                Name = name,
                IconId = tick ? 0u : IconOf(s.ActionId),
                Amount = s.Amount,
                AmountText = CombatTrackerElement.Number(s.Amount, shortNumbers),
                Share = ((double)s.Amount / total * 100d).ToString("0.0", CultureInfo.InvariantCulture) + "%",
                Hits = s.Hits.ToString(CultureInfo.InvariantCulture),
                Crit = tick ? "—" : Percent(s.Crits, s.Hits),
                Second = second,
                Average = s.Hits > 0 ? CombatTrackerElement.Number((float)s.Amount / s.Hits, shortNumbers) : "—",
                Biggest = CombatTrackerElement.Number(s.MaxHit, shortNumbers),
            };

            // Biggest first, by insertion: a rotation is a couple of dozen skills.
            int at = this.Skills.Count;
            this.Skills.Add(line);

            while (at > 0 && this.Skills[at - 1].Amount < line.Amount)
            {
                this.Skills[at] = this.Skills[at - 1];
                at--;
            }

            this.Skills[at] = line;
        }

        long top = this.Skills.Count > 0 ? Math.Max(1, this.Skills[0].Amount) : 1;

        for (int i = 0; i < this.Skills.Count; i++)
        {
            SkillLine line = this.Skills[i];
            line.Fraction = (float)line.Amount / top;
            this.Skills[i] = line;
        }
    }

    private void BuildChart(FightLog fight, CombatLog log, string name, bool heal)
    {
        foreach (PlayerChip chip in this.Chips)
        {
            var series = new List<float>();
            Gather(fight, log, chip.Name, heal, null, series);

            if (series.Count == 0)
            {
                continue;
            }

            this.Lines.Add(new ChartLine
            {
                Values = Smooth(series),
                JobId = chip.JobId,
                Name = chip.Name,
                Mine = string.Equals(chip.Name, name, StringComparison.Ordinal),
            });
        }

        this.Boss = fight.BossHp.ToArray();
        int length = this.Boss.Length;

        foreach (ChartLine line in this.Lines)
        {
            length = Math.Max(length, line.Values.Length);

            foreach (float v in line.Values)
            {
                this.MaxAll = MathF.Max(this.MaxAll, v);

                if (line.Mine)
                {
                    this.MaxMine = MathF.Max(this.MaxMine, v);
                }
            }
        }

        this.Length = length;
        this.EndTime = CombatEvent.FormatDuration(Math.Max(0, length - 1));
    }

    /// <summary>
    /// A player's skills and seconds in one fight, their pets' folded in. The log knows a
    /// player by name and id; a pet only by its own id, tied to its owner through
    /// <see cref="CombatLog.Owners"/>.
    /// </summary>
    private static void Gather(FightLog fight, CombatLog log, string name, bool heal, Dictionary<uint, SkillTotals>? skills, List<float>? series)
    {
        uint playerId = 0;

        foreach (ActorLog actor in fight.Actors.Values)
        {
            if (CombatLog.IsPlayer(actor.Id) && string.Equals(actor.Name, name, StringComparison.Ordinal))
            {
                playerId = actor.Id;
                break;
            }
        }

        if (playerId == 0)
        {
            return;
        }

        foreach (ActorLog actor in fight.Actors.Values)
        {
            bool mine = actor.Id == playerId
                || (actor.IsNpc && log.Owners.TryGetValue(actor.Id, out uint owner) && owner == playerId);

            if (!mine)
            {
                continue;
            }

            if (skills is not null)
            {
                foreach (KeyValuePair<uint, SkillTotals> pair in actor.Skills)
                {
                    if (pair.Value.Heal != heal)
                    {
                        continue;
                    }

                    if (!skills.TryGetValue(pair.Key, out SkillTotals? sum))
                    {
                        sum = new SkillTotals { ActionId = pair.Value.ActionId, Kind = pair.Value.Kind, Heal = heal, Name = pair.Value.Name };
                        skills[pair.Key] = sum;
                    }

                    sum.Amount += pair.Value.Amount;
                    sum.Hits += pair.Value.Hits;
                    sum.Crits += pair.Value.Crits;
                    sum.DirectHits += pair.Value.DirectHits;
                    sum.CritDirectHits += pair.Value.CritDirectHits;
                    sum.OverHeal += pair.Value.OverHeal;
                    sum.MaxHit = Math.Max(sum.MaxHit, pair.Value.MaxHit);
                }
            }

            if (series is not null)
            {
                List<float> from = heal ? actor.Healing : actor.Damage;

                for (int i = 0; i < from.Count; i++)
                {
                    if (i < series.Count)
                    {
                        series[i] += from[i];
                    }
                    else
                    {
                        series.Add(from[i]);
                    }
                }
            }
        }
    }

    /// <summary>Per second over the last few seconds: the curve follows the fight without jumping with every hit.</summary>
    private static float[] Smooth(List<float> perSecond)
    {
        var smooth = new float[perSecond.Count];
        float sum = 0f;

        for (int i = 0; i < perSecond.Count; i++)
        {
            sum += perSecond[i];

            if (i >= SmoothSeconds)
            {
                sum -= perSecond[i - SmoothSeconds];
            }

            smooth[i] = sum / Math.Min(SmoothSeconds, i + 1);
        }

        return smooth;
    }

    /// <summary>The game's icon for an action, from the sheet once and remembered.</summary>
    private static uint IconOf(uint actionId)
    {
        if (actionId == 0)
        {
            return 0;
        }

        if (!ActionIcons.TryGetValue(actionId, out uint icon))
        {
            icon = 0;
            var sheet = Services.Data.GetExcelSheet<Lumina.Excel.Sheets.Action>();

            if (sheet is not null && sheet.TryGetRow(actionId, out Lumina.Excel.Sheets.Action row))
            {
                icon = row.Icon;
            }

            ActionIcons[actionId] = icon;
        }

        return icon;
    }

    private static string Display(string name, string? you) =>
        string.Equals(name, "YOU", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(you) ? you : name;

    private static float Amount(CombatEvent fight, string displayName, string? you, bool heal)
    {
        if (fight.Combatants is null)
        {
            return 0f;
        }

        foreach (Combatant c in fight.Combatants.Values)
        {
            if (string.Equals(Display(c.Name, you), displayName, StringComparison.Ordinal))
            {
                return heal ? c.HealedTotal : c.DamageTotal;
            }
        }

        return 0f;
    }

    private static RateTile Share(string label, float? part, float? whole)
    {
        bool known = part is not null && whole is > 0f;
        return new RateTile
        {
            Label = label,
            Value = known ? Percent(part!.Value, whole!.Value) : "—",
            Known = known,
        };
    }

    private static RateTile Amount(string label, float? value, bool shortNumbers) => new()
    {
        Label = label,
        Value = value is null ? "—" : CombatTrackerElement.Number(value.Value, shortNumbers),
        Known = value is not null,
    };

    private static RateTile Count(string label, float? value) => new()
    {
        Label = label,
        Value = value is null ? "—" : ((int)value.Value).ToString(CultureInfo.InvariantCulture),
        Known = value is not null,
    };

    private static string Percent(double part, double whole) =>
        whole > 0 ? (part / whole * 100d).ToString("0", CultureInfo.InvariantCulture) + "%" : "—";
}
