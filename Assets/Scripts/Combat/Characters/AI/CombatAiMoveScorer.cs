using UnityEngine;
using UnityEngine.AI;

public static class CombatAiMoveScorer
{
    public static CombatAiScoreBreakdown Score(
        CombatAiDebugSnapshot snapshot,
        CombatAiPersonalityProfile personalityProfile,
        CombatAiWeaponWeightsProfile weaponWeightsProfile,
        string code,
        CombatMoveTarget target,
        CombatObjective objective,
        Character focusEnemy,
        float focusCommitmentRemainingSeconds)
    {
        float distance = GetTravelDistance(snapshot.Context, code, target);
        WeaponKind weaponKind = snapshot.Owner.EquippedWeapon != null ? snapshot.Owner.EquippedWeapon.Kind : WeaponKind.Unarmed;
        var breakdown = new CombatAiScoreBreakdown
        {
            BaseScore = target.HasDestination ? Mathf.Lerp(24f, 4f, Mathf.Clamp01(distance / 40f)) : 8f,
            SituationScore = GetSituationScore(snapshot.Assessment, code, objective)
                + GetHighGroundUtilityScore(snapshot.Owner, snapshot.Context, snapshot.Assessment, code, target)
                + CombatAiFocusTargeting.GetMoveScore(
                    snapshot.Context,
                    snapshot.Owner.EquippedWeapon,
                    code,
                    target,
                    focusEnemy,
                    focusCommitmentRemainingSeconds)
                + GetCoverBonus(weaponKind, snapshot.Assessment, code)
                - GetAllyDestinationOverlapPenalty(snapshot.Context, code, target)
                - GetSwordIsolationPenalty(snapshot.Context, snapshot.Assessment, weaponKind, code, target)
                - GetRouteRiskPenalty(snapshot.Context, personalityProfile, code, target),
            WeaponScore = GetWeaponScore(weaponWeightsProfile, snapshot.Owner.EquippedWeapon, code),
            PersonalityScore = GetPersonalityScore(personalityProfile, code, objective),
        };
        AddReasons(code, breakdown);
        if (breakdown.WeaponScore != 0f) AddReason(breakdown, CombatAiReasonCode.WeaponPreference);
        if (breakdown.PersonalityScore != 0f) AddReason(breakdown, CombatAiReasonCode.PersonalityPreference);
        if (GetCoverBonus(weaponKind, snapshot.Assessment, code) > 0f) AddReason(breakdown, CombatAiReasonCode.SelfExposedByEnemy);
        if (GetRouteRiskPenalty(snapshot.Context, personalityProfile, code, target) >= 12f) AddReason(breakdown, CombatAiReasonCode.RouteRiskHigh);
        return breakdown;
    }

    public static float ScoreDirect(
        Character owner,
        CombatAiContext context,
        CombatAiAssessment assessment,
        CombatAiPersonalityProfile personalityProfile,
        CombatAiWeaponWeightsProfile weaponWeightsProfile,
        string code,
        CombatMoveTarget target,
        CombatObjective objective,
        Character focusEnemy,
        float focusCommitmentRemainingSeconds)
    {
        float distance = GetTravelDistance(context, code, target);
        WeaponKind weaponKind = owner.EquippedWeapon != null ? owner.EquippedWeapon.Kind : WeaponKind.Unarmed;
        return (target.HasDestination ? Mathf.Lerp(24f, 4f, Mathf.Clamp01(distance / 40f)) : 8f)
            + GetSituationScore(assessment, code, objective)
            + GetHighGroundUtilityScore(owner, context, assessment, code, target)
            + CombatAiFocusTargeting.GetMoveScore(context, owner.EquippedWeapon, code, target, focusEnemy, focusCommitmentRemainingSeconds)
            + GetWeaponScore(weaponWeightsProfile, owner.EquippedWeapon, code)
            + GetPersonalityScore(personalityProfile, code, objective)
            + GetCoverBonus(weaponKind, assessment, code)
            - GetAllyDestinationOverlapPenalty(context, code, target)
            - GetSwordIsolationPenalty(context, assessment, weaponKind, code, target)
            - GetRouteRiskPenalty(context, personalityProfile, code, target);
    }

