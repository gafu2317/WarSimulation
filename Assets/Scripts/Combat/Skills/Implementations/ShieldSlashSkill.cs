using UnityEngine;

public sealed class ShieldSlashSkill : SkillBase
{
    private readonly int _baseDamage;
    private readonly float _strScale;
    private readonly float _maxRange;
    private readonly float _cooldownSeconds;

    public ShieldSlashSkill(
        int baseDamage = 7,
        float strScale = 0.45f,
        float maxRange = 2f,
        float cooldownSeconds = 1.1f)
    {
        _baseDamage = baseDamage;
        _strScale = strScale;
        _maxRange = maxRange;
        _cooldownSeconds = cooldownSeconds;
    }

    public override string Name => "盾撃";

    public override float CooldownSeconds => _cooldownSeconds;

    public override float MaxRange => _maxRange;

    public override void Execute(Character self, Character target)
    {
        if (self == null || target == null || target.Health == null) return;
        if (!target.Health.IsTargetable) return;

        float distance = Vector3.Distance(self.transform.position, target.transform.position);
        if (distance > _maxRange) return;

        int damage = Mathf.Max(1, _baseDamage + Mathf.RoundToInt(self.STR * _strScale));
        target.Health.TakeDamage(damage, self);
    }
}
