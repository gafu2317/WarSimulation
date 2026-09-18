public sealed class ShieldIronWallSkill : SkillBase
{
    public const string EffectKey = "ShieldIronWall";

    private readonly float _cooldownSeconds;
    private readonly float _durationSeconds;
    private readonly float _damageReduction;

    public ShieldIronWallSkill(
        float cooldownSeconds = 8f,
        float durationSeconds = 4f,
        float damageReduction = 0.4f)
    {
        _cooldownSeconds = cooldownSeconds;
        _durationSeconds = durationSeconds;
        _damageReduction = damageReduction;
    }

    public override string Name => "鉄壁";
    public override string EffectDescription =>
        $"自身の被ダメージ {_damageReduction:0%} 軽減（{_durationSeconds:0.##}秒）";
    public override float CooldownSeconds => _cooldownSeconds;
    public override SkillTargetKind TargetKind => SkillTargetKind.Self;

    public override void Execute(Character self, SkillExecutionContext context)
    {
        if (self == null || self.Health == null || !self.Health.IsAlive) return;

        self.StatusEffects?.ApplyDamageReduction(
            _damageReduction,
            _durationSeconds,
            EffectKey,
            self);
    }
}
