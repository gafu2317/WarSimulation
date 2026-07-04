using System.Collections.Generic;
using UnityEngine;

public static class CombatAiPlanner
{
    private const float UnselectableScore = -100000f;
    private const float RosaryPreferredSupportDistance = 5.5f;
    private const float RosaryCloseHealDistance = 2.5f;
    private const float RosaryEnemyClearanceDistance = 6.5f;
    private const float WandRangeSlack = 0.75f;
    private const float WandEnemyClearanceDistance = 7f;
    private const float SwordRetargetThreshold = 20f;

    public static CombatAiPlan BuildPlan(
        CombatAiContext context,
        CombatAiPersonalityProfile personalityProfile,
        CombatAiWeaponWeightsProfile weaponWeightsProfile = null,
        Character focusEnemy = null,
        float focusCommitmentRemainingSeconds = 0f,
        CombatObjective previousObjective = CombatObjective.Search)
    {
        CombatAiDebugSnapshot snapshot = BuildDebugSnapshot(
            context,
            personalityProfile,
            weaponWeightsProfile,
            focusEnemy,
            focusCommitmentRemainingSeconds,
            previousObjective);
        return snapshot != null ? snapshot.FinalPlan : CombatAiPlan.None;
    }

    public static CombatAiDebugSnapshot BuildDebugSnapshot(
        CombatAiContext context,
        CombatAiPersonalityProfile personalityProfile,
        CombatAiWeaponWeightsProfile weaponWeightsProfile = null,
        Character focusEnemy = null,
        float focusCommitmentRemainingSeconds = 0f,
        CombatObjective previousObjective = CombatObjective.Search)
    {
        if (context == null || context.Owner == null)
        {
            return null;
        }

        var snapshot = new CombatAiDebugSnapshot
        {
            Owner = context.Owner,
            Context = context,
            ContextSummary = CombatAiAssessmentBuilder.BuildSummary(context, personalityProfile, weaponWeightsProfile),
            Assessment = CombatAiAssessmentBuilder.Build(context),
        };

        CombatAiObjectiveScorer.BuildEntries(
            snapshot,
            personalityProfile,
            weaponWeightsProfile,
            focusEnemy,
            focusCommitmentRemainingSeconds,
            previousObjective);
        snapshot.SelectedObjective = SelectHighest(snapshot.ObjectiveEntries);
        BuildMoveEntries(
            snapshot,
            personalityProfile,
            weaponWeightsProfile,
            focusEnemy,
            focusCommitmentRemainingSeconds);
        snapshot.SelectedMove = SelectHighest(snapshot.MoveEntries);
        BuildSkillEntries(
            snapshot,
            personalityProfile,
            weaponWeightsProfile,
            focusEnemy,
            focusCommitmentRemainingSeconds);
        snapshot.SelectedSkill = SelectHighest(snapshot.SkillEntries);

        snapshot.FinalPlan = new CombatAiPlan(
            snapshot.SelectedObjective != null ? snapshot.SelectedObjective.Objective : CombatObjective.Search,
            snapshot.SelectedMove != null ? snapshot.SelectedMove.Target : CombatMoveTarget.None,
            snapshot.SelectedSkill != null ? snapshot.SelectedSkill.Skill : null,
            snapshot.SelectedSkill != null ? snapshot.SelectedSkill.SkillContext : SkillExecutionContext.None);
        return snapshot;
    }

    private static void BuildMoveEntries(
        CombatAiDebugSnapshot snapshot,
        CombatAiPersonalityProfile personalityProfile,
        CombatAiWeaponWeightsProfile weaponWeightsProfile,
        Character focusEnemy,
        float focusCommitmentRemainingSeconds)
    {
        snapshot.MoveEntries.Clear();
        CombatObjective objective = snapshot.SelectedObjective != null ? snapshot.SelectedObjective.Objective : CombatObjective.Search;
        AddMoveCandidate(snapshot, personalityProfile, weaponWeightsProfile, CombatAiMoveCode.AdvanceEnemyStone, "敵魔石へ前進", CreateEnemyStoneTarget(snapshot.Context), objective, focusEnemy, focusCommitmentRemainingSeconds);
        AddMoveCandidate(snapshot, personalityProfile, weaponWeightsProfile, CombatAiMoveCode.ReturnOwnStone, "自軍魔石へ戻る", CreateOwnStoneTarget(snapshot.Context), objective, focusEnemy, focusCommitmentRemainingSeconds);
        AddMoveCandidate(snapshot, personalityProfile, weaponWeightsProfile, CombatAiMoveCode.PursueEnemy, "敵へ接近", CreateBestEnemyTarget(snapshot.Context, focusEnemy, focusCommitmentRemainingSeconds), objective, focusEnemy, focusCommitmentRemainingSeconds);
        AddMoveCandidate(snapshot, personalityProfile, weaponWeightsProfile, CombatAiMoveCode.SupportAlly, "味方へ接近", CreateBestAllyTarget(snapshot.Context), objective, focusEnemy, focusCommitmentRemainingSeconds);
        AddMoveCandidate(snapshot, personalityProfile, weaponWeightsProfile, CombatAiMoveCode.TakeHighGround, "高所へ移動", CreateNearestPositionTarget(snapshot.Owner, snapshot.Context.HighGroundCandidates), objective, focusEnemy, focusCommitmentRemainingSeconds);
        AddMoveCandidate(snapshot, personalityProfile, weaponWeightsProfile, CombatAiMoveCode.MoveForest, "森へ移動", CreateNearestPositionTarget(snapshot.Owner, snapshot.Context.ForestCandidates), objective, focusEnemy, focusCommitmentRemainingSeconds);
        AddMoveCandidate(snapshot, personalityProfile, weaponWeightsProfile, CombatAiMoveCode.SearchLastKnown, "最終既知地点へ移動", CreateLastKnownEnemyTarget(snapshot.Context), objective, focusEnemy, focusCommitmentRemainingSeconds);
        AddMoveCandidate(snapshot, personalityProfile, weaponWeightsProfile, CombatAiMoveCode.HoldPosition, "待機", CombatMoveTarget.None, objective, focusEnemy, focusCommitmentRemainingSeconds);
    }

