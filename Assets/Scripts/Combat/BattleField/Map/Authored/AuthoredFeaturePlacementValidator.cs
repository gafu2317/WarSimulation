using System.Collections.Generic;
using UnityEngine;

namespace WarSimulation.Combat.Map
{
    public static class AuthoredFeaturePlacementValidator
    {
        public static bool TryValidate(
            MapData map,
            FeatureType type,
            Vector2 current,
            Vector2 candidate,
            IReadOnlyList<Vector2> excluded,
            out string reason)
        {
            reason = null;
            FeaturePlacementRadii radii = map.PlacementRadii;
            float bodyRadius = type == FeatureType.Rock ? radii.Rock : radii.Tree;
            float fullRadius = type == FeatureType.Rock
                ? radii.Rock
                : Mathf.Max(radii.Tree, radii.TreeCanopy);
            float mapMargin = type == FeatureType.Rock ? bodyRadius : 0f;
            if (!TreePlacementUtility.IsInsidePlayableBounds(map, candidate, mapMargin))
                return Fail("マップ外には配置できません", out reason);

            Vector3 world = new(candidate.x, 0f, candidate.y);
            if (map.GroundStates.SampleAt(world) == GroundState.Water)
                return Fail("水上には配置できません", out reason);
            if (map.Height.SampleCliffFace(world))
                return Fail("崖面には配置できません", out reason);
            float riverMargin = type == FeatureType.Rock ? bodyRadius + radii.Clearance : 0f;
            if (RiverCorridorUtility.Contains(map, candidate, riverMargin))
                return Fail("川には配置できません", out reason);
            for (int i = 0; type == FeatureType.Rock && i < map.Lakes.Count; i++)
            {
                LakeRegion lake = map.Lakes[i];
                Vector2 delta = candidate - lake.Center;
                float reach = lake.EffectiveRadius(delta.x, delta.y) + fullRadius + radii.Clearance;
                if (delta.sqrMagnitude < reach * reach)
                    return Fail("湖には配置できません", out reason);
            }

            float bridgeMargin = map.BridgeFeatureExclusionMargin + fullRadius + radii.Clearance;
            if (BridgePlacementUtility.IsNearAnyBridge(map, candidate, bridgeMargin))
                return Fail("橋の近くには配置できません", out reason);

            bool skippedCurrent = false;
            for (int i = 0; i < map.Features.Count; i++)
            {
                PlacedFeature other = map.Features[i];
                if (other.Type == FeatureType.Bridge) continue;
                var center = new Vector2(other.WorldPosition.x, other.WorldPosition.z);
                if (other.Type == type && IsExcluded(center, current, excluded, ref skippedCurrent)) continue;

                float own = radii.Radius(type, other.Type);
                float theirs = radii.Radius(other.Type, type);
                float distance = own > 0f && theirs > 0f ? own + theirs + radii.Clearance : 0f;
                if (distance > 0f && (candidate - center).sqrMagnitude < distance * distance)
                    return Fail("ほかの配置物と重なります", out reason);
            }

            return true;
        }

        private static bool IsExcluded(
            Vector2 center,
            Vector2 current,
            IReadOnlyList<Vector2> excluded,
            ref bool skippedCurrent)
        {
            if (!skippedCurrent && center == current)
            {
                skippedCurrent = true;
                return true;
            }
            if (excluded == null) return false;
            for (int i = 0; i < excluded.Count; i++)
                if (center == excluded[i]) return true;
            return false;
        }

        private static bool Fail(string message, out string reason)
        {
            reason = message;
            return false;
        }
    }
}
