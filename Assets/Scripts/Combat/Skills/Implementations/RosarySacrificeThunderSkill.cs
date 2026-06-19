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
    public override bool CanTargetMagicStone => true;

    public override void Execute(Character self, SkillExecutionContext context)
    {
        if (self == null || self.Health == null) return;

        self.Health.TakeDamage(_hpCost, self);
        if (!self.Health.IsAlive) return;
        if (!context.HasAnyResolvedTarget) return;

        int damage = Mathf.Max(1, Mathf.RoundToInt(self.GetEffectiveStat(CombatStat.FAI) * _faiScale));
        bool dealtDamage = false;
        for (int i = 0; i < context.ResolvedTargets.Count; i++)
        {
            Character target = context.ResolvedTargets[i];
            dealtDamage |= TakeDamage(self, target, damage) > 0;
        }

        for (int i = 0; i < context.ResolvedStones.Count; i++)
        {
            dealtDamage |= TakeDamage(context.ResolvedStones[i], damage) > 0;
        }

        if (dealtDamage)
        {
            BreakStealthOnUse(self);
        }
    }
}
