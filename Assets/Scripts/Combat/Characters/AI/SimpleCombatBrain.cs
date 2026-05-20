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
        public float Score { get; }

        public ActionOption(ActionKind kind, float score, Character target = null)
        {
            Kind = kind;
            Target = target;
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
    private float _nextDecisionTime;
    private float _nextMoveCommandTime;
    private Decision _lastDecision = new Decision(
        new MoveOption(MoveKind.Idle, 0f),
        new ActionOption(ActionKind.None, 0f));
    private Vector3 _spawnPosition;
    private bool _hasPatrolDestination;
    private Vector3 _patrolDestination;
    private int _patrolPointIndex;

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
        if (nearestVisibleTarget != null)
        {
            CurrentTarget = nearestVisibleTarget;
        }

        MoveOption move = PickBestMoveOption(BuildMoveOptions(visibleTargets, nearestVisibleTarget));
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

    private List<MoveOption> BuildMoveOptions(List<Character> visibleTargets, Character nearestVisibleTarget)
    {
        WeaponBase weapon = GetCurrentWeapon();
        var options = new List<MoveOption>
        {
            new MoveOption(MoveKind.Idle, 0f),
        };

        AddRetreatOption(options);
        AddChaseOption(options, nearestVisibleTarget, weapon);
        AddCombatHoldOption(options, visibleTargets);
        AddLastKnownPositionOption(options, visibleTargets.Count == 0);
        AddDefendHomeBaseOption(options, visibleTargets);
        AddFollowAllyOption(options, weapon);
        AddSeekHighGroundOption(options, weapon);
        AddHideInForestOption(options, weapon, visibleTargets);
        AddAssaultEnemyBaseOption(options, visibleTargets.Count == 0);
        AddPatrolOption(options);

        return options;
    }

    private List<ActionOption> BuildActionOptions(List<Character> visibleTargets)
    {
        var options = new List<ActionOption>
        {
            new ActionOption(ActionKind.None, 0f),
        };

        Character target = FindBestAttackTarget(visibleTargets);
        if (target != null)
        {
            options.Add(new ActionOption(ActionKind.AttackEnemy, 100f, target));
        }

        return options;
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
        float chaseThreshold = _attack.CurrentWeapon.Range * 0.8f;
        if (distance <= chaseThreshold) return;

        float score = 85f + weapon.ChaseEnemyBias;
        options.Add(new MoveOption(
            MoveKind.ChaseEnemy,
            score,
            nearestVisibleTarget,
            nearestVisibleTarget.transform.position));
    }

    private void AddCombatHoldOption(List<MoveOption> options, List<Character> visibleTargets)
    {
        if (_attack == null) return;

        for (int i = 0; i < visibleTargets.Count; i++)
        {
            Character target = visibleTargets[i];
            if (!_attack.IsInRange(target)) continue;

            options.Add(new MoveOption(MoveKind.Idle, 80f, target, transform.position));
            return;
        }
    }

    private void AddLastKnownPositionOption(List<MoveOption> options, bool hasNoVisibleTargets)
    {
        if (!hasNoVisibleTargets || CurrentTarget == null || _vision == null) return;

        if (!IsValidTarget(CurrentTarget))
        {
            CurrentTarget = null;
            return;
        }

        if (!_vision.TryGetLastKnownPosition(CurrentTarget, out Vector3 lastKnownPosition))
        {
            CurrentTarget = null;
            return;
        }

        if (IsArrivedAt(lastKnownPosition, _lastKnownArrivalDistance))
        {
            CurrentTarget = null;
            return;
        }

        options.Add(new MoveOption(
            MoveKind.MoveToLastKnownEnemyPosition,
            70f,
            CurrentTarget,
            lastKnownPosition));
    }

    private void AddDefendHomeBaseOption(List<MoveOption> options, List<Character> visibleTargets)
    {
        if (ResolveCharacterSystem() == null ||
            !_characterSystem.TryGetHomePosition(_owner, out Vector3 homePosition))
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

    private void AddFollowAllyOption(List<MoveOption> options, WeaponBase weapon)
    {
        if (ResolveCharacterSystem() == null) return;

        Character ally = weapon.FollowMeleeAllyBias > 0f
            ? FindNearestMeleeAllyOutsideFollowDistance()
            : FindNearestAllyOutsideFollowDistance();
        if (ally == null) return;

        float distance = Vector3.Distance(transform.position, ally.transform.position);
        float score = Mathf.Lerp(45f, 75f, Mathf.Clamp01((distance - _followDistance) / _followDistance));
        score += weapon.FollowMeleeAllyBias;
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

        options.Add(new MoveOption(
            MoveKind.HideInForest,
            weapon.HideInForestBias,
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
        if (action.Kind != ActionKind.AttackEnemy) return;

        _attack?.TryAttack(action.Target);
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
        ResolveCharacterSystem();
        ResolveMapSystem();
    }
}
