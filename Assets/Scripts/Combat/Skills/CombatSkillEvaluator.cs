using System.Collections.Generic;
using UnityEngine;

public static class CombatSkillEvaluator
{
    public static CombatSkillEvaluationResult Evaluate(Character owner, SkillBase skill, SkillExecutionContext context)
    {
        var request = new CombatSkillEvaluationRequest(
            owner,
            context.PrimaryTarget,
            context.HasTargetPoint,
            context.TargetPoint);
        return Evaluate(skill, request);
    }

    public static CombatSkillEvaluationResult Evaluate(SkillBase skill, CombatSkillEvaluationRequest request)
    {
        Character owner = request.Owner;
        Vector3 originPoint = owner != null ? Flatten(owner.transform.position) : default;
        Vector3 requestedPoint = request.HasTargetPoint ? Flatten(request.TargetPoint) : default;

        var baseResult = new CombatSkillEvaluationResult(
            canUse: false,
            failureReason: string.Empty,
            context: SkillExecutionContext.None,
            originPoint: originPoint,
            hasRangePreview: skill != null && skill.MaxRange > 0f && !float.IsPositiveInfinity(skill.MaxRange),
            rangeRadius: skill != null ? skill.MaxRange : 0f,
            areaCenter: request.HasTargetPoint ? requestedPoint : default,
            hasAreaPreview: skill != null &&
                skill.AreaRadius > 0f &&
                (skill.TargetKind == SkillTargetKind.Point || skill.TargetKind == SkillTargetKind.Area),
            areaRadius: skill != null ? skill.AreaRadius : 0f,
            resolvedTargets: System.Array.Empty<Character>());

        if (owner == null)
        {
            return Fail(baseResult, "owner missing");
        }

        if (owner.Health == null || !owner.Health.CanAct)
        {
            return Fail(baseResult, "owner cannot act");
        }

        if (skill == null)
        {
            return Fail(baseResult, "skill missing");
        }

        if (owner.SkillCooldowns != null && !owner.SkillCooldowns.IsReady(skill))
        {
            return Fail(baseResult, "cooldown " + owner.SkillCooldowns.GetRemainingSeconds(skill).ToString("0.0") + "s");
        }

        switch (skill.TargetKind)
        {
            case SkillTargetKind.None:
                return Success(baseResult, SkillExecutionContext.None);

            case SkillTargetKind.Self:
                return Success(
                    baseResult,
                    SkillExecutionContext.ForSelf(owner),
                    resolvedTargets: new[] { owner });

            case SkillTargetKind.Enemy:
                return EvaluateEnemyTarget(baseResult, owner, skill, request.PrimaryTarget);

            case SkillTargetKind.Ally:
                return EvaluateAllyTarget(baseResult, owner, skill, request.PrimaryTarget, allowSelf: false);

            case SkillTargetKind.AllyOrSelf:
                return EvaluateAllyTarget(baseResult, owner, skill, request.PrimaryTarget, allowSelf: true);

            case SkillTargetKind.Point:
                if (!request.HasTargetPoint)
                {
                    return Fail(baseResult, "point missing");
                }

                if (!IsInHorizontalRange(originPoint, requestedPoint, skill.MaxRange))
                {
                    return Fail(baseResult, "point out of range");
                }

                return Success(
                    new CombatSkillEvaluationResult(
                        canUse: true,
                        failureReason: string.Empty,
                        context: SkillExecutionContext.ForPoint(request.TargetPoint),
                        originPoint: originPoint,
                        hasRangePreview: baseResult.HasRangePreview,
                        rangeRadius: baseResult.RangeRadius,
                        areaCenter: requestedPoint,
                        hasAreaPreview: baseResult.HasAreaPreview,
                        areaRadius: baseResult.AreaRadius,
                        resolvedTargets: System.Array.Empty<Character>()),
                    SkillExecutionContext.ForPoint(request.TargetPoint));

            case SkillTargetKind.Area:
                if (!request.HasTargetPoint)
                {
                    return Fail(baseResult, "point missing");
                }

                if (!IsInHorizontalRange(originPoint, requestedPoint, skill.MaxRange))
                {
                    return Fail(baseResult, "point out of range");
                }

                if (skill.AreaRadius <= 0f)
                {
                    return Fail(baseResult, "area radius missing");
                }

                SkillExecutionContext areaContext = CombatSkillTargeting.CreateEnemyAreaContext(owner, request.TargetPoint, skill.AreaRadius);
                if (areaContext.ResolvedTargets == null || areaContext.ResolvedTargets.Count == 0)
                {
                    return Fail(
                        new CombatSkillEvaluationResult(
                            canUse: false,
                            failureReason: "no targets in area",
                            context: areaContext,
                            originPoint: originPoint,
                            hasRangePreview: baseResult.HasRangePreview,
                            rangeRadius: baseResult.RangeRadius,
                            areaCenter: requestedPoint,
                            hasAreaPreview: true,
                            areaRadius: skill.AreaRadius,
                            resolvedTargets: areaContext.ResolvedTargets),
                        "no targets in area");
                }

                return Success(
                    new CombatSkillEvaluationResult(
                        canUse: true,
                        failureReason: string.Empty,
                        context: areaContext,
                        originPoint: originPoint,
                        hasRangePreview: baseResult.HasRangePreview,
                        rangeRadius: baseResult.RangeRadius,
                        areaCenter: requestedPoint,
                        hasAreaPreview: true,
                        areaRadius: skill.AreaRadius,
                        resolvedTargets: areaContext.ResolvedTargets),
                    areaContext,
                    areaContext.ResolvedTargets);

            case SkillTargetKind.AllEnemies:
            {
                SkillExecutionContext allEnemiesContext = CombatSkillTargeting.CreateAllEnemiesContext(owner);
                if (allEnemiesContext.ResolvedTargets == null || allEnemiesContext.ResolvedTargets.Count == 0)
                {
                    return Fail(
                        new CombatSkillEvaluationResult(
                            canUse: false,
                            failureReason: "no enemies",
                            context: allEnemiesContext,
                            originPoint: originPoint,
                            hasRangePreview: false,
                            rangeRadius: 0f,
                            areaCenter: default,
                            hasAreaPreview: false,
                            areaRadius: 0f,
                            resolvedTargets: allEnemiesContext.ResolvedTargets),
                        "no enemies");
                }

                return Success(
                    new CombatSkillEvaluationResult(
                        canUse: true,
                        failureReason: string.Empty,
                        context: allEnemiesContext,
                        originPoint: originPoint,
                        hasRangePreview: false,
                        rangeRadius: 0f,
                        areaCenter: default,
                        hasAreaPreview: false,
                        areaRadius: 0f,
                        resolvedTargets: allEnemiesContext.ResolvedTargets),
                    allEnemiesContext,
                    allEnemiesContext.ResolvedTargets);
            }

            case SkillTargetKind.AllAllies:
            {
                SkillExecutionContext allAlliesContext = CombatSkillTargeting.CreateAllAlliesContext(owner, includeSelf: true);
                if (allAlliesContext.ResolvedTargets == null || allAlliesContext.ResolvedTargets.Count == 0)
                {
                    return Fail(
                        new CombatSkillEvaluationResult(
                            canUse: false,
                            failureReason: "no allies",
                            context: allAlliesContext,
                            originPoint: originPoint,
                            hasRangePreview: false,
                            rangeRadius: 0f,
                            areaCenter: default,
                            hasAreaPreview: false,
                            areaRadius: 0f,
                            resolvedTargets: allAlliesContext.ResolvedTargets),
                        "no allies");
                }

                return Success(
                    new CombatSkillEvaluationResult(
                        canUse: true,
                        failureReason: string.Empty,
                        context: allAlliesContext,
                        originPoint: originPoint,
                        hasRangePreview: false,
                        rangeRadius: 0f,
                        areaCenter: default,
                        hasAreaPreview: false,
                        areaRadius: 0f,
                        resolvedTargets: allAlliesContext.ResolvedTargets),
                    allAlliesContext,
                    allAlliesContext.ResolvedTargets);
            }

            default:
                return Fail(baseResult, "unsupported target kind");
        }
    }

