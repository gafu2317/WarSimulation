using UnityEngine;

namespace WarSimulation.Combat.Map
{
    /// <summary>
    /// 木の散布フェーズ：<see cref="MapConfig"/> の個数に応じてマップ全体に
    /// <see cref="FeatureType.Tree"/> をランダム配置する（空き候補から抽選）。
    /// 水セル・川幅・崖面・森クラスター領域（<see cref="MapData.ForestRegions"/>）・橋近傍・マップ playable 範囲外・既存の木（クラスター内を含む）からの最小距離を避ける。
    /// </summary>
    public sealed class TreeScatterPhase : IMapGenerationPhase
    {
        public void Execute(MapData map, IRandom rng, MapConfig config)
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

            var candidates = new PlacementCandidates(map, FeatureType.Tree,
                Rect.MinMaxRect(minCenter, minCenter, maxCenter, maxCenter),
                config.ScatterTreeMinDistance, rng);
            candidates.KeepWhere(pos =>
                TreePlacementUtility.IsValidTreeSite(map, pos, hasHeightLimit: false, maxHeight: 0f) &&
                !TreePlacementUtility.IsInsideAnyForest(map, pos));

            int placed = 0;
            while (placed < target && candidates.TryTake(rng, out Vector2 pos))
            {
                float x = pos.x, z = pos.y;
                float y = map.Height.SampleAt(new Vector3(x, 0f, z));
                map.AddFeature(new PlacedFeature(
                    FeatureType.Tree,
                    new Vector3(x, y, z),
                    Quaternion.identity));
                placed++;
            }
            if (placed < target)
                Debug.LogWarning($"[TreeScatter] 配置可能な候補を使い切りました: {placed}/{target} 本");
        }

    }
}