    private static void BuildSkillEntries(
        CombatAiDebugSnapshot snapshot,
        CombatAiPersonalityProfile personalityProfile,
        CombatAiWeaponWeightsProfile weaponWeightsProfile,
        Character focusEnemy,
        float focusCommitmentRemainingSeconds)
    {
        snapshot.SkillEntries.Clear();
        IReadOnlyList<SkillBase> skills = snapshot.Owner.AvailableCombatSkills;
        for (int i = 0; i < skills.Count; i++)
        {
            SkillBase skill = skills[i];
            AddSkillEntries(snapshot, personalityProfile, weaponWeightsProfile, skill, focusEnemy, focusCommitmentRemainingSeconds);
        }

        var waitBreakdown = new CombatAiScoreBreakdown
        {
            BaseScore = snapshot.SkillEntries.Count == 0 ? 12f : 3f,
            SituationScore = snapshot.SelectedObjective != null && snapshot.SelectedObjective.Objective == CombatObjective.Retreat ? 8f : 0f,
        };
        snapshot.SkillEntries.Add(new CombatAiSkillCandidateEntry
        {
            Code = "Wait",
            Label = CombatAiDebugLabels.Format("Wait", "何もしない"),
            Skill = null,
            SkillContext = SkillExecutionContext.None,
            Evaluation = default,
            Breakdown = waitBreakdown,
        });
    }

    private static void AddMoveCandidate(
        CombatAiDebugSnapshot snapshot,
        CombatAiPersonalityProfile personalityProfile,
        CombatAiWeaponWeightsProfile weaponWeightsProfile,
        string code,
        string japanese,
        CombatMoveTarget target,
        CombatObjective objective,
        Character focusEnemy,
        float focusCommitmentRemainingSeconds)
    {
        if (code != CombatAiMoveCode.HoldPosition && !target.HasDestination)
        {
            return;
        }

        CombatAiScoreBreakdown breakdown = CombatAiMoveScorer.Score(
            snapshot,
            personalityProfile,
            weaponWeightsProfile,
            code,
            target,
            objective,
            focusEnemy,
            focusCommitmentRemainingSeconds);

        snapshot.MoveEntries.Add(new CombatAiMoveCandidateEntry
        {
            Code = code,
            Label = CombatAiDebugLabels.MoveCode(code, japanese),
            Target = target,
            Breakdown = breakdown,
        });
    }

    private static void AddSkillEntries(
        CombatAiDebugSnapshot snapshot,
        CombatAiPersonalityProfile personalityProfile,
        CombatAiWeaponWeightsProfile weaponWeightsProfile,
        SkillBase skill,
        Character focusEnemy,
        float focusCommitmentRemainingSeconds)
    {
        List<SkillExecutionContext> contexts = CombatAiSkillContextBuilder.Build(snapshot.Context, snapshot.Owner, skill);
        for (int i = 0; i < contexts.Count; i++)
        {
            AddSkillEntry(snapshot, personalityProfile, weaponWeightsProfile, skill, contexts[i], i, focusEnemy, focusCommitmentRemainingSeconds);
        }
    }

    private static void AddSkillEntry(
        CombatAiDebugSnapshot snapshot,
        CombatAiPersonalityProfile personalityProfile,
        CombatAiWeaponWeightsProfile weaponWeightsProfile,
        SkillBase skill,
        SkillExecutionContext context,
        int contextIndex,
        Character focusEnemy,
        float focusCommitmentRemainingSeconds)
    {
        CombatSkillEvaluationResult evaluation = CombatSkillEvaluator.Evaluate(snapshot.Owner, skill, context);
        CombatObjective objective = snapshot.SelectedObjective != null ? snapshot.SelectedObjective.Objective : CombatObjective.Search;
        var breakdown = new CombatAiScoreBreakdown
        {
            BaseScore = GetSkillBaseScore(skill, objective),
            WeaponScore = GetSkillWeaponScore(weaponWeightsProfile, snapshot.Owner.EquippedWeapon, skill),
            PersonalityScore = GetSkillPersonalityScore(personalityProfile, skill, objective),
            SituationScore = GetSkillSituationScore(snapshot.Context, snapshot.Assessment, skill, evaluation, objective)
                + CombatAiFocusTargeting.GetSkillScore(snapshot.Context, snapshot.Owner.EquippedWeapon, skill, evaluation, focusEnemy, focusCommitmentRemainingSeconds),
        };
        AddSkillReasons(evaluation, breakdown);
        if (breakdown.WeaponScore != 0f) AddReason(breakdown, CombatAiReasonCode.WeaponPreference);
        if (breakdown.PersonalityScore != 0f) AddReason(breakdown, CombatAiReasonCode.PersonalityPreference);

        snapshot.SkillEntries.Add(new CombatAiSkillCandidateEntry
        {
            Code = BuildSkillCandidateCode(skill, contextIndex),
            Label = CombatAiDebugLabels.Skill(skill) + " / " + FormatSkillContextLabel(evaluation.Context),
            Skill = skill,
            SkillContext = evaluation.Context,
            Evaluation = evaluation,
            Breakdown = breakdown,
        });
    }

