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

        CombatSceneContext.Instance?.CharacterSystem?.SnapAllCharactersToNavMesh();
    }

    public void SetCurrentMap(MapData map)
    {
        CurrentMap = map;
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

    public TerrainInfo GetTerrainInfo(Vector3 worldPosition)
    {
        TryGetTerrainInfo(worldPosition, out TerrainInfo info);
        return info;
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
