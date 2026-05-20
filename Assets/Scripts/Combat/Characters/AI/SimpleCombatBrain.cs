using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Character))]
[RequireComponent(typeof(CombatVision))]
[RequireComponent(typeof(CombatHealth))]
[RequireComponent(typeof(CombatAttack))]
[RequireComponent(typeof(CombatCharacterBody))]
public sealed class SimpleCombatBrain : MonoBehaviour
{
    public enum MoveKind
    {
        Idle = 0,
        Patrol = 1,
        AssaultEnemyBase = 2,
        DefendHomeBase = 3,
        FollowAlly = 4,
        ChaseEnemy = 5,
        MoveToLastKnownEnemyPosition = 6,
        RetreatToHome = 7,
        MoveToHighGround = 8,
        HideInForest = 9,
    }

    public enum ActionKind
    {
        None = 0,
        AttackEnemy = 1,
        UseSkill = 2,
    }

    public readonly struct MoveOption
    {
        public MoveKind Kind { get; }
        public Character Target { get; }
        public Vector3 Destination { get; }
        public float Score { get; }

        public MoveOption(MoveKind kind, float score, Character target = null, Vector3 destination = default)
        {
            Kind = kind;
            Target = target;
            Destination = destination;
            Score = score;
        }
    }

    public readonly struct ActionOption
    {
        public ActionKind Kind { get; }
        public Character Target { get; }
        public SkillBase Skill { get; }
        public float Score { get; }

        public ActionOption(ActionKind kind, float score, Character target = null, SkillBase skill = null)
        {
            Kind = kind;
            Target = target;
            Skill = skill;
            Score = score;
        }
    }

    public readonly struct Decision
    {
        public MoveOption Move { get; }
        public ActionOption Action { get; }

        public Decision(MoveOption move, ActionOption action)
        {
            Move = move;
            Action = action;
        }
    }

    [SerializeField, Min(0.02f)] private float _decisionInterval = 0.2f;
    [SerializeField, Min(0.02f)] private float _moveCommandInterval = 0.5f;
    [SerializeField, Range(0.01f, 1f)] private float _lowHpRetreatThreshold = 0.35f;
    [SerializeField, Range(0.01f, 1f)] private float _criticalHpRetreatThreshold = 0.2f;
    [SerializeField, Min(0f)] private float _homeDefenseRadius = 8f;
    [SerializeField, Min(0.1f)] private float _followDistance = 4f;
    [SerializeField, Min(0.1f)] private float _patrolRadius = 8f;
    [SerializeField, Min(0.01f)] private float _patrolArrivalDistance = 1.25f;
    [SerializeField, Min(0.01f)] private float _lastKnownArrivalDistance = 1f;
    [SerializeField, Min(0f)] private float _pursueLastKnownScore = 90f;
    [SerializeField, Min(0f)] private float _moveIntentLockSeconds = 2f;
    [SerializeField, Min(0f)] private float _moveIntentSwitchMargin = 15f;
    [SerializeField, Min(0f)] private float _highGroundThreshold = 5f;
    [SerializeField, Min(1)] private int _highGroundSearchSamples = 8;
    [SerializeField, Min(1f)] private float _highGroundSearchRadius = 15f;
    [SerializeField, Min(1f)] private float _forestSearchRadius = 15f;
    [SerializeField, Min(1)] private int _forestSearchSamples = 8;
    [SerializeField] private Transform[] _patrolPoints = new Transform[0];

    private Character _owner;
    private CombatVision _vision;
    private CombatHealth _health;
    private CombatAttack _attack;
    private CombatCharacterBody _body;
    private CombatCharacterSystem _characterSystem;
    private CombatMapSystem _mapSystem;
    private CombatSkillCooldowns _skillCooldowns;
    private float _nextDecisionTime;
    private float _nextMoveCommandTime;
    private Decision _lastDecision = new Decision(
        new MoveOption(MoveKind.Idle, 0f),
        new ActionOption(ActionKind.None, 0f));
    private Vector3 _spawnPosition;
    private bool _hasPatrolDestination;
    private Vector3 _patrolDestination;
    private int _patrolPointIndex;
    private bool _hasMoveIntentLock;
    private MoveOption _lockedMove;
    private float _moveIntentLockedUntil;

    public Character CurrentTarget { get; private set; }

    private void Awake()
    {
        ResolveComponents();
        _spawnPosition = transform.position;
    }

    private void Update()
    {
        if (Time.time < _nextDecisionTime) return;

        _nextDecisionTime = Time.time + _decisionInterval;
        Tick();
    }

