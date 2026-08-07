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

    public override SkillId Id => _skillId;

    public override string Name => _inner.Name;

    public override float CooldownSeconds => _inner.CooldownSeconds;

    public override float CastTimeSeconds => _inner.CastTimeSeconds;

    public override string CooldownKey => _skillId.ToString();

    public override SkillTargetKind TargetKind => _inner.TargetKind;

    public override float MaxRange => _inner.MaxRange;

    public override float AreaRadius => _inner.AreaRadius;

    public override bool CanTargetMagicStone => _inner.CanTargetMagicStone;

    public override int SelfHpCost => _inner.SelfHpCost;

    public override int EstimateDamage(Character self, SkillExecutionContext context, Character target)
    {
        return _inner.EstimateDamage(self, context, target);
    }

    public override int EstimateHealing(Character self, SkillExecutionContext context, Character target)
    {
        return _inner.EstimateHealing(self, context, target);
    }

    public override void Execute(Character self, SkillExecutionContext context)
    {
        if (self != null)
        {
            CombatAiWorldLabel label = self.GetComponent<CombatAiWorldLabel>();
            if (label != null)
            {
                label.ShowSkill(Name);
            }
        }

        _inner.Execute(self, context);
    }

}
