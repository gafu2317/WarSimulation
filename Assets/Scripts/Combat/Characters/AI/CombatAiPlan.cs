public readonly struct CombatAiPlan
{
    public static readonly CombatAiPlan None = new CombatAiPlan(
        CombatObjective.Search,
        CombatMoveTarget.None,
        null,
        SkillExecutionContext.None);

    public CombatObjective Objective { get; }
    public CombatMoveTarget MoveTarget { get; }
    public SkillBase Skill { get; }
    public Character SkillTarget { get; }
    public SkillExecutionContext SkillContext { get; }

    public CombatAiPlan(
        CombatObjective objective,
        CombatMoveTarget moveTarget,
        SkillBase skill,
        Character skillTarget)
        : this(
            objective,
            moveTarget,
            skill,
            skillTarget != null ? SkillExecutionContext.ForTarget(skillTarget) : SkillExecutionContext.None)
    {
    }

    public CombatAiPlan(
        CombatObjective objective,
        CombatMoveTarget moveTarget,
        SkillBase skill,
        SkillExecutionContext skillContext)
    {
        Objective = objective;
        MoveTarget = moveTarget;
        Skill = skill;
        SkillContext = skillContext;
        SkillTarget = skillContext.PrimaryTarget;
    }
}