    public Decision Decide()
    {
        ResolveComponents();

        if (_health == null || !_health.CanAct)
        {
            return new Decision(
                new MoveOption(MoveKind.Idle, 0f),
                new ActionOption(ActionKind.None, 0f));
        }

        _vision?.UpdateVision();

        List<Character> visibleTargets = GetVisibleTargets();
        Character nearestVisibleTarget = FindNearest(visibleTargets);
        UpdateCurrentTarget(visibleTargets, nearestVisibleTarget);

        bool isPursuingMemory = IsPursuingRememberedTarget();
        List<MoveOption> moveOptions = BuildMoveOptions(visibleTargets, nearestVisibleTarget, isPursuingMemory);
        MoveOption bestMove = PickBestMoveOption(moveOptions);
        MoveOption move = ApplyMoveIntentStability(bestMove, moveOptions, visibleTargets);
        ActionOption action = PickBestActionOption(BuildActionOptions(visibleTargets));
        return new Decision(move, action);
    }

    public void Tick()
    {
        Decision decision = Decide();
        _lastDecision = decision;

        if (_health == null || !_health.CanAct)
        {
            return;
        }

        ExecuteMove(decision.Move);
        ExecuteAction(decision.Action);
    }

    public Decision GetLastDecision()
    {
        return _lastDecision;
    }

    private List<MoveOption> BuildMoveOptions(
        List<Character> visibleTargets,
        Character nearestVisibleTarget,
        bool isPursuingMemory)
    {
        WeaponBase weapon = GetCurrentWeapon();
        var options = new List<MoveOption>
        {
            new MoveOption(MoveKind.Idle, 0f),
        };

        AddRetreatOption(options);
        AddChaseOption(options, nearestVisibleTarget, weapon);
        AddCombatHoldOption(options, visibleTargets, weapon);
        AddLastKnownPositionOption(options, isPursuingMemory);
        AddDefendHomeBaseOption(options, visibleTargets);
        if (!isPursuingMemory)
        {
            AddFollowAllyOption(options, weapon, visibleTargets);
        }

        AddSeekHighGroundOption(options, weapon);
        if (!isPursuingMemory)
        {
            AddHideInForestOption(options, weapon, visibleTargets);
            AddAssaultEnemyBaseOption(options, visibleTargets.Count == 0);
            AddPatrolOption(options);
        }

        return options;
    }

    private List<ActionOption> BuildActionOptions(List<Character> visibleTargets)
    {
        var options = new List<ActionOption>
        {
            new ActionOption(ActionKind.None, 0f),
        };

        WeaponBase weapon = GetCurrentWeapon();
        float bestSkillScore = GetBestSkillScore(visibleTargets);
        Character target = FindBestAttackTarget(visibleTargets);
        if (target != null)
        {
            float attackScore = ResolveAttackEnemyScore(weapon, bestSkillScore);
            options.Add(new ActionOption(ActionKind.AttackEnemy, attackScore, target));
        }

        AddUseSkillOptions(options, visibleTargets);

        return options;
    }

    private void AddUseSkillOptions(List<ActionOption> options, List<Character> visibleTargets)
    {
        WeaponBase weapon = GetCurrentWeapon();
        IReadOnlyList<SkillBase> skills = weapon.Skills;
        if (skills == null || skills.Count == 0) return;

        for (int i = 0; i < skills.Count; i++)
        {
            SkillBase skill = skills[i];
            if (skill == null) continue;
            if (_skillCooldowns != null && !_skillCooldowns.IsReady(skill)) continue;

            List<Character> candidates = GetSkillTargetCandidates(skill, visibleTargets);
            if (candidates.Count == 0) continue;

            Character bestTarget = null;
            float bestScore = float.NegativeInfinity;
            for (int j = 0; j < candidates.Count; j++)
            {
                Character target = candidates[j];
                if (!IsValidSkillTarget(skill, target)) continue;

                float score = skill.EvaluateScore(_owner, target);
                if (score <= bestScore) continue;

                bestTarget = target;
                bestScore = score;
            }

            if (bestTarget == null || bestScore <= 0f) continue;

            options.Add(new ActionOption(ActionKind.UseSkill, bestScore, bestTarget, skill));
        }
    }

    private List<Character> GetSkillTargetCandidates(SkillBase skill, List<Character> visibleTargets)
    {
        var candidates = new List<Character>();
        if (skill == null) return candidates;

        switch (skill.TargetKind)
        {
            case SkillTargetKind.Ally:
            case SkillTargetKind.AllyOrSelf:
                AddAllySkillCandidates(candidates, includeSelf: skill.TargetKind == SkillTargetKind.AllyOrSelf);
                break;
            default:
                candidates.AddRange(visibleTargets);
                break;
        }

        return candidates;
    }

