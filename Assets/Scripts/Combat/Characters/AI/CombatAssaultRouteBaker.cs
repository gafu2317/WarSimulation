using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using WarSimulation.Combat.Map;

public readonly struct CombatAssaultRouteValidationFailure
{
    public string RouteId { get; }
    public int SegmentIndex { get; }
    public int WaypointIndex { get; }
    public string Message { get; }

    public CombatAssaultRouteValidationFailure(
        string routeId,
        int segmentIndex,
        string message,
        int waypointIndex = -1)
    {
        RouteId = routeId;
        SegmentIndex = segmentIndex;
        WaypointIndex = waypointIndex;
        Message = message;
    }
}

public static class CombatAssaultRouteBaker
{
    private const float SampleRadius = 8f;

    public static bool CanPlaceWaypoint(Transform mapOrigin, Vector2 waypoint)
    {
        int areaMask = CombatStoneAssaultRoutes.CreateAreaMask();
        Vector3 local = new(waypoint.x, 0f, waypoint.y);
        Vector3 world = mapOrigin != null ? mapOrigin.TransformPoint(local) : local;
        return CombatStoneAssaultRoutes.TrySamplePosition(world, SampleRadius, areaMask, out _);
    }

    public static List<AuthoredAssaultRoute> ReplaceAutomaticRoutes(
        IReadOnlyList<AuthoredAssaultRoute> existing,
        IReadOnlyList<AuthoredAssaultRoute> generated)
    {
        var result = new List<AuthoredAssaultRoute>();
        if (existing != null)
        {
            for (int i = 0; i < existing.Count; i++)
            {
                AuthoredAssaultRoute route = existing[i];
                if (route != null && route.Source == AuthoredAssaultRouteSource.Manual)
                    result.Add(route);
            }
        }
        if (generated != null) result.AddRange(generated);
        return result;
    }

    public static bool TryBuildAutomaticRoutes(
        MapData map,
        Transform mapOrigin,
        IReadOnlyList<AuthoredBridgePlacement> authoredBridges,
        out List<AuthoredAssaultRoute> authored,
        out List<AssaultRoute> baked,
        out string error)
    {
        authored = new List<AuthoredAssaultRoute>();
        baked = new List<AssaultRoute>();
        error = null;
        int areaMask = CombatStoneAssaultRoutes.CreateAreaMask();
        if (!TryGetSampledStones(map, mapOrigin, areaMask, out Vector3 start, out Vector3 goal))
        {
            error = "進攻ルート用の主魔石をNavMesh上へ配置できません";
            return false;
        }

        List<CombatStoneAssaultRoutes.Candidate> candidates =
            CombatStoneAssaultRoutes.BuildCandidates(map, mapOrigin, start, goal, areaMask);
        if (candidates.Count == 0)
        {
            error = "通行可能な進攻ルート候補がありません";
            return false;
        }

        for (int i = 0; i < candidates.Count; i++)
        {
            CombatStoneAssaultRoutes.Candidate candidate = candidates[i];
            int bridgeIndex = FindAuthoredBridgeIndex(map, candidate.BridgeFeatureIndex, authoredBridges);
            string routeId = candidate.HasBridgeWaypoints
                ? $"auto:bridge:{bridgeIndex}"
                : "auto:direct";
            string displayName = candidate.HasBridgeWaypoints
                ? $"橋ルート {bridgeIndex + 1}"
                : "直進";
            var waypoints = new List<Vector2>();
            if (candidate.HasBridgeWaypoints)
            {
                Vector3 enter = ToLocal(mapOrigin, candidate.EnterWorld);
                Vector3 exit = ToLocal(mapOrigin, candidate.ExitWorld);
                waypoints.Add(new Vector2(enter.x, enter.z));
                waypoints.Add(new Vector2(exit.x, exit.z));
            }

            authored.Add(new AuthoredAssaultRoute(
                routeId,
                displayName,
                AuthoredAssaultRouteSource.Auto,
                waypoints));
            var corners = new List<Vector3>(candidate.Corners.Count);
            for (int c = 0; c < candidate.Corners.Count; c++)
                corners.Add(ToLocal(mapOrigin, candidate.Corners[c]));
            baked.Add(new AssaultRoute(routeId, displayName, corners));
        }

        return true;
    }

