public sealed class BibleInvulnerableSkill : SkillBase
{
    private readonly float _durationSeconds;
    private readonly float _cooldownSeconds;

    public BibleInvulnerableSkill(
        float durationSeconds = 3f,
        float cooldownSeconds = 8f)
    {
        _durationSeconds = durationSeconds;
        _cooldownSeconds = cooldownSeconds;
    }

    public override string Name => "無敵";
    public override float CooldownSeconds => _cooldownSeconds;
    public override SkillTargetKind TargetKind => SkillTargetKind.Self;

    public override void Execute(Character self, SkillExecutionContext context)
    {
        if (self == null) return;
        self.StatusEffects?.ApplyInvulnerable(_durationSeconds, "BibleInvulnerable");
    }
}
