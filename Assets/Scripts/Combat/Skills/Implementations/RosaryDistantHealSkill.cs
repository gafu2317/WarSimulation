using UnityEngine;

public sealed class RosaryDistantHealSkill : SkillBase
{
    private readonly int _baseHeal;
    private readonly float _faiScale;
    private readonly float _maxRange;
    private readonly float _cooldownSeconds;

    public RosaryDistantHealSkill(
        int baseHeal = 3,
        float faiScale = 0.3f,
        float maxRange = 9f,
        float cooldownSeconds = 3.5f)
    {
        _baseHeal = baseHeal;
        _faiScale = faiScale;
        _maxRange = maxRange;
        _cooldownSeconds = cooldownSeconds;
    }

    public override string Name => "遠隔癒し";

    public override float CooldownSeconds => _cooldownSeconds;

    public override SkillTargetKind TargetKind => SkillTargetKind.AllyOrSelf;

    public override float MaxRange => _maxRange;

    public override void Execute(Character self, SkillExecutionContext context)
    {
        Character target = context.PrimaryTarget;
        if (self == null || target == null || target.Health == null) return;
        if (!target.Health.IsAlive) return;
        float distance = target == self ? 0f : ComputeHorizontalDistance(self, target);

        int healAmount = Mathf.Max(1, _baseHeal + Mathf.RoundToInt(self.FAI * _faiScale));
        healAmount = ComputeDistanceScaledAmount(
            healAmount,
            distance,
            _maxRange,
            nearMultiplier: 1.5f,
            farMultiplier: 0.8f);
        target.Health.Heal(healAmount);
    }
}