    private void AddAllySkillCandidates(List<Character> candidates, bool includeSelf)
    {
        if (ResolveCharacterSystem() == null) return;

        IReadOnlyList<Character> allies = _characterSystem.GetAlliesOf(_owner);
        for (int i = 0; i < allies.Count; i++)
        {
            Character ally = allies[i];
            if (ally == null) continue;
            if (!includeSelf && ally == _owner) continue;

            candidates.Add(ally);
        }

        if (includeSelf && _owner != null && !candidates.Contains(_owner))
        {
            candidates.Add(_owner);
        }
    }

    private bool IsValidSkillTarget(SkillBase skill, Character target)
    {
        if (target == null || target.Health == null || _owner == null) return false;

        switch (skill.TargetKind)
        {
            case SkillTargetKind.Ally:
            case SkillTargetKind.AllyOrSelf:
                if (target == _owner) return target.Health.CanAct;
                return target.Team == _owner.Team && target.Health.CanAct;
            default:
                return IsValidTarget(target);
        }
    }

    private void AddRetreatOption(List<MoveOption> options)
    {
        if (_health == null || _health.MaxHP <= 0) return;

        float hpRatio = GetHpRatio();
        if (hpRatio > _lowHpRetreatThreshold) return;

        if (ResolveCharacterSystem() != null &&
            _characterSystem.TryGetHomePosition(_owner, out Vector3 homePosition))
        {
            float score = hpRatio <= _criticalHpRetreatThreshold ? 120f : 90f;
            options.Add(new MoveOption(MoveKind.RetreatToHome, score, destination: homePosition));
        }
    }

    private void AddChaseOption(List<MoveOption> options, Character nearestVisibleTarget, WeaponBase weapon)
    {
        if (nearestVisibleTarget == null || _attack == null) return;

        float distance = Vector3.Distance(transform.position, nearestVisibleTarget.transform.position);
        float range = _attack.CurrentWeapon.Range;
        float chaseThreshold = range * GetChaseStopRatio(weapon);
        if (distance <= chaseThreshold) return;

        float score = 85f + weapon.ChaseEnemyBias;
        if (weapon.HideInForestBias > 0f)
        {
            score -= weapon.HideInForestBias * 0.25f;
        }

        options.Add(new MoveOption(
            MoveKind.ChaseEnemy,
            score,
            nearestVisibleTarget,
            nearestVisibleTarget.transform.position));
    }

    private void AddCombatHoldOption(List<MoveOption> options, List<Character> visibleTargets, WeaponBase weapon)
    {
        if (_attack == null) return;

        for (int i = 0; i < visibleTargets.Count; i++)
        {
            Character target = visibleTargets[i];
            if (!_attack.IsInRange(target)) continue;

            float holdScore = ResolveCombatHoldScore(weapon);
            options.Add(new MoveOption(MoveKind.Idle, holdScore, target, transform.position));
            return;
        }
    }

    private void AddLastKnownPositionOption(List<MoveOption> options, bool isPursuingMemory)
    {
        if (!isPursuingMemory || CurrentTarget == null || _vision == null) return;

        if (!IsValidTarget(CurrentTarget) || !_vision.HasMemoryOf(CurrentTarget))
        {
            CurrentTarget = null;
            return;
        }

        if (!_vision.TryGetLastKnownPosition(CurrentTarget, out Vector3 lastKnownPosition))
        {
            CurrentTarget = null;
            return;
        }

        float score = _pursueLastKnownScore;
        float remaining = _vision.GetMemoryRemainingSeconds(CurrentTarget);
        if (remaining <= 3f)
        {
            score += 5f;
        }

        if (IsArrivedAt(lastKnownPosition, _lastKnownArrivalDistance))
        {
            BoostMoveOptionScore(options, MoveKind.Idle, score);
            return;
        }

        options.Add(new MoveOption(
            MoveKind.MoveToLastKnownEnemyPosition,
            score,
            CurrentTarget,
            lastKnownPosition));
    }

