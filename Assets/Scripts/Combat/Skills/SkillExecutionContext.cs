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
        : this(
            primaryTarget,
            null,
            hasTargetPoint,
            targetPoint,
            resolvedTargets,
            System.Array.Empty<MagicStone>())
    {
    }

    public SkillExecutionContext(
        Character primaryTarget,
        MagicStone primaryStone,
        bool hasTargetPoint,
        Vector3 targetPoint,
        IReadOnlyList<Character> resolvedTargets,
        IReadOnlyList<MagicStone> resolvedStones)
    {
        PrimaryTarget = primaryTarget;
        PrimaryStone = primaryStone;
        HasTargetPoint = hasTargetPoint;
        TargetPoint = targetPoint;
        ResolvedTargets = resolvedTargets ?? System.Array.Empty<Character>();
        ResolvedStones = resolvedStones ?? System.Array.Empty<MagicStone>();
    }

    public Character PrimaryTarget { get; }
    public MagicStone PrimaryStone { get; }
    public bool HasTargetPoint { get; }
    public Vector3 TargetPoint { get; }
    public IReadOnlyList<Character> ResolvedTargets { get; }
    public IReadOnlyList<MagicStone> ResolvedStones { get; }
    public bool HasAnyResolvedTarget => PrimaryTarget != null ||
        PrimaryStone != null ||
        ResolvedTargets.Count > 0 ||
        ResolvedStones.Count > 0;

    public static SkillExecutionContext ForTarget(Character primaryTarget)
    {
        return new SkillExecutionContext(
            primaryTarget,
            null,
            hasTargetPoint: false,
            default,
            primaryTarget != null ? new[] { primaryTarget } : System.Array.Empty<Character>(),
            System.Array.Empty<MagicStone>());
    }

    public static SkillExecutionContext ForTarget(MagicStone primaryStone)
    {
        return new SkillExecutionContext(
            null,
            primaryStone,
            hasTargetPoint: false,
            default,
            System.Array.Empty<Character>(),
            primaryStone != null ? new[] { primaryStone } : System.Array.Empty<MagicStone>());
    }

    public static SkillExecutionContext ForTargets(IReadOnlyList<Character> resolvedTargets)
    {
        return ForTargets(resolvedTargets, System.Array.Empty<MagicStone>());
    }

    public static SkillExecutionContext ForTargets(
        IReadOnlyList<Character> resolvedTargets,
        IReadOnlyList<MagicStone> resolvedStones)
    {
        Character primaryTarget = resolvedTargets != null && resolvedTargets.Count > 0
            ? resolvedTargets[0]
            : null;
        MagicStone primaryStone = primaryTarget == null && resolvedStones != null && resolvedStones.Count > 0
            ? resolvedStones[0]
            : null;
        return new SkillExecutionContext(
            primaryTarget,
            primaryStone,
            hasTargetPoint: false,
            default,
            resolvedTargets ?? System.Array.Empty<Character>(),
            resolvedStones ?? System.Array.Empty<MagicStone>());
    }

    public static SkillExecutionContext ForSelf(Character self)
    {
        return ForTarget(self);
    }

    public static SkillExecutionContext ForPoint(Vector3 targetPoint)
    {
        return new SkillExecutionContext(
            null,
            null,
            hasTargetPoint: true,
            targetPoint,
            System.Array.Empty<Character>(),
            System.Array.Empty<MagicStone>());
    }

    public static SkillExecutionContext ForPoint(
        Vector3 targetPoint,
        IReadOnlyList<Character> resolvedTargets)
    {
        return ForPoint(targetPoint, resolvedTargets, System.Array.Empty<MagicStone>());
    }

    public static SkillExecutionContext ForPoint(
        Vector3 targetPoint,
        IReadOnlyList<Character> resolvedTargets,
        IReadOnlyList<MagicStone> resolvedStones)
    {
        Character primaryTarget = resolvedTargets != null && resolvedTargets.Count > 0
            ? resolvedTargets[0]
            : null;
        MagicStone primaryStone = primaryTarget == null && resolvedStones != null && resolvedStones.Count > 0
            ? resolvedStones[0]
            : null;
        return new SkillExecutionContext(
            primaryTarget,
            primaryStone,
            hasTargetPoint: true,
            targetPoint,
            resolvedTargets ?? System.Array.Empty<Character>(),
            resolvedStones ?? System.Array.Empty<MagicStone>());
    }
}
