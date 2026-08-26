using System.Collections.Generic;
using UnityEngine;

public static class CombatAiObjectiveScorer
{
    private const float NumericalAdvantagePerCharacter = 6f;
    private const float MaximumNumericalAdvantageScore = 24f;
    /// <summary>現目標を維持し、この差を超えたときだけ切替（境界振動の抑制）。</summary>
    private const float ObjectiveSwitchMargin = 28f;
    /// <summary>撤退・自石防衛への割り込みにも最低限の差を要求する。</summary>
    private const float CrisisInterruptMargin = 12f;
    private static readonly CombatObjective[] AllObjectives = (CombatObjective[])System.Enum.GetValues(typeof(CombatObjective));

    public static CombatObjective SelectBestObjective(
        CombatAiContext context,
        CombatAiAssessment assessment,
        CombatAiPersonalityProfile personalityProfile,
        Character focusEnemy,
        float focusCommitmentRemainingSeconds,
        CombatObjective previousObjective,
        List<CombatAiReasonCode> selectedReasons = null,
        float objectiveCommitmentRemainingSeconds = 0f,
        List<CombatAiObjectiveScoreEntry> entries = null)
    {
        entries?.Clear();
        return EvaluateObjectives(
            context,
            assessment,
            personalityProfile,
            focusEnemy,
            focusCommitmentRemainingSeconds,
            previousObjective,
            objectiveCommitmentRemainingSeconds,
            entries,
            selectedReasons);
    }

