using UnityEngine;

public sealed class RosaryRegenerationSkill : SkillBase
{
    private readonly float _maxRange;
    private readonly float _faiScalePerTick;
    private readonly float _durationSeconds;
    private readonly float _tickIntervalSeconds;
    private readonly float _cooldownSeconds;

    public RosaryRegenerationSkill(
        float maxRange = 25f,
        float faiScalePerTick = 0.175f,
        float durationSeconds = 5f,
        float tickIntervalSeconds = 1f,
        float cooldownSeconds = 6f)
    {
        _maxRange = maxRange;
        _faiScalePerTick = faiScalePerTick;
        _durationSeconds = durationSeconds;
        _tickIntervalSeconds = tickIntervalSeconds;
        _cooldownSeconds = cooldownSeconds;
    }

    public override string Name => "継続回復";
    public override string PowerDescription =>
        $"FAI × {_faiScalePerTick:0.###}/{_tickIntervalSeconds:0.##}秒 × {_durationSeconds:0.##}秒";
    public override string EffectDescription => "対象に継続回復";
    public override float CooldownSeconds => _cooldownSeconds;
    public override float CastTimeSeconds => 1f;
    public override SkillTargetKind TargetKind => SkillTargetKind.AllyOrSelf;
    public override float MaxRange => _maxRange;

    public override int EstimateHealing(Character self, SkillExecutionContext context, Character target)
    {
        context = context.Capture(self);
        int tickCount = Mathf.Max(1, Mathf.CeilToInt(_durationSeconds / Mathf.Max(0.1f, _tickIntervalSeconds)));
        int healPerTick = Mathf.Max(
            1,
            Mathf.RoundToInt(context.GetEffectiveStat(CombatStat.FAI) * _faiScalePerTick));
        return healPerTick * tickCount;
    }

    public override void Execute(Character self, SkillExecutionContext context)
    {
        Character target = context.PrimaryTarget;
        if (self == null || target == null || target.Health == null) return;
        if (!target.Health.IsAlive) return;
        context = context.Capture(self);

        target.StatusEffects?.ApplyHealOverTime(
            Mathf.Max(1, Mathf.RoundToInt(context.GetEffectiveStat(CombatStat.FAI) * _faiScalePerTick)),
            _durationSeconds,
            _tickIntervalSeconds,
            "RosaryRegeneration",
            self);
    }
}
