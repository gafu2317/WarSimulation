using System.Collections.Generic;
using UnityEngine;
using WarSimulation.Combat.Map;

public static class CombatAssaultRouteCache
{
    private static MapData _cachedMap;
    private static Transform _cachedOrigin;
    private static bool _buildCompleted;
    private static readonly List<CombatAiAssaultRoute> ForwardRoutes = new();
    private static readonly List<CombatAiAssaultRoute> ReverseRoutes = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForPlay()
    {
        Invalidate();
    }

    public static void Invalidate()
    {
        _cachedMap = null;
        _cachedOrigin = null;
        _buildCompleted = false;
        ForwardRoutes.Clear();
        ReverseRoutes.Clear();
    }

    public static IReadOnlyList<CombatAiAssaultRoute> GetRoutes(
        CombatTeam team,
        bool stonePositionReversed)
    {
        bool reverse = team == CombatTeam.Enemy;
        if (stonePositionReversed) reverse = !reverse;
        return reverse ? ReverseRoutes : ForwardRoutes;
    }

    public static void EnsureBuilt(CombatMapSystem mapSystem)
    {
        if (mapSystem == null || mapSystem.CurrentMap == null) return;
        MapData map = mapSystem.CurrentMap;
        Transform origin = mapSystem.MapOrigin;
        if (_buildCompleted && ReferenceEquals(_cachedMap, map) && ReferenceEquals(_cachedOrigin, origin))
            return;
        Hydrate(map, origin);
    }

    public static bool TryHydrate(MapData map, Transform mapOrigin)
    {
        if (map == null || map.AssaultRoutes.Count == 0) return false;
        Hydrate(map, mapOrigin);
        return true;
    }

    public static bool TryFindMainStoneWorld(
        MapData map,
        Transform mapOrigin,
        FeatureType type,
        out Vector3 world)
    {
        world = default;
        if (map == null) return false;
        for (int i = 0; i < map.Features.Count; i++)
        {
            PlacedFeature feature = map.Features[i];
            if (feature.Type != type) continue;
            world = mapOrigin != null
                ? mapOrigin.TransformPoint(feature.WorldPosition)
                : feature.WorldPosition;
            return true;
        }

        return false;
    }

    private static void Hydrate(MapData map, Transform origin)
    {
        Invalidate();
        _cachedMap = map;
        _cachedOrigin = origin;
        for (int i = 0; i < map.AssaultRoutes.Count; i++)
        {
            AssaultRoute route = map.AssaultRoutes[i];
            var forward = new Vector3[route.Corners.Count];
            var reverse = new Vector3[route.Corners.Count];
            for (int c = 0; c < route.Corners.Count; c++)
            {
                forward[c] = ToWorld(origin, route.Corners[c]);
                reverse[route.Corners.Count - 1 - c] = forward[c];
            }

            ForwardRoutes.Add(new CombatAiAssaultRoute(route.RouteId, route.DisplayName, forward));
            ReverseRoutes.Add(new CombatAiAssaultRoute(route.RouteId, route.DisplayName, reverse));
        }

        _buildCompleted = true;
    }

    private static Vector3 ToWorld(Transform origin, Vector3 local) =>
        origin != null ? origin.TransformPoint(local) : local;
}

public readonly struct CombatAiAssaultRoute
{
    public string RouteId { get; }
    public string DisplayName { get; }
    public IReadOnlyList<Vector3> Corners { get; }

    public CombatAiAssaultRoute(string routeId, string displayName, IReadOnlyList<Vector3> corners)
    {
        RouteId = routeId ?? string.Empty;
        DisplayName = displayName ?? string.Empty;
        Corners = corners ?? System.Array.Empty<Vector3>();
    }
}