    private static float GetSkillBaseScore(SkillBase skill, CombatObjective objective)
    {
        if (skill == null) return 0f;
        float score = objective switch
        {
            CombatObjective.AttackEnemy => CombatAiSkillClassifier.IsDamage(skill) ? 32f : CombatAiSkillClassifier.IsDebuff(skill) ? 16f : 4f,
            CombatObjective.SupportAlly => CombatAiSkillClassifier.IsSupport(skill) ? 34f : CombatAiSkillClassifier.IsDamage(skill) ? 8f : 4f,
            CombatObjective.DefendOwnStone => CombatAiSkillClassifier.IsProtect(skill) ? 26f : CombatAiSkillClassifier.IsDamage(skill) ? 18f : 6f,
            CombatObjective.DestroyEnemyStone => CombatAiSkillClassifier.IsDamage(skill) ? 26f : 4f,
            CombatObjective.Retreat => CombatAiSkillClassifier.IsProtect(skill) || CombatAiSkillClassifier.IsHeal(skill) ? 20f : 2f,
            CombatObjective.Search => CombatAiSkillClassifier.IsMobility(skill) ? 12f : 4f,
            _ => 4f,
        };
        if (CombatAiSkillClassifier.IsBasicAttack(skill))
        {
            score += 6f;
        }

        return score;
    }

    private static float GetSkillWeaponScore(
        CombatAiWeaponWeightsProfile weaponWeightsProfile,
        WeaponBase weapon,
        SkillBase skill)
    {
        if (weapon == null || skill == null) return 0f;
        return weaponWeightsProfile != null
            ? weaponWeightsProfile.GetSkillWeight(weapon.Kind, skill)
            : CombatAiWeaponWeightsProfile.GetDefaultSkillWeight(weapon.Kind, skill);
    }

    private static float GetSkillPersonalityScore(CombatAiPersonalityProfile personalityProfile, SkillBase skill, CombatObjective objective)
    {
        if (personalityProfile == null || skill == null) return 0f;
        if (CombatAiSkillClassifier.IsDamage(skill))
        {
            return personalityProfile.Aggression * 10f - personalityProfile.Caution * 2f;
        }

        if (CombatAiSkillClassifier.IsSupport(skill))
        {
            return personalityProfile.SupportBias * 12f + personalityProfile.Caution * 4f;
        }

        if (CombatAiSkillClassifier.IsMobility(skill) && objective == CombatObjective.Search)
        {
            return personalityProfile.ExplorationBias * 10f;
        }

        return 0f;
    }

    private static float GetSkillSituationScore(
        CombatAiContext context,
        CombatAiAssessment assessment,
        SkillBase skill,
        CombatSkillEvaluationResult evaluation,
        CombatObjective objective)
    {
        if (skill == null) return 0f;
        if (!evaluation.CanUse) return UnselectableScore;

        float score = evaluation.CanUse ? 16f : -20f;
        if (CombatAiSkillClassifier.IsHeal(skill) || CombatAiSkillClassifier.IsBuff(skill) || CombatAiSkillClassifier.IsProtect(skill))
        {
            score += assessment.GetValue("AllyFragility") * 0.25f;
        }

        if (CombatAiSkillClassifier.IsDamage(skill) || CombatAiSkillClassifier.IsDebuff(skill))
        {
            score += assessment.GetValue("ReachableEnemyValue") * 0.25f;
        }

        score += GetObjectiveSkillAlignmentScore(skill, evaluation, assessment, context, objective);

        if (evaluation.HasAreaPreview && evaluation.ResolvedTargetCount >= 2)
        {
            score += 10f;
        }

        score += GetSkillTargetScore(context, skill, evaluation.Context);
        return score;
    }

    private static float GetSkillTargetScore(CombatAiContext context, SkillBase skill, SkillExecutionContext skillContext)
    {
        if (skillContext.PrimaryTarget == null)
        {
            return skillContext.PrimaryStone != null && CombatAiSkillClassifier.IsDamage(skill) ? 24f : 0f;
        }

        if (CombatAiSkillClassifier.IsHeal(skill) || CombatAiSkillClassifier.IsBuff(skill) || CombatAiSkillClassifier.IsProtect(skill))
        {
            CombatCharacterIntel ally = FindAllyIntel(context, skillContext.PrimaryTarget);
            if (ally.MaxHP <= 0) return 0f;

            float hpRatio = (float)ally.HP / ally.MaxHP;
            float missingHpRatio = 1f - hpRatio;
            if (CombatAiSkillClassifier.IsHeal(skill) && missingHpRatio <= 0.05f)
            {
                return -80f;
            }

            float score = missingHpRatio * (CombatAiSkillClassifier.IsHeal(skill) ? 50f : 18f);
            if (HasEnemyNearby(context.EnemyIntel, ally.CurrentPosition, 8f))
            {
                score += CombatAiSkillClassifier.IsProtect(skill) ? 24f : 8f;
            }

            score += GetSupportTargetAffinityScore(skill, ally);
            if (HasEquivalentEffect(skill, ally.StatusEffects))
            {
                score -= 70f;
            }

            return score;
        }

        if (CombatAiSkillClassifier.IsDamage(skill) || CombatAiSkillClassifier.IsDebuff(skill))
        {
            CombatCharacterIntel enemy = FindEnemyIntel(context, skillContext.PrimaryTarget);
            if (enemy.Character == null) return 0f;

            float hpRatio = enemy.MaxHP > 0 ? (float)enemy.HP / enemy.MaxHP : 1f;
            float score = (1f - hpRatio) * 18f + (enemy.HasDirectSight ? 8f : enemy.HasMemory ? 3f : 0f);
            score += GetDebuffTargetAffinityScore(skill, enemy);
            if (HasEquivalentEffect(skill, enemy.StatusEffects))
            {
                score -= 70f;
            }

            return score;
        }

        return 0f;
    }

