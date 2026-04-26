using UnityEngine;

namespace WarSimulation.Combat.Map
{
    /// <summary>
    /// 木の散布フェーズ：<see cref="MapGenerationConfig"/> の個数に応じてマップ全体に
    /// <see cref="FeatureType.Tree"/> をランダム配置する（<see cref="RockPhase"/> と同様の棄却サンプリング）。
    /// 水セル・森クラスター領域（<see cref="MapData.ForestRegions"/>）・既存の木（クラスター内を含む）からの最小距離を避ける。
    /// </summary>
    public sealed class TreeScatterPhase : IMapGenerationPhase
    {
        public void Execute(MapData map, IRandom rng, MapGenerationConfig config)
        {
            if (map == null || rng == null || config == null) return;
            int target = config.ScatterTreeCount;
            if (target <= 0) return;

            float margin = config.ScatterTreePlacementMargin;
            float minCenter = margin;
            float maxCenter = config.WorldSize - margin;
            if (maxCenter <= minCenter)
            {
                minCenter = maxCenter = config.WorldSize * 0.5f;
            }

            float minDist = Mathf.Max(0f, config.ScatterTreeMinDistance);
            float minDistSq = minDist * minDist;
            int maxAttempts = Mathf.Max(target * 20, 100);
            int placed = 0;

            for (int attempt = 0; attempt < maxAttempts && placed < target; attempt++)
            {
                float x = Mathf.Lerp(minCenter, maxCenter, rng.NextFloat());
                float z = Mathf.Lerp(minCenter, maxCenter, rng.NextFloat());
                Vector3 worldPos = new(x, 0f, z);

                if (map.GroundStates.SampleAt(worldPos) == GroundState.Water) continue;

                if (IsInsideForest(map, new Vector2(x, z))) continue;

                if (minDistSq > 0f && IsTooCloseToAnyTree(map, x, z, minDistSq)) continue;

                float y = map.Height.SampleAt(worldPos);
                map.AddFeature(new PlacedFeature(
                    FeatureType.Tree,
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

        private static bool IsTooCloseToAnyTree(MapData map, float x, float z, float minDistSq)
        {
            var features = map.Features;
            for (int i = 0; i < features.Count; i++)
            {
                if (features[i].Type != FeatureType.Tree) continue;
                Vector3 wp = features[i].WorldPosition;
                float ddx = wp.x - x;
                float ddz = wp.z - z;
                if (ddx * ddx + ddz * ddz < minDistSq) return true;
            }
            return false;
        }
    }
}
