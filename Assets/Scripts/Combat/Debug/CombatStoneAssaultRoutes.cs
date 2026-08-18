using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using WarSimulation.Combat.Map;

/// <summary>
/// 魔石進攻ルート候補。
/// 直進が橋を使わないなら直進のみ。橋を使う（または直進不可）なら各橋経由を列挙する。
/// </summary>
public static class CombatStoneAssaultRoutes
{
    public sealed class Candidate
    {
        public int BridgeFeatureIndex;
        public List<Vector3> Corners;
        public bool HasBridgeWaypoints;
        public Vector3 EnterWorld;
        public Vector3 ExitWorld;
    }

    public sealed class BuildSettings
    {
        public float WaypointSampleRadius = 8f;
        public float BridgeEndpointMargin = 1f;
    }

    public static List<Candidate> BuildCandidates(
        MapData map,
        Transform mapOrigin,
        Vector3 startWorld,
        Vector3 goalWorld,
        int areaMask,
        BuildSettings settings = null)
    {
        settings ??= new BuildSettings();
        var candidates = new List<Candidate>();
        if (map == null) return candidates;

        bool hasDirect = TryBuildPath(startWorld, goalWorld, areaMask, out List<Vector3> direct);
        // 川越えの最短は「直進1本」に見えるが、実際は橋を1本だけ使う。
        // その場合は直進で打ち切らず、各橋ルートを列挙する。
        if (hasDirect && !PathTouchesAnyBridge(direct, map, mapOrigin))
        {
            candidates.Add(new Candidate
            {
                BridgeFeatureIndex = -1,
                Corners = direct,
            });
            return candidates;
        }

        for (int featureIndex = 0; featureIndex < map.Features.Count; featureIndex++)
        {
            PlacedFeature bridge = map.Features[featureIndex];
            if (bridge.Type != FeatureType.Bridge || bridge.Scale.z <= 0f) continue;

            GetBridgeEndpointIdeals(bridge, mapOrigin, settings, out Vector3 idealA, out Vector3 idealB);
            float sampleRadius = GetEndpointSampleRadius(bridge, settings);
            Vector3 a = idealA;
            Vector3 b = idealB;
            bool sampled = TrySamplePosition(idealA, sampleRadius, areaMask, out a) &&
                TrySamplePosition(idealB, sampleRadius, areaMask, out b);
            if (!sampled) continue;

            if (!TryBuildBridgeRoute(
                    startWorld,
                    goalWorld,
                    a,
                    b,
                    featureIndex,
                    areaMask,
                    out Candidate candidate))
            {
                continue;
            }

            candidates.Add(candidate);
        }

        if (candidates.Count > 0)
        {
            candidates.Sort((a, b) => GetLength(a.Corners).CompareTo(GetLength(b.Corners)));
            return candidates;
        }

        if (hasDirect)
        {
            candidates.Add(new Candidate
            {
                BridgeFeatureIndex = -1,
                Corners = direct,
            });
        }

        return candidates;
    }

    public static int CreateAreaMask()
    {
        int areaMask = NavMesh.AllAreas;
        int riverArea = NavMesh.GetAreaFromName(CombatNavMeshAreaGridBuilder.RiverAreaName);
        int lakeArea = NavMesh.GetAreaFromName(CombatNavMeshAreaGridBuilder.LakeAreaName);
        if (riverArea >= 0) areaMask &= ~(1 << riverArea);
        if (lakeArea >= 0) areaMask &= ~(1 << lakeArea);
        return areaMask;
    }

    public static bool TrySamplePosition(Vector3 position, float radius, int areaMask, out Vector3 sampledPosition)
    {
        if (NavMesh.SamplePosition(position, out NavMeshHit hit, radius, areaMask))
        {
            sampledPosition = hit.position;
            return true;
        }

        sampledPosition = default;
        return false;
    }

    private static bool TryBuildBridgeRoute(
        Vector3 startWorld,
        Vector3 goalWorld,
        Vector3 a,
        Vector3 b,
        int featureIndex,
        int areaMask,
        out Candidate candidate)
    {
        candidate = null;

        // 全体長ではなく「魔石→入口」が短い向きを優先する。
        // 入口を対岸に取ると、橋を使わずマップ端迂回の start→enter になりやすい。
        bool ab = TryConnectVia(startWorld, a, b, goalWorld, areaMask, out List<Vector3> abCorners);
        bool ba = TryConnectVia(startWorld, b, a, goalWorld, areaMask, out List<Vector3> baCorners);
        if (!ab && !ba) return false;

        Vector3 enter;
        Vector3 exit;
        List<Vector3> corners;
        if (ab && ba)
        {
            bool useAb = PathLength(startWorld, a, areaMask) <= PathLength(startWorld, b, areaMask);
            corners = useAb ? abCorners : baCorners;
            enter = useAb ? a : b;
            exit = useAb ? b : a;
        }
        else if (ab)
        {
            corners = abCorners;
            enter = a;
            exit = b;
        }
        else
        {
            corners = baCorners;
            enter = b;
            exit = a;
        }

        candidate = new Candidate
        {
            BridgeFeatureIndex = featureIndex,
            Corners = corners,
            HasBridgeWaypoints = true,
            EnterWorld = enter,
            ExitWorld = exit,
        };
        return true;
    }