    private static void AddSkillReasons(CombatSkillEvaluationResult evaluation, CombatAiScoreBreakdown breakdown)
    {
        if (evaluation.CanUse)
        {
            AddReason(breakdown, CombatAiReasonCode.SkillReady);
            AddReason(breakdown, CombatAiReasonCode.SkillMatchesObjective);
        }
        else
        {
            AddReason(breakdown, CombatAiReasonCode.TargetInvalid);
            if (!string.IsNullOrEmpty(evaluation.FailureReason) && evaluation.FailureReason.Contains("range"))
            {
                AddReason(breakdown, CombatAiReasonCode.TargetOutOfRange);
            }
        }

        if ((evaluation.Context.PrimaryTarget != null || evaluation.Context.PrimaryStone != null) && evaluation.CanUse)
        {
            AddReason(breakdown, CombatAiReasonCode.TargetInSkillRange);
        }

        if (evaluation.HasAreaPreview && evaluation.ResolvedTargetCount >= 2)
        {
            AddReason(breakdown, CombatAiReasonCode.SkillAreaHitsMultiple);
        }
    }

    private static string BuildSkillCandidateCode(SkillBase skill, int contextIndex)
    {
        string skillCode = skill is IdentifiedSkill identified ? identified.SkillId.ToString() : skill.GetType().Name;
        return skillCode + "#" + contextIndex;
    }

    private static string FormatSkillContextLabel(SkillExecutionContext context)
    {
        if (context.PrimaryTarget != null)
        {
            return context.PrimaryTarget.name;
        }

        if (context.PrimaryStone != null)
        {
            return context.PrimaryStone.name;
        }

        if (context.HasTargetPoint)
        {
            Vector3 point = context.TargetPoint;
            return "(" + point.x.ToString("0.0") + ", " + point.z.ToString("0.0") + ")";
        }

        return "None";
    }

    private static CombatAiObjectiveScoreEntry SelectHighest(List<CombatAiObjectiveScoreEntry> entries)
    {
        CombatAiObjectiveScoreEntry best = null;
        float bestScore = float.NegativeInfinity;
        for (int i = 0; i < entries.Count; i++)
        {
            CombatAiObjectiveScoreEntry entry = entries[i];
            if (entry == null || entry.Breakdown == null) continue;
            if (entry.Breakdown.Total <= bestScore) continue;
            bestScore = entry.Breakdown.Total;
            best = entry;
        }

        return best;
    }

    private static CombatAiMoveCandidateEntry SelectHighest(List<CombatAiMoveCandidateEntry> entries)
    {
        CombatAiMoveCandidateEntry best = null;
        float bestScore = float.NegativeInfinity;
        for (int i = 0; i < entries.Count; i++)
        {
            CombatAiMoveCandidateEntry entry = entries[i];
            if (entry == null || entry.Breakdown == null) continue;
            if (entry.Breakdown.Total <= bestScore) continue;
            bestScore = entry.Breakdown.Total;
            best = entry;
        }

        return best;
    }

    private static CombatAiSkillCandidateEntry SelectHighest(List<CombatAiSkillCandidateEntry> entries)
    {
        CombatAiSkillCandidateEntry best = null;
        float bestScore = float.NegativeInfinity;
        for (int i = 0; i < entries.Count; i++)
        {
            CombatAiSkillCandidateEntry entry = entries[i];
            if (entry == null || entry.Breakdown == null) continue;
            if (entry.Breakdown.Total <= bestScore) continue;
            bestScore = entry.Breakdown.Total;
            best = entry;
        }

        return best;
    }

    private static void AddReason(CombatAiScoreBreakdown breakdown, CombatAiReasonCode reason)
    {
        if (!breakdown.ReasonCodes.Contains(reason))
        {
            breakdown.ReasonCodes.Add(reason);
        }
    }

    private static CombatMoveTarget CreateEnemyStoneTarget(CombatAiContext context)
    {
        return context.HasEnemyStonePosition ? CombatMoveTarget.ForPosition(context.EnemyStonePosition) : CombatMoveTarget.None;
    }

    private static CombatMoveTarget CreateOwnStoneTarget(CombatAiContext context)
    {
        return context.HasOwnStonePosition ? CombatMoveTarget.ForPosition(context.OwnStonePosition) : CombatMoveTarget.None;
    }

    private static CombatMoveTarget CreateBestEnemyTarget(CombatAiContext context, Character focusEnemy, float focusCommitmentRemainingSeconds)
    {
        Character enemy = FindBestEnemyCharacter(context, focusEnemy, focusCommitmentRemainingSeconds);
        if (enemy == null)
        {
            return CombatMoveTarget.None;
        }

        Character owner = context != null ? context.Owner : null;
        WeaponBase weapon = owner != null ? owner.EquippedWeapon : null;
        if (owner != null && weapon != null && weapon.Kind == WeaponKind.Wand)
        {
            return CreateWandAttackTarget(context, owner, enemy);
        }

        return CombatMoveTarget.ForCharacter(enemy);
    }

    private static CombatMoveTarget CreateBestAllyTarget(CombatAiContext context)
    {
        Character ally = FindBestAllyCharacter(context);
        if (ally == null)
        {
            return CombatMoveTarget.None;
        }

        Character owner = context != null ? context.Owner : null;
        WeaponBase weapon = owner != null ? owner.EquippedWeapon : null;
        if (owner != null && weapon != null && weapon.Kind == WeaponKind.Rosary)
        {
            return CreateRosarySupportTarget(context, owner, ally);
        }

        return CombatMoveTarget.ForCharacter(ally);
    }

