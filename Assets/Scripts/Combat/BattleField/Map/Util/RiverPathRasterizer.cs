using System.Collections.Generic;
using UnityEngine;

namespace WarSimulation.Combat.Map
{
    /// <summary>
    /// ワールド XZ の制御点列を、隙間のない HeightMap セル経路へ変換する。
    /// </summary>
    public static class RiverPathRasterizer
    {
        public static List<Vector2Int> Rasterize(IReadOnlyList<Vector2> controlPoints, HeightMap height)
        {
            var path = new List<Vector2Int>();
            if (controlPoints == null || height == null || controlPoints.Count == 0)
                return path;

            Vector2Int previous = WorldToCell(height, controlPoints[0]);
            path.Add(previous);

            for (int i = 1; i < controlPoints.Count; i++)
            {
                Vector2Int next = WorldToCell(height, controlPoints[i]);
                AppendLine(path, previous, next);
                previous = next;
            }

            return path;
        }

        public static Vector2Int WorldToCell(HeightMap height, Vector2 world)
        {
            return new Vector2Int(
                Mathf.Clamp(Mathf.FloorToInt(world.x / height.CellSize), 0, height.Width - 1),
                Mathf.Clamp(Mathf.FloorToInt(world.y / height.CellSize), 0, height.Height - 1));
        }

        private static void AppendLine(List<Vector2Int> path, Vector2Int from, Vector2Int to)
        {
            int dx = Mathf.Abs(to.x - from.x);
            int dy = Mathf.Abs(to.y - from.y);
            int sx = from.x < to.x ? 1 : -1;
            int sy = from.y < to.y ? 1 : -1;
            int err = dx - dy;
            int x = from.x;
            int y = from.y;

            while (true)
            {
                var cell = new Vector2Int(x, y);
                if (path.Count == 0 || path[path.Count - 1] != cell)
                    path.Add(cell);
                if (x == to.x && y == to.y) break;

                int e2 = err * 2;
                if (e2 > -dy)
                {
                    err -= dy;
                    x += sx;
                }

                if (e2 < dx)
                {
                    err += dx;
                    y += sy;
                }
            }
        }
    }
}
