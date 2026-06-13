using UnityEngine;

public sealed class WandGodsHandSkill : SkillBase
{
    private readonly float _intScale;
    private readonly float _maxRange;
    private readonly float _cooldownSeconds;

    public WandGodsHandSkill(
        float intScale = 1.6f,
        float maxRange = 10f,
        float cooldownSeconds = 9f)
    {
        _intScale = intScale;
        _maxRange = maxRange;
        _cooldownSeconds = cooldownSeconds;
    }

    public override string Name => "神の手";
    public override float CooldownSeconds => _cooldownSeconds;
    public override float MaxRange => _maxRange;

    public override void Execute(Character self, SkillExecutionContext context)
    {
        Character target = context.PrimaryTarget;
        if (self == null || target == null || target.Health == null) return;
        if (!target.Health.IsTargetable) return;

        float distance = ComputeHorizontalDistance(self, target);

        int damage = Mathf.Max(1, Mathf.RoundToInt(self.GetEffectiveStat(CombatStat.INT) * _intScale));
        damage = ComputeStealthAwareDamage(self, target, damage);
        target.Health.TakeDamage(damage, self);
        BreakStealthOnUse(self);
    }
}
