using UnityEngine;

public sealed class StatDebuffSkill : SkillBase
{
    private readonly CombatStatusEffects.StatKind _stat;
    private readonly float _debuffMultiplier;
    private readonly float _durationSeconds;
    private readonly float _maxRange;
    private readonly float _cooldownSeconds;
    private readonly string _name;

    public StatDebuffSkill(
        CombatStatusEffects.StatKind stat,
        float debuffMultiplier = 0.7f,
        float durationSeconds = 5f,
        float maxRange = 7f,
        float cooldownSeconds = 5f,
        string name = null)
    {
        _stat = stat;
        _debuffMultiplier = debuffMultiplier;
        _durationSeconds = durationSeconds;
        _maxRange = maxRange;
        _cooldownSeconds = cooldownSeconds;
        _name = name ?? $"{stat}デバフ";
    }

    public CombatStatusEffects.StatKind Stat => _stat;

    public override string Name => _name;

    public override float CooldownSeconds => _cooldownSeconds;

    public override float MaxRange => _maxRange;

    public static string GetEffectKey(CombatStatusEffects.StatKind stat) => $"StatDebuff_{stat}";

    public override void Execute(Character self, SkillExecutionContext context)
    {
        Character target = context.PrimaryTarget;
        if (self == null || target == null) return;

        float distance = ComputeHorizontalDistance(self, target);

        target.StatusEffects?.Apply(
            _stat,
            _debuffMultiplier,
            _durationSeconds,
            GetEffectKey(_stat));
    }
}
