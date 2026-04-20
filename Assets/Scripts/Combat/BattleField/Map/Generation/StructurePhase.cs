using System.Collections.Generic;
using UnityEngine;

namespace WarSimulation.Combat.Map
{
    /// <summary>
    /// 大構造フェーズ：山・丘・盆地などの大きな高度変化を配置する。
    /// 使うのは <see cref="HeightStampShape"/> アプリセットのリスト（複数種類・重複可）であり、
    /// 「HeightShapeKind が 3 種類＝山が 3 種類」ではない点に注意。
    /// MapGenerationConfig.StructureStamps から都度ランダムにスタンプを選び、
    /// スケール・向きも含めて仮決定したうえで「水との距離」「既存印との距離」の
    /// 2 条件を通れば HeightMap に 1 個押す。StructureStampCount に達するか、
    /// 総試行数が StructureMaxGlobalSearchIterations を超えたら打ち切る。
    ///
    /// 検査半径: _radius * scaleMax (Ridge は ridgeLength/2 も加える)。
    /// ノイズ拡張分は StructureRiverClearance / StructureMinCenterSeparation が吸収する前提で
    /// 事前棄却は控えめにする（空き地に置ける確率を優先）。
    /// 既存印との距離 = StructureMinCenterDistanceFactor × (実効半径の和) + StructureMinCenterSeparation。
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

            int target = Mathf.Max(0, config.StructureStampCount);
            int maxGlobal = Mathf.Max(1, config.StructureMaxGlobalSearchIterations);

            float minSep = Mathf.Max(0f, config.StructureMinCenterSeparation);
            float distFactor = Mathf.Clamp01(config.StructureMinCenterDistanceFactor);
            float clearance = Mathf.Max(0f, config.StructureRiverClearance);

            var placedCenters = new List<Vector2>(target);
            var placedExtents = new List<float>(target);

            int placed = 0;
            int attempts = 0;
            int waterRejects = 0;
            int distanceRejects = 0;

            // 総試行回数 = maxGlobal を 1 重ループで消費する（以前は maxGlobal × perStampAttempts で過剰試行になっていた）
            for (; placed < target && attempts < maxGlobal; attempts++)
            {
                HeightStampShape shape = stamps[rng.NextInt(0, stamps.Count)];
                if (shape == null) continue;

                float scaleX = Mathf.Lerp(0.7f, 1.3f, rng.NextFloat());
                float scaleY = Mathf.Lerp(0.7f, 1.3f, rng.NextFloat());
                float rotation = rng.NextFloat() * Mathf.PI * 2f;

                float extent = ComputeStampExtent(shape, scaleX, scaleY);

                Vector2 center = new Vector2(
                    Mathf.Lerp(minCenter, maxCenter, rng.NextFloat()),
                    Mathf.Lerp(minCenter, maxCenter, rng.NextFloat()));

                if (map.GroundStates.HasAnyCellInCircle(center, extent + clearance, GroundState.Water))
                {
                    waterRejects++;
                    continue;
                }

                if (placedCenters.Count > 0
                    && !IsFarEnoughFromPlaced(center, extent, placedCenters, placedExtents, minSep, distFactor))
                {
                    distanceRejects++;
                    continue;
                }

                shape.Apply(map, new StampPlacement(center, rotation, new Vector2(scaleX, scaleY)));
                placedCenters.Add(center);
                placedExtents.Add(extent);
                placed++;
            }

            map.StructureStampPlacedCount = placed;
            map.StructureTotalAttempts = attempts;
            map.StructureWaterRejects = waterRejects;
            map.StructureDistanceRejects = distanceRejects;
        }

        /// <summary>
        /// 候補中心が、既存の各中心から「係数×実効半径の和 + 余白」以上離れているか。
        /// </summary>
        private static bool IsFarEnoughFromPlaced(
            Vector2 center, float newExtent,
            List<Vector2> placedCenters, List<float> placedExtents,
            float extraSeparation, float distanceFactor)
        {
            for (int i = 0; i < placedCenters.Count; i++)
            {
                float need = distanceFactor * (placedExtents[i] + newExtent) + extraSeparation;
                if (need <= 0f) continue;
                float needSq = need * need;
                if ((placedCenters[i] - center).sqrMagnitude < needSq)
                    return false;
            }
            return true;
        }

        /// <summary>
        /// 水チェックと距離判定の両方で使う「実効半径」（ワールド単位）。
        /// <see cref="HeightStampShape.Apply"/> が高度を動かす最大半径と一致させる
        /// （ノイズで外側に揺らぐ分も含む）。これにより clearance が 0 でも「山が水に入らない」が保証される。
        /// </summary>
        private static float ComputeStampExtent(HeightStampShape shape, float scaleX, float scaleY)
        {
            float scaleMax = Mathf.Max(scaleX, scaleY);
            float noiseExpand = 1f + shape.NoiseAmplitude;
            if (shape.Kind == HeightShapeKind.Ridge)
                return (shape.Radius * noiseExpand + shape.RidgeLength * 0.5f) * scaleMax;
            return shape.Radius * noiseExpand * scaleMax;
        }
    }
}
