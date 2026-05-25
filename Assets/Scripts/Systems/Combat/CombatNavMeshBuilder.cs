using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using WarSimulation.Combat.Map;

[DisallowMultipleComponent]
public sealed class CombatNavMeshBuilder : MonoBehaviour
{
    private const float MinAreaCost = 0.01f;
    private const string AreaVolumeRootName = "GeneratedNavAreaVolumes";
    private const string WalkableAreaName = "Walkable";
    private const string RiverAreaName = "River";
    private const string ForestAreaName = "Forest";
    private const string SnowAreaName = "Snow";
    private const string SwampAreaName = "Swamp";
    private const string LakeAreaName = "Lake";
    private const string FrozenLakeAreaName = "FrozenLake";

    [System.Serializable]
    public sealed class NavMeshAreaCost
    {
        [SerializeField] private string _areaName = "Walkable";
        [SerializeField, Min(MinAreaCost)] private float _cost = 1f;

        public string AreaName => _areaName;
        public float Cost => _cost;

        public NavMeshAreaCost() { }

        public NavMeshAreaCost(string areaName, float cost)
        {
            _areaName = areaName;
            _cost = cost;
        }
    }

    private enum NavAreaKind
    {
        Walkable,
        Forest,
        Snow,
        Swamp,
        River,
        Lake,
        FrozenLake,
    }

    [SerializeField] private NavMeshSurface _surface;
    [SerializeField] private LayerMask _layerMask = ~0;
    [SerializeField] private NavMeshCollectGeometry _geometry = NavMeshCollectGeometry.RenderMeshes;
    [SerializeField] private bool _buildHeightMesh = false;

    [Header("Area Costs")]
    [SerializeField] private NavMeshAreaCost[] _areaCosts =
    {
        new(WalkableAreaName, 1f),
        new(RiverAreaName, 20f),
        new(ForestAreaName, 1f),
        new(SnowAreaName, 1.3f),
        new(SwampAreaName, 1.5f),
        new(LakeAreaName, 20f),
        new(FrozenLakeAreaName, 1f),
    };

    public NavMeshSurface Surface => _surface;

    public bool Build(MapData map)
    {
        if (map == null)
        {
            Debug.LogWarning($"[{nameof(CombatNavMeshBuilder)}] Build called with null MapData.");
            return false;
        }

        EnsureSurface();
        _surface.collectObjects = CollectObjects.Children;
        _surface.layerMask = _layerMask;
        _surface.useGeometry = _geometry;
        _surface.buildHeightMesh = _buildHeightMesh;
        _surface.ignoreNavMeshAgent = true;
        _surface.ignoreNavMeshObstacle = true;
        RebuildAreaVolumes(map);
        _surface.BuildNavMesh();
        ApplyAreaCosts();

        return _surface.navMeshData != null;
    }

    public void Clear()
    {
        if (_surface == null) return;
        _surface.RemoveData();
        _surface.navMeshData = null;
        ClearAreaVolumes();
    }

    private void EnsureSurface()
    {
        if (_surface != null) return;

        _surface = GetComponent<NavMeshSurface>();
        if (_surface == null)
        {
            _surface = gameObject.AddComponent<NavMeshSurface>();
        }
    }

    private void RebuildAreaVolumes(MapData map)
    {
        ClearAreaVolumes();

        Transform root = CreateAreaVolumeRoot();
        NavAreaKind[,] areaGrid = BuildNavAreaGrid(map);
        EmitAreaVolumesFromGrid(map, root, areaGrid);
    }

    private Transform CreateAreaVolumeRoot()
    {
        var root = new GameObject(AreaVolumeRootName);
        root.transform.SetParent(transform, worldPositionStays: false);
        return root.transform;
    }

    private void ClearAreaVolumes()
    {
        Transform existing = transform.Find(AreaVolumeRootName);
        if (existing == null) return;

        GameObject existingGameObject = existing.gameObject;
        if (Application.isPlaying)
        {
            existingGameObject.SetActive(false);
            Destroy(existingGameObject);
        }
        else
        {
            DestroyImmediate(existingGameObject);
        }
    }

    private static NavAreaKind[,] BuildNavAreaGrid(MapData map)
    {
        GroundStateGrid ground = map.GroundStates;
        int width = ground.Width;
        int height = ground.Height;
        var areaGrid = new NavAreaKind[width, height];

        PaintForestAreas(map, areaGrid);
        PaintGroundStateAreas(map, areaGrid);
        PaintRiverAreas(map, areaGrid);
        PaintLakeAreas(map, areaGrid);
        PaintBridgeAreas(map, areaGrid);

        return areaGrid;
    }

