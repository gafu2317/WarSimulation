using System.Collections.Generic;
using UnityEngine;

namespace WarSimulation.Combat.Map
{
    /// <summary>
    /// 山フェーズ：大山 1 個と小山リストを、川・湖より先に配置する。
    /// 小山の配置規則は旧 StructurePhase と同じ棄却サンプリングを使う。
    /// </summary>
    public sealed class MountainPhase : IMapGenerationPhase
    {
        private const float CliffDirectionJitterDeg = 20f;

        public void Execute(MapData map, IRandom rng, MapGenerationConfig config)
        {
            if (map == null || rng == null || config == null) return;

            int target = Mathf.Max(0, config.MountainStampTargetTotal);
            if (target == 0) return;

            int maxGlobal = Mathf.Max(1, config.MountainMaxGlobalSearchIterations);
            int perPlacementAttempts = Mathf.Max(1, config.MountainMaxPlacementAttempts);
            float minSep = Mathf.Max(0f, config.MountainMinCenterSeparation);
            float distFactor = Mathf.Clamp01(config.MountainMinCenterDistanceFactor);

            var placedCenters = new List<Vector2>(target);
            var placedExtents = new List<float>(target);

            int placed = 0;
            int attempts = 0;
            int distanceRejects = 0;

            if (TryPlaceLargeMountain(map, rng, config, placedCenters, placedExtents))
            {
                placed++;
            }

            IReadOnlyList<StructureStampEntry> entries = config.SmallMountainStampEntries;
            if (entries != null)
            {
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
                            StampPlacement placement = CreateRandomPlacement(
                                rng, shape, config.WorldSize, config.MountainPlacementMargin);
                            float extent = HeightStampPlacementUtility.ComputeExtent(shape, placement.Scale);

                            if (placedCenters.Count > 0 &&
                                !HeightStampPlacementUtility.IsFarEnoughFromPlaced(
                                    placement.Center, extent, placedCenters, placedExtents, minSep, distFactor))
                            {
                                distanceRejects++;
                                continue;
                            }

                            ApplyMountain(map, MountainKind.Small, shape, placement, extent, placedCenters, placedExtents);
                            placed++;
                            slotPlaced = true;
                        }
                    }
                }
            }

            map.StructureStampPlacedCount = placed;
            map.StructureTotalAttempts = attempts;
            map.StructureWaterRejects = 0;
            map.StructureDistanceRejects = distanceRejects;
        }

        private static bool TryPlaceLargeMountain(
            MapData map,
            IRandom rng,
            MapGenerationConfig config,
            List<Vector2> placedCenters,
            List<float> placedExtents)
        {
            HeightStampShape shape = config.LargeMountainShape;
            IReadOnlyList<Vector2> candidates = config.LargeMountainCandidatePositionsNormalized;
            if (shape == null || candidates == null || candidates.Count == 0) return false;

            Vector2 normalized = candidates[rng.NextInt(0, candidates.Count)];
            Vector2 center = HeightStampPlacementUtility.NormalizedToWorld(normalized, config.WorldSize);
            Vector2 scale = CreateRandomScale(rng);
            float rotation = CreateRotation(rng, shape, center, config.WorldSize);
            var placement = new StampPlacement(center, rotation, scale);
            float extent = HeightStampPlacementUtility.ComputeExtent(shape, placement.Scale);

            ApplyMountain(map, MountainKind.Large, shape, placement, extent, placedCenters, placedExtents);
            return true;
        }

        private static StampPlacement CreateRandomPlacement(
            IRandom rng, HeightStampShape shape, float worldSize, float margin)
        {
            float minCenter = margin;
            float maxCenter = worldSize - margin;
            if (maxCenter <= minCenter)
            {
                minCenter = maxCenter = worldSize * 0.5f;
            }

            Vector2 center = new Vector2(
                Mathf.Lerp(minCenter, maxCenter, rng.NextFloat()),
                Mathf.Lerp(minCenter, maxCenter, rng.NextFloat()));
            Vector2 scale = CreateRandomScale(rng);
            float rotation = CreateRotation(rng, shape, center, worldSize);

            return new StampPlacement(center, rotation, scale);
        }

        private static Vector2 CreateRandomScale(IRandom rng)
        {
            return new Vector2(
                Mathf.Lerp(0.7f, 1.3f, rng.NextFloat()),
                Mathf.Lerp(0.7f, 1.3f, rng.NextFloat()));
        }

        private static float CreateRotation(IRandom rng, HeightStampShape shape, Vector2 center, float worldSize)
        {
            if (shape == null || shape.CliffArcDeg <= 0f || shape.Kind == HeightShapeKind.Ridge)
                return rng.NextFloat() * Mathf.PI * 2f;

            Vector2 toMapCenter = new Vector2(worldSize * 0.5f, worldSize * 0.5f) - center;
            if (toMapCenter.sqrMagnitude < 0.0001f)
                return rng.NextFloat() * Mathf.PI * 2f;

            float targetDirectionRad = Mathf.Atan2(toMapCenter.y, toMapCenter.x);
            float jitterRad = Mathf.Lerp(-CliffDirectionJitterDeg, CliffDirectionJitterDeg, rng.NextFloat()) * Mathf.Deg2Rad;
            return targetDirectionRad + jitterRad - shape.CliffDirectionDeg * Mathf.Deg2Rad;
        }

        private static void ApplyMountain(
            MapData map,
            MountainKind kind,
            HeightStampShape shape,
            StampPlacement placement,
            float extent,
            List<Vector2> placedCenters,
            List<float> placedExtents)
        {
            MountainApplyUtility.Apply(map, kind, shape, placement);
            placedCenters.Add(placement.Center);
            placedExtents.Add(extent);
        }
    }
}
