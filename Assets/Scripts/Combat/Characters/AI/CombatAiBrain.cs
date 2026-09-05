using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Character))]
[RequireComponent(typeof(CombatAiContextCollector))]
public sealed class CombatAiBrain : MonoBehaviour
{
    private const float SwordFocusCommitmentSeconds = 2.5f;
    private const float BattleJunkieFocusCommitmentSeconds = 12f;
    private static readonly ProfilerMarker CollectContextMarker = new("CombatAI.CollectContext");
    private static readonly ProfilerMarker BuildPlanMarker = new("CombatAI.BuildPlan");

    [SerializeField] private bool _enabled = true;
    [SerializeField] private bool _executeMovement = true;
    [SerializeField] private bool _executeSkills = true;

    private Character _owner;
    private CombatAiContextCollector _contextCollector;
    private CombatCharacterBody _body;
    private Character _focusedEnemy;
    private float _focusedEnemyLockedUntilTime;
    private Character _revengeTarget;
    private readonly List<CombatAiReasonCode> _objectiveReasonCodes = new List<CombatAiReasonCode>();
    private CombatAiPlan _preparedPreviousPlan;
    private CombatAiPlan _preparedPlan;
    private CombatAiContext _preparedContext;
    private bool _hasPreparedDecision;
    private bool _hasRequestedMove;
    private Vector3 _lastRequestedMoveDestination;
    private Vector3 _lastMoveObservedPosition;
    private int _stagnantMoveDecisionCount;
    private int _consecutiveMoveFailures;
    private Vector3 _blockedMoveDestination;
    private int _blockedMoveUntilDecisionTick;

    public CombatAiPlan LastPlan { get; private set; } = CombatAiPlan.None;
    public CombatAiContext LastContext { get; private set; }
    public CombatSkillEvaluationResult LastSkillEvaluation { get; private set; }
    public bool HasLastSkillEvaluation { get; private set; }
    public bool IsAiEnabled => _enabled;
    public bool HasBlockedMove => IsMoveDestinationBlocked();
    public Vector3 BlockedMoveDestination => _blockedMoveDestination;

    private void Awake()
    {
        ResolveDependencies();
    }

    private void OnEnable()
    {
        ResolveDependencies();
        SubscribeRevengeTracking();
    }

    private void OnDisable()
    {
        UnsubscribeRevengeTracking();
    }

    public bool TickNow()
    {
        return PrepareScheduledDecision() && ExecutePreparedDecision();
    }

    public bool PrepareScheduledDecision()
    {
        return PrepareScheduledDecision(null, false);
    }

    public bool PrepareScheduledDecision(
        CombatAiTeamReservations reservations,
        bool perceptionPrepared)
    {
        return PrepareScheduledDecision(reservations, perceptionPrepared, null, null);
    }

    internal bool PrepareScheduledDecision(
        CombatAiTeamReservations reservations,
        bool perceptionPrepared,
        CombatAiWorldSnapshot worldSnapshot,
        CombatAiBatchPhaseMeasurements phaseMeasurements)
    {
        if (!_enabled) return false;

        ResolveDependencies();
        CombatBattleRandom.AdvanceDecisionTick(_owner);
        UpdateMoveProgress();
        _body?.BeginAiNavigationDecision();
        bool prepared = PrepareDecisionCore(
            reservations,
            perceptionPrepared,
            worldSnapshot,
            phaseMeasurements);
        if (!prepared) _body?.EndAiNavigationDecision();
        return prepared;
    }

    public bool TryGetPreparedPlan(out CombatAiPlan plan)
    {
        plan = _preparedPlan;
        return _hasPreparedDecision;
    }

    public bool ExecutePreparedDecision()
    {
        if (!_hasPreparedDecision) return false;

        _hasPreparedDecision = false;
        LastContext = _preparedContext;
        LastPlan = _preparedPlan;
        NotifyPlanSelected(_preparedPreviousPlan, LastPlan);
        bool executed = ExecutePlan(LastPlan);
        _body?.EndAiNavigationDecision();
        return executed;
    }

    public void ResetForBattle()
    {
        _focusedEnemy = null;
        _focusedEnemyLockedUntilTime = 0f;
        _revengeTarget = null;
        LastPlan = CombatAiPlan.None;
        LastContext = null;
        HasLastSkillEvaluation = false;
        _objectiveReasonCodes.Clear();
        _hasPreparedDecision = false;
        _hasRequestedMove = false;
        _lastRequestedMoveDestination = default;
        _lastMoveObservedPosition = default;
        _stagnantMoveDecisionCount = 0;
        _consecutiveMoveFailures = 0;
        _blockedMoveDestination = default;
        _blockedMoveUntilDecisionTick = 0;
        _body?.EndAiNavigationDecision();
    }