    private void AddDefendHomeBaseOption(List<MoveOption> options, List<Character> visibleTargets)
    {
        if (ResolveCharacterSystem() == null ||
            !_characterSystem.TryGetMainStoneHomePosition(_owner, out Vector3 homePosition))
        {
            return;
        }

        float score = 55f;
        bool canBoostDefense = GetHpRatio() > _lowHpRetreatThreshold;
        for (int i = 0; canBoostDefense && i < visibleTargets.Count; i++)
        {
            Character target = visibleTargets[i];
            if (Vector3.Distance(homePosition, target.transform.position) <= _homeDefenseRadius)
            {
                score += 40f;
                break;
            }
        }

        options.Add(new MoveOption(MoveKind.DefendHomeBase, score, destination: homePosition));
    }

    private void AddFollowAllyOption(List<MoveOption> options, WeaponBase weapon, List<Character> visibleTargets)
    {
        if (ResolveCharacterSystem() == null) return;

        Character ally = weapon.FollowMeleeAllyBias > 0f
            ? FindNearestMeleeAllyOutsideFollowDistance()
            : FindNearestAllyOutsideFollowDistance();
        if (ally == null) return;

        float distance = Vector3.Distance(transform.position, ally.transform.position);
        float score = Mathf.Lerp(45f, 75f, Mathf.Clamp01((distance - _followDistance) / _followDistance));
        score += weapon.FollowMeleeAllyBias;
        if (weapon.FollowMeleeAllyBias > 0f && visibleTargets.Count > 0)
        {
            score += 20f;
        }

        options.Add(new MoveOption(MoveKind.FollowAlly, score, ally, ally.transform.position));
    }

    private void AddSeekHighGroundOption(List<MoveOption> options, WeaponBase weapon)
    {
        if (weapon.SeekHighGroundBias <= 0f) return;

        CombatMapSystem mapSystem = ResolveMapSystem();
        if (mapSystem == null || !mapSystem.TryGetTerrainInfo(transform.position, out TerrainInfo currentTerrain))
        {
            return;
        }

        if (currentTerrain.Height >= _highGroundThreshold)
        {
            BoostMoveOptionScore(options, MoveKind.Idle, weapon.SeekHighGroundBias);
            return;
        }

        if (!TryFindHighGroundDestination(out Vector3 destination))
        {
            return;
        }

        options.Add(new MoveOption(
            MoveKind.MoveToHighGround,
            weapon.SeekHighGroundBias,
            destination: destination));
    }

    private void AddHideInForestOption(List<MoveOption> options, WeaponBase weapon, List<Character> visibleTargets)
    {
        if (weapon.HideInForestBias <= 0f) return;

        CombatMapSystem mapSystem = ResolveMapSystem();
        if (mapSystem == null || !mapSystem.TryGetTerrainInfo(transform.position, out TerrainInfo currentTerrain))
        {
            return;
        }

        if (currentTerrain.IsForest)
        {
            for (int i = 0; i < visibleTargets.Count; i++)
            {
                Character target = visibleTargets[i];
                if (_attack == null || !_attack.IsInRange(target)) continue;

                BoostMoveOptionScore(options, MoveKind.Idle, weapon.HideInForestBias);
                return;
            }

            return;
        }

        if (!TryFindForestDestination(out Vector3 destination))
        {
            return;
        }

        float hideScore = weapon.HideInForestBias;
        if (visibleTargets.Count > 0)
        {
            hideScore += 25f;
        }

        options.Add(new MoveOption(
            MoveKind.HideInForest,
            hideScore,
            destination: destination));
    }

    private void AddAssaultEnemyBaseOption(List<MoveOption> options, bool hasNoVisibleTargets)
    {
        if (ResolveCharacterSystem() == null ||
            !_characterSystem.TryGetEnemyHomePosition(_owner, out Vector3 enemyHomePosition))
        {
            return;
        }

        float score = hasNoVisibleTargets ? 60f : 40f;
        options.Add(new MoveOption(MoveKind.AssaultEnemyBase, score, destination: enemyHomePosition));
    }

    private void AddPatrolOption(List<MoveOption> options)
    {
        if (!TryGetPatrolDestination(out Vector3 destination)) return;

        options.Add(new MoveOption(MoveKind.Patrol, 20f, destination: destination));
    }

    private MoveOption PickBestMoveOption(List<MoveOption> options)
    {
        MoveOption best = options[0];
        for (int i = 1; i < options.Count; i++)
        {
            if (options[i].Score > best.Score)
            {
                best = options[i];
            }
        }

        return best;
    }

