using UnityEngine;

namespace WarSimulation.Combat.Map
{
    public static class BridgeBuildUtility
    {
        private const float WaterSurfaceRatio = 0.85f;

        public static void PlaceAutoBridges(MapData map, MapGenerationConfig config)
        {
            if (map == null || config == null) return;
            if (config.BridgesPerRiver <= 0 || map.Rivers.Count == 0) return;

            int bridgesPerRiver = config.BridgesPerRiver;
            float depth = config.RiverShape != null ? config.RiverShape.DepthMeters : 0.6f;
            float waterY = -depth * (1f - WaterSurfaceRatio);
            float bridgeY = waterY + config.BridgeHeightAboveWater;
            float cs = map.Height.CellSize;

            for (int r = 0; r < map.Rivers.Count; r++)
            {
                RiverPath river = map.Rivers[r];
                var cells = river.Cells;
                if (cells.Count < 3) continue;

                float bridgeLength = river.WidthMeters + config.BridgeLengthExtraMargin;
                Vector3 bridgeScale = new Vector3(config.BridgeWidth, config.BridgeThickness, bridgeLength);

                for (int b = 0; b < bridgesPerRiver; b++)
                {
                    int idx = (int)((b + 1L) * cells.Count / (bridgesPerRiver + 1L));
                    idx = Mathf.Clamp(idx, 1, cells.Count - 2);

                    Vector2Int prev = cells[idx - 1];
                    Vector2Int next = cells[idx + 1];
                    Vector2Int here = cells[idx];

                    Vector2 tangent = new Vector2(next.x - prev.x, next.y - prev.y);
                    if (tangent.sqrMagnitude < 1e-6f) tangent = new Vector2(1f, 0f);
                    tangent.Normalize();

                    float yRot = Mathf.Atan2(-tangent.y, tangent.x) * Mathf.Rad2Deg;
                    Vector3 worldPos = new Vector3(
                        (here.x + 0.5f) * cs,
                        bridgeY,
                        (here.y + 0.5f) * cs);

                    map.AddFeature(new PlacedFeature(
                        FeatureType.Bridge,
                        worldPos,
                        Quaternion.Euler(0f, yRot, 0f),
                        bridgeScale));
                }
            }
        }

        public static float ResolveBridgeY(MapGenerationConfig config, float riverDepthMeters)
        {
            float depth = riverDepthMeters > 0f
                ? riverDepthMeters
                : (config != null && config.RiverShape != null ? config.RiverShape.DepthMeters : 0.6f);
            float waterY = -depth * (1f - WaterSurfaceRatio);
            float above = config != null ? config.BridgeHeightAboveWater : 0.3f;
            return waterY + above;
        }
    }
}