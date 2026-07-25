using System.Collections.Generic;
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

    [SerializeField] private bool _enabled = true;
    [SerializeField, Min(0.05f)] private float _decisionIntervalSeconds = 0.5f;
    [SerializeField] private bool _executeMovement = true;
    [SerializeField] private bool _executeSkills = true;
    [SerializeField] private bool _executeStoneAttacks = true;

    private Character _owner;
    private CombatAiContextCollector _contextCollector;
    private CombatAiPersonalityRuntime _personalityRuntime;
    private float _nextDecisionTime;
    private float _nextStoneAttackTime;
    private Character _focusedEnemy;
    private float _focusedEnemyLockedUntilTime;
    private float _stoneAssaultLockedUntilTime;
    private float _personalityPauseUntilTime;
    private MagicStone _cachedEnemyMainStone;
    private readonly List<CombatAiReasonCode> _objectiveReasonCodes = new List<CombatAiReasonCode>();

    public CombatAiPlan LastPlan { get; private set; } = CombatAiPlan.None;
    public CombatAiContext LastContext { get; private set; }
    public CombatSkillEvaluationResult LastSkillEvaluation { get; private set; }
    public bool HasLastSkillEvaluation { get; private set; }
    public int LastStoneDamage { get; private set; }
    public bool IsAiEnabled => _enabled;

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

    private void Update()
    {
        if (!_enabled || Time.time < _nextDecisionTime) return;

        _nextDecisionTime = Time.time + _decisionIntervalSeconds;
        ResolveDependencies();
        CombatBattleRandom.AdvanceDecisionTick(_owner);
        TickNowCore();
    }

    public bool TickNow()
    {
        ResolveDependencies();
        CombatBattleRandom.AdvanceDecisionTick(_owner);
        return TickNowCore();
    }

    public void ResetForBattle()
    {
        _nextDecisionTime = 0f;
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
    }

    private bool TickNowCore()
    {
        ResolveDependencies();
        if (!CanRun()) return false;
        if (_owner.SkillCaster.IsCasting) return false;
        if (ShouldKeepPersonalityPause()) return false;

        LastContext = _contextCollector.Collect(_owner);
        _personalityRuntime.Refresh();
        if (!_owner.Health.CanAct) return false;
        PruneFocusedEnemy(LastContext);
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
            LastPlan = revengePlan;
            NotifyPlanSelected(previousPlan, LastPlan);
            return ExecutePlan(LastPlan);
        }

        LastPlan = CombatAiPlanner.BuildPlan(
            LastContext,
            _owner.PersonalityProfile,
            _focusedEnemy,
            GetFocusCommitmentRemainingSeconds(),
            scoringPreviousObjective,
            _objectiveReasonCodes);
        UpdateStoneAssaultCommitment(
            previousObjective,
            LastPlan.Objective,
            keepsStoneAssaultCommitment);
        NotifyPlanSelected(previousPlan, LastPlan);
        return ExecutePlan(LastPlan);
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
        ResolveDependencies();
        if (!CanRun()) return false;
        if (_owner.SkillCaster.IsCasting) return false;

        LastPlan = plan;
        bool moved = TryExecuteMovement(plan);
        bool usedSkill = TryExecuteSkill(plan);
        bool attackedStone = !usedSkill && TryExecuteStoneAttack(plan);
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

        CombatVision vision = _owner.Vision;
        vision?.UpdateVision();
        if (vision != null && !vision.HasLineOfSight(stone.transform)) return false;

        WeaponBase weapon = _owner.EquippedWeapon ?? WeaponBase.Unarmed;
        float attackRange = Mathf.Max(0.5f, weapon.Range) + 1.2f;
        if (HorizontalDistance(_owner.transform.position, stone.transform.position) > attackRange) return false;

        int damage = Mathf.Max(1, Mathf.RoundToInt(GetEffectiveScalingStat(weapon.ScalingStat) * 0.5f));
        LastStoneDamage = stoneSystem.TakeDamage(stone.FeatureIndex, damage, _owner);
        if (LastStoneDamage <= 0) return false;

        _nextStoneAttackTime = Time.time + Mathf.Max(0.1f, weapon.CooldownSeconds);
        return true;
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
        return _owner.MoveToTarget(destination);
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
