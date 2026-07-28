using System.Collections.Generic;
using UnityEngine;
using WarSimulation.Combat.Map;

/// <summary>
/// 進攻ルート候補のキャッシュ。BuildCandidates は重いのでマップ＋NavMesh 準備後に一度だけ作る。
/// </summary>
public static class CombatAssaultRouteCache
{
    private const float StoneSampleRadius = 8f;

    private static MapData _cachedMap;
    private static Transform _cachedOrigin;
    private static bool _buildCompleted;
    private static readonly List<CombatAiAssaultRoute> AllyRoutes = new List<CombatAiAssaultRoute>();
    private static readonly List<CombatAiAssaultRoute> EnemyRoutes = new List<CombatAiAssaultRoute>();

    public static void Invalidate()
    {
        _cachedMap = null;
        _cachedOrigin = null;
        _buildCompleted = false;
        AllyRoutes.Clear();
        EnemyRoutes.Clear();
    }

    public static IReadOnlyList<CombatAiAssaultRoute> GetRoutes(CombatTeam team)
    {
        return team == CombatTeam.Enemy ? EnemyRoutes : AllyRoutes;
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

        Rebuild(map, origin);
    }

    public static void Rebuild(MapData map, Transform mapOrigin)
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
        _buildCompleted = true;
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

    private static bool TryFindMainStoneWorld(
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
