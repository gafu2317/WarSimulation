using UnityEngine;

namespace WarSimulation.Combat.Map
{
    /// <summary>
    /// 地面状態フェーズ：GroundStateGrid に沼・雪などの「地面の状態」パッチを配置する。
    /// <see cref="MapGenerationConfig.GroundPatchStamps"/> からランダムに選んだスタンプを、
    /// マップ内のランダムな位置に <see cref="MapGenerationConfig.GroundPatchStampCount"/> 回押す。
    ///
    /// 適用順は River → Lake → Structure → GroundPatch なので、このフェーズ時点で
    /// HeightMap は確定し、Water セルも打たれている。スタンプ側は「Water は触らない」
    /// ルールだけ守ればよい。
    /// </summary>
    public sealed class GroundPatchPhase : IMapGenerationPhase
    {
        public void Execute(MapData map, IRandom rng, MapGenerationConfig config)
        {
            if (map == null || rng == null || config == null) return;
            var stamps = config.GroundPatchStamps;
            if (stamps == null || stamps.Count == 0) return;

            float minCenter = config.GroundPatchPlacementMargin;
            float maxCenter = config.WorldSize - config.GroundPatchPlacementMargin;
            if (maxCenter <= minCenter)
            {
                minCenter = maxCenter = config.WorldSize * 0.5f;
            }

            int count = config.GroundPatchStampCount;
            for (int i = 0; i < count; i++)
            {
                GroundPatchStampShape shape = stamps[rng.NextInt(0, stamps.Count)];
                if (shape == null) continue;

                Vector2 center = new(
                    Mathf.Lerp(minCenter, maxCenter, rng.NextFloat()),
                    Mathf.Lerp(minCenter, maxCenter, rng.NextFloat()));

                shape.Apply(map, new StampPlacement(center));
            }
        }
    }
}