    private bool PrepareDecisionCore(
        CombatAiTeamReservations reservations,
        bool perceptionPrepared,
        CombatAiWorldSnapshot worldSnapshot,
        CombatAiBatchPhaseMeasurements phaseMeasurements)
    {
        _hasPreparedDecision = false;
        ResolveDependencies();
        if (!CanRun()) return false;
        if (_owner.SkillCaster.IsCasting) return false;

        CombatAiContext nextContext;
        long phaseStartTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
        using (CollectContextMarker.Auto())
        {
            nextContext = _contextCollector.Collect(
                _owner,
                reservations,
                perceptionPrepared,
                IsMoveDestinationBlocked(),
                _blockedMoveDestination,
                _revengeTarget,
                worldSnapshot);
        }
        phaseMeasurements?.AddContext(phaseStartTimestamp);
        if (!_owner.Health.CanAct) return false;
        PruneFocusedEnemy(nextContext);
        PruneRevengeTarget(nextContext);
        CombatAiPlan previousPlan = LastPlan;
        _objectiveReasonCodes.Clear();

        CombatAiPlan nextPlan;
        phaseStartTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
        using (BuildPlanMarker.Auto())
        {
            nextPlan = CombatAiPlanner.BuildPlan(
                nextContext,
                _owner.PersonalityProfile,
                _focusedEnemy,
                GetFocusCommitmentRemainingSeconds(),
                previousPlan.Objective,
                _objectiveReasonCodes,
                previousPlan.MoveTarget);
        }
        phaseMeasurements?.AddPlanning(phaseStartTimestamp);
        SetPreparedDecision(previousPlan, nextPlan, nextContext);
        return true;
    }

    private void SetPreparedDecision(
        CombatAiPlan previousPlan,
        CombatAiPlan nextPlan,
        CombatAiContext nextContext)
    {
        _preparedPreviousPlan = previousPlan;
        _preparedPlan = nextPlan;
        _preparedContext = nextContext;
        _hasPreparedDecision = true;
    }

    private void NotifyPlanSelected(CombatAiPlan previous, CombatAiPlan next)
    {
        CombatAiDecisionEvents.RaisePlanSelected(_owner, previous, next);
        CombatAiDecisionEvents.RaiseObjectiveChanged(
            _owner,
            previous.Objective,
            next.Objective,
            _objectiveReasonCodes);
    }

    public bool ExecutePlan(CombatAiPlan plan)
    {
        _hasPreparedDecision = false;
        ResolveDependencies();
        if (!CanRun())
        {
            CombatAiDecisionEvents.RaisePlanExecuted(_owner, plan, false, false, "AI cannot execute");
            return false;
        }
        if (_owner.SkillCaster.IsCasting)
        {
            CombatAiDecisionEvents.RaisePlanExecuted(_owner, plan, false, false, "already casting");
            return false;
        }

        LastPlan = plan;
        bool moved = TryExecuteMovement(plan);
        bool usedSkill = TryExecuteSkill(plan, out string skillFailureReason);
        UpdateFocusedEnemy(plan);
        string failureReason = ResolveExecutionFailure(plan, moved, usedSkill, skillFailureReason);
        CombatAiDecisionEvents.RaisePlanExecuted(_owner, plan, moved, usedSkill, failureReason);
        return usedSkill || moved;
    }

    private bool TryExecuteSkill(CombatAiPlan plan, out string failureReason)
    {
        failureReason = string.Empty;
        HasLastSkillEvaluation = false;
        if (!_executeSkills || plan.Skill == null) return false;

        TryFaceSkillContext(plan.SkillContext);

        CombatSkillEvaluationResult evaluation = CombatSkillEvaluator.Evaluate(
            _owner,
            plan.Skill,
            plan.SkillContext);
        LastSkillEvaluation = evaluation;
        HasLastSkillEvaluation = true;

        if (!evaluation.CanUse)
        {
            failureReason = evaluation.FailureReason;
            return false;
        }
        if (!HasFacingLineOfSightForStones(evaluation.Context))
        {
            failureReason = "stone line of sight lost";
            return false;
        }

        bool started = _owner.SkillCaster.TryStartCast(plan.Skill, evaluation.Context);
        if (!started) failureReason = "cast start rejected";
        return started;
    }

