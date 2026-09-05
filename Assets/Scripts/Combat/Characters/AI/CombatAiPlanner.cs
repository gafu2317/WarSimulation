using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;

public static partial class CombatAiPlanner
{
    internal const float EmergencyRetreatTriggerHpRatio = 0.15f;
    internal const float EmergencyRetreatReleaseHpRatio = 0.5f;
    private const float RosaryPreferredSupportDistance = 5.5f;
    private const float RosaryCloseHealDistance = 2.5f;
    private const float RosaryEnemyClearanceDistance = 6.5f;
    private const float StandoffRangeSlack = 0.75f;
    private const float StandoffEnemyClearanceDistance = 7f;
    private static readonly ProfilerMarker BuildAssessmentMarker = new("CombatAI.BuildAssessment");
    private static readonly ProfilerMarker SelectStateMarker = new("CombatAI.SelectState");
    private static readonly ProfilerMarker BuildStatePlanMarker = new("CombatAI.BuildStatePlan");

    [System.ThreadStatic] private static List<SkillExecutionContext> s_skillContextsBuffer;

    private static List<SkillExecutionContext> SkillContextsBuffer =>
        s_skillContextsBuffer ??= new List<SkillExecutionContext>();

    public static CombatAiPlan BuildPlan(
        CombatAiContext context,
        CombatAiPersonalityProfile personalityProfile,
        Character focusEnemy = null,
        float focusCommitmentRemainingSeconds = 0f,
        CombatObjective previousObjective = CombatObjective.Search,
        List<CombatAiReasonCode> selectedStateReasons = null,
        CombatMoveTarget previousMoveTarget = default,
        bool hasReachedHighGround = false)
    {
        if (context == null || context.Owner == null) return CombatAiPlan.None;

        CombatAiAssessment assessment;
        using (BuildAssessmentMarker.Auto()) assessment = CombatAiAssessmentBuilder.Build(context);

        CombatAiReasonCode reason;
        CombatObjective state;
        using (SelectStateMarker.Auto()) state = SelectState(
            context,
            assessment,
            personalityProfile,
            previousObjective,
            previousMoveTarget,
            hasReachedHighGround,
            out reason);

        selectedStateReasons?.Clear();
        if (reason != CombatAiReasonCode.None) selectedStateReasons?.Add(reason);

        CombatCharacterIntel tagalongLeader = default;
        if (personalityProfile != null && personalityProfile.Kind == CombatAiPersonalityKind.Tagalong)
        {
            CombatAiPersonalityBehavior.TryFindAssignedAllyWithObjective(context, out tagalongLeader);
        }

        CombatCharacterIntel revengeTarget = default;
        if (personalityProfile != null && personalityProfile.Kind == CombatAiPersonalityKind.Avenger)
        {
            CombatAiPersonalityBehavior.TryFindKnownRecentAttacker(context, out revengeTarget);
        }

        using (BuildStatePlanMarker.Auto())
        {
            return BuildPlanForState(
                context,
                personalityProfile,
                focusEnemy,
                focusCommitmentRemainingSeconds,
                previousObjective,
                previousMoveTarget,
                state,
                reason,
                tagalongLeader,
                revengeTarget,
                hasReachedHighGround);
        }
    }

    public static CombatAiDebugSnapshot BuildDebugSnapshot(
        CombatAiContext context,
        CombatAiPersonalityProfile personalityProfile,
        Character focusEnemy = null,
        float focusCommitmentRemainingSeconds = 0f,
        CombatObjective previousObjective = CombatObjective.Search,
        CombatMoveTarget previousMoveTarget = default,
        bool hasReachedHighGround = false)
    {
        if (context == null || context.Owner == null) return null;

        CombatAiAssessment assessment = CombatAiAssessmentBuilder.Build(context);
        CombatAiPlan plan = BuildPlan(
            context,
            personalityProfile,
            focusEnemy,
            focusCommitmentRemainingSeconds,
            previousObjective,
            null,
            previousMoveTarget,
            hasReachedHighGround);
        return new CombatAiDebugSnapshot
        {
            Owner = context.Owner,
            Context = context,
            Assessment = assessment,
            PreviousState = previousObjective,
            Plan = plan,
        };
    }

    private static CombatObjective SelectState(
        CombatAiContext context,
        CombatAiAssessment assessment,
        CombatAiPersonalityProfile personality,
        CombatObjective previousObjective,
        CombatMoveTarget previousMoveTarget,
        bool hasReachedHighGround,
        out CombatAiReasonCode reason)
    {
        CombatAiPersonalityKind personalityKind = personality != null
            ? personality.Kind
            : CombatAiPersonalityKind.Neutral;

        if (ShouldSelectEmergencyRetreat(context, previousObjective, previousMoveTarget))
        {
            reason = CombatAiReasonCode.EmergencyRetreat;
            return CombatObjective.EmergencyRetreat;
        }

        if (personalityKind == CombatAiPersonalityKind.Gatekeeper && context.HasOwnStonePosition)
        {
            reason = CombatAiReasonCode.PersonalityPreference;
            return CombatObjective.DefendOwnStone;
        }

        if (personalityKind == CombatAiPersonalityKind.Avenger &&
            CombatAiPersonalityBehavior.TryFindKnownRecentAttacker(context, out _))
        {
            reason = CombatAiReasonCode.PersonalityPreference;
            return CombatObjective.AttackEnemy;
        }

        if (personalityKind == CombatAiPersonalityKind.Tagalong &&
            CombatAiPersonalityBehavior.TryFindAssignedAllyWithObjective(context, out CombatCharacterIntel leader))
        {
            reason = CombatAiReasonCode.PersonalityPreference;
            return leader.Objective;
        }

        if (personalityKind == CombatAiPersonalityKind.Reckless && HasLivingEnemyStone(context))
        {
            reason = CombatAiReasonCode.PersonalityPreference;
            return CombatObjective.DestroyEnemyStone;
        }

        if (personalityKind == CombatAiPersonalityKind.BattleJunkie)
        {
            if (HasAttackTarget(context, allowRemembered: true))
            {
                reason = CombatAiReasonCode.PersonalityPreference;
                return CombatObjective.AttackEnemy;
            }

            if (HasLivingEnemyStone(context))
            {
                reason = CombatAiReasonCode.PersonalityPreference;
                return CombatObjective.DestroyEnemyStone;
            }
        }

        if (personalityKind == CombatAiPersonalityKind.Lonely &&
            !CombatAiPersonalityBehavior.HasNearbyAlly(context, CombatAiPersonalityBehavior.LonelyNearbyAllyRadius))
        {
            reason = CombatAiReasonCode.PersonalityPreference;
            return CombatObjective.Search;
        }

        if (assessment.GetValue(CombatAiMetricIndex.OwnStoneThreat) > 25f)
        {
            reason = CombatAiReasonCode.OwnStoneThreatHigh;
            return CombatObjective.DefendOwnStone;
        }

        if (HasMarkedStoneAttacker(context))
        {
            reason = CombatAiReasonCode.OwnStoneAttackerMarked;
            return CombatObjective.AttackEnemy;
        }

        if (ShouldSupport(context, assessment, personalityKind))
        {
            reason = personalityKind == CombatAiPersonalityKind.Devoted
                ? CombatAiReasonCode.PersonalityPreference
                : CombatAiReasonCode.AllyFragilityHigh;
            return CombatObjective.SupportAlly;
        }

        if (ShouldAttack(context))
        {
            reason = CombatAiReasonCode.EnemyInRange;
            return CombatObjective.AttackEnemy;
        }

        if (personalityKind == CombatAiPersonalityKind.HighGround &&
            HasLivingEnemyStone(context) &&
            (hasReachedHighGround || IsAtHighGround(context) ||
             previousObjective == CombatObjective.DestroyEnemyStone))
        {
            reason = CombatAiReasonCode.PersonalityPreference;
            return CombatObjective.DestroyEnemyStone;
        }

        if (ShouldSearchBeforeStone(context))
        {
            reason = CombatAiReasonCode.EnemyLocationUncertain;
            return CombatObjective.Search;
        }

        if (HasLivingEnemyStone(context))
        {
            reason = CombatAiReasonCode.EnemyStoneKnown;
            return CombatObjective.DestroyEnemyStone;
        }

        reason = CombatAiReasonCode.EnemyLocationUncertain;
        return CombatObjective.Search;
    }