    private static CombatSkillEvaluationResult EvaluateEnemyTarget(
        CombatSkillEvaluationResult baseResult,
        Character owner,
        SkillBase skill,
        Character target)
    {
        if (target == null)
        {
            return Fail(baseResult, "target missing");
        }

        if (target.Health == null || !target.Health.IsTargetable)
        {
            return Fail(baseResult, "target not targetable");
        }

        if (target.Team == owner.Team)
        {
            return Fail(baseResult, "enemy required");
        }

        if (!IsInHorizontalRange(baseResult.OriginPoint, Flatten(target.transform.position), skill.MaxRange))
        {
            return Fail(baseResult, "target out of range");
        }

        SkillExecutionContext context = SkillExecutionContext.ForTarget(target);
        return Success(baseResult, context, context.ResolvedTargets);
    }

    private static CombatSkillEvaluationResult EvaluateAllyTarget(
        CombatSkillEvaluationResult baseResult,
        Character owner,
        SkillBase skill,
        Character target,
        bool allowSelf)
    {
        if (target == null)
        {
            return Fail(baseResult, "target missing");
        }

        if (target == owner)
        {
            if (!allowSelf)
            {
                return Fail(baseResult, "self not allowed");
            }

            if (target.Health == null || !target.Health.IsAlive)
            {
                return Fail(baseResult, "self not alive");
            }

            SkillExecutionContext selfContext = SkillExecutionContext.ForTarget(target);
            return Success(baseResult, selfContext, selfContext.ResolvedTargets);
        }

        if (target.Team != owner.Team)
        {
            return Fail(baseResult, "ally required");
        }

        if (target.Health == null || !target.Health.IsAlive)
        {
            return Fail(baseResult, "ally not alive");
        }

        if (!IsInHorizontalRange(baseResult.OriginPoint, Flatten(target.transform.position), skill.MaxRange))
        {
            return Fail(baseResult, "ally out of range");
        }

        SkillExecutionContext context = SkillExecutionContext.ForTarget(target);
        return Success(baseResult, context, context.ResolvedTargets);
    }