    private static void GetBridgeEndpointIdeals(
        PlacedFeature bridge,
        Transform mapOrigin,
        BuildSettings settings,
        out Vector3 firstWorld,
        out Vector3 secondWorld)
    {
        float halfLength = bridge.Scale.z * 0.5f;
        Vector3 along = bridge.Rotation * Vector3.forward;
        // 橋の外側の岸（陸）側。デッキ中央ではなく Terrain に載りやすい点。
        float landOut = halfLength + Mathf.Max(0.5f, settings.BridgeEndpointMargin);
        Vector3 aLocal = bridge.WorldPosition - along * landOut;
        Vector3 bLocal = bridge.WorldPosition + along * landOut;
        aLocal.y = bridge.WorldPosition.y;
        bLocal.y = bridge.WorldPosition.y;
        firstWorld = ToWorld(mapOrigin, aLocal);
        secondWorld = ToWorld(mapOrigin, bLocal);
    }

    private static float GetEndpointSampleRadius(PlacedFeature bridge, BuildSettings settings)
    {
        return Mathf.Max(2f, Mathf.Min(settings.WaypointSampleRadius, Mathf.Max(bridge.Scale.x, 3f)));
    }

    private static bool TryConnectVia(
        Vector3 start,
        Vector3 enter,
        Vector3 exit,
        Vector3 goal,
        int areaMask,
        out List<Vector3> corners)
    {
        corners = new List<Vector3>();
        return TryAppendPath(corners, start, enter, areaMask) &&
            TryAppendPath(corners, enter, exit, areaMask) &&
            TryAppendPath(corners, exit, goal, areaMask);
    }

    private static bool TryBuildPath(Vector3 start, Vector3 end, int areaMask, out List<Vector3> corners)
    {
        corners = new List<Vector3>();
        return TryAppendPath(corners, start, end, areaMask);
    }

    private static bool TryAppendPath(List<Vector3> destination, Vector3 start, Vector3 end, int areaMask)
    {
        var path = new NavMeshPath();
        if (!NavMesh.CalculatePath(start, end, areaMask, path) ||
            path.status != NavMeshPathStatus.PathComplete ||
            path.corners.Length < 2)
        {
            return false;
        }

        for (int i = 0; i < path.corners.Length; i++)
        {
            Vector3 corner = path.corners[i];
            if (destination.Count > 0 && Vector3.SqrMagnitude(destination[^1] - corner) < 0.01f) continue;
            destination.Add(corner);
        }

        return true;
    }

    private static bool PathTouchesAnyBridge(
        IReadOnlyList<Vector3> corners,
        MapData map,
        Transform mapOrigin)
    {
        for (int i = 0; i < corners.Count; i++)
        {
            if (IsInsideAnyBridge(corners[i], map, mapOrigin)) return true;
        }

        for (int i = 1; i < corners.Count; i++)
        {
            Vector3 a = corners[i - 1];
            Vector3 b = corners[i];
            float length = HorizontalDistance(a, b);
            int samples = Mathf.Max(1, Mathf.CeilToInt(length / 2f));
            for (int sample = 1; sample < samples; sample++)
            {
                if (IsInsideAnyBridge(Vector3.Lerp(a, b, sample / (float)samples), map, mapOrigin))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsInsideAnyBridge(Vector3 world, MapData map, Transform mapOrigin)
    {
        for (int i = 0; i < map.Features.Count; i++)
        {
            PlacedFeature feature = map.Features[i];
            if (feature.Type != FeatureType.Bridge) continue;

            Vector3 local = Quaternion.Inverse(feature.Rotation) *
                (world - ToWorld(mapOrigin, feature.WorldPosition));
            if (Mathf.Abs(local.x) <= feature.Scale.x * 0.5f + 0.5f &&
                Mathf.Abs(local.z) <= feature.Scale.z * 0.5f + 0.5f)
            {
                return true;
            }
        }

        return false;
    }

    private static Vector3 ToWorld(Transform mapOrigin, Vector3 mapLocal)
    {
        return mapOrigin != null ? mapOrigin.TransformPoint(mapLocal) : mapLocal;
    }

    private static float PathLength(Vector3 start, Vector3 end, int areaMask)
    {
        var path = new NavMeshPath();
        if (!NavMesh.CalculatePath(start, end, areaMask, path) ||
            path.status != NavMeshPathStatus.PathComplete ||
            path.corners.Length < 2)
        {
            return float.MaxValue;
        }

        return GetLength(path.corners);
    }

    private static float GetLength(IReadOnlyList<Vector3> corners)
    {
        float length = 0f;
        for (int i = 1; i < corners.Count; i++) length += HorizontalDistance(corners[i - 1], corners[i]);
        return length;
    }

    private static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }
}