    private static bool ShouldSelectEmergencyRetreat(
        CombatAiContext context,
        CombatObjective previousObjective,
        CombatMoveTarget previousMoveTarget)
    {
        float hpRatio = GetHealthRatio(context.Owner);
        if (hpRatio >= EmergencyRetreatReleaseHpRatio) return false;

        if (previousObjective == CombatObjective.EmergencyRetreat)
        {
            if (IsEmergencyRetreatRosaryTarget(context, previousMoveTarget)) return true;
            return !IsAtOwnStoneSafetyArea(context);
        }

        return hpRatio <= EmergencyRetreatTriggerHpRatio && !IsAtOwnStoneSafetyArea(context);
    }

    private static float GetHealthRatio(Character owner)
    {
        if (owner == null || owner.Health == null || owner.Health.MaxHP <= 0) return 1f;
        return owner.Health.HP / (float)owner.Health.MaxHP;
    }

    private static bool IsAtOwnStoneSafetyArea(CombatAiContext context)
    {
        return context.HasOwnStonePosition &&
            HorizontalDistance(context.Owner.transform.position, context.OwnStonePosition) <=
            CombatAiAssessmentBuilder.OwnStoneAreaRadius;
    }

    private static bool IsEmergencyRetreatRosaryTarget(
        CombatAiContext context,
        CombatMoveTarget moveTarget)
    {
        if (moveTarget.Kind != CombatMoveTargetKind.Character || moveTarget.TargetCharacter == null) return false;

        CombatCharacterIntel ally = context.FindAllyIntel(moveTarget.TargetCharacter);
        return ally.Character != null && ally.IsAlive && ally.CanAct && ally.WeaponKind == WeaponKind.Rosary;
    }

