using UnityEngine;

namespace WarSimulation.Combat.Map
{
    /// <summary>
    /// 森フェーズ：<see cref="ForestClusterStampShape"/> を配置して木（<see cref="FeatureType.Tree"/>）を
    /// クラスター状に散布する。スタンプ側が <see cref="MapData.ForestRegions"/> にも
    /// ノイズ歪み込みの不整形領域を登録するので、後続フェーズ（RockPhase など）がそれを参照して森を避けられる。
    /// </summary>
    public sealed class ForestPhase : IMapGenerationPhase
    {
        public void Execute(MapData map, IRandom rng, MapGenerationConfig config)
        {
            if (map == null || rng == null || config == null) return;
            var stamps = config.ForestClusterStamps;
            if (stamps == null || stamps.Count == 0) return;

            float minCenter = config.ForestPlacementMargin;
            float maxCenter = config.WorldSize - config.ForestPlacementMargin;
            if (maxCenter <= minCenter)
            {
                minCenter = maxCenter = config.WorldSize * 0.5f;
            }

            int count = config.ForestClusterCount;
            for (int i = 0; i < count; i++)
            {
                ForestClusterStampShape shape = stamps[rng.NextInt(0, stamps.Count)];
                if (shape == null) continue;

                Vector2 center = new(
                    Mathf.Lerp(minCenter, maxCenter, rng.NextFloat()),
                    Mathf.Lerp(minCenter, maxCenter, rng.NextFloat()));

                shape.Apply(map, new StampPlacement(center));
            }
        }
    }
}
