using UnityEngine;
using WarSimulation.Combat.Map;

public readonly struct TerrainInfo
{
    public Vector3 WorldPosition { get; }
    public Vector3 MapLocalPosition { get; }
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
        Vector3 worldPosition,
        Vector3 mapLocalPosition,
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
        WorldPosition = worldPosition;
        MapLocalPosition = mapLocalPosition;
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

public class CombatMapSystem : MonoBehaviour
{
    [SerializeField] private MapGenerator _mapGenerator;

    public MapData CurrentMap { get; private set; }

    // 天気
    public enum Weather { Sunny, Rainy, Hot, Cold, Thunder }
    public Weather CurrentWeather { private set; get; }

    // 風のベクトル（向きと強さの両方を持つ）
    public Vector3 WindVector { private set; get; }

    public Transform MapOrigin => _mapGenerator != null ? _mapGenerator.transform : transform;

    public void SetCurrentMap(MapData map)
    {
        CurrentMap = map;
    }

    public MapData GenerateAndSetCurrentMap()
    {
        if (_mapGenerator == null)
        {
            Debug.LogWarning($"[{nameof(CombatMapSystem)}] MapGenerator is not assigned.");
            SetCurrentMap(null);
            return null;
        }

        MapData map = _mapGenerator.Generate();
        SetCurrentMap(map);
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
        Vector3 mapLocalPosition = new Vector3(localInput.x, height, localInput.z);
        Vector3 surfaceWorldPosition = origin.TransformPoint(mapLocalPosition);

        GroundState groundState = groundStates.SampleAt(sampleLocal);
        bool isFrozenLake = groundState == GroundState.Water &&
            FrozenLakeQueries.IsFrozenLakeWaterAt(map, sampleLocal.x, sampleLocal.z);

        info = new TerrainInfo(
            surfaceWorldPosition,
            mapLocalPosition,
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
