public sealed class ShieldGuardSkill : SkillBase
{
    public const string EffectKey = "ShieldGuard";

    private readonly float _buffMultiplier;
    private readonly float _durationSeconds;
    private readonly float _cooldownSeconds;

    public ShieldGuardSkill(
        float buffMultiplier = 1.25f,
        float durationSeconds = 5f,
        float cooldownSeconds = 5f)
    {
        _buffMultiplier = buffMultiplier;
        _durationSeconds = durationSeconds;
        _cooldownSeconds = cooldownSeconds;
    }

    public override string Name => "守護";

    public override float CooldownSeconds => _cooldownSeconds;

    public override SkillTargetKind TargetKind => SkillTargetKind.AllyOrSelf;

    public override void Execute(Character self, Character target)
    {
        if (target == null) return;

        target.StatusEffects?.Apply(
            CombatStatusEffects.StatKind.STR,
            _buffMultiplier,
            _durationSeconds,
            EffectKey);
    }
}
