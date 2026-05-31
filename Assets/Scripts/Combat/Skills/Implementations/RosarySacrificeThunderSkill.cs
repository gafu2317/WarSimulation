using UnityEngine;

public sealed class RosarySacrificeThunderSkill : SkillBase
{
    private readonly int _hpCost;
    private readonly int _baseDamage;
    private readonly float _faiScale;
    private readonly float _cooldownSeconds;

    public RosarySacrificeThunderSkill(
        int hpCost = 8,
        int baseDamage = 12,
        float faiScale = 0.6f,
        float cooldownSeconds = 9f)
    {
        _hpCost = hpCost;
        _baseDamage = baseDamage;
        _faiScale = faiScale;
        _cooldownSeconds = cooldownSeconds;
    }

    public override string Name => "神の雷";
    public override float CooldownSeconds => _cooldownSeconds;
    public override SkillTargetKind TargetKind => SkillTargetKind.AllEnemies;

    public override void Execute(Character self, SkillExecutionContext context)
    {
        if (self == null || self.Health == null) return;

        self.Health.TakeDamage(_hpCost, self);
        if (!self.Health.IsAlive) return;

        int damage = Mathf.Max(1, _baseDamage + Mathf.RoundToInt(self.FAI * _faiScale));
        for (int i = 0; i < context.ResolvedTargets.Count; i++)
        {
            Character target = context.ResolvedTargets[i];
            if (target == null || target.Health == null || !target.Health.IsTargetable) continue;

            target.Health.TakeDamage(damage, self);
        }
    }
}
