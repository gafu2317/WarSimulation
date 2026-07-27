using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;
using WarSimulation.Combat.Map;

[DisallowMultipleComponent]
[RequireComponent(typeof(Character))]
[RequireComponent(typeof(CombatAiContextCollector))]
[RequireComponent(typeof(CombatAiPersonalityRuntime))]
public sealed class CombatAiBrain : MonoBehaviour
{
    private const float SwordFocusCommitmentSeconds = 2.5f;
    private const float BattleJunkieFocusCommitmentSeconds = 8f;
    private const float StoneAssaultCommitmentSeconds = 15f;
    private const float CloseStoneAttackRange = 3.2f;
    private static readonly ProfilerMarker CollectContextMarker = new("CombatAI.CollectContext");
    private static readonly ProfilerMarker RefreshPersonalityMarker = new("CombatAI.RefreshPersonality");
    private static readonly ProfilerMarker BuildPlanMarker = new("CombatAI.BuildPlan");

    [SerializeField] private bool _enabled = true;
    [SerializeField] private bool _executeMovement = true;
    [SerializeField] private bool _executeSkills = true;
    [SerializeField] private bool _executeStoneAttacks = true;

    private Character _owner;
    private CombatAiContextCollector _contextCollector;
    private CombatAiPersonalityRuntime _personalityRuntime;
    private float _nextStoneAttackTime;
    private Character _focusedEnemy;
    private float _focusedEnemyLockedUntilTime;
    private float _stoneAssaultLockedUntilTime;
    private float _personalityPauseUntilTime;
    private MagicStone _cachedEnemyMainStone;
    private readonly List<CombatAiReasonCode> _objectiveReasonCodes = new List<CombatAiReasonCode>();
    private CombatAiPlan _preparedPreviousPlan;
    private CombatAiPlan _preparedPlan;
    private CombatAiContext _preparedContext;
    private bool _preparedUpdatesStoneAssaultCommitment;
    private bool _preparedKeepsStoneAssaultCommitment;
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
    public int LastStoneDamage { get; private set; }
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
        if (_owner != null && _owner.Health != null)
        {
            _owner.Health.IncomingDamage -= HandleIncomingDamage;
            _owner.Health.IncomingDamage += HandleIncomingDamage;
        }
    }

    private void OnDisable()
    {
        if (_owner != null && _owner.Health != null)
        {
            _owner.Health.IncomingDamage -= HandleIncomingDamage;
        }
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
        if (!_enabled) return false;

        ResolveDependencies();
        CombatBattleRandom.AdvanceDecisionTick(_owner);
        UpdateMoveProgress();
        return PrepareDecisionCore(reservations, perceptionPrepared);
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
        if (_preparedUpdatesStoneAssaultCommitment)
        {
            UpdateStoneAssaultCommitment(
                _preparedPreviousPlan.Objective,
                LastPlan.Objective,
                _preparedKeepsStoneAssaultCommitment);
        }

        NotifyPlanSelected(_preparedPreviousPlan, LastPlan);
        return ExecutePlan(LastPlan);
    }

    public void ResetForBattle()
    {
        _nextStoneAttackTime = 0f;
        _focusedEnemy = null;
        _focusedEnemyLockedUntilTime = 0f;
        _stoneAssaultLockedUntilTime = 0f;
        _personalityPauseUntilTime = 0f;
        _cachedEnemyMainStone = null;
        LastPlan = CombatAiPlan.None;
        LastContext = null;
        HasLastSkillEvaluation = false;
        LastStoneDamage = 0;
        _objectiveReasonCodes.Clear();
        _hasPreparedDecision = false;
        _hasRequestedMove = false;
        _lastRequestedMoveDestination = default;
        _lastMoveObservedPosition = default;
        _stagnantMoveDecisionCount = 0;
        _consecutiveMoveFailures = 0;
        _blockedMoveDestination = default;
        _blockedMoveUntilDecisionTick = 0;
    }

    private bool PrepareDecisionCore(
        CombatAiTeamReservations reservations,
        bool perceptionPrepared)
    {
        _hasPreparedDecision = false;
        ResolveDependencies();
        if (!CanRun()) return false;
        if (_owner.SkillCaster.IsCasting) return false;
        if (ShouldKeepPersonalityPause()) return false;

        CombatAiContext nextContext;
        using (CollectContextMarker.Auto())
        {
            nextContext = _contextCollector.Collect(
                _owner,
                reservations,
                perceptionPrepared,
                IsMoveDestinationBlocked(),
                _blockedMoveDestination);
        }
        using (RefreshPersonalityMarker.Auto()) _personalityRuntime.Refresh();
        if (!_owner.Health.CanAct) return false;
        PruneFocusedEnemy(nextContext);
        CombatAiPlan previousPlan = LastPlan;
        CombatObjective previousObjective = previousPlan.Objective;
        bool keepsStoneAssaultCommitment = previousObjective == CombatObjective.DestroyEnemyStone &&
            Time.time < _stoneAssaultLockedUntilTime;
        CombatObjective scoringPreviousObjective = previousObjective == CombatObjective.DestroyEnemyStone &&
            !keepsStoneAssaultCommitment
                ? CombatObjective.Search
                : previousObjective;
        _objectiveReasonCodes.Clear();
        if (_personalityRuntime.TryBuildRevengePlan(out CombatAiPlan revengePlan))
        {
            SetPreparedDecision(previousPlan, revengePlan, nextContext, false, false);
            return true;
        }

        CombatAiPlan nextPlan;
        using (BuildPlanMarker.Auto())
        {
            nextPlan = CombatAiPlanner.BuildPlan(
                nextContext,
                _owner.PersonalityProfile,
                _focusedEnemy,
                GetFocusCommitmentRemainingSeconds(),
                scoringPreviousObjective,
                _objectiveReasonCodes);
        }
        SetPreparedDecision(
            previousPlan,
            nextPlan,
            nextContext,
            true,
            keepsStoneAssaultCommitment);
        return true;
    }

    private void SetPreparedDecision(
        CombatAiPlan previousPlan,
        CombatAiPlan nextPlan,
        CombatAiContext nextContext,
        bool updatesStoneAssaultCommitment,
        bool keepsStoneAssaultCommitment)
    {
        _preparedPreviousPlan = previousPlan;
        _preparedPlan = nextPlan;
        _preparedContext = nextContext;
        _preparedUpdatesStoneAssaultCommitment = updatesStoneAssaultCommitment;
        _preparedKeepsStoneAssaultCommitment = keepsStoneAssaultCommitment;
        _hasPreparedDecision = true;
    }

    private void UpdateStoneAssaultCommitment(
        CombatObjective previousObjective,
        CombatObjective nextObjective,
        bool keptExistingCommitment)
    {
        if (nextObjective != CombatObjective.DestroyEnemyStone)
        {
            _stoneAssaultLockedUntilTime = 0f;
            return;
        }

        if (previousObjective != CombatObjective.DestroyEnemyStone || !keptExistingCommitment)
        {
            _stoneAssaultLockedUntilTime = Time.time + StoneAssaultCommitmentSeconds;
        }
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
        if (!CanRun()) return false;
        if (_owner.SkillCaster.IsCasting) return false;

        LastPlan = plan;
        bool moved = TryExecuteMovement(plan);
        bool attackedStone = TryExecuteStoneAttack(plan);
        bool usedSkill = !attackedStone && TryExecuteSkill(plan);
        _personalityRuntime?.NotifyPlanExecuted(plan, usedSkill);
        UpdateFocusedEnemy(plan);
        bool acted = attackedStone || usedSkill || moved;
        if (acted && HasPersonality(CombatAiPersonalityKind.Lazy))
        {
            _personalityPauseUntilTime = Time.time + 1.5f;
        }
        return acted;
    }

    private bool TryExecuteStoneAttack(CombatAiPlan plan)
    {
        LastStoneDamage = 0;
        if (!_executeStoneAttacks || plan.Objective != CombatObjective.DestroyEnemyStone) return false;
        if (Time.time < _nextStoneAttackTime) return false;

        MagicStone stone = FindEnemyMainStone();
        CombatMagicStoneSystem stoneSystem = CombatMagicStoneSystemResolver.Resolve();
        if (stone == null || stoneSystem == null || stone.FeatureIndex < 0) return false;
        if (!stoneSystem.TryGetState(stone.FeatureIndex, out MagicStoneRuntimeState state) || state.HP <= 0) return false;

        WeaponBase weapon = _owner.EquippedWeapon ?? WeaponBase.Unarmed;
        float attackRange = Mathf.Max(0.5f, weapon.Range) + 1.2f;
        float distance = HorizontalDistance(_owner.transform.position, stone.transform.position);
        if (distance > attackRange) return false;

        FaceTarget(stone.transform.position);
        CombatVision vision = _owner.Vision;
        vision?.UpdateVision();
        if (distance > CloseStoneAttackRange &&
            vision != null &&
            !vision.HasLineOfSight(stone.transform)) return false;

        int damage = Mathf.Max(1, Mathf.RoundToInt(GetEffectiveScalingStat(weapon.ScalingStat) * 0.5f));
        LastStoneDamage = stoneSystem.TakeDamage(stone.FeatureIndex, damage, _owner);
        if (LastStoneDamage <= 0) return false;

        _nextStoneAttackTime = Time.time + Mathf.Max(0.1f, weapon.CooldownSeconds);
        return true;
    }

    private void FaceTarget(Vector3 targetPosition)
    {
        Vector3 direction = targetPosition - _owner.transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.001f)
        {
            _owner.transform.rotation = Quaternion.LookRotation(direction);
        }
    }

    private bool TryExecuteSkill(CombatAiPlan plan)
    {
        HasLastSkillEvaluation = false;
        if (!_executeSkills || plan.Skill == null) return false;

        CombatSkillEvaluationResult evaluation = CombatSkillEvaluator.Evaluate(
            _owner,
            plan.Skill,
            plan.SkillContext);
        LastSkillEvaluation = evaluation;
        HasLastSkillEvaluation = true;

        if (!evaluation.CanUse) return false;

        return _owner.SkillCaster.TryStartCast(plan.Skill, evaluation.Context);
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

    private MagicStone FindEnemyMainStone()
    {
        if (_cachedEnemyMainStone != null && _cachedEnemyMainStone.gameObject.activeInHierarchy)
        {
            return _cachedEnemyMainStone;
        }

        FeatureType targetType = _owner.Team == CombatTeam.Ally
            ? FeatureType.EnemyMainStone
            : FeatureType.OwnMainStone;
        MagicStone[] stones = FindObjectsByType<MagicStone>(FindObjectsInactive.Exclude);
        MagicStone best = null;
        float bestDistance = float.PositiveInfinity;
        for (int i = 0; i < stones.Length; i++)
        {
            MagicStone stone = stones[i];
            if (stone == null || stone.FeatureType != targetType) continue;

            float distance = HorizontalDistance(_owner.transform.position, stone.transform.position);
            if (distance >= bestDistance) continue;
            bestDistance = distance;
            best = stone;
        }

        _cachedEnemyMainStone = best;
        return best;
    }

    private float GetEffectiveScalingStat(CombatStat stat)
    {
        return _owner != null ? _owner.GetEffectiveStat(stat) : 0f;
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

    private bool ShouldKeepPersonalityPause()
    {
        if (!HasPersonality(CombatAiPersonalityKind.Lazy) || Time.time >= _personalityPauseUntilTime) return false;
        return _owner.Health.HP > _owner.Health.MaxHP * 0.4f;
    }

    private bool HasPersonality(CombatAiPersonalityKind kind)
    {
        return _owner != null && _owner.PersonalityProfile != null && _owner.PersonalityProfile.Kind == kind;
    }

    private void HandleIncomingDamage(CombatHealth.IncomingDamageContext damage)
    {
        if (HasPersonality(CombatAiPersonalityKind.Innocent) && CombatBattleRandom.Roll(_owner, "InnocentAvoidDamage", 0.25f))
        {
            damage.IsHandled = true;
            damage.PreventionSource = CombatEffectSource.Capture(_owner);
        }
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

        if (_personalityRuntime == null)
        {
            _personalityRuntime = GetComponent<CombatAiPersonalityRuntime>();
            if (_personalityRuntime == null)
            {
                _personalityRuntime = gameObject.AddComponent<CombatAiPersonalityRuntime>();
            }
        }
    }

}
