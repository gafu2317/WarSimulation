using System.Collections.Generic;
using UnityEngine;

namespace WarSimulation.Combat.Map
{
    /// <summary>
    /// 特殊地形の接地や輪郭を壊さないため、保護セルを避けて平地へ決定的な低周波ノイズを一度だけ加える。
    /// </summary>
    public static class PlainReliefUtility
    {
        public static void Apply(MapData map, float amplitude, float frequency, int buildSeed)
        {
            if (map == null || amplitude <= 0f || frequency <= 0f) return;

            HeightMap height = map.Height;
            bool[,] protectedCells = BuildProtectedCells(map);
            int[,] distances = BuildProtectedDistances(protectedCells);
            var rng = new SystemRandom(buildSeed);
            float noiseOffsetX = rng.NextFloat() * 1000f;
            float noiseOffsetZ = rng.NextFloat() * 1000f;
            float fadeDistance = 1f / frequency;

            for (int z = 0; z < height.Height; z++)
            {
                for (int x = 0; x < height.Width; x++)
                {
                    int distance = distances[x, z];
                    if (distance == 0) continue;

                    float weight = distance < 0
                        ? 1f
                        : Mathf.SmoothStep(
                            0f,
                            1f,
                            Mathf.Clamp01(distance * height.CellSize / fadeDistance));
                    if (weight <= 0f) continue;

                    float worldX = (x + 0.5f) * height.CellSize;
                    float worldZ = (z + 0.5f) * height.CellSize;
                    float noise = (Mathf.PerlinNoise(
                        worldX * frequency + noiseOffsetX,
                        worldZ * frequency + noiseOffsetZ) - 0.5f) * 2f;
                    height.SetHeight(
                        x,
                        z,
                        height.GetHeight(x, z) + noise * amplitude * weight);
                }
            }
        }

        private static bool[,] BuildProtectedCells(MapData map)
        {
            HeightMap height = map.Height;
            bool[,] protectedCells = new bool[height.Width, height.Height];
            for (int z = 0; z < height.Height; z++)
            {
                for (int x = 0; x < height.Width; x++)
                {
                    protectedCells[x, z] = !map.GroundStates.IsInBounds(x, z) ||
                        map.GroundStates.GetCell(x, z) != GroundState.Normal ||
                        height.IsCliffFaceCell(x, z);
                }
            }

            for (int i = 0; i < map.Rivers.Count; i++)
                MarkRiverCorridor(height, map.Rivers[i], protectedCells);

            for (int i = 0; i < map.Lakes.Count; i++)
                MarkLake(height, map.Lakes[i], protectedCells);

            for (int i = 0; i < map.Mountains.Count; i++)
                MarkMountain(height, map.Mountains[i], protectedCells);

            for (int i = 0; i < map.ForestRegions.Count; i++)
                MarkForest(height, map.ForestRegions[i], protectedCells);

            for (int i = 0; i < map.Features.Count; i++)
            {
                PlacedFeature feature = map.Features[i];
                if (feature.Type == FeatureType.Bridge)
                    MarkBridge(height, feature, protectedCells);
            }

            return protectedCells;
        }

        private static void MarkRiverCorridor(
            HeightMap height,
            RiverPath river,
            bool[,] protectedCells)
        {
            IReadOnlyList<Vector2Int> cells = river.Cells;
            if (cells == null || cells.Count < 2) return;

            float radius = Mathf.Max(0f, river.WidthMeters * 0.5f);
            if (radius <= 0f) return;
            float radiusSqr = radius * radius;
            float cellSize = height.CellSize;
            for (int i = 0; i < cells.Count - 1; i++)
            {
                Vector2 a = CellCenter(height, cells[i]);
                Vector2 b = CellCenter(height, cells[i + 1]);
                int x0 = Mathf.Max(0, Mathf.FloorToInt(
                    (Mathf.Min(a.x, b.x) - radius) / cellSize) - 1);
                int x1 = Mathf.Min(height.Width - 1, Mathf.CeilToInt(
                    (Mathf.Max(a.x, b.x) + radius) / cellSize) + 1);
                int z0 = Mathf.Max(0, Mathf.FloorToInt(
                    (Mathf.Min(a.y, b.y) - radius) / cellSize) - 1);
                int z1 = Mathf.Min(height.Height - 1, Mathf.CeilToInt(
                    (Mathf.Max(a.y, b.y) + radius) / cellSize) + 1);
                for (int z = z0; z <= z1; z++)
                {
                    for (int x = x0; x <= x1; x++)
                    {
                        if (RiverCorridorUtility.DistanceSqPointToSegment(
                                CellCenter(height, x, z), a, b) <= radiusSqr)
                            protectedCells[x, z] = true;
                    }
                }
            }
        }