    private static float GetRouteRiskPenalty(
        CombatAiContext context,
        CombatAiPersonalityProfile personalityProfile,
        string code,
        CombatMoveTarget target)
    {
        if (context == null || context.Owner == null || !target.HasDestination) return 0f;

        Vector3 start = context.Owner.transform.position;
        float risk = EvaluateRouteRisk(context, start, target.Destination);
        if (code == CombatAiMoveCode.AdvanceViaBridge && context.HasEnemyStonePosition)
        {
            float remainingRisk = EvaluateRouteRisk(context, target.Destination, context.EnemyStonePosition);
            risk = (risk + remainingRisk) * 0.5f;
        }

        float multiplier = personalityProfile != null && personalityProfile.Kind == CombatAiPersonalityKind.Cautious
            ? 1.5f
            : 0.55f;
        return risk * multiplier;
    }

    private static float GetTravelDistance(CombatAiContext context, string code, CombatMoveTarget target)
    {
        if (context == null || context.Owner == null || !target.HasDestination) return 0f;

        float distance = EvaluateRouteDistance(context.Owner.transform.position, target.Destination);
        if (code == CombatAiMoveCode.AdvanceViaBridge && context.HasEnemyStonePosition)
        {
            distance += EvaluateRouteDistance(target.Destination, context.EnemyStonePosition);
        }

        return distance;
    }

    private static float EvaluateRouteDistance(Vector3 start, Vector3 destination)
    {
        var path = new NavMeshPath();
        if (!NavMesh.CalculatePath(start, destination, NavMesh.AllAreas, path) || path.corners.Length < 2)
        {
            return HorizontalDistance(start, destination);
        }

        float distance = 0f;
        for (int i = 1; i < path.corners.Length; i++)
        {
            distance += HorizontalDistance(path.corners[i - 1], path.corners[i]);
        }

        return distance;
    }

    public static float EvaluateRouteRisk(CombatAiContext context, Vector3 start, Vector3 destination)
    {
        if (context == null || context.EnemyIntel.Count == 0) return 0f;

        var path = new NavMeshPath();
        Vector3[] corners = NavMesh.CalculatePath(start, destination, NavMesh.AllAreas, path) && path.corners.Length >= 2
            ? path.corners
            : new[] { start, destination };
        float danger = 0f;
        int sampleCount = 0;
        for (int i = 1; i < corners.Length; i++)
        {
            Vector3 from = corners[i - 1];
            Vector3 to = corners[i];
            float length = HorizontalDistance(from, to);
            int segmentSamples = Mathf.Max(1, Mathf.CeilToInt(length / 2f));
            for (int sample = 1; sample <= segmentSamples; sample++)
            {
                Vector3 point = Vector3.Lerp(from, to, sample / (float)segmentSamples);
                danger += EvaluatePointDanger(context, point);
                sampleCount++;
            }
        }

        return sampleCount > 0 ? Mathf.Clamp(danger / sampleCount * 100f, 0f, 100f) : 0f;
    }

    private static float EvaluatePointDanger(CombatAiContext context, Vector3 point)
    {
        float danger = 0f;
        for (int i = 0; i < context.EnemyIntel.Count; i++)
        {
            CombatCharacterIntel enemy = context.EnemyIntel[i];
            if (!enemy.IsAlive || !enemy.HasKnownPosition || !enemy.CanAct) continue;

            float dangerRadius = Mathf.Max(1.5f, enemy.WeaponRange + 2f);
            float distance = HorizontalDistance(point, enemy.KnownPosition);
            if (distance > dangerRadius || !HasLineOfSight(enemy, point)) continue;

            danger += Mathf.Lerp(1f, 0.15f, Mathf.Clamp01(distance / dangerRadius));
        }

        int supportingAllies = 0;
        for (int i = 0; i < context.AllyIntel.Count; i++)
        {
            CombatCharacterIntel ally = context.AllyIntel[i];
            if (ally.CanAct && HorizontalDistance(point, ally.CurrentPosition) <= 6f)
            {
                supportingAllies++;
            }
        }

        return Mathf.Clamp01(danger / (1f + supportingAllies * 0.25f));
    }