    private static CombatObjective EvaluateObjectives(
        CombatAiContext context,
        CombatAiAssessment assessment,
        CombatAiPersonalityProfile personalityProfile,
        Character focusEnemy,
        float focusCommitmentRemainingSeconds,
        CombatObjective previousObjective,
        float objectiveCommitmentRemainingSeconds,
        List<CombatAiObjectiveScoreEntry> entries,
        List<CombatAiReasonCode> selectedReasons = null)
    {
        WeaponBase weapon = context.Owner != null ? context.Owner.EquippedWeapon : null;
        CombatObjective best = CombatObjective.Search;
        float bestScore = float.NegativeInfinity;
        float bestBaseScore = 0f;
        float bestSituationScore = 0f;
        float bestWeaponScore = 0f;
        float bestPersonalityScore = 0f;
        float previousScore = float.NegativeInfinity;
        float previousBaseScore = 0f;
        float previousSituationScore = 0f;
        float previousWeaponScore = 0f;
        float previousPersonalityScore = 0f;
        bool previousSelectable = false;
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
            float personalityScore = CombatAiPersonalityBehavior.GetObjectiveScore(
                context, personalityProfile, objective);
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

            if (objective == previousObjective)
            {
                previousSelectable = true;
                previousScore = score;
                previousBaseScore = baseScore;
                previousSituationScore = situationScore;
                previousWeaponScore = weaponScore;
                previousPersonalityScore = personalityScore;
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

        if (previousSelectable &&
            best != previousObjective &&
            ShouldKeepPreviousObjective(
                previousObjective,
                best,
                bestScore,
                previousScore,
                objectiveCommitmentRemainingSeconds))
        {
            best = previousObjective;
            bestScore = previousScore;
            bestBaseScore = previousBaseScore;
            bestSituationScore = previousSituationScore;
            bestWeaponScore = previousWeaponScore;
            bestPersonalityScore = previousPersonalityScore;
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

    private static bool ShouldKeepPreviousObjective(
        CombatObjective previous,
        CombatObjective challenger,
        float challengerScore,
        float previousScore,
        float objectiveCommitmentRemainingSeconds)
    {
        // 索敵はコミット対象外。初回判断を 28pt ヒステリシスで固めない。
        if (previous == CombatObjective.Search && objectiveCommitmentRemainingSeconds <= 0f)
        {
            return false;
        }

        // 危機目標でも僅差では割り込ませない（防衛↔援護の 2〜4 秒往復を抑える）。
        if (IsCrisisObjective(challenger))
        {
            return challengerScore < previousScore + CrisisInterruptMargin;
        }

        if (objectiveCommitmentRemainingSeconds > 0f) return true;
        return challengerScore < previousScore + ObjectiveSwitchMargin;
    }

    private static bool IsCrisisObjective(CombatObjective objective)
    {
        return objective == CombatObjective.Retreat || objective == CombatObjective.DefendOwnStone;
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
        AddReasons(context, assessment, objective, breakdown);
        if (GetNumericalAdvantageAdjustment(context, objective) != 0f)
        {
            AddReason(
                breakdown,
                GetNumericalBalance(context) >= 0f
                    ? CombatAiReasonCode.NumericalAdvantage
                    : CombatAiReasonCode.NumericalDisadvantage);
        }
        if (weaponScore != 0f) AddReason(breakdown, CombatAiReasonCode.WeaponPreference);
        if (personalityScore != 0f) AddReason(breakdown, CombatAiReasonCode.PersonalityPreference);
        return breakdown;
    }

    private static bool IsObjectiveSelectable(CombatAiContext context, CombatObjective objective)
    {
        WeaponBase ownerWeapon = context.Owner != null ? context.Owner.EquippedWeapon : null;
        if (ownerWeapon != null && ownerWeapon.Kind == WeaponKind.Shield)
        {
            if (objective == CombatObjective.AttackEnemy && HasAdvancingFrontlineAlly(context))
            {
                return false;
            }

            // 剣／杖がいるときは索敵より追従を優先する。
            if (objective == CombatObjective.Search && HasPreferredEscortAlly(context))
            {
                return false;
            }
        }

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
            CombatObjective.SupportAlly => hasPossibleEnemy && HasLivingAlly(context),
            CombatObjective.Search => hasPossibleEnemy,
            CombatObjective.Retreat => hasPossibleEnemy,
            _ => false,
        };
    }

    private static bool HasAttackTarget(CombatAiContext context)
    {
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
        bool assaultWeapon = CombatAiPositioning.IsAssaultWeapon(weapon);
        float score = objective switch
        {
            CombatObjective.AttackEnemy => GetAttackEnemySituationScore(context, assessment, weapon),
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
                - (100f - assessment.GetValue(CombatAiMetricIndex.EnemyLocationConfidence))
                    * GetDestroyStoneConfidencePenalty(weapon)
                + (context.HasEnemyStonePosition ? 4f : 0f)
                + (assaultWeapon && CanAttackEnemyStoneWithoutMoving(context) ? 52f : 0f)
                + UnityEngine.Mathf.Max(0f, 8f - assessment.GetValue(CombatAiMetricIndex.AllyFragility) * 0.1f),
            CombatObjective.Search => (100f - assessment.GetValue(CombatAiMetricIndex.EnemyLocationConfidence)) * 0.55f
                + assessment.GetValue(CombatAiMetricIndex.TerrainAdvantage) * 0.2f
                - (context.HasEnemyStonePosition
                    ? (assaultWeapon ? 36f : 14f)
                    : 0f),
            CombatObjective.Retreat => assessment.GetValue(CombatAiMetricIndex.SelfThreat) * 0.9f
                + assessment.GetValue(CombatAiMetricIndex.RetreatRouteSafety) * 0.3f
                + assessment.GetValue(CombatAiMetricIndex.AllyFragility) * 0.1f,
            _ => 0f,
        };

        return score
            + GetNumericalAdvantageAdjustment(context, objective)
            + GetWeaponSituationAdjustment(context, assessment, weapon, objective);
    }

    private static float GetAttackEnemySituationScore(
        CombatAiContext context,
        CombatAiAssessment assessment,
        WeaponBase weapon)
    {
        float score = assessment.GetValue(CombatAiMetricIndex.ReachableEnemyValue) * 0.9f
            + assessment.GetValue(CombatAiMetricIndex.EnemyThreatLevel) * 0.25f
            + assessment.GetValue(CombatAiMetricIndex.KillableTargetValue) * 0.35f
            + assessment.GetValue(CombatAiMetricIndex.TerrainAdvantage) * 0.15f
            - assessment.GetValue(CombatAiMetricIndex.SelfThreat) * 0.35f;

        // 攻撃職が、その場で魔石を殴れるなら近い敵がいても魔石破壊を優先する。
        if (CombatAiPositioning.IsAssaultWeapon(weapon) && CanAttackEnemyStoneWithoutMoving(context))
        {
            score -= 56f;
        }
        // 攻撃職は「本当に近い敵」のときだけ敵攻撃へ切り替える。遠距離の既知敵では魔石を優先する。
        else if (CombatAiPositioning.IsAssaultWeapon(weapon) && !HasCloseEngagementThreat(context, weapon))
        {
            score -= 48f;
        }

        return score;
    }

    private static bool CanAttackEnemyStoneWithoutMoving(CombatAiContext context)
    {
        if (context?.Owner == null || !context.HasEnemyStonePosition) return false;
        if (context.HasEnemyStoneHealth && context.EnemyStoneHP <= 0) return false;

        float distance = HorizontalDistance(
            context.Owner.transform.position,
            context.EnemyStonePosition);
        IReadOnlyList<SkillBase> skills = context.Owner.AvailableCombatSkills;
        CombatSkillCooldowns cooldowns = context.Owner.SkillCooldowns;
        for (int i = 0; i < skills.Count; i++)
        {
            SkillBase skill = skills[i];
            if (skill == null || !skill.CanTargetMagicStone) continue;
            if (!CombatAiSkillClassifier.IsDamage(skill)) continue;
            if (cooldowns != null && !cooldowns.IsReady(skill)) continue;
            if (distance <= skill.MaxRange) return true;
        }

        return false;
    }

    private static float GetDestroyStoneConfidencePenalty(WeaponBase weapon)
    {
        // 攻撃職は敵未確認でも魔石前進を捨てにくくする。支援職は従来どおり索敵寄り。
        return CombatAiPositioning.IsAssaultWeapon(weapon) ? 0.15f : 0.5f;
    }

    private static bool HasCloseEngagementThreat(CombatAiContext context, WeaponBase weapon)
    {
        if (context == null || context.Owner == null) return false;

        float engagementRange = GetCloseEngagementRange(weapon);
        Vector3 ownerPosition = context.Owner.transform.position;
        for (int i = 0; i < context.EnemyIntel.Count; i++)
        {
            CombatCharacterIntel enemy = context.EnemyIntel[i];
            if (!enemy.IsAlive || !enemy.HasKnownPosition) continue;
            if (HorizontalDistance(ownerPosition, enemy.KnownPosition) <= engagementRange)
            {
                return true;
            }
        }

        return false;
    }

    private static float GetCloseEngagementRange(WeaponBase weapon)
    {
        if (weapon == null) return 4f;

        // 杖は射程が長いので、魔石割り込みは「近い脅威」に限定する。
        if (weapon.Kind == WeaponKind.Wand)
        {
            return Mathf.Min(weapon.Range, 10f);
        }

        return Mathf.Max(3.5f, weapon.Range + 2.5f);
    }

    private static float GetNumericalAdvantageAdjustment(CombatAiContext context, CombatObjective objective)
    {
        float advantage = Mathf.Clamp(
            GetNumericalBalance(context) * NumericalAdvantagePerCharacter,
            -MaximumNumericalAdvantageScore,
            MaximumNumericalAdvantageScore);
        return objective switch
        {
            CombatObjective.AttackEnemy => advantage,
            CombatObjective.DestroyEnemyStone => advantage * 0.75f,
            CombatObjective.Retreat => advantage * -0.75f,
            _ => 0f,
        };
    }

    private static int GetNumericalBalance(CombatAiContext context)
    {
        int livingAllies = context.Owner != null &&
            context.Owner.Health != null &&
            context.Owner.Health.IsAlive
                ? 1
                : 0;
        for (int i = 0; i < context.AllyIntel.Count; i++)
        {
            if (context.AllyIntel[i].IsAlive) livingAllies++;
        }

        int livingEnemies = 0;
        for (int i = 0; i < context.EnemyIntel.Count; i++)
        {
            if (context.EnemyIntel[i].IsAlive) livingEnemies++;
        }

        return livingAllies - livingEnemies;
    }

    private static bool HasLivingAlly(CombatAiContext context)
    {
        for (int i = 0; i < context.AllyIntel.Count; i++)
        {
            if (context.AllyIntel[i].IsAlive) return true;
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
            CombatObjective.AttackEnemy when HasCloseEngagementThreat(context, context.Owner != null ? context.Owner.EquippedWeapon : null)
                && assessment.GetValue(CombatAiMetricIndex.ReachableEnemyValue) > 30f
                && assessment.GetValue(CombatAiMetricIndex.SelfThreat) < 35f => 16f,
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
        bool hasPreferredEscort = HasPreferredEscortAlly(context);
        return objective switch
        {
            CombatObjective.DefendOwnStone when context.HasOwnStonePosition
                && ownStoneThreat > 18f => 18f,
            CombatObjective.DefendOwnStone when ownStoneThreat < 12f && (hasFrontlineAlly || hasPreferredEscort) => -24f,
            CombatObjective.SupportAlly when ownStoneThreat < 22f && hasPreferredEscort => 40f,
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

    private static bool HasPreferredEscortAlly(CombatAiContext context)
    {
        for (int i = 0; i < context.AllyIntel.Count; i++)
        {
            CombatCharacterIntel ally = context.AllyIntel[i];
            if (!ally.CanAct || ally.Character == context.Owner) continue;
            if (CombatAiPositioning.IsAssaultWeaponKind(ally.WeaponKind)) return true;
        }

        return false;
    }

    private static bool HasAdvancingFrontlineAlly(CombatAiContext context)
    {
        for (int i = 0; i < context.AllyIntel.Count; i++)
        {
            if (CombatAiPositioning.IsAdvancingAlly(context, context.AllyIntel[i])) return true;
        }

        return false;
    }

    private static float GetWandSituationAdjustment(
        CombatAiContext context,
        CombatAiAssessment assessment,
        CombatObjective objective)
    {
        return objective switch
        {
            CombatObjective.Search when assessment.GetValue(CombatAiMetricIndex.EnemyLocationConfidence) < 45f
                && assessment.GetValue(CombatAiMetricIndex.ReachableEnemyValue) < 24f
                && !context.HasEnemyStonePosition => 16f,
            CombatObjective.AttackEnemy when HasCloseEngagementThreat(context, context.Owner != null ? context.Owner.EquippedWeapon : null)
                && assessment.GetValue(CombatAiMetricIndex.ReachableEnemyValue) > 28f
                && assessment.GetValue(CombatAiMetricIndex.EnemyLocationConfidence) > 35f => 14f,
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
            CombatObjective.Search when assessment.GetValue(CombatAiMetricIndex.EnemyLocationConfidence) < 45f
                || assessment.GetValue(CombatAiMetricIndex.ReachableEnemyValue) < 18f => 14f,
            CombatObjective.DestroyEnemyStone when context.HasEnemyStonePosition
                && assessment.GetValue(CombatAiMetricIndex.EnemyStoneReachability) > 34f
                && assessment.GetValue(CombatAiMetricIndex.OwnStoneThreat) < 18f => 4f,
            _ => 0f,
        };
    }

    private static float GetBibleSituationAdjustment(
        CombatAiContext context,
        CombatAiAssessment assessment,
        CombatObjective objective)
    {
        bool stableFrontline = assessment.GetValue(CombatAiMetricIndex.AllyFragility) < 18f
            && assessment.GetValue(CombatAiMetricIndex.OwnStoneThreat) < 16f;
        return objective switch
        {
            CombatObjective.SupportAlly when context.AllyIntel.Count > 0
                && assessment.GetValue(CombatAiMetricIndex.AllyFragility) > 12f => 18f,
            CombatObjective.DefendOwnStone when context.HasOwnStonePosition
                && (assessment.GetValue(CombatAiMetricIndex.OwnStoneThreat) > 18f
                    || assessment.GetValue(CombatAiMetricIndex.AllyFragility) > 20f) => 14f,
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
        bool stableLine = assessment.GetValue(CombatAiMetricIndex.AllyFragility) < 16f
            && assessment.GetValue(CombatAiMetricIndex.SelfThreat) < 20f;
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

    private static void AddReasons(
        CombatAiContext context,
        CombatAiAssessment assessment,
        CombatObjective objective,
        CombatAiScoreBreakdown breakdown)
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
                if (assessment.GetValue(CombatAiMetricIndex.EnemyStoneReachability) > 25f
                    || CanAttackEnemyStoneWithoutMoving(context))
                {
                    AddReason(breakdown, CombatAiReasonCode.EnemyStoneReachable);
                }
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
