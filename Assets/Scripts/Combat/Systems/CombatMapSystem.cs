using UnityEngine;
using WarSimulation.Combat.Map;

public readonly struct TerrainInfo
{
    public Vector3 SurfaceNormal { get; }
    public Vector2Int Cell { get; }
    public float Height { get; }
    public GroundState GroundState { get; }
    public bool IsWater { get; }
    public bool IsCliffFace { get; }
    public float SlopeDeg { get; }
    public bool IsForest { get; }
    public bool IsFrozenLake { get; }
    public bool IsInBounds { get; }
    public string BiomeId { get; }

    public TerrainInfo(
        Vector3 surfaceNormal,
        Vector2Int cell,
        float height,
        GroundState groundState,
        bool isCliffFace,
        float slopeDeg,
        bool isForest,
        bool isFrozenLake,
        bool isInBounds,
        string biomeId)
    {
        SurfaceNormal = surfaceNormal;
        Cell = cell;
        Height = height;
        GroundState = groundState;
        IsWater = groundState == GroundState.Water;
        IsCliffFace = isCliffFace;
        SlopeDeg = slopeDeg;
        IsForest = isForest;
        IsFrozenLake = isFrozenLake;
        IsInBounds = isInBounds;
        BiomeId = biomeId;
    }
}

public readonly struct TerrainTraversalInfo
{
    public TerrainInfo TerrainInfo { get; }
    public bool CanStand { get; }
    public float MoveSpeedMultiplier { get; }
    public string BlockedReason { get; }

    public GroundState GroundState => TerrainInfo.GroundState;
    public bool IsFrozenLake => TerrainInfo.IsFrozenLake;
    public bool IsInBounds => TerrainInfo.IsInBounds;
    public bool IsCliffFace => TerrainInfo.IsCliffFace;
    public float SlopeDeg => TerrainInfo.SlopeDeg;

    public TerrainTraversalInfo(
        TerrainInfo terrainInfo,
        bool canStand,
        float moveSpeedMultiplier,
        string blockedReason)
    {
        TerrainInfo = terrainInfo;
        CanStand = canStand;
        MoveSpeedMultiplier = moveSpeedMultiplier;
        BlockedReason = blockedReason;
    }
}

public class CombatMapSystem : MonoBehaviour
{
    [SerializeField] private MapGenerator _mapGenerator;
    [SerializeField] private bool _generateMapOnStart = true;
    [SerializeField] private bool _renderGeneratedMapOnStart = true;

    [Header("Traversal")]
    [SerializeField, Min(0f)] private float _normalSpeedMultiplier = 1f;
    [SerializeField, Min(0f)] private float _snowSpeedMultiplier = 0.75f;
    [SerializeField, Min(0f)] private float _swampSpeedMultiplier = 0.6f;
    [SerializeField, Min(0f)] private float _waterSpeedMultiplier = 0.25f;
    [SerializeField, Min(0f)] private float _frozenLakeSpeedMultiplier = 0.9f;

    public MapData CurrentMap { get; private set; }
    public float MinimumTerrainHeight { get; private set; }
    public float MaximumTerrainHeight { get; private set; }

    private TerrainData _cachedTerrainData;
    private float _cachedTerrainMinimumHeight;
    private float _cachedTerrainMaximumHeight;

    // 天気
    public enum Weather { Sunny, Rainy, Hot, Cold, Thunder }
    public Weather CurrentWeather { private set; get; }

    // 風のベクトル（向きと強さの両方を持つ）
    public Vector3 WindVector { private set; get; }

    public Transform MapOrigin => _mapGenerator != null ? _mapGenerator.transform : transform;

    private void Start()
    {
        if (!_generateMapOnStart || CurrentMap != null) return;

        GenerateAndSetCurrentMap(_renderGeneratedMapOnStart);
    }

    public void SetCurrentMap(MapData map)
    {
        CurrentMap = map;
        UpdateTerrainHeightRange(map);
        InitializeMagicStoneSystem(map);
    }

    private void UpdateTerrainHeightRange(MapData map)
    {
        MinimumTerrainHeight = 0f;
        MaximumTerrainHeight = 0f;
        if (map == null) return;

        HeightMap heightMap = map.Height;
        MinimumTerrainHeight = float.PositiveInfinity;
        MaximumTerrainHeight = float.NegativeInfinity;
        for (int z = 0; z < heightMap.Height; z++)
        {
            for (int x = 0; x < heightMap.Width; x++)
            {
                float height = heightMap.GetHeight(x, z);
                MinimumTerrainHeight = Mathf.Min(MinimumTerrainHeight, height);
                MaximumTerrainHeight = Mathf.Max(MaximumTerrainHeight, height);
            }
        }
    }

