using UnityEngine;

public sealed class RosaryStrikeSkill : SkillBase
{
    private readonly float _faiScale;
    private readonly float _maxRange;
    private readonly float _cooldownSeconds;

    public RosaryStrikeSkill(
        float faiScale = 0.6f,
        float maxRange = 4f,
        float cooldownSeconds = 1.3f)
    {
        _faiScale = faiScale;
        _maxRange = maxRange;
        _cooldownSeconds = cooldownSeconds;
    }

    public override string Name => "通常攻撃";

    public override float CooldownSeconds => _cooldownSeconds;

    public override float MaxRange => _maxRange;

    public override void Execute(Character self, SkillExecutionContext context)
    {
        Character target = context.PrimaryTarget;
        if (self == null || target == null || target.Health == null) return;
        if (!target.Health.IsTargetable) return;

        float distance = ComputeHorizontalDistance(self, target);

        int damage = Mathf.Max(1, Mathf.RoundToInt(self.GetEffectiveStat(CombatStat.FAI) * _faiScale));
        damage = ComputeStealthAwareDamage(self, target, damage);
        target.Health.TakeDamage(damage, self);
        BreakStealthOnUse(self);
    }
}
