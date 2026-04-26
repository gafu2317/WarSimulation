using System.Collections.Generic;
using UnityEngine;

namespace WarSimulation.Combat.Map
{
    /// <summary>
    /// 大構造フェーズ：山・丘・盆地などの大きな高度変化を配置する。
    /// <see cref="MapGenerationConfig.StructureStampEntries"/> を上から順に、
    /// 各行の Count 回だけその Shape を押す（大きい山を先に置きたいならリスト先頭に並べる）。
    /// 「水との距離」「既存印との距離」を通ったら 1 個押す。
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

            float minCenter = config.StructurePlacementMargin;
            float maxCenter = config.WorldSize - config.StructurePlacementMargin;
            if (maxCenter <= minCenter)
            {
                minCenter = maxCenter = config.WorldSize * 0.5f;
            }

            int target = Mathf.Max(0, config.StructureStampTargetTotal);
            if (target == 0) return;

            int maxGlobal = Mathf.Max(1, config.StructureMaxGlobalSearchIterations);
            int perPlacementAttempts = Mathf.Max(1, config.StructureMaxPlacementAttempts);

            float minSep = Mathf.Max(0f, config.StructureMinCenterSeparation);
            float distFactor = Mathf.Clamp01(config.StructureMinCenterDistanceFactor);
            float clearance = Mathf.Max(0f, config.StructureRiverClearance);

            var placedCenters = new List<Vector2>(target);
            var placedExtents = new List<float>(target);

            int placed = 0;
            int attempts = 0;
            int waterRejects = 0;
            int distanceRejects = 0;

            IReadOnlyList<StructureStampEntry> entries = config.StructureStampEntries;
            if (entries == null) return;

            for (int ei = 0; ei < entries.Count; ei++)
            {
                if (attempts >= maxGlobal) break;

                StructureStampEntry entry = entries[ei];
                if (entry == null || entry.Shape == null || entry.Count <= 0) continue;

                HeightStampShape shape = entry.Shape;
                for (int rep = 0; rep < entry.Count; rep++)
                {
                    if (attempts >= maxGlobal) break;

                    bool slotPlaced = false;
                    for (int t = 0; t < perPlacementAttempts && attempts < maxGlobal && !slotPlaced; t++)
                    {
                        attempts++;

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
                        slotPlaced = true;
                    }
                }
            }

            map.StructureStampPlacedCount = placed;
            map.StructureTotalAttempts = attempts;
            map.StructureWaterRejects = waterRejects;
            map.StructureDistanceRejects = distanceRejects;
        }

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
