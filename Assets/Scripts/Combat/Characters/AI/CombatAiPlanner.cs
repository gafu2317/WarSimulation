using System.Collections.Generic;
using UnityEngine;

public static class CombatAiPlanner
{
    private const float UnselectableScore = -100000f;

    public static CombatAiPlan BuildPlan(
        CombatAiContext context,
        CombatAiPersonalityProfile personalityProfile,
        CombatAiWeaponWeightsProfile weaponWeightsProfile = null)
    {
        CombatAiDebugSnapshot snapshot = BuildDebugSnapshot(context, personalityProfile, weaponWeightsProfile);
        return snapshot != null ? snapshot.FinalPlan : CombatAiPlan.None;
    }

    public static CombatAiDebugSnapshot BuildDebugSnapshot(
        CombatAiContext context,
        CombatAiPersonalityProfile personalityProfile,
        CombatAiWeaponWeightsProfile weaponWeightsProfile = null)
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

        BuildObjectiveEntries(snapshot, personalityProfile, weaponWeightsProfile);
        snapshot.SelectedObjective = SelectHighest(snapshot.ObjectiveEntries);
        BuildMoveEntries(snapshot, personalityProfile, weaponWeightsProfile);
        snapshot.SelectedMove = SelectHighest(snapshot.MoveEntries);
        BuildSkillEntries(snapshot, personalityProfile, weaponWeightsProfile);
        snapshot.SelectedSkill = SelectHighest(snapshot.SkillEntries);

