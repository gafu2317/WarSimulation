using System;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using Unity.Profiling;
using WarSimulation.Combat.Map;

[DisallowMultipleComponent]
public sealed class CombatNavMeshBuilder : MonoBehaviour
{
    private static readonly ProfilerMarker BuildMarker = new("CombatLoading.NavMeshBuild");
    private static readonly ProfilerMarker LoadMarker = new("CombatLoading.NavMeshLoad");
    private static readonly ProfilerMarker ClearMarker = new("CombatLoading.NavMeshClear");

    /// <summary>Render3D 後など、現行マップの NavMesh がベイク完了したときに発火する。</summary>
    public static event Action Built;

    /// <summary>NavMesh データが破棄されたときに発火する。</summary>
    public static event Action Cleared;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetEventsForPlay()
    {
        Built = null;
        Cleared = null;
    }

    private const float MinRelativeAreaCost = 0.01f;
    private const float MinNavMeshAreaCost = 1f;
    private const string AreaVolumeRootName = "GeneratedNavAreaVolumes";
    private const string WalkableAreaName = CombatNavMeshAreaGridBuilder.WalkableAreaName;
    private const string RiverAreaName = CombatNavMeshAreaGridBuilder.RiverAreaName;
    private const string ForestAreaName = CombatNavMeshAreaGridBuilder.ForestAreaName;
    private const string SnowAreaName = CombatNavMeshAreaGridBuilder.SnowAreaName;
    private const string SwampAreaName = CombatNavMeshAreaGridBuilder.SwampAreaName;
    private const string LakeAreaName = CombatNavMeshAreaGridBuilder.LakeAreaName;
    private const string FrozenLakeAreaName = CombatNavMeshAreaGridBuilder.FrozenLakeAreaName;

    [System.Serializable]
    public sealed class NavMeshAreaCost
    {
        [SerializeField] private string _areaName = "Walkable";
        [SerializeField, Min(MinRelativeAreaCost)] private float _cost = 1f;

        public string AreaName => _areaName;
        public float Cost => _cost;

        public NavMeshAreaCost() { }

        public NavMeshAreaCost(string areaName, float cost)
        {
            _areaName = areaName;
            _cost = cost;
        }
    }

    [SerializeField] private NavMeshSurface _surface;
    [SerializeField] private LayerMask _layerMask = ~0;
    [SerializeField] private NavMeshCollectGeometry _geometry = NavMeshCollectGeometry.RenderMeshes;
    [SerializeField] private bool _buildHeightMesh = false;

    [Header("Area Costs")]
    [SerializeField] private NavMeshAreaCost[] _areaCosts =
    {
        new(WalkableAreaName, 2f),
        new(RiverAreaName, 40f),
        new(ForestAreaName, 1f),
        new(SnowAreaName, 3f),
        new(SwampAreaName, 3f),
        new(LakeAreaName, 1998f),
        new(FrozenLakeAreaName, 2f),
    };

    public NavMeshSurface Surface => _surface;

    public bool Build(MapData map)
    {
        using var _ = BuildMarker.Auto();
        if (map == null)
        {
            Debug.LogWarning($"[{nameof(CombatNavMeshBuilder)}] Build called with null MapData.");
            return false;
        }

        EnsureSurface();
        ConfigureSurface();
        RebuildAreaVolumes(map);
        _surface.BuildNavMesh();
        ApplyAreaCosts();

        bool built = _surface.navMeshData != null;
        if (built)
        {
            Built?.Invoke();
        }

        return built;
    }

    /// <summary>
    /// Editor 事前ベイク済みの NavMeshData を載せる。Area volume 再生成と BuildNavMesh は行わない。
    /// </summary>
    public bool Load(NavMeshData bakedNavMesh)
    {
        using var _ = LoadMarker.Auto();
        if (bakedNavMesh == null)
        {
            Debug.LogWarning($"[{nameof(CombatNavMeshBuilder)}] Load called with null NavMeshData.");
            return false;
        }

        EnsureSurface();
        ConfigureSurface();
        ClearAreaVolumes();
        _surface.RemoveData();
        _surface.navMeshData = bakedNavMesh;
        _surface.AddData();
        ApplyAreaCosts();

        bool loaded = _surface.navMeshData != null;
        if (loaded)
        {
            Built?.Invoke();
        }

        return loaded;
    }

    public void Clear()
    {
        using var _ = ClearMarker.Auto();
        if (_surface == null) return;
        _surface.RemoveData();
        _surface.navMeshData = null;
        ClearAreaVolumes();
        Cleared?.Invoke();
    }

    private void ConfigureSurface()
    {
        _surface.collectObjects = CollectObjects.Children;
        _surface.layerMask = _layerMask;
        _surface.useGeometry = _geometry;
        _surface.buildHeightMesh = _buildHeightMesh;
        _surface.ignoreNavMeshAgent = true;
        _surface.ignoreNavMeshObstacle = true;
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
        CombatNavAreaKind[,] areaGrid = CombatNavMeshAreaGridBuilder.Build(map);
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

    private void EmitAreaVolumesFromGrid(MapData map, Transform root, CombatNavAreaKind[,] areaGrid)
    {
        GroundStateGrid grid = map.GroundStates;
        GetVolumeHeight(map, out float centerY, out float height);

        for (int z = 0; z < grid.Height; z++)
        {
            int runStart = -1;
            CombatNavAreaKind runArea = CombatNavAreaKind.Walkable;

            for (int x = 0; x <= grid.Width; x++)
            {
                CombatNavAreaKind area = x < grid.Width ? areaGrid[x, z] : CombatNavAreaKind.Walkable;

                if (runStart < 0)
                {
                    if (area == CombatNavAreaKind.Walkable) continue;
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
                    ResolveAreaIndex(CombatNavMeshAreaGridBuilder.GetAreaName(runArea)),
                    CombatNavMeshAreaGridBuilder.GetAreaName(runArea));

                runStart = -1;

                if (area != CombatNavAreaKind.Walkable)
                {
                    runStart = x;
                    runArea = area;
                }
            }
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

        float scale = CalculateAreaCostScale(_areaCosts);
        for (int i = 0; i < _areaCosts.Length; i++)
        {
            NavMeshAreaCost areaCost = _areaCosts[i];
            if (areaCost == null) continue;

            int area = ResolveAreaIndex(areaCost.AreaName);
            NavMesh.SetAreaCost(area, Mathf.Max(MinNavMeshAreaCost, areaCost.Cost * scale));
        }
    }

    private static float CalculateAreaCostScale(NavMeshAreaCost[] areaCosts)
    {
        float minimum = float.PositiveInfinity;
        for (int i = 0; i < areaCosts.Length; i++)
        {
            NavMeshAreaCost areaCost = areaCosts[i];
            if (areaCost != null && areaCost.Cost > 0f)
            {
                minimum = Mathf.Min(minimum, areaCost.Cost);
            }
        }

        return minimum < MinNavMeshAreaCost
            ? MinNavMeshAreaCost / minimum
            : 1f;
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
}
