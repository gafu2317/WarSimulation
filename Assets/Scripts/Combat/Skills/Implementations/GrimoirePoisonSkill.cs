using UnityEngine;

public sealed class GrimoirePoisonSkill : SkillBase
{
    private readonly float _maxRange;
    private readonly float _intScalePerTick;
    private readonly float _durationSeconds;
    private readonly float _tickIntervalSeconds;
    private readonly float _cooldownSeconds;

    public GrimoirePoisonSkill(
        float maxRange = 25f,
        float intScalePerTick = 0.1f,
        float durationSeconds = 5f,
        float tickIntervalSeconds = 1f,
        float cooldownSeconds = 6f)
    {
        _maxRange = maxRange;
        _intScalePerTick = intScalePerTick;
        _durationSeconds = durationSeconds;
        _tickIntervalSeconds = tickIntervalSeconds;
        _cooldownSeconds = cooldownSeconds;
    }

    public override string Name => "毒";

    public override string EffectDescription =>
        $"毒 INT × {_intScalePerTick:0.###}/{_tickIntervalSeconds:0.##}秒 × {_durationSeconds:0.##}秒";
    public override float CooldownSeconds => _cooldownSeconds;
    public override float CastTimeSeconds => 1.1f;
    public override float MaxRange => _maxRange;

    public override void Execute(Character self, SkillExecutionContext context)
    {
        Character target = context.PrimaryTarget;
        if (self == null || target == null || target.Health == null) return;
        if (!target.Health.IsTargetable) return;
        context = context.Capture(self);

        int damagePerTick = Mathf.Max(
            1,
            Mathf.RoundToInt(context.GetEffectiveStat(CombatStat.INT) * _intScalePerTick));
        target.StatusEffects?.ApplyPoison(
            damagePerTick,
            _durationSeconds,
            _tickIntervalSeconds,
            "GrimoirePoison",
            self);
    }
}