    private static CombatMoveTarget CreateRosarySupportTarget(CombatAiContext context, Character owner, Character ally)
    {
        if (context == null || owner == null || ally == null)
        {
            return CombatMoveTarget.None;
        }

        CombatCharacterIntel allyIntel = FindAllyIntel(context, ally);
        if (allyIntel.Character == null || allyIntel.MaxHP <= 0)
        {
            return CombatMoveTarget.ForCharacter(ally);
        }

        int missingHp = Mathf.Max(0, allyIntel.MaxHP - allyIntel.HP);
        float currentDistance = HorizontalDistance(owner.transform.position, ally.transform.position);
        int currentHealAmount = EstimateCurrentRosaryHealAmount(owner, ally, currentDistance);
        float desiredDistance = missingHp > currentHealAmount
            ? RosaryCloseHealDistance
            : RosaryPreferredSupportDistance;
        CombatCharacterIntel nearestThreat = FindNearestKnownEnemyIntel(context, owner.transform.position);
        bool threatTooClose = nearestThreat.Character != null &&
            HorizontalDistance(owner.transform.position, nearestThreat.KnownPosition) < RosaryEnemyClearanceDistance;

        if (Mathf.Abs(currentDistance - desiredDistance) <= 0.75f && !threatTooClose)
        {
            return CombatMoveTarget.None;
        }

        Vector3 destination = ResolveSupportStandoffPosition(context, owner, ally, desiredDistance);
        return CombatMoveTarget.ForPosition(destination);
    }

    private static CombatMoveTarget CreateWandAttackTarget(CombatAiContext context, Character owner, Character enemy)
    {
        if (owner == null || enemy == null)
        {
            return CombatMoveTarget.None;
        }

        float desiredDistance = EstimatePreferredWandAttackDistance(owner);
        if (desiredDistance <= 0f)
        {
            return CombatMoveTarget.ForCharacter(enemy);
        }

        float currentDistance = HorizontalDistance(owner.transform.position, enemy.transform.position);
        float minimumHoldDistance = Mathf.Max(0f, desiredDistance - WandRangeSlack);
        float maximumHoldDistance = desiredDistance + WandRangeSlack;
        CombatCharacterIntel nearestThreat = FindNearestKnownEnemyIntel(context, owner.transform.position);
        bool threatTooClose = nearestThreat.Character != null &&
            HorizontalDistance(owner.transform.position, nearestThreat.KnownPosition) < WandEnemyClearanceDistance;
        if (currentDistance >= minimumHoldDistance && currentDistance <= maximumHoldDistance && !threatTooClose)
        {
            return CombatMoveTarget.None;
        }

        Character standoffEnemy = threatTooClose && nearestThreat.Character != null ? nearestThreat.Character : enemy;
        Vector3 destination = ResolveEnemyStandoffPosition(owner, standoffEnemy, desiredDistance);
        return CombatMoveTarget.ForPosition(destination);
    }

    private static CombatMoveTarget CreateLastKnownEnemyTarget(CombatAiContext context)
    {
        float bestScore = float.NegativeInfinity;
        Vector3 bestPosition = default;
        bool found = false;
        for (int i = 0; i < context.EnemyIntel.Count; i++)
        {
            CombatCharacterIntel enemy = context.EnemyIntel[i];
            if (!enemy.HasKnownPosition) continue;

            float score = enemy.HasDirectSight ? 1000f : 0f;
            score -= enemy.HasMemory ? enemy.MemoryAgeSeconds : 0f;
            score += enemy.MaxHP > 0 ? (1f - enemy.HP / (float)enemy.MaxHP) * 10f : 0f;
            score -= HorizontalDistance(context.Owner.transform.position, enemy.KnownPosition) * 0.05f;
            if (score <= bestScore) continue;

            bestScore = score;
            bestPosition = enemy.KnownPosition;
            found = true;
        }

        return found ? CombatMoveTarget.ForPosition(bestPosition) : CombatMoveTarget.None;
    }

    private static CombatMoveTarget CreateNearestPositionTarget(Character owner, IReadOnlyList<Vector3> positions)
    {
        if (owner == null || positions == null || positions.Count == 0) return CombatMoveTarget.None;

        const float minimumMeaningfulDistance = 2f;
        float bestDistance = float.PositiveInfinity;
        Vector3 best = default;
        bool found = false;
        for (int i = 0; i < positions.Count; i++)
        {
            float distance = HorizontalDistance(owner.transform.position, positions[i]);
            if (distance <= minimumMeaningfulDistance) continue;
            if (distance >= bestDistance) continue;
            bestDistance = distance;
            best = positions[i];
            found = true;
        }

        return found ? CombatMoveTarget.ForPosition(best) : CombatMoveTarget.None;
    }

    private static Character FindBestEnemyCharacter(CombatAiContext context, Character focusEnemy, float focusCommitmentRemainingSeconds)
    {
        Character focusCandidate = CombatAiFocusTargeting.IsValid(context, focusEnemy) ? focusEnemy : null;
        Character best = focusCandidate;
        float bestScore = focusCandidate != null
            ? ScoreEnemyTarget(context, focusCandidate) + CombatAiFocusTargeting.GetSelectionBonus(context, focusCandidate, focusCommitmentRemainingSeconds)
            : float.NegativeInfinity;

        for (int i = 0; i < context.EnemyIntel.Count; i++)
        {
            CombatCharacterIntel enemy = context.EnemyIntel[i];
            if (enemy.Character == null || !enemy.HasKnownPosition) continue;
            float score = ScoreEnemyTarget(context, enemy.Character);
            if (enemy.Character == focusCandidate)
            {
                score += CombatAiFocusTargeting.GetSelectionBonus(context, enemy.Character, focusCommitmentRemainingSeconds);
            }

            float requiredScore = bestScore;
            if (focusCandidate != null && enemy.Character != focusCandidate && focusCommitmentRemainingSeconds > 0f)
            {
                requiredScore += SwordRetargetThreshold;
            }

            if (score <= requiredScore) continue;
            bestScore = score;
            best = enemy.Character;
        }

        return best;
    }

