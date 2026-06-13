using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
public sealed class CombatNavigationSystem : MonoBehaviour
{
    [SerializeField] private CombatMapSystem _mapSystem;
    [SerializeField, Min(0.01f)] private float _navMeshSampleRadius = 2f;
    [SerializeField] private int _areaMask = NavMesh.AllAreas;

    public bool TryResolveDestination(
        NavMeshAgent agent,
        Vector3 requestedWorldPosition,
        out Vector3 destination)
    {
        return TryResolveDestination(agent, requestedWorldPosition, out destination, out _);
    }

    public bool TryResolveDestination(
        NavMeshAgent agent,
        Vector3 requestedWorldPosition,
        out Vector3 destination,
        out NavMeshPath path)
    {
        destination = default;
        path = null;
        if (agent == null || !agent.isOnNavMesh) return false;

        CombatMapSystem mapSystem = ResolveMapSystem();
        if (mapSystem == null || !mapSystem.CanStandAt(requestedWorldPosition)) return false;

        if (!NavMesh.SamplePosition(requestedWorldPosition, out NavMeshHit hit, _navMeshSampleRadius, _areaMask))
        {
            return false;
        }

        if (!mapSystem.CanStandAt(hit.position)) return false;

        var resolvedPath = new NavMeshPath();
        if (!agent.CalculatePath(hit.position, resolvedPath)) return false;
        if (resolvedPath.status != NavMeshPathStatus.PathComplete) return false;

        destination = hit.position;
        path = resolvedPath;
        return true;
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
}
