using System.Collections.Generic;
using UnityEngine;
using WarSimulation.Combat.Map;

/// <summary>
/// 進攻ルート候補のキャッシュ。BuildCandidates は重いのでマップ＋NavMesh 準備後に一度だけ作る。
/// AuthoredMap にベイク済みがあれば hydrate、無ければランタイム列挙。
/// </summary>
public static class CombatAssaultRouteCache
{
    private const float StoneSampleRadius = 8f;

    private static MapData _cachedMap;
    private static Transform _cachedOrigin;
    private static bool _buildCompleted;
    private static bool _hasRouteOrientation;
    private static bool _routesMatchReversedPositions;
    private static MapData _rebuildFallbackLoggedForMap;
    private static readonly List<CombatAiAssaultRoute> AllyRoutes = new List<CombatAiAssaultRoute>();
    private static readonly List<CombatAiAssaultRoute> EnemyRoutes = new List<CombatAiAssaultRoute>();

    public static void Invalidate()
    {
        _cachedMap = null;
        _cachedOrigin = null;
        _buildCompleted = false;
        _hasRouteOrientation = false;
        _routesMatchReversedPositions = false;
        AllyRoutes.Clear();
        EnemyRoutes.Clear();
    }

    public static IReadOnlyList<CombatAiAssaultRoute> GetRoutes(
        CombatTeam team,
        bool stonePositionReversed)
    {
        bool useOppositeTeamRoutes = _hasRouteOrientation &&
            _routesMatchReversedPositions != stonePositionReversed;
        bool useEnemyRoutes = team == CombatTeam.Enemy;
        if (useOppositeTeamRoutes) useEnemyRoutes = !useEnemyRoutes;
        return useEnemyRoutes ? EnemyRoutes : AllyRoutes;
    }

    public static void EnsureBuilt(CombatMapSystem mapSystem)
    {
        if (mapSystem == null) return;

        MapData map = mapSystem.CurrentMap;
        Transform origin = mapSystem.MapOrigin;
        if (map == null) return;
        if (_buildCompleted &&
            ReferenceEquals(_cachedMap, map) &&
            ReferenceEquals(_cachedOrigin, origin))
        {
            return;
        }

        if (TryHydrateFromAuthored(mapSystem.AuthoredMap, map, origin))
        {
            return;
        }

        // Procedural / test maps often have no AuthoredMap; only warn when an authored
        // map is present but baked routes could not be hydrated.
        AuthoredMapDefinition authored = mapSystem.AuthoredMap;
        if (authored != null && !ReferenceEquals(_rebuildFallbackLoggedForMap, map))
        {
            _rebuildFallbackLoggedForMap = map;
            Debug.LogWarning(
                $"[{nameof(CombatAssaultRouteCache)}] AssaultRoutes: Rebuild (runtime fallback). " +
                $"hasBakedData={authored.HasBakedAssaultRoutesData} " +
                $"storedFp={authored.AssaultRouteBakeFingerprint} " +
                $"currentFp={authored.ComputeBakeFingerprint()}");
        }

        Rebuild(map, origin, mapSystem.IsStonePositionReversed);
    }

    public static bool TryHydrateFromAuthored(
        AuthoredMapDefinition authored,
        MapData map,
        Transform mapOrigin)
    {
        if (authored == null || map == null || !authored.HasValidBakedAssaultRoutes)
        {
            return false;
        }

        Invalidate();
        _cachedMap = map;
        _cachedOrigin = mapOrigin;
        HydrateTeam(authored.BakedAllyAssaultRoutes, mapOrigin, AllyRoutes);
        HydrateTeam(authored.BakedEnemyAssaultRoutes, mapOrigin, EnemyRoutes);
        _hasRouteOrientation = true;
        _routesMatchReversedPositions = false;
        _buildCompleted = true;
        return true;
    }

    public static void Rebuild(
        MapData map,
        Transform mapOrigin,
        bool stonePositionReversed = false)
    {
        Invalidate();
        if (map == null) return;

        _cachedMap = map;
        _cachedOrigin = mapOrigin;
        int areaMask = CombatStoneAssaultRoutes.CreateAreaMask(allowRiverCrossing: false);

        if (!TryFindMainStoneWorld(map, mapOrigin, FeatureType.OwnMainStone, out Vector3 ownStone) ||
            !TryFindMainStoneWorld(map, mapOrigin, FeatureType.EnemyMainStone, out Vector3 enemyStone))
        {
            // 魔石が無いマップは再試行不要
            _hasRouteOrientation = true;
            _routesMatchReversedPositions = stonePositionReversed;
            _buildCompleted = true;
            return;
        }

        if (!CombatStoneAssaultRoutes.TrySamplePosition(ownStone, StoneSampleRadius, areaMask, out Vector3 allyStart) ||
            !CombatStoneAssaultRoutes.TrySamplePosition(enemyStone, StoneSampleRadius, areaMask, out Vector3 allyGoal) ||
            !CombatStoneAssaultRoutes.TrySamplePosition(enemyStone, StoneSampleRadius, areaMask, out Vector3 enemyStart) ||
            !CombatStoneAssaultRoutes.TrySamplePosition(ownStone, StoneSampleRadius, areaMask, out Vector3 enemyGoal))
        {
            // NavMesh 未準備。次の Collect で再試行する
            return;
        }

        BuildTeamRoutes(map, mapOrigin, allyStart, allyGoal, areaMask, AllyRoutes);
        BuildTeamRoutes(map, mapOrigin, enemyStart, enemyGoal, areaMask, EnemyRoutes);
        _hasRouteOrientation = true;
        _routesMatchReversedPositions = stonePositionReversed;
        _buildCompleted = true;
    }

