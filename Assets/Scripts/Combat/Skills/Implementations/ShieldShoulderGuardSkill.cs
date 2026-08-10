using UnityEngine;

public sealed class ShieldShoulderGuardSkill : SkillBase
{
    private readonly float _maxRange;
    private readonly float _durationSeconds;
    private readonly float _damageMultiplier;
    private readonly float _cooldownSeconds;

    public ShieldShoulderGuardSkill(
        float maxRange = 8f,
        float durationSeconds = 3f,
        float damageMultiplier = 0.6f,
        float cooldownSeconds = 7f)
    {
        _maxRange = maxRange;
        _durationSeconds = durationSeconds;
        _damageMultiplier = damageMultiplier;
        _cooldownSeconds = cooldownSeconds;
    }

    public override string Name => "肩代わり";
    public override string EffectDescription =>
        $"味方への攻撃を肩代わり（肩代わり先の {_damageMultiplier:0%} ダメージ、{_durationSeconds:0.##}秒）";
    public override float CooldownSeconds => _cooldownSeconds;
    public override SkillTargetKind TargetKind => SkillTargetKind.Ally;
    public override float MaxRange => _maxRange;

    public override void Execute(Character self, SkillExecutionContext context)
    {
        Character target = context.PrimaryTarget;
        if (self == null || target == null || target == self) return;
        if (target.Health == null || !target.Health.IsAlive) return;
        if (target.Team != self.Team) return;

        ShieldShoulderGuardEffect effect = target.GetComponent<ShieldShoulderGuardEffect>();
        if (effect == null)
        {
            effect = target.gameObject.AddComponent<ShieldShoulderGuardEffect>();
        }

        effect.Initialize(self, target, _damageMultiplier, _durationSeconds);
        CombatSkillActionEvents.RecordCharacterEffect(
            CombatActionEffectKind.PersistentEffectStarted,
            CombatEffectSource.Capture(self),
            target,
            statusKey: "ShieldShoulderGuard");
    }
}
