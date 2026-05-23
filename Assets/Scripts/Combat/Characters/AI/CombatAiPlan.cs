public readonly struct CombatAiPlan
{
    public static readonly CombatAiPlan None = new CombatAiPlan(
        CombatObjective.Search,
        CombatMoveTarget.None,
        null,
        null);

    public CombatObjective Objective { get; }
    public CombatMoveTarget MoveTarget { get; }
    public SkillBase Skill { get; }
    public Character SkillTarget { get; }

    public CombatAiPlan(
        CombatObjective objective,
        CombatMoveTarget moveTarget,
        SkillBase skill,
        Character skillTarget)
    {
        Objective = objective;
        MoveTarget = moveTarget;
        Skill = skill;
        SkillTarget = skillTarget;
    }
}
