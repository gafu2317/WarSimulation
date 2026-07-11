using UnityEngine;

public sealed class WandGodsHandSkill : SkillBase
{
    private readonly float _intScale;
    private readonly float _maxRange;
    private readonly float _cooldownSeconds;

    public WandGodsHandSkill(
        float intScale = 1.3f,
        float maxRange = 16f,
        float cooldownSeconds = 9f)
    {
        _intScale = intScale;
        _maxRange = maxRange;
        _cooldownSeconds = cooldownSeconds;
    }

    public override string Name => "神の手";
    public override float CooldownSeconds => _cooldownSeconds;
    public override float CastTimeSeconds => 2.5f;
    public override float MaxRange => _maxRange;
    public override bool CanTargetMagicStone => true;

    public override int EstimateDamage(Character self, SkillExecutionContext context, Character target)
    {
        if (self == null || target == null) return 0;
        context = context.Capture(self);
        int damage = Mathf.Max(1, Mathf.RoundToInt(context.GetEffectiveStat(CombatStat.INT) * _intScale));
        return ApplyDamageModifiers(self, context, target, damage);
    }

    public override void Execute(Character self, SkillExecutionContext context)
    {
        if (self == null || !context.HasAnyResolvedTarget) return;
        context = context.Capture(self);

        int damage = Mathf.Max(1, Mathf.RoundToInt(context.GetEffectiveStat(CombatStat.INT) * _intScale));
        if (TakeDamage(self, context, damage) > 0)
        {
            BreakStealthOnUse(self);
        }
    }
}
