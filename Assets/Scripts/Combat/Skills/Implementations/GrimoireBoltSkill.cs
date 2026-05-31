using UnityEngine;

public sealed class GrimoireBoltSkill : SkillBase
{
    private readonly int _baseDamage;
    private readonly float _intScale;
    private readonly float _maxRange;
    private readonly float _cooldownSeconds;

    public GrimoireBoltSkill(
        int baseDamage = 9,
        float intScale = 0.55f,
        float maxRange = 6f,
        float cooldownSeconds = 1.3f)
    {
        _baseDamage = baseDamage;
        _intScale = intScale;
        _maxRange = maxRange;
        _cooldownSeconds = cooldownSeconds;
    }

    public override string Name => "呪弾";

    public override float CooldownSeconds => _cooldownSeconds;

    public override float MaxRange => _maxRange;

    public override void Execute(Character self, SkillExecutionContext context)
    {
        Character target = context.PrimaryTarget;
        if (self == null || target == null || target.Health == null) return;
        if (!target.Health.IsTargetable) return;

        float distance = Vector3.Distance(self.transform.position, target.transform.position);
        if (distance > _maxRange) return;

        int damage = Mathf.Max(1, _baseDamage + Mathf.RoundToInt(self.INT * _intScale));
        damage = ComputeStealthAwareDamage(self, target, damage);
        target.Health.TakeDamage(damage, self);
        BreakStealthOnUse(self);
    }
}
