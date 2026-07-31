using UnityEngine;

namespace WarSimulation.Combat.Map
{
    public static class BridgeBuildUtility
    {
        private const float WaterSurfaceRatio = 0.85f;

        public static float ResolveBridgeY(MapConfig config, float riverDepthMeters)
        {
            float depth = riverDepthMeters > 0f
                ? riverDepthMeters
                : (config != null && config.RiverShape != null ? config.RiverShape.DepthMeters : 0.6f);
            float waterY = -depth * (1f - WaterSurfaceRatio);
            float above = config != null ? config.BridgeHeightAboveWater : 0.3f;
            return waterY + above;
        }

        public static Vector3 DefaultBridgeScale(MapConfig config, float riverWidthMeters)
        {
            float width = config != null ? config.BridgeWidth : 2f;
            float thickness = config != null ? config.BridgeThickness : 0.25f;
            float lengthExtra = config != null ? config.BridgeLengthExtraMargin : 1f;
            float riverW = riverWidthMeters > 0.0001f
                ? riverWidthMeters
                : (config?.RiverShape != null ? config.RiverShape.WidthMeters : width);
            return new Vector3(width, thickness, riverW + lengthExtra);
        }

        public static float FindNearestRiverDepth(MapData map, Vector2 center, float fallback)
        {
            if (map == null || map.Rivers.Count == 0) return fallback;

            float bestSq = float.PositiveInfinity;
            float depth = fallback;
            float cs = map.Height.CellSize;
            for (int r = 0; r < map.Rivers.Count; r++)
            {
                RiverPath river = map.Rivers[r];
                for (int c = 0; c < river.Cells.Count; c++)
                {
                    Vector2Int cell = river.Cells[c];
                    Vector2 world = new Vector2((cell.x + 0.5f) * cs, (cell.y + 0.5f) * cs);
                    float sq = (world - center).sqrMagnitude;
                    if (sq >= bestSq) continue;
                    bestSq = sq;
                    depth = river.DepthMeters;
                }
            }

            return depth;
        }

        /// <summary>
        /// 近くの川セルへスナップし、接線から向き・寸法を決める。川が遠い場合は desired をそのまま使う。
        /// </summary>
        public static void ResolveAuthoredPlacement(
            MapData map,
            MapConfig config,
            Vector2 desiredCenter,
            float snapRadiusMeters,
            out Vector2 center,
            out float rotationDeg,
            out Vector3 scale)
        {
            center = desiredCenter;
            rotationDeg = 0f;
            scale = DefaultBridgeScale(config, 0f);

            if (map?.Height == null || map.Rivers.Count == 0)
                return;

            float cs = map.Height.CellSize;
            float bestSq = snapRadiusMeters > 0f
                ? snapRadiusMeters * snapRadiusMeters
                : float.PositiveInfinity;
            bool found = false;
            Vector2 bestCenter = desiredCenter;
            Vector2 bestTangent = new Vector2(1f, 0f);
            float bestWidth = 0f;

            for (int r = 0; r < map.Rivers.Count; r++)
            {
                RiverPath river = map.Rivers[r];
                var cells = river.Cells;
                if (cells == null || cells.Count < 2) continue;

                for (int c = 0; c < cells.Count; c++)
                {
                    Vector2Int cell = cells[c];
                    Vector2 world = new Vector2((cell.x + 0.5f) * cs, (cell.y + 0.5f) * cs);
                    float sq = (world - desiredCenter).sqrMagnitude;
                    if (sq >= bestSq) continue;

                    bestSq = sq;
                    bestCenter = world;
                    bestWidth = river.WidthMeters;
                    int prev = Mathf.Max(0, c - 1);
                    int next = Mathf.Min(cells.Count - 1, c + 1);
                    if (prev == next)
                    {
                        bestTangent = new Vector2(1f, 0f);
                    }
                    else
                    {
                        Vector2Int a = cells[prev];
                        Vector2Int b = cells[next];
                        bestTangent = new Vector2(b.x - a.x, b.y - a.y);
                        if (bestTangent.sqrMagnitude < 1e-6f)
                            bestTangent = new Vector2(1f, 0f);
                    }

                    found = true;
                }
            }

            if (!found) return;

            bestTangent.Normalize();
            center = bestCenter;
            rotationDeg = Mathf.Atan2(-bestTangent.y, bestTangent.x) * Mathf.Rad2Deg;
            scale = DefaultBridgeScale(config, bestWidth);
        }
    }
}
