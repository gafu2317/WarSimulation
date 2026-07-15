using UnityEngine;

public static class CombatAiObjectiveScorer
{
    private const float NumericalAdvantagePerCharacter = 6f;
    private const float MaximumNumericalAdvantageScore = 24f;
    private static readonly CombatObjective[] AllObjectives = (CombatObjective[])System.Enum.GetValues(typeof(CombatObjective));

    public static void BuildEntries(
        CombatAiDebugSnapshot snapshot,
        CombatAiPersonalityProfile personalityProfile,
        Character focusEnemy,
        float focusCommitmentRemainingSeconds,
        CombatObjective previousObjective)
    {
        snapshot.ObjectiveEntries.Clear();
        CombatObjective selected = EvaluateObjectives(
            snapshot.Context,
            snapshot.Assessment,
            personalityProfile,
            focusEnemy,
            focusCommitmentRemainingSeconds,
            previousObjective,
            snapshot.ObjectiveEntries);
        snapshot.SelectedObjective = FindEntry(snapshot.ObjectiveEntries, selected);
    }

    public static CombatObjective SelectBestObjective(
        CombatAiContext context,
        CombatAiAssessment assessment,
        CombatAiPersonalityProfile personalityProfile,
        Character focusEnemy,
        float focusCommitmentRemainingSeconds,
        CombatObjective previousObjective,
        System.Collections.Generic.List<CombatAiReasonCode> selectedReasons = null)
    {
        return EvaluateObjectives(
            context,
            assessment,
            personalityProfile,
            focusEnemy,
            focusCommitmentRemainingSeconds,
            previousObjective,
            entries: null,
            selectedReasons);
    }

    private static CombatObjective EvaluateObjectives(
        CombatAiContext context,
        CombatAiAssessment assessment,
        CombatAiPersonalityProfile personalityProfile,
        Character focusEnemy,
        float focusCommitmentRemainingSeconds,
        CombatObjective previousObjective,
        System.Collections.Generic.List<CombatAiObjectiveScoreEntry> entries,
        System.Collections.Generic.List<CombatAiReasonCode> selectedReasons = null)
    {
        WeaponBase weapon = context.Owner != null ? context.Owner.EquippedWeapon : null;
        CombatObjective best = CombatObjective.Search;
        float bestScore = float.NegativeInfinity;
        float bestBaseScore = 0f;
        float bestSituationScore = 0f;
        float bestWeaponScore = 0f;
        float bestPersonalityScore = 0f;
        for (int i = 0; i < AllObjectives.Length; i++)
        {
            CombatObjective objective = AllObjectives[i];
            if (!IsObjectiveSelectable(context, objective)) continue;

            float baseScore = GetBaseScore(objective);
            float situationScore = GetSituationScore(context, assessment, weapon, objective)
                + CombatAiFocusTargeting.GetObjectiveScore(
                    context,
                    weapon,
                    objective,
                    focusEnemy,
                    focusCommitmentRemainingSeconds,
                    previousObjective);
            float weaponScore = GetWeaponScore(weapon, objective);
            float personalityScore = GetPersonalityScore(context, personalityProfile, assessment, objective);
            float score = baseScore + situationScore + weaponScore + personalityScore;

            if (entries != null)
            {
                CombatAiScoreBreakdown breakdown = CreateBreakdown(
                    context,
                    assessment,
                    objective,
                    baseScore,
                    situationScore,
                    weaponScore,
                    personalityScore);
                entries.Add(new CombatAiObjectiveScoreEntry
                {
                    Objective = objective,
                    Label = CombatAiDebugLabels.Objective(objective),
                    Breakdown = breakdown,
                });
            }

            if (score > bestScore)
            {
                bestScore = score;
                best = objective;
                bestBaseScore = baseScore;
                bestSituationScore = situationScore;
                bestWeaponScore = weaponScore;
                bestPersonalityScore = personalityScore;
            }
        }

        if (selectedReasons != null)
        {
            selectedReasons.Clear();
            if (!float.IsNegativeInfinity(bestScore))
            {
                CombatAiScoreBreakdown selectedBreakdown = CreateBreakdown(
                    context,
                    assessment,
                    best,
                    bestBaseScore,
                    bestSituationScore,
                    bestWeaponScore,
                    bestPersonalityScore);
                selectedReasons.AddRange(selectedBreakdown.ReasonCodes);
            }
        }

        return best;
    }

    private static CombatAiScoreBreakdown CreateBreakdown(
        CombatAiContext context,
        CombatAiAssessment assessment,
        CombatObjective objective,
        float baseScore,
        float situationScore,
        float weaponScore,
        float personalityScore)
    {
        var breakdown = new CombatAiScoreBreakdown
        {
            BaseScore = baseScore,
            SituationScore = situationScore,
            WeaponScore = weaponScore,
            PersonalityScore = personalityScore,
        };
        AddReasons(assessment, objective, breakdown);
        if (GetNumericalAdvantageAdjustment(context, objective) != 0f)
        {
            AddReason(breakdown, CombatAiReasonCode.NumericalAdvantage);
        }
        if (weaponScore != 0f) AddReason(breakdown, CombatAiReasonCode.WeaponPreference);
        if (personalityScore != 0f) AddReason(breakdown, CombatAiReasonCode.PersonalityPreference);
        return breakdown;
    }

