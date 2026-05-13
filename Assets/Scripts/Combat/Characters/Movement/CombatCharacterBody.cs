using System;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public sealed class CombatCharacterBody : MonoBehaviour
{
    [SerializeField] private CombatMapSystem _mapSystem;
    [SerializeField] private CombatNavigationSystem _navigationSystem;

    [Header("Wind")]
    [Tooltip("風の影響をどれくらい受けるかの係数")]
    [SerializeField] private float _windEffectMultiplier = 0.5f;

    [Tooltip("向かい風で遅くなる際の最低速度倍率")]
    [SerializeField] private float _minWindSpeedRatio = 0.2f;

    private NavMeshAgent _agent;
    private float _baseSpeed;
    private bool _hasVisiblePath;

    public bool IsMoving =>
        _agent != null &&
        _agent.isOnNavMesh &&
        !_agent.isStopped &&
        _agent.hasPath &&
        !_agent.pathPending &&
        _agent.remainingDistance > Mathf.Max(_agent.stoppingDistance, 0.05f);

    public float BaseSpeed
    {
        get => _baseSpeed;
        set
        {
            _baseSpeed = Mathf.Max(0f, value);
            if (_agent != null) _agent.speed = _baseSpeed;
        }
    }

    public event Action<Vector3[]> RouteChanged;

    public Vector3[] CurrentRouteCorners { get; private set; } = Array.Empty<Vector3>();

    private void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        _baseSpeed = _agent != null ? _agent.speed : 0f;
    }

    private void Update()
    {
        UpdateMoveSpeed();
        ClearRouteIfArrived();
    }

    public bool TrySetDestination(Vector3 worldPosition)
    {
        if (_agent == null || !_agent.isOnNavMesh) return false;

        if (!ResolveNavigationSystem().TryResolveDestination(
                _agent,
                worldPosition,
                out Vector3 destination,
                out NavMeshPath path))
        {
            return false;
        }

        _agent.isStopped = false;
        if (!_agent.SetDestination(destination)) return false;

        SetRoute(path.corners);
        return true;
    }

    public void Stop()
    {
        if (_agent == null || !_agent.isOnNavMesh) return;

        _agent.isStopped = true;
        _agent.ResetPath();
        _agent.speed = _baseSpeed;
        ClearRoute();
    }

    public float GetTerrainSpeedMultiplier()
    {
        CombatMapSystem mapSystem = ResolveMapSystem();
        if (mapSystem == null ||
            !mapSystem.TryGetTraversalInfo(transform.position, out TerrainTraversalInfo traversalInfo))
        {
            return 1f;
        }

        return traversalInfo.MoveSpeedMultiplier;
    }

    private void UpdateMoveSpeed()
    {
        if (_agent == null) return;

        if (!_agent.isOnNavMesh || !_agent.hasPath)
        {
            _agent.speed = _baseSpeed;
            return;
        }

        float terrainMultiplier = GetTerrainSpeedMultiplier();
        float windMultiplier = GetWindSpeedMultiplier();
        _agent.speed = _baseSpeed * terrainMultiplier * windMultiplier;
    }

    private void SetRoute(Vector3[] corners)
    {
        CurrentRouteCorners = corners != null ? (Vector3[])corners.Clone() : Array.Empty<Vector3>();
        _hasVisiblePath = CurrentRouteCorners.Length > 1;
        RouteChanged?.Invoke(CurrentRouteCorners);
    }

    private void ClearRoute()
    {
        if (!_hasVisiblePath && CurrentRouteCorners.Length == 0) return;

        CurrentRouteCorners = Array.Empty<Vector3>();
        _hasVisiblePath = false;
        RouteChanged?.Invoke(CurrentRouteCorners);
    }

    private void ClearRouteIfArrived()
    {
        if (!_hasVisiblePath || _agent == null || !_agent.isOnNavMesh || _agent.pathPending) return;

        float arrivalDistance = Mathf.Max(_agent.stoppingDistance, 0.05f);
        if (!_agent.hasPath || _agent.remainingDistance <= arrivalDistance)
        {
            ClearRoute();
        }
    }

    private float GetWindSpeedMultiplier()
    {
        CombatMapSystem mapSystem = ResolveMapSystem();
        if (mapSystem == null) return 1f;

        Vector3 windVector = mapSystem.WindVector;
        float windMagnitude = windVector.magnitude;
        if (windMagnitude < Mathf.Epsilon) return 1f;

        Vector3 moveDirection = _agent.desiredVelocity.normalized;
        if (moveDirection.sqrMagnitude < Mathf.Epsilon) return 1f;

        float dotProduct = Vector3.Dot(moveDirection, windVector.normalized);
        float speedRatio = 1f + dotProduct * windMagnitude * _windEffectMultiplier;
        return Mathf.Max(_minWindSpeedRatio, speedRatio);
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

    private CombatNavigationSystem ResolveNavigationSystem()
    {
        if (_navigationSystem != null) return _navigationSystem;

        _navigationSystem = FindAnyObjectByType<CombatNavigationSystem>();
        if (_navigationSystem != null) return _navigationSystem;

        _navigationSystem = gameObject.AddComponent<CombatNavigationSystem>();
        return _navigationSystem;
    }
}
