using UnityEngine;

namespace WarSimulation.Combat.Map
{
    /// <summary>
    /// 岩フェーズ：マップ全体にランダムで岩を散布する。
    /// 以下を避けた候補から抽選する：
    ///   - 森・湖の外接範囲と、岩の占有半径ぶんの周辺
    ///   - 水セル（川・湖の中心部タグ）
    ///   - 川・湖の掘削・見た目の範囲（Water タグが岸に付かない設定でも岸に岩が乗らないようにする）
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

            float margin = Mathf.Max(config.RockPlacementMargin, map.PlacementRadii.Rock);
            float minCenter = margin;
            float maxCenter = config.WorldSize - margin;
            if (maxCenter < minCenter)
            {
                Debug.LogWarning("[RockPhase] 岩の占有範囲をマップ内に収められません。");
                return;
            }

            float ratio = Mathf.Clamp01(config.RockTopHeightExclusionRatio);
            GetMapHeightRange(map.Height, out float minH, out float maxH);
            float heightRangeMin = Mathf.Max(0f, minH);
            float allowedMaxHeight = heightRangeMin + (1f - ratio) * (maxH - heightRangeMin);

            var candidates = new PlacementCandidates(map, FeatureType.Rock,
                Rect.MinMaxRect(minCenter, minCenter, maxCenter, maxCenter),
                config.RockMinDistance, rng);
            float clearance = map.PlacementRadii.Rock + map.PlacementRadii.Clearance;
            foreach (var forest in map.ForestRegions)
                candidates.ExcludeCircle(forest.Center, forest.OuterRadius + clearance);
            foreach (var lake in map.Lakes)
                candidates.ExcludeCircle(lake.Center, lake.OuterRadius + clearance);
            candidates.KeepWhere(xz =>
            {
                var world = new Vector3(xz.x, 0f, xz.y);
                return map.GroundStates.SampleAt(world) != GroundState.Water &&
                    !RiverCorridorUtility.Contains(map, xz, clearance) &&
                    (maxH <= minH || map.Height.SampleAt(world) <= allowedMaxHeight);
            });

            int placed = 0;
            while (placed < target && candidates.TryTake(rng, out Vector2 pos))
            {
                float x = pos.x, z = pos.y;
                float y = map.Height.SampleAt(new Vector3(x, 0f, z));
                map.AddFeature(new PlacedFeature(
                    FeatureType.Rock,
                    new Vector3(x, y, z),
                    Quaternion.identity));
                placed++;
            }
            if (placed < target)
                Debug.LogWarning($"[RockPhase] 配置可能な候補を使い切りました: {placed}/{target} 個");
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
