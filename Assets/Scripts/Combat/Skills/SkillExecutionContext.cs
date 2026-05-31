using System.Collections.Generic;
using UnityEngine;

public readonly struct SkillExecutionContext
{
    public static readonly SkillExecutionContext None = new SkillExecutionContext(
        null,
        hasTargetPoint: false,
        default,
        System.Array.Empty<Character>());

    public SkillExecutionContext(
        Character primaryTarget,
        bool hasTargetPoint,
        Vector3 targetPoint,
        IReadOnlyList<Character> resolvedTargets)
    {
        PrimaryTarget = primaryTarget;
        HasTargetPoint = hasTargetPoint;
        TargetPoint = targetPoint;
        ResolvedTargets = resolvedTargets ?? System.Array.Empty<Character>();
    }

    public Character PrimaryTarget { get; }
    public bool HasTargetPoint { get; }
    public Vector3 TargetPoint { get; }
    public IReadOnlyList<Character> ResolvedTargets { get; }

    public static SkillExecutionContext ForTarget(Character primaryTarget)
    {
        return new SkillExecutionContext(
            primaryTarget,
            hasTargetPoint: false,
            default,
            primaryTarget != null ? new[] { primaryTarget } : System.Array.Empty<Character>());
    }

    public static SkillExecutionContext ForTargets(IReadOnlyList<Character> resolvedTargets)
    {
        Character primaryTarget = resolvedTargets != null && resolvedTargets.Count > 0
            ? resolvedTargets[0]
            : null;
        return new SkillExecutionContext(
            primaryTarget,
            hasTargetPoint: false,
            default,
            resolvedTargets ?? System.Array.Empty<Character>());
    }

    public static SkillExecutionContext ForSelf(Character self)
    {
        return ForTarget(self);
    }

    public static SkillExecutionContext ForPoint(Vector3 targetPoint)
    {
        return new SkillExecutionContext(
            null,
            hasTargetPoint: true,
            targetPoint,
            System.Array.Empty<Character>());
    }

    public static SkillExecutionContext ForPoint(
        Vector3 targetPoint,
        IReadOnlyList<Character> resolvedTargets)
    {
        Character primaryTarget = resolvedTargets != null && resolvedTargets.Count > 0
            ? resolvedTargets[0]
            : null;
        return new SkillExecutionContext(
            primaryTarget,
            hasTargetPoint: true,
            targetPoint,
            resolvedTargets ?? System.Array.Empty<Character>());
    }
}
