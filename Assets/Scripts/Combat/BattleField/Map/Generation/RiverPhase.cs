using System.Collections.Generic;
using UnityEngine;

namespace WarSimulation.Combat.Map
{
    public sealed class RiverPhase : IMapGenerationPhase
    {
        private const float RetryCenterOffsetFactor = 0.1f;
        private const int AngleSearchDivisions = 24;
        private static readonly float[] MeanderScales = { 1f, 0.7f, 0.4f };

        public void Execute(MapData map, IRandom rng, MapGenerationConfig config)
        {
            if (map == null || rng == null || config == null) return;
            if (config.RiverShape == null) return;
            if (config.CrossMapRiverCount <= 0) return;

            var builder = new FlatRiverPathBuilder();
            for (int i = 0; i < config.CrossMapRiverCount; i++)
            {
                GenerateCenterCrossingRiver(map, rng, config, builder);
            }
        }

        private static void GenerateCenterCrossingRiver(
            MapData map,
            IRandom rng,
            MapGenerationConfig config,
            FlatRiverPathBuilder builder)
        {
            HeightMap height = map.Height;
            List<Vector2Int> centerCandidates = CollectCenterCandidates(map, config);
            if (centerCandidates.Count == 0) return;

            int maxAttempts = Mathf.Max(1, config.RiverMaxPathSearchAttempts);
            List<Vector2Int> centerAttemptOrder = BuildMountainDistantCenterAttemptOrder(
                map,
                centerCandidates,
                maxAttempts,
                config.WorldSize * RetryCenterOffsetFactor);
            List<Vector2Int> bestPath = null;
            float bestScore = float.NegativeInfinity;

            for (int attempt = 0; attempt < centerAttemptOrder.Count; attempt++)
            {
                Vector2Int center = centerAttemptOrder[attempt];
                float angleOffset = rng.NextFloat() * Mathf.PI * 2f;
                for (int angleAttempt = 0; angleAttempt < AngleSearchDivisions; angleAttempt++)
                {
                    float angle = angleOffset + (Mathf.PI * 2f * angleAttempt / AngleSearchDivisions);

                    if (!TryBuildRayToEdge(map, center, angle, config.RiverExistingWaterClearance, out List<Vector2Int> firstRay))
                        continue;

                    float oppositeAngle = angle + Mathf.PI;
                    if (!TryBuildRayToEdge(map, center, oppositeAngle, config.RiverExistingWaterClearance, out List<Vector2Int> secondRay))
                        continue;

                    Vector2Int firstEdge = firstRay[firstRay.Count - 1];
                    Vector2Int secondEdge = secondRay[secondRay.Count - 1];
                    if (firstEdge == secondEdge) continue;

                    float noiseSeed = rng.NextFloat() * 1000f;
                    if (!TryBuildMeanderedPath(
                            map,
                            config,
                            builder,
                            firstEdge,
                            center,
                            secondEdge,
                            noiseSeed,
                            out List<Vector2Int> path))
                        continue;

                    if (PathLengthMeters(path, height) < config.RiverMinPathLengthMeters) continue;
                    if (!IsPathPassable(map, path, config.RiverExistingWaterClearance)) continue;

                    float score = ScorePath(path, height);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestPath = path;
                    }
                }
            }

            if (bestPath == null) return;

            config.RiverShape.Carve(map, bestPath);
            map.AddRiver(new RiverPath(
                bestPath,
                config.RiverShape.WidthMeters,
                config.RiverShape.DepthMeters,
                config.RiverShape.WaterTagRatio));
        }

        private static List<Vector2Int> CollectCenterCandidates(MapData map, MapGenerationConfig config)
        {
            HeightMap h = map.Height;
            float ratio = Mathf.Clamp(config.RiverCenterCandidateAreaRatio, 0.1f, 1f);
            float min = (1f - ratio) * 0.5f * config.WorldSize;
            float max = (1f + ratio) * 0.5f * config.WorldSize;
            float cs = h.CellSize;
            int xMin = Mathf.Clamp(Mathf.FloorToInt(min / cs), 0, h.Width - 1);
            int xMax = Mathf.Clamp(Mathf.CeilToInt(max / cs), 0, h.Width - 1);
            int zMin = Mathf.Clamp(Mathf.FloorToInt(min / cs), 0, h.Height - 1);
            int zMax = Mathf.Clamp(Mathf.CeilToInt(max / cs), 0, h.Height - 1);

            var candidates = new List<Vector2Int>();
            int stride = Mathf.Max(1, Mathf.RoundToInt(1f / cs));
            for (int z = zMin; z <= zMax; z += stride)
            {
                for (int x = xMin; x <= xMax; x += stride)
                {
                    if (HeightZeroCellUtility.IsRiverPassable(map, x, z, config.RiverExistingWaterClearance))
                        candidates.Add(new Vector2Int(x, z));
                }
            }

            return candidates;
        }