    private static CombatSkillEvaluationResult Fail(CombatSkillEvaluationResult baseResult, string reason)
    {
        return new CombatSkillEvaluationResult(
            canUse: false,
            failureReason: reason,
            context: baseResult.Context,
            originPoint: baseResult.OriginPoint,
            hasRangePreview: baseResult.HasRangePreview,
            rangeRadius: baseResult.RangeRadius,
            areaCenter: baseResult.AreaCenter,
            hasAreaPreview: baseResult.HasAreaPreview,
            areaRadius: baseResult.AreaRadius,
            resolvedTargets: baseResult.ResolvedTargets);
    }

    private static CombatSkillEvaluationResult Success(
        CombatSkillEvaluationResult baseResult,
        SkillExecutionContext context,
        IReadOnlyList<Character> resolvedTargets = null)
    {
        return new CombatSkillEvaluationResult(
            canUse: true,
            failureReason: string.Empty,
            context: context,
            originPoint: baseResult.OriginPoint,
            hasRangePreview: baseResult.HasRangePreview,
            rangeRadius: baseResult.RangeRadius,
            areaCenter: baseResult.AreaCenter,
            hasAreaPreview: baseResult.HasAreaPreview,
            areaRadius: baseResult.AreaRadius,
            resolvedTargets: resolvedTargets ?? context.ResolvedTargets);
    }

    private static bool IsInHorizontalRange(Vector3 from, Vector3 to, float maxRange)
    {
        if (float.IsPositiveInfinity(maxRange)) return true;
        if (maxRange < 0f) return false;

        Vector3 delta = Flatten(to) - Flatten(from);
        return delta.sqrMagnitude <= maxRange * maxRange;
    }

    private static Vector3 Flatten(Vector3 value)
    {
        value.y = 0f;
        return value;
    }
}
