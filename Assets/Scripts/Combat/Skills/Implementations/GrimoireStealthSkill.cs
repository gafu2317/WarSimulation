public sealed class GrimoireStealthSkill : SkillBase
{
    private readonly float _durationSeconds;
    private readonly float _cooldownSeconds;

    public GrimoireStealthSkill(
        float durationSeconds = 5f,
        float cooldownSeconds = 7f)
    {
        _durationSeconds = durationSeconds;
        _cooldownSeconds = cooldownSeconds;
    }

    public override string Name => "不可視";

    public override string EffectDescription => $"自身を不可視化（{_durationSeconds:0.##}秒）";
    public override float CooldownSeconds => _cooldownSeconds;
    public override float CastTimeSeconds => 0.8f;
    public override SkillTargetKind TargetKind => SkillTargetKind.Self;

    public override void Execute(Character self, SkillExecutionContext context)
    {
        if (self == null) return;

        self.StatusEffects?.ApplyStealth(_durationSeconds, "GrimoireStealth", self);
        var enemies = CombatSkillTargeting.GetAllEnemies(self);
        for (int i = 0; i < enemies.Count; i++)
        {
            enemies[i]?.Vision?.Forget(self);
        }
    }
}
