using UnityEngine;

public sealed class GrimoireStrDebuffSkill : SkillBase
{
    public const string EffectKey = "GrimoireStrDebuff";

    private readonly float _debuffMultiplier;
    private readonly float _durationSeconds;
    private readonly float _maxRange;
    private readonly float _cooldownSeconds;

    public GrimoireStrDebuffSkill(
        float debuffMultiplier = 0.7f,
        float durationSeconds = 5f,
        float maxRange = 7f,
        float cooldownSeconds = 5f)
    {
        _debuffMultiplier = debuffMultiplier;
        _durationSeconds = durationSeconds;
        _maxRange = maxRange;
        _cooldownSeconds = cooldownSeconds;
    }

    public override string Name => "STRデバフ";

    public override float CooldownSeconds => _cooldownSeconds;

    public override float EvaluateScore(Character self, Character target)
    {
        if (self == null || target == null) return 0f;

        float distance = Vector3.Distance(self.transform.position, target.transform.position);
        if (distance > _maxRange) return 0f;

        return 90f;
    }

    public override void Execute(Character self, Character target)
    {
        if (target == null) return;

        target.StatusEffects?.Apply(
            CombatStatusEffects.StatKind.STR,
            _debuffMultiplier,
            _durationSeconds,
            EffectKey);
    }
}