    private static CombatAiPlan BuildPlanForState(
        CombatAiContext context,
        CombatAiPersonalityProfile personality,
        Character focusEnemy,
        float focusCommitmentRemainingSeconds,
        CombatObjective previousState,
        CombatMoveTarget previousMoveTarget,
        CombatObjective state,
        CombatAiReasonCode reason,
        CombatCharacterIntel tagalongLeader,
        CombatCharacterIntel revengeTarget,
        bool hasReachedHighGround)
    {
        CombatMoveTarget moveTarget = CombatMoveTarget.None;
        string actionCode;
        bool usesHighGround = personality != null && personality.Kind == CombatAiPersonalityKind.HighGround;
        bool isAtHighGround = usesHighGround && IsAtHighGround(context);
        bool highGroundEstablished = usesHighGround && (hasReachedHighGround || isAtHighGround);
        bool hasHighGroundCandidate = usesHighGround && context.HighGroundCandidates.Count > 0;
        bool usesTagalongTarget = personality != null && personality.Kind == CombatAiPersonalityKind.Tagalong &&
            context.MarkedStoneAttacker == null &&
            tagalongLeader.Character != null && tagalongLeader.Objective == state &&
            TryCreateTagalongTarget(context, tagalongLeader, out moveTarget);
        bool usesRevengeTarget = personality != null && personality.Kind == CombatAiPersonalityKind.Avenger &&
            context.MarkedStoneAttacker == null &&
            revengeTarget.Character != null && state == CombatObjective.AttackEnemy;
        CombatMoveTarget highGroundTarget = CombatMoveTarget.None;
        CombatMoveTarget previousHighGroundTarget = usesHighGround
            ? CreatePreviousHighGroundTarget(context, previousMoveTarget)
            : CombatMoveTarget.None;
        bool hasHighGroundMove = usesHighGround && !highGroundEstablished;
        if (hasHighGroundMove)
        {
            highGroundTarget = previousHighGroundTarget;
            if (!highGroundTarget.HasDestination)
            {
                highGroundTarget = CreateBestHighGroundTarget(context);
            }

            hasHighGroundMove = IsUsableMove(context, highGroundTarget);
        }
        if (state == CombatObjective.EmergencyRetreat)
        {
            moveTarget = BuildEmergencyRetreatMove(context, previousState, previousMoveTarget, out actionCode);
        }
        else if (usesRevengeTarget)
        {
            moveTarget = CreateRevengeTarget(context, revengeTarget);
            actionCode = CombatAiMoveCode.PersonalitySignature;
        }
        else if (hasHighGroundMove)
        {
            moveTarget = highGroundTarget;
            actionCode = CombatAiMoveCode.PersonalitySignature;
        }
        else if (hasHighGroundCandidate && !highGroundEstablished)
        {
            actionCode = CombatAiMoveCode.PersonalitySignature;
        }
        else if (usesTagalongTarget)
        {
            actionCode = CombatAiMoveCode.PersonalitySignature;
        }
        else
        {
            switch (state)
            {
                case CombatObjective.DefendOwnStone:
                    moveTarget = BuildDefendMove(
                        context,
                        personality != null && personality.Kind == CombatAiPersonalityKind.Gatekeeper,
                        out actionCode);
                    break;
                case CombatObjective.SupportAlly:
                    moveTarget = BuildSupportMove(context, personality, out actionCode);
                    break;
                case CombatObjective.AttackEnemy:
                    moveTarget = BuildAttackMove(
                        context,
                        focusEnemy,
                        focusCommitmentRemainingSeconds,
                        personality != null && personality.Kind == CombatAiPersonalityKind.BattleJunkie,
                        context.MarkedStoneAttacker,
                        seekHighGround: !usesHighGround,
                        out actionCode);
                    break;
                case CombatObjective.DestroyEnemyStone:
                    moveTarget = BuildDestroyStoneMove(context, personality, previousState, previousMoveTarget, out actionCode);
                    break;
                default:
                    moveTarget = BuildSearchMove(context, personality, out actionCode);
                    break;
            }
        }

        Character preferredTarget = state == CombatObjective.AttackEnemy && context.MarkedStoneAttacker != null
            ? context.MarkedStoneAttacker
            : personality != null && personality.Kind == CombatAiPersonalityKind.Tagalong
                ? tagalongLeader.IntendedTarget
                : personality != null && personality.Kind == CombatAiPersonalityKind.Avenger
                    ? revengeTarget.Character
                    : personality != null && personality.Kind == CombatAiPersonalityKind.Gatekeeper &&
                      state == CombatObjective.DefendOwnStone && moveTarget.TargetCharacter != null
                        ? moveTarget.TargetCharacter
                        : null;
        SelectSkill(
            context,
            personality,
            state,
            preferredTarget,
            out SkillBase skill,
            out SkillExecutionContext skillContext);
        if (isAtHighGround && skill != null &&
            (state == CombatObjective.AttackEnemy ||
             state == CombatObjective.SupportAlly ||
             state == CombatObjective.DestroyEnemyStone))
        {
            moveTarget = CombatMoveTarget.None;
            actionCode = CombatAiMoveCode.PersonalitySignature;
        }
        return new CombatAiPlan(state, moveTarget, skill, skillContext, actionCode, reason);
    }

    private static CombatMoveTarget BuildEmergencyRetreatMove(
        CombatAiContext context,
        CombatObjective previousState,
        CombatMoveTarget previousMoveTarget,
        out string actionCode)
    {
        if (previousState == CombatObjective.EmergencyRetreat &&
            TryReuseEmergencyRetreatTarget(context, previousMoveTarget, out CombatMoveTarget retainedTarget, out actionCode))
        {
            return retainedTarget;
        }

        CombatMoveTarget selected = CombatMoveTarget.None;
        float selectedDistance = float.PositiveInfinity;
        string selectedActionCode = CombatAiMoveCode.HoldPosition;

        CombatMoveTarget ownStone = CreateOwnStoneTarget(context);
        if (IsUsableMove(context, ownStone))
        {
            selected = ownStone;
            selectedDistance = HorizontalDistance(context.Owner.transform.position, ownStone.Destination);
            selectedActionCode = CombatAiMoveCode.ReturnOwnStone;
        }

        for (int i = 0; i < context.AllyIntel.Count; i++)
        {
            CombatCharacterIntel ally = context.AllyIntel[i];
            if (ally.Character == null || !ally.IsAlive || !ally.CanAct || ally.WeaponKind != WeaponKind.Rosary) continue;

            CombatMoveTarget rosary = CombatMoveTarget.ForCharacter(ally.Character);
            if (!IsUsableMove(context, rosary)) continue;

            float distance = HorizontalDistance(context.Owner.transform.position, ally.CurrentPosition);
            if (distance >= selectedDistance) continue;

            selected = rosary;
            selectedDistance = distance;
            selectedActionCode = CombatAiMoveCode.SupportAlly;
        }

        actionCode = selectedActionCode;
        return selected;
    }

    private static bool TryReuseEmergencyRetreatTarget(
        CombatAiContext context,
        CombatMoveTarget previousMoveTarget,
        out CombatMoveTarget retainedTarget,
        out string actionCode)
    {
        retainedTarget = CombatMoveTarget.None;
        actionCode = CombatAiMoveCode.HoldPosition;

        if (previousMoveTarget.Kind == CombatMoveTargetKind.Position && context.HasOwnStonePosition &&
            HorizontalDistance(previousMoveTarget.Destination, context.OwnStonePosition) <= 0.01f)
        {
            CombatMoveTarget ownStone = CreateOwnStoneTarget(context);
            if (!IsUsableMove(context, ownStone)) return false;

            retainedTarget = ownStone;
            actionCode = CombatAiMoveCode.ReturnOwnStone;
            return true;
        }

        if (!IsEmergencyRetreatRosaryTarget(context, previousMoveTarget)) return false;

        CombatMoveTarget rosary = CombatMoveTarget.ForCharacter(previousMoveTarget.TargetCharacter);
        if (!IsUsableMove(context, rosary)) return false;

        retainedTarget = rosary;
        actionCode = CombatAiMoveCode.SupportAlly;
        return true;
    }

    private static CombatMoveTarget BuildDefendMove(
        CombatAiContext context,
        bool guardsOwnStone,
        out string actionCode)
    {
        CombatMoveTarget block = CreateBestBodyBlockTarget(context, includeAllies: !guardsOwnStone);
        if (IsUsableMove(context, block))
        {
            actionCode = CombatAiMoveCode.InterceptThreat;
            return block;
        }

        CombatMoveTarget threat = CreateThreatNearOwnStoneTarget(context);
        if (IsUsableMove(context, threat))
        {
            actionCode = CombatAiMoveCode.PursueEnemy;
            return threat;
        }

        CombatMoveTarget ownStone = CreateOwnStoneTarget(context);
        if (IsUsableMove(context, ownStone))
        {
            actionCode = CombatAiMoveCode.ReturnOwnStone;
            return ownStone;
        }

        actionCode = CombatAiMoveCode.HoldPosition;
        return CombatMoveTarget.None;
    }

