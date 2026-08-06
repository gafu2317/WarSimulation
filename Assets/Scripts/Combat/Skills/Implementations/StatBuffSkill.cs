public sealed class StatBuffSkill : SkillBase
{
    private readonly CombatStatusEffects.StatKind _stat;
    private readonly float _buffMultiplier;
    private readonly float _durationSeconds;
    private readonly float _cooldownSeconds;
    private readonly string _name;

    public StatBuffSkill(
        CombatStatusEffects.StatKind stat,
        float buffMultiplier = 1.25f,
        float durationSeconds = 5f,
        float cooldownSeconds = 5f,
        string name = null)
    {
        _stat = stat;
        _buffMultiplier = buffMultiplier;
        _durationSeconds = durationSeconds;
        _cooldownSeconds = cooldownSeconds;
        _name = name ?? $"{stat}バフ";
    }

    public CombatStatusEffects.StatKind Stat => _stat;

    public override string Name => _name;

    public override float CooldownSeconds => _cooldownSeconds;

    public override float CastTimeSeconds => 0.9f;

    public override SkillTargetKind TargetKind => SkillTargetKind.AllyOrSelf;

    // 射程制限なし。届くかは視界・射線（CombatSkillEvaluator）で決める。
    public override float MaxRange => float.PositiveInfinity;

    public static string GetEffectKey(CombatStatusEffects.StatKind stat) => $"StatBuff_{stat}";

    public override void Execute(Character self, SkillExecutionContext context)
    {
        Character target = context.PrimaryTarget;
        if (target == null || target.Health == null || !target.Health.IsAlive) return;

        target.StatusEffects?.Apply(
            _stat,
            _buffMultiplier,
            _durationSeconds,
            GetEffectKey(_stat),
            self);
    }
}
