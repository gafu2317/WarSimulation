using UnityEngine;

public sealed class SwordSlashSkill : SkillBase
{
    private readonly float _strScale;
    private readonly float _maxRange;
    private readonly float _cooldownSeconds;

    public SwordSlashSkill(
        float strScale = 1f,
        float maxRange = 2f,
        float cooldownSeconds = 1f)
    {
        _strScale = strScale;
        _maxRange = maxRange;
        _cooldownSeconds = cooldownSeconds;
    }

    public override string Name => "斬撃";

    public override float CooldownSeconds => _cooldownSeconds;

    public override float MaxRange => _maxRange;
    public override bool CanTargetMagicStone => true;

    public override void Execute(Character self, SkillExecutionContext context)
    {
        if (self == null || !context.HasAnyResolvedTarget) return;

        int damage = Mathf.Max(1, Mathf.RoundToInt(self.GetEffectiveStat(CombatStat.STR) * _strScale));
        if (TakeDamage(self, context, damage) > 0)
        {
            BreakStealthOnUse(self);
        }
    }
}