    private static CombatMoveTarget BuildSupportMove(
        CombatAiContext context,
        CombatAiPersonalityProfile personality,
        out string actionCode)
    {
        if (personality != null && personality.Kind == CombatAiPersonalityKind.Devoted)
        {
            CombatMoveTarget devoted = CreateDevotedLowHpAllyTarget(context);
            if (IsUsableMove(context, devoted))
            {
                actionCode = CombatAiMoveCode.PersonalitySignature;
                return devoted;
            }
        }

        CombatMoveTarget block = CreateBestBodyBlockTarget(context);
        if (IsUsableMove(context, block))
        {
            actionCode = CombatAiMoveCode.InterceptThreat;
            return block;
        }

        if (GetWeaponKind(context.Owner) == WeaponKind.Bible)
        {
            CombatMoveTarget highGround = CreateBestSupportHighGroundTarget(context);
            if (IsUsableMove(context, highGround))
            {
                actionCode = CombatAiMoveCode.TakeHighGround;
                return highGround;
            }
        }

        CombatMoveTarget ally = CreateBestAllyTarget(context);
        if (IsUsableMove(context, ally))
        {
            actionCode = CombatAiMoveCode.SupportAlly;
            return ally;
        }

        actionCode = CombatAiMoveCode.HoldPosition;
        return CombatMoveTarget.None;
    }

    private static CombatMoveTarget BuildAttackMove(
        CombatAiContext context,
        Character focusEnemy,
        float focusCommitmentRemainingSeconds,
        bool pursueRememberedEnemy,
        Character priorityEnemy,
        bool seekHighGround,
        out string actionCode)
    {
        WeaponKind weaponKind = GetWeaponKind(context.Owner);
        if (seekHighGround && (weaponKind == WeaponKind.Grimoire || weaponKind == WeaponKind.Wand))
        {
            CombatMoveTarget highGround = CreateBestHighGroundTarget(context);
            if (IsUsableMove(context, highGround) && !HasEnemyInReadySkillRange(context))
            {
                actionCode = CombatAiMoveCode.TakeHighGround;
                return highGround;
            }
        }

        CombatMoveTarget enemy = CreateBestEnemyTarget(
            context,
            priorityEnemy,
            focusEnemy,
            focusCommitmentRemainingSeconds,
            pursueRememberedEnemy);
        if (IsUsableMove(context, enemy))
        {
            actionCode = CombatAiMoveCode.PursueEnemy;
            return enemy;
        }

        actionCode = CombatAiMoveCode.HoldPosition;
        return CombatMoveTarget.None;
    }

    private static CombatMoveTarget BuildDestroyStoneMove(
        CombatAiContext context,
        CombatAiPersonalityProfile personality,
        CombatObjective previousState,
        CombatMoveTarget previousMoveTarget,
        out string actionCode)
    {
        if (personality != null && personality.Kind == CombatAiPersonalityKind.Cunning)
        {
            if (previousState == CombatObjective.DestroyEnemyStone && IsUsableMove(context, previousMoveTarget))
            {
                actionCode = previousMoveTarget.HasAssaultRouteKey
                    ? CombatAiMoveCode.AdvanceAssaultRoute
                    : CombatAiMoveCode.AdvanceEnemyStone;
                return previousMoveTarget;
            }

            CombatMoveTarget cunning = CreateCunningLowRiskStoneTarget(context);
            if (IsUsableMove(context, cunning))
            {
                actionCode = cunning.HasAssaultRouteKey
                    ? CombatAiMoveCode.AdvanceAssaultRoute
                    : CombatAiMoveCode.PersonalitySignature;
                return cunning;
            }
        }

        if (personality != null && personality.Kind == CombatAiPersonalityKind.Reckless)
        {
            CombatMoveTarget reckless = CreateEnemyStoneTarget(context);
            if (IsUsableMove(context, reckless))
            {
                actionCode = CombatAiMoveCode.AdvanceEnemyStone;
                return reckless;
            }
        }

        CombatMoveTarget routeTarget = CreateLeastCongestedAssaultTarget(context, out actionCode);
        if (IsUsableMove(context, routeTarget)) return routeTarget;

        CombatMoveTarget stone = CreateEnemyStoneTarget(context);
        if (IsUsableMove(context, stone))
        {
            actionCode = CombatAiMoveCode.AdvanceEnemyStone;
            return stone;
        }

        actionCode = CombatAiMoveCode.HoldPosition;
        return CombatMoveTarget.None;
    }

    private static CombatMoveTarget BuildSearchMove(
        CombatAiContext context,
        CombatAiPersonalityProfile personality,
        out string actionCode)
    {
        if (personality != null)
        {
            CombatMoveTarget signature = personality.Kind switch
            {
                CombatAiPersonalityKind.AttentionSeeker => CreateAttentionSeekerTarget(context),
                CombatAiPersonalityKind.Lonely => CreateLonelyClingTarget(context),
                _ => CombatMoveTarget.None,
            };
            if (IsUsableMove(context, signature))
            {
                actionCode = CombatAiMoveCode.PersonalitySignature;
                return signature;
            }
        }

        CombatMoveTarget lastKnown = CreateLastKnownEnemyTarget(context);
        if (IsUsableMove(context, lastKnown))
        {
            actionCode = CombatAiMoveCode.SearchLastKnown;
            return lastKnown;
        }

        CombatMoveTarget highGround = CreateBestHighGroundTarget(context);
        if (IsUsableMove(context, highGround))
        {
            actionCode = CombatAiMoveCode.TakeHighGround;
            return highGround;
        }

        CombatMoveTarget forest = CreateNearestPositionTarget(context.Owner, context.ForestCandidates);
        if (IsUsableMove(context, forest))
        {
            actionCode = CombatAiMoveCode.MoveForest;
            return forest;
        }

        actionCode = CombatAiMoveCode.HoldPosition;
        return CombatMoveTarget.None;
    }

