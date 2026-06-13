using UnityEngine;

public sealed class RosaryRegenerationSkill : SkillBase
{
    private readonly float _maxRange;
    private readonly int _healPerTick;
    private readonly float _durationSeconds;
    private readonly float _tickIntervalSeconds;
    private readonly float _cooldownSeconds;

    public RosaryRegenerationSkill(
        float maxRange = 5f,
        int healPerTick = 5,
        float durationSeconds = 5f,
        float tickIntervalSeconds = 1f,
        float cooldownSeconds = 6f)
    {
        _maxRange = maxRange;
        _healPerTick = healPerTick;
        _durationSeconds = durationSeconds;
        _tickIntervalSeconds = tickIntervalSeconds;
        _cooldownSeconds = cooldownSeconds;
    }

    public override string Name => "継続回復";
    public override float CooldownSeconds => _cooldownSeconds;
    public override SkillTargetKind TargetKind => SkillTargetKind.AllyOrSelf;
    public override float MaxRange => _maxRange;

    public override void Execute(Character self, SkillExecutionContext context)
    {
        Character target = context.PrimaryTarget;
        if (self == null || target == null || target.Health == null) return;
        if (!target.Health.IsAlive) return;

        target.StatusEffects?.ApplyHealOverTime(_healPerTick, _durationSeconds, _tickIntervalSeconds, "RosaryRegeneration");
    }
}
