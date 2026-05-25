using UnityEngine;

namespace WarSimulation.Combat.Map
{
    internal static class TreePlacementUtility
    {
        public static bool IsValidTreeSite(
            MapData map,
            Vector2 pos,
            bool hasHeightLimit,
            float maxHeight)
        {
            return IsValidTreeSite(map, pos, hasHeightLimit, maxHeight, requireForestRegion: false, default);
        }

        public static bool IsValidTreeSite(
            MapData map,
            ForestRegion forestRegion,
            Vector2 pos,
            bool hasHeightLimit,
            float maxHeight)
        {
            return IsValidTreeSite(map, pos, hasHeightLimit, maxHeight, requireForestRegion: true, forestRegion);
        }

        public static bool IsInsideAnyForest(MapData map, Vector2 pos)
        {
            var regions = map.ForestRegions;
            for (int i = 0; i < regions.Count; i++)
            {
                if (regions[i].Contains(pos)) return true;
            }
            return false;
        }

        private static bool IsValidTreeSite(
            MapData map,
            Vector2 pos,
            bool hasHeightLimit,
            float maxHeight,
            bool requireForestRegion,
            ForestRegion forestRegion)
        {
            if (map == null) return false;
            if (requireForestRegion && !forestRegion.Contains(pos)) return false;

            Vector3 world3 = new(pos.x, 0f, pos.y);
            if (map.GroundStates.SampleAt(world3) == GroundState.Water) return false;
            if (map.Height.SampleCliffFace(world3)) return false;
            if (RiverCorridorUtility.Contains(map, pos)) return false;
            if (hasHeightLimit && map.Height.SampleAt(world3) > maxHeight) return false;
            if (BridgePlacementUtility.IsNearAnyBridge(map, pos, map.BridgeFeatureExclusionMargin)) return false;

            return true;
        }

    }
}