    private MoveOption ApplyMoveIntentStability(
        MoveOption best,
        List<MoveOption> options,
        List<Character> visibleTargets)
    {
        if (ShouldSwitchMoveIntentImmediately(best, visibleTargets))
        {
            UpdateMoveIntentLock(best);
            return best;
        }

        if (_hasMoveIntentLock &&
            Time.time < _moveIntentLockedUntil &&
            TryFindEquivalentMoveOption(_lockedMove, options, out MoveOption equivalent))
        {
            if (best.Score < _lockedMove.Score + _moveIntentSwitchMargin)
            {
                UpdateMoveIntentLock(equivalent);
                return equivalent;
            }
        }

        UpdateMoveIntentLock(best);
        return best;
    }

    private bool ShouldSwitchMoveIntentImmediately(MoveOption best, List<Character> visibleTargets)
    {
        if (best.Kind == MoveKind.RetreatToHome) return true;
        if (best.Kind == MoveKind.MoveToLastKnownEnemyPosition) return true;

        if (best.Kind == MoveKind.Idle &&
            best.Target != null &&
            _attack != null &&
            _attack.CanAttack(best.Target))
        {
            return true;
        }

        return false;
    }

    private bool TryFindEquivalentMoveOption(
        MoveOption locked,
        List<MoveOption> options,
        out MoveOption equivalent)
    {
        for (int i = 0; i < options.Count; i++)
        {
            MoveOption option = options[i];
            if (!AreMoveTargetsEquivalent(locked, option)) continue;

            equivalent = option;
            return true;
        }

        if (locked.Kind == MoveKind.MoveToLastKnownEnemyPosition &&
            IsPursuingRememberedTarget())
        {
            for (int i = 0; i < options.Count; i++)
            {
                MoveOption option = options[i];
                if (option.Kind != MoveKind.Idle) continue;
                if (option.Target != CurrentTarget) continue;

                equivalent = option;
                return true;
            }
        }

        equivalent = default;
        return false;
    }

    private static bool AreMoveTargetsEquivalent(MoveOption a, MoveOption b)
    {
        if (a.Kind != b.Kind) return false;
        return a.Target == b.Target;
    }

    private void UpdateMoveIntentLock(MoveOption move)
    {
        if (move.Kind == MoveKind.Idle)
        {
            if (!IsPursuingRememberedTarget() || move.Score < _pursueLastKnownScore * 0.5f)
            {
                _hasMoveIntentLock = false;
                return;
            }
        }

        _lockedMove = move;
        _hasMoveIntentLock = true;
        _moveIntentLockedUntil = Time.time + _moveIntentLockSeconds;
    }

    private ActionOption PickBestActionOption(List<ActionOption> options)
    {
        ActionOption best = options[0];
        for (int i = 1; i < options.Count; i++)
        {
            if (options[i].Score > best.Score)
            {
                best = options[i];
            }
        }

        return best;
    }

    private void ExecuteMove(MoveOption move)
    {
        if (move.Kind == MoveKind.Idle)
        {
            _body?.Stop();
            return;
        }

        TryMove(move.Destination);
    }

    private void ExecuteAction(ActionOption action)
    {
        if (action.Kind == ActionKind.AttackEnemy)
        {
            _attack?.TryAttack(action.Target);
            return;
        }

        if (action.Kind == ActionKind.UseSkill)
        {
            if (action.Skill == null) return;
            if (_skillCooldowns != null && !_skillCooldowns.IsReady(action.Skill)) return;

            action.Skill.Execute(_owner, action.Target);
            _skillCooldowns?.StartCooldown(action.Skill);
        }
    }

    private void UpdateCurrentTarget(List<Character> visibleTargets, Character nearestVisibleTarget)
    {
        if (nearestVisibleTarget != null)
        {
            CurrentTarget = nearestVisibleTarget;
            return;
        }

        if (CurrentTarget != null &&
            _vision != null &&
            _vision.HasMemoryOf(CurrentTarget) &&
            IsValidTarget(CurrentTarget))
        {
            return;
        }

        CurrentTarget = FindBestRememberedTarget();
    }

    private bool IsPursuingRememberedTarget()
    {
        if (CurrentTarget == null || _vision == null) return false;
        if (_vision.IsVisible(CurrentTarget)) return false;

        return _vision.HasMemoryOf(CurrentTarget) && IsValidTarget(CurrentTarget);
    }

    private Character FindBestRememberedTarget()
    {
        if (_vision == null) return null;

        IReadOnlyList<Character> remembered = _vision.RememberedEnemies;
        Character best = null;
        float bestSqrDistance = float.PositiveInfinity;
        for (int i = 0; i < remembered.Count; i++)
        {
            Character enemy = remembered[i];
            if (!IsValidTarget(enemy)) continue;
            if (!_vision.TryGetLastKnownPosition(enemy, out Vector3 lastKnownPosition)) continue;

            float sqrDistance = (lastKnownPosition - transform.position).sqrMagnitude;
            if (sqrDistance >= bestSqrDistance) continue;

            best = enemy;
            bestSqrDistance = sqrDistance;
        }

        return best;
    }

