using UnityEngine;

public sealed class WandAreaBlastSkill : SkillBase
{
    private readonly float _intScale;
    private readonly float _maxRange;
    private readonly float _radius;
    private readonly float _cooldownSeconds;

    public WandAreaBlastSkill(
        float intScale = 0.5f,
        float maxRange = 18f,
        float radius = 3f,
        float cooldownSeconds = 5f)
    {
        _intScale = intScale;
        _maxRange = maxRange;
        _radius = radius;
        _cooldownSeconds = cooldownSeconds;
    }

    public override string Name => "範囲魔法";
    public override float CooldownSeconds => _cooldownSeconds;
    public override SkillTargetKind TargetKind => SkillTargetKind.Area;
    public override float MaxRange => _maxRange;
    public override float AreaRadius => _radius;
    public override bool CanTargetMagicStone => true;

    public override void Execute(Character self, SkillExecutionContext context)
    {
        if (self == null || !context.HasTargetPoint) return;
        if (!context.HasAnyResolvedTarget) return;

        int damage = Mathf.Max(1, Mathf.RoundToInt(self.GetEffectiveStat(CombatStat.INT) * _intScale));
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