        private static List<Vector2Int> BuildMountainDistantCenterAttemptOrder(
            MapData map,
            List<Vector2Int> candidates,
            int maxAttempts,
            float retryCenterOffsetMeters)
        {
            candidates.Sort((a, b) =>
                MountainClearanceScore(map, b).CompareTo(MountainClearanceScore(map, a)));

            int limit = Mathf.Min(maxAttempts, candidates.Count);
            var ordered = new List<Vector2Int>(limit);
            if (limit == 0) return ordered;

            Vector2Int anchor = candidates[0];
            ordered.Add(anchor);

            float minOffsetSq = retryCenterOffsetMeters * retryCenterOffsetMeters;
            HeightMap h = map.Height;
            Vector2 anchorWorld = HeightZeroCellUtility.CellCenter(h, anchor.x, anchor.y);
            for (int i = 1; i < candidates.Count && ordered.Count < limit; i++)
            {
                Vector2 candidateWorld = HeightZeroCellUtility.CellCenter(h, candidates[i].x, candidates[i].y);
                if ((candidateWorld - anchorWorld).sqrMagnitude < minOffsetSq) continue;
                ordered.Add(candidates[i]);
            }

            for (int i = 1; i < candidates.Count && ordered.Count < limit; i++)
            {
                if (!ordered.Contains(candidates[i]))
                    ordered.Add(candidates[i]);
            }

            return ordered;
        }

        private static float MountainClearanceScore(MapData map, Vector2Int cell)
        {
            IReadOnlyList<MountainRegion> mountains = map.Mountains;
            if (mountains == null || mountains.Count == 0)
                return 0f;

            Vector2 world = HeightZeroCellUtility.CellCenter(map.Height, cell.x, cell.y);
            float best = float.PositiveInfinity;
            for (int i = 0; i < mountains.Count; i++)
            {
                MountainRegion mountain = mountains[i];
                float clearance = Vector2.Distance(world, mountain.Center) - mountain.Extent;
                if (clearance < best)
                    best = clearance;
            }

            return best;
        }

        private static bool TryBuildRayToEdge(
            MapData map,
            Vector2Int center,
            float angle,
            float existingWaterClearance,
            out List<Vector2Int> path)
        {
            path = null;
            HeightMap h = map.Height;
            Vector2 startWorld = HeightZeroCellUtility.CellCenter(h, center.x, center.y);
            Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            if (dir.sqrMagnitude < 0.0001f) return false;

            float t = FindBoundaryDistance(startWorld, dir, h.WorldSize);
            if (t <= h.CellSize) return false;

            Vector2 endWorld = startWorld + dir * t;
            Vector2Int endCell = WorldToCell(h, endWorld);
            if (!IsPerimeterCell(h, endCell)) return false;

            var line = BuildLine(center, endCell);
            if (line.Count < 2) return false;
            if (!IsPathPassable(map, line, existingWaterClearance)) return false;

            path = line;
            return true;
        }

        private static float FindBoundaryDistance(Vector2 start, Vector2 dir, Vector2 worldSize)
        {
            float best = float.PositiveInfinity;
            if (dir.x > 0.0001f) best = Mathf.Min(best, (worldSize.x - 0.001f - start.x) / dir.x);
            if (dir.x < -0.0001f) best = Mathf.Min(best, (0.001f - start.x) / dir.x);
            if (dir.y > 0.0001f) best = Mathf.Min(best, (worldSize.y - 0.001f - start.y) / dir.y);
            if (dir.y < -0.0001f) best = Mathf.Min(best, (0.001f - start.y) / dir.y);
            return best;
        }

