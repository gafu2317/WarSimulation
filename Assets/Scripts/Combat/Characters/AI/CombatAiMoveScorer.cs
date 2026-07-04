using UnityEngine;

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
        float distance = target.HasDestination
            ? HorizontalDistance(snapshot.Owner.transform.position, target.Destination)
            : 0f;
        var breakdown = new CombatAiScoreBreakdown
        {
            BaseScore = target.HasDestination ? Mathf.Lerp(24f, 4f, Mathf.Clamp01(distance / 40f)) : 8f,
            SituationScore = GetSituationScore(snapshot.Assessment, code, objective)
                + CombatAiFocusTargeting.GetMoveScore(
                    snapshot.Context,
                    snapshot.Owner.EquippedWeapon,
                    code,
                    target,
                    focusEnemy,
                    focusCommitmentRemainingSeconds),
            WeaponScore = GetWeaponScore(weaponWeightsProfile, snapshot.Owner.EquippedWeapon, code),
            PersonalityScore = GetPersonalityScore(personalityProfile, code, objective),
        };
        AddReasons(code, breakdown);
        if (breakdown.WeaponScore != 0f) AddReason(breakdown, CombatAiReasonCode.WeaponPreference);
        if (breakdown.PersonalityScore != 0f) AddReason(breakdown, CombatAiReasonCode.PersonalityPreference);
        return breakdown;
    }

    private static float GetSituationScore(CombatAiAssessment assessment, string code, CombatObjective objective)
    {
        return code switch
        {
            CombatAiMoveCode.AdvanceEnemyStone => assessment.GetValue("EnemyStoneReachability") * 0.6f
                - assessment.GetValue("OwnStoneThreat") * 0.2f
                + GetSearchAdvanceBonus(assessment, objective),
            CombatAiMoveCode.ReturnOwnStone => assessment.GetValue("OwnStoneThreat") * 0.65f + assessment.GetValue("RetreatRouteSafety") * 0.2f,
            CombatAiMoveCode.PursueEnemy => assessment.GetValue("ReachableEnemyValue") * 0.65f + assessment.GetValue("EnemyLocationConfidence") * 0.1f,
            CombatAiMoveCode.SupportAlly => assessment.GetValue("AllyFragility") * 0.65f,
            CombatAiMoveCode.TakeHighGround => GetHighGroundSituationScore(assessment, objective),
            CombatAiMoveCode.MoveForest => assessment.GetValue("RetreatRouteSafety") * 0.3f + assessment.GetValue("TerrainAdvantage") * 0.3f,
            CombatAiMoveCode.SearchLastKnown => (100f - assessment.GetValue("EnemyLocationConfidence")) * 0.45f,
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
        return weaponWeightsProfile != null
            ? weaponWeightsProfile.GetMoveWeight(kind, code)
            : CombatAiWeaponWeightsProfile.GetDefaultMoveWeight(kind, code);
    }

    private static float GetPersonalityScore(CombatAiPersonalityProfile personalityProfile, string code, CombatObjective objective)
    {
        if (personalityProfile == null) return 0f;
        return code switch
        {
            CombatAiMoveCode.PursueEnemy => personalityProfile.Aggression * 14f - personalityProfile.Caution * 4f,
            CombatAiMoveCode.AdvanceEnemyStone => personalityProfile.ObjectiveFocus * 16f + personalityProfile.RiskTolerance * 6f,
            CombatAiMoveCode.ReturnOwnStone => personalityProfile.Caution * 10f,
            CombatAiMoveCode.SupportAlly => personalityProfile.SupportBias * 14f,
            CombatAiMoveCode.TakeHighGround => personalityProfile.PreferredRangeBias * 10f,
            CombatAiMoveCode.MoveForest => personalityProfile.Caution * 8f,
            CombatAiMoveCode.SearchLastKnown => personalityProfile.ExplorationBias * 8f + (objective == CombatObjective.Search ? 4f : 0f),
            _ => 0f,
        };
    }

    private static void AddReasons(string code, CombatAiScoreBreakdown breakdown)
    {
        switch (code)
        {
            case CombatAiMoveCode.AdvanceEnemyStone:
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

        return (100f - assessment.GetValue("EnemyLocationConfidence")) * 0.3f;
    }

    private static float GetHighGroundSituationScore(CombatAiAssessment assessment, CombatObjective objective)
    {
        float terrainAdvantage = assessment.GetValue("TerrainAdvantage");
        if (objective != CombatObjective.Search)
        {
            return terrainAdvantage * 0.8f;
        }

        float confidence = assessment.GetValue("EnemyLocationConfidence");
        if (confidence <= 0f)
        {
            return terrainAdvantage * 0.15f;
        }

        return terrainAdvantage * 0.3f + confidence * 0.15f;
    }

    private static float GetObjectiveAlignmentScore(string code, CombatObjective objective)
    {
        return objective switch
        {
            CombatObjective.DestroyEnemyStone when code == CombatAiMoveCode.AdvanceEnemyStone => 42f,
            CombatObjective.DefendOwnStone when code == CombatAiMoveCode.ReturnOwnStone => 42f,
            CombatObjective.AttackEnemy when code == CombatAiMoveCode.PursueEnemy => 40f,
            CombatObjective.SupportAlly when code == CombatAiMoveCode.SupportAlly => 42f,
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
