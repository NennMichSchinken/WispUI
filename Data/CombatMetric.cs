namespace WispUI.Data;

using WispUI.Localization;

/// <summary>
/// What a meter is counting. Named <c>CombatMetric</c> rather than <c>Metric</c> because
/// <see cref="WispUI.Style.Tokens.Metric"/> already holds every measurement in the suite, and
/// the renderer needs both in the same file.
/// </summary>
public enum CombatMetric
{
    DamageDone,
    DamageTaken,
    HealingDone,
    HealingTaken,
    Deaths,
}

public static class CombatMetrics
{
    public static readonly CombatMetric[] All =
    {
        CombatMetric.DamageDone,
        CombatMetric.DamageTaken,
        CombatMetric.HealingDone,
        CombatMetric.HealingTaken,
        CombatMetric.Deaths,
    };

    /// <summary>What the title bar calls it. From Strings, like every other word on screen.</summary>
    public static string Name(CombatMetric m) => m switch
    {
        CombatMetric.DamageTaken => Strings.MetricDamageTaken,
        CombatMetric.HealingDone => Strings.MetricHealingDone,
        CombatMetric.HealingTaken => Strings.MetricHealingTaken,
        CombatMetric.Deaths => Strings.MetricDeaths,
        _ => Strings.MetricDamageDone,
    };

    /// <summary>The number the bar length is a fraction of.</summary>
    public static float Value(Combatant c, CombatMetric m) => m switch
    {
        CombatMetric.DamageTaken => c.DamageTaken,
        CombatMetric.HealingDone => c.HealedTotal,
        CombatMetric.HealingTaken => c.HealingTaken,
        CombatMetric.Deaths => c.DeathCount,
        _ => c.DamageTotal,
    };

    /// <summary>
    /// The per-second rate in brackets after the total, where there is one. Damage taken has
    /// no rate worth reading — it is a thing that happened to you, not a thing you sustained.
    /// </summary>
    public static float Rate(Combatant c, CombatMetric m) => m switch
    {
        CombatMetric.DamageDone => c.Dps,
        CombatMetric.HealingDone => c.Hps,
        _ => 0f,
    };

    public static bool HasRate(CombatMetric m) => m is CombatMetric.DamageDone or CombatMetric.HealingDone;

    /// <summary>A plain tally rather than an amount — never shortened to "1.2k".</summary>
    public static bool IsCount(CombatMetric m) => m is CombatMetric.Deaths;
}
