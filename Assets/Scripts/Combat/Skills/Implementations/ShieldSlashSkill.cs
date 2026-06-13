using UnityEngine;

public sealed class ShieldSlashSkill : SkillBase
{
    private readonly float _strScale;
    private readonly float _maxRange;
    private readonly float _cooldownSeconds;

    public ShieldSlashSkill(
        float strScale = 0.45f,
        float maxRange = 2f,
        float cooldownSeconds = 1.1f)
    {
        _strScale = strScale;
        _maxRange = maxRange;
        _cooldownSeconds = cooldownSeconds;
    }

    public override string Name => "盾撃";

    public override float CooldownSeconds => _cooldownSeconds;

    public override float MaxRange => _maxRange;

    public override void Execute(Character self, SkillExecutionContext context)
    {
        Character target = context.PrimaryTarget;
        if (self == null || target == null || target.Health == null) return;
        if (!target.Health.IsTargetable) return;

        float distance = ComputeHorizontalDistance(self, target);

        int damage = Mathf.Max(1, Mathf.RoundToInt(self.GetEffectiveStat(CombatStat.STR) * _strScale));
        damage = ComputeStealthAwareDamage(self, target, damage);
        target.Health.TakeDamage(damage, self);
        BreakStealthOnUse(self);
    }
}