    private List<Character> GetVisibleTargets()
    {
        var targets = new List<Character>();
        if (_vision == null) return targets;

        IReadOnlyList<Character> visibleEnemies = _vision.VisibleEnemies;
        for (int i = 0; i < visibleEnemies.Count; i++)
        {
            Character enemy = visibleEnemies[i];
            if (!IsValidTarget(enemy)) continue;

            targets.Add(enemy);
        }

        return targets;
    }

    private Character FindNearest(List<Character> targets)
    {
        Character nearest = null;
        float nearestSqrDistance = float.PositiveInfinity;
        for (int i = 0; i < targets.Count; i++)
        {
            Character target = targets[i];
            float sqrDistance = (target.transform.position - transform.position).sqrMagnitude;
            if (sqrDistance >= nearestSqrDistance) continue;

            nearest = target;
            nearestSqrDistance = sqrDistance;
        }

        return nearest;
    }

    private Character FindBestAttackTarget(List<Character> visibleTargets)
    {
        Character best = null;
        float bestSqrDistance = float.PositiveInfinity;
        for (int i = 0; i < visibleTargets.Count; i++)
        {
            Character target = visibleTargets[i];
            if (_attack == null || !_attack.CanAttack(target)) continue;

            float sqrDistance = (target.transform.position - transform.position).sqrMagnitude;
            if (sqrDistance >= bestSqrDistance) continue;

            best = target;
            bestSqrDistance = sqrDistance;
        }

        return best;
    }

    private Character FindNearestAllyOutsideFollowDistance()
    {
        if (_characterSystem == null) return null;

        IReadOnlyList<Character> allies = _characterSystem.GetAlliesOf(_owner);
        Character nearest = null;
        float nearestSqrDistance = float.PositiveInfinity;
        for (int i = 0; i < allies.Count; i++)
        {
            Character ally = allies[i];
            if (ally == null || ally == _owner) continue;
            if (ally.Health != null && !ally.Health.IsTargetable) continue;

            float distance = Vector3.Distance(transform.position, ally.transform.position);
            if (distance <= _followDistance) continue;

            float sqrDistance = distance * distance;
            if (sqrDistance >= nearestSqrDistance) continue;

            nearest = ally;
            nearestSqrDistance = sqrDistance;
        }

        return nearest;
    }

    private Character FindNearestMeleeAllyOutsideFollowDistance()
    {
        if (_characterSystem == null) return null;

        IReadOnlyList<Character> allies = _characterSystem.GetAlliesOf(_owner);
        Character nearest = null;
        float nearestSqrDistance = float.PositiveInfinity;
        for (int i = 0; i < allies.Count; i++)
        {
            Character ally = allies[i];
            if (ally == null || ally == _owner) continue;
            if (ally.Health != null && !ally.Health.IsTargetable) continue;
            if (!IsMeleeWeaponKind(GetWeaponKind(ally))) continue;

            float distance = Vector3.Distance(transform.position, ally.transform.position);
            if (distance <= _followDistance) continue;

            float sqrDistance = distance * distance;
            if (sqrDistance >= nearestSqrDistance) continue;

            nearest = ally;
            nearestSqrDistance = sqrDistance;
        }

        return nearest;
    }

    private static bool IsMeleeWeaponKind(WeaponKind kind)
    {
        return kind == WeaponKind.Sword || kind == WeaponKind.Shield;
    }

    private static WeaponKind GetWeaponKind(Character character)
    {
        if (character == null) return WeaponKind.Unarmed;

        WeaponBase weapon = character.EquippedWeapon;
        return weapon != null ? weapon.Kind : WeaponKind.Unarmed;
    }

    private WeaponBase GetCurrentWeapon()
    {
        if (_attack != null) return _attack.CurrentWeapon;
        return _owner != null && _owner.EquippedWeapon != null ? _owner.EquippedWeapon : WeaponBase.Unarmed;
    }

    private const float DefaultAttackScore = 100f;
    private const float SupportWeaponAttackScore = 82f;
    private const float BaseCombatHoldScore = 80f;
    private const float ReducedCombatHoldScore = 40f;