    /// <summary>Editor ベイク用。ワールド座標の候補をマップローカル POD に変換する。</summary>
    public static List<AuthoredBakedAssaultRoute> BuildBakedRoutesForTeam(
        MapData map,
        Transform mapOrigin,
        Vector3 startWorld,
        Vector3 goalWorld,
        int areaMask)
    {
        var baked = new List<AuthoredBakedAssaultRoute>();
        List<CombatStoneAssaultRoutes.Candidate> candidates = CombatStoneAssaultRoutes.BuildCandidates(
            map,
            mapOrigin,
            startWorld,
            goalWorld,
            areaMask);
        for (int i = 0; i < candidates.Count; i++)
        {
            CombatStoneAssaultRoutes.Candidate candidate = candidates[i];
            if (candidate == null) continue;
            Vector3 enterLocal = ToLocal(mapOrigin, candidate.EnterWorld);
            Vector3 exitLocal = ToLocal(mapOrigin, candidate.ExitWorld);
            baked.Add(new AuthoredBakedAssaultRoute(
                candidate.BridgeFeatureIndex,
                candidate.HasBridgeWaypoints,
                enterLocal,
                exitLocal));
        }

        return baked;
    }

    public static bool TryFindMainStoneWorld(
        MapData map,
        Transform mapOrigin,
        FeatureType type,
        out Vector3 world)
    {
        world = default;
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

    private static void HydrateTeam(
        IReadOnlyList<AuthoredBakedAssaultRoute> baked,
        Transform mapOrigin,
        List<CombatAiAssaultRoute> destination)
    {
        destination.Clear();
        if (baked == null) return;
        for (int i = 0; i < baked.Count; i++)
        {
            AuthoredBakedAssaultRoute route = baked[i];
            destination.Add(new CombatAiAssaultRoute(
                route.BridgeFeatureIndex,
                route.HasBridgeWaypoints,
                ToWorld(mapOrigin, route.EnterLocal),
                ToWorld(mapOrigin, route.ExitLocal)));
        }
    }

    private static void BuildTeamRoutes(
        MapData map,
        Transform mapOrigin,
        Vector3 start,
        Vector3 goal,
        int areaMask,
        List<CombatAiAssaultRoute> destination)
    {
        destination.Clear();
        List<CombatStoneAssaultRoutes.Candidate> candidates = CombatStoneAssaultRoutes.BuildCandidates(
            map,
            mapOrigin,
            start,
            goal,
            areaMask);
        for (int i = 0; i < candidates.Count; i++)
        {
            CombatStoneAssaultRoutes.Candidate candidate = candidates[i];
            if (candidate == null) continue;
            destination.Add(new CombatAiAssaultRoute(
                candidate.BridgeFeatureIndex,
                candidate.HasBridgeWaypoints,
                candidate.EnterWorld,
                candidate.ExitWorld));
        }
    }

    private static Vector3 ToLocal(Transform mapOrigin, Vector3 world)
    {
        return mapOrigin != null ? mapOrigin.InverseTransformPoint(world) : world;
    }

    private static Vector3 ToWorld(Transform mapOrigin, Vector3 local)
    {
        return mapOrigin != null ? mapOrigin.TransformPoint(local) : local;
    }
}

public readonly struct CombatAiAssaultRoute
{
    public int BridgeFeatureIndex { get; }
    public bool HasBridgeWaypoints { get; }
    public Vector3 EnterWorld { get; }
    public Vector3 ExitWorld { get; }

    public CombatAiAssaultRoute(
        int bridgeFeatureIndex,
        bool hasBridgeWaypoints,
        Vector3 enterWorld,
        Vector3 exitWorld)
    {
        BridgeFeatureIndex = bridgeFeatureIndex;
        HasBridgeWaypoints = hasBridgeWaypoints;
        EnterWorld = enterWorld;
        ExitWorld = exitWorld;
    }
}
