public sealed class StatDebuffSkill : SkillBase
{
    private readonly CombatStatusEffects.StatKind _stat;
    private readonly float _debuffMultiplier;
    private readonly float _durationSeconds;
    private readonly float _cooldownSeconds;
    private readonly string _name;

    public StatDebuffSkill(
        CombatStatusEffects.StatKind stat,
        float debuffMultiplier = 0.7f,
        float durationSeconds = 5f,
        float cooldownSeconds = 5f,
        string name = null)
    {
        _stat = stat;
        _debuffMultiplier = debuffMultiplier;
        _durationSeconds = durationSeconds;
        _cooldownSeconds = cooldownSeconds;
        _name = name ?? $"{stat}デバフ";
    }

    public CombatStatusEffects.StatKind Stat => _stat;

    public override string Name => _name;

    public override string EffectDescription =>
        $"対象の {_stat} を × {_debuffMultiplier:0.##}（{_durationSeconds:0.##}秒）";

    public override float CooldownSeconds => _cooldownSeconds;

    public override float CastTimeSeconds => 1f;

    // 射程制限なし。届くかは認識・視界・射線（CombatSkillEvaluator）で決める。
    public override float MaxRange => float.PositiveInfinity;

    public static string GetEffectKey(CombatStatusEffects.StatKind stat) => $"StatDebuff_{stat}";

    public override void Execute(Character self, SkillExecutionContext context)
    {
        Character target = context.PrimaryTarget;
        if (self == null || target == null || target.Health == null || !target.Health.IsTargetable) return;

        target.StatusEffects?.Apply(
            _stat,
            _debuffMultiplier,
            _durationSeconds,
            GetEffectKey(_stat),
            self);
    }
}
