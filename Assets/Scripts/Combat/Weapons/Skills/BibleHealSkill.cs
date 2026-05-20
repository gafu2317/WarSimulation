using UnityEngine;

public sealed class BibleHealSkill : SkillBase
{
    public const string EffectKey = "BibleHeal";

    private readonly int _baseHeal;
    private readonly float _faiScale;
    private readonly float _cooldownSeconds;

    public BibleHealSkill(
        int baseHeal = 8,
        float faiScale = 0.5f,
        float cooldownSeconds = 4f)
    {
        _baseHeal = baseHeal;
        _faiScale = faiScale;
        _cooldownSeconds = cooldownSeconds;
    }

    public override string Name => "回復";

    public override float CooldownSeconds => _cooldownSeconds;

    public override SkillTargetKind TargetKind => SkillTargetKind.AllyOrSelf;

    public override float EvaluateScore(Character self, Character target)
    {
        if (self == null || target == null || target.Health == null) return 0f;
        if (!target.Health.CanAct) return 0f;
        if (target.Health.HP >= target.Health.MaxHP) return 0f;

        float missingRatio = 1f - (target.Health.HP / (float)target.Health.MaxHP);
        return 70f + missingRatio * 30f;
    }

    public override void Execute(Character self, Character target)
    {
        if (self == null || target == null || target.Health == null) return;
        if (!target.Health.CanAct) return;

        int healAmount = Mathf.Max(1, _baseHeal + Mathf.RoundToInt(self.FAI * _faiScale));
        target.Health.Heal(healAmount);
    }
}