    private static void SelectSkill(
        CombatAiContext context,
        CombatAiPersonalityProfile personality,
        CombatObjective state,
        Character preferredTarget,
        out SkillBase selectedSkill,
        out SkillExecutionContext selectedContext)
    {
        selectedSkill = null;
        selectedContext = SkillExecutionContext.None;
        int selectedActionPriority = int.MaxValue;
        int selectedTargetPriority = int.MaxValue;
        bool restrictToEnemyStoneCenter = personality != null &&
            personality.Kind == CombatAiPersonalityKind.Reckless &&
            state == CombatObjective.DestroyEnemyStone;
        IReadOnlyList<SkillBase> skills = context.Owner.AvailableCombatSkills;
        for (int i = 0; i < skills.Count; i++)
        {
            SkillBase skill = skills[i];
            if (skill == null ||
                !CanUseSkillInState(context, personality, state, skill)) continue;

            List<SkillExecutionContext> contexts = SkillContextsBuffer;
            CombatAiSkillContextBuilder.Build(context, context.Owner, skill, contexts);
            for (int j = 0; j < contexts.Count; j++)
            {
                CombatSkillEvaluationResult evaluation = CombatSkillEvaluator.Evaluate(context.Owner, skill, contexts[j]);
                if (!evaluation.CanUse || !IsUsefulSkillContext(context, state, skill, evaluation.Context) ||
                    !ContainsPreferredTarget(evaluation.Context, preferredTarget) ||
                    (restrictToEnemyStoneCenter && !IsEnemyStoneFocusedContext(context, evaluation.Context))) continue;

                int actionPriority = GetSkillActionPriority(state, skill, evaluation.Context);
                int targetPriority = GetSkillTargetPriority(context, skill, evaluation.Context);
                if (actionPriority > selectedActionPriority ||
                    actionPriority == selectedActionPriority && targetPriority >= selectedTargetPriority) continue;

                selectedActionPriority = actionPriority;
                selectedTargetPriority = targetPriority;

                selectedSkill = skill;
                selectedContext = evaluation.Context;
            }
        }
    }

    private static bool IsEnemyStoneFocusedContext(CombatAiContext context, SkillExecutionContext skillContext)
    {
        if (skillContext.PrimaryStone != null) return true;
        if (context == null || !context.HasEnemyStonePosition || !skillContext.HasTargetPoint) return false;

        return HorizontalDistance(context.EnemyStonePosition, skillContext.TargetPoint) <= 0.01f;
    }

    private static bool ContainsPreferredTarget(SkillExecutionContext context, Character preferredTarget)
    {
        if (preferredTarget == null) return true;
        if (context.PrimaryTarget == preferredTarget) return true;
        for (int i = 0; i < context.ResolvedTargets.Count; i++)
        {
            if (context.ResolvedTargets[i] == preferredTarget) return true;
        }

        return false;
    }

    private static bool CanUseSkillInState(
        CombatAiContext context,
        CombatAiPersonalityProfile personality,
        CombatObjective state,
        SkillBase skill)
    {
        if (state == CombatObjective.EmergencyRetreat)
        {
            return CombatAiSkillClassifier.IsHeal(skill) ||
                CombatAiSkillClassifier.IsProtect(skill) ||
                CombatAiSkillClassifier.IsStealth(skill);
        }

        if (personality != null && personality.Kind == CombatAiPersonalityKind.Lonely &&
            !CombatAiPersonalityBehavior.HasNearbyAlly(context, CombatAiPersonalityBehavior.LonelyNearbyAllyRadius)) return false;

        if (personality != null && personality.Kind == CombatAiPersonalityKind.Reckless)
            return CombatAiSkillClassifier.IsDamage(skill) && skill.CanTargetMagicStone;

        return state switch
        {
            CombatObjective.EmergencyRetreat => CombatAiSkillClassifier.IsHeal(skill) || CombatAiSkillClassifier.IsProtect(skill) || CombatAiSkillClassifier.IsStealth(skill),
            CombatObjective.SupportAlly => CombatAiSkillClassifier.IsSupport(skill),
            CombatObjective.Search => CombatAiSkillClassifier.IsMobility(skill) || CombatAiSkillClassifier.IsStealth(skill),
            CombatObjective.DestroyEnemyStone => CombatAiSkillClassifier.IsDamage(skill) || CombatAiSkillClassifier.IsBuff(skill),
            CombatObjective.AttackEnemy => CombatAiSkillClassifier.IsDamage(skill) || CombatAiSkillClassifier.IsDebuff(skill) || CombatAiSkillClassifier.IsProtect(skill),
            CombatObjective.DefendOwnStone => CombatAiSkillClassifier.IsDamage(skill) || CombatAiSkillClassifier.IsDebuff(skill) || CombatAiSkillClassifier.IsProtect(skill) || CombatAiSkillClassifier.IsHeal(skill),
            _ => false,
        };
    }

    private static bool IsUsefulSkillContext(
        CombatAiContext context,
        CombatObjective state,
        SkillBase skill,
        SkillExecutionContext skillContext)
    {
        if (CombatAiSkillClassifier.IsDamage(skill))
        {
            if (state == CombatObjective.DestroyEnemyStone && skillContext.PrimaryStone == null && skillContext.ResolvedStones.Count == 0) return false;
            for (int i = 0; i < skillContext.ResolvedTargets.Count; i++)
            {
                Character target = skillContext.ResolvedTargets[i];
                CombatCharacterIntel enemy = context.FindEnemyIntel(target);
                if (enemy.Character != null && enemy.HasKnownPosition &&
                    enemy.HP - context.GetAllyPendingDamage(target) > 0) return true;
            }

            return skillContext.PrimaryStone != null || skillContext.ResolvedStones.Count > 0;
        }

        if (CombatAiSkillClassifier.IsHeal(skill))
        {
            if (skill.TargetKind == SkillTargetKind.Point && skillContext.HasTargetPoint)
            {
                return HasInjuredAllyAtPoint(context, skillContext.TargetPoint, skill.AreaRadius);
            }

            for (int i = 0; i < skillContext.ResolvedTargets.Count; i++)
            {
                Character target = skillContext.ResolvedTargets[i];
                CombatCharacterIntel ally = context.FindAllyIntel(target);
                if (ally.Character == null || ally.MaxHP <= 0) continue;
                int projectedHp = ally.HP + context.GetAllyPendingHealing(target) - context.GetEnemyPendingDamage(target);
                if (projectedHp < ally.MaxHP) return true;
            }

            return false;
        }

        if ((CombatAiSkillClassifier.IsBuff(skill) || CombatAiSkillClassifier.IsDebuff(skill) || CombatAiSkillClassifier.IsProtect(skill)) && HasMatchingStatus(skill, skillContext)) return false;
        return true;
    }

