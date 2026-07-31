using System.Collections.Generic;
using UnityEngine;

namespace WarSimulation.Combat.Map
{
    /// <summary>
    /// 岩フェーズ：マップ全体にランダムで岩を散布する。
    /// 棄却サンプリングで以下を避ける：
    ///   - 水セル（川・湖の中心部タグ）
    ///   - 川・湖の掘削・見た目の範囲（Water タグが岸に付かない設定でも岸に岩が乗らないようにする）
    ///   - 森クラスター領域（<see cref="MapData.ForestRegions"/>）
    ///   - 橋フットプリント近傍（<see cref="MapData.BridgeFeatureExclusionMargin"/>）
    ///   - マップ高度レンジ上位（既定 30%）
    ///   - 既存の岩からの最小距離
    /// </summary>
    public sealed class RockPhase : IMapGenerationPhase
    {
        public void Execute(MapData map, IRandom rng, MapConfig config)
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

            float ratio = Mathf.Clamp01(config.RockTopHeightExclusionRatio);
            GetMapHeightRange(map.Height, out float minH, out float maxH);
            float allowedMaxHeight = minH + (1f - ratio) * (maxH - minH);

            for (int attempt = 0; attempt < maxAttempts && placed < target; attempt++)
            {
                float x = Mathf.Lerp(minCenter, maxCenter, rng.NextFloat());
                float z = Mathf.Lerp(minCenter, maxCenter, rng.NextFloat());
                Vector3 worldPos = new(x, 0f, z);

                if (map.GroundStates.SampleAt(worldPos) == GroundState.Water) continue;

                var xz = new Vector2(x, z);
                if (IsInsideLakeCarve(map, xz)) continue;
                if (RiverCorridorUtility.Contains(map, xz)) continue;

                if (IsInsideForest(map, xz)) continue;

                if (BridgePlacementUtility.IsNearAnyBridge(map, xz, config.BridgeFeatureExclusionMargin)) continue;

                if (minDistSq > 0f && IsTooCloseToExistingRock(map, startIndex, x, z, minDistSq)) continue;

                float y = map.Height.SampleAt(worldPos);
                if (maxH > minH && y > allowedMaxHeight) continue;

                map.AddFeature(new PlacedFeature(
                    FeatureType.Rock,
                    new Vector3(x, y, z),
                    Quaternion.identity));
                placed++;
            }
        }

        private static bool IsInsideLakeCarve(MapData map, Vector2 xz)
        {
            var lakes = map.Lakes;
            for (int i = 0; i < lakes.Count; i++)
            {
                if (lakes[i].ContainsCarve(xz)) return true;
            }
            return false;
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

        private static void GetMapHeightRange(HeightMap heightMap, out float min, out float max)
        {
            min = float.PositiveInfinity;
            max = float.NegativeInfinity;
            for (int z = 0; z < heightMap.Height; z++)
            {
                for (int x = 0; x < heightMap.Width; x++)
                {
                    float v = heightMap.GetHeight(x, z);
                    if (v < min) min = v;
                    if (v > max) max = v;
                }
            }

            if (float.IsInfinity(min) || float.IsInfinity(max))
            {
                min = 0f;
                max = 0f;
            }
        }
    }
}