    public bool TryGetSightHeightContext(
        Vector3 worldPosition,
        out float currentHeight,
        out float minimumHeight,
        out float maximumHeight)
    {
        currentHeight = 0f;
        minimumHeight = 0f;
        maximumHeight = 0f;

        if (CurrentMap != null && TryGetTerrainInfo(worldPosition, out TerrainInfo terrainInfo))
        {
            currentHeight = terrainInfo.Height;
            minimumHeight = MinimumTerrainHeight;
            maximumHeight = MaximumTerrainHeight;
            return true;
        }

        Terrain terrain = Terrain.activeTerrain != null
            ? Terrain.activeTerrain
            : FindAnyObjectByType<Terrain>();
        TerrainData terrainData = terrain != null ? terrain.terrainData : null;
        if (terrainData == null) return false;

        CacheTerrainHeightRange(terrainData);
        Vector3 localPosition = terrain.transform.InverseTransformPoint(worldPosition);
        Vector3 terrainSize = terrainData.size;
        float normalizedX = terrainSize.x > Mathf.Epsilon ? Mathf.Clamp01(localPosition.x / terrainSize.x) : 0f;
        float normalizedZ = terrainSize.z > Mathf.Epsilon ? Mathf.Clamp01(localPosition.z / terrainSize.z) : 0f;
        currentHeight = terrainData.GetInterpolatedHeight(normalizedX, normalizedZ);
        minimumHeight = _cachedTerrainMinimumHeight;
        maximumHeight = _cachedTerrainMaximumHeight;
        return true;
    }

    private void CacheTerrainHeightRange(TerrainData terrainData)
    {
        if (_cachedTerrainData == terrainData) return;

        float[,] heights = terrainData.GetHeights(0, 0, terrainData.heightmapResolution, terrainData.heightmapResolution);
        float minimumNormalizedHeight = float.PositiveInfinity;
        float maximumNormalizedHeight = float.NegativeInfinity;
        for (int z = 0; z < heights.GetLength(0); z++)
        {
            for (int x = 0; x < heights.GetLength(1); x++)
            {
                float height = heights[z, x];
                minimumNormalizedHeight = Mathf.Min(minimumNormalizedHeight, height);
                maximumNormalizedHeight = Mathf.Max(maximumNormalizedHeight, height);
            }
        }

        _cachedTerrainData = terrainData;
        _cachedTerrainMinimumHeight = minimumNormalizedHeight * terrainData.size.y;
        _cachedTerrainMaximumHeight = maximumNormalizedHeight * terrainData.size.y;
    }

    public MapData GenerateAndSetCurrentMap()
    {
        return GenerateAndSetCurrentMap(render3D: false);
    }

    public MapData GenerateAndSetCurrentMap(bool render3D)
    {
        if (_mapGenerator == null)
        {
            Debug.LogWarning($"[{nameof(CombatMapSystem)}] MapGenerator is not assigned.");
            SetCurrentMap(null);
            return null;
        }

        MapData map = _mapGenerator.Generate();
        SetCurrentMap(map);
        if (render3D && map != null)
        {
            _mapGenerator.Render3D(map);
        }

        return map;
    }

    private void InitializeMagicStoneSystem(MapData map)
    {
        CombatMagicStoneSystem stoneSystem = ResolveMagicStoneSystem();
        stoneSystem?.Initialize(map);
    }

    private CombatMagicStoneSystem ResolveMagicStoneSystem()
    {
        return CombatMagicStoneSystemResolver.Resolve();
    }

    public TerrainInfo GetTerrainInfo(Vector3 worldPosition)
    {
        TryGetTerrainInfo(worldPosition, out TerrainInfo info);
        return info;
    }

    /// <summary>
    /// マップ原点ローカル XZ（<see cref="MapData"/> / <see cref="HeightMap"/> 座標系）を地表のワールド座標へ変換する。
    /// </summary>
    public Vector3 MapLocalToSurfaceWorldPosition(Vector3 mapLocalPosition)
    {
        MapData map = CurrentMap;
        float height = 0f;
        if (map != null)
        {
            height = map.Height.SampleAt(new Vector3(mapLocalPosition.x, 0f, mapLocalPosition.z));
        }

        mapLocalPosition.y = height;
        Transform origin = MapOrigin;
        return origin != null ? origin.TransformPoint(mapLocalPosition) : mapLocalPosition;
    }

