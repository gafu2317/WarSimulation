using System.Collections.Generic;
using UnityEngine;

public static class CombatAiPlanner
{
    private const float UnselectableScore = -100000f;

    public static CombatAiPlan BuildPlan(CombatAiContext context, CombatAiPersonalityProfile personalityProfile)
    {
        CombatAiDebugSnapshot snapshot = BuildDebugSnapshot(context, personalityProfile);
        return snapshot != null ? snapshot.FinalPlan : CombatAiPlan.None;
    }

    public static CombatAiDebugSnapshot BuildDebugSnapshot(CombatAiContext context, CombatAiPersonalityProfile personalityProfile)
    {
        if (context == null || context.Owner == null)
        {
            return null;
        }

        var snapshot = new CombatAiDebugSnapshot
        {
            Owner = context.Owner,
            Context = context,
            ContextSummary = CombatAiAssessmentBuilder.BuildSummary(context, personalityProfile),
            Assessment = CombatAiAssessmentBuilder.Build(context),
        };

        BuildObjectiveEntries(snapshot, personalityProfile);
        snapshot.SelectedObjective = SelectHighest(snapshot.ObjectiveEntries);
        BuildMoveEntries(snapshot, personalityProfile);
        snapshot.SelectedMove = SelectHighest(snapshot.MoveEntries);
        BuildSkillEntries(snapshot, personalityProfile);
        snapshot.SelectedSkill = SelectHighest(snapshot.SkillEntries);

        snapshot.FinalPlan = new CombatAiPlan(
            snapshot.SelectedObjective != null ? snapshot.SelectedObjective.Objective : CombatObjective.Search,
            snapshot.SelectedMove != null ? snapshot.SelectedMove.Target : CombatMoveTarget.None,
            snapshot.SelectedSkill != null ? snapshot.SelectedSkill.Skill : null,
            snapshot.SelectedSkill != null ? snapshot.SelectedSkill.SkillContext : SkillExecutionContext.None);
        return snapshot;
    }