        private static bool TryBuildMeanderedPath(
            MapData map,
            MapGenerationConfig config,
            FlatRiverPathBuilder builder,
            Vector2Int firstEdge,
            Vector2Int center,
            Vector2Int secondEdge,
            float noiseSeed,
            out List<Vector2Int> path)
        {
            path = null;
            for (int i = 0; i < MeanderScales.Length; i++)
            {
                float scale = MeanderScales[i];
                List<Vector2Int> first = builder.Build(
                    map.Height,
                    firstEdge,
                    center,
                    config.FlatRiverMeanderAmplitude * scale,
                    config.FlatRiverMeanderFrequency,
                    noiseSeed,
                    config.FlatRiverSpineCurveBend * scale);
                if (!IsPathPassable(map, first, config.RiverExistingWaterClearance)) continue;

                List<Vector2Int> second = builder.Build(
                    map.Height,
                    center,
                    secondEdge,
                    config.FlatRiverMeanderAmplitude * scale,
                    config.FlatRiverMeanderFrequency,
                    noiseSeed + 31.7f,
                    config.FlatRiverSpineCurveBend * scale);
                if (!IsPathPassable(map, second, config.RiverExistingWaterClearance)) continue;

                path = CombinePaths(first, second);
                return true;
            }

            return false;
        }

        private static List<Vector2Int> CombinePaths(List<Vector2Int> first, List<Vector2Int> second)
        {
            var combined = new List<Vector2Int>(first.Count + second.Count);
            combined.AddRange(first);
            for (int i = 1; i < second.Count; i++)
                combined.Add(second[i]);
            return combined;
        }

        private static bool IsPathPassable(MapData map, IReadOnlyList<Vector2Int> path, float existingWaterClearance)
        {
            if (path == null || path.Count < 2) return false;
            for (int i = 0; i < path.Count; i++)
            {
                Vector2Int c = path[i];
                if (!HeightZeroCellUtility.IsRiverPassable(map, c.x, c.y, existingWaterClearance))
                    return false;
            }
            return true;
        }

        private static float PathLengthMeters(IReadOnlyList<Vector2Int> path, HeightMap h)
        {
            if (path == null || path.Count < 2) return 0f;

            float lengthCells = 0f;
            for (int i = 1; i < path.Count; i++)
            {
                int dx = Mathf.Abs(path[i].x - path[i - 1].x);
                int dz = Mathf.Abs(path[i].y - path[i - 1].y);
                lengthCells += dx != 0 && dz != 0 ? 1.4142135f : 1f;
            }

            return lengthCells * h.CellSize;
        }

        private static float ScorePath(IReadOnlyList<Vector2Int> path, HeightMap h)
        {
            float length = PathLengthMeters(path, h);
            float edgePenalty = 0f;
            for (int i = 1; i < path.Count - 1; i++)
            {
                if (IsPerimeterCell(h, path[i]))
                    edgePenalty += 5f;
            }
            return length - edgePenalty;
        }

        private static List<Vector2Int> BuildLine(Vector2Int from, Vector2Int to)
        {
            var path = new List<Vector2Int>();
            int dx = Mathf.Abs(to.x - from.x);
            int dy = Mathf.Abs(to.y - from.y);
            int sx = from.x < to.x ? 1 : -1;
            int sy = from.y < to.y ? 1 : -1;
            int err = dx - dy;
            int x = from.x;
            int y = from.y;

            while (true)
            {
                var cell = new Vector2Int(x, y);
                if (path.Count == 0 || path[path.Count - 1] != cell)
                    path.Add(cell);
                if (x == to.x && y == to.y) break;

                int e2 = err * 2;
                if (e2 > -dy) { err -= dy; x += sx; }
                if (e2 < dx) { err += dx; y += sy; }
            }

            return path;
        }

        private static Vector2Int WorldToCell(HeightMap h, Vector2 world)
        {
            return new Vector2Int(
                Mathf.Clamp(Mathf.FloorToInt(world.x / h.CellSize), 0, h.Width - 1),
                Mathf.Clamp(Mathf.FloorToInt(world.y / h.CellSize), 0, h.Height - 1));
        }

        private static bool IsPerimeterCell(HeightMap h, Vector2Int cell)
        {
            return cell.x == 0 || cell.y == 0 || cell.x == h.Width - 1 || cell.y == h.Height - 1;
        }
    }
}
