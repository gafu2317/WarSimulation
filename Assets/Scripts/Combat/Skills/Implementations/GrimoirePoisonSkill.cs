using UnityEngine;

public sealed class GrimoirePoisonSkill : SkillBase
{
    private readonly float _maxRange;
    private readonly int _damagePerTick;
    private readonly float _durationSeconds;
    private readonly float _tickIntervalSeconds;
    private readonly float _cooldownSeconds;

    public GrimoirePoisonSkill(
        float maxRange = 6f,
        int damagePerTick = 2,
        float durationSeconds = 5f,
        float tickIntervalSeconds = 1f,
        float cooldownSeconds = 6f)
    {
        _maxRange = maxRange;
        _damagePerTick = damagePerTick;
        _durationSeconds = durationSeconds;
        _tickIntervalSeconds = tickIntervalSeconds;
        _cooldownSeconds = cooldownSeconds;
    }

    public override string Name => "毒";
    public override float CooldownSeconds => _cooldownSeconds;
    public override float CastTimeSeconds => 1.1f;
    public override float MaxRange => _maxRange;

    public override void Execute(Character self, SkillExecutionContext context)
    {
        Character target = context.PrimaryTarget;
        if (self == null || target == null || target.Health == null) return;
        if (!target.Health.IsTargetable) return;

        target.StatusEffects?.ApplyPoison(_damagePerTick, _durationSeconds, _tickIntervalSeconds, "GrimoirePoison");
    }
}
