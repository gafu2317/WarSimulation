public readonly struct CombatAiPlan
{
    public static readonly CombatAiPlan None = new CombatAiPlan(
        CombatObjective.Search,
        CombatMoveTarget.None,
        null,
        SkillExecutionContext.None,
        CombatAiMoveCode.HoldPosition,
        CombatAiReasonCode.None);

    public CombatObjective Objective { get; }
    public CombatMoveTarget MoveTarget { get; }
    public SkillBase Skill { get; }
    public Character SkillTarget { get; }
    public SkillExecutionContext SkillContext { get; }
    public string ActionCode { get; }
    public CombatAiReasonCode TransitionReason { get; }

    public CombatAiPlan(
        CombatObjective objective,
        CombatMoveTarget moveTarget,
        SkillBase skill,
        Character skillTarget)
        : this(
            objective,
            moveTarget,
            skill,
            skillTarget != null ? SkillExecutionContext.ForTarget(skillTarget) : SkillExecutionContext.None,
            CombatAiMoveCode.HoldPosition,
            CombatAiReasonCode.None)
    {
    }

    public CombatAiPlan(
        CombatObjective objective,
        CombatMoveTarget moveTarget,
        SkillBase skill,
        SkillExecutionContext skillContext)
        : this(
            objective,
            moveTarget,
            skill,
            skillContext,
            CombatAiMoveCode.HoldPosition,
            CombatAiReasonCode.None)
    {
    }

    public CombatAiPlan(
        CombatObjective objective,
        CombatMoveTarget moveTarget,
        SkillBase skill,
        SkillExecutionContext skillContext,
        string actionCode,
        CombatAiReasonCode transitionReason)
    {
        Objective = objective;
        MoveTarget = moveTarget;
        Skill = skill;
        SkillContext = skillContext;
        SkillTarget = skillContext.PrimaryTarget;
        ActionCode = string.IsNullOrEmpty(actionCode) ? CombatAiMoveCode.HoldPosition : actionCode;
        TransitionReason = transitionReason;
    }
}