    public bool TryGetTerrainInfo(Vector3 worldPosition, out TerrainInfo info)
    {
        info = default;
        MapData map = CurrentMap;
        if (map == null) return false;

        Transform origin = MapOrigin;
        Vector3 localInput = origin.InverseTransformPoint(worldPosition);
        Vector3 sampleLocal = new Vector3(localInput.x, 0f, localInput.z);

        GroundStateGrid groundStates = map.GroundStates;
        Vector2Int cell = groundStates.WorldToCell(sampleLocal);
        bool isInBounds = IsInMapBounds(sampleLocal, groundStates);

        float height = map.Height.SampleAt(sampleLocal);
        Vector3 mapLocalSurfaceNormal = map.Height.SampleNormal(sampleLocal);
        Vector3 surfaceNormal = origin.TransformDirection(mapLocalSurfaceNormal).normalized;

        GroundState groundState = groundStates.SampleAt(sampleLocal);
        bool isFrozenLake = groundState == GroundState.Water &&
            FrozenLakeQueries.IsFrozenLakeWaterAt(map, sampleLocal.x, sampleLocal.z);

        info = new TerrainInfo(
            surfaceNormal,
            cell,
            height,
            groundState,
            map.Height.SampleCliffFace(sampleLocal),
            map.Height.SampleSlopeDeg(sampleLocal),
            IsInsideAnyForest(map, sampleLocal.x, sampleLocal.z),
            isFrozenLake,
            isInBounds,
            map.GetBiomeId(cell.x, cell.y));
        return true;
    }

    public bool CanStandAt(Vector3 worldPosition)
    {
        return TryGetTraversalInfo(worldPosition, out TerrainTraversalInfo info) && info.CanStand;
    }

    public bool TryGetTraversalInfo(Vector3 worldPosition, out TerrainTraversalInfo info)
    {
        info = default;
        if (!TryGetTerrainInfo(worldPosition, out TerrainInfo terrainInfo)) return false;

        bool canStand = GetCanStand(terrainInfo, out string blockedReason);
        float speedMultiplier = canStand
            ? GetMoveSpeedMultiplier(terrainInfo, GetMapLocalPosition(worldPosition))
            : 0f;
        info = new TerrainTraversalInfo(terrainInfo, canStand, speedMultiplier, blockedReason);
        return true;
    }

    public bool SetGroundState(Vector2Int cell, GroundState state)
    {
        if (!IsValidCell(cell)) return false;
        CurrentMap.GroundStates.SetCell(cell.x, cell.y, state);
        return true;
    }

    public bool SetBiomeId(Vector2Int cell, string biomeId)
    {
        if (!IsValidCell(cell)) return false;
        CurrentMap.SetBiomeId(cell.x, cell.y, biomeId);
        return true;
    }

    public bool ClearBiomeId(Vector2Int cell)
    {
        return SetBiomeId(cell, MapData.UnsetBiomeId);
    }

    private bool IsValidCell(Vector2Int cell)
    {
        MapData map = CurrentMap;
        return map != null && map.GroundStates.IsInBounds(cell.x, cell.y);
    }

    private bool GetCanStand(TerrainInfo info, out string blockedReason)
    {
        if (!info.IsInBounds)
        {
            blockedReason = "OutOfBounds";
            return false;
        }

        blockedReason = "";
        return true;
    }

    private float GetMoveSpeedMultiplier(TerrainInfo info, Vector3 mapLocalPosition)
    {
        MapData map = CurrentMap;
        if (map != null &&
            BridgePlacementUtility.IsNearAnyBridge(
                map,
                new Vector2(mapLocalPosition.x, mapLocalPosition.z),
                map.BridgeFeatureExclusionMargin))
        {
            return _normalSpeedMultiplier;
        }

        if (info.IsFrozenLake) return _frozenLakeSpeedMultiplier;

        return info.GroundState switch
        {
            GroundState.Snow => _snowSpeedMultiplier,
            GroundState.Swamp => _swampSpeedMultiplier,
            GroundState.Water => _waterSpeedMultiplier,
            _ => _normalSpeedMultiplier,
        };
    }

    private Vector3 GetMapLocalPosition(Vector3 worldPosition)
    {
        Transform origin = MapOrigin;
        Vector3 localInput = origin.InverseTransformPoint(worldPosition);
        return new Vector3(localInput.x, 0f, localInput.z);
    }

    private static bool IsInMapBounds(Vector3 mapLocalPosition, GroundStateGrid grid)
    {
        Vector2 size = grid.WorldSize;
        return mapLocalPosition.x >= 0f &&
            mapLocalPosition.z >= 0f &&
            mapLocalPosition.x <= size.x &&
            mapLocalPosition.z <= size.y;
    }

    private static bool IsInsideAnyForest(MapData map, float x, float z)
    {
        System.Collections.Generic.List<ForestRegion> regions = map.ForestRegions;
        if (regions == null || regions.Count == 0) return false;

        Vector2 p = new Vector2(x, z);
        for (int i = 0; i < regions.Count; i++)
        {
            if (regions[i].Contains(p)) return true;
        }
        return false;
    }
}