    private static string ResolveExecutionFailure(
        CombatAiPlan plan,
        bool movementStarted,
        bool skillStarted,
        string skillFailureReason)
    {
        if (!string.IsNullOrEmpty(skillFailureReason)) return skillFailureReason;
        if (plan.MoveTarget.HasDestination && !movementStarted) return "movement not started";
        if (plan.Skill != null && !skillStarted) return "skill not started";
        return string.Empty;
    }

    private void TryFaceSkillContext(SkillExecutionContext context)
    {
        if (_owner == null) return;

        if (context.PrimaryStone != null)
        {
            _owner.FaceHorizontalToward(context.PrimaryStone.transform.position);
            return;
        }

        if (context.ResolvedStones != null && context.ResolvedStones.Count > 0 && context.ResolvedStones[0] != null)
            _owner.FaceHorizontalToward(context.ResolvedStones[0].transform.position);
    }

    private bool HasFacingLineOfSightForStones(SkillExecutionContext context)
    {
        CombatVision vision = _owner != null ? _owner.Vision : null;
        if (vision == null) return true;

        if (context.PrimaryStone != null)
        {
            vision.UpdateVision();
            return vision.HasLineOfSight(context.PrimaryStone.transform);
        }

        if (context.ResolvedStones == null || context.ResolvedStones.Count == 0) return true;

        vision.UpdateVision();
        for (int i = 0; i < context.ResolvedStones.Count; i++)
        {
            MagicStone stone = context.ResolvedStones[i];
            if (stone == null) continue;
            if (!vision.HasLineOfSight(stone.transform)) return false;
        }

        return true;
    }

    private bool TryExecuteMovement(CombatAiPlan plan)
    {
        if (!_executeMovement || !plan.MoveTarget.HasDestination) return false;

        Vector3 destination = plan.MoveTarget.Kind == CombatMoveTargetKind.Character &&
            plan.MoveTarget.TargetCharacter != null
                ? plan.MoveTarget.TargetCharacter.transform.position
                : plan.MoveTarget.Destination;
        if (IsMoveDestinationBlocked() && HorizontalDistance(destination, _blockedMoveDestination) <= 2f)
        {
            return false;
        }

        bool moved = _owner.MoveToTarget(destination);
        if (moved)
        {
            RegisterRequestedMove(destination);
        }
        else
        {
            RegisterMoveFailure(destination);
        }

        return moved;
    }

    private void RegisterRequestedMove(Vector3 destination)
    {
        if (!_hasRequestedMove || HorizontalDistance(destination, _lastRequestedMoveDestination) > 2f)
        {
            _stagnantMoveDecisionCount = 0;
            _lastMoveObservedPosition = _owner.transform.position;
        }

        _hasRequestedMove = true;
        _lastRequestedMoveDestination = destination;
        _consecutiveMoveFailures = 0;
    }

    private void RegisterMoveFailure(Vector3 destination)
    {
        if (HorizontalDistance(destination, _lastRequestedMoveDestination) <= 2f)
        {
            _consecutiveMoveFailures++;
        }
        else
        {
            _lastRequestedMoveDestination = destination;
            _consecutiveMoveFailures = 1;
        }

        if (_consecutiveMoveFailures >= 2)
        {
            BlockMoveDestination(destination);
        }
    }

    private void UpdateMoveProgress()
    {
        if (!_hasRequestedMove || _owner == null) return;

        Vector3 currentPosition = _owner.transform.position;
        float remainingDistance = HorizontalDistance(currentPosition, _lastRequestedMoveDestination);
        float movedDistance = HorizontalDistance(currentPosition, _lastMoveObservedPosition);
        if (remainingDistance <= 1.5f || movedDistance >= 0.2f)
        {
            _stagnantMoveDecisionCount = 0;
        }
        else
        {
            _stagnantMoveDecisionCount++;
        }

        _lastMoveObservedPosition = currentPosition;
        if (_stagnantMoveDecisionCount >= 4)
        {
            BlockMoveDestination(_lastRequestedMoveDestination);
        }
    }

    private void BlockMoveDestination(Vector3 destination)
    {
        _blockedMoveDestination = destination;
        _blockedMoveUntilDecisionTick = CombatBattleRandom.GetDecisionTick(_owner) + 6;
        _hasRequestedMove = false;
        _stagnantMoveDecisionCount = 0;
        _consecutiveMoveFailures = 0;
    }

    private bool IsMoveDestinationBlocked()
    {
        return CombatBattleRandom.GetDecisionTick(_owner) < _blockedMoveUntilDecisionTick;
    }

