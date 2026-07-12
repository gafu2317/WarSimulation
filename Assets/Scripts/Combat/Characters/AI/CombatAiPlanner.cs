using System.Collections.Generic;
using UnityEngine;

public static partial class CombatAiPlanner
{
    private const float UnselectableScore = -100000f;
    private const float RosaryPreferredSupportDistance = 5.5f;
    private const float RosaryCloseHealDistance = 2.5f;
    private const float RosaryEnemyClearanceDistance = 6.5f;
    private const float WandRangeSlack = 0.75f;
    private const float WandEnemyClearanceDistance = 7f;
    private const float SwordRetargetThreshold = 20f;

    private static readonly List<SkillExecutionContext> s_skillContextsBuffer = new List<SkillExecutionContext>();

    public static CombatAiPlan BuildPlan(
        CombatAiContext context,
        CombatAiPersonalityProfile personalityProfile,
        CombatAiWeaponWeightsProfile weaponWeightsProfile = null,
        Character focusEnemy = null,
        float focusCommitmentRemainingSeconds = 0f,
        CombatObjective previousObjective = CombatObjective.Search)
    {
        if (context == null || context.Owner == null) return CombatAiPlan.None;
        return BuildPlanDirect(context, personalityProfile, weaponWeightsProfile, focusEnemy, focusCommitmentRemainingSeconds, previousObjective);
    }

    private static CombatAiPlan BuildPlanDirect(
        CombatAiContext context,
        CombatAiPersonalityProfile personalityProfile,
        CombatAiWeaponWeightsProfile weaponWeightsProfile,
        Character focusEnemy,
        float focusCommitmentRemainingSeconds,
        CombatObjective previousObjective)
    {
        CombatAiAssessment assessment = CombatAiAssessmentBuilder.Build(context, captureDebug: false);

        CombatObjective objective = CombatAiObjectiveScorer.SelectBestObjective(
            context, assessment, personalityProfile, weaponWeightsProfile,
            focusEnemy, focusCommitmentRemainingSeconds, previousObjective);

        CombatMoveTarget moveTarget = SelectBestMoveDirect(
            context, assessment, personalityProfile, weaponWeightsProfile,
            objective, focusEnemy, focusCommitmentRemainingSeconds);

        SelectBestSkillDirect(
            context, assessment, personalityProfile, weaponWeightsProfile,
            objective, focusEnemy, focusCommitmentRemainingSeconds,
            out SkillBase bestSkill, out SkillExecutionContext bestSkillContext);

        return new CombatAiPlan(objective, moveTarget, bestSkill, bestSkillContext);
    }

