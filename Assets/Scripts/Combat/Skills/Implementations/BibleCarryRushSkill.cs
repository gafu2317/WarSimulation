using UnityEngine;

public sealed class BibleCarryRushSkill : SkillBase
{
    private readonly float _maxRange;
    private readonly float _durationSeconds;
    private readonly float _speedMultiplier;
    private readonly float _cooldownSeconds;

    public BibleCarryRushSkill(
        float maxRange = 4f,
        float durationSeconds = 4f,
        float speedMultiplier = 1.8f,
        float cooldownSeconds = 8f)
    {
        _maxRange = maxRange;
        _durationSeconds = durationSeconds;
        _speedMultiplier = speedMultiplier;
        _cooldownSeconds = cooldownSeconds;
    }

    public override string Name => "高速移動";
    public override float CooldownSeconds => _cooldownSeconds;
    public override SkillTargetKind TargetKind => SkillTargetKind.Ally;
    public override float MaxRange => _maxRange;

    public override void Execute(Character self, SkillExecutionContext context)
    {
        Character target = context.PrimaryTarget;
        if (self == null || target == null || target == self) return;
        if (target.Team != self.Team || target.Health == null || !target.Health.IsAlive) return;

        BibleCarryRushEffect effect = self.GetComponent<BibleCarryRushEffect>();
        if (effect == null)
        {
            effect = self.gameObject.AddComponent<BibleCarryRushEffect>();
        }

        effect.Initialize(self, target, _speedMultiplier, _durationSeconds);
    }
}
