using System.Collections.Generic;
using UnityEngine;

public static class CombatSkillEvaluator
{
    public static CombatSkillEvaluationResult Evaluate(Character owner, SkillBase skill, SkillExecutionContext context)
    {
        if (owner == null || skill == null)
        {
            var request = new CombatSkillEvaluationRequest(
                owner,
                context.PrimaryTarget,
                context.PrimaryStone,
                context.HasTargetPoint,
                context.TargetPoint);
            return Evaluate(skill, request);
        }

        Vector3 originPoint = Flatten(owner.transform.position);
        Vector3 requestedPoint = context.HasTargetPoint ? Flatten(context.TargetPoint) : default;
        var baseResult = new CombatSkillEvaluationResult(
            canUse: false,
            failureReason: string.Empty,
            context: context,
            originPoint: originPoint,
            hasRangePreview: skill.MaxRange > 0f && !float.IsPositiveInfinity(skill.MaxRange),
            rangeRadius: skill.MaxRange,
            areaCenter: context.HasTargetPoint ? requestedPoint : default,
            hasAreaPreview: skill.AreaRadius > 0f &&
                (skill.TargetKind == SkillTargetKind.Point || skill.TargetKind == SkillTargetKind.Area),
            areaRadius: skill.AreaRadius,
            resolvedTargets: context.ResolvedTargets,
            resolvedStones: context.ResolvedStones);

        if (owner.Health == null || !owner.Health.CanAct)
        {
            return Fail(baseResult, "owner cannot act");
        }

        if (owner.SkillCooldowns != null && !owner.SkillCooldowns.IsReady(skill))
        {
            return Fail(baseResult, "cooldown " + owner.SkillCooldowns.GetRemainingSeconds(skill).ToString("0.0") + "s");
        }

        switch (skill.TargetKind)
        {
            case SkillTargetKind.RecognizedEnemies:
                return EvaluateProvidedTargets(baseResult, owner, skill, context.ResolvedTargets, context.ResolvedStones, requireEnemy: true, emptyReason: "no enemies");
            case SkillTargetKind.AllAllies:
                return EvaluateProvidedTargets(baseResult, owner, skill, context.ResolvedTargets, context.ResolvedStones, requireEnemy: false, emptyReason: "no allies");
            case SkillTargetKind.Area:
                if (!context.HasTargetPoint)
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

                if (!context.HasAnyResolvedTarget)
                {
                    return Fail(baseResult, "no targets in area");
                }

                if (!AreValidTargets(owner, skill, context.ResolvedTargets, context.ResolvedStones, requireEnemy: true))
                {
                    return Fail(baseResult, "invalid targets");
                }

                return Success(baseResult, context, context.ResolvedTargets, context.ResolvedStones);
            default:
            {
                var request = new CombatSkillEvaluationRequest(
                    owner,
                    context.PrimaryTarget,
                    context.PrimaryStone,
                    context.HasTargetPoint,
                    context.TargetPoint);
                return Evaluate(skill, request);
            }
        }
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
                return EvaluateEnemyTarget(baseResult, owner, skill, request.PrimaryTarget, request.PrimaryStone);

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

                SkillExecutionContext areaContext = CombatSkillTargeting.CreateEnemyAreaContext(
                    owner,
                    request.TargetPoint,
                    skill.AreaRadius,
                    includeMagicStones: skill.CanTargetMagicStone);
                if (!areaContext.HasAnyResolvedTarget)
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
                            resolvedTargets: areaContext.ResolvedTargets,
                            resolvedStones: areaContext.ResolvedStones),
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
                        resolvedTargets: areaContext.ResolvedTargets,
                        resolvedStones: areaContext.ResolvedStones),
                    areaContext,
                    areaContext.ResolvedTargets,
                    areaContext.ResolvedStones);

            case SkillTargetKind.RecognizedEnemies:
            {
                SkillExecutionContext recognizedEnemiesContext = CombatSkillTargeting.CreateRecognizedEnemiesContext(owner);
                if (!recognizedEnemiesContext.HasAnyResolvedTarget)
                {
                    return Fail(
                        new CombatSkillEvaluationResult(
                            canUse: false,
                            failureReason: "no enemies",
                            context: recognizedEnemiesContext,
                            originPoint: originPoint,
                            hasRangePreview: false,
                            rangeRadius: 0f,
                            areaCenter: default,
                            hasAreaPreview: false,
                            areaRadius: 0f,
                            resolvedTargets: recognizedEnemiesContext.ResolvedTargets,
                            resolvedStones: recognizedEnemiesContext.ResolvedStones),
                        "no enemies");
                }

                return Success(
                    new CombatSkillEvaluationResult(
                        canUse: true,
                        failureReason: string.Empty,
                        context: recognizedEnemiesContext,
                        originPoint: originPoint,
                        hasRangePreview: false,
                        rangeRadius: 0f,
                        areaCenter: default,
                        hasAreaPreview: false,
                        areaRadius: 0f,
                        resolvedTargets: recognizedEnemiesContext.ResolvedTargets,
                        resolvedStones: recognizedEnemiesContext.ResolvedStones),
                    recognizedEnemiesContext,
                    recognizedEnemiesContext.ResolvedTargets,
                    recognizedEnemiesContext.ResolvedStones);
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
        Character target,
        MagicStone stone)
    {
        if (target == null && stone == null)
        {
            return Fail(baseResult, "target missing");
        }

        if (stone != null)
        {
            if (!IsValidEnemyTarget(owner, skill, stone))
            {
                return Fail(baseResult, "stone not targetable");
            }

            SkillExecutionContext stoneContext = SkillExecutionContext.ForTarget(stone);
            return Success(baseResult, stoneContext, stoneContext.ResolvedTargets, stoneContext.ResolvedStones);
        }

        if (target.Health == null || !target.Health.IsTargetable)
        {
            return Fail(baseResult, "target not targetable");
        }

        if (target.Team == owner.Team)
        {
            return Fail(baseResult, "enemy required");
        }

        CombatVision vision = owner.Vision;
        if (ShouldRequireRecognition(owner, target) && !vision.HasRecognitionOf(target))
        {
            return Fail(baseResult, "enemy not recognized");
        }

        if (!IsInHorizontalRange(baseResult.OriginPoint, Flatten(target.transform.position), skill.MaxRange))
        {
            return Fail(baseResult, "target out of range");
        }

        SkillExecutionContext context = SkillExecutionContext.ForTarget(target);
        return Success(baseResult, context, context.ResolvedTargets, context.ResolvedStones);
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

    private static CombatSkillEvaluationResult EvaluateProvidedTargets(
        CombatSkillEvaluationResult baseResult,
        Character owner,
        SkillBase skill,
        IReadOnlyList<Character> targets,
        IReadOnlyList<MagicStone> stones,
        bool requireEnemy,
        string emptyReason)
    {
        if ((targets == null || targets.Count == 0) && (stones == null || stones.Count == 0))
        {
            return Fail(baseResult, emptyReason);
        }

        if (!AreValidTargets(owner, skill, targets, stones, requireEnemy))
        {
            return Fail(baseResult, "invalid targets");
        }

        SkillExecutionContext context = SkillExecutionContext.ForTargets(targets, stones);
        return Success(baseResult, context, targets, stones);
    }

    private static bool AreValidTargets(
        Character owner,
        SkillBase skill,
        IReadOnlyList<Character> targets,
        IReadOnlyList<MagicStone> stones,
        bool requireEnemy)
    {
        if (targets != null)
        {
            for (int i = 0; i < targets.Count; i++)
            {
                Character target = targets[i];
                bool isValid = requireEnemy
                    ? IsValidEnemyTarget(owner, skill, target)
                    : IsValidAllyTarget(owner, skill, target, allowSelf: true);
                if (!isValid)
                {
                    return false;
                }
            }
        }

        if (stones != null && stones.Count > 0)
        {
            if (!requireEnemy) return false;
            for (int i = 0; i < stones.Count; i++)
            {
                if (!IsValidEnemyTarget(owner, skill, stones[i])) return false;
            }
        }

        return true;
    }

    private static bool IsValidEnemyTarget(Character owner, SkillBase skill, Character target)
    {
        if (target == null || target.Health == null) return false;
        if (target.Team == owner.Team || !target.Health.IsTargetable) return false;
        CombatVision vision = owner.Vision;
        if (ShouldRequireRecognition(owner, target) && !vision.HasRecognitionOf(target)) return false;
        return IsInHorizontalRange(Flatten(owner.transform.position), Flatten(target.transform.position), skill.MaxRange);
    }

    private static bool IsValidEnemyTarget(Character owner, SkillBase skill, MagicStone stone)
    {
        if (skill == null || !skill.CanTargetMagicStone) return false;
        if (!CombatSkillTargeting.IsValidEnemyStone(owner, stone)) return false;
        if (!IsInHorizontalRange(Flatten(owner.transform.position), Flatten(stone.transform.position), skill.MaxRange))
        {
            return false;
        }

        // 魔石は位置既知。計画可否は遮蔽のみ（向き＝FOVは不要）。撃つ直前に石へ向いて本視線を確保する。
        CombatVision vision = owner.Vision;
        if (vision == null) return true;
        vision.UpdateVision();
        return vision.HasUnobstructedSight(stone.transform);
    }

    private static bool IsValidAllyTarget(Character owner, SkillBase skill, Character target, bool allowSelf)
    {
        if (target == null || target.Health == null) return false;
        if (target == owner)
        {
            return allowSelf && target.Health.IsAlive;
        }

        if (target.Team != owner.Team || !target.Health.IsAlive) return false;
        return IsInHorizontalRange(Flatten(owner.transform.position), Flatten(target.transform.position), skill.MaxRange);
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
            resolvedTargets: baseResult.ResolvedTargets,
            resolvedStones: baseResult.ResolvedStones);
    }

    private static CombatSkillEvaluationResult Success(
        CombatSkillEvaluationResult baseResult,
        SkillExecutionContext context,
        IReadOnlyList<Character> resolvedTargets = null,
        IReadOnlyList<MagicStone> resolvedStones = null)
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
            resolvedTargets: resolvedTargets ?? context.ResolvedTargets,
            resolvedStones: resolvedStones ?? context.ResolvedStones);
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

    private static bool ShouldRequireRecognition(Character owner, Character target)
    {
        if (owner == null || target == null || owner.Vision == null) return false;

        CombatSceneContext context = CombatSceneContext.Instance;
        if (context != null && context.CharacterSystem != null)
        {
            return ContainsCharacter(context.CharacterSystem, owner) &&
                ContainsCharacter(context.CharacterSystem, target);
        }

        CombatCharacterSystem[] systems = Object.FindObjectsByType<CombatCharacterSystem>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < systems.Length; i++)
        {
            CombatCharacterSystem system = systems[i];
            if (ContainsCharacter(system, owner) && ContainsCharacter(system, target))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsCharacter(CombatCharacterSystem system, Character character)
    {
        if (system == null || character == null) return false;
        return system.AllyCharacters.Contains(character) || system.EnemyCharacters.Contains(character);
    }
}
