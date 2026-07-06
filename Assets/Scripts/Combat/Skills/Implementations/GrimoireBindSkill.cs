using UnityEngine;

public sealed class GrimoireBindSkill : SkillBase
{
    private readonly float _maxRange;
    private readonly float _durationSeconds;
    private readonly float _cooldownSeconds;

    public GrimoireBindSkill(
        float maxRange = 6f,
        float durationSeconds = 3f,
        float cooldownSeconds = 7f)
    {
        _maxRange = maxRange;
        _durationSeconds = durationSeconds;
        _cooldownSeconds = cooldownSeconds;
    }

    public override string Name => "金縛り";
    public override float CooldownSeconds => _cooldownSeconds;
    public override float CastTimeSeconds => 1.4f;
    public override float MaxRange => _maxRange;

    public override void Execute(Character self, SkillExecutionContext context)
    {
        Character target = context.PrimaryTarget;
        if (self == null || target == null || target.Health == null) return;
        if (!target.Health.IsTargetable) return;

        target.StatusEffects?.ApplyBind(_durationSeconds, "GrimoireBind");
    }
}