    private static bool HasLineOfSight(CombatCharacterIntel enemy, Vector3 point)
    {
        Vector3 from = enemy.KnownPosition + Vector3.up;
        Vector3 to = point + Vector3.up;
        if (!Physics.Linecast(from, to, out RaycastHit hit, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            return true;
        }

        Character hitCharacter = hit.collider != null ? hit.collider.GetComponentInParent<Character>() : null;
        return hitCharacter == enemy.Character;
    }

    private static float GetAllyDestinationOverlapPenalty(CombatAiContext context, string code, CombatMoveTarget target)
    {
        if (context == null || !target.HasDestination) return 0f;

        float penalty = 0f;
        for (int i = 0; i < context.AllyIntel.Count; i++)
        {
            CombatCharacterIntel ally = context.AllyIntel[i];
            if (!ally.HasIntendedDestination) continue;
            if (HorizontalDistance(ally.IntendedDestination, target.Destination) <= 3f)
            {
                if (code == CombatAiMoveCode.TakeHighGround &&
                    ally.HasObjective && ally.Objective == CombatObjective.Search)
                {
                    return 36f;
                }

                penalty += 12f;
            }
        }

        return Mathf.Min(36f, penalty);
    }

    private static float GetSwordIsolationPenalty(
        CombatAiContext context,
        CombatAiAssessment assessment,
        WeaponKind weaponKind,
        string code,
        CombatMoveTarget target)
    {
        if (context == null || weaponKind != WeaponKind.Sword || code != CombatAiMoveCode.PursueEnemy || !target.HasDestination)
        {
            return 0f;
        }

        int nearbyEnemies = 0;
        for (int i = 0; i < context.EnemyIntel.Count; i++)
        {
            CombatCharacterIntel enemy = context.EnemyIntel[i];
            if (enemy.IsAlive && enemy.CanAct && enemy.HasKnownPosition && HorizontalDistance(enemy.KnownPosition, target.Destination) <= 6f)
            {
                nearbyEnemies++;
            }
        }

        int supportingAllies = 0;
        float nearestAllyDistance = float.PositiveInfinity;
        for (int i = 0; i < context.AllyIntel.Count; i++)
        {
            CombatCharacterIntel ally = context.AllyIntel[i];
            if (!ally.CanAct) continue;
            float distance = HorizontalDistance(ally.CurrentPosition, target.Destination);
            nearestAllyDistance = Mathf.Min(nearestAllyDistance, distance);
            if (distance <= 8f) supportingAllies++;
        }

        float penalty = Mathf.Max(0, nearbyEnemies - supportingAllies - 1) * 18f;
        if (nearestAllyDistance > 12f) penalty += 18f;
        else if (nearestAllyDistance > 8f) penalty += 8f;

        if (assessment.GetValue(CombatAiMetricIndex.KillableTargetValue) > 0f)
        {
            penalty *= 0.35f;
        }

        return penalty;
    }

    private static float GetCoverBonus(WeaponKind weaponKind, CombatAiAssessment assessment, string code)
    {
        if (code != CombatAiMoveCode.MoveForest) return 0f;

        switch (weaponKind)
        {
            case WeaponKind.Wand:
            case WeaponKind.Grimoire:
            case WeaponKind.Bible:
            case WeaponKind.Rosary:
                return assessment.GetValue(CombatAiMetricIndex.SelfExposure) * 0.6f;
            default:
                return 0f;
        }
    }

    private static float GetSituationScore(CombatAiAssessment assessment, string code, CombatObjective objective)
    {
        return code switch
        {
            CombatAiMoveCode.AdvanceEnemyStone => assessment.GetValue(CombatAiMetricIndex.EnemyStoneReachability) * 0.6f
                - assessment.GetValue(CombatAiMetricIndex.OwnStoneThreat) * 0.2f
                + GetSearchAdvanceBonus(assessment, objective),
            CombatAiMoveCode.AdvanceViaBridge => assessment.GetValue(CombatAiMetricIndex.EnemyStoneReachability) * 0.55f
                - assessment.GetValue(CombatAiMetricIndex.OwnStoneThreat) * 0.2f,
            CombatAiMoveCode.ReturnOwnStone => assessment.GetValue(CombatAiMetricIndex.OwnStoneThreat) * 0.65f + assessment.GetValue(CombatAiMetricIndex.RetreatRouteSafety) * 0.2f,
            CombatAiMoveCode.PursueEnemy => assessment.GetValue(CombatAiMetricIndex.ReachableEnemyValue) * 0.65f + assessment.GetValue(CombatAiMetricIndex.EnemyLocationConfidence) * 0.1f,
            CombatAiMoveCode.SupportAlly => assessment.GetValue(CombatAiMetricIndex.AllyFragility) * 0.65f,
            CombatAiMoveCode.InterceptThreat => assessment.GetValue(CombatAiMetricIndex.AllyFragility) * 0.45f
                + assessment.GetValue(CombatAiMetricIndex.OwnStoneThreat) * 0.45f,
            CombatAiMoveCode.TakeHighGround => assessment.GetValue(CombatAiMetricIndex.TerrainAdvantage) * 0.4f,
            CombatAiMoveCode.MoveForest => assessment.GetValue(CombatAiMetricIndex.RetreatRouteSafety) * 0.3f + assessment.GetValue(CombatAiMetricIndex.TerrainAdvantage) * 0.3f,
            CombatAiMoveCode.SearchLastKnown => (100f - assessment.GetValue(CombatAiMetricIndex.EnemyLocationConfidence)) * 0.45f,
            CombatAiMoveCode.HoldPosition => objective == CombatObjective.DefendOwnStone ? 12f : 2f,
            _ => 0f,
        } + GetObjectiveAlignmentScore(code, objective);
    }

    private static float GetWeaponScore(
        CombatAiWeaponWeightsProfile weaponWeightsProfile,
        WeaponBase weapon,
        string code)
    {
        WeaponKind kind = weapon != null ? weapon.Kind : WeaponKind.Unarmed;
        if (code == CombatAiMoveCode.InterceptThreat)
        {
            return kind == WeaponKind.Shield ? 28f : -40f;
        }

        float score = weaponWeightsProfile != null
            ? weaponWeightsProfile.GetMoveWeight(kind, code)
            : CombatAiWeaponWeightsProfile.GetDefaultMoveWeight(kind, code);
        return code == CombatAiMoveCode.TakeHighGround && weapon != null
            ? score + weapon.SeekHighGroundBias * 0.4f
            : score;
    }

    private static float GetPersonalityScore(CombatAiPersonalityProfile personalityProfile, string code, CombatObjective objective)
    {
        if (personalityProfile == null) return 0f;
        float score = code switch
        {
            CombatAiMoveCode.PursueEnemy => personalityProfile.Aggression * 14f - personalityProfile.Caution * 4f,
            CombatAiMoveCode.AdvanceEnemyStone => personalityProfile.ObjectiveFocus * 16f + personalityProfile.RiskTolerance * 6f,
            CombatAiMoveCode.AdvanceViaBridge => personalityProfile.ObjectiveFocus * 14f + personalityProfile.Caution * 6f,
            CombatAiMoveCode.ReturnOwnStone => personalityProfile.Caution * 10f,
            CombatAiMoveCode.SupportAlly => personalityProfile.SupportBias * 14f,
            CombatAiMoveCode.InterceptThreat => personalityProfile.SupportBias * 12f + personalityProfile.Caution * 4f,
            CombatAiMoveCode.TakeHighGround => personalityProfile.PreferredRangeBias * 10f,
            CombatAiMoveCode.MoveForest => personalityProfile.Caution * 8f,
            CombatAiMoveCode.SearchLastKnown => personalityProfile.ExplorationBias * 8f + (objective == CombatObjective.Search ? 4f : 0f),
            _ => 0f,
        };
        return score + CombatAiPersonalityBehavior.GetMoveScore(personalityProfile, code);
    }

    private static void AddReasons(string code, CombatAiScoreBreakdown breakdown)
    {
        switch (code)
        {
            case CombatAiMoveCode.AdvanceEnemyStone:
            case CombatAiMoveCode.AdvanceViaBridge:
                AddReason(breakdown, CombatAiReasonCode.EnemyStoneReachable);
                break;
            case CombatAiMoveCode.ReturnOwnStone:
                AddReason(breakdown, CombatAiReasonCode.OwnStoneThreatHigh);
                break;
            case CombatAiMoveCode.PursueEnemy:
                AddReason(breakdown, CombatAiReasonCode.ReachableEnemyHigh);
                break;
            case CombatAiMoveCode.SupportAlly:
                AddReason(breakdown, CombatAiReasonCode.AllyFragilityHigh);
                break;
            case CombatAiMoveCode.InterceptThreat:
                AddReason(breakdown, CombatAiReasonCode.BodyBlockValuable);
                break;
            case CombatAiMoveCode.TakeHighGround:
                AddReason(breakdown, CombatAiReasonCode.HighGroundAvailable);
                break;
            case CombatAiMoveCode.MoveForest:
                AddReason(breakdown, CombatAiReasonCode.ForestAvailable);
                break;
            case CombatAiMoveCode.SearchLastKnown:
                AddReason(breakdown, CombatAiReasonCode.EnemyLocationUncertain);
                break;
        }
    }

    private static float GetSearchAdvanceBonus(CombatAiAssessment assessment, CombatObjective objective)
    {
        if (objective != CombatObjective.Search)
        {
            return 0f;
        }

        return (100f - assessment.GetValue(CombatAiMetricIndex.EnemyLocationConfidence)) * 0.3f;
    }

    private static float GetHighGroundUtilityScore(
        Character owner,
        CombatAiContext context,
        CombatAiAssessment assessment,
        string code,
        CombatMoveTarget target)
    {
        if (code != CombatAiMoveCode.TakeHighGround || owner == null || context == null || !target.HasDestination)
        {
            return 0f;
        }

        GetUsefulSkillRanges(owner, out float hostileRange, out float supportRange);
        int currentTargets = CountActionableTargets(context, owner.transform.position, hostileRange, supportRange, false);
        int highGroundTargets = CountActionableTargets(context, target.Destination, hostileRange, supportRange, true);
        int actionableGain = Mathf.Max(0, highGroundTargets - currentTargets);
        int intelGain = CountNewIntelTargets(context, target.Destination);
        float sightGain = GetSightAreaGain(owner, target.Destination);
        float uncertainty = 1f - Mathf.Clamp01(assessment.GetValue(CombatAiMetricIndex.EnemyLocationConfidence) / 100f);
        return Mathf.Min(54f, actionableGain * 18f + intelGain * 10f + sightGain * uncertainty * 36f);
    }

    private static float GetSightAreaGain(Character owner, Vector3 destination)
    {
        CombatVision vision = owner.Vision;
        if (vision == null) return 0f;

        float currentRange = vision.CurrentSightRange;
        float destinationRange = vision.GetSightRangeAt(destination);
        if (destinationRange <= currentRange || destinationRange <= Mathf.Epsilon) return 0f;

        return 1f - currentRange * currentRange / (destinationRange * destinationRange);
    }

    private static void GetUsefulSkillRanges(Character owner, out float hostileRange, out float supportRange)
    {
        hostileRange = owner.EquippedWeapon != null ? owner.EquippedWeapon.Range : 0f;
        supportRange = 0f;
        for (int i = 0; i < owner.AvailableCombatSkills.Count; i++)
        {
            SkillBase skill = owner.AvailableCombatSkills[i];
            if (skill == null || float.IsInfinity(skill.MaxRange)) continue;
            if (CombatAiSkillClassifier.IsDamage(skill) || CombatAiSkillClassifier.IsDebuff(skill))
            {
                hostileRange = Mathf.Max(hostileRange, skill.MaxRange);
            }
            if (CombatAiSkillClassifier.IsSupport(skill)) supportRange = Mathf.Max(supportRange, skill.MaxRange);
        }
    }

    private static int CountActionableTargets(
        CombatAiContext context,
        Vector3 position,
        float hostileRange,
        float supportRange,
        bool fromHighGround)
    {
        int count = 0;
        if (hostileRange > 0f)
        {
            for (int i = 0; i < context.EnemyIntel.Count; i++)
            {
                CombatCharacterIntel enemy = context.EnemyIntel[i];
                if (!enemy.IsAlive || !enemy.HasKnownPosition) continue;
                if (!fromHighGround && !enemy.HasDirectSight) continue;
                if (HorizontalDistance(position, enemy.KnownPosition) > hostileRange) continue;
                if (HasProjectedLineOfSight(position, enemy.KnownPosition, enemy.Character)) count++;
            }
        }

        if (supportRange <= 0f) return count;
        for (int i = 0; i < context.AllyIntel.Count; i++)
        {
            CombatCharacterIntel ally = context.AllyIntel[i];
            if (!ally.IsAlive || HorizontalDistance(position, ally.CurrentPosition) > supportRange) continue;
            if (HasProjectedLineOfSight(position, ally.CurrentPosition, ally.Character)) count++;
        }

        return count;
    }

    private static int CountNewIntelTargets(CombatAiContext context, Vector3 highGroundPosition)
    {
        int count = 0;
        for (int i = 0; i < context.EnemyIntel.Count; i++)
        {
            CombatCharacterIntel enemy = context.EnemyIntel[i];
            if (!enemy.IsAlive || !enemy.HasKnownPosition || enemy.HasDirectSight) continue;
            if (HorizontalDistance(highGroundPosition, enemy.KnownPosition) > 100f) continue;
            if (HasProjectedLineOfSight(highGroundPosition, enemy.KnownPosition, enemy.Character)) count++;
        }

        return count;
    }

    private static bool HasProjectedLineOfSight(Vector3 from, Vector3 to, Character target)
    {
        from += Vector3.up;
        to += Vector3.up;
        if (!Physics.Linecast(from, to, out RaycastHit hit, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            return true;
        }

        return hit.collider != null && hit.collider.GetComponentInParent<Character>() == target;
    }

    private static float GetObjectiveAlignmentScore(string code, CombatObjective objective)
    {
        return objective switch
        {
            CombatObjective.DestroyEnemyStone when code == CombatAiMoveCode.AdvanceEnemyStone => 42f,
            CombatObjective.DestroyEnemyStone when code == CombatAiMoveCode.AdvanceViaBridge => 42f,
            CombatObjective.DefendOwnStone when code == CombatAiMoveCode.ReturnOwnStone => 42f,
            CombatObjective.AttackEnemy when code == CombatAiMoveCode.PursueEnemy => 40f,
            CombatObjective.SupportAlly when code == CombatAiMoveCode.SupportAlly => 42f,
            CombatObjective.SupportAlly when code == CombatAiMoveCode.InterceptThreat => 38f,
            CombatObjective.DefendOwnStone when code == CombatAiMoveCode.InterceptThreat => 38f,
            CombatObjective.Search when code == CombatAiMoveCode.SearchLastKnown => 34f,
            CombatObjective.Retreat when code == CombatAiMoveCode.ReturnOwnStone || code == CombatAiMoveCode.MoveForest => 24f,
            _ => 0f,
        };
    }

    private static void AddReason(CombatAiScoreBreakdown breakdown, CombatAiReasonCode reason)
    {
        if (!breakdown.ReasonCodes.Contains(reason))
        {
            breakdown.ReasonCodes.Add(reason);
        }
    }

    private static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }
}
