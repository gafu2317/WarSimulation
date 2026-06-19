using UnityEngine;

public readonly struct CombatSkillEvaluationRequest
{
    public CombatSkillEvaluationRequest(
        Character owner,
        Character primaryTarget,
        bool hasTargetPoint,
        Vector3 targetPoint)
        : this(owner, primaryTarget, null, hasTargetPoint, targetPoint)
    {
    }

    public CombatSkillEvaluationRequest(
        Character owner,
        Character primaryTarget,
        MagicStone primaryStone,
        bool hasTargetPoint,
        Vector3 targetPoint)
    {
        Owner = owner;
        PrimaryTarget = primaryTarget;
        PrimaryStone = primaryStone;
        HasTargetPoint = hasTargetPoint;
        TargetPoint = targetPoint;
    }

    public Character Owner { get; }
    public Character PrimaryTarget { get; }
    public MagicStone PrimaryStone { get; }
    public bool HasTargetPoint { get; }
    public Vector3 TargetPoint { get; }

    public static CombatSkillEvaluationRequest ForTarget(Character owner, Character primaryTarget)
    {
        return new CombatSkillEvaluationRequest(owner, primaryTarget, false, default);
    }

    public static CombatSkillEvaluationRequest ForTarget(Character owner, MagicStone primaryStone)
    {
        return new CombatSkillEvaluationRequest(owner, null, primaryStone, false, default);
    }

    public static CombatSkillEvaluationRequest ForSelf(Character owner)
    {
        return ForTarget(owner, owner);
    }

    public static CombatSkillEvaluationRequest ForPoint(Character owner, Vector3 targetPoint)
    {
        return new CombatSkillEvaluationRequest(owner, null, true, targetPoint);
    }
}
