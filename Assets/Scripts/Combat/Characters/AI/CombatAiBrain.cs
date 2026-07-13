using UnityEngine;
using WarSimulation.Combat.Map;

[DisallowMultipleComponent]
[RequireComponent(typeof(Character))]
[RequireComponent(typeof(CombatAiContextCollector))]
public sealed class CombatAiBrain : MonoBehaviour
{
    private const float SwordFocusCommitmentSeconds = 2.5f;

    [SerializeField] private bool _enabled = true;
    [SerializeField, Min(0.05f)] private float _decisionIntervalSeconds = 0.5f;
    [SerializeField] private bool _executeMovement = true;
    [SerializeField] private bool _executeSkills = true;
    [SerializeField] private bool _executeStoneAttacks = true;
    [SerializeField] private CombatAiWeaponWeightsProfile _weaponWeightsProfile;

    private Character _owner;
    private CombatAiContextCollector _contextCollector;
    private float _nextDecisionTime;
    private float _nextStoneAttackTime;
    private Character _focusedEnemy;
    private float _focusedEnemyLockedUntilTime;
    private MagicStone _cachedEnemyMainStone;

    public CombatAiPlan LastPlan { get; private set; } = CombatAiPlan.None;
    public CombatAiContext LastContext { get; private set; }
    public CombatSkillEvaluationResult LastSkillEvaluation { get; private set; }
    public bool HasLastSkillEvaluation { get; private set; }
    public int LastStoneDamage { get; private set; }
    public CombatAiWeaponWeightsProfile WeaponWeightsProfile => ResolveWeaponWeightsProfile();
    public bool IsAiEnabled => _enabled;

    private void Awake()
    {
        ResolveDependencies();
    }

    private void Update()
    {
        if (!_enabled || Time.time < _nextDecisionTime) return;

        _nextDecisionTime = Time.time + _decisionIntervalSeconds;
        TickNow();
    }

    public bool TickNow()
    {
        ResolveDependencies();
        if (!CanRun()) return false;
        if (_owner.SkillCaster.IsCasting) return false;

        LastContext = _contextCollector.Collect(_owner);
        PruneFocusedEnemy(LastContext);
        CombatObjective previousObjective = LastPlan.Objective;
        LastPlan = CombatAiPlanner.BuildPlan(
            LastContext,
            _owner.PersonalityProfile,
            ResolveWeaponWeightsProfile(),
            _focusedEnemy,
            GetFocusCommitmentRemainingSeconds(),
            previousObjective);
        if (LastPlan.Objective != previousObjective)
        {
            CombatAiDecisionEvents.RaiseObjectiveChanged(_owner, previousObjective, LastPlan.Objective);
        }
        return ExecutePlan(LastPlan);
    }

    public bool ExecutePlan(CombatAiPlan plan)
    {
        ResolveDependencies();
        if (!CanRun()) return false;
        if (_owner.SkillCaster.IsCasting) return false;

        LastPlan = plan;
        bool usedSkill = TryExecuteSkill(plan);
        bool attackedStone = !usedSkill && TryExecuteStoneAttack(plan);
        bool moved = !usedSkill && !attackedStone && TryExecuteMovement(plan);
        UpdateFocusedEnemy(plan);
        return attackedStone || usedSkill || moved;
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
        LastStoneDamage = stoneSystem.TakeDamage(stone.FeatureIndex, damage);
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
        _owner.MoveToTarget(destination);
        return true;
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
        if (_owner == null || _owner.EquippedWeapon == null || _owner.EquippedWeapon.Kind != WeaponKind.Sword)
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
            _focusedEnemyLockedUntilTime = Time.time + SwordFocusCommitmentSeconds;
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

            if (enemy.HP > 0 && enemy.CanAct && enemy.HasKnownPosition)
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
    }

    private CombatAiWeaponWeightsProfile ResolveWeaponWeightsProfile()
    {
        if (_weaponWeightsProfile != null)
        {
            return _weaponWeightsProfile;
        }

        CombatSceneContext context = CombatSceneContext.Instance;
        return context != null ? context.AiWeaponWeightsProfile : null;
    }
}
