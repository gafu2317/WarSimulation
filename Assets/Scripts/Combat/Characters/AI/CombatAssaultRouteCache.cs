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
        if (map.AssaultRoutes.Count > 0)
            Hydrate(map, origin);
        else if (mapSystem.AuthoredMap != null && mapSystem.AuthoredMap.HasValidBakedAssaultRoutes)
            HydrateLegacy(mapSystem.AuthoredMap, map, origin);
        else
            Hydrate(map, origin);
    }

    public static bool TryHydrateFromAuthored(
        AuthoredMapDefinition authored,
        MapData map,
        Transform mapOrigin)
    {
        if (authored == null || map == null)
            return false;
        if (map.AssaultRoutes.Count > 0) Hydrate(map, mapOrigin);
        else if (authored.HasValidBakedAssaultRoutes) HydrateLegacy(authored, map, mapOrigin);
        else return false;
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

    private static void HydrateLegacy(
        AuthoredMapDefinition authored,
        MapData map,
        Transform origin)
    {
        Invalidate();
        _cachedMap = map;
        _cachedOrigin = origin;
        TryFindMainStoneWorld(map, origin, FeatureType.OwnMainStone, out Vector3 ownStone);
        TryFindMainStoneWorld(map, origin, FeatureType.EnemyMainStone, out Vector3 enemyStone);
        IReadOnlyList<AuthoredBakedAssaultRoute> legacyRoutes = authored.BakedAllyAssaultRoutes;
        for (int i = 0; i < legacyRoutes.Count; i++)
        {
            AuthoredBakedAssaultRoute legacy = legacyRoutes[i];
            string id = legacy.HasBridgeWaypoints
                ? $"auto:bridge:{legacy.BridgeFeatureIndex}"
                : "auto:direct";
            var forward = legacy.HasBridgeWaypoints
                ? new[]
                {
                    ownStone,
                    ToWorld(origin, legacy.EnterLocal),
                    ToWorld(origin, legacy.ExitLocal),
                    enemyStone,
                }
                : new[] { ownStone, enemyStone };
            var reverse = new Vector3[forward.Length];
            for (int c = 0; c < forward.Length; c++) reverse[forward.Length - 1 - c] = forward[c];
            string name = legacy.HasBridgeWaypoints ? $"橋ルート {i + 1}" : "直進";
            ForwardRoutes.Add(new CombatAiAssaultRoute(id, name, forward));
            ReverseRoutes.Add(new CombatAiAssaultRoute(id, name, reverse));
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
