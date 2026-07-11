using UnityEngine;

public sealed class GrimoireBoltSkill : SkillBase
{
    private readonly float _intScale;
    private readonly float _maxRange;
    private readonly float _cooldownSeconds;

    public GrimoireBoltSkill(
        float intScale = 0.35f,
        float maxRange = 8f,
        float cooldownSeconds = 1.3f)
    {
        _intScale = intScale;
        _maxRange = maxRange;
        _cooldownSeconds = cooldownSeconds;
    }

    public override string Name => "通常攻撃";

    public override float CooldownSeconds => _cooldownSeconds;

    public override float CastTimeSeconds => 0.7f;

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