    public static bool TryValidateRoutes(
        MapData map,
        Transform mapOrigin,
        IReadOnlyList<AuthoredAssaultRoute> authored,
        out List<AssaultRoute> baked,
        out List<CombatAssaultRouteValidationFailure> failures)
    {
        baked = new List<AssaultRoute>();
        failures = new List<CombatAssaultRouteValidationFailure>();
        int areaMask = CombatStoneAssaultRoutes.CreateAreaMask();
        if (!TryGetSampledStones(map, mapOrigin, areaMask, out Vector3 start, out Vector3 goal))
        {
            failures.Add(new CombatAssaultRouteValidationFailure(
                string.Empty, -1, "進攻ルート用の主魔石をNavMesh上へ配置できません"));
            return false;
        }

        if (authored == null || authored.Count == 0) return false;
        var ids = new HashSet<string>();
        for (int i = 0; i < authored.Count; i++)
        {
            AuthoredAssaultRoute route = authored[i];
            if (route == null || string.IsNullOrWhiteSpace(route.RouteId) || !ids.Add(route.RouteId))
            {
                failures.Add(new CombatAssaultRouteValidationFailure(
                    route?.RouteId ?? string.Empty, -1, "進攻ルートIDが空または重複しています"));
                continue;
            }

            if (TryValidateRoute(mapOrigin, route, start, goal, areaMask, out AssaultRoute result,
                    out int segmentIndex, out int waypointIndex, out string error))
            {
                baked.Add(result);
            }
            else
            {
                failures.Add(new CombatAssaultRouteValidationFailure(
                    route.RouteId,
                    segmentIndex,
                    error,
                    waypointIndex));
            }
        }

        return failures.Count == 0 && baked.Count > 0;
    }

    private static bool TryValidateRoute(
        Transform mapOrigin,
        AuthoredAssaultRoute route,
        Vector3 start,
        Vector3 goal,
        int areaMask,
        out AssaultRoute baked,
        out int failedSegment,
        out int failedWaypoint,
        out string error)
    {
        baked = null;
        failedSegment = -1;
        failedWaypoint = -1;
        error = null;
        var points = new List<Vector3> { start };
        if (route.Waypoints != null)
        {
            for (int i = 0; i < route.Waypoints.Count; i++)
            {
                Vector2 point = route.Waypoints[i];
                Vector3 world = ToWorld(mapOrigin, new Vector3(point.x, 0f, point.y));
                if (!CombatStoneAssaultRoutes.TrySamplePosition(world, SampleRadius, areaMask, out Vector3 sampled))
                {
                    failedSegment = i;
                    failedWaypoint = i;
                    error = $"経由点 {i + 1} をNavMesh上へ配置できません";
                    return false;
                }

                points.Add(sampled);
            }
        }

        points.Add(goal);
        var corners = new List<Vector3>();
        for (int i = 0; i < points.Count - 1; i++)
        {
            var path = new NavMeshPath();
            if (!NavMesh.CalculatePath(points[i], points[i + 1], areaMask, path) ||
                path.status != NavMeshPathStatus.PathComplete || path.corners.Length < 2)
            {
                failedSegment = i;
                error = $"区間 {i + 1} が通行できません";
                return false;
            }

            for (int c = 0; c < path.corners.Length; c++)
            {
                Vector3 local = ToLocal(mapOrigin, path.corners[c]);
                if (corners.Count == 0 || (corners[corners.Count - 1] - local).sqrMagnitude > 0.0001f)
                    corners.Add(local);
            }
        }

        baked = new AssaultRoute(route.RouteId, route.DisplayName, corners);
        return true;
    }

    private static bool TryGetSampledStones(
        MapData map,
        Transform mapOrigin,
        int areaMask,
        out Vector3 start,
        out Vector3 goal)
    {
        start = default;
        goal = default;
        return map != null &&
            CombatAssaultRouteCache.TryFindMainStoneWorld(
                map, mapOrigin, FeatureType.OwnMainStone, out Vector3 ownStone) &&
            CombatAssaultRouteCache.TryFindMainStoneWorld(
                map, mapOrigin, FeatureType.EnemyMainStone, out Vector3 enemyStone) &&
            CombatStoneAssaultRoutes.TrySamplePosition(ownStone, SampleRadius, areaMask, out start) &&
            CombatStoneAssaultRoutes.TrySamplePosition(enemyStone, SampleRadius, areaMask, out goal);
    }

    private static int FindAuthoredBridgeIndex(
        MapData map,
        int featureIndex,
        IReadOnlyList<AuthoredBridgePlacement> authoredBridges)
    {
        if (featureIndex < 0) return -1;
        int bridgeOrdinal = -1;
        for (int i = 0; i <= featureIndex && i < map.Features.Count; i++)
        {
            if (map.Features[i].Type == FeatureType.Bridge) bridgeOrdinal++;
        }

        if (authoredBridges == null) return bridgeOrdinal;
        int current = -1;
        for (int i = 0; i < authoredBridges.Count; i++)
        {
            if (authoredBridges[i] == null) continue;
            current++;
            if (current == bridgeOrdinal) return i;
        }
        return bridgeOrdinal;
    }

    private static Vector3 ToLocal(Transform origin, Vector3 world) =>
        origin != null ? origin.InverseTransformPoint(world) : world;

    private static Vector3 ToWorld(Transform origin, Vector3 local) =>
        origin != null ? origin.TransformPoint(local) : local;
}
