public sealed class IdentifiedSkill : SkillBase
{
    private readonly SkillBase _inner;
    private readonly SkillId _skillId;

    public IdentifiedSkill(SkillBase inner, SkillId skillId)
    {
        _inner = inner;
        _skillId = skillId;
    }

    public SkillId SkillId => _skillId;

    public override string Name => _inner.Name;

    public override float CooldownSeconds => _inner.CooldownSeconds;

    public override string CooldownKey => _skillId.ToString();

    public override SkillTargetKind TargetKind => _inner.TargetKind;

    public override float MaxRange => _inner.MaxRange;

    public override float AreaRadius => _inner.AreaRadius;

    public override bool CanTargetMagicStone => _inner.CanTargetMagicStone;

    public override void Execute(Character self, SkillExecutionContext context)
    {
        CombatSkillDebugIndicatorSystem.Show(self, _skillId, Name, context);
        _inner.Execute(self, context);
    }
}
