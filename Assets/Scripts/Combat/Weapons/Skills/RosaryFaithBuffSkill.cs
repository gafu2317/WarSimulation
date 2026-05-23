public sealed class RosaryFaithBuffSkill : SkillBase
{
    public const string EffectKey = "RosaryFaithBuff";

    private readonly float _buffMultiplier;
    private readonly float _durationSeconds;
    private readonly float _cooldownSeconds;
    private readonly float _refreshThresholdSeconds;

    public RosaryFaithBuffSkill(
        float buffMultiplier = 1.2f,
        float durationSeconds = 6f,
        float cooldownSeconds = 6f,
        float refreshThresholdSeconds = 2f)
    {
        _buffMultiplier = buffMultiplier;
        _durationSeconds = durationSeconds;
        _cooldownSeconds = cooldownSeconds;
        _refreshThresholdSeconds = refreshThresholdSeconds;
    }

    public override string Name => "信仰バフ";

    public override float CooldownSeconds => _cooldownSeconds;

    public override SkillTargetKind TargetKind => SkillTargetKind.AllyOrSelf;

    public override float EvaluateScore(Character self, Character target)
    {
        if (self == null || target == null || target.Health == null) return 0f;
        if (!target.Health.CanAct) return 0f;

        float allyPriorityBonus = target != self ? 10f : 0f;

        CombatStatusEffects statusEffects = target.StatusEffects;
        if (statusEffects == null) return 80f + allyPriorityBonus;

        if (!statusEffects.HasActiveEffect(EffectKey))
        {
            return 85f + allyPriorityBonus;
        }

        float remaining = statusEffects.GetRemainingSeconds(EffectKey);
        if (remaining <= _refreshThresholdSeconds)
        {
            return 60f + allyPriorityBonus;
        }

        return 0f;
    }

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
