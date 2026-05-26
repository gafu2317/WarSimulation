public sealed class RosaryFaithBuffSkill : SkillBase
{
    public const string EffectKey = "RosaryFaithBuff";

    private readonly float _buffMultiplier;
    private readonly float _durationSeconds;
    private readonly float _cooldownSeconds;

    public RosaryFaithBuffSkill(
        float buffMultiplier = 1.2f,
        float durationSeconds = 6f,
        float cooldownSeconds = 6f)
    {
        _buffMultiplier = buffMultiplier;
        _durationSeconds = durationSeconds;
        _cooldownSeconds = cooldownSeconds;
    }

    public override string Name => "信仰バフ";

    public override float CooldownSeconds => _cooldownSeconds;

    public override SkillTargetKind TargetKind => SkillTargetKind.AllyOrSelf;

    public override void Execute(Character self, Character target)
    {
        if (target == null) return;

        target.StatusEffects?.Apply(
            CombatStatusEffects.StatKind.FAI,
            _buffMultiplier,
            _durationSeconds,
            EffectKey);
    }
}
