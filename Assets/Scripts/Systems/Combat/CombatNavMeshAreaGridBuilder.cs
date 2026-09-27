using System.Collections.Generic;
using UnityEngine;
using WarSimulation.Combat.Map;

public enum CombatNavAreaKind
{
    Walkable,
    Forest,
    Snow,
    Swamp,
    River,
    Lake,
    FrozenLake,
}

public static class CombatNavMeshAreaGridBuilder
{
    public const string WalkableAreaName = "Walkable";
    public const string RiverAreaName = "River";
    public const string ForestAreaName = "Forest";
    public const string SnowAreaName = "Snow";
    public const string SwampAreaName = "Swamp";
    public const string LakeAreaName = "Lake";
    public const string FrozenLakeAreaName = "FrozenLake";

    public static CombatNavAreaKind[,] Build(MapData map)
    {
        GroundStateGrid ground = map.GroundStates;
        int width = ground.Width;
        int height = ground.Height;
        var areaGrid = new CombatNavAreaKind[width, height];

        PaintForestAreas(map, areaGrid);
        PaintGroundStateAreas(map, areaGrid);
        PaintRiverAreas(map, areaGrid);
        PaintLakeAreas(map, areaGrid);
        PaintBridgeAreas(map, areaGrid);

        return areaGrid;
    }

    public static string GetAreaName(CombatNavAreaKind area)
    {
        switch (area)
        {
            case CombatNavAreaKind.Forest:
                return ForestAreaName;
            case CombatNavAreaKind.Snow:
                return SnowAreaName;
            case CombatNavAreaKind.Swamp:
                return SwampAreaName;
            case CombatNavAreaKind.River:
                return RiverAreaName;
            case CombatNavAreaKind.Lake:
                return LakeAreaName;
            case CombatNavAreaKind.FrozenLake:
                return FrozenLakeAreaName;
            default:
                return WalkableAreaName;
        }
    }

    private static void PaintForestAreas(MapData map, CombatNavAreaKind[,] areaGrid)
    {
        List<ForestRegion> regions = map.ForestRegions;
        if (regions == null || regions.Count == 0) return;

        GroundStateGrid grid = map.GroundStates;
        for (int z = 0; z < grid.Height; z++)
        {
            for (int x = 0; x < grid.Width; x++)
            {
                if (IsInsideAnyForest(regions, GetCellCenter(grid, x, z)))
                {
                    areaGrid[x, z] = CombatNavAreaKind.Forest;
                }
            }
        }
    }

    private static void PaintGroundStateAreas(MapData map, CombatNavAreaKind[,] areaGrid)
    {
        GroundStateGrid grid = map.GroundStates;
        for (int z = 0; z < grid.Height; z++)
        {
            for (int x = 0; x < grid.Width; x++)
            {
                GroundState state = grid.GetCell(x, z);
                if (state == GroundState.Snow)
                {
                    areaGrid[x, z] = CombatNavAreaKind.Snow;
                }
                else if (state == GroundState.Swamp)
                {
                    areaGrid[x, z] = CombatNavAreaKind.Swamp;
                }
            }
        }
    }

    private static void PaintRiverAreas(MapData map, CombatNavAreaKind[,] areaGrid)
    {
        List<RiverPath> rivers = map.Rivers;
        if (rivers == null || rivers.Count == 0) return;

        GroundStateGrid grid = map.GroundStates;

        for (int r = 0; r < rivers.Count; r++)
        {
            RiverPath river = rivers[r];
            IReadOnlyList<Vector2Int> cells = river.Cells;
            if (cells == null || cells.Count < 2) continue;

            float halfWidth = river.WidthMeters * 0.5f;
            float radiusSq = halfWidth * halfWidth;

            for (int i = 0; i < cells.Count - 1; i++)
            {
                Vector2Int c0 = cells[i];
                Vector2Int c1 = cells[i + 1];
                Vector2 a = new((c0.x + 0.5f) * grid.CellSize, (c0.y + 0.5f) * grid.CellSize);
                Vector2 b = new((c1.x + 0.5f) * grid.CellSize, (c1.y + 0.5f) * grid.CellSize);

                float minX = Mathf.Min(a.x, b.x) - halfWidth;
                float maxX = Mathf.Max(a.x, b.x) + halfWidth;
                float minZ = Mathf.Min(a.y, b.y) - halfWidth;
                float maxZ = Mathf.Max(a.y, b.y) + halfWidth;

                int xMin = Mathf.Max(0, Mathf.FloorToInt(minX / grid.CellSize));
                int xMax = Mathf.Min(grid.Width - 1, Mathf.CeilToInt(maxX / grid.CellSize));
                int zMin = Mathf.Max(0, Mathf.FloorToInt(minZ / grid.CellSize));
                int zMax = Mathf.Min(grid.Height - 1, Mathf.CeilToInt(maxZ / grid.CellSize));

                for (int z = zMin; z <= zMax; z++)
                {
                    for (int x = xMin; x <= xMax; x++)
                    {
                        Vector2 center = GetCellCenter(grid, x, z);
                        if (RiverCorridorUtility.DistanceSqPointToSegment(center, a, b) <= radiusSq)
                        {
                            areaGrid[x, z] = CombatNavAreaKind.River;
                        }
                    }
                }
            }
        }
    }

