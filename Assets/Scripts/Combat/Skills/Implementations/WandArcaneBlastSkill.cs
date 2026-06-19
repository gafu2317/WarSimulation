using UnityEngine;

public sealed class WandArcaneBlastSkill : SkillBase
{
    private readonly float _intScale;
    private readonly float _maxRange;
    private readonly float _cooldownSeconds;

    public WandArcaneBlastSkill(
        float intScale = 1.2f,
        float maxRange = 24f,
        float cooldownSeconds = 7f)
    {
        _intScale = intScale;
        _maxRange = maxRange;
        _cooldownSeconds = cooldownSeconds;
    }

    public override string Name => "極大魔弾";

    public override float CooldownSeconds => _cooldownSeconds;

    public override float MaxRange => _maxRange;
    public override bool CanTargetMagicStone => true;

    public override void Execute(Character self, SkillExecutionContext context)
    {
        if (self == null || !context.HasAnyResolvedTarget) return;

        int damage = Mathf.Max(1, Mathf.RoundToInt(self.GetEffectiveStat(CombatStat.INT) * _intScale));
        if (TakeDamage(self, context, damage) > 0)
        {
            BreakStealthOnUse(self);
        }
    }
}
