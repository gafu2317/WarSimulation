using System.Collections.Generic;
using Unity.Profiling;
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
    private static readonly ProfilerMarker BuildAssessmentMarker = new("CombatAI.BuildAssessment");
    private static readonly ProfilerMarker SelectObjectiveMarker = new("CombatAI.SelectObjective");
    private static readonly ProfilerMarker SelectMoveMarker = new("CombatAI.SelectMove");
    private static readonly ProfilerMarker SelectSkillMarker = new("CombatAI.SelectSkill");

    [System.ThreadStatic] private static List<SkillExecutionContext> s_skillContextsBuffer;

    private static List<SkillExecutionContext> SkillContextsBuffer =>
        s_skillContextsBuffer ??= new List<SkillExecutionContext>();

    private static CombatMoveTarget CreatePersonalitySignatureTarget(
        CombatAiContext context,
        CombatAiPersonalityProfile personalityProfile,
        Character focusEnemy,
        float focusCommitmentRemainingSeconds)
    {
        if (context == null || context.Owner == null || personalityProfile == null) return CombatMoveTarget.None;

        return personalityProfile.Kind switch
        {
            CombatAiPersonalityKind.AttentionSeeker => CreateAttentionSeekerTarget(context),
            CombatAiPersonalityKind.BattleJunkie => CreateBestEnemyTarget(
                context, focusEnemy, focusCommitmentRemainingSeconds),
            CombatAiPersonalityKind.Coward => CreateCowardLowAttentionTarget(context),
            CombatAiPersonalityKind.Cunning => CreateCunningLowRiskStoneTarget(context),
            CombatAiPersonalityKind.Devoted => CreateDevotedLowHpAllyTarget(context),
            CombatAiPersonalityKind.Lonely => CreateLonelyClingTarget(context),
            CombatAiPersonalityKind.Reckless => CreateEnemyStoneTarget(context),
            _ => CombatMoveTarget.None,
        };
    }

    public static CombatAiPlan BuildPlan(
        CombatAiContext context,
        CombatAiPersonalityProfile personalityProfile,
        Character focusEnemy = null,
        float focusCommitmentRemainingSeconds = 0f,
        CombatObjective previousObjective = CombatObjective.Search,
        List<CombatAiReasonCode> selectedObjectiveReasons = null,
        float objectiveCommitmentRemainingSeconds = 0f)
    {
        if (context == null || context.Owner == null) return CombatAiPlan.None;
        return BuildPlanCore(
            context,
            personalityProfile,
            focusEnemy,
            focusCommitmentRemainingSeconds,
            previousObjective,
            objectiveCommitmentRemainingSeconds,
            null,
            selectedObjectiveReasons);
    }

    private static CombatAiPlan BuildPlanCore(
        CombatAiContext context,
        CombatAiPersonalityProfile personalityProfile,
        Character focusEnemy,
        float focusCommitmentRemainingSeconds,
        CombatObjective previousObjective,
        float objectiveCommitmentRemainingSeconds,
        CombatAiDebugSnapshot snapshot,
        List<CombatAiReasonCode> selectedObjectiveReasons)
    {
        bool captureDebug = snapshot != null;
        CombatAiAssessment assessment;
        using (BuildAssessmentMarker.Auto())
        {
            assessment = CombatAiAssessmentBuilder.Build(context);
        }
        CombatObjective objective;
        using (SelectObjectiveMarker.Auto())
        {
            if (captureDebug)
            {
                snapshot.Assessment = assessment;
                CombatAiObjectiveScorer.BuildEntries(
                    snapshot,
                    personalityProfile,
                    focusEnemy,
                    focusCommitmentRemainingSeconds,
                    previousObjective,
                    objectiveCommitmentRemainingSeconds);
                objective = snapshot.SelectedObjective != null
                    ? snapshot.SelectedObjective.Objective
                    : CombatObjective.Search;
                if (selectedObjectiveReasons != null)
                {
                    selectedObjectiveReasons.Clear();
                    if (snapshot.SelectedObjective?.Breakdown != null)
                    {
                        selectedObjectiveReasons.AddRange(snapshot.SelectedObjective.Breakdown.ReasonCodes);
                    }
                }
            }
            else
            {
                objective = CombatAiObjectiveScorer.SelectBestObjective(
                    context,
                    assessment,
                    personalityProfile,
                    focusEnemy,
                    focusCommitmentRemainingSeconds,
                    previousObjective,
                    selectedObjectiveReasons,
                    objectiveCommitmentRemainingSeconds);
            }
        }

        CombatMoveTarget moveTarget;
        CombatAiMoveCandidateEntry selectedMove;
        using (SelectMoveMarker.Auto())
        {
            moveTarget = SelectBestMove(
                context, assessment, personalityProfile,
                objective, focusEnemy, focusCommitmentRemainingSeconds,
                captureDebug ? snapshot.MoveEntries : null,
                out selectedMove);
        }
        if (captureDebug) snapshot.SelectedMove = selectedMove;

        SkillBase bestSkill;
        SkillExecutionContext bestSkillContext;
        CombatAiSkillCandidateEntry selectedSkill;
        using (SelectSkillMarker.Auto())
        {
            SelectBestSkill(
                context, assessment, personalityProfile,
                objective, focusEnemy, focusCommitmentRemainingSeconds,
                captureDebug ? snapshot.SkillEntries : null,
                out bestSkill,
                out bestSkillContext,
                out selectedSkill);
        }
        if (captureDebug) snapshot.SelectedSkill = selectedSkill;

        var plan = new CombatAiPlan(objective, moveTarget, bestSkill, bestSkillContext);
        return plan;
    }

    private static CombatMoveTarget SelectBestMove(
        CombatAiContext context,
        CombatAiAssessment assessment,
        CombatAiPersonalityProfile personalityProfile,
        CombatObjective objective,
        Character focusEnemy,
        float focusCommitmentRemainingSeconds,
        List<CombatAiMoveCandidateEntry> entries,
        out CombatAiMoveCandidateEntry selectedEntry)
    {
        Character owner = context.Owner;
        CombatMoveTarget bestTarget = CombatMoveTarget.None;
        float bestScore = float.NegativeInfinity;
        selectedEntry = null;
        entries?.Clear();

        TryScoreMoveCandidate(owner, context, assessment, personalityProfile,
            CombatAiMoveCode.AdvanceEnemyStone, "敵魔石へ前進", CreateEnemyStoneTarget(context), objective, focusEnemy,
            focusCommitmentRemainingSeconds, entries, ref bestScore, ref bestTarget, ref selectedEntry);
        for (int i = 0; i < context.AssaultRoutes.Count; i++)
        {
            CombatAiAssaultRoute route = context.AssaultRoutes[i];
            CreateAssaultRouteAdvanceCandidate(
                context,
                route,
                out string code,
                out string japanese,
                out CombatMoveTarget target);
            TryScoreMoveCandidate(owner, context, assessment, personalityProfile,
                code, japanese, target, objective, focusEnemy,
                focusCommitmentRemainingSeconds, entries, ref bestScore, ref bestTarget, ref selectedEntry);
        }
        TryScoreMoveCandidate(owner, context, assessment, personalityProfile,
            CombatAiMoveCode.ReturnOwnStone, "自軍魔石へ戻る", CreateOwnStoneTarget(context), objective, focusEnemy,
            focusCommitmentRemainingSeconds, entries, ref bestScore, ref bestTarget, ref selectedEntry);
        TryScoreMoveCandidate(owner, context, assessment, personalityProfile,
            CombatAiMoveCode.PursueEnemy, "敵へ接近", CreateBestEnemyTarget(context, focusEnemy, focusCommitmentRemainingSeconds),
            objective, focusEnemy, focusCommitmentRemainingSeconds, entries, ref bestScore, ref bestTarget, ref selectedEntry);
        TryScoreMoveCandidate(owner, context, assessment, personalityProfile,
            CombatAiMoveCode.SupportAlly, "味方へ接近", CreateBestAllyTarget(context), objective, focusEnemy,
            focusCommitmentRemainingSeconds, entries, ref bestScore, ref bestTarget, ref selectedEntry);
        TryScoreMoveCandidate(owner, context, assessment, personalityProfile,
            CombatAiMoveCode.InterceptThreat, "敵の進路を遮断", CreateBestBodyBlockTarget(context), objective, focusEnemy,
            focusCommitmentRemainingSeconds, entries, ref bestScore, ref bestTarget, ref selectedEntry);
        WeaponKind ownerWeaponKind = owner.EquippedWeapon != null ? owner.EquippedWeapon.Kind : WeaponKind.Unarmed;
        for (int i = 0; ownerWeaponKind != WeaponKind.Shield && i < context.HighGroundCandidates.Count; i++)
        {
            TryScoreMoveCandidate(owner, context, assessment, personalityProfile,
                CombatAiMoveCode.TakeHighGround, "高所へ移動", CreatePositionTargetIfMeaningful(owner, context.HighGroundCandidates[i]),
                objective, focusEnemy, focusCommitmentRemainingSeconds, entries, ref bestScore, ref bestTarget, ref selectedEntry);
        }
        {
            CombatMoveTarget forestTarget = IsRangedOrSupportWeapon(ownerWeaponKind)
                ? CreateCoverPositionTarget(context, owner)
                : CreateNearestPositionTarget(owner, context.ForestCandidates);
            TryScoreMoveCandidate(owner, context, assessment, personalityProfile,
                CombatAiMoveCode.MoveForest, "森へ移動", forestTarget, objective, focusEnemy, focusCommitmentRemainingSeconds,
                entries, ref bestScore, ref bestTarget, ref selectedEntry);
        }
        TryScoreMoveCandidate(owner, context, assessment, personalityProfile,
            CombatAiMoveCode.SearchLastKnown, "最終既知地点へ移動", CreateLastKnownEnemyTarget(context), objective, focusEnemy,
            focusCommitmentRemainingSeconds, entries, ref bestScore, ref bestTarget, ref selectedEntry);
        TryScoreMoveCandidate(owner, context, assessment, personalityProfile,
            CombatAiMoveCode.PersonalitySignature, "性格固有の移動",
            CreatePersonalitySignatureTarget(context, personalityProfile, focusEnemy, focusCommitmentRemainingSeconds),
            objective, focusEnemy, focusCommitmentRemainingSeconds, entries, ref bestScore, ref bestTarget, ref selectedEntry);
        TryScoreMoveCandidate(owner, context, assessment, personalityProfile,
            CombatAiMoveCode.HoldPosition, "待機", CombatMoveTarget.None, objective, focusEnemy,
            focusCommitmentRemainingSeconds, entries, ref bestScore, ref bestTarget, ref selectedEntry);

        return bestTarget;
    }

    private static void TryScoreMoveCandidate(
        Character owner,
        CombatAiContext context,
        CombatAiAssessment assessment,
        CombatAiPersonalityProfile personalityProfile,
        string code,
        string japanese,
        CombatMoveTarget target,
        CombatObjective objective,
        Character focusEnemy,
        float focusCommitmentRemainingSeconds,
        List<CombatAiMoveCandidateEntry> entries,
        ref float bestScore,
        ref CombatMoveTarget bestTarget,
        ref CombatAiMoveCandidateEntry selectedEntry)
    {
        if (code != CombatAiMoveCode.HoldPosition && !target.HasDestination) return;
        if (target.HasDestination && context.IsMoveDestinationBlocked(target.Destination)) return;
        if (target.HasDestination && !CombatAiMoveScorer.IsReachable(owner, target.Destination)) return;
        if (code == CombatAiMoveCode.AdvanceViaBridge &&
            context.HasEnemyStonePosition &&
            !CombatAiMoveScorer.IsReachableVia(
                owner,
                target.Destination,
                context.EnemyStonePosition))
        {
            return;
        }
        CombatAiScoreBreakdown breakdown = entries != null ? new CombatAiScoreBreakdown() : null;
        float score = CombatAiMoveScorer.Score(
            owner, context, assessment, personalityProfile,
            code, target, objective, focusEnemy, focusCommitmentRemainingSeconds, breakdown);
        CombatAiMoveCandidateEntry entry = null;
        if (entries != null)
        {
            entry = new CombatAiMoveCandidateEntry
            {
                Code = code,
                Label = CombatAiDebugLabels.MoveCode(code, japanese),
                Target = target,
                Breakdown = breakdown,
            };
            entries.Add(entry);
        }
        if (score > bestScore)
        {
            bestScore = score;
            bestTarget = target;
            selectedEntry = entry;
        }
    }

    private static void SelectBestSkill(
        CombatAiContext context,
        CombatAiAssessment assessment,
        CombatAiPersonalityProfile personalityProfile,
        CombatObjective objective,
        Character focusEnemy,
        float focusCommitmentRemainingSeconds,
        List<CombatAiSkillCandidateEntry> entries,
        out SkillBase bestSkill,
        out SkillExecutionContext bestSkillContext,
        out CombatAiSkillCandidateEntry selectedEntry)
    {
        bestSkill = null;
        bestSkillContext = SkillExecutionContext.None;
        selectedEntry = null;
        entries?.Clear();
        IReadOnlyList<SkillBase> skills = context.Owner.AvailableCombatSkills;
        float waitBaseScore = skills.Count == 0 ? 12f : 3f;
        float waitSituationScore = objective == CombatObjective.Retreat ? 8f : 0f;
        float bestScore = waitBaseScore + waitSituationScore;
        CombatAiSkillCandidateEntry waitEntry = null;
        if (entries != null)
        {
            waitEntry = new CombatAiSkillCandidateEntry
            {
                Code = "Wait",
                Label = CombatAiDebugLabels.Format("Wait", "何もしない"),
                Skill = null,
                SkillContext = SkillExecutionContext.None,
                Evaluation = default,
                Breakdown = new CombatAiScoreBreakdown
                {
                    BaseScore = waitBaseScore,
                    SituationScore = waitSituationScore,
                },
            };
            selectedEntry = waitEntry;
        }

        for (int i = 0; i < skills.Count; i++)
        {
            SkillBase skill = skills[i];
            if (skill == null) continue;

            List<SkillExecutionContext> skillContexts = SkillContextsBuffer;
            CombatAiSkillContextBuilder.Build(context, context.Owner, skill, skillContexts);
            for (int j = 0; j < skillContexts.Count; j++)
            {
                CombatSkillEvaluationResult evaluation = CombatSkillEvaluator.Evaluate(context.Owner, skill, skillContexts[j]);
                if (!CanPersonalityUseSkill(personalityProfile, skill, evaluation.Context, context)) continue;
                CombatAiScoreBreakdown breakdown = entries != null ? new CombatAiScoreBreakdown() : null;
                float score = ScoreSkillCandidate(
                    context,
                    assessment,
                    personalityProfile,
                    skill,
                    evaluation,
                    objective,
                    focusEnemy,
                    focusCommitmentRemainingSeconds,
                    breakdown);
                CombatAiSkillCandidateEntry entry = null;
                if (entries != null)
                {
                    entry = new CombatAiSkillCandidateEntry
                    {
                        Code = BuildSkillCandidateCode(skill, j),
                        Label = CombatAiDebugLabels.Skill(skill) + " / " + FormatSkillContextLabel(evaluation.Context),
                        Skill = skill,
                        SkillContext = evaluation.Context,
                        Evaluation = evaluation,
                        Breakdown = breakdown,
                    };
                    entries.Add(entry);
                }
                if (score > bestScore)
                {
                    bestScore = score;
                    bestSkill = skill;
                    bestSkillContext = evaluation.Context;
                    selectedEntry = entry;
                }
            }
        }

        if (entries != null) entries.Add(waitEntry);
    }

    private static float ScoreSkillCandidate(
        CombatAiContext context,
        CombatAiAssessment assessment,
        CombatAiPersonalityProfile personalityProfile,
        SkillBase skill,
        CombatSkillEvaluationResult evaluation,
        CombatObjective objective,
        Character focusEnemy,
        float focusCommitmentRemainingSeconds,
        CombatAiScoreBreakdown breakdown)
    {
        float baseScore = GetSkillBaseScore(skill, objective);
        float weaponScore = GetSkillWeaponScore(context.Owner.EquippedWeapon, skill);
        float personalityScore = CombatAiPersonalityBehavior.GetSkillScore(
            context.Owner, personalityProfile, skill, objective);
        float situationScore = GetSkillSituationScore(context, assessment, skill, evaluation, objective)
            + CombatAiFocusTargeting.GetSkillScore(
                context,
                context.Owner.EquippedWeapon,
                skill,
                evaluation,
                focusEnemy,
                focusCommitmentRemainingSeconds);
        if (breakdown != null)
        {
            breakdown.BaseScore = baseScore;
            breakdown.WeaponScore = weaponScore;
            breakdown.PersonalityScore = personalityScore;
            breakdown.SituationScore = situationScore;
            AddSkillReasons(evaluation, breakdown);
            if (weaponScore != 0f) AddReason(breakdown, CombatAiReasonCode.WeaponPreference);
            if (personalityScore != 0f) AddReason(breakdown, CombatAiReasonCode.PersonalityPreference);
        }

        return baseScore + weaponScore + personalityScore + situationScore;
    }

    public static CombatAiDebugSnapshot BuildDebugSnapshot(
        CombatAiContext context,
        CombatAiPersonalityProfile personalityProfile,
        Character focusEnemy = null,
        float focusCommitmentRemainingSeconds = 0f,
        CombatObjective previousObjective = CombatObjective.Search,
        float objectiveCommitmentRemainingSeconds = 0f)
    {
        if (context == null || context.Owner == null)
        {
            return null;
        }

        var snapshot = new CombatAiDebugSnapshot
        {
            Owner = context.Owner,
            Context = context,
        };
        BuildPlanCore(
            context,
            personalityProfile,
            focusEnemy,
            focusCommitmentRemainingSeconds,
            previousObjective,
            objectiveCommitmentRemainingSeconds,
            snapshot,
            null);
        return snapshot;
    }

    private static bool CanPersonalityUseSkill(
        CombatAiPersonalityProfile personalityProfile,
        SkillBase skill,
        SkillExecutionContext skillContext,
        CombatAiContext aiContext)
    {
        if (personalityProfile == null || skill == null) return true;
        if (personalityProfile.Kind == CombatAiPersonalityKind.Lonely &&
            !CombatAiPersonalityBehavior.HasNearbyAlly(
                aiContext,
                CombatAiPersonalityBehavior.LonelyNearbyAllyRadius))
        {
            return false;
        }

        if (personalityProfile.Kind == CombatAiPersonalityKind.Reckless)
        {
            return CombatAiSkillClassifier.IsDamage(skill) && skillContext.PrimaryStone != null;
        }

        return true;
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

    private static float GetSkillWeaponScore(WeaponBase weapon, SkillBase skill)
    {
        if (weapon == null || skill == null) return 0f;
        return CombatAiWeaponWeights.GetSkillWeight(weapon.Kind, skill);
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

        float score = 16f;
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
        // その場で使える魔石攻撃は、敵単体より優先する。
        if (evaluation.CanUse &&
            CombatAiSkillClassifier.IsDamage(skill) &&
            (evaluation.Context.PrimaryStone != null || evaluation.ResolvedStones.Count > 0))
        {
            score += 100f;
        }

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
            if (!ally.IsAlive || ally.MaxHP <= 0) return -80f;

            int reservedHealing = GetAllyPendingHealing(context, ally.Character);
            int projectedHP = Mathf.Min(ally.MaxHP, ally.HP + reservedHealing);
            float hpRatio = projectedHP / (float)ally.MaxHP;
            float missingHpRatio = 1f - hpRatio;
            if (CombatAiSkillClassifier.IsHeal(skill) && missingHpRatio <= 0.05f)
            {
                return -80f;
            }

            float score = missingHpRatio * (CombatAiSkillClassifier.IsHeal(skill) ? 50f : 18f);
            score += CombatAiPositioning.GetAdvanceProgress(context, ally.CurrentPosition) *
                (CombatAiSkillClassifier.IsHeal(skill) ? 8f : 12f);
            if (CombatAiSkillClassifier.IsHeal(skill))
            {
                int missingHp = Mathf.Max(0, ally.MaxHP - projectedHP);
                int healing = skill.EstimateHealing(context.Owner, skillContext, ally.Character);
                score += ally.MaxHP > 0 ? Mathf.Min(missingHp, healing) / (float)ally.MaxHP * 40f : 0f;
                score += GetPostHealSurvivalScore(context, ally, healing);
            }
            if (HasEnemyNearby(context.EnemyIntel, ally.CurrentPosition, 8f))
            {
                score += CombatAiSkillClassifier.IsProtect(skill) ? 24f : 8f;
            }

            score += GetSupportTargetAffinityScore(context, skill, ally);
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
            if (ally.Character == null || !ally.IsAlive || ally.MaxHP <= 0) continue;
            if (HorizontalDistance(skillContext.TargetPoint, ally.CurrentPosition) > skill.AreaRadius) continue;
            int projectedHP = Mathf.Min(
                ally.MaxHP,
                ally.HP + GetAllyPendingHealing(context, ally.Character));
            int missingHp = Mathf.Max(0, ally.MaxHP - projectedHP);
            int healing = skill.EstimateHealing(context.Owner, skillContext, ally.Character);
            score += missingHp / (float)ally.MaxHP * 28f;
            score += Mathf.Min(missingHp, healing) / (float)ally.MaxHP * 36f;
            score += GetPostHealSurvivalScore(context, ally, healing);
            score += CombatAiPositioning.GetAdvanceProgress(context, ally.CurrentPosition) * 6f;
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
        int reservedHealing = GetAllyPendingHealing(context, ally.Character);
        int projectedHp = Mathf.Min(ally.MaxHP, ally.HP + reservedHealing + healing) - incomingDamage;
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

        // その場で当てられる魔石は、敵キャラも同時ヒットしていても加点する。
        if (capturedContext.PrimaryStone != null || capturedContext.ResolvedStones.Count > 0)
        {
            int stoneHits = Mathf.Max(1, capturedContext.ResolvedStones.Count);
            score += 24f * stoneHits;
            if (foundCharacterTarget)
            {
                score += 12f;
            }
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

    private static int GetAllyPendingHealing(CombatAiContext context, Character target)
    {
        int healing = 0;
        for (int i = 0; i < context.AllyPendingHealing.Count; i++)
        {
            CombatAiPendingHealing pending = context.AllyPendingHealing[i];
            if (pending.Target == target)
            {
                healing += pending.Healing;
            }
        }

        return healing;
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
            if (!enemy.IsAlive || !enemy.HasKnownPosition || !enemy.CanAct) continue;
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
            if (!enemies[i].IsAlive || !enemies[i].HasKnownPosition) continue;
            if (HorizontalDistance(position, enemies[i].KnownPosition) <= radius) return true;
        }

        return false;
    }

    private static bool HasVisibleLivingEnemy(CombatAiContext context)
    {
        for (int i = 0; i < context.EnemyIntel.Count; i++)
        {
            CombatCharacterIntel enemy = context.EnemyIntel[i];
            if (enemy.IsAlive && enemy.HasDirectSight) return true;
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
            CombatObjective.AttackEnemy when CombatAiSkillClassifier.IsDamage(skill)
                && evaluation.Context.PrimaryStone != null => 28f,
            CombatObjective.AttackEnemy when CombatAiSkillClassifier.IsDamage(skill) => 18f,
            CombatObjective.AttackEnemy when CombatAiSkillClassifier.IsDebuff(skill) => 14f,
            CombatObjective.SupportAlly when CombatAiSkillClassifier.IsHeal(skill) => 24f,
            CombatObjective.SupportAlly when CombatAiSkillClassifier.IsBuff(skill) || CombatAiSkillClassifier.IsProtect(skill) => 18f,
            CombatObjective.DefendOwnStone when CombatAiSkillClassifier.IsProtect(skill) => 20f,
            CombatObjective.DefendOwnStone when CombatAiSkillClassifier.IsDamage(skill) => assessment.GetValue(CombatAiMetricIndex.OwnStoneThreat) * 0.2f,
            CombatObjective.Retreat when CombatAiSkillClassifier.IsHeal(skill) || CombatAiSkillClassifier.IsProtect(skill) || CombatAiSkillClassifier.IsStealth(skill) => 20f,
            CombatObjective.Search when CombatAiSkillClassifier.IsStealth(skill) || CombatAiSkillClassifier.IsMobility(skill) => 16f,
            CombatObjective.DestroyEnemyStone when CombatAiSkillClassifier.IsSupport(skill) && assessment.GetValue(CombatAiMetricIndex.AllyFragility) < 20f => -20f,
            CombatObjective.DestroyEnemyStone when CombatAiSkillClassifier.IsDamage(skill) && HasVisibleLivingEnemy(context) => 6f,
            _ => 0f,
        };
    }

    private static float GetSupportTargetAffinityScore(
        CombatAiContext context,
        SkillBase skill,
        CombatCharacterIntel ally)
    {
        if (skill is not IdentifiedSkill identified) return 0f;

        float score = identified.SkillId switch
        {
            SkillId.Bible_StrBuff when ally.WeaponKind == WeaponKind.Sword || ally.WeaponKind == WeaponKind.Shield => 28f,
            SkillId.Bible_IntBuff when ally.WeaponKind == WeaponKind.Wand || ally.WeaponKind == WeaponKind.Grimoire => 28f,
            SkillId.Bible_FaiBuff when ally.WeaponKind == WeaponKind.Bible || ally.WeaponKind == WeaponKind.Rosary => 28f,
            SkillId.Bible_AgiBuff when ally.WeaponKind == WeaponKind.Sword => 14f,
            SkillId.Shield_ShoulderGuard when CombatAiPositioning.IsAdvancingAlly(context, ally) => 36f,
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
            if (!enemy.IsAlive || !enemy.HasDirectSight || !enemy.CanAct) continue;

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