    private static void PaintLakeAreas(MapData map, CombatNavAreaKind[,] areaGrid)
    {
        List<LakeRegion> lakes = map.Lakes;
        if (lakes == null || lakes.Count == 0) return;

        GroundStateGrid grid = map.GroundStates;
        for (int i = 0; i < lakes.Count; i++)
        {
            LakeRegion lake = lakes[i];
            float outer = lake.OuterRadius;
            int centerX = Mathf.FloorToInt(lake.Center.x / grid.CellSize);
            int centerZ = Mathf.FloorToInt(lake.Center.y / grid.CellSize);
            int radius = Mathf.CeilToInt(outer / grid.CellSize);

            int xMin = Mathf.Max(0, centerX - radius);
            int xMax = Mathf.Min(grid.Width - 1, centerX + radius);
            int zMin = Mathf.Max(0, centerZ - radius);
            int zMax = Mathf.Min(grid.Height - 1, centerZ + radius);

            for (int z = zMin; z <= zMax; z++)
            {
                for (int x = xMin; x <= xMax; x++)
                {
                    Vector2 center = GetCellCenter(grid, x, z);
                    if (!lake.ContainsCarve(center)) continue;

                    areaGrid[x, z] = lake.IsFrozen
                        ? CombatNavAreaKind.FrozenLake
                        : CombatNavAreaKind.Lake;
                }
            }
        }
    }

    private static void PaintBridgeAreas(MapData map, CombatNavAreaKind[,] areaGrid)
    {
        List<PlacedFeature> features = map.Features;
        if (features == null || features.Count == 0) return;

        GroundStateGrid grid = map.GroundStates;
        for (int i = 0; i < features.Count; i++)
        {
            PlacedFeature feature = features[i];
            if (feature.Type != FeatureType.Bridge) continue;

            float halfWidth = Mathf.Max(0f, feature.Scale.x) * 0.5f;
            float halfLength = Mathf.Max(0f, feature.Scale.z) * 0.5f;
            if (halfWidth <= 0f || halfLength <= 0f) continue;

            Quaternion invRot = Quaternion.Inverse(feature.Rotation);
            Vector3 center = feature.WorldPosition;
            float maxExtent = Mathf.Sqrt(halfWidth * halfWidth + halfLength * halfLength);

            int xMin = Mathf.Max(0, Mathf.FloorToInt((center.x - maxExtent) / grid.CellSize));
            int xMax = Mathf.Min(grid.Width - 1, Mathf.CeilToInt((center.x + maxExtent) / grid.CellSize));
            int zMin = Mathf.Max(0, Mathf.FloorToInt((center.z - maxExtent) / grid.CellSize));
            int zMax = Mathf.Min(grid.Height - 1, Mathf.CeilToInt((center.z + maxExtent) / grid.CellSize));

            for (int z = zMin; z <= zMax; z++)
            {
                for (int x = xMin; x <= xMax; x++)
                {
                    Vector2 p = GetCellCenter(grid, x, z);
                    Vector3 local = invRot * (new Vector3(p.x, 0f, p.y) - center);
                    if (Mathf.Abs(local.x) <= halfWidth && Mathf.Abs(local.z) <= halfLength)
                    {
                        areaGrid[x, z] = CombatNavAreaKind.Walkable;
                    }
                }
            }
        }
    }

    private static Vector2 GetCellCenter(GroundStateGrid grid, int x, int z)
    {
        return new Vector2((x + 0.5f) * grid.CellSize, (z + 0.5f) * grid.CellSize);
    }

    private static bool IsInsideAnyForest(List<ForestRegion> regions, Vector2 point)
    {
        for (int i = 0; i < regions.Count; i++)
        {
            if (regions[i].Contains(point)) return true;
        }

        return false;
    }
}