    private static CombatMoveTarget SelectBestMoveDirect(
        CombatAiContext context,
        CombatAiAssessment assessment,
        CombatAiPersonalityProfile personalityProfile,
        CombatAiWeaponWeightsProfile weaponWeightsProfile,
        CombatObjective objective,
        Character focusEnemy,
        float focusCommitmentRemainingSeconds)
    {
        Character owner = context.Owner;
        CombatMoveTarget bestTarget = CombatMoveTarget.None;
        float bestScore = float.NegativeInfinity;

        TryScoreMoveDirectCandidate(owner, context, assessment, personalityProfile, weaponWeightsProfile,
            CombatAiMoveCode.AdvanceEnemyStone, CreateEnemyStoneTarget(context), objective, focusEnemy, focusCommitmentRemainingSeconds, ref bestScore, ref bestTarget);
        for (int i = 0; i < context.BridgePositions.Count; i++)
        {
            TryScoreMoveDirectCandidate(owner, context, assessment, personalityProfile, weaponWeightsProfile,
                CombatAiMoveCode.AdvanceViaBridge, CreateBridgeWaypointTarget(context, context.BridgePositions[i]), objective,
                focusEnemy, focusCommitmentRemainingSeconds, ref bestScore, ref bestTarget);
        }
        TryScoreMoveDirectCandidate(owner, context, assessment, personalityProfile, weaponWeightsProfile,
            CombatAiMoveCode.ReturnOwnStone, CreateOwnStoneTarget(context), objective, focusEnemy, focusCommitmentRemainingSeconds, ref bestScore, ref bestTarget);
        TryScoreMoveDirectCandidate(owner, context, assessment, personalityProfile, weaponWeightsProfile,
            CombatAiMoveCode.PursueEnemy, CreateBestEnemyTarget(context, focusEnemy, focusCommitmentRemainingSeconds), objective, focusEnemy, focusCommitmentRemainingSeconds, ref bestScore, ref bestTarget);
        TryScoreMoveDirectCandidate(owner, context, assessment, personalityProfile, weaponWeightsProfile,
            CombatAiMoveCode.SupportAlly, CreateBestAllyTarget(context), objective, focusEnemy, focusCommitmentRemainingSeconds, ref bestScore, ref bestTarget);
        TryScoreMoveDirectCandidate(owner, context, assessment, personalityProfile, weaponWeightsProfile,
            CombatAiMoveCode.InterceptThreat, CreateBestBodyBlockTarget(context), objective, focusEnemy, focusCommitmentRemainingSeconds, ref bestScore, ref bestTarget);
        TryScoreMoveDirectCandidate(owner, context, assessment, personalityProfile, weaponWeightsProfile,
            CombatAiMoveCode.TakeHighGround, CreateNearestPositionTarget(owner, context.HighGroundCandidates), objective, focusEnemy, focusCommitmentRemainingSeconds, ref bestScore, ref bestTarget);
        {
            WeaponKind ownerWeaponKind = owner.EquippedWeapon != null ? owner.EquippedWeapon.Kind : WeaponKind.Unarmed;
            CombatMoveTarget forestTarget = IsRangedOrSupportWeapon(ownerWeaponKind)
                ? CreateCoverPositionTarget(context, owner)
                : CreateNearestPositionTarget(owner, context.ForestCandidates);
            TryScoreMoveDirectCandidate(owner, context, assessment, personalityProfile, weaponWeightsProfile,
                CombatAiMoveCode.MoveForest, forestTarget, objective, focusEnemy, focusCommitmentRemainingSeconds, ref bestScore, ref bestTarget);
        }
        TryScoreMoveDirectCandidate(owner, context, assessment, personalityProfile, weaponWeightsProfile,
            CombatAiMoveCode.SearchLastKnown, CreateLastKnownEnemyTarget(context), objective, focusEnemy, focusCommitmentRemainingSeconds, ref bestScore, ref bestTarget);
        TryScoreMoveDirectCandidate(owner, context, assessment, personalityProfile, weaponWeightsProfile,
            CombatAiMoveCode.HoldPosition, CombatMoveTarget.None, objective, focusEnemy, focusCommitmentRemainingSeconds, ref bestScore, ref bestTarget);

        return bestTarget;
    }

    private static void TryScoreMoveDirectCandidate(
        Character owner,
        CombatAiContext context,
        CombatAiAssessment assessment,
        CombatAiPersonalityProfile personalityProfile,
        CombatAiWeaponWeightsProfile weaponWeightsProfile,
        string code,
        CombatMoveTarget target,
        CombatObjective objective,
        Character focusEnemy,
        float focusCommitmentRemainingSeconds,
        ref float bestScore,
        ref CombatMoveTarget bestTarget)
    {
        if (code != CombatAiMoveCode.HoldPosition && !target.HasDestination) return;
        float score = CombatAiMoveScorer.ScoreDirect(owner, context, assessment, personalityProfile, weaponWeightsProfile,
            code, target, objective, focusEnemy, focusCommitmentRemainingSeconds);
        if (score > bestScore)
        {
            bestScore = score;
            bestTarget = target;
        }
    }

