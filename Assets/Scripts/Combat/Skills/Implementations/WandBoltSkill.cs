using UnityEngine;

public sealed class WandBoltSkill : SkillBase
{
    private readonly float _intScale;
    private readonly float _maxRange;
    private readonly float _cooldownSeconds;

    public WandBoltSkill(
        float intScale = 0.4f,
        float maxRange = 10f,
        float cooldownSeconds = 1.4f)
    {
        _intScale = intScale;
        _maxRange = maxRange;
        _cooldownSeconds = cooldownSeconds;
    }

    public override string Name => "魔弾";

    public override float CooldownSeconds => _cooldownSeconds;

    public override float CastTimeSeconds => 0.6f;

    public override float MaxRange => _maxRange;
    public override bool CanTargetMagicStone => true;

    public override int EstimateDamage(Character self, SkillExecutionContext context, Character target)
    {
        if (self == null || target == null) return 0;
        context = context.Capture(self);
        int damage = Mathf.Max(1, Mathf.RoundToInt(context.GetEffectiveStat(CombatStat.INT) * _intScale));
        damage = ComputeDistanceScaledAmount(
            damage,
            context.GetDistance(target),
            _maxRange,
            nearMultiplier: 0.7f,
            farMultiplier: 1.3f);
        return ApplyDamageModifiers(self, context, target, damage);
    }

    public override void Execute(Character self, SkillExecutionContext context)
    {
        if (self == null || !context.HasAnyResolvedTarget) return;
        context = context.Capture(self);

        float distance = context.PrimaryTarget != null
            ? context.GetDistance(context.PrimaryTarget)
            : context.GetDistance(context.PrimaryStone);

        int damage = Mathf.Max(1, Mathf.RoundToInt(context.GetEffectiveStat(CombatStat.INT) * _intScale));
        damage = ComputeDistanceScaledAmount(
            damage,
            distance,
            _maxRange,
            nearMultiplier: 0.7f,
            farMultiplier: 1.3f);
        if (TakeDamage(self, context, damage) > 0)
        {
            BreakStealthOnUse(self);
        }
    }
}
