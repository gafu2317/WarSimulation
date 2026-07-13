using UnityEngine;

public sealed class RosaryDistantHealSkill : SkillBase
{
    private readonly float _faiScale;
    private readonly float _maxRange;
    private readonly float _cooldownSeconds;

    public RosaryDistantHealSkill(
        float faiScale = 0.4f,
        float maxRange = 40f,
        float cooldownSeconds = 3.5f)
    {
        _faiScale = faiScale;
        _maxRange = maxRange;
        _cooldownSeconds = cooldownSeconds;
    }

    public override string Name => "遠隔癒し";

    public override float CooldownSeconds => _cooldownSeconds;

    public override float CastTimeSeconds => 0.9f;

    public override SkillTargetKind TargetKind => SkillTargetKind.AllyOrSelf;

    public override float MaxRange => _maxRange;

    public override int EstimateHealing(Character self, SkillExecutionContext context, Character target)
    {
        if (self == null || target == null) return 0;
        context = context.Capture(self);
        float distance = target == self ? 0f : context.GetDistance(target);
        float baseAmount = Mathf.Max(1f, context.GetEffectiveStat(CombatStat.FAI) * _faiScale);
        float t = _maxRange <= 0f ? 0f : Mathf.Clamp01(distance / _maxRange);
        return Mathf.Max(1, Mathf.RoundToInt(baseAmount * Mathf.Lerp(1.4f, 0.8f, t)));
    }

    public override void Execute(Character self, SkillExecutionContext context)
    {
        Character target = context.PrimaryTarget;
        if (self == null || target == null || target.Health == null) return;
        if (!target.Health.IsAlive) return;
        context = context.Capture(self);
        target.Health.Heal(EstimateHealing(self, context, target));
    }
}