    private static float ScoreEnemyTarget(CombatAiContext context, Character enemyCharacter)
    {
        CombatCharacterIntel enemy = FindEnemyIntel(context, enemyCharacter);
        if (enemy.Character == null || !enemy.HasKnownPosition) return float.NegativeInfinity;

        float hpRatio = enemy.MaxHP > 0 ? (float)enemy.HP / enemy.MaxHP : 1f;
        return (1f - hpRatio) * 60f + (enemy.HasDirectSight ? 25f : enemy.HasMemory ? 10f : 0f);
    }

    private static Character FindBestAllyCharacter(CombatAiContext context)
    {
        Character best = null;
        float bestScore = float.NegativeInfinity;
        for (int i = 0; i < context.AllyIntel.Count; i++)
        {
            CombatCharacterIntel ally = context.AllyIntel[i];
            if (ally.Character == null) continue;
            float hpRatio = ally.MaxHP > 0 ? (float)ally.HP / ally.MaxHP : 1f;
            float score = (1f - hpRatio) * 60f + (HasEnemyNearby(context.EnemyIntel, ally.CurrentPosition, 8f) ? 20f : 0f);
            if (score <= bestScore) continue;
            bestScore = score;
            best = ally.Character;
        }

        return best;
    }

    private static CombatCharacterIntel FindNearestKnownEnemyIntel(CombatAiContext context, Vector3 position)
    {
        if (context == null)
        {
            return default;
        }

        CombatCharacterIntel best = default;
        float bestDistance = float.PositiveInfinity;
        for (int i = 0; i < context.EnemyIntel.Count; i++)
        {
            CombatCharacterIntel enemy = context.EnemyIntel[i];
            if (enemy.Character == null || !enemy.HasKnownPosition || enemy.HP <= 0)
            {
                continue;
            }

            float distance = HorizontalDistance(position, enemy.KnownPosition);
            if (distance >= bestDistance)
            {
                continue;
            }

            bestDistance = distance;
            best = enemy;
        }

        return best;
    }

    private static int EstimateCurrentRosaryHealAmount(Character owner, Character ally, float distance)
    {
        if (owner == null || ally == null)
        {
            return 0;
        }

        IReadOnlyList<SkillBase> skills = owner.AvailableCombatSkills;
        CombatSkillCooldowns cooldowns = owner.SkillCooldowns;
        int bestHeal = 0;
        for (int i = 0; i < skills.Count; i++)
        {
            SkillBase skill = skills[i];
            if (skill == null || !CombatAiSkillClassifier.IsHeal(skill))
            {
                continue;
            }

            if (cooldowns != null && !cooldowns.IsReady(skill))
            {
                continue;
            }

            int estimate = EstimateRosaryHealAmount(owner, skill, distance);
            if (estimate > bestHeal)
            {
                bestHeal = estimate;
            }
        }

        return bestHeal;
    }

    private static float EstimatePreferredWandAttackDistance(Character owner)
    {
        if (owner == null)
        {
            return 0f;
        }

        IReadOnlyList<SkillBase> skills = owner.AvailableCombatSkills;
        CombatSkillCooldowns cooldowns = owner.SkillCooldowns;
        float bestRange = 0f;
        for (int i = 0; i < skills.Count; i++)
        {
            SkillBase skill = skills[i];
            if (skill == null || !CombatAiSkillClassifier.IsDamage(skill))
            {
                continue;
            }

            if (cooldowns != null && !cooldowns.IsReady(skill))
            {
                continue;
            }

            bestRange = Mathf.Max(bestRange, skill.MaxRange);
        }

        if (bestRange <= 0f)
        {
            WeaponBase weapon = owner.EquippedWeapon;
            bestRange = weapon != null ? weapon.Range : 0f;
        }

        return Mathf.Max(0f, bestRange - 1f);
    }

    private static int EstimateRosaryHealAmount(Character owner, SkillBase skill, float distance)
    {
        if (owner == null || skill == null)
        {
            return 0;
        }

        float fai = owner.GetEffectiveStat(CombatStat.FAI);
        if (skill is IdentifiedSkill identified)
        {
            switch (identified.SkillId)
            {
                case SkillId.Rosary_DistantHeal:
                {
                    if (distance > 9f)
                    {
                        return 0;
                    }

                    int baseHeal = Mathf.Max(1, Mathf.RoundToInt(fai * 0.45f));
                    return ComputeDistanceScaledAmount(baseHeal, distance, 9f, 1.5f, 0.8f);
                }
                case SkillId.Rosary_CloseHeal:
                    return distance <= 3f ? Mathf.Max(1, Mathf.RoundToInt(fai * 1.1f)) : 0;
                case SkillId.Rosary_Regeneration:
                    return distance <= 5f ? 25 : 0;
                case SkillId.Rosary_HealingArea:
                    return distance <= 7f ? 15 : 0;
                default:
                    return 0;
            }
        }

        return 0;
    }

    private static Vector3 ResolveSupportStandoffPosition(
        CombatAiContext context,
        Character owner,
        Character ally,
        float desiredDistance)
    {
        Vector3 allyPosition = ally.transform.position;
        Vector3 direction = Vector3.zero;
        float nearestEnemyDistance = float.PositiveInfinity;
        for (int i = 0; i < context.EnemyIntel.Count; i++)
        {
            CombatCharacterIntel enemy = context.EnemyIntel[i];
            if (!enemy.HasKnownPosition)
            {
                continue;
            }

            float distance = HorizontalDistance(enemy.KnownPosition, allyPosition);
            if (distance >= nearestEnemyDistance)
            {
                continue;
            }

            nearestEnemyDistance = distance;
            direction = Flatten(allyPosition - enemy.KnownPosition);
        }

        if (direction.sqrMagnitude <= 0.01f)
        {
            direction = Flatten(owner.transform.position - allyPosition);
        }

        if (direction.sqrMagnitude <= 0.01f)
        {
            direction = Vector3.back;
        }

        direction.Normalize();
        Vector3 destination = allyPosition + direction * desiredDistance;
        destination.y = owner.transform.position.y;
        return destination;
    }