    private static float GetChaseStopRatio(WeaponBase weapon)
    {
        if (weapon.HideInForestBias > 0f) return 0.98f;
        if (weapon.Range >= 5f) return 0.95f;
        return 0.8f;
    }

    private float ResolveCombatHoldScore(WeaponBase weapon)
    {
        if (weapon.HideInForestBias > 0f)
        {
            if (IsOwnerInForest())
            {
                return BaseCombatHoldScore + weapon.HideInForestBias * 0.15f;
            }

            return ReducedCombatHoldScore;
        }

        if (weapon.SeekHighGroundBias > 0f &&
            TryGetOwnerTerrain(out TerrainInfo terrain) &&
            terrain.Height >= _highGroundThreshold)
        {
            return BaseCombatHoldScore + weapon.SeekHighGroundBias * 0.2f;
        }

        return BaseCombatHoldScore;
    }

    private bool IsOwnerInForest()
    {
        return TryGetOwnerTerrain(out TerrainInfo terrain) && terrain.IsForest;
    }

    private bool TryGetOwnerTerrain(out TerrainInfo terrain)
    {
        terrain = default;
        CombatMapSystem mapSystem = ResolveMapSystem();
        return mapSystem != null && mapSystem.TryGetTerrainInfo(transform.position, out terrain);
    }

    private float GetBestSkillScore(List<Character> visibleTargets)
    {
        WeaponBase weapon = GetCurrentWeapon();
        IReadOnlyList<SkillBase> skills = weapon.Skills;
        if (skills == null || skills.Count == 0) return 0f;

        float bestScore = 0f;
        for (int i = 0; i < skills.Count; i++)
        {
            SkillBase skill = skills[i];
            if (skill == null) continue;
            if (_skillCooldowns != null && !_skillCooldowns.IsReady(skill)) continue;

            List<Character> candidates = GetSkillTargetCandidates(skill, visibleTargets);
            for (int j = 0; j < candidates.Count; j++)
            {
                Character target = candidates[j];
                if (!IsValidSkillTarget(skill, target)) continue;

                float score = skill.EvaluateScore(_owner, target);
                if (score > bestScore)
                {
                    bestScore = score;
                }
            }
        }

        return bestScore;
    }

    private static float ResolveAttackEnemyScore(WeaponBase weapon, float bestSkillScore)
    {
        if (bestSkillScore <= 0f) return DefaultAttackScore;
        if (HasAllyOrSelfSupportSkill(weapon)) return SupportWeaponAttackScore;
        if (bestSkillScore >= 85f) return bestSkillScore - 5f;
        return DefaultAttackScore;
    }

    private static bool HasAllyOrSelfSupportSkill(WeaponBase weapon)
    {
        IReadOnlyList<SkillBase> skills = weapon.Skills;
        if (skills == null) return false;

        for (int i = 0; i < skills.Count; i++)
        {
            SkillBase skill = skills[i];
            if (skill == null) continue;
            if (skill.TargetKind == SkillTargetKind.Ally ||
                skill.TargetKind == SkillTargetKind.AllyOrSelf)
            {
                return true;
            }
        }

        return false;
    }

    private static void BoostMoveOptionScore(List<MoveOption> options, MoveKind kind, float bonus)
    {
        for (int i = 0; i < options.Count; i++)
        {
            MoveOption option = options[i];
            if (option.Kind != kind) continue;

            options[i] = new MoveOption(option.Kind, option.Score + bonus, option.Target, option.Destination);
            return;
        }

        if (kind == MoveKind.Idle)
        {
            options.Add(new MoveOption(MoveKind.Idle, bonus));
        }
    }

    private bool TryFindHighGroundDestination(out Vector3 destination)
    {
        destination = default;
        CombatMapSystem mapSystem = ResolveMapSystem();
        if (mapSystem == null) return false;

        Vector3 origin = transform.position;
        float currentHeight = mapSystem.TryGetTerrainInfo(origin, out TerrainInfo currentTerrain)
            ? currentTerrain.Height
            : origin.y;
        float bestHeight = float.NegativeInfinity;
        bool found = false;

        for (int i = 0; i < _highGroundSearchSamples; i++)
        {
            float angle = i * (360f / _highGroundSearchSamples) * Mathf.Deg2Rad;
            Vector3 sample = origin + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * _highGroundSearchRadius;
            if (!mapSystem.TryGetTerrainInfo(sample, out TerrainInfo info)) continue;
            if (!info.IsInBounds) continue;
            if (info.Height <= bestHeight) continue;

            bestHeight = info.Height;
            destination = new Vector3(sample.x, info.Height, sample.z);
            found = true;
        }

        return found && bestHeight > currentHeight;
    }

