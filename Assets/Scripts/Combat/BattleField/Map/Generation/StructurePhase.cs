using UnityEngine;

namespace WarSimulation.Combat.Map
{
    /// <summary>
    /// 大構造フェーズ：山・丘・盆地などの大きな高度変化を配置する。
    /// MapGenerationConfig.StructureStamps の中からランダムに選んだスタンプを、
    /// マップ内のランダムな位置・回転・非一様スケールで _structureStampCount 回押す。
    ///
    /// 水との共存：RiverPhase・LakePhase が先に走ってから本フェーズが動く前提。
    /// 両フェーズが GroundStateGrid に書き込む Water 状態（＝川＋湖）を避けるよう、
    /// 候補中心がスタンプ半径 + StructureRiverClearance 以内に Water セルを含む場合は
    /// 棄却して再抽選する（最大 StructureMaxPlacementAttempts 回）。
    /// </summary>
    public sealed class StructurePhase : IMapGenerationPhase
    {
        public void Execute(MapData map, IRandom rng, MapGenerationConfig config)
        {
            if (map == null || rng == null || config == null) return;

            var stamps = config.StructureStamps;
            if (stamps == null || stamps.Count == 0) return;

            float minCenter = config.StructurePlacementMargin;
            float maxCenter = config.WorldSize - config.StructurePlacementMargin;
            if (maxCenter <= minCenter)
            {
                minCenter = maxCenter = config.WorldSize * 0.5f;
            }

            int count = config.StructureStampCount;
            int maxAttempts = Mathf.Max(1, config.StructureMaxPlacementAttempts);

            for (int i = 0; i < count; i++)
            {
                HeightStampShape shape = stamps[rng.NextInt(0, stamps.Count)];
                if (shape == null) continue;

                // 最大スケールと shape 半径からクリアランス距離を算出
                float baseRadius = shape.Kind == HeightShapeKind.Ridge
                    ? shape.Radius + shape.RidgeLength * 0.5f
                    : shape.Radius;

                if (!TryPickCenter(
                    map.GroundStates, rng, minCenter, maxCenter,
                    baseRadius, config.StructureRiverClearance,
                    maxAttempts, out Vector2 center))
                {
                    // 川・湖で塞がっていて置く場所がない：このスタンプはスキップして次へ
                    continue;
                }

                float rotation = rng.NextFloat() * Mathf.PI * 2f;
                float scaleX = Mathf.Lerp(0.7f, 1.3f, rng.NextFloat());
                float scaleY = Mathf.Lerp(0.7f, 1.3f, rng.NextFloat());

                shape.Apply(map, new StampPlacement(center, rotation, new Vector2(scaleX, scaleY)));
            }
        }

        /// <summary>
        /// 配置可能な中心点を最大 <paramref name="maxAttempts"/> 回の乱択で探す。
        /// 非一様スケール前の半径 + clearance に River タグセルを含まなければ採用。
        /// 見つからなければ false。
        /// </summary>
        private static bool TryPickCenter(
            GroundStateGrid g, IRandom rng,
            float minCenter, float maxCenter,
            float baseRadius, float clearance,
            int maxAttempts, out Vector2 center)
        {
            // 非一様スケール最大 1.3 倍を見込むので、検査半径は baseRadius * 1.3 + clearance
            float checkRadius = baseRadius * 1.3f + Mathf.Max(0f, clearance);

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                center = new Vector2(
                    Mathf.Lerp(minCenter, maxCenter, rng.NextFloat()),
                    Mathf.Lerp(minCenter, maxCenter, rng.NextFloat()));

                if (!OverlapsWaterCell(g, center, checkRadius))
                {
                    return true;
                }
            }

            center = default;
            return false;
        }

        /// <summary>
        /// <paramref name="center"/> を中心とする半径 <paramref name="radius"/> の円内に
        /// GroundStateGrid 上の Water セル（川＋湖）があるかを判定する。
        /// </summary>
        private static bool OverlapsWaterCell(GroundStateGrid g, Vector2 center, float radius)
        {
            if (g == null || radius <= 0f) return false;

            float gCell = g.CellSize;
            int cx = Mathf.FloorToInt(center.x / gCell);
            int cy = Mathf.FloorToInt(center.y / gCell);
            int r = Mathf.CeilToInt(radius / gCell);
            float rSqr = radius * radius;

            for (int y = cy - r; y <= cy + r; y++)
            {
                for (int x = cx - r; x <= cx + r; x++)
                {
                    if (!g.IsInBounds(x, y)) continue;
                    if (g.GetCell(x, y) != GroundState.Water) continue;

                    float wx = (x + 0.5f) * gCell - center.x;
                    float wy = (y + 0.5f) * gCell - center.y;
                    if (wx * wx + wy * wy <= rSqr) return true;
                }
            }
            return false;
        }
    }
}