    private static bool HasInjuredAllyAtPoint(CombatAiContext context, Vector3 point, float radius)
    {
        if (context == null) return false;

        for (int i = 0; i < context.AllyIntel.Count; i++)
        {
            if (IsInjuredAllyAtPoint(context, context.AllyIntel[i], point, radius)) return true;
        }

        return IsInjuredOwnerAtPoint(context, point, radius);
    }

    private static bool IsInjuredAllyAtPoint(
        CombatAiContext context,
        CombatCharacterIntel ally,
        Vector3 point,
        float radius)
    {
        if (ally.Character == null || !ally.IsAlive || ally.MaxHP <= 0 ||
            HorizontalDistance(ally.CurrentPosition, point) > radius) return false;

        int projectedHp = ally.HP + context.GetAllyPendingHealing(ally.Character) -
            context.GetEnemyPendingDamage(ally.Character);
        return projectedHp < ally.MaxHP;
    }

    private static bool IsInjuredOwnerAtPoint(CombatAiContext context, Vector3 point, float radius)
    {
        if (context.Owner == null || context.Owner.Health == null || !context.Owner.Health.IsAlive ||
            context.Owner.MaxHP <= 0 || HorizontalDistance(context.Owner.transform.position, point) > radius)
        {
            return false;
        }

        int projectedHp = context.Owner.HP + context.GetAllyPendingHealing(context.Owner) -
            context.GetEnemyPendingDamage(context.Owner);
        return projectedHp < context.Owner.MaxHP;
    }

    private static int GetSkillActionPriority(CombatObjective state, SkillBase skill, SkillExecutionContext skillContext)
    {
        return state switch
        {
            CombatObjective.EmergencyRetreat when CombatAiSkillClassifier.IsProtect(skill) => 0,
            CombatObjective.EmergencyRetreat when CombatAiSkillClassifier.IsHeal(skill) => 1,
            CombatObjective.EmergencyRetreat => 2,
            CombatObjective.SupportAlly when CombatAiSkillClassifier.IsHeal(skill) => 0,
            CombatObjective.SupportAlly when CombatAiSkillClassifier.IsProtect(skill) => 1,
            CombatObjective.SupportAlly => 2,
            CombatObjective.DefendOwnStone when CombatAiSkillClassifier.IsProtect(skill) => 0,
            CombatObjective.DefendOwnStone when CombatAiSkillClassifier.IsDamage(skill) => 1,
            CombatObjective.DefendOwnStone when CombatAiSkillClassifier.IsDebuff(skill) => 2,
            CombatObjective.AttackEnemy when CombatAiSkillClassifier.IsDebuff(skill) => 0,
            CombatObjective.AttackEnemy when CombatAiSkillClassifier.IsDamage(skill) => 1,
            CombatObjective.DestroyEnemyStone when skillContext.PrimaryStone != null || skillContext.ResolvedStones.Count > 0 => 0,
            CombatObjective.DestroyEnemyStone when CombatAiSkillClassifier.IsBuff(skill) => 1,
            _ => 3,
        };
    }

    private static int GetSkillTargetPriority(CombatAiContext context, SkillBase skill, SkillExecutionContext skillContext)
    {
        if (CombatAiSkillClassifier.IsHeal(skill))
        {
            int lowestProjectedHpPercent = 100;
            for (int i = 0; i < skillContext.ResolvedTargets.Count; i++)
            {
                Character target = skillContext.ResolvedTargets[i];
                CombatCharacterIntel ally = context.FindAllyIntel(target);
                if (ally.Character == null || ally.MaxHP <= 0) continue;
                int projectedHp = ally.HP + context.GetAllyPendingHealing(target) - context.GetEnemyPendingDamage(target);
                lowestProjectedHpPercent = Mathf.Min(lowestProjectedHpPercent, projectedHp * 100 / ally.MaxHP);
            }

            return lowestProjectedHpPercent - skillContext.ResolvedTargets.Count;
        }

        if (CombatAiSkillClassifier.IsDamage(skill))
        {
            int lowestProjectedHp = int.MaxValue;
            for (int i = 0; i < skillContext.ResolvedTargets.Count; i++)
            {
                Character target = skillContext.ResolvedTargets[i];
                CombatCharacterIntel enemy = context.FindEnemyIntel(target);
                if (enemy.Character == null || !enemy.HasKnownPosition) continue;
                lowestProjectedHp = Mathf.Min(lowestProjectedHp, enemy.HP - context.GetAllyPendingDamage(target));
            }

            return lowestProjectedHp == int.MaxValue ? 0 : lowestProjectedHp - skillContext.ResolvedTargets.Count;
        }

        return -skillContext.ResolvedTargets.Count;
    }