    private static void SelectBestSkillDirect(
        CombatAiContext context,
        CombatAiAssessment assessment,
        CombatAiPersonalityProfile personalityProfile,
        CombatAiWeaponWeightsProfile weaponWeightsProfile,
        CombatObjective objective,
        Character focusEnemy,
        float focusCommitmentRemainingSeconds,
        out SkillBase bestSkill,
        out SkillExecutionContext bestSkillContext)
    {
        bestSkill = null;
        bestSkillContext = SkillExecutionContext.None;
        IReadOnlyList<SkillBase> skills = context.Owner.AvailableCombatSkills;
        float waitBaseScore = skills.Count == 0 ? 12f : 3f;
        float waitSituationScore = objective == CombatObjective.Retreat ? 8f : 0f;
        float bestScore = waitBaseScore + waitSituationScore;

        for (int i = 0; i < skills.Count; i++)
        {
            SkillBase skill = skills[i];
            if (skill == null) continue;

            CombatAiSkillContextBuilder.Build(context, context.Owner, skill, s_skillContextsBuffer);
            for (int j = 0; j < s_skillContextsBuffer.Count; j++)
            {
                CombatSkillEvaluationResult evaluation = CombatSkillEvaluator.Evaluate(context.Owner, skill, s_skillContextsBuffer[j]);
                float score = GetSkillBaseScore(skill, objective)
                    + GetSkillWeaponScore(weaponWeightsProfile, context.Owner.EquippedWeapon, skill)
                    + GetSkillPersonalityScore(personalityProfile, skill, objective)
                    + GetSkillSituationScore(context, assessment, skill, evaluation, objective)
                    + CombatAiFocusTargeting.GetSkillScore(context, context.Owner.EquippedWeapon, skill, evaluation, focusEnemy, focusCommitmentRemainingSeconds);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestSkill = skill;
                    bestSkillContext = evaluation.Context;
                }
            }
        }
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
            Assessment = CombatAiAssessmentBuilder.Build(context, captureDebug: true),
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
        for (int i = 0; i < snapshot.Context.BridgePositions.Count; i++)
        {
            AddMoveCandidate(
                snapshot,
                personalityProfile,
                weaponWeightsProfile,
                CombatAiMoveCode.AdvanceViaBridge,
                "橋を経由して敵魔石へ前進",
                CreateBridgeWaypointTarget(snapshot.Context, snapshot.Context.BridgePositions[i]),
                objective,
                focusEnemy,
                focusCommitmentRemainingSeconds);
        }
        AddMoveCandidate(snapshot, personalityProfile, weaponWeightsProfile, CombatAiMoveCode.ReturnOwnStone, "自軍魔石へ戻る", CreateOwnStoneTarget(snapshot.Context), objective, focusEnemy, focusCommitmentRemainingSeconds);
        AddMoveCandidate(snapshot, personalityProfile, weaponWeightsProfile, CombatAiMoveCode.PursueEnemy, "敵へ接近", CreateBestEnemyTarget(snapshot.Context, focusEnemy, focusCommitmentRemainingSeconds), objective, focusEnemy, focusCommitmentRemainingSeconds);
        AddMoveCandidate(snapshot, personalityProfile, weaponWeightsProfile, CombatAiMoveCode.SupportAlly, "味方へ接近", CreateBestAllyTarget(snapshot.Context), objective, focusEnemy, focusCommitmentRemainingSeconds);
        AddMoveCandidate(snapshot, personalityProfile, weaponWeightsProfile, CombatAiMoveCode.InterceptThreat, "敵の進路を遮断", CreateBestBodyBlockTarget(snapshot.Context), objective, focusEnemy, focusCommitmentRemainingSeconds);
        AddMoveCandidate(snapshot, personalityProfile, weaponWeightsProfile, CombatAiMoveCode.TakeHighGround, "高所へ移動", CreateNearestPositionTarget(snapshot.Owner, snapshot.Context.HighGroundCandidates), objective, focusEnemy, focusCommitmentRemainingSeconds);
        {
            WeaponKind ownerWeaponKind = snapshot.Owner.EquippedWeapon != null ? snapshot.Owner.EquippedWeapon.Kind : WeaponKind.Unarmed;
            CombatMoveTarget forestTarget = IsRangedOrSupportWeapon(ownerWeaponKind)
                ? CreateCoverPositionTarget(snapshot.Context, snapshot.Owner)
                : CreateNearestPositionTarget(snapshot.Owner, snapshot.Context.ForestCandidates);
            AddMoveCandidate(snapshot, personalityProfile, weaponWeightsProfile, CombatAiMoveCode.MoveForest, "森へ移動", forestTarget, objective, focusEnemy, focusCommitmentRemainingSeconds);
        }
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
            score += assessment.GetValue(CombatAiMetricIndex.AllyFragility) * 0.25f;
        }

        if (CombatAiSkillClassifier.IsDamage(skill) || CombatAiSkillClassifier.IsDebuff(skill))
        {
            score += assessment.GetValue(CombatAiMetricIndex.ReachableEnemyValue) * 0.25f;
        }

        if (CombatAiSkillClassifier.IsDamage(skill))
        {
            score += assessment.GetValue(CombatAiMetricIndex.KillableTargetValue) * 0.15f;
        }

        score += GetObjectiveSkillAlignmentScore(skill, evaluation, assessment, context, objective);
        score -= GetCastRiskPenalty(context, skill.CastTimeSeconds);

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
            if (CombatAiSkillClassifier.IsSupport(skill) && skillContext.HasTargetPoint && skill.AreaRadius > 0f)
            {
                return GetAreaSupportTargetScore(context, skill, skillContext);
            }

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
            if (CombatAiSkillClassifier.IsHeal(skill))
            {
                int missingHp = Mathf.Max(0, ally.MaxHP - ally.HP);
                int healing = skill.EstimateHealing(context.Owner, skillContext, ally.Character);
                score += ally.MaxHP > 0 ? Mathf.Min(missingHp, healing) / (float)ally.MaxHP * 40f : 0f;
                score += GetPostHealSurvivalScore(context, ally, healing);
            }
            if (HasEnemyNearby(context.EnemyIntel, ally.CurrentPosition, 8f))
            {
                score += CombatAiSkillClassifier.IsProtect(skill) ? 24f : 8f;
            }

            score += GetSupportTargetAffinityScore(skill, ally);
            score -= GetStatusRedundancyPenalty(skill, ally.StatusEffects);

            return score;
        }

        if (CombatAiSkillClassifier.IsDamage(skill) || CombatAiSkillClassifier.IsDebuff(skill))
        {
            if (CombatAiSkillClassifier.IsDamage(skill))
            {
                return GetDamageSkillTargetScore(context, skill, skillContext);
            }

            CombatCharacterIntel enemy = FindEnemyIntel(context, skillContext.PrimaryTarget);
            if (enemy.Character == null) return 0f;
            float hpRatio = enemy.MaxHP > 0 ? (float)enemy.HP / enemy.MaxHP : 1f;
            float score = (1f - hpRatio) * 18f + (enemy.HasDirectSight ? 8f : enemy.HasMemory ? 3f : 0f);
            score += GetDebuffTargetAffinityScore(skill, enemy);
            score -= GetStatusRedundancyPenalty(skill, enemy.StatusEffects);

            return score;
        }

        return 0f;
    }

    private static float GetAreaSupportTargetScore(
        CombatAiContext context,
        SkillBase skill,
        SkillExecutionContext skillContext)
    {
        float score = 0f;
        for (int i = 0; i < context.AllyIntel.Count; i++)
        {
            CombatCharacterIntel ally = context.AllyIntel[i];
            if (ally.Character == null || ally.MaxHP <= 0) continue;
            if (HorizontalDistance(skillContext.TargetPoint, ally.CurrentPosition) > skill.AreaRadius) continue;
            int missingHp = Mathf.Max(0, ally.MaxHP - ally.HP);
            int healing = skill.EstimateHealing(context.Owner, skillContext, ally.Character);
            score += missingHp / (float)ally.MaxHP * 28f;
            score += Mathf.Min(missingHp, healing) / (float)ally.MaxHP * 36f;
            score += GetPostHealSurvivalScore(context, ally, healing);
        }

        Character owner = context.Owner;
        if (owner != null && owner.Health != null && owner.MaxHP > 0 &&
            HorizontalDistance(skillContext.TargetPoint, owner.transform.position) <= skill.AreaRadius)
        {
            int missingHp = Mathf.Max(0, owner.MaxHP - owner.Health.HP);
            int healing = skill.EstimateHealing(owner, skillContext, owner);
            score += missingHp / (float)owner.MaxHP * 28f;
            score += Mathf.Min(missingHp, healing) / (float)owner.MaxHP * 36f;
        }

        return score;
    }

    private static float GetPostHealSurvivalScore(CombatAiContext context, CombatCharacterIntel ally, int healing)
    {
        int incomingDamage = 0;
        for (int i = 0; i < context.EnemyPendingDamage.Count; i++)
        {
            CombatAiPendingDamage pending = context.EnemyPendingDamage[i];
            if (pending.Target == ally.Character)
            {
                incomingDamage += pending.Damage;
            }
        }

        if (incomingDamage <= 0) return 0f;
        int projectedHp = Mathf.Min(ally.MaxHP, ally.HP + healing) - incomingDamage;
        if (projectedHp <= 0) return -24f;
        return projectedHp <= ally.MaxHP * 0.3f ? 8f : 20f;
    }

    private static float GetDamageSkillTargetScore(CombatAiContext context, SkillBase skill, SkillExecutionContext skillContext)
    {
        if (context == null || context.Owner == null || skill == null)
        {
            return 0f;
        }

        SkillExecutionContext capturedContext = skillContext.Capture(context.Owner);
        float score = 0f;
        bool foundCharacterTarget = false;
        for (int i = 0; i < capturedContext.ResolvedTargets.Count; i++)
        {
            CombatCharacterIntel enemy = FindEnemyIntel(context, capturedContext.ResolvedTargets[i]);
            if (enemy.Character == null || enemy.HP <= 0) continue;

            int predictedDamage = skill.EstimateDamage(context.Owner, capturedContext, enemy.Character);
            if (predictedDamage <= 0) continue;

            foundCharacterTarget = true;
            int pendingDamage = GetAllyPendingDamage(context, enemy.Character);
            score += GetDamageAgainstEnemyScore(skill, enemy, predictedDamage, pendingDamage);
        }

        if (!foundCharacterTarget && capturedContext.PrimaryStone != null)
        {
            score += 24f;
        }

        if (skill.SelfHpCost > 0)
        {
            score -= GetSelfHpCostPenalty(context, skill.SelfHpCost);
        }

        score -= Mathf.Clamp(skill.CooldownSeconds, 0f, 10f) * 0.7f;
        if (skill.CastTimeSeconds > 0f)
        {
            score -= Mathf.Clamp(skill.CastTimeSeconds, 0f, 3f) * 1.5f;
        }

        if (capturedContext.ResolvedTargets.Count >= 2)
        {
            score += Mathf.Min(18f, (capturedContext.ResolvedTargets.Count - 1) * 6f);
        }

        return score;
    }

    private static float GetDamageAgainstEnemyScore(
        SkillBase skill,
        CombatCharacterIntel enemy,
        int predictedDamage,
        int pendingDamage)
    {
        int hp = Mathf.Max(0, enemy.HP - pendingDamage);
        if (hp <= 0) return -80f;
        int maxHp = Mathf.Max(hp, enemy.MaxHP);
        int effectiveDamage = Mathf.Min(predictedDamage, hp);
        int overkillDamage = Mathf.Max(0, predictedDamage - hp);
        float hpRatio = maxHp > 0 ? hp / (float)maxHp : 1f;
        float effectiveDamageRatio = maxHp > 0 ? effectiveDamage / (float)maxHp : 0f;
        float targetValue = (1f - hpRatio) * 14f
            + GetEnemyRoleTargetValue(enemy.WeaponKind)
            + (enemy.HasDirectSight ? 8f : enemy.HasMemory ? 3f : 0f);

        float score = targetValue + effectiveDamageRatio * 44f;
        if (predictedDamage >= hp)
        {
            score += 28f;
            if (CombatAiSkillClassifier.IsBasicAttack(skill))
            {
                score += 8f;
            }
        }

        score -= Mathf.Clamp01(overkillDamage / (float)maxHp) * 18f;
        return score;
    }

    private static float GetEnemyRoleTargetValue(WeaponKind weaponKind)
    {
        return weaponKind switch
        {
            WeaponKind.Sword => 12f,
            WeaponKind.Wand => 12f,
            WeaponKind.Grimoire => 9f,
            WeaponKind.Shield => 6f,
            WeaponKind.Bible => 4f,
            WeaponKind.Rosary => 3f,
            _ => 2f,
        };
    }

    private static int GetAllyPendingDamage(CombatAiContext context, Character target)
    {
        int damage = 0;
        for (int i = 0; i < context.AllyPendingDamage.Count; i++)
        {
            CombatAiPendingDamage pending = context.AllyPendingDamage[i];
            if (pending.Target == target)
            {
                damage += pending.Damage;
            }
        }

        return damage;
    }

    private static float GetSelfHpCostPenalty(CombatAiContext context, int hpCost)
    {
        Character owner = context != null ? context.Owner : null;
        if (owner == null || owner.Health == null || owner.MaxHP <= 0)
        {
            return hpCost;
        }

        float costRatio = hpCost / (float)owner.MaxHP;
        float remainingRatio = (owner.Health.HP - hpCost) / (float)owner.MaxHP;
        float penalty = costRatio * 45f;
        if (remainingRatio <= 0f)
        {
            penalty += 100f;
        }
        else if (remainingRatio < 0.25f)
        {
            penalty += (0.25f - remainingRatio) * 80f;
        }

        int remainingHp = owner.Health.HP - hpCost;
        for (int i = 0; i < context.EnemyIntel.Count; i++)
        {
            CombatCharacterIntel enemy = context.EnemyIntel[i];
            if (!enemy.HasKnownPosition || !enemy.CanAct) continue;
            float distance = HorizontalDistance(owner.transform.position, enemy.KnownPosition);
            if (distance > enemy.WeaponRange + enemy.MoveSpeed * 1.5f) continue;

            penalty += remainingHp <= owner.MaxHP * 0.4f ? 18f : 6f;
        }

        return penalty;
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
            CombatObjective.DefendOwnStone when CombatAiSkillClassifier.IsDamage(skill) => assessment.GetValue(CombatAiMetricIndex.OwnStoneThreat) * 0.2f,
            CombatObjective.Retreat when CombatAiSkillClassifier.IsHeal(skill) || CombatAiSkillClassifier.IsProtect(skill) || CombatAiSkillClassifier.IsStealth(skill) => 20f,
            CombatObjective.Search when CombatAiSkillClassifier.IsStealth(skill) || CombatAiSkillClassifier.IsMobility(skill) => 16f,
            CombatObjective.DestroyEnemyStone when CombatAiSkillClassifier.IsSupport(skill) && assessment.GetValue(CombatAiMetricIndex.AllyFragility) < 20f => -20f,
            CombatObjective.DestroyEnemyStone when CombatAiSkillClassifier.IsDamage(skill) && context.VisibleEnemies.Count > 0 => 6f,
            _ => 0f,
        };
    }

    private static float GetSupportTargetAffinityScore(SkillBase skill, CombatCharacterIntel ally)
    {
        if (skill is not IdentifiedSkill identified) return 0f;

        float score = identified.SkillId switch
        {
            SkillId.Bible_StrBuff when ally.WeaponKind == WeaponKind.Sword || ally.WeaponKind == WeaponKind.Shield => 28f,
            SkillId.Bible_IntBuff when ally.WeaponKind == WeaponKind.Wand || ally.WeaponKind == WeaponKind.Grimoire => 28f,
            SkillId.Bible_FaiBuff when ally.WeaponKind == WeaponKind.Bible || ally.WeaponKind == WeaponKind.Rosary => 28f,
            SkillId.Bible_AgiBuff when ally.WeaponKind == WeaponKind.Sword => 14f,
            SkillId.Shield_ShoulderGuard when ally.WeaponKind != WeaponKind.Shield => 10f,
            SkillId.Bible_Gotsume when ally.WeaponKind == WeaponKind.Sword || ally.WeaponKind == WeaponKind.Shield => 14f,
            _ => 0f,
        };

        if (ally.HasObjective && ally.Objective == CombatObjective.AttackEnemy &&
            (ally.WeaponKind == WeaponKind.Sword || ally.WeaponKind == WeaponKind.Shield))
        {
            score += 10f;
        }

        return score;
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

    private static float GetCastRiskPenalty(CombatAiContext context, float castTimeSeconds)
    {
        if (context == null || context.Owner == null || castTimeSeconds <= 0f) return 0f;

        float shortestThreatTime = float.PositiveInfinity;
        Vector3 ownerPosition = context.Owner.transform.position;
        for (int i = 0; i < context.EnemyIntel.Count; i++)
        {
            CombatCharacterIntel enemy = context.EnemyIntel[i];
            if (!enemy.HasDirectSight || !enemy.CanAct) continue;

            float distance = HorizontalDistance(ownerPosition, enemy.CurrentPosition);
            float distanceToThreat = Mathf.Max(0f, distance - Mathf.Max(0.5f, enemy.WeaponRange));
            shortestThreatTime = Mathf.Min(shortestThreatTime, distanceToThreat / enemy.MoveSpeed);
        }

        if (float.IsPositiveInfinity(shortestThreatTime) || shortestThreatTime >= castTimeSeconds)
        {
            return 0f;
        }

        float risk = 1f - shortestThreatTime / castTimeSeconds;
        return Mathf.Lerp(12f, 48f, Mathf.Clamp01(risk));
    }

    private static float GetStatusRedundancyPenalty(SkillBase skill, IReadOnlyList<CombatStatusEffectSnapshot> effects)
    {
        if (skill is not IdentifiedSkill identified || effects == null) return 0f;

        float longestRemainingSeconds = 0f;
        for (int i = 0; i < effects.Count; i++)
        {
            CombatStatusEffectSnapshot effect = effects[i];
            if (MatchesEffect(identified.SkillId, effect))
            {
                longestRemainingSeconds = Mathf.Max(longestRemainingSeconds, effect.RemainingSeconds);
            }
        }

        return Mathf.Clamp01(longestRemainingSeconds / 4f) * 70f;
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