        private static void MarkLake(
            HeightMap height,
            LakeRegion lake,
            bool[,] protectedCells)
        {
            MarkCircleRegion(
                height,
                lake.Center,
                lake.OuterRadius,
                protectedCells,
                position => lake.ContainsCarve(position));
        }

        private static void MarkMountain(
            HeightMap height,
            MountainRegion mountain,
            bool[,] protectedCells)
        {
            float radius = Mathf.Max(0f, mountain.Extent);
            MarkCircleRegion(
                height,
                mountain.Center,
                radius,
                protectedCells,
                position => (position - mountain.Center).sqrMagnitude <= radius * radius);
        }

        private static void MarkForest(
            HeightMap height,
            ForestRegion forest,
            bool[,] protectedCells)
        {
            MarkCircleRegion(
                height,
                forest.Center,
                forest.OuterRadius,
                protectedCells,
                forest.Contains);
        }

        private static void MarkBridge(
            HeightMap height,
            PlacedFeature bridge,
            bool[,] protectedCells)
        {
            float halfWidth = Mathf.Max(0f, bridge.Scale.x) * 0.5f;
            float halfLength = Mathf.Max(0f, bridge.Scale.z) * 0.5f;
            float radius = new Vector2(halfWidth, halfLength).magnitude;
            MarkCircleRegion(
                height,
                new Vector2(bridge.WorldPosition.x, bridge.WorldPosition.z),
                radius,
                protectedCells,
                position => BridgePlacementUtility.IsInsideExpandedFootprint(
                    bridge,
                    position,
                    0f));
        }

        private static void MarkCircleRegion(
            HeightMap height,
            Vector2 center,
            float radius,
            bool[,] protectedCells,
            System.Func<Vector2, bool> contains)
        {
            if (radius <= 0f) return;

            float cellSize = height.CellSize;
            int x0 = Mathf.Max(0, Mathf.FloorToInt((center.x - radius) / cellSize) - 1);
            int x1 = Mathf.Min(height.Width - 1, Mathf.CeilToInt((center.x + radius) / cellSize) + 1);
            int z0 = Mathf.Max(0, Mathf.FloorToInt((center.y - radius) / cellSize) - 1);
            int z1 = Mathf.Min(height.Height - 1, Mathf.CeilToInt((center.y + radius) / cellSize) + 1);
            for (int z = z0; z <= z1; z++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    if (contains(CellCenter(height, x, z)))
                        protectedCells[x, z] = true;
                }
            }
        }

        private static int[,] BuildProtectedDistances(bool[,] protectedCells)
        {
            int width = protectedCells.GetLength(0);
            int height = protectedCells.GetLength(1);
            var distances = new int[width, height];
            var queue = new Queue<Vector2Int>();
            for (int z = 0; z < height; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    distances[x, z] = protectedCells[x, z] ? 0 : -1;
                    if (protectedCells[x, z]) queue.Enqueue(new Vector2Int(x, z));
                }
            }

            while (queue.Count > 0)
            {
                Vector2Int cell = queue.Dequeue();
                int nextDistance = distances[cell.x, cell.y] + 1;
                VisitDistance(cell.x - 1, cell.y, nextDistance, distances, queue);
                VisitDistance(cell.x + 1, cell.y, nextDistance, distances, queue);
                VisitDistance(cell.x, cell.y - 1, nextDistance, distances, queue);
                VisitDistance(cell.x, cell.y + 1, nextDistance, distances, queue);
            }

            return distances;
        }

        private static void VisitDistance(
            int x,
            int z,
            int distance,
            int[,] distances,
            Queue<Vector2Int> queue)
        {
            if (x < 0 || x >= distances.GetLength(0) ||
                z < 0 || z >= distances.GetLength(1) || distances[x, z] >= 0)
                return;

            distances[x, z] = distance;
            queue.Enqueue(new Vector2Int(x, z));
        }

        private static Vector2 CellCenter(HeightMap height, Vector2Int cell) =>
            CellCenter(height, cell.x, cell.y);

        private static Vector2 CellCenter(HeightMap height, int x, int z) =>
            new((x + 0.5f) * height.CellSize, (z + 0.5f) * height.CellSize);
    }
}
