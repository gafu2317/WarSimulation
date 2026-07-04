using System.Collections.Generic;
using UnityEngine;

public readonly struct SkillExecutionContext
{
    private readonly Dictionary<Character, (float Distance, float DamageMultiplier)> _capturedTargets;
    private readonly Vector4 _capturedStats;
    private readonly float _capturedPrimaryStoneDistance;

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
        IsCaptured = false;
        _capturedTargets = null;
        _capturedStats = default;
        _capturedPrimaryStoneDistance = 0f;
    }

    public Character PrimaryTarget { get; }
    public MagicStone PrimaryStone { get; }
    public bool HasTargetPoint { get; }
    public Vector3 TargetPoint { get; }
    public IReadOnlyList<Character> ResolvedTargets { get; }
    public IReadOnlyList<MagicStone> ResolvedStones { get; }
    public bool IsCaptured { get; }
    public bool HasAnyResolvedTarget => PrimaryTarget != null ||
        PrimaryStone != null ||
        ResolvedTargets.Count > 0 ||
        ResolvedStones.Count > 0;

    public SkillExecutionContext Capture(Character owner)
    {
        if (IsCaptured || owner == null) return this;

        Vector3 origin = owner.transform.position;
        var capturedTargets = new Dictionary<Character, (float Distance, float DamageMultiplier)>();
        CaptureTarget(capturedTargets, PrimaryTarget, owner, origin);
        for (int i = 0; i < ResolvedTargets.Count; i++)
        {
            CaptureTarget(capturedTargets, ResolvedTargets[i], owner, origin);
        }

        var stats = new Vector4(
            owner.GetEffectiveStat(CombatStat.STR),
            owner.GetEffectiveStat(CombatStat.INT),
            owner.GetEffectiveStat(CombatStat.FAI),
            owner.GetEffectiveStat(CombatStat.AGI));
        float stoneDistance = PrimaryStone != null
            ? HorizontalDistance(origin, PrimaryStone.transform.position)
            : 0f;
        return new SkillExecutionContext(this, capturedTargets, stats, stoneDistance);
    }

    public float GetEffectiveStat(CombatStat stat)
    {
        return IsCaptured ? _capturedStats[(int)stat] : 0f;
    }

    public float GetDistance(Character target)
    {
        return IsCaptured && target != null && _capturedTargets.TryGetValue(target, out var snapshot)
            ? snapshot.Distance
            : 0f;
    }

    public float GetDistance(MagicStone target)
    {
        return IsCaptured && target != null && target == PrimaryStone ? _capturedPrimaryStoneDistance : 0f;
    }

    public float GetDamageMultiplier(Character target)
    {
        return IsCaptured && target != null && _capturedTargets.TryGetValue(target, out var snapshot)
            ? snapshot.DamageMultiplier
            : 1f;
    }

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

    private SkillExecutionContext(
        SkillExecutionContext context,
        Dictionary<Character, (float Distance, float DamageMultiplier)> capturedTargets,
        Vector4 capturedStats,
        float capturedPrimaryStoneDistance)
        : this(
            context.PrimaryTarget,
            context.PrimaryStone,
            context.HasTargetPoint,
            context.TargetPoint,
            context.ResolvedTargets,
            context.ResolvedStones)
    {
        IsCaptured = true;
        _capturedTargets = capturedTargets;
        _capturedStats = capturedStats;
        _capturedPrimaryStoneDistance = capturedPrimaryStoneDistance;
    }

    private static void CaptureTarget(
        Dictionary<Character, (float Distance, float DamageMultiplier)> targets,
        Character target,
        Character owner,
        Vector3 origin)
    {
        if (target == null || targets.ContainsKey(target)) return;

        CombatVision vision = target.Vision;
        vision?.UpdateVision();
        float multiplier = vision != null && !vision.HasRecognitionOf(owner) ? 1.5f : 1f;
        targets[target] = (HorizontalDistance(origin, target.transform.position), multiplier);
    }

    private static float HorizontalDistance(Vector3 from, Vector3 to)
    {
        from.y = 0f;
        to.y = 0f;
        return Vector3.Distance(from, to);
    }
}