    private float GetFocusCommitmentRemainingSeconds()
    {
        return Mathf.Max(0f, _focusedEnemyLockedUntilTime - Time.time);
    }

    private void UpdateFocusedEnemy(CombatAiPlan plan)
    {
        bool keepsEnemyFocus = _owner != null &&
            ((_owner.EquippedWeapon != null && _owner.EquippedWeapon.Kind == WeaponKind.Sword) ||
             (_owner.PersonalityProfile != null && _owner.PersonalityProfile.Kind == CombatAiPersonalityKind.BattleJunkie));
        if (!keepsEnemyFocus)
        {
            _focusedEnemy = null;
            _focusedEnemyLockedUntilTime = 0f;
            return;
        }

        Character nextFocus = null;
        if (plan.SkillContext.PrimaryTarget != null && plan.SkillContext.PrimaryTarget.Team != _owner.Team)
        {
            nextFocus = plan.SkillContext.PrimaryTarget;
        }
        else if (plan.MoveTarget.Kind == CombatMoveTargetKind.Character &&
                 plan.MoveTarget.TargetCharacter != null &&
                 plan.MoveTarget.TargetCharacter.Team != _owner.Team)
        {
            nextFocus = plan.MoveTarget.TargetCharacter;
        }

        if (nextFocus != null)
        {
            _focusedEnemy = nextFocus;
            float duration = _owner.PersonalityProfile != null &&
                _owner.PersonalityProfile.Kind == CombatAiPersonalityKind.BattleJunkie
                    ? BattleJunkieFocusCommitmentSeconds
                    : SwordFocusCommitmentSeconds;
            _focusedEnemyLockedUntilTime = Time.time + duration;
        }
        else if (GetFocusCommitmentRemainingSeconds() <= 0f)
        {
            _focusedEnemy = null;
        }
    }

    private void PruneFocusedEnemy(CombatAiContext context)
    {
        if (_focusedEnemy == null || context == null)
        {
            return;
        }

        for (int i = 0; i < context.EnemyIntel.Count; i++)
        {
            CombatCharacterIntel enemy = context.EnemyIntel[i];
            if (enemy.Character != _focusedEnemy) continue;

            if (enemy.HP > 0 && enemy.HasKnownPosition)
            {
                return;
            }

            break;
        }

        _focusedEnemy = null;
        _focusedEnemyLockedUntilTime = 0f;
    }

    private void PruneRevengeTarget(CombatAiContext context)
    {
        if (_revengeTarget == null || context == null)
        {
            return;
        }

        CombatCharacterIntel intel = context.FindEnemyIntel(_revengeTarget);
        if (intel.Character == _revengeTarget && intel.IsAlive && intel.HasKnownPosition)
        {
            return;
        }

        _revengeTarget = null;
    }

    private void OnOwnerDamaged(int amount, CombatEffectSource attackSource)
    {
        if (amount <= 0 || _owner == null ||
            _owner.PersonalityProfile == null ||
            _owner.PersonalityProfile.Kind != CombatAiPersonalityKind.Avenger) return;

        Character attacker = attackSource.Character;
        if (attacker == null || attacker == _owner || attacker.Team == _owner.Team) return;
        if (attacker.Health == null || !attacker.Health.IsAlive) return;
        _revengeTarget = attacker;
    }

    private void SubscribeRevengeTracking()
    {
        if (_owner == null || _owner.Health == null) return;
        _owner.Health.DamagedWithSource -= OnOwnerDamaged;
        _owner.Health.DamagedWithSource += OnOwnerDamaged;
    }

    private void UnsubscribeRevengeTracking()
    {
        if (_owner == null || _owner.Health == null) return;
        _owner.Health.DamagedWithSource -= OnOwnerDamaged;
    }

    private static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    private bool CanRun()
    {
        if (_owner == null || _contextCollector == null) return false;
        if (_owner.Health == null || !_owner.Health.CanAct) return false;
        return CombatBattleFlow.AllowsCombatActions;
    }

    private void ResolveDependencies()
    {
        if (_owner == null)
        {
            _owner = GetComponent<Character>();
        }

        if (_contextCollector == null)
        {
            _contextCollector = GetComponent<CombatAiContextCollector>();
            if (_contextCollector == null)
            {
                _contextCollector = gameObject.AddComponent<CombatAiContextCollector>();
            }
        }

        if (_body == null)
        {
            _body = GetComponent<CombatCharacterBody>();
        }
    }

}