    private static Vector3 ResolveEnemyStandoffPosition(Character owner, Character enemy, float desiredDistance)
    {
        Vector3 enemyPosition = enemy.transform.position;
        Vector3 direction = Flatten(owner.transform.position - enemyPosition);
        if (direction.sqrMagnitude <= 0.01f)
        {
            direction = Vector3.back;
        }

        direction.Normalize();
        Vector3 destination = enemyPosition + direction * desiredDistance;
        destination.y = owner.transform.position.y;
        return destination;
    }

    private static CombatCharacterIntel FindAllyIntel(CombatAiContext context, Character character)
    {
        for (int i = 0; i < context.AllyIntel.Count; i++)
        {
            if (context.AllyIntel[i].Character == character)
            {
                return context.AllyIntel[i];
            }
        }

        if (context.Owner == character && character != null && character.Health != null)
        {
            return new CombatCharacterIntel(
                character,
                character.Team,
                character.transform.position,
                hasDirectSight: true,
                hasMemory: false,
                hasKnownPosition: true,
                knownPosition: character.transform.position,
                hasLastKnownPosition: false,
                lastKnownPosition: default,
                memoryAgeSeconds: 0f,
                recognizesOwner: false,
                hp: character.Health.HP,
                maxHp: character.Health.MaxHP,
                canAct: character.Health.CanAct,
                weaponKind: character.EquippedWeapon != null ? character.EquippedWeapon.Kind : WeaponKind.Unarmed,
                weaponRange: character.EquippedWeapon != null ? character.EquippedWeapon.Range : WeaponBase.Unarmed.Range,
                statusEffects: character.StatusEffects != null
                    ? character.StatusEffects.GetActiveEffectSnapshots()
                    : System.Array.Empty<CombatStatusEffectSnapshot>(),
                hasObjective: false,
                objective: default);
        }

        return default;
    }

    private static CombatCharacterIntel FindEnemyIntel(CombatAiContext context, Character character)
    {
        for (int i = 0; i < context.EnemyIntel.Count; i++)
        {
            if (context.EnemyIntel[i].Character == character)
            {
                return context.EnemyIntel[i];
            }
        }

        return default;
    }

    private static bool HasEnemyNearby(IReadOnlyList<CombatCharacterIntel> enemies, Vector3 position, float radius)
    {
        for (int i = 0; i < enemies.Count; i++)
        {
            if (!enemies[i].HasKnownPosition) continue;
            if (HorizontalDistance(position, enemies[i].KnownPosition) <= radius) return true;
        }

        return false;
    }

    private static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    private static int ComputeDistanceScaledAmount(
        int baseAmount,
        float distance,
        float maxRange,
        float nearMultiplier,
        float farMultiplier)
    {
        if (baseAmount <= 0) return 1;
        if (maxRange <= 0f) return Mathf.Max(1, baseAmount);

        float t = Mathf.Clamp01(distance / maxRange);
        float multiplier = Mathf.Lerp(nearMultiplier, farMultiplier, t);
        return Mathf.Max(1, Mathf.RoundToInt(baseAmount * multiplier));
    }

    private static Vector3 Flatten(Vector3 value)
    {
        value.y = 0f;
        return value;
    }

    private static float GetObjectiveSkillAlignmentScore(
        SkillBase skill,
        CombatSkillEvaluationResult evaluation,
        CombatAiAssessment assessment,
        CombatAiContext context,
        CombatObjective objective)
    {
        if (!evaluation.CanUse) return 0f;

        return objective switch
        {
            CombatObjective.AttackEnemy when CombatAiSkillClassifier.IsDamage(skill) => 18f,
            CombatObjective.AttackEnemy when CombatAiSkillClassifier.IsDebuff(skill) => 14f,
            CombatObjective.SupportAlly when CombatAiSkillClassifier.IsHeal(skill) => 24f,
            CombatObjective.SupportAlly when CombatAiSkillClassifier.IsBuff(skill) || CombatAiSkillClassifier.IsProtect(skill) => 18f,
            CombatObjective.DefendOwnStone when CombatAiSkillClassifier.IsProtect(skill) => 20f,
            CombatObjective.DefendOwnStone when CombatAiSkillClassifier.IsDamage(skill) => assessment.GetValue("OwnStoneThreat") * 0.2f,
            CombatObjective.Retreat when CombatAiSkillClassifier.IsHeal(skill) || CombatAiSkillClassifier.IsProtect(skill) || CombatAiSkillClassifier.IsStealth(skill) => 20f,
            CombatObjective.Search when CombatAiSkillClassifier.IsStealth(skill) || CombatAiSkillClassifier.IsMobility(skill) => 16f,
            CombatObjective.DestroyEnemyStone when CombatAiSkillClassifier.IsSupport(skill) && assessment.GetValue("AllyFragility") < 20f => -20f,
            CombatObjective.DestroyEnemyStone when CombatAiSkillClassifier.IsDamage(skill) && context.VisibleEnemies.Count > 0 => 6f,
            _ => 0f,
        };
    }

    private static float GetSupportTargetAffinityScore(SkillBase skill, CombatCharacterIntel ally)
    {
        if (skill is not IdentifiedSkill identified) return 0f;

        return identified.SkillId switch
        {
            SkillId.Bible_StrBuff when ally.WeaponKind == WeaponKind.Sword || ally.WeaponKind == WeaponKind.Shield => 28f,
            SkillId.Bible_IntBuff when ally.WeaponKind == WeaponKind.Wand || ally.WeaponKind == WeaponKind.Grimoire => 28f,
            SkillId.Bible_FaiBuff when ally.WeaponKind == WeaponKind.Bible || ally.WeaponKind == WeaponKind.Rosary => 28f,
            SkillId.Bible_AgiBuff when ally.WeaponKind == WeaponKind.Sword => 14f,
            SkillId.Shield_ShoulderGuard when ally.WeaponKind != WeaponKind.Shield => 10f,
            SkillId.Bible_Gotsume when ally.WeaponKind == WeaponKind.Sword || ally.WeaponKind == WeaponKind.Shield => 14f,
            _ => 0f,
        };
    }