        snapshot.FinalPlan = new CombatAiPlan(
            snapshot.SelectedObjective != null ? snapshot.SelectedObjective.Objective : CombatObjective.Search,
            snapshot.SelectedMove != null ? snapshot.SelectedMove.Target : CombatMoveTarget.None,
            snapshot.SelectedSkill != null ? snapshot.SelectedSkill.Skill : null,
            snapshot.SelectedSkill != null ? snapshot.SelectedSkill.SkillContext : SkillExecutionContext.None);
        return snapshot;
    }

    private static void BuildObjectiveEntries(
        CombatAiDebugSnapshot snapshot,
        CombatAiPersonalityProfile personalityProfile,
        CombatAiWeaponWeightsProfile weaponWeightsProfile)
    {
        snapshot.ObjectiveEntries.Clear();
        foreach (CombatObjective objective in System.Enum.GetValues(typeof(CombatObjective)))
        {
            var breakdown = new CombatAiScoreBreakdown
            {
                BaseScore = GetObjectiveBaseScore(objective),
                SituationScore = GetObjectiveSituationScore(
                    snapshot.Context,
                    snapshot.Assessment,
                    snapshot.Owner.EquippedWeapon,
                    objective),
                WeaponScore = GetObjectiveWeaponScore(weaponWeightsProfile, snapshot.Owner.EquippedWeapon, objective),
                PersonalityScore = GetObjectivePersonalityScore(personalityProfile, objective),
            };

            AddObjectiveReasons(snapshot.Assessment, objective, breakdown);
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

    private static void BuildMoveEntries(
        CombatAiDebugSnapshot snapshot,
        CombatAiPersonalityProfile personalityProfile,
        CombatAiWeaponWeightsProfile weaponWeightsProfile)
    {
        snapshot.MoveEntries.Clear();
        CombatObjective objective = snapshot.SelectedObjective != null ? snapshot.SelectedObjective.Objective : CombatObjective.Search;
        AddMoveCandidate(snapshot, personalityProfile, weaponWeightsProfile, "AdvanceEnemyStone", "敵魔石へ前進", CreateEnemyStoneTarget(snapshot.Context), objective);
        AddMoveCandidate(snapshot, personalityProfile, weaponWeightsProfile, "ReturnOwnStone", "自軍魔石へ戻る", CreateOwnStoneTarget(snapshot.Context), objective);
        AddMoveCandidate(snapshot, personalityProfile, weaponWeightsProfile, "PursueEnemy", "敵へ接近", CreateBestEnemyTarget(snapshot.Context), objective);
        AddMoveCandidate(snapshot, personalityProfile, weaponWeightsProfile, "SupportAlly", "味方へ接近", CreateBestAllyTarget(snapshot.Context), objective);
        AddMoveCandidate(snapshot, personalityProfile, weaponWeightsProfile, "TakeHighGround", "高所へ移動", CreateNearestPositionTarget(snapshot.Owner, snapshot.Context.HighGroundCandidates), objective);
        AddMoveCandidate(snapshot, personalityProfile, weaponWeightsProfile, "MoveForest", "森へ移動", CreateNearestPositionTarget(snapshot.Owner, snapshot.Context.ForestCandidates), objective);
        AddMoveCandidate(snapshot, personalityProfile, weaponWeightsProfile, "SearchLastKnown", "最終既知地点へ移動", CreateLastKnownEnemyTarget(snapshot.Context), objective);
        AddMoveCandidate(snapshot, personalityProfile, weaponWeightsProfile, "HoldPosition", "待機", CombatMoveTarget.None, objective);
    }

    private static void BuildSkillEntries(
        CombatAiDebugSnapshot snapshot,
        CombatAiPersonalityProfile personalityProfile,
        CombatAiWeaponWeightsProfile weaponWeightsProfile)
    {
        snapshot.SkillEntries.Clear();
        IReadOnlyList<SkillBase> skills = snapshot.Owner.AvailableCombatSkills;
        for (int i = 0; i < skills.Count; i++)
        {
            SkillBase skill = skills[i];
            AddSkillEntries(snapshot, personalityProfile, weaponWeightsProfile, skill);
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
        CombatObjective objective)
    {
        if (code != "HoldPosition" && !target.HasDestination)
        {
            return;
        }

        float distance = target.HasDestination ? HorizontalDistance(snapshot.Owner.transform.position, target.Destination) : 0f;
        var breakdown = new CombatAiScoreBreakdown
        {
            BaseScore = target.HasDestination ? Mathf.Lerp(24f, 4f, Mathf.Clamp01(distance / 40f)) : 8f,
            SituationScore = GetMoveSituationScore(snapshot.Assessment, code, objective),
            WeaponScore = GetMoveWeaponScore(weaponWeightsProfile, snapshot.Owner.EquippedWeapon, code),
            PersonalityScore = GetMovePersonalityScore(personalityProfile, code, objective),
        };
        AddMoveReasons(code, breakdown);
        if (breakdown.WeaponScore != 0f) AddReason(breakdown, CombatAiReasonCode.WeaponPreference);
        if (breakdown.PersonalityScore != 0f) AddReason(breakdown, CombatAiReasonCode.PersonalityPreference);

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
        SkillBase skill)
    {
        List<SkillExecutionContext> contexts = BuildSkillContexts(snapshot.Context, snapshot.Owner, skill);
        for (int i = 0; i < contexts.Count; i++)
        {
            AddSkillEntry(snapshot, personalityProfile, weaponWeightsProfile, skill, contexts[i], i);
        }
    }

    private static void AddSkillEntry(
        CombatAiDebugSnapshot snapshot,
        CombatAiPersonalityProfile personalityProfile,
        CombatAiWeaponWeightsProfile weaponWeightsProfile,
        SkillBase skill,
        SkillExecutionContext context,
        int contextIndex)
    {
        CombatSkillEvaluationResult evaluation = CombatSkillEvaluator.Evaluate(snapshot.Owner, skill, context);
        CombatObjective objective = snapshot.SelectedObjective != null ? snapshot.SelectedObjective.Objective : CombatObjective.Search;
        var breakdown = new CombatAiScoreBreakdown
        {
            BaseScore = GetSkillBaseScore(skill, objective),
            WeaponScore = GetSkillWeaponScore(weaponWeightsProfile, snapshot.Owner.EquippedWeapon, skill),
            PersonalityScore = GetSkillPersonalityScore(personalityProfile, skill, objective),
            SituationScore = GetSkillSituationScore(snapshot.Context, snapshot.Assessment, skill, evaluation, objective),
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

    private static float GetObjectiveBaseScore(CombatObjective objective)
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

    private static float GetObjectiveSituationScore(
        CombatAiContext context,
        CombatAiAssessment assessment,
        WeaponBase weapon,
        CombatObjective objective)
    {
        float score = objective switch
        {
            CombatObjective.AttackEnemy => assessment.GetValue("ReachableEnemyValue") * 0.9f
                + assessment.GetValue("TerrainAdvantage") * 0.15f
                - assessment.GetValue("SelfThreat") * 0.35f,
            CombatObjective.DefendOwnStone => assessment.GetValue("OwnStoneThreat") * 0.95f
                + assessment.GetValue("AllyFragility") * 0.2f
                + assessment.GetValue("EnemyLocationConfidence") * 0.1f,
            CombatObjective.SupportAlly => assessment.GetValue("AllyFragility") * 0.95f
                + assessment.GetValue("TerrainAdvantage") * 0.1f,
            CombatObjective.DestroyEnemyStone => assessment.GetValue("EnemyStoneReachability") * 0.85f
                - assessment.GetValue("OwnStoneThreat") * 0.35f
                - assessment.GetValue("SelfThreat") * 0.2f
                + (context.HasEnemyStonePosition ? 24f : 0f)
                + Mathf.Max(0f, 20f - assessment.GetValue("AllyFragility") * 0.2f),
            CombatObjective.Search => (100f - assessment.GetValue("EnemyLocationConfidence")) * 0.55f
                + assessment.GetValue("TerrainAdvantage") * 0.2f
                - (context.HasEnemyStonePosition ? 28f : 0f),
            CombatObjective.Retreat => assessment.GetValue("SelfThreat") * 0.9f
                + assessment.GetValue("RetreatRouteSafety") * 0.3f
                + assessment.GetValue("AllyFragility") * 0.1f,
            _ => 0f,
        };

        return score + GetWeaponObjectiveSituationAdjustment(context, assessment, weapon, objective);
    }

    private static float GetWeaponObjectiveSituationAdjustment(
        CombatAiContext context,
        CombatAiAssessment assessment,
        WeaponBase weapon,
        CombatObjective objective)
    {
        WeaponKind kind = weapon != null ? weapon.Kind : WeaponKind.Unarmed;
        return kind switch
        {
            WeaponKind.Sword => GetSwordObjectiveSituationAdjustment(context, assessment, objective),
            WeaponKind.Shield => GetShieldObjectiveSituationAdjustment(context, assessment, objective),
            WeaponKind.Wand => GetWandObjectiveSituationAdjustment(context, assessment, objective),
            WeaponKind.Grimoire => GetGrimoireObjectiveSituationAdjustment(context, assessment, objective),
            WeaponKind.Bible => GetBibleObjectiveSituationAdjustment(context, assessment, objective),
            WeaponKind.Rosary => GetRosaryObjectiveSituationAdjustment(context, assessment, objective),
            _ => 0f,
        };
    }

    private static float GetSwordObjectiveSituationAdjustment(
        CombatAiContext context,
        CombatAiAssessment assessment,
        CombatObjective objective)
    {
        return objective switch
        {
            CombatObjective.AttackEnemy when assessment.GetValue("ReachableEnemyValue") > 30f
                && assessment.GetValue("SelfThreat") < 35f => 16f,
            CombatObjective.DestroyEnemyStone when context.HasEnemyStonePosition
                && assessment.GetValue("EnemyStoneReachability") > 28f
                && assessment.GetValue("OwnStoneThreat") < 24f => 14f,
            CombatObjective.DefendOwnStone when assessment.GetValue("OwnStoneThreat") > 28f => 12f,
            _ => 0f,
        };
    }

    private static float GetShieldObjectiveSituationAdjustment(
        CombatAiContext context,
        CombatAiAssessment assessment,
        CombatObjective objective)
    {
        return objective switch
        {
            CombatObjective.DefendOwnStone when context.HasOwnStonePosition
                && (assessment.GetValue("OwnStoneThreat") > 18f || assessment.GetValue("AllyFragility") > 22f) => 18f,
            CombatObjective.AttackEnemy when assessment.GetValue("OwnStoneThreat") < 16f
                && assessment.GetValue("ReachableEnemyValue") > 24f => 10f,
            CombatObjective.DestroyEnemyStone when context.HasEnemyStonePosition
                && assessment.GetValue("EnemyStoneReachability") > 30f
                && assessment.GetValue("OwnStoneThreat") < 12f
                && assessment.GetValue("AllyFragility") < 18f => 8f,
            _ => 0f,
        };
    }

    private static float GetWandObjectiveSituationAdjustment(
        CombatAiContext context,
        CombatAiAssessment assessment,
        CombatObjective objective)
    {
        bool lacksReliableShot = assessment.GetValue("ReachableEnemyValue") < 24f;
        return objective switch
        {
            CombatObjective.Search when assessment.GetValue("EnemyLocationConfidence") < 45f
                || (context.VisibleEnemies.Count == 0 && context.HighGroundCandidates.Count > 0)
                || lacksReliableShot => 16f,
            CombatObjective.AttackEnemy when assessment.GetValue("ReachableEnemyValue") > 28f
                && assessment.GetValue("EnemyLocationConfidence") > 35f => 14f,
            CombatObjective.DestroyEnemyStone when context.HasEnemyStonePosition
                && assessment.GetValue("EnemyStoneReachability") > 34f
                && lacksReliableShot => 4f,
            _ => 0f,
        };
    }

    private static float GetGrimoireObjectiveSituationAdjustment(
        CombatAiContext context,
        CombatAiAssessment assessment,
        CombatObjective objective)
    {
        bool multipleEnemiesVisible = context.VisibleEnemies.Count >= 2;
        return objective switch
        {
            CombatObjective.AttackEnemy when multipleEnemiesVisible
                || assessment.GetValue("ReachableEnemyValue") > 28f => 16f,
            CombatObjective.DestroyEnemyStone when context.HasEnemyStonePosition
                && assessment.GetValue("EnemyStoneReachability") > 30f
                && assessment.GetValue("OwnStoneThreat") < 18f => 10f,
            CombatObjective.Search when assessment.GetValue("EnemyLocationConfidence") < 35f
                && assessment.GetValue("ReachableEnemyValue") < 18f => 10f,
            _ => 0f,
        };
    }

    private static float GetBibleObjectiveSituationAdjustment(
        CombatAiContext context,
        CombatAiAssessment assessment,
        CombatObjective objective)
    {
        bool stableFrontline = assessment.GetValue("AllyFragility") < 18f && assessment.GetValue("OwnStoneThreat") < 16f;
        return objective switch
        {
            CombatObjective.SupportAlly when context.AllyIntel.Count > 0
                && assessment.GetValue("AllyFragility") > 12f => 18f,
            CombatObjective.DefendOwnStone when context.HasOwnStonePosition
                && (assessment.GetValue("OwnStoneThreat") > 18f || assessment.GetValue("AllyFragility") > 20f) => 14f,
            CombatObjective.AttackEnemy when stableFrontline
                && assessment.GetValue("ReachableEnemyValue") > 26f => 6f,
            CombatObjective.DestroyEnemyStone when stableFrontline
                && context.HasEnemyStonePosition
                && assessment.GetValue("EnemyStoneReachability") > 30f => 6f,
            _ => 0f,
        };
    }

    private static float GetRosaryObjectiveSituationAdjustment(
        CombatAiContext context,
        CombatAiAssessment assessment,
        CombatObjective objective)
    {
        bool stableLine = assessment.GetValue("AllyFragility") < 16f && assessment.GetValue("SelfThreat") < 20f;
        return objective switch
        {
            CombatObjective.SupportAlly when context.AllyIntel.Count > 0
                && assessment.GetValue("AllyFragility") > 10f => 20f,
            CombatObjective.Retreat when assessment.GetValue("SelfThreat") > 18f
                || assessment.GetValue("AllyFragility") > 28f => 16f,
            CombatObjective.DefendOwnStone when stableLine
                && assessment.GetValue("OwnStoneThreat") > 18f => 8f,
            CombatObjective.Search when stableLine
                && assessment.GetValue("EnemyLocationConfidence") < 35f => 6f,
            _ => 0f,
        };
    }

    private static void AddObjectiveReasons(CombatAiAssessment assessment, CombatObjective objective, CombatAiScoreBreakdown breakdown)
    {
        switch (objective)
        {
            case CombatObjective.AttackEnemy:
                if (assessment.GetValue("ReachableEnemyValue") > 35f) AddReason(breakdown, CombatAiReasonCode.ReachableEnemyHigh);
                if (assessment.GetValue("TerrainAdvantage") > 20f) AddReason(breakdown, CombatAiReasonCode.TerrainAdvantageHigh);
                break;
            case CombatObjective.DefendOwnStone:
                if (assessment.GetValue("OwnStoneThreat") > 25f) AddReason(breakdown, CombatAiReasonCode.OwnStoneThreatHigh);
                break;
            case CombatObjective.SupportAlly:
                if (assessment.GetValue("AllyFragility") > 25f) AddReason(breakdown, CombatAiReasonCode.AllyFragilityHigh);
                break;
            case CombatObjective.DestroyEnemyStone:
                if (assessment.GetValue("EnemyStoneReachability") > 25f) AddReason(breakdown, CombatAiReasonCode.EnemyStoneReachable);
                break;
            case CombatObjective.Search:
                if (assessment.GetValue("EnemyLocationConfidence") < 30f) AddReason(breakdown, CombatAiReasonCode.EnemyLocationUncertain);
                break;
            case CombatObjective.Retreat:
                if (assessment.GetValue("SelfThreat") > 30f) AddReason(breakdown, CombatAiReasonCode.SelfThreatHigh);
                if (assessment.GetValue("RetreatRouteSafety") > 20f) AddReason(breakdown, CombatAiReasonCode.RetreatRouteSafe);
                break;
        }
    }

    private static float GetObjectiveWeaponScore(
        CombatAiWeaponWeightsProfile weaponWeightsProfile,
        WeaponBase weapon,
        CombatObjective objective)
    {
        WeaponKind kind = weapon != null ? weapon.Kind : WeaponKind.Unarmed;
        if (weaponWeightsProfile != null)
        {
            return weaponWeightsProfile.GetObjectiveWeight(kind, objective);
        }

        return kind switch
        {
            WeaponKind.Sword => objective switch
            {
                CombatObjective.AttackEnemy => 24f,
                CombatObjective.DestroyEnemyStone => 22f,
                CombatObjective.DefendOwnStone => 4f,
                CombatObjective.Search => -4f,
                CombatObjective.SupportAlly => -14f,
                _ => 0f,
            },
            WeaponKind.Shield => objective switch
            {
                CombatObjective.DefendOwnStone => 28f,
                CombatObjective.SupportAlly => 18f,
                CombatObjective.AttackEnemy => 8f,
                CombatObjective.DestroyEnemyStone => 4f,
                CombatObjective.Search => -6f,
                _ => 0f,
            },
            WeaponKind.Wand => objective switch
            {
                CombatObjective.AttackEnemy => 24f,
                CombatObjective.DestroyEnemyStone => 12f,
                CombatObjective.Search => 10f,
                CombatObjective.Retreat => 8f,
                CombatObjective.SupportAlly => -12f,
                CombatObjective.DefendOwnStone => -6f,
                _ => 0f,
            },
            WeaponKind.Grimoire => objective switch
            {
                CombatObjective.AttackEnemy => 22f,
                CombatObjective.DestroyEnemyStone => 16f,
                CombatObjective.Search => 8f,
                CombatObjective.Retreat => 8f,
                CombatObjective.SupportAlly => -10f,
                CombatObjective.DefendOwnStone => -6f,
                _ => 0f,
            },
            WeaponKind.Bible => objective switch
            {
                CombatObjective.SupportAlly => 28f,
                CombatObjective.DefendOwnStone => 20f,
                CombatObjective.AttackEnemy => 2f,
                CombatObjective.DestroyEnemyStone => -4f,
                CombatObjective.Retreat => 6f,
                _ => 0f,
            },
            WeaponKind.Rosary => objective switch
            {
                CombatObjective.SupportAlly => 32f,
                CombatObjective.Retreat => 16f,
                CombatObjective.DefendOwnStone => 12f,
                CombatObjective.AttackEnemy => -4f,
                CombatObjective.DestroyEnemyStone => -16f,
                CombatObjective.Search => 2f,
                _ => 0f,
            },
            _ => 0f,
        };
    }

    private static float GetObjectivePersonalityScore(CombatAiPersonalityProfile personalityProfile, CombatObjective objective)
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

    private static float GetMoveSituationScore(CombatAiAssessment assessment, string code, CombatObjective objective)
    {
        return code switch
        {
            "AdvanceEnemyStone" => assessment.GetValue("EnemyStoneReachability") * 0.6f
                - assessment.GetValue("OwnStoneThreat") * 0.2f
                + GetSearchAdvanceBonus(assessment, objective),
            "ReturnOwnStone" => assessment.GetValue("OwnStoneThreat") * 0.65f + assessment.GetValue("RetreatRouteSafety") * 0.2f,
            "PursueEnemy" => assessment.GetValue("ReachableEnemyValue") * 0.65f + assessment.GetValue("EnemyLocationConfidence") * 0.1f,
            "SupportAlly" => assessment.GetValue("AllyFragility") * 0.65f,
            "TakeHighGround" => GetHighGroundSituationScore(assessment, objective),
            "MoveForest" => assessment.GetValue("RetreatRouteSafety") * 0.3f + assessment.GetValue("TerrainAdvantage") * 0.3f,
            "SearchLastKnown" => (100f - assessment.GetValue("EnemyLocationConfidence")) * 0.45f,
            "HoldPosition" => objective == CombatObjective.DefendOwnStone ? 12f : 2f,
            _ => 0f,
        } + GetMoveObjectiveAlignmentScore(code, objective);
    }

    private static float GetMoveWeaponScore(
        CombatAiWeaponWeightsProfile weaponWeightsProfile,
        WeaponBase weapon,
        string code)
    {
        WeaponKind kind = weapon != null ? weapon.Kind : WeaponKind.Unarmed;
        if (weaponWeightsProfile != null)
        {
            return weaponWeightsProfile.GetMoveWeight(kind, code);
        }

        return kind switch
        {
            WeaponKind.Sword => code switch
            {
                "PursueEnemy" => 22f,
                "AdvanceEnemyStone" => 16f,
                _ => 0f,
            },
            WeaponKind.Shield => code switch
            {
                "ReturnOwnStone" => 22f,
                "SupportAlly" => 20f,
                "PursueEnemy" => 4f,
                _ => 0f,
            },
            WeaponKind.Wand => code switch
            {
                "TakeHighGround" => 20f,
                "MoveForest" => 12f,
                "PursueEnemy" => -10f,
                "AdvanceEnemyStone" => 6f,
                _ => 0f,
            },
            WeaponKind.Grimoire => code switch
            {
                "TakeHighGround" => 10f,
                "MoveForest" => 12f,
                "PursueEnemy" => -4f,
                "AdvanceEnemyStone" => 8f,
                _ => 0f,
            },
            WeaponKind.Bible => code switch
            {
                "SupportAlly" => 24f,
                "ReturnOwnStone" => 12f,
                "MoveForest" => 8f,
                _ => 0f,
            },
            WeaponKind.Rosary => code switch
            {
                "SupportAlly" => 26f,
                "ReturnOwnStone" => 8f,
                "MoveForest" => 10f,
                "PursueEnemy" => -12f,
                _ => 0f,
            },
            _ => 0f,
        };
    }

    private static float GetMovePersonalityScore(CombatAiPersonalityProfile personalityProfile, string code, CombatObjective objective)
    {
        if (personalityProfile == null) return 0f;
        return code switch
        {
            "PursueEnemy" => personalityProfile.Aggression * 14f - personalityProfile.Caution * 4f,
            "AdvanceEnemyStone" => personalityProfile.ObjectiveFocus * 16f + personalityProfile.RiskTolerance * 6f,
            "ReturnOwnStone" => personalityProfile.Caution * 10f,
            "SupportAlly" => personalityProfile.SupportBias * 14f,
            "TakeHighGround" => personalityProfile.PreferredRangeBias * 10f,
            "MoveForest" => personalityProfile.Caution * 8f,
            "SearchLastKnown" => personalityProfile.ExplorationBias * 8f + (objective == CombatObjective.Search ? 4f : 0f),
            _ => 0f,
        };
    }

    private static void AddMoveReasons(string code, CombatAiScoreBreakdown breakdown)
    {
        switch (code)
        {
            case "AdvanceEnemyStone":
                AddReason(breakdown, CombatAiReasonCode.EnemyStoneReachable);
                break;
            case "ReturnOwnStone":
                AddReason(breakdown, CombatAiReasonCode.OwnStoneThreatHigh);
                break;
            case "PursueEnemy":
                AddReason(breakdown, CombatAiReasonCode.ReachableEnemyHigh);
                break;
            case "SupportAlly":
                AddReason(breakdown, CombatAiReasonCode.AllyFragilityHigh);
                break;
            case "TakeHighGround":
                AddReason(breakdown, CombatAiReasonCode.HighGroundAvailable);
                break;
            case "MoveForest":
                AddReason(breakdown, CombatAiReasonCode.ForestAvailable);
                break;
            case "SearchLastKnown":
                AddReason(breakdown, CombatAiReasonCode.EnemyLocationUncertain);
                break;
        }
    }

    private static float GetSkillBaseScore(SkillBase skill, CombatObjective objective)
    {
        if (skill == null) return 0f;
        float score = objective switch
        {
            CombatObjective.AttackEnemy => IsDamageSkill(skill) ? 32f : IsDebuffSkill(skill) ? 16f : 4f,
            CombatObjective.SupportAlly => IsSupportSkill(skill) ? 34f : IsDamageSkill(skill) ? 8f : 4f,
            CombatObjective.DefendOwnStone => IsProtectSkill(skill) ? 26f : IsDamageSkill(skill) ? 18f : 6f,
            CombatObjective.DestroyEnemyStone => IsDamageSkill(skill) ? 26f : 4f,
            CombatObjective.Retreat => IsProtectSkill(skill) || IsHealSkill(skill) ? 20f : 2f,
            CombatObjective.Search => IsMobilitySkill(skill) ? 12f : 4f,
            _ => 4f,
        };
        if (IsBasicAttackSkill(skill))
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
        if (weaponWeightsProfile != null)
        {
            WeaponKind kindFromProfile = weapon.Kind;
            if (IsDamageSkill(skill)) return weaponWeightsProfile.GetDamageSkillWeight(kindFromProfile);
            if (IsProtectSkill(skill)) return weaponWeightsProfile.GetProtectSkillWeight(kindFromProfile);
            if (IsHealSkill(skill)) return weaponWeightsProfile.GetHealSkillWeight(kindFromProfile);
            if (IsBuffSkill(skill)) return weaponWeightsProfile.GetBuffSkillWeight(kindFromProfile);
            if (IsDebuffSkill(skill)) return weaponWeightsProfile.GetDebuffSkillWeight(kindFromProfile);
            if (IsStealthSkill(skill)) return weaponWeightsProfile.GetStealthSkillWeight(kindFromProfile);
            return 0f;
        }

        return weapon.Kind switch
        {
            WeaponKind.Sword => IsDamageSkill(skill) ? 18f : -8f,
            WeaponKind.Shield => IsProtectSkill(skill) ? 24f : IsDamageSkill(skill) ? 4f : 0f,
            WeaponKind.Wand => IsDamageSkill(skill) ? 20f : IsProtectSkill(skill) || IsHealSkill(skill) ? -10f : 0f,
            WeaponKind.Grimoire => IsDebuffSkill(skill) ? 24f : IsDamageSkill(skill) ? 8f : IsStealthSkill(skill) ? 12f : 0f,
            WeaponKind.Bible => IsBuffSkill(skill) || IsProtectSkill(skill) ? 24f : IsDamageSkill(skill) ? 0f : 0f,
            WeaponKind.Rosary => IsHealSkill(skill) ? 28f : IsProtectSkill(skill) ? 10f : IsDamageSkill(skill) ? -6f : 0f,
            _ => 0f,
        };
    }

    private static float GetSkillPersonalityScore(CombatAiPersonalityProfile personalityProfile, SkillBase skill, CombatObjective objective)
    {
        if (personalityProfile == null || skill == null) return 0f;
        if (IsDamageSkill(skill))
        {
            return personalityProfile.Aggression * 10f - personalityProfile.Caution * 2f;
        }

        if (IsSupportSkill(skill))
        {
            return personalityProfile.SupportBias * 12f + personalityProfile.Caution * 4f;
        }

        if (IsMobilitySkill(skill) && objective == CombatObjective.Search)
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
        if (IsHealSkill(skill) || IsBuffSkill(skill) || IsProtectSkill(skill))
        {
            score += assessment.GetValue("AllyFragility") * 0.25f;
        }

        if (IsDamageSkill(skill) || IsDebuffSkill(skill))
        {
            score += assessment.GetValue("ReachableEnemyValue") * 0.25f;
        }

        score += GetObjectiveSkillAlignmentScore(skill, evaluation, assessment, context, objective);

        if (evaluation.HasAreaPreview && evaluation.ResolvedTargets != null && evaluation.ResolvedTargets.Count >= 2)
        {
            score += 10f;
        }

        score += GetSkillTargetScore(context, skill, evaluation.Context);
        return score;
    }

    private static float GetSkillTargetScore(CombatAiContext context, SkillBase skill, SkillExecutionContext skillContext)
    {
        if (skillContext.PrimaryTarget == null) return 0f;

        if (IsHealSkill(skill) || IsBuffSkill(skill) || IsProtectSkill(skill))
        {
            CombatCharacterIntel ally = FindAllyIntel(context, skillContext.PrimaryTarget);
            if (ally.MaxHP <= 0) return 0f;

            float hpRatio = (float)ally.HP / ally.MaxHP;
            float missingHpRatio = 1f - hpRatio;
            if (IsHealSkill(skill) && missingHpRatio <= 0.05f)
            {
                return -80f;
            }

            float score = missingHpRatio * (IsHealSkill(skill) ? 50f : 18f);
            if (HasEnemyNearby(context.EnemyIntel, ally.CurrentPosition, 8f))
            {
                score += IsProtectSkill(skill) ? 24f : 8f;
            }

            score += GetSupportTargetAffinityScore(skill, ally);
            if (HasEquivalentEffect(skill, ally.StatusEffects))
            {
                score -= 70f;
            }

            return score;
        }

        if (IsDamageSkill(skill) || IsDebuffSkill(skill))
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

        if (evaluation.Context.PrimaryTarget != null && evaluation.CanUse)
        {
            AddReason(breakdown, CombatAiReasonCode.TargetInSkillRange);
        }

        if (evaluation.HasAreaPreview && evaluation.ResolvedTargets != null && evaluation.ResolvedTargets.Count >= 2)
        {
            AddReason(breakdown, CombatAiReasonCode.SkillAreaHitsMultiple);
        }
    }

    private static List<SkillExecutionContext> BuildSkillContexts(CombatAiContext context, Character owner, SkillBase skill)
    {
        var contexts = new List<SkillExecutionContext>();
        if (owner == null || skill == null)
        {
            contexts.Add(SkillExecutionContext.None);
            return contexts;
        }

        switch (skill.TargetKind)
        {
            case SkillTargetKind.None:
                contexts.Add(SkillExecutionContext.None);
                break;
            case SkillTargetKind.Self:
                contexts.Add(SkillExecutionContext.ForSelf(owner));
                break;
            case SkillTargetKind.Enemy:
                AddEnemyTargetContexts(context, contexts);
                break;
            case SkillTargetKind.Ally:
                AddAllyTargetContexts(context, contexts, owner, includeSelf: false);
                break;
            case SkillTargetKind.AllyOrSelf:
                AddAllyTargetContexts(context, contexts, owner, includeSelf: true);
                break;
            case SkillTargetKind.Point:
                AddPointTargetContexts(context, owner, contexts);
                break;
            case SkillTargetKind.Area:
                AddAreaTargetContexts(context, owner, skill, contexts);
                break;
            case SkillTargetKind.RecognizedEnemies:
                contexts.Add(CombatSkillTargeting.CreateRecognizedEnemiesContext(owner));
                break;
            case SkillTargetKind.AllAllies:
                contexts.Add(CombatSkillTargeting.CreateAllAlliesContext(owner, includeSelf: true));
                break;
            default:
                contexts.Add(SkillExecutionContext.None);
                break;
        }

        if (contexts.Count == 0)
        {
            contexts.Add(SkillExecutionContext.None);
        }

        return contexts;
    }

    private static void AddEnemyTargetContexts(CombatAiContext context, List<SkillExecutionContext> contexts)
    {
        for (int i = 0; i < context.EnemyIntel.Count; i++)
        {
            CombatCharacterIntel enemy = context.EnemyIntel[i];
            if (enemy.Character == null || !enemy.HasKnownPosition) continue;

            AddUniqueTargetContext(contexts, enemy.Character);
        }
    }

    private static void AddAllyTargetContexts(
        CombatAiContext context,
        List<SkillExecutionContext> contexts,
        Character owner,
        bool includeSelf)
    {
        for (int i = 0; i < context.AllyIntel.Count; i++)
        {
            CombatCharacterIntel ally = context.AllyIntel[i];
            if (ally.Character == null) continue;

            AddUniqueTargetContext(contexts, ally.Character);
        }

        if (includeSelf)
        {
            AddUniqueTargetContext(contexts, owner);
        }
    }

    private static void AddPointTargetContexts(
        CombatAiContext context,
        Character owner,
        List<SkillExecutionContext> contexts)
    {
        for (int i = 0; i < context.EnemyIntel.Count; i++)
        {
            CombatCharacterIntel enemy = context.EnemyIntel[i];
            if (!enemy.HasKnownPosition) continue;

            AddUniquePointContext(contexts, enemy.KnownPosition);
        }

        if (context.HasEnemyStonePosition)
        {
            AddUniquePointContext(contexts, context.EnemyStonePosition);
        }

        if (contexts.Count == 0 && owner != null)
        {
            AddUniquePointContext(contexts, owner.transform.position);
        }
    }

    private static void AddAreaTargetContexts(
        CombatAiContext context,
        Character owner,
        SkillBase skill,
        List<SkillExecutionContext> contexts)
    {
        if (skill == null)
        {
            return;
        }

        for (int i = 0; i < context.EnemyIntel.Count; i++)
        {
            CombatCharacterIntel enemy = context.EnemyIntel[i];
            if (!enemy.HasKnownPosition) continue;

            AddUniqueAreaContext(contexts, owner, enemy.KnownPosition, skill.AreaRadius);
        }

        if (context.HasEnemyStonePosition)
        {
            AddUniqueAreaContext(contexts, owner, context.EnemyStonePosition, skill.AreaRadius);
        }

        if (contexts.Count == 0 && owner != null)
        {
            AddUniqueAreaContext(contexts, owner, owner.transform.position, skill.AreaRadius);
        }
    }

    private static void AddUniqueTargetContext(List<SkillExecutionContext> contexts, Character target)
    {
        if (target == null) return;

        for (int i = 0; i < contexts.Count; i++)
        {
            if (contexts[i].PrimaryTarget == target && !contexts[i].HasTargetPoint)
            {
                return;
            }
        }

        contexts.Add(SkillExecutionContext.ForTarget(target));
    }

    private static void AddUniquePointContext(List<SkillExecutionContext> contexts, Vector3 point)
    {
        for (int i = 0; i < contexts.Count; i++)
        {
            if (contexts[i].HasTargetPoint && HorizontalDistance(contexts[i].TargetPoint, point) <= 0.1f)
            {
                return;
            }
        }

        contexts.Add(SkillExecutionContext.ForPoint(point));
    }

    private static void AddUniqueAreaContext(
        List<SkillExecutionContext> contexts,
        Character owner,
        Vector3 point,
        float radius)
    {
        for (int i = 0; i < contexts.Count; i++)
        {
            if (contexts[i].HasTargetPoint && HorizontalDistance(contexts[i].TargetPoint, point) <= 0.1f)
            {
                return;
            }
        }

        contexts.Add(CombatSkillTargeting.CreateEnemyAreaContext(owner, point, radius));
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

    private static CombatMoveTarget CreateBestEnemyTarget(CombatAiContext context)
    {
        Character enemy = FindBestEnemyCharacter(context);
        return enemy != null ? CombatMoveTarget.ForCharacter(enemy) : CombatMoveTarget.None;
    }

    private static CombatMoveTarget CreateBestAllyTarget(CombatAiContext context)
    {
        Character ally = FindBestAllyCharacter(context);
        return ally != null ? CombatMoveTarget.ForCharacter(ally) : CombatMoveTarget.None;
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

    private static Character FindBestEnemyCharacter(CombatAiContext context)
    {
        Character best = null;
        float bestScore = float.NegativeInfinity;
        for (int i = 0; i < context.EnemyIntel.Count; i++)
        {
            CombatCharacterIntel enemy = context.EnemyIntel[i];
            if (enemy.Character == null || !enemy.HasKnownPosition) continue;
            float hpRatio = enemy.MaxHP > 0 ? (float)enemy.HP / enemy.MaxHP : 1f;
            float score = (1f - hpRatio) * 60f + (enemy.HasDirectSight ? 25f : enemy.HasMemory ? 10f : 0f);
            if (score <= bestScore) continue;
            bestScore = score;
            best = enemy.Character;
        }

        return best;
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

    private static Vector3 ResolveSkillPoint(CombatAiContext context, Character owner)
    {
        for (int i = 0; i < context.EnemyIntel.Count; i++)
        {
            CombatCharacterIntel enemy = context.EnemyIntel[i];
            if (enemy.HasKnownPosition) return enemy.KnownPosition;
        }

        if (context.HasEnemyStonePosition) return context.EnemyStonePosition;
        return owner != null ? owner.transform.position : default;
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

    private static float GetMoveObjectiveAlignmentScore(string code, CombatObjective objective)
    {
        return objective switch
        {
            CombatObjective.DestroyEnemyStone when code == "AdvanceEnemyStone" => 42f,
            CombatObjective.DefendOwnStone when code == "ReturnOwnStone" => 42f,
            CombatObjective.AttackEnemy when code == "PursueEnemy" => 40f,
            CombatObjective.SupportAlly when code == "SupportAlly" => 42f,
            CombatObjective.Search when code == "SearchLastKnown" => 34f,
            CombatObjective.Retreat when code == "ReturnOwnStone" || code == "MoveForest" => 24f,
            _ => 0f,
        };
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
            CombatObjective.AttackEnemy when IsDamageSkill(skill) => 18f,
            CombatObjective.AttackEnemy when IsDebuffSkill(skill) => 14f,
            CombatObjective.SupportAlly when IsHealSkill(skill) => 24f,
            CombatObjective.SupportAlly when IsBuffSkill(skill) || IsProtectSkill(skill) => 18f,
            CombatObjective.DefendOwnStone when IsProtectSkill(skill) => 20f,
            CombatObjective.DefendOwnStone when IsDamageSkill(skill) => assessment.GetValue("OwnStoneThreat") * 0.2f,
            CombatObjective.Retreat when IsHealSkill(skill) || IsProtectSkill(skill) || IsStealthSkill(skill) => 20f,
            CombatObjective.Search when IsStealthSkill(skill) || IsMobilitySkill(skill) => 16f,
            CombatObjective.DestroyEnemyStone when IsSupportSkill(skill) && assessment.GetValue("AllyFragility") < 20f => -20f,
            CombatObjective.DestroyEnemyStone when IsDamageSkill(skill) && context.VisibleEnemies.Count > 0 => 6f,
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

    private static bool IsBasicAttackSkill(SkillBase skill)
    {
        return skill != null && skill.Name == "通常攻撃";
    }

    private static bool IsDamageSkill(SkillBase skill)
    {
        if (skill == null) return false;
        string code = skill is IdentifiedSkill identified ? identified.SkillId.ToString() : skill.GetType().Name;
        return IsBasicAttackSkill(skill)
            || code.Contains("Bolt")
            || code.Contains("Blast")
            || code.Contains("Slash")
            || code.Contains("Smite")
            || code.Contains("Strike")
            || code.Contains("Thunder");
    }

    private static bool IsBuffSkill(SkillBase skill)
    {
        if (skill == null) return false;
        string code = skill is IdentifiedSkill identified ? identified.SkillId.ToString() : skill.GetType().Name;
        return code.Contains("Buff") || code.Contains("Invulnerable") || code.Contains("Gotsume");
    }

    private static bool IsDebuffSkill(SkillBase skill)
    {
        if (skill == null) return false;
        string code = skill is IdentifiedSkill identified ? identified.SkillId.ToString() : skill.GetType().Name;
        return code.Contains("Debuff") || code.Contains("Poison") || code.Contains("Bind");
    }

    private static bool IsHealSkill(SkillBase skill)
    {
        if (skill == null) return false;
        string code = skill is IdentifiedSkill identified ? identified.SkillId.ToString() : skill.GetType().Name;
        return code.Contains("Heal") || code.Contains("Regeneration") || code.Contains("HealingArea");
    }

    private static bool IsProtectSkill(SkillBase skill)
    {
        if (skill == null) return false;
        string code = skill is IdentifiedSkill identified ? identified.SkillId.ToString() : skill.GetType().Name;
        return code.Contains("Invulnerable") || code.Contains("Gotsume") || code.Contains("ShoulderGuard");
    }

    private static bool IsMobilitySkill(SkillBase skill)
    {
        if (skill == null) return false;
        string code = skill is IdentifiedSkill identified ? identified.SkillId.ToString() : skill.GetType().Name;
        return code.Contains("CarryRush");
    }

    private static bool IsStealthSkill(SkillBase skill)
    {
        if (skill == null) return false;
        string code = skill is IdentifiedSkill identified ? identified.SkillId.ToString() : skill.GetType().Name;
        return code.Contains("Stealth");
    }

    private static bool IsSupportSkill(SkillBase skill)
    {
        return IsBuffSkill(skill) || IsHealSkill(skill) || IsProtectSkill(skill);
    }
}
