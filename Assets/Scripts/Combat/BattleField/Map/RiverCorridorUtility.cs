using System.Collections.Generic;
using UnityEngine;

namespace WarSimulation.Combat.Map
{
    /// <summary>
    /// 川の掘削 corridor（経路折れ線＋<see cref="RiverPath.WidthMeters"/> 全幅）判定。
    /// <see cref="RiverShape.Carve"/> と同じ幅を使う。
    /// </summary>
    public static class RiverCorridorUtility
    {
        public static bool Contains(MapData map, Vector2 worldXZ)
        {
            if (map == null) return false;

            var rivers = map.Rivers;
            if (rivers == null || rivers.Count == 0) return false;

            float cellSize = map.Height.CellSize;
            for (int r = 0; r < rivers.Count; r++)
            {
                if (Contains(rivers[r], cellSize, worldXZ)) return true;
            }

            return false;
        }

        public static bool Contains(RiverPath river, float cellSize, Vector2 worldXZ)
        {
            IReadOnlyList<Vector2Int> cells = river.Cells;
            if (cells == null || cells.Count < 2) return false;

            float halfW = river.WidthMeters * 0.5f;
            float rSq = halfW * halfW;

            for (int i = 0; i < cells.Count - 1; i++)
            {
                Vector2Int c0 = cells[i];
                Vector2Int c1 = cells[i + 1];
                Vector2 a = new((c0.x + 0.5f) * cellSize, (c0.y + 0.5f) * cellSize);
                Vector2 b = new((c1.x + 0.5f) * cellSize, (c1.y + 0.5f) * cellSize);
                if (DistanceSqPointToSegment(worldXZ, a, b) <= rSq) return true;
            }

            return false;
        }

        public static float DistanceSqPointToSegment(Vector2 p, Vector2 a, Vector2 b)
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