    private static float GetDebuffTargetAffinityScore(SkillBase skill, CombatCharacterIntel enemy)
    {
        if (skill is not IdentifiedSkill identified) return 0f;

        return identified.SkillId switch
        {
            SkillId.Grimoire_StrDebuff when enemy.WeaponKind == WeaponKind.Sword || enemy.WeaponKind == WeaponKind.Shield => 28f,
            SkillId.StatDebuff_INT when enemy.WeaponKind == WeaponKind.Wand || enemy.WeaponKind == WeaponKind.Grimoire => 28f,
            SkillId.StatDebuff_FAI when enemy.WeaponKind == WeaponKind.Bible || enemy.WeaponKind == WeaponKind.Rosary => 28f,
            SkillId.StatDebuff_AGI when enemy.WeaponKind == WeaponKind.Sword => 16f,
            SkillId.Grimoire_Bind when enemy.WeaponKind == WeaponKind.Sword || enemy.WeaponKind == WeaponKind.Shield => 20f,
            SkillId.Grimoire_Poison when enemy.MaxHP > 0 && enemy.HP / (float)enemy.MaxHP > 0.35f => 14f,
            _ => 0f,
        };
    }

    private static bool HasEquivalentEffect(SkillBase skill, IReadOnlyList<CombatStatusEffectSnapshot> effects)
    {
        if (skill is not IdentifiedSkill identified || effects == null) return false;

        for (int i = 0; i < effects.Count; i++)
        {
            CombatStatusEffectSnapshot effect = effects[i];
            if (MatchesEffect(identified.SkillId, effect))
            {
                return true;
            }
        }

        return false;
    }

    private static bool MatchesEffect(SkillId skillId, CombatStatusEffectSnapshot effect)
    {
        return skillId switch
        {
            SkillId.Bible_StrBuff => effect.IsBuff && effect.Stat == CombatStatusEffects.StatKind.STR,
            SkillId.Bible_IntBuff => effect.IsBuff && effect.Stat == CombatStatusEffects.StatKind.INT,
            SkillId.Bible_FaiBuff => effect.IsBuff && effect.Stat == CombatStatusEffects.StatKind.FAI,
            SkillId.Bible_AgiBuff => effect.IsBuff && effect.Stat == CombatStatusEffects.StatKind.AGI,
            SkillId.Grimoire_StrDebuff => effect.IsDebuff && effect.Stat == CombatStatusEffects.StatKind.STR,
            SkillId.StatDebuff_INT => effect.IsDebuff && effect.Stat == CombatStatusEffects.StatKind.INT,
            SkillId.StatDebuff_FAI => effect.IsDebuff && effect.Stat == CombatStatusEffects.StatKind.FAI,
            SkillId.StatDebuff_AGI => effect.IsDebuff && effect.Stat == CombatStatusEffects.StatKind.AGI,
            SkillId.Grimoire_Bind => effect.Type == CombatStatusEffects.EffectType.Bind,
            SkillId.Grimoire_Poison => effect.Type == CombatStatusEffects.EffectType.Poison,
            SkillId.Grimoire_Stealth => effect.Type == CombatStatusEffects.EffectType.Stealth,
            SkillId.Bible_Invulnerable => effect.Type == CombatStatusEffects.EffectType.Invulnerable,
            SkillId.Rosary_Regeneration => effect.Type == CombatStatusEffects.EffectType.HealOverTime,
            _ => false,
        };
    }

}

public static class CombatAiSkillClassifier
{
    public static bool IsBasicAttack(SkillBase skill)
    {
        return skill != null && skill.Name == "通常攻撃";
    }

    public static bool IsDamage(SkillBase skill)
    {
        if (skill == null) return false;
        string code = GetCode(skill);
        return IsBasicAttack(skill)
            || code.Contains("Bolt")
            || code.Contains("Blast")
            || code.Contains("Slash")
            || code.Contains("Smite")
            || code.Contains("Strike")
            || code.Contains("Thunder");
    }

    public static bool IsBuff(SkillBase skill)
    {
        if (skill == null) return false;
        string code = GetCode(skill);
        return code.Contains("Buff") || code.Contains("Invulnerable") || code.Contains("Gotsume");
    }

    public static bool IsDebuff(SkillBase skill)
    {
        if (skill == null) return false;
        string code = GetCode(skill);
        return code.Contains("Debuff") || code.Contains("Poison") || code.Contains("Bind");
    }

    public static bool IsHeal(SkillBase skill)
    {
        if (skill == null) return false;
        string code = GetCode(skill);
        return code.Contains("Heal") || code.Contains("Regeneration") || code.Contains("HealingArea");
    }

    public static bool IsProtect(SkillBase skill)
    {
        if (skill == null) return false;
        string code = GetCode(skill);
        return code.Contains("Invulnerable") || code.Contains("Gotsume") || code.Contains("ShoulderGuard");
    }

    public static bool IsMobility(SkillBase skill)
    {
        return GetCode(skill).Contains("CarryRush");
    }

    public static bool IsStealth(SkillBase skill)
    {
        return GetCode(skill).Contains("Stealth");
    }

    public static bool IsSupport(SkillBase skill)
    {
        return IsBuff(skill) || IsHeal(skill) || IsProtect(skill);
    }

    private static string GetCode(SkillBase skill)
    {
        if (skill == null) return string.Empty;
        return skill is IdentifiedSkill identified ? identified.SkillId.ToString() : skill.GetType().Name;
    }
}
