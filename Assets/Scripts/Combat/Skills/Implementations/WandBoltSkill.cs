using UnityEngine;

public sealed class WandBoltSkill : SkillBase
{
    private readonly int _baseDamage;
    private readonly float _intScale;
    private readonly float _maxRange;
    private readonly float _cooldownSeconds;

    public WandBoltSkill(
        int baseDamage = 10,
        float intScale = 0.6f,
        float maxRange = 8f,
        float cooldownSeconds = 1.4f)
    {
        _baseDamage = baseDamage;
        _intScale = intScale;
        _maxRange = maxRange;
        _cooldownSeconds = cooldownSeconds;
    }

    public override string Name => "魔弾";

    public override float CooldownSeconds => _cooldownSeconds;

    public override float MaxRange => _maxRange;

    public override void Execute(Character self, Character target)
    {
        if (self == null || target == null || target.Health == null) return;
        if (!target.Health.IsTargetable) return;

        float distance = Vector3.Distance(self.transform.position, target.transform.position);
        if (distance > _maxRange) return;

        int damage = Mathf.Max(1, _baseDamage + Mathf.RoundToInt(self.INT * _intScale));
        target.Health.TakeDamage(damage, self);
    }
}
