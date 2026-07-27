using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using WarSimulation.Combat.Map;

/// <summary>
/// 魔石進攻ルート候補。
/// 直進が通るならそれだけ。通らないときだけ各橋経由を列挙する。
/// </summary>
public static class CombatStoneAssaultRoutes
{
    public sealed class Candidate
    {
        public string Label;
        public int BridgeFeatureIndex;
        public List<Vector3> Corners;
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

        if (TryBuildPath(startWorld, goalWorld, areaMask, out List<Vector3> direct))
        {
            candidates.Add(new Candidate
            {
                Label = "直進",
                BridgeFeatureIndex = -1,
                Corners = direct,
            });
            return candidates;
        }

        for (int featureIndex = 0; featureIndex < map.Features.Count; featureIndex++)
        {
            PlacedFeature bridge = map.Features[featureIndex];
            if (bridge.Type != FeatureType.Bridge) continue;
            if (!TryBuildBridgeRoute(
                    mapOrigin,
                    startWorld,
                    goalWorld,
                    bridge,
                    featureIndex,
                    areaMask,
                    settings,
                    out Candidate candidate))
            {
                continue;
            }

            candidates.Add(candidate);
        }

        candidates.Sort((a, b) => GetLength(a.Corners).CompareTo(GetLength(b.Corners)));
        return candidates;
    }

    public static List<Candidate> TakeUpTo(List<Candidate> candidates, int maximumCount)
    {
        var selected = new List<Candidate>();
        if (candidates == null || maximumCount <= 0) return selected;
        for (int i = 0; i < candidates.Count && selected.Count < maximumCount; i++)
        {
            selected.Add(candidates[i]);
        }

        return selected;
    }

    public static int CreateAreaMask(bool allowRiverCrossing)
    {
        if (allowRiverCrossing) return NavMesh.AllAreas;

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
        Transform mapOrigin,
        Vector3 startWorld,
        Vector3 goalWorld,
        PlacedFeature bridge,
        int featureIndex,
        int areaMask,
        BuildSettings settings,
        out Candidate candidate)
    {
        candidate = null;
        if (!TryGetBridgeEndpoints(bridge, mapOrigin, settings, areaMask, out Vector3 a, out Vector3 b))
        {
            return false;
        }

        bool ab = TryConnectVia(startWorld, a, b, goalWorld, areaMask, out List<Vector3> abCorners);
        bool ba = TryConnectVia(startWorld, b, a, goalWorld, areaMask, out List<Vector3> baCorners);
        if (!ab && !ba) return false;

        candidate = new Candidate
        {
            Label = "橋" + featureIndex,
            BridgeFeatureIndex = featureIndex,
            Corners = !ba || (ab && GetLength(abCorners) <= GetLength(baCorners)) ? abCorners : baCorners,
        };
        return true;
    }

    private static bool TryGetBridgeEndpoints(
        PlacedFeature bridge,
        Transform mapOrigin,
        BuildSettings settings,
        int areaMask,
        out Vector3 firstWorld,
        out Vector3 secondWorld)
    {
        firstWorld = default;
        secondWorld = default;
        float halfLength = bridge.Scale.z * 0.5f;
        if (halfLength <= 0f) return false;

        float inset = Mathf.Min(0.75f + settings.BridgeEndpointMargin, halfLength * 0.5f);
        Vector3 along = bridge.Rotation * Vector3.forward;
        Vector3 deckLift = Vector3.up * Mathf.Max(0.25f, bridge.Scale.y * 0.5f);
        Vector3 a = ToWorld(mapOrigin, bridge.WorldPosition - along * (halfLength - inset)) + deckLift;
        Vector3 b = ToWorld(mapOrigin, bridge.WorldPosition + along * (halfLength - inset)) + deckLift;
        float radius = Mathf.Max(settings.WaypointSampleRadius, bridge.Scale.x);
        return TrySamplePosition(a, radius, areaMask, out firstWorld) &&
            TrySamplePosition(b, radius, areaMask, out secondWorld);
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

    private static Vector3 ToWorld(Transform mapOrigin, Vector3 mapLocal)
    {
        return mapOrigin != null ? mapOrigin.TransformPoint(mapLocal) : mapLocal;
    }

    private static float GetLength(IReadOnlyList<Vector3> corners)
    {
        float length = 0f;
        for (int i = 1; i < corners.Count; i++)
        {
            Vector3 a = corners[i - 1];
            Vector3 b = corners[i];
            a.y = 0f;
            b.y = 0f;
            length += Vector3.Distance(a, b);
        }

        return length;
    }
}
