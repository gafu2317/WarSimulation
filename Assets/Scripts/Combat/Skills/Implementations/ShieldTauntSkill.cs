using System.Collections.Generic;
using UnityEngine;

public sealed class ShieldTauntSkill : SkillBase
{
    public const string EffectKey = "ShieldTaunt";

    private readonly float _cooldownSeconds;
    private readonly float _durationSeconds;
    private readonly float _radius;

    public ShieldTauntSkill(
        float cooldownSeconds = 8f,
        float durationSeconds = 4f,
        float radius = 6f)
    {
        _cooldownSeconds = cooldownSeconds;
        _durationSeconds = durationSeconds;
        _radius = radius;
    }

    public override string Name => "挑発";
    public override string EffectDescription =>
        $"周囲 {_radius:0.##}m の敵が自身を優先（{_durationSeconds:0.##}秒）";
    public override float CooldownSeconds => _cooldownSeconds;
    public override SkillTargetKind TargetKind => SkillTargetKind.Self;
    public override float AreaRadius => _radius;

    public override void Execute(Character self, SkillExecutionContext context)
    {
        if (self == null || self.Health == null || !self.Health.IsAlive) return;

        self.StatusEffects?.ApplyTaunt(_durationSeconds, EffectKey, self);
        IReadOnlyList<Character> enemies = CombatSkillTargeting.GetEnemiesInRadius(
            self,
            self.transform.position,
            _radius);
        for (int i = 0; i < enemies.Count; i++)
        {
            Character enemy = enemies[i];
            if (enemy == null || enemy.Health == null || !enemy.Health.IsTargetable) continue;

            ShieldTauntTargetEffect effect = enemy.GetComponent<ShieldTauntTargetEffect>();
            if (effect == null) effect = enemy.gameObject.AddComponent<ShieldTauntTargetEffect>();
            effect.Initialize(self, _durationSeconds);
        }
    }
}