    private static CombatAiObjectiveScoreEntry FindEntry(
        System.Collections.Generic.List<CombatAiObjectiveScoreEntry> entries,
        CombatObjective objective)
    {
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i].Objective == objective) return entries[i];
        }

        return null;
    }

    private static bool IsObjectiveSelectable(CombatAiContext context, CombatObjective objective)
    {
        if (context == null) return false;

        bool hasLivingEnemy = false;
        for (int i = 0; i < context.EnemyIntel.Count; i++)
        {
            if (context.EnemyIntel[i].IsAlive)
            {
                hasLivingEnemy = true;
                break;
            }
        }

        bool hasPossibleEnemy = context.EnemyIntel.Count == 0 || hasLivingEnemy;
        return objective switch
        {
            CombatObjective.DestroyEnemyStone => context.HasEnemyStonePosition &&
                (!context.HasEnemyStoneHealth || context.EnemyStoneHP > 0),
            CombatObjective.AttackEnemy => HasAttackTarget(context),
            CombatObjective.DefendOwnStone => hasPossibleEnemy && context.HasOwnStonePosition,
            CombatObjective.SupportAlly => hasPossibleEnemy && context.AllyIntel.Count > 0,
            CombatObjective.Search => hasPossibleEnemy,
            CombatObjective.Retreat => hasPossibleEnemy,
            _ => false,
        };
    }

    private static bool HasAttackTarget(CombatAiContext context)
    {
        if (context == null) return false;

        for (int i = 0; i < context.EnemyIntel.Count; i++)
        {
            CombatCharacterIntel enemy = context.EnemyIntel[i];
            if (!enemy.IsAlive || !enemy.HasKnownPosition) continue;

            int pendingDamage = 0;
            for (int j = 0; j < context.AllyPendingDamage.Count; j++)
            {
                CombatAiPendingDamage pending = context.AllyPendingDamage[j];
                if (pending.Target == enemy.Character) pendingDamage += pending.Damage;
            }

            if (enemy.HP - pendingDamage > 0) return true;
        }

        return false;
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
                + assessment.GetValue(CombatAiMetricIndex.WinProximity) * 0.45f
                - assessment.GetValue(CombatAiMetricIndex.OwnStoneThreat) * 0.35f
                - assessment.GetValue(CombatAiMetricIndex.SelfThreat) * 0.2f
                - assessment.GetValue(CombatAiMetricIndex.EnemyThreatLevel) * 0.28f
                + (context.HasEnemyStonePosition ? 4f : 0f)
                + UnityEngine.Mathf.Max(0f, 8f - assessment.GetValue(CombatAiMetricIndex.AllyFragility) * 0.1f),
            CombatObjective.Search => (100f - assessment.GetValue(CombatAiMetricIndex.EnemyLocationConfidence)) * 0.55f
                + assessment.GetValue(CombatAiMetricIndex.TerrainAdvantage) * 0.2f
                - (context.HasEnemyStonePosition ? 14f : 0f),
            CombatObjective.Retreat => assessment.GetValue(CombatAiMetricIndex.SelfThreat) * 0.9f
                + assessment.GetValue(CombatAiMetricIndex.RetreatRouteSafety) * 0.3f
                + assessment.GetValue(CombatAiMetricIndex.AllyFragility) * 0.1f,
            _ => 0f,
        };

        if (objective == CombatObjective.DestroyEnemyStone &&
            IsDamageWeapon(weapon) &&
            HasStableAllyFrontline(context))
        {
            score += 14f;
        }

        return score
            + GetNumericalAdvantageAdjustment(context, objective)
            + GetWeaponSituationAdjustment(context, assessment, weapon, objective);
    }

    private static float GetNumericalAdvantageAdjustment(CombatAiContext context, CombatObjective objective)
    {
        int livingAllies = context.Owner != null && context.Owner.Health != null && context.Owner.Health.IsAlive ? 1 : 0;
        for (int i = 0; i < context.AllyIntel.Count; i++)
        {
            if (context.AllyIntel[i].IsAlive) livingAllies++;
        }

        int livingEnemies = 0;
        for (int i = 0; i < context.EnemyIntel.Count; i++)
        {
            if (context.EnemyIntel[i].IsAlive) livingEnemies++;
        }

        float advantage = Mathf.Clamp(
            (livingAllies - livingEnemies) * NumericalAdvantagePerCharacter,
            0f,
            MaximumNumericalAdvantageScore);
        return objective switch
        {
            CombatObjective.AttackEnemy => advantage,
            CombatObjective.DestroyEnemyStone => advantage * 0.75f,
            CombatObjective.Retreat => advantage * -0.75f,
            _ => 0f,
        };
    }

    private static bool IsDamageWeapon(WeaponBase weapon)
    {
        if (weapon == null) return false;
        return weapon.Kind == WeaponKind.Sword ||
            weapon.Kind == WeaponKind.Wand ||
            weapon.Kind == WeaponKind.Grimoire;
    }

    private static bool HasStableAllyFrontline(CombatAiContext context)
    {
        for (int i = 0; i < context.AllyIntel.Count; i++)
        {
            CombatCharacterIntel ally = context.AllyIntel[i];
            if (!ally.CanAct || ally.MaxHP <= 0 || ally.HP / (float)ally.MaxHP < 0.5f) continue;
            if (!ally.HasObjective ||
                (ally.Objective != CombatObjective.AttackEnemy && ally.Objective != CombatObjective.DefendOwnStone))
            {
                continue;
            }

            if (IsLivingEnemy(context, ally.IntendedTarget))
            {
                return true;
            }

            for (int j = 0; j < context.EnemyIntel.Count; j++)
            {
                CombatCharacterIntel enemy = context.EnemyIntel[j];
                if (!enemy.IsAlive || !enemy.HasKnownPosition) continue;
                if (HorizontalDistance(ally.CurrentPosition, enemy.KnownPosition) <= 8f)
                {
                    return true;
                }
            }
        }

        return false;
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
        float ownStoneThreat = assessment.GetValue(CombatAiMetricIndex.OwnStoneThreat);
        bool hasFrontlineAlly = HasAdvancingFrontlineAlly(context);
        return objective switch
        {
            CombatObjective.DefendOwnStone when context.HasOwnStonePosition
                && ownStoneThreat > 18f => 18f,
            CombatObjective.DefendOwnStone when ownStoneThreat < 12f && hasFrontlineAlly => -24f,
            CombatObjective.SupportAlly when ownStoneThreat < 18f && hasFrontlineAlly => 32f,
            CombatObjective.AttackEnemy when ownStoneThreat < 16f
                && assessment.GetValue(CombatAiMetricIndex.ReachableEnemyValue) > 24f => 10f,
            CombatObjective.DestroyEnemyStone when context.HasEnemyStonePosition
                && assessment.GetValue(CombatAiMetricIndex.EnemyStoneReachability) > 30f
                && ownStoneThreat < 12f
                && assessment.GetValue(CombatAiMetricIndex.AllyFragility) < 18f => 8f,
            _ => 0f,
        };
    }

    private static bool HasAdvancingFrontlineAlly(CombatAiContext context)
    {
        if (context == null) return false;

        for (int i = 0; i < context.AllyIntel.Count; i++)
        {
            CombatCharacterIntel ally = context.AllyIntel[i];
            if (!ally.CanAct || !ally.HasObjective) continue;
            if (ally.Objective != CombatObjective.AttackEnemy &&
                ally.Objective != CombatObjective.DestroyEnemyStone) continue;
            if (ally.IntendedTarget != null || ally.HasIntendedDestination || IsNearKnownEnemy(context, ally.CurrentPosition))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsNearKnownEnemy(CombatAiContext context, Vector3 position)
    {
        for (int i = 0; i < context.EnemyIntel.Count; i++)
        {
            CombatCharacterIntel enemy = context.EnemyIntel[i];
            if (enemy.IsAlive && enemy.HasKnownPosition && HorizontalDistance(position, enemy.KnownPosition) <= 10f)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsLivingEnemy(CombatAiContext context, Character character)
    {
        if (character == null) return false;

        for (int i = 0; i < context.EnemyIntel.Count; i++)
        {
            CombatCharacterIntel enemy = context.EnemyIntel[i];
            if (enemy.Character == character) return enemy.IsAlive;
        }

        return false;
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
        bool multipleEnemiesVisible = CountVisibleLivingEnemies(context) >= 2;
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

    private static float GetWeaponScore(WeaponBase weapon, CombatObjective objective)
    {
        WeaponKind kind = weapon != null ? weapon.Kind : WeaponKind.Unarmed;
        return CombatAiWeaponWeights.GetObjectiveWeight(kind, objective);
    }

    private static int CountVisibleLivingEnemies(CombatAiContext context)
    {
        int count = 0;
        for (int i = 0; i < context.EnemyIntel.Count; i++)
        {
            CombatCharacterIntel enemy = context.EnemyIntel[i];
            if (enemy.IsAlive && enemy.HasDirectSight) count++;
        }

        return count;
    }

    private static float GetPersonalityScore(
        CombatAiContext context,
        CombatAiPersonalityProfile personalityProfile,
        CombatAiAssessment assessment,
        CombatObjective objective)
    {
        return CombatAiPersonalityBehavior.GetObjectiveScore(context, personalityProfile, assessment, objective);
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

    private static float HorizontalDistance(UnityEngine.Vector3 a, UnityEngine.Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return UnityEngine.Vector3.Distance(a, b);
    }
}
