using System.Collections.Generic;
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
            if (IsInsideRiverCorridor(map, pos)) return false;
            if (hasHeightLimit && map.Height.SampleAt(world3) > maxHeight) return false;

            return true;
        }

        /// <summary>
        /// 川は Water タグより広く掘削されることがあるため、経路線分と川幅でフル幅を判定する。
        /// </summary>
        private static bool IsInsideRiverCorridor(MapData map, Vector2 xz)
        {
            var rivers = map.Rivers;
            if (rivers.Count == 0) return false;
            float cs = map.Height.CellSize;

            for (int r = 0; r < rivers.Count; r++)
            {
                RiverPath river = rivers[r];
                IReadOnlyList<Vector2Int> cells = river.Cells;
                if (cells == null || cells.Count < 2) continue;

                float halfW = river.WidthMeters * 0.5f;
                float rSq = halfW * halfW;

                for (int i = 0; i < cells.Count - 1; i++)
                {
                    Vector2Int c0 = cells[i];
                    Vector2Int c1 = cells[i + 1];
                    Vector2 a = new((c0.x + 0.5f) * cs, (c0.y + 0.5f) * cs);
                    Vector2 b = new((c1.x + 0.5f) * cs, (c1.y + 0.5f) * cs);
                    if (DistanceSqPointToSegment(xz, a, b) <= rSq) return true;
                }
            }

            return false;
        }

        private static float DistanceSqPointToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float abSqr = ab.sqrMagnitude;
            if (abSqr < 1e-8f) return (p - a).sqrMagnitude;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / abSqr);
            Vector2 closest = a + ab * t;
            return (p - closest).sqrMagnitude;
        }
    }
}