    private static bool HasMatchingStatus(SkillBase skill, SkillExecutionContext context)
    {
        if (skill is not IdentifiedSkill identified) return false;
        for (int i = 0; i < context.ResolvedTargets.Count; i++)
        {
            Character target = context.ResolvedTargets[i];
            if (target == null || target.StatusEffects == null) continue;
            IReadOnlyList<CombatStatusEffectSnapshot> effects = target.StatusEffects.GetActiveEffectSnapshots();
            for (int j = 0; j < effects.Count; j++)
            {
                if (MatchesEffect(identified.SkillId, effects[j])) return true;
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

    private static bool ShouldSupport(CombatAiContext context, CombatAiAssessment assessment, CombatAiPersonalityKind personalityKind)
    {
        if (!HasLivingAlly(context)) return false;
        if (personalityKind == CombatAiPersonalityKind.Devoted && CombatAiPersonalityBehavior.FindLowestHpAllyInRange(context, CombatAiPersonalityBehavior.DevotedAssistRadius) != null) return true;

        WeaponKind weaponKind = GetWeaponKind(context.Owner);
        float fragility = assessment.GetValue(CombatAiMetricIndex.AllyFragility);
        if (weaponKind == WeaponKind.Shield && HasPreferredEscortAlly(context)) return fragility > 0f;
        if (!HasSupportSkill(context)) return false;
        if (weaponKind == WeaponKind.Rosary) return fragility > 10f;
        if (weaponKind == WeaponKind.Bible) return fragility > 12f;
        return fragility > 25f;
    }

    private static bool ShouldAttack(CombatAiContext context)
    {
        if (!HasAttackTarget(context)) return false;

        WeaponKind kind = GetWeaponKind(context.Owner);
        if (kind == WeaponKind.Sword || kind == WeaponKind.Wand) return HasEnemyInsideEngagementRange(context);
        if (kind == WeaponKind.Shield && HasPreferredEscortAlly(context)) return false;
        return HasVisibleLivingEnemy(context) || !HasLivingEnemyStone(context);
    }

    private static bool ShouldSearchBeforeStone(CombatAiContext context)
    {
        WeaponKind kind = GetWeaponKind(context.Owner);
        return kind == WeaponKind.Grimoire || kind == WeaponKind.Bible || kind == WeaponKind.Rosary;
    }

    private static bool HasEnemyInsideEngagementRange(CombatAiContext context)
    {
        WeaponBase weapon = context.Owner != null ? context.Owner.EquippedWeapon : null;
        float range = weapon == null ? 4f : weapon.Kind == WeaponKind.Wand ? Mathf.Min(weapon.Range, 10f) : Mathf.Max(3.5f, weapon.Range + 2.5f);
        for (int i = 0; i < context.EnemyIntel.Count; i++)
        {
            CombatCharacterIntel enemy = context.EnemyIntel[i];
            if (enemy.IsAlive && enemy.HasDirectSight && enemy.HasKnownPosition &&
                HorizontalDistance(context.Owner.transform.position, enemy.KnownPosition) <= range) return true;
        }

        return false;
    }

    private static bool HasAttackTarget(CombatAiContext context, bool allowRemembered = false)
    {
        for (int i = 0; i < context.EnemyIntel.Count; i++)
        {
            CombatCharacterIntel enemy = context.EnemyIntel[i];
            if (enemy.Character != null && enemy.IsAlive && (enemy.HasDirectSight || allowRemembered) &&
                enemy.HasKnownPosition && enemy.HP - context.GetAllyPendingDamage(enemy.Character) > 0) return true;
        }

        return false;
    }

    private static bool HasMarkedStoneAttacker(CombatAiContext context)
    {
        if (context == null || context.MarkedStoneAttacker == null) return false;

        CombatCharacterIntel marked = context.FindEnemyIntel(context.MarkedStoneAttacker);
        return marked.Character != null && marked.IsAlive && marked.HasKnownPosition;
    }

    private static bool HasVisibleLivingEnemy(CombatAiContext context)
    {
        for (int i = 0; i < context.EnemyIntel.Count; i++)
        {
            if (context.EnemyIntel[i].IsAlive && context.EnemyIntel[i].HasDirectSight) return true;
        }

        return false;
    }

    private static bool HasLivingEnemyStone(CombatAiContext context) =>
        context.HasEnemyStonePosition && (!context.HasEnemyStoneHealth || context.EnemyStoneHP > 0);

    private static bool HasLivingAlly(CombatAiContext context)
    {
        for (int i = 0; i < context.AllyIntel.Count; i++)
        {
            if (context.AllyIntel[i].IsAlive) return true;
        }

        return false;
    }

    private static bool HasPreferredEscortAlly(CombatAiContext context)
    {
        for (int i = 0; i < context.AllyIntel.Count; i++)
        {
            CombatCharacterIntel ally = context.AllyIntel[i];
            if (ally.CanAct && !IsSupportingAlly(ally) &&
                CombatAiPositioning.IsFrontlineFollowAlly(context, ally)) return true;
        }

        return false;
    }

    private static bool HasSupportSkill(CombatAiContext context)
    {
        if (context == null || context.Owner == null) return false;

        IReadOnlyList<SkillBase> skills = context.Owner.AvailableCombatSkills;
        for (int i = 0; i < skills.Count; i++)
        {
            if (CombatAiSkillClassifier.IsSupport(skills[i])) return true;
        }

        return false;
    }

    private static bool IsSupportingAlly(CombatCharacterIntel ally) =>
        ally.HasObjective && ally.Objective == CombatObjective.SupportAlly;

    private static bool HasEnemyInReadySkillRange(CombatAiContext context)
    {
        float range = GetReadyOffensiveRange(context.Owner);
        if (range <= 0f) return false;
        for (int i = 0; i < context.EnemyIntel.Count; i++)
        {
            CombatCharacterIntel enemy = context.EnemyIntel[i];
            if (enemy.IsAlive && enemy.HasDirectSight && enemy.HasKnownPosition &&
                HorizontalDistance(context.Owner.transform.position, enemy.KnownPosition) <= range) return true;
        }

        return false;
    }

    private static CombatMoveTarget CreateThreatNearOwnStoneTarget(CombatAiContext context)
    {
        if (!context.HasOwnStonePosition) return CombatMoveTarget.None;
        CombatCharacterIntel marked = context.FindEnemyIntel(context.MarkedStoneAttacker);
        if (marked.Character != null && marked.IsAlive && marked.HasKnownPosition &&
            HorizontalDistance(marked.KnownPosition, context.OwnStonePosition) <= CombatAiAssessmentBuilder.OwnStoneAreaRadius)
        {
            return marked.HasDirectSight
                ? CombatMoveTarget.ForCharacter(marked.Character)
                : CombatMoveTarget.ForPosition(marked.KnownPosition);
        }

        CombatCharacterIntel nearest = default;
        float nearestDistance = float.PositiveInfinity;
        for (int i = 0; i < context.EnemyIntel.Count; i++)
        {
            CombatCharacterIntel enemy = context.EnemyIntel[i];
            if (enemy.Character == null || !enemy.IsAlive || !enemy.HasKnownPosition) continue;
            float distance = HorizontalDistance(enemy.KnownPosition, context.OwnStonePosition);
            if (distance > CombatAiAssessmentBuilder.OwnStoneAreaRadius) continue;
            if (distance >= nearestDistance) continue;
            nearestDistance = distance;
            nearest = enemy;
        }

        if (nearest.Character == null) return CombatMoveTarget.None;
        return nearest.HasDirectSight
            ? CombatMoveTarget.ForCharacter(nearest.Character)
            : CombatMoveTarget.ForPosition(nearest.KnownPosition);
    }

    private static CombatMoveTarget CreateBestHighGroundTarget(CombatAiContext context)
    {
        CombatMoveTarget best = CombatMoveTarget.None;
        int bestVisibleTargets = -1;
        float bestDistance = float.PositiveInfinity;
        for (int i = 0; i < context.HighGroundCandidates.Count; i++)
        {
            Vector3 candidate = context.HighGroundCandidates[i];
            CombatMoveTarget target = CreatePositionTargetIfMeaningful(context.Owner, candidate);
            if (!IsUsableMove(context, target)) continue;

            int visibleTargets = 0;
            for (int j = 0; j < context.EnemyIntel.Count; j++)
            {
                CombatCharacterIntel enemy = context.EnemyIntel[j];
                if (enemy.IsAlive && enemy.HasDirectSight && enemy.Character != null &&
                    HasLineOfSightFrom(candidate, enemy.Character)) visibleTargets++;
            }

            float distance = HorizontalDistance(context.Owner.transform.position, candidate);
            if (visibleTargets < bestVisibleTargets || visibleTargets == bestVisibleTargets && distance >= bestDistance) continue;
            bestVisibleTargets = visibleTargets;
            bestDistance = distance;
            best = target;
        }

        return best;
    }

    private static CombatMoveTarget CreatePreviousHighGroundTarget(
        CombatAiContext context,
        CombatMoveTarget previousMoveTarget)
    {
        if (previousMoveTarget.Kind != CombatMoveTargetKind.Position ||
            previousMoveTarget.HasAssaultRouteKey) return CombatMoveTarget.None;

        for (int i = 0; i < context.HighGroundCandidates.Count; i++)
        {
            if (context.HighGroundCandidates[i] == previousMoveTarget.Destination)
            {
                return previousMoveTarget;
            }
        }

        return CombatMoveTarget.None;
    }

    internal static bool IsAtHighGround(CombatAiContext context)
    {
        if (context == null || context.Owner == null) return false;

        for (int i = 0; i < context.HighGroundCandidates.Count; i++)
        {
            if (!CreatePositionTargetIfMeaningful(context.Owner, context.HighGroundCandidates[i]).HasDestination)
            {
                return true;
            }
        }

        return false;
    }

    private static CombatMoveTarget CreateBestSupportHighGroundTarget(CombatAiContext context)
    {
        Character ally = FindBestAllyCharacter(context);
        float supportRange = GetSupportRange(context.Owner);
        if (ally == null || supportRange <= 0f) return CombatMoveTarget.None;

        CombatMoveTarget best = CombatMoveTarget.None;
        float bestDistance = float.PositiveInfinity;
        for (int i = 0; i < context.HighGroundCandidates.Count; i++)
        {
            Vector3 candidate = context.HighGroundCandidates[i];
            CombatMoveTarget target = CreatePositionTargetIfMeaningful(context.Owner, candidate);
            if (!IsUsableMove(context, target) ||
                HorizontalDistance(candidate, ally.transform.position) > supportRange ||
                !HasLineOfSightFrom(candidate, ally)) continue;

            float distance = HorizontalDistance(context.Owner.transform.position, candidate);
            if (distance >= bestDistance) continue;
            bestDistance = distance;
            best = target;
        }

        return best;
    }

    private static CombatMoveTarget CreateLeastCongestedAssaultTarget(CombatAiContext context, out string actionCode)
    {
        CombatMoveTarget best = CombatMoveTarget.None;
        int lowestCongestion = int.MaxValue;
        float shortestDistance = float.PositiveInfinity;
        for (int i = 0; i < context.AssaultRoutes.Count; i++)
        {
            CombatAiAssaultRoute route = context.AssaultRoutes[i];
            CreateAssaultRouteAdvanceCandidate(context, route, out _, out _, out CombatMoveTarget target);
            if (!IsUsableMove(context, target)) continue;
            int congestion = CountAlliesUsingRoute(context, route);
            float distance = HorizontalDistance(context.Owner.transform.position, target.Destination);
            if (congestion > lowestCongestion || congestion == lowestCongestion && distance >= shortestDistance) continue;
            lowestCongestion = congestion;
            shortestDistance = distance;
            best = target;
        }

        actionCode = best.HasAssaultRouteKey ? CombatAiMoveCode.AdvanceAssaultRoute : CombatAiMoveCode.AdvanceEnemyStone;
        return best;
    }

    private static int CountAlliesUsingRoute(CombatAiContext context, CombatAiAssaultRoute route)
    {
        int count = 0;
        for (int i = 0; i < context.AllyIntel.Count; i++)
        {
            CombatCharacterIntel ally = context.AllyIntel[i];
            if (!ally.CanAct || !ally.HasIntendedDestination) continue;
            for (int j = 0; j < route.Corners.Count; j++)
            {
                if (HorizontalDistance(ally.IntendedDestination, route.Corners[j]) > 4f) continue;
                count++;
                break;
            }
        }

        return count;
    }

    private static bool IsUsableMove(CombatAiContext context, CombatMoveTarget target)
    {
        if (!target.HasDestination || context.IsMoveDestinationBlocked(target.Destination)) return false;
        if (!CombatAiNavigation.IsReachable(context.Owner, target.Destination)) return false;
        return !target.HasAssaultRouteKey || !context.HasEnemyStonePosition ||
            CombatAiNavigation.IsReachableVia(context.Owner, target.Destination, context.EnemyStonePosition);
    }

    private static WeaponKind GetWeaponKind(Character owner) =>
        owner != null && owner.EquippedWeapon != null ? owner.EquippedWeapon.Kind : WeaponKind.Unarmed;

    private static bool HasEnemyNearby(IReadOnlyList<CombatCharacterIntel> enemies, Vector3 position, float radius)
    {
        for (int i = 0; i < enemies.Count; i++)
        {
            if (enemies[i].IsAlive && enemies[i].HasKnownPosition && HorizontalDistance(position, enemies[i].KnownPosition) <= radius) return true;
        }

        return false;
    }

    private static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    private static Vector3 Flatten(Vector3 value)
    {
        value.y = 0f;
        return value;
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
        float multiplier = Mathf.Lerp(nearMultiplier, farMultiplier, Mathf.Clamp01(distance / maxRange));
        return Mathf.Max(1, Mathf.RoundToInt(baseAmount * multiplier));
    }
}
