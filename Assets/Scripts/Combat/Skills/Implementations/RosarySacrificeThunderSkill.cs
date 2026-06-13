using UnityEngine;

public sealed class RosarySacrificeThunderSkill : SkillBase
{
    private readonly int _hpCost;
    private readonly float _faiScale;
    private readonly float _cooldownSeconds;

    public RosarySacrificeThunderSkill(
        int hpCost = 8,
        float faiScale = 0.9f,
        float cooldownSeconds = 9f)
    {
        _hpCost = hpCost;
        _faiScale = faiScale;
        _cooldownSeconds = cooldownSeconds;
    }

    public override string Name => "神の雷";
    public override float CooldownSeconds => _cooldownSeconds;
    public override SkillTargetKind TargetKind => SkillTargetKind.RecognizedEnemies;

    public override void Execute(Character self, SkillExecutionContext context)
    {
        if (self == null || self.Health == null) return;

        self.Health.TakeDamage(_hpCost, self);
        if (!self.Health.IsAlive) return;
        if (context.ResolvedTargets == null || context.ResolvedTargets.Count == 0) return;

        int damage = Mathf.Max(1, Mathf.RoundToInt(self.GetEffectiveStat(CombatStat.FAI) * _faiScale));
        for (int i = 0; i < context.ResolvedTargets.Count; i++)
        {
            Character target = context.ResolvedTargets[i];
            if (target == null || target.Health == null || !target.Health.IsTargetable) continue;

            target.Health.TakeDamage(damage, self);
        }

        BreakStealthOnUse(self);
    }
}
