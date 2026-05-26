using UnityEngine;

public sealed class RosaryStrikeSkill : SkillBase
{
    private readonly int _baseDamage;
    private readonly float _faiScale;
    private readonly float _maxRange;
    private readonly float _cooldownSeconds;

    public RosaryStrikeSkill(
        int baseDamage = 6,
        float faiScale = 0.45f,
        float maxRange = 4f,
        float cooldownSeconds = 1.3f)
    {
        _baseDamage = baseDamage;
        _faiScale = faiScale;
        _maxRange = maxRange;
        _cooldownSeconds = cooldownSeconds;
    }

    public override string Name => "聖撃";

    public override float CooldownSeconds => _cooldownSeconds;

    public override float MaxRange => _maxRange;

    public override void Execute(Character self, Character target)
    {
        if (self == null || target == null || target.Health == null) return;
        if (!target.Health.IsTargetable) return;

        float distance = Vector3.Distance(self.transform.position, target.transform.position);
        if (distance > _maxRange) return;

        int damage = Mathf.Max(1, _baseDamage + Mathf.RoundToInt(self.FAI * _faiScale));
        target.Health.TakeDamage(damage, self);
    }
}
