using UnityEngine;

public sealed class RosaryCloseHealSkill : SkillBase
{
    private readonly float _faiScale;
    private readonly float _maxRange;
    private readonly float _cooldownSeconds;

    public RosaryCloseHealSkill(
        float faiScale = 1.1f,
        float maxRange = 3f,
        float cooldownSeconds = 6f)
    {
        _faiScale = faiScale;
        _maxRange = maxRange;
        _cooldownSeconds = cooldownSeconds;
    }

    public override string Name => "大回復";

    public override float CooldownSeconds => _cooldownSeconds;

    public override float CastTimeSeconds => 1.3f;

    public override SkillTargetKind TargetKind => SkillTargetKind.AllyOrSelf;

    public override float MaxRange => _maxRange;

    public override void Execute(Character self, SkillExecutionContext context)
    {
        Character target = context.PrimaryTarget;
        if (self == null || target == null || target.Health == null) return;
        if (!target.Health.IsAlive) return;
        context = context.Capture(self);

        int healAmount = Mathf.Max(1, Mathf.RoundToInt(context.GetEffectiveStat(CombatStat.FAI) * _faiScale));
        target.Health.Heal(healAmount);
    }
}
