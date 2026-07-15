using UnityEngine;

public sealed class RosarySacrificeThunderSkill : SkillBase
{
    private readonly int _hpCost;
    private readonly float _faiScale;
    private readonly float _cooldownSeconds;

    public RosarySacrificeThunderSkill(
        int hpCost = 12,
        float faiScale = 0.7f,
        float cooldownSeconds = 9f)
    {
        _hpCost = hpCost;
        _faiScale = faiScale;
        _cooldownSeconds = cooldownSeconds;
    }

    public override string Name => "神の雷";
    public override float CooldownSeconds => _cooldownSeconds;
    public override float CastTimeSeconds => 2.5f;
    public override SkillTargetKind TargetKind => SkillTargetKind.RecognizedEnemies;
    public override bool CanTargetMagicStone => true;

    public override int SelfHpCost => _hpCost;

    public override int EstimateDamage(Character self, SkillExecutionContext context, Character target)
    {
        if (self == null || target == null) return 0;
        context = context.Capture(self);
        int damage = Mathf.Max(1, Mathf.RoundToInt(context.GetEffectiveStat(CombatStat.FAI) * _faiScale));
        return ApplyDamageModifiers(self, context, target, damage);
    }

    public override void Execute(Character self, SkillExecutionContext context)
    {
        if (self == null || self.Health == null) return;
        context = context.Capture(self);

        self.Health.TakeDamage(_hpCost, self);
        if (!self.Health.IsAlive) return;
        if (!context.HasAnyResolvedTarget) return;

        int damage = Mathf.Max(1, Mathf.RoundToInt(context.GetEffectiveStat(CombatStat.FAI) * _faiScale));
        bool dealtDamage = false;
        for (int i = 0; i < context.ResolvedTargets.Count; i++)
        {
            Character target = context.ResolvedTargets[i];
            dealtDamage |= TakeDamage(self, context, target, damage) > 0;
        }

        for (int i = 0; i < context.ResolvedStones.Count; i++)
        {
            dealtDamage |= TakeDamage(self, context.ResolvedStones[i], damage) > 0;
        }

        if (dealtDamage)
        {
            BreakStealthOnUse(self);
        }
    }
}
