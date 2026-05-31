using System.Collections.Generic;
using UnityEngine;

public readonly struct CombatSkillEvaluationResult
{
    public CombatSkillEvaluationResult(
        bool canUse,
        string failureReason,
        SkillExecutionContext context,
        Vector3 originPoint,
        bool hasRangePreview,
        float rangeRadius,
        Vector3 areaCenter,
        bool hasAreaPreview,
        float areaRadius,
        IReadOnlyList<Character> resolvedTargets)
    {
        CanUse = canUse;
        FailureReason = failureReason ?? string.Empty;
        Context = context;
        OriginPoint = originPoint;
        HasRangePreview = hasRangePreview;
        RangeRadius = rangeRadius;
        AreaCenter = areaCenter;
        HasAreaPreview = hasAreaPreview;
        AreaRadius = areaRadius;
        ResolvedTargets = resolvedTargets ?? System.Array.Empty<Character>();
    }

    public bool CanUse { get; }
    public string FailureReason { get; }
    public SkillExecutionContext Context { get; }
    public Vector3 OriginPoint { get; }
    public bool HasRangePreview { get; }
    public float RangeRadius { get; }
    public Vector3 AreaCenter { get; }
    public bool HasAreaPreview { get; }
    public float AreaRadius { get; }
    public IReadOnlyList<Character> ResolvedTargets { get; }
}
