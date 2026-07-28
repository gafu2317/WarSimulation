using UnityEngine;
using UnityEngine.AI;

public static class CombatAiMoveScorer
{
    public static bool IsReachable(Character owner, Vector3 destination)
    {
        if (owner == null) return false;

        NavMeshAgent agent = owner.GetComponent<NavMeshAgent>();
        if (agent == null || !agent.isOnNavMesh) return true;

        CombatCharacterBody body = owner.GetComponent<CombatCharacterBody>();
        if (body != null)
        {
            return body.CanReachDestination(destination);
        }

        var path = new NavMeshPath();
        return agent.CalculatePath(destination, path) &&
            path.status == NavMeshPathStatus.PathComplete &&
            path.corners.Length >= 2;
    }

    public static bool IsReachableVia(
        Character owner,
        Vector3 waypoint,
        Vector3 destination)
    {
        if (!IsReachable(owner, waypoint)) return false;
        return IsReachable(owner, destination);
    }

    public static float Score(
        Character owner,
        CombatAiContext context,
        CombatAiAssessment assessment,
        CombatAiPersonalityProfile personalityProfile,
        string code,
        CombatMoveTarget target,
        CombatObjective objective,
        Character focusEnemy,
        float focusCommitmentRemainingSeconds,
        CombatAiScoreBreakdown breakdown = null)
    {
        float distance = GetTravelDistance(context, code, target);
        WeaponKind weaponKind = owner.EquippedWeapon != null ? owner.EquippedWeapon.Kind : WeaponKind.Unarmed;
        float baseScore = target.HasDestination ? Mathf.Lerp(24f, 4f, Mathf.Clamp01(distance / 40f)) : 8f;
        float coverBonus = GetCoverBonus(weaponKind, assessment, code);
        float routeRiskPenalty = GetRouteRiskPenalty(context, personalityProfile, code, target);
        float assaultRouteCongestionPenalty = GetAssaultRouteCongestionPenalty(context, code, target);
        float situationScore = GetSituationScore(assessment, code, objective)
            + GetHighGroundUtilityScore(owner, context, assessment, code, target)
            + CombatAiFocusTargeting.GetMoveScore(context, owner.EquippedWeapon, code, target, focusEnemy, focusCommitmentRemainingSeconds)
            + coverBonus
            - GetAllyDestinationOverlapPenalty(context, code, target)
            - assaultRouteCongestionPenalty
            - GetSwordIsolationPenalty(context, assessment, weaponKind, code, target)
            - routeRiskPenalty;
        float weaponScore = GetWeaponScore(owner.EquippedWeapon, code);
        float personalityScore = CombatAiPersonalityBehavior.GetMoveScore(personalityProfile, code, objective);

        if (breakdown != null)
        {
            breakdown.BaseScore = baseScore;
            breakdown.SituationScore = situationScore;
            breakdown.WeaponScore = weaponScore;
            breakdown.PersonalityScore = personalityScore;
            AddReasons(code, breakdown);
            if (weaponScore != 0f) AddReason(breakdown, CombatAiReasonCode.WeaponPreference);
            if (personalityScore != 0f) AddReason(breakdown, CombatAiReasonCode.PersonalityPreference);
            if (coverBonus > 0f) AddReason(breakdown, CombatAiReasonCode.SelfExposedByEnemy);
            if (routeRiskPenalty >= 12f) AddReason(breakdown, CombatAiReasonCode.RouteRiskHigh);
            if (assaultRouteCongestionPenalty > 0f) AddReason(breakdown, CombatAiReasonCode.AssaultRouteCongested);
        }

        return baseScore + situationScore + weaponScore + personalityScore;
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

    private static float GetAssaultRouteCongestionPenalty(
        CombatAiContext context,
        string code,
        CombatMoveTarget target)
    {
        if (context == null ||
            context.Owner == null ||
            !target.HasAssaultRouteKey ||
            (code != CombatAiMoveCode.AdvanceViaBridge && code != CombatAiMoveCode.AdvanceEnemyStone))
        {
            return 0f;
        }

        if (!TryFindAssaultRoute(context, target.AssaultRouteKey, out CombatAiAssaultRoute route))
        {
            return 0f;
        }

        const float nearThreshold = 4f;
        int congestingAllies = 0;
        for (int i = 0; i < context.AllyIntel.Count; i++)
        {
            CombatCharacterIntel ally = context.AllyIntel[i];
            if (ally.Character == context.Owner || !ally.CanAct) continue;
            if (IsAllyUsingAssaultRoute(context, ally, route, nearThreshold))
            {
                congestingAllies++;
            }
        }

        return Mathf.Min(36f, congestingAllies * 12f);
    }

    private static bool TryFindAssaultRoute(
        CombatAiContext context,
        int routeKey,
        out CombatAiAssaultRoute route)
    {
        for (int i = 0; i < context.AssaultRoutes.Count; i++)
        {
            CombatAiAssaultRoute candidate = context.AssaultRoutes[i];
            if (candidate.BridgeFeatureIndex != routeKey) continue;
            route = candidate;
            return true;
        }

        route = default;
        return false;
    }

    private static bool IsAllyUsingAssaultRoute(
        CombatAiContext context,
        CombatCharacterIntel ally,
        CombatAiAssaultRoute route,
        float nearThreshold)
    {
        if (ally.HasIntendedDestination)
        {
            if (IsNearAssaultRouteAnchor(context, ally.IntendedDestination, route, nearThreshold))
            {
                return true;
            }
        }

        if (!CombatAiPositioning.IsAdvancingAlly(context, ally)) return false;
        return IsNearAssaultRouteCorridor(ally.CurrentPosition, route, nearThreshold);
    }

    private static bool IsNearAssaultRouteAnchor(
        CombatAiContext context,
        Vector3 position,
        CombatAiAssaultRoute route,
        float nearThreshold)
    {
        if (route.HasBridgeWaypoints)
        {
            if (HorizontalDistance(position, route.EnterWorld) <= nearThreshold) return true;
            if (HorizontalDistance(position, route.ExitWorld) <= nearThreshold) return true;
            return context.HasEnemyStonePosition &&
                HorizontalDistance(position, context.EnemyStonePosition) <= nearThreshold + 2.5f &&
                HorizontalDistance(position, route.ExitWorld) <= nearThreshold + 6f;
        }

        return context.HasEnemyStonePosition &&
            HorizontalDistance(position, context.EnemyStonePosition) <= nearThreshold + 2.5f;
    }

    private static bool IsNearAssaultRouteCorridor(
        Vector3 position,
        CombatAiAssaultRoute route,
        float nearThreshold)
    {
        if (!route.HasBridgeWaypoints)
        {
            return false;
        }

        return DistanceToSegmentHorizontal(position, route.EnterWorld, route.ExitWorld) <= nearThreshold;
    }

    private static float DistanceToSegmentHorizontal(Vector3 point, Vector3 a, Vector3 b)
    {
        Vector2 p = new Vector2(point.x, point.z);
        Vector2 start = new Vector2(a.x, a.z);
        Vector2 end = new Vector2(b.x, b.z);
        Vector2 ab = end - start;
        float lengthSquared = ab.sqrMagnitude;
        if (lengthSquared <= 0.0001f) return Vector2.Distance(p, start);

        float t = Mathf.Clamp01(Vector2.Dot(p - start, ab) / lengthSquared);
        Vector2 closest = start + ab * t;
        return Vector2.Distance(p, closest);
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

    private static float GetWeaponScore(WeaponBase weapon, string code)
    {
        WeaponKind kind = weapon != null ? weapon.Kind : WeaponKind.Unarmed;
        float score = CombatAiWeaponWeights.GetMoveWeight(kind, code);
        return code == CombatAiMoveCode.TakeHighGround && weapon != null
            ? score + weapon.SeekHighGroundBias * 0.4f
            : score;
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
            CombatObjective.Search when code == CombatAiMoveCode.TakeHighGround => 34f,
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
