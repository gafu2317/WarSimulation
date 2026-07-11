public static class CombatAiObjectiveScorer
{
    private static readonly CombatObjective[] AllObjectives = (CombatObjective[])System.Enum.GetValues(typeof(CombatObjective));

    public static void BuildEntries(
        CombatAiDebugSnapshot snapshot,
        CombatAiPersonalityProfile personalityProfile,
        CombatAiWeaponWeightsProfile weaponWeightsProfile,
        Character focusEnemy,
        float focusCommitmentRemainingSeconds,
        CombatObjective previousObjective)
    {
        snapshot.ObjectiveEntries.Clear();
        for (int i = 0; i < AllObjectives.Length; i++)
        {
            CombatObjective objective = AllObjectives[i];
            var breakdown = new CombatAiScoreBreakdown
            {
                BaseScore = GetBaseScore(objective),
                SituationScore = GetSituationScore(
                    snapshot.Context,
                    snapshot.Assessment,
                    snapshot.Owner.EquippedWeapon,
                    objective)
                    + CombatAiFocusTargeting.GetObjectiveScore(
                        snapshot.Context,
                        snapshot.Owner.EquippedWeapon,
                        objective,
                        focusEnemy,
                        focusCommitmentRemainingSeconds,
                        previousObjective),
                WeaponScore = GetWeaponScore(weaponWeightsProfile, snapshot.Owner.EquippedWeapon, objective),
                PersonalityScore = GetPersonalityScore(personalityProfile, objective),
            };

            AddReasons(snapshot.Assessment, objective, breakdown);
            if (breakdown.WeaponScore != 0f) AddReason(breakdown, CombatAiReasonCode.WeaponPreference);
            if (breakdown.PersonalityScore != 0f) AddReason(breakdown, CombatAiReasonCode.PersonalityPreference);

            snapshot.ObjectiveEntries.Add(new CombatAiObjectiveScoreEntry
            {
                Objective = objective,
                Label = CombatAiDebugLabels.Objective(objective),
                Breakdown = breakdown,
            });
        }
    }

    public static CombatObjective SelectBestObjective(
        CombatAiContext context,
        CombatAiAssessment assessment,
        CombatAiPersonalityProfile personalityProfile,
        CombatAiWeaponWeightsProfile weaponWeightsProfile,
        Character focusEnemy,
        float focusCommitmentRemainingSeconds,
        CombatObjective previousObjective)
    {
        WeaponBase weapon = context.Owner != null ? context.Owner.EquippedWeapon : null;
        CombatObjective best = default;
        float bestScore = float.NegativeInfinity;
        for (int i = 0; i < AllObjectives.Length; i++)
        {
            CombatObjective objective = AllObjectives[i];
            float score = GetBaseScore(objective)
                + GetSituationScore(context, assessment, weapon, objective)
                + CombatAiFocusTargeting.GetObjectiveScore(context, weapon, objective, focusEnemy, focusCommitmentRemainingSeconds, previousObjective)
                + GetWeaponScore(weaponWeightsProfile, weapon, objective)
                + GetPersonalityScore(personalityProfile, objective);
            if (score > bestScore)
            {
                bestScore = score;
                best = objective;
            }
        }

        return best;
    }

    private static float GetBaseScore(CombatObjective objective)
    {
        return objective switch
        {
            CombatObjective.AttackEnemy => 28f,
            CombatObjective.DefendOwnStone => 22f,
            CombatObjective.SupportAlly => 18f,
            CombatObjective.DestroyEnemyStone => 20f,
            CombatObjective.Search => 12f,
            CombatObjective.Retreat => 10f,
            _ => 10f,
        };
    }

    private static float GetSituationScore(
        CombatAiContext context,
        CombatAiAssessment assessment,
        WeaponBase weapon,
        CombatObjective objective)
    {
        float score = objective switch
        {
            CombatObjective.AttackEnemy => assessment.GetValue(CombatAiMetricIndex.ReachableEnemyValue) * 0.9f
                + assessment.GetValue(CombatAiMetricIndex.EnemyThreatLevel) * 0.25f
                + assessment.GetValue(CombatAiMetricIndex.KillableTargetValue) * 0.35f
                + assessment.GetValue(CombatAiMetricIndex.TerrainAdvantage) * 0.15f
                - assessment.GetValue(CombatAiMetricIndex.SelfThreat) * 0.35f,
            CombatObjective.DefendOwnStone => assessment.GetValue(CombatAiMetricIndex.OwnStoneThreat) * 0.95f
                + assessment.GetValue(CombatAiMetricIndex.AllyFragility) * 0.2f
                + assessment.GetValue(CombatAiMetricIndex.EnemyLocationConfidence) * 0.1f,
            CombatObjective.SupportAlly => assessment.GetValue(CombatAiMetricIndex.AllyFragility) * 0.95f
                + assessment.GetValue(CombatAiMetricIndex.TerrainAdvantage) * 0.1f,
            CombatObjective.DestroyEnemyStone => assessment.GetValue(CombatAiMetricIndex.EnemyStoneReachability) * 0.85f
                - assessment.GetValue(CombatAiMetricIndex.OwnStoneThreat) * 0.35f
                - assessment.GetValue(CombatAiMetricIndex.SelfThreat) * 0.2f
                - assessment.GetValue(CombatAiMetricIndex.EnemyThreatLevel) * 0.28f
                + (context.HasEnemyStonePosition ? 4f : 0f)
                + UnityEngine.Mathf.Max(0f, 8f - assessment.GetValue(CombatAiMetricIndex.AllyFragility) * 0.1f),
            CombatObjective.Search => (100f - assessment.GetValue(CombatAiMetricIndex.EnemyLocationConfidence)) * 0.55f
                + assessment.GetValue(CombatAiMetricIndex.TerrainAdvantage) * 0.2f
                - (context.HasEnemyStonePosition ? 14f : 0f)
                + (context.VisibleEnemies.Count == 0 && context.HighGroundCandidates.Count > 0 ? 14f : 0f),
            CombatObjective.Retreat => assessment.GetValue(CombatAiMetricIndex.SelfThreat) * 0.9f
                + assessment.GetValue(CombatAiMetricIndex.RetreatRouteSafety) * 0.3f
                + assessment.GetValue(CombatAiMetricIndex.AllyFragility) * 0.1f,
            _ => 0f,
        };

        return score + GetWeaponSituationAdjustment(context, assessment, weapon, objective);
    }

    private static float GetWeaponSituationAdjustment(
        CombatAiContext context,
        CombatAiAssessment assessment,
        WeaponBase weapon,
        CombatObjective objective)
    {
        WeaponKind kind = weapon != null ? weapon.Kind : WeaponKind.Unarmed;
        return kind switch
        {
            WeaponKind.Sword => GetSwordSituationAdjustment(context, assessment, objective),
            WeaponKind.Shield => GetShieldSituationAdjustment(context, assessment, objective),
            WeaponKind.Wand => GetWandSituationAdjustment(context, assessment, objective),
            WeaponKind.Grimoire => GetGrimoireSituationAdjustment(context, assessment, objective),
            WeaponKind.Bible => GetBibleSituationAdjustment(context, assessment, objective),
            WeaponKind.Rosary => GetRosarySituationAdjustment(context, assessment, objective),
            _ => 0f,
        };
    }

    private static float GetSwordSituationAdjustment(
        CombatAiContext context,
        CombatAiAssessment assessment,
        CombatObjective objective)
    {
        return objective switch
        {
            CombatObjective.AttackEnemy when assessment.GetValue(CombatAiMetricIndex.ReachableEnemyValue) > 30f
                && assessment.GetValue(CombatAiMetricIndex.SelfThreat) < 35f => 16f,
            CombatObjective.DestroyEnemyStone when context.HasEnemyStonePosition
                && assessment.GetValue(CombatAiMetricIndex.EnemyStoneReachability) > 28f
                && assessment.GetValue(CombatAiMetricIndex.OwnStoneThreat) < 24f => 14f,
            CombatObjective.DefendOwnStone when assessment.GetValue(CombatAiMetricIndex.OwnStoneThreat) > 28f => 12f,
            _ => 0f,
        };
    }

    private static float GetShieldSituationAdjustment(
        CombatAiContext context,
        CombatAiAssessment assessment,
        CombatObjective objective)
    {
        return objective switch
        {
            CombatObjective.DefendOwnStone when context.HasOwnStonePosition
                && (assessment.GetValue(CombatAiMetricIndex.OwnStoneThreat) > 18f || assessment.GetValue(CombatAiMetricIndex.AllyFragility) > 22f) => 18f,
            CombatObjective.AttackEnemy when assessment.GetValue(CombatAiMetricIndex.OwnStoneThreat) < 16f
                && assessment.GetValue(CombatAiMetricIndex.ReachableEnemyValue) > 24f => 10f,
            CombatObjective.DestroyEnemyStone when context.HasEnemyStonePosition
                && assessment.GetValue(CombatAiMetricIndex.EnemyStoneReachability) > 30f
                && assessment.GetValue(CombatAiMetricIndex.OwnStoneThreat) < 12f
                && assessment.GetValue(CombatAiMetricIndex.AllyFragility) < 18f => 8f,
            _ => 0f,
        };
    }

    private static float GetWandSituationAdjustment(
        CombatAiContext context,
        CombatAiAssessment assessment,
        CombatObjective objective)
    {
        bool lacksReliableShot = assessment.GetValue(CombatAiMetricIndex.ReachableEnemyValue) < 24f;
        return objective switch
        {
            CombatObjective.Search when assessment.GetValue(CombatAiMetricIndex.EnemyLocationConfidence) < 45f
                || (context.VisibleEnemies.Count == 0 && context.HighGroundCandidates.Count > 0)
                || lacksReliableShot => 16f,
            CombatObjective.AttackEnemy when assessment.GetValue(CombatAiMetricIndex.ReachableEnemyValue) > 28f
                && assessment.GetValue(CombatAiMetricIndex.EnemyLocationConfidence) > 35f => 14f,
            CombatObjective.DestroyEnemyStone when context.HasEnemyStonePosition
                && assessment.GetValue(CombatAiMetricIndex.EnemyStoneReachability) > 34f
                && lacksReliableShot => 4f,
            _ => 0f,
        };
    }

    private static float GetGrimoireSituationAdjustment(
        CombatAiContext context,
        CombatAiAssessment assessment,
        CombatObjective objective)
    {
        bool multipleEnemiesVisible = context.VisibleEnemies.Count >= 2;
        return objective switch
        {
            CombatObjective.AttackEnemy when multipleEnemiesVisible
                || assessment.GetValue(CombatAiMetricIndex.ReachableEnemyValue) > 28f => 16f,
            CombatObjective.DestroyEnemyStone when context.HasEnemyStonePosition
                && assessment.GetValue(CombatAiMetricIndex.EnemyStoneReachability) > 30f
                && assessment.GetValue(CombatAiMetricIndex.OwnStoneThreat) < 18f => 10f,
            CombatObjective.Search when assessment.GetValue(CombatAiMetricIndex.EnemyLocationConfidence) < 35f
                && assessment.GetValue(CombatAiMetricIndex.ReachableEnemyValue) < 18f => 10f,
            _ => 0f,
        };
    }

    private static float GetBibleSituationAdjustment(
        CombatAiContext context,
        CombatAiAssessment assessment,
        CombatObjective objective)
    {
        bool stableFrontline = assessment.GetValue(CombatAiMetricIndex.AllyFragility) < 18f && assessment.GetValue(CombatAiMetricIndex.OwnStoneThreat) < 16f;
        return objective switch
        {
            CombatObjective.SupportAlly when context.AllyIntel.Count > 0
                && assessment.GetValue(CombatAiMetricIndex.AllyFragility) > 12f => 18f,
            CombatObjective.DefendOwnStone when context.HasOwnStonePosition
                && (assessment.GetValue(CombatAiMetricIndex.OwnStoneThreat) > 18f || assessment.GetValue(CombatAiMetricIndex.AllyFragility) > 20f) => 14f,
            CombatObjective.AttackEnemy when stableFrontline
                && assessment.GetValue(CombatAiMetricIndex.ReachableEnemyValue) > 26f => 6f,
            CombatObjective.DestroyEnemyStone when stableFrontline
                && context.HasEnemyStonePosition
                && assessment.GetValue(CombatAiMetricIndex.EnemyStoneReachability) > 30f => 6f,
            _ => 0f,
        };
    }

    private static float GetRosarySituationAdjustment(
        CombatAiContext context,
        CombatAiAssessment assessment,
        CombatObjective objective)
    {
        bool stableLine = assessment.GetValue(CombatAiMetricIndex.AllyFragility) < 16f && assessment.GetValue(CombatAiMetricIndex.SelfThreat) < 20f;
        return objective switch
        {
            CombatObjective.SupportAlly when context.AllyIntel.Count > 0
                && assessment.GetValue(CombatAiMetricIndex.AllyFragility) > 10f => 20f,
            CombatObjective.Retreat when assessment.GetValue(CombatAiMetricIndex.SelfThreat) > 18f
                || assessment.GetValue(CombatAiMetricIndex.AllyFragility) > 28f => 16f,
            CombatObjective.DefendOwnStone when stableLine
                && assessment.GetValue(CombatAiMetricIndex.OwnStoneThreat) > 18f => 8f,
            CombatObjective.Search when stableLine
                && assessment.GetValue(CombatAiMetricIndex.EnemyLocationConfidence) < 35f => 6f,
            _ => 0f,
        };
    }

    private static float GetWeaponScore(
        CombatAiWeaponWeightsProfile weaponWeightsProfile,
        WeaponBase weapon,
        CombatObjective objective)
    {
        WeaponKind kind = weapon != null ? weapon.Kind : WeaponKind.Unarmed;
        return weaponWeightsProfile != null
            ? weaponWeightsProfile.GetObjectiveWeight(kind, objective)
            : CombatAiWeaponWeightsProfile.GetDefaultObjectiveWeight(kind, objective);
    }

    private static float GetPersonalityScore(CombatAiPersonalityProfile personalityProfile, CombatObjective objective)
    {
        if (personalityProfile == null) return 0f;

        return objective switch
        {
            CombatObjective.AttackEnemy => personalityProfile.Aggression * 20f - personalityProfile.Caution * 8f,
            CombatObjective.DefendOwnStone => personalityProfile.ObjectiveFocus * 16f + personalityProfile.Caution * 6f,
            CombatObjective.SupportAlly => personalityProfile.SupportBias * 20f + personalityProfile.Caution * 4f,
            CombatObjective.DestroyEnemyStone => personalityProfile.ObjectiveFocus * 18f + personalityProfile.RiskTolerance * 8f,
            CombatObjective.Search => personalityProfile.ExplorationBias * 18f,
            CombatObjective.Retreat => personalityProfile.Caution * 18f - personalityProfile.RiskTolerance * 6f,
            _ => 0f,
        };
    }

    private static void AddReasons(CombatAiAssessment assessment, CombatObjective objective, CombatAiScoreBreakdown breakdown)
    {
        switch (objective)
        {
            case CombatObjective.AttackEnemy:
                if (assessment.GetValue(CombatAiMetricIndex.ReachableEnemyValue) > 35f) AddReason(breakdown, CombatAiReasonCode.ReachableEnemyHigh);
                if (assessment.GetValue(CombatAiMetricIndex.EnemyThreatLevel) > 45f) AddReason(breakdown, CombatAiReasonCode.EnemyThreatHigh);
                if (assessment.GetValue(CombatAiMetricIndex.KillableTargetValue) > 35f) AddReason(breakdown, CombatAiReasonCode.KillableTargetHigh);
                if (assessment.GetValue(CombatAiMetricIndex.TerrainAdvantage) > 20f) AddReason(breakdown, CombatAiReasonCode.TerrainAdvantageHigh);
                break;
            case CombatObjective.DefendOwnStone:
                if (assessment.GetValue(CombatAiMetricIndex.OwnStoneThreat) > 25f) AddReason(breakdown, CombatAiReasonCode.OwnStoneThreatHigh);
                break;
            case CombatObjective.SupportAlly:
                if (assessment.GetValue(CombatAiMetricIndex.AllyFragility) > 25f) AddReason(breakdown, CombatAiReasonCode.AllyFragilityHigh);
                break;
            case CombatObjective.DestroyEnemyStone:
                if (assessment.GetValue(CombatAiMetricIndex.EnemyStoneReachability) > 25f) AddReason(breakdown, CombatAiReasonCode.EnemyStoneReachable);
                if (assessment.GetValue(CombatAiMetricIndex.EnemyThreatLevel) > 45f) AddReason(breakdown, CombatAiReasonCode.EnemyThreatHigh);
                break;
            case CombatObjective.Search:
                if (assessment.GetValue(CombatAiMetricIndex.EnemyLocationConfidence) < 30f) AddReason(breakdown, CombatAiReasonCode.EnemyLocationUncertain);
                break;
            case CombatObjective.Retreat:
                if (assessment.GetValue(CombatAiMetricIndex.SelfThreat) > 30f) AddReason(breakdown, CombatAiReasonCode.SelfThreatHigh);
                if (assessment.GetValue(CombatAiMetricIndex.RetreatRouteSafety) > 20f) AddReason(breakdown, CombatAiReasonCode.RetreatRouteSafe);
                break;
        }
    }

    private static void AddReason(CombatAiScoreBreakdown breakdown, CombatAiReasonCode reason)
    {
        if (!breakdown.ReasonCodes.Contains(reason))
        {
            breakdown.ReasonCodes.Add(reason);
        }
    }
}