    private bool TryFindForestDestination(out Vector3 destination)
    {
        destination = default;
        CombatMapSystem mapSystem = ResolveMapSystem();
        if (mapSystem == null) return false;

        Vector3 origin = transform.position;
        float nearestDistance = float.PositiveInfinity;
        bool found = false;

        for (int i = 0; i < _forestSearchSamples; i++)
        {
            float angle = i * (360f / _forestSearchSamples) * Mathf.Deg2Rad;
            Vector3 sample = origin + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * _forestSearchRadius;
            if (!mapSystem.TryGetTerrainInfo(sample, out TerrainInfo info)) continue;
            if (!info.IsInBounds || !info.IsForest) continue;

            float distance = Vector3.Distance(origin, sample);
            if (distance >= nearestDistance) continue;

            nearestDistance = distance;
            destination = new Vector3(sample.x, info.Height, sample.z);
            found = true;
        }

        return found;
    }

    private CombatMapSystem ResolveMapSystem()
    {
        if (_mapSystem != null) return _mapSystem;

        CombatSceneContext context = CombatSceneContext.Instance;
        if (context != null && context.MapSystem != null)
        {
            _mapSystem = context.MapSystem;
            return _mapSystem;
        }

        _mapSystem = FindAnyObjectByType<CombatMapSystem>();
        return _mapSystem;
    }

    private bool IsValidTarget(Character target)
    {
        if (target == null || _attack == null) return false;
        if (!_attack.IsEnemyTarget(target)) return false;

        return target.Health != null && target.Health.IsTargetable;
    }

    private float GetHpRatio()
    {
        if (_health == null || _health.MaxHP <= 0) return 1f;

        return _health.HP / (float)_health.MaxHP;
    }

    private bool IsArrivedAt(Vector3 destination, float arrivalDistance)
    {
        Vector3 currentPosition = transform.position;
        currentPosition.y = 0f;
        destination.y = 0f;

        return Vector3.Distance(currentPosition, destination) <= arrivalDistance;
    }

    private bool TryGetPatrolDestination(out Vector3 destination)
    {
        destination = default;

        if (_patrolPoints != null && _patrolPoints.Length > 0)
        {
            Transform point = _patrolPoints[Mathf.Abs(_patrolPointIndex) % _patrolPoints.Length];
            if (point == null)
            {
                _patrolPointIndex++;
                return false;
            }

            destination = point.position;
            if (IsArrivedAt(destination, _patrolArrivalDistance))
            {
                _patrolPointIndex++;
                point = _patrolPoints[Mathf.Abs(_patrolPointIndex) % _patrolPoints.Length];
                if (point == null) return false;
                destination = point.position;
            }

            return true;
        }

        if (!_hasPatrolDestination || IsArrivedAt(_patrolDestination, _patrolArrivalDistance))
        {
            _patrolDestination = CreateRandomPatrolDestination();
            _hasPatrolDestination = true;
        }

        destination = _patrolDestination;
        return true;
    }

    private Vector3 CreateRandomPatrolDestination()
    {
        Vector2 offset = Random.insideUnitCircle * _patrolRadius;
        return _spawnPosition + new Vector3(offset.x, 0f, offset.y);
    }

    private void TryMove(Vector3 destination)
    {
        if (_body == null || Time.time < _nextMoveCommandTime) return;

        _nextMoveCommandTime = Time.time + _moveCommandInterval;
        bool moved = _body.TrySetDestination(destination);
        if (!moved)
        {
            _hasPatrolDestination = false;
        }
    }

    private CombatCharacterSystem ResolveCharacterSystem()
    {
        if (_characterSystem != null) return _characterSystem;

        CombatSceneContext context = CombatSceneContext.Instance;
        if (context != null && context.CharacterSystem != null)
        {
            _characterSystem = context.CharacterSystem;
            return _characterSystem;
        }

        _characterSystem = FindAnyObjectByType<CombatCharacterSystem>();
        return _characterSystem;
    }

    private void ResolveComponents()
    {
        _owner ??= GetComponent<Character>();
        _vision ??= GetComponent<CombatVision>();
        _health ??= GetComponent<CombatHealth>();
        _attack ??= GetComponent<CombatAttack>();
        _body ??= GetComponent<CombatCharacterBody>();
        _skillCooldowns ??= _owner != null ? _owner.SkillCooldowns : GetComponent<CombatSkillCooldowns>();
        ResolveCharacterSystem();
        ResolveMapSystem();
    }
}
