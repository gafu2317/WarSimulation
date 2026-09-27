using UnityEngine;
using UnityEngine.AI;
using WarSimulation.Combat.Map;

[DisallowMultipleComponent]
public sealed class CombatNavigationSystem : MonoBehaviour
{
    private const string RiverAreaName = "River";

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

    internal bool TryFindRiverCrossingDestination(
        NavMeshAgent agent,
        Vector3 requestedWorldPosition,
        out Vector3 destination,
        out NavMeshPath path)
    {
        destination = default;
        path = null;
        if (agent == null || !agent.isOnNavMesh) return false;

        int riverArea = NavMesh.GetAreaFromName(RiverAreaName);
        if (riverArea < 0) return false;

        CombatMapSystem mapSystem = ResolveMapSystem();
        MapData map = mapSystem != null ? mapSystem.CurrentMap : null;
        if (map == null || map.Rivers == null || map.Rivers.Count == 0) return false;

        Vector3 start = agent.transform.position;
        float cellSize = map.Height.CellSize;
        float sampleRadius = Mathf.Max(_navMeshSampleRadius, cellSize);
        float bestDistance = float.PositiveInfinity;
        float bestRiverWidth = 0f;
        Vector3 bestRiverPoint = default;
        int riverMask = 1 << riverArea;

        for (int riverIndex = 0; riverIndex < map.Rivers.Count; riverIndex++)
        {
            RiverPath river = map.Rivers[riverIndex];
            if (river.Cells == null || river.Cells.Count == 0) continue;

            for (int cellIndex = 0; cellIndex < river.Cells.Count; cellIndex++)
            {
                Vector2Int cell = river.Cells[cellIndex];
                Vector3 local = new Vector3(
                    (cell.x + 0.5f) * cellSize,
                    0f,
                    (cell.y + 0.5f) * cellSize);
                Vector3 world = mapSystem.MapOrigin != null
                    ? mapSystem.MapOrigin.TransformPoint(local)
                    : local;
                if (!NavMesh.SamplePosition(world, out NavMeshHit hit, sampleRadius, riverMask)) continue;

                float distance = DistanceToSegmentSquared(hit.position, start, requestedWorldPosition);
                if (distance >= bestDistance) continue;

                bestDistance = distance;
                bestRiverWidth = river.WidthMeters;
                bestRiverPoint = hit.position;
            }
        }

        if (float.IsPositiveInfinity(bestDistance)) return false;

        Vector3 towardDestination = requestedWorldPosition - bestRiverPoint;
        towardDestination.y = 0f;
        if (towardDestination.sqrMagnitude <= 0.01f) return false;
        towardDestination.Normalize();

        Vector3 beyondRiver = bestRiverPoint + towardDestination *
            (bestRiverWidth + agent.radius);
        if (!NavMesh.SamplePosition(
                beyondRiver,
                out NavMeshHit beyondHit,
                sampleRadius,
                NavMesh.AllAreas))
        {
            return false;
        }

        if (mapSystem != null && !mapSystem.CanStandAt(beyondHit.position)) return false;

        var crossingPath = new NavMeshPath();
        if (!agent.CalculatePath(beyondHit.position, crossingPath) ||
            crossingPath.status != NavMeshPathStatus.PathComplete ||
            !PathTouchesArea(agent, crossingPath, riverArea))
        {
            return false;
        }

        destination = beyondHit.position;
        path = crossingPath;
        return true;
    }

    internal bool PathTouchesRiver(NavMeshAgent agent, NavMeshPath path)
    {
        int riverArea = NavMesh.GetAreaFromName(RiverAreaName);
        return riverArea >= 0 && PathTouchesArea(agent, path, riverArea);
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

    private static bool PathTouchesArea(NavMeshAgent agent, NavMeshPath path, int area)
    {
        if (agent == null || path == null || path.corners == null || path.corners.Length < 2)
        {
            return false;
        }

        int areaMask = 1 << area;
        float sampleRadius = Mathf.Max(agent.radius, 0.1f);
        Vector3[] corners = path.corners;
        for (int i = 1; i < corners.Length; i++)
        {
            Vector3 from = corners[i - 1];
            Vector3 to = corners[i];
            int samples = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(from, to)));
            for (int sample = 0; sample <= samples; sample++)
            {
                Vector3 point = Vector3.Lerp(from, to, sample / (float)samples);
                if (NavMesh.SamplePosition(point, out _, sampleRadius, areaMask)) return true;
            }
        }

        return false;
    }

    private static float DistanceToSegmentSquared(Vector3 point, Vector3 start, Vector3 end)
    {
        point.y = 0f;
        start.y = 0f;
        end.y = 0f;
        Vector3 segment = end - start;
        if (segment.sqrMagnitude <= 0.01f) return (point - start).sqrMagnitude;

        float progress = Mathf.Clamp01(Vector3.Dot(point - start, segment) / segment.sqrMagnitude);
        return (point - Vector3.Lerp(start, end, progress)).sqrMagnitude;
    }
}