    private static void BuildObjectiveEntries(CombatAiDebugSnapshot snapshot, CombatAiPersonalityProfile personalityProfile)
    {
        snapshot.ObjectiveEntries.Clear();
        foreach (CombatObjective objective in System.Enum.GetValues(typeof(CombatObjective)))
        {
            var breakdown = new CombatAiScoreBreakdown
            {
                BaseScore = GetObjectiveBaseScore(objective),
                SituationScore = GetObjectiveSituationScore(snapshot.Assessment, objective),
                WeaponScore = GetObjectiveWeaponScore(snapshot.Owner.EquippedWeapon, objective),
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

    private static void BuildMoveEntries(CombatAiDebugSnapshot snapshot, CombatAiPersonalityProfile personalityProfile)
    {
        snapshot.MoveEntries.Clear();
        CombatObjective objective = snapshot.SelectedObjective != null ? snapshot.SelectedObjective.Objective : CombatObjective.Search;
        AddMoveCandidate(snapshot, personalityProfile, "AdvanceEnemyStone", "敵魔石へ前進", CreateEnemyStoneTarget(snapshot.Context), objective);
        AddMoveCandidate(snapshot, personalityProfile, "ReturnOwnStone", "自軍魔石へ戻る", CreateOwnStoneTarget(snapshot.Context), objective);
        AddMoveCandidate(snapshot, personalityProfile, "PursueEnemy", "敵へ接近", CreateBestEnemyTarget(snapshot.Context), objective);
        AddMoveCandidate(snapshot, personalityProfile, "SupportAlly", "味方へ接近", CreateBestAllyTarget(snapshot.Context), objective);
        AddMoveCandidate(snapshot, personalityProfile, "TakeHighGround", "高所へ移動", CreateNearestPositionTarget(snapshot.Owner, snapshot.Context.HighGroundCandidates), objective);
        AddMoveCandidate(snapshot, personalityProfile, "MoveForest", "森へ移動", CreateNearestPositionTarget(snapshot.Owner, snapshot.Context.ForestCandidates), objective);
        AddMoveCandidate(snapshot, personalityProfile, "SearchLastKnown", "最終既知地点へ移動", CreateLastKnownEnemyTarget(snapshot.Context), objective);
        AddMoveCandidate(snapshot, personalityProfile, "HoldPosition", "待機", CombatMoveTarget.None, objective);
    }

    private static void BuildSkillEntries(CombatAiDebugSnapshot snapshot, CombatAiPersonalityProfile personalityProfile)
    {
        snapshot.SkillEntries.Clear();
        IReadOnlyList<SkillBase> skills = snapshot.Owner.AvailableCombatSkills;
        for (int i = 0; i < skills.Count; i++)
        {
            SkillBase skill = skills[i];
            AddSkillEntries(snapshot, personalityProfile, skill);
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
            WeaponScore = GetMoveWeaponScore(snapshot.Owner.EquippedWeapon, code),
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

    private static void AddSkillEntries(CombatAiDebugSnapshot snapshot, CombatAiPersonalityProfile personalityProfile, SkillBase skill)
    {
        List<SkillExecutionContext> contexts = BuildSkillContexts(snapshot.Context, snapshot.Owner, skill);
        for (int i = 0; i < contexts.Count; i++)
        {
            AddSkillEntry(snapshot, personalityProfile, skill, contexts[i], i);
        }
    }

    private static void AddSkillEntry(
        CombatAiDebugSnapshot snapshot,
        CombatAiPersonalityProfile personalityProfile,
        SkillBase skill,
        SkillExecutionContext context,
        int contextIndex)
    {
        CombatSkillEvaluationResult evaluation = CombatSkillEvaluator.Evaluate(snapshot.Owner, skill, context);
        CombatObjective objective = snapshot.SelectedObjective != null ? snapshot.SelectedObjective.Objective : CombatObjective.Search;
        var breakdown = new CombatAiScoreBreakdown
        {
            BaseScore = GetSkillBaseScore(skill, objective),
            WeaponScore = GetSkillWeaponScore(snapshot.Owner.EquippedWeapon, skill),
            PersonalityScore = GetSkillPersonalityScore(personalityProfile, skill, objective),
            SituationScore = GetSkillSituationScore(snapshot.Context, snapshot.Assessment, skill, evaluation),
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

    private static float GetObjectiveSituationScore(CombatAiAssessment assessment, CombatObjective objective)
    {
        return objective switch
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
                - assessment.GetValue("SelfThreat") * 0.2f,
            CombatObjective.Search => (100f - assessment.GetValue("EnemyLocationConfidence")) * 0.55f
                + assessment.GetValue("TerrainAdvantage") * 0.2f,
            CombatObjective.Retreat => assessment.GetValue("SelfThreat") * 0.9f
                + assessment.GetValue("RetreatRouteSafety") * 0.3f
                + assessment.GetValue("AllyFragility") * 0.1f,
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

    private static float GetObjectiveWeaponScore(WeaponBase weapon, CombatObjective objective)
    {
        WeaponKind kind = weapon != null ? weapon.Kind : WeaponKind.Unarmed;
        return kind switch
        {
            WeaponKind.Sword => objective switch
            {
                CombatObjective.AttackEnemy => 18f,
                CombatObjective.DestroyEnemyStone => 12f,
                CombatObjective.SupportAlly => -8f,
                _ => 0f,
            },
            WeaponKind.Shield => objective switch
            {
                CombatObjective.DefendOwnStone => 18f,
                CombatObjective.SupportAlly => 6f,
                CombatObjective.AttackEnemy => 8f,
                _ => 0f,
            },
            WeaponKind.Wand => objective switch
            {
                CombatObjective.AttackEnemy => 14f,
                CombatObjective.Search => 8f,
                CombatObjective.Retreat => 6f,
                _ => 0f,
            },
            WeaponKind.Grimoire => objective switch
            {
                CombatObjective.AttackEnemy => 14f,
                CombatObjective.DestroyEnemyStone => 8f,
                CombatObjective.Search => 6f,
                _ => 0f,
            },
            WeaponKind.Bible => objective switch
            {
                CombatObjective.SupportAlly => 18f,
                CombatObjective.DefendOwnStone => 10f,
                CombatObjective.AttackEnemy => 4f,
                _ => 0f,
            },
            WeaponKind.Rosary => objective switch
            {
                CombatObjective.SupportAlly => 16f,
                CombatObjective.Retreat => 10f,
                CombatObjective.Search => 6f,
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
            "AdvanceEnemyStone" => assessment.GetValue("EnemyStoneReachability") * 0.6f - assessment.GetValue("OwnStoneThreat") * 0.2f,
            "ReturnOwnStone" => assessment.GetValue("OwnStoneThreat") * 0.65f + assessment.GetValue("RetreatRouteSafety") * 0.2f,
            "PursueEnemy" => assessment.GetValue("ReachableEnemyValue") * 0.65f + assessment.GetValue("EnemyLocationConfidence") * 0.1f,
            "SupportAlly" => assessment.GetValue("AllyFragility") * 0.65f,
            "TakeHighGround" => assessment.GetValue("TerrainAdvantage") * 0.8f,
            "MoveForest" => assessment.GetValue("RetreatRouteSafety") * 0.3f + assessment.GetValue("TerrainAdvantage") * 0.3f,
            "SearchLastKnown" => (100f - assessment.GetValue("EnemyLocationConfidence")) * 0.45f,
            "HoldPosition" => objective == CombatObjective.DefendOwnStone ? 12f : 2f,
            _ => 0f,
        };
    }

    private static float GetMoveWeaponScore(WeaponBase weapon, string code)
    {
        WeaponKind kind = weapon != null ? weapon.Kind : WeaponKind.Unarmed;
        return kind switch
        {
            WeaponKind.Sword => code switch
            {
                "PursueEnemy" => 18f,
                "TakeHighGround" => 6f,
                _ => 0f,
            },
            WeaponKind.Shield => code switch
            {
                "ReturnOwnStone" => 14f,
                "SupportAlly" => 10f,
                _ => 0f,
            },
            WeaponKind.Wand => code switch
            {
                "TakeHighGround" => 18f,
                "MoveForest" => 6f,
                "PursueEnemy" => -8f,
                _ => 0f,
            },
            WeaponKind.Grimoire => code switch
            {
                "TakeHighGround" => 8f,
                "PursueEnemy" => 4f,
                _ => 0f,
            },
            WeaponKind.Bible => code switch
            {
                "SupportAlly" => 16f,
                "ReturnOwnStone" => 6f,
                _ => 0f,
            },
            WeaponKind.Rosary => code switch
            {
                "SupportAlly" => 14f,
                "MoveForest" => 6f,
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

    private static float GetSkillWeaponScore(WeaponBase weapon, SkillBase skill)
    {
        if (weapon == null || skill == null) return 0f;
        return weapon.Kind switch
        {
            WeaponKind.Sword => IsDamageSkill(skill) ? 10f : 0f,
            WeaponKind.Shield => IsProtectSkill(skill) ? 12f : IsDamageSkill(skill) ? 4f : 0f,
            WeaponKind.Wand => IsDamageSkill(skill) ? 12f : 0f,
            WeaponKind.Grimoire => IsDebuffSkill(skill) || IsDamageSkill(skill) ? 10f : 0f,
            WeaponKind.Bible => IsBuffSkill(skill) || IsProtectSkill(skill) ? 12f : IsDamageSkill(skill) ? 3f : 0f,
            WeaponKind.Rosary => IsHealSkill(skill) ? 14f : IsDamageSkill(skill) ? 4f : 0f,
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
        CombatSkillEvaluationResult evaluation)
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
            float score = (1f - hpRatio) * 24f;
            if (HasEnemyNearby(context.EnemyIntel, ally.CurrentPosition, 8f))
            {
                score += 8f;
            }

            return score;
        }

        if (IsDamageSkill(skill) || IsDebuffSkill(skill))
        {
            CombatCharacterIntel enemy = FindEnemyIntel(context, skillContext.PrimaryTarget);
            if (enemy.Character == null) return 0f;

            float hpRatio = enemy.MaxHP > 0 ? (float)enemy.HP / enemy.MaxHP : 1f;
            return (1f - hpRatio) * 18f + (enemy.HasDirectSight ? 8f : enemy.HasMemory ? 3f : 0f);
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
            case SkillTargetKind.AllEnemies:
                contexts.Add(CombatSkillTargeting.CreateAllEnemiesContext(owner));
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
        for (int i = 0; i < context.EnemyIntel.Count; i++)
        {
            CombatCharacterIntel enemy = context.EnemyIntel[i];
            if (enemy.HasKnownPosition)
            {
                return CombatMoveTarget.ForPosition(enemy.KnownPosition);
            }
        }

        return CombatMoveTarget.None;
    }

    private static CombatMoveTarget CreateNearestPositionTarget(Character owner, IReadOnlyList<Vector3> positions)
    {
        if (owner == null || positions == null || positions.Count == 0) return CombatMoveTarget.None;

        float bestDistance = float.PositiveInfinity;
        Vector3 best = default;
        bool found = false;
        for (int i = 0; i < positions.Count; i++)
        {
            float distance = HorizontalDistance(owner.transform.position, positions[i]);
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
                statusEffects: System.Array.Empty<CombatStatusEffectSnapshot>(),
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

    private static bool IsSupportSkill(SkillBase skill)
    {
        return IsBuffSkill(skill) || IsHealSkill(skill) || IsProtectSkill(skill);
    }
}
