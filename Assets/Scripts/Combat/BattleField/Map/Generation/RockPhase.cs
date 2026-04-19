using UnityEngine;

namespace WarSimulation.Combat.Map
{
    /// <summary>
    /// 岩フェーズ：マップ全体にランダムで岩を散布する。
    /// 棄却サンプリングで以下を避ける：
    ///   - 水セル（川・湖）
    ///   - 森クラスター領域（<see cref="MapData.ForestRegions"/>）
    ///   - 既存の岩からの最小距離
    /// </summary>
    public sealed class RockPhase : IMapGenerationPhase
    {
        public void Execute(MapData map, IRandom rng, MapGenerationConfig config)
        {
            if (map == null || rng == null || config == null) return;
            int target = config.RockCount;
            if (target <= 0) return;

            float margin = config.RockPlacementMargin;
            float minCenter = margin;
            float maxCenter = config.WorldSize - margin;
            if (maxCenter <= minCenter)
            {
                minCenter = maxCenter = config.WorldSize * 0.5f;
            }

            float minDist = Mathf.Max(0f, config.RockMinDistance);
            float minDistSq = minDist * minDist;
            int maxAttempts = Mathf.Max(target * 20, 100);
            int placed = 0;
            int startIndex = map.Features.Count;

            for (int attempt = 0; attempt < maxAttempts && placed < target; attempt++)
            {
                float x = Mathf.Lerp(minCenter, maxCenter, rng.NextFloat());
                float z = Mathf.Lerp(minCenter, maxCenter, rng.NextFloat());
                Vector3 worldPos = new(x, 0f, z);

                if (map.GroundStates.SampleAt(worldPos) == GroundState.Water) continue;

                if (IsInsideForest(map, new Vector2(x, z))) continue;

                if (minDistSq > 0f && IsTooCloseToExistingRock(map, startIndex, x, z, minDistSq)) continue;

                float y = map.Height.SampleAt(worldPos);
                map.AddFeature(new PlacedFeature(
                    FeatureType.Rock,
                    new Vector3(x, y, z),
                    Quaternion.identity));
                placed++;
            }
        }

        private static bool IsInsideForest(MapData map, Vector2 pos)
        {
            var regions = map.ForestRegions;
            for (int i = 0; i < regions.Count; i++)
            {
                if (regions[i].Contains(pos)) return true;
            }
            return false;
        }

        private static bool IsTooCloseToExistingRock(MapData map, int startIndex, float x, float z, float minDistSq)
        {
            var features = map.Features;
            for (int i = startIndex; i < features.Count; i++)
            {
                if (features[i].Type != FeatureType.Rock) continue;
                Vector3 wp = features[i].WorldPosition;
                float ddx = wp.x - x;
                float ddz = wp.z - z;
                if (ddx * ddx + ddz * ddz < minDistSq) return true;
            }
            return false;
        }
    }
}