    private static void PaintForestAreas(MapData map, NavAreaKind[,] areaGrid)
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
                    areaGrid[x, z] = NavAreaKind.Forest;
                }
            }
        }
    }

    private static void PaintGroundStateAreas(MapData map, NavAreaKind[,] areaGrid)
    {
        GroundStateGrid grid = map.GroundStates;
        for (int z = 0; z < grid.Height; z++)
        {
            for (int x = 0; x < grid.Width; x++)
            {
                GroundState state = grid.GetCell(x, z);
                if (state == GroundState.Snow)
                {
                    areaGrid[x, z] = NavAreaKind.Snow;
                }
                else if (state == GroundState.Swamp)
                {
                    areaGrid[x, z] = NavAreaKind.Swamp;
                }
            }
        }
    }

    private static void PaintRiverAreas(MapData map, NavAreaKind[,] areaGrid)
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
                            areaGrid[x, z] = NavAreaKind.River;
                        }
                    }
                }
            }
        }
    }

    private static void PaintLakeAreas(MapData map, NavAreaKind[,] areaGrid)
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
                        ? NavAreaKind.FrozenLake
                        : NavAreaKind.Lake;
                }
            }
        }
    }

    private static void PaintBridgeAreas(MapData map, NavAreaKind[,] areaGrid)
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
                        areaGrid[x, z] = NavAreaKind.Walkable;
                    }
                }
            }
        }
    }

    private void EmitAreaVolumesFromGrid(MapData map, Transform root, NavAreaKind[,] areaGrid)
    {
        GroundStateGrid grid = map.GroundStates;
        GetVolumeHeight(map, out float centerY, out float height);

        for (int z = 0; z < grid.Height; z++)
        {
            int runStart = -1;
            NavAreaKind runArea = NavAreaKind.Walkable;

            for (int x = 0; x <= grid.Width; x++)
            {
                NavAreaKind area = x < grid.Width ? areaGrid[x, z] : NavAreaKind.Walkable;

                if (runStart < 0)
                {
                    if (area == NavAreaKind.Walkable) continue;
                    runStart = x;
                    runArea = area;
                    continue;
                }

                if (x < grid.Width && area == runArea) continue;

                AddCellRunVolume(
                    root,
                    grid,
                    runStart,
                    x - 1,
                    z,
                    centerY,
                    height,
                    ResolveAreaIndex(GetAreaName(runArea)),
                    GetAreaName(runArea));

                runStart = -1;

                if (area != NavAreaKind.Walkable)
                {
                    runStart = x;
                    runArea = area;
                }
            }
        }
    }

    private static string GetAreaName(NavAreaKind area)
    {
        switch (area)
        {
            case NavAreaKind.Forest:
                return ForestAreaName;
            case NavAreaKind.Snow:
                return SnowAreaName;
            case NavAreaKind.Swamp:
                return SwampAreaName;
            case NavAreaKind.River:
                return RiverAreaName;
            case NavAreaKind.Lake:
                return LakeAreaName;
            case NavAreaKind.FrozenLake:
                return FrozenLakeAreaName;
            default:
                return WalkableAreaName;
        }
    }

    private void AddCellRunVolume(
        Transform root,
        GroundStateGrid grid,
        int startX,
        int endX,
        int z,
        float centerY,
        float height,
        int area,
        string label)
    {
        float cellSize = grid.CellSize;
        float width = (endX - startX + 1) * cellSize;
        Vector3 center = new Vector3(
            (startX + endX + 1) * 0.5f * cellSize,
            centerY,
            (z + 0.5f) * cellSize);
        Vector3 size = new Vector3(width, height, cellSize);
        AddVolume(root, $"{label}_{startX}_{z}_{endX}", center, size, area);
    }

    private static void AddVolume(Transform root, string name, Vector3 center, Vector3 size, int area)
    {
        var go = new GameObject(name);
        go.transform.SetParent(root, worldPositionStays: false);
        var volume = go.AddComponent<NavMeshModifierVolume>();
        volume.center = center;
        volume.size = size;
        volume.area = area;
    }

    private int ResolveAreaIndex(string areaName)
    {
        int area = NavMesh.GetAreaFromName(areaName);
        if (area >= 0) return area;

        int fallback = NavMesh.GetAreaFromName(WalkableAreaName);
        if (fallback < 0) fallback = 0;
        Debug.LogWarning(
            $"[{nameof(CombatNavMeshBuilder)}] NavMesh area '{areaName}' is not defined.",
            this);
        return fallback;
    }

    private void ApplyAreaCosts()
    {
        if (_areaCosts == null) return;

        for (int i = 0; i < _areaCosts.Length; i++)
        {
            NavMeshAreaCost areaCost = _areaCosts[i];
            if (areaCost == null) continue;

            int area = ResolveAreaIndex(areaCost.AreaName);
            NavMesh.SetAreaCost(area, Mathf.Max(MinAreaCost, areaCost.Cost));
        }
    }

    private static void GetVolumeHeight(MapData map, out float centerY, out float height)
    {
        HeightMap heightMap = map.Height;
        float min = float.PositiveInfinity;
        float max = float.NegativeInfinity;

        for (int z = 0; z < heightMap.Height; z++)
        {
            for (int x = 0; x < heightMap.Width; x++)
            {
                float h = heightMap.GetHeight(x, z);
                if (h < min) min = h;
                if (h > max) max = h;
            }
        }

        if (float.IsInfinity(min) || float.IsInfinity(max))
        {
            min = 0f;
            max = 0f;
        }

        height = Mathf.Max(4f, max - min + 4f);
        centerY = (min + max) * 0.5f;
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
