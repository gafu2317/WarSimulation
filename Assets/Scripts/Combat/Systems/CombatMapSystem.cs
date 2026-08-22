using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Profiling;
using UnityEngine.Serialization;
using UnityEngine.SceneManagement;
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

public enum CombatMapApplyFailure
{
    None,
    MissingDefinition,
    MissingSharedConfig,
    MissingBakedMapData,
    MissingBakedNavMesh,
    MissingBakedRenderData,
    MissingMapSceneHost,
    RuntimeMapCreationFailed,
    RenderOrNavMeshLoadFailed,
    MapNotReady,
    MissingBakedRuntimeScene,
    BakedRuntimeSceneLoadFailed,
}

public enum MapPreparationState
{
    Unloaded,
    Loading,
    Ready,
    Failed,
}

public class CombatMapSystem : MonoBehaviour
{
    private static readonly ProfilerMarker PrepareMapMarker =
        new("CombatLoading.PrepareMap");
    private static readonly ProfilerMarker ActivateMapMarker =
        new("CombatLoading.ActivateMap");

    [FormerlySerializedAs("_mapGenerator")]
    [SerializeField] private MapSceneHost _mapSceneHost;
    [FormerlySerializedAs("_generateMapOnStart")]
    [SerializeField] private bool _buildMapOnStart = true;
    [FormerlySerializedAs("_renderGeneratedMapOnStart")]
    [SerializeField] private bool _renderMapOnStart = true;

    [Header("Map")]
    [SerializeField] private AuthoredMapDefinition _authoredMap;

    [Header("Traversal")]
    [SerializeField, Min(0f)] private float _normalSpeedMultiplier = 1f;
    [SerializeField, Min(0f)] private float _snowSpeedMultiplier = 0.75f;
    [SerializeField, Min(0f)] private float _swampSpeedMultiplier = 0.6f;
    [SerializeField, Min(0f)] private float _waterSpeedMultiplier = 0.25f;
    [SerializeField, Min(0f)] private float _frozenLakeSpeedMultiplier = 0.9f;

    public MapData CurrentMap { get; private set; }
    public float MinimumTerrainHeight { get; private set; }
    public float MaximumTerrainHeight { get; private set; }
    public bool IsStonePositionReversed { get; private set; }
    public event Action StonePositionsChanged;
    public event Action CurrentMapChanged;

    private TerrainData _cachedTerrainData;
    private float _cachedTerrainMinimumHeight;
    private float _cachedTerrainMaximumHeight;
    private bool _isNavMeshReady;
    private AuthoredMapDefinition _preparedDefinition;
    private MapData _preparedMap;
    private int _preparedFingerprint;
    private CombatMapApplyFailure _preparationFailure;
    private readonly HashSet<Vector2Int> _dirtyGroundCells = new();
    private readonly HashSet<Vector2Int> _dirtyBiomeCells = new();
    private Scene _loadedRuntimeScene;
    private int _preparationRequestVersion;

    public MapPreparationState PreparationState { get; private set; }
    public CombatMapApplyFailure PreparationFailure => _preparationFailure;

    // 天気
    public enum Weather { Sunny, Rainy, Hot, Cold, Thunder }
    public Weather CurrentWeather { private set; get; }

    // 風のベクトル（向きと強さの両方を持つ）
    public Vector3 WindVector { private set; get; }

    public Transform MapOrigin => _mapSceneHost != null ? _mapSceneHost.transform : transform;

    public AuthoredMapDefinition AuthoredMap => _authoredMap;
    public MapSceneHost SceneHost => _mapSceneHost;

    private void Start()
    {
        if (!_buildMapOnStart || _isNavMeshReady) return;

        if (_renderMapOnStart)
        {
            EnsureMapAndNavMeshInitialized();
        }
        else if (CurrentMap == null)
        {
            LoadBakedMapAndNavMesh();
        }
    }

    public bool EnsureMapAndNavMeshInitialized()
    {
        if (_isNavMeshReady && CurrentMap != null) return true;
        if (!_buildMapOnStart)
        {
            Debug.LogWarning(
                $"[{nameof(CombatMapSystem)}] Cannot initialize the map and NavMesh because map building is disabled.",
                this);
            return false;
        }

        return LoadBakedMapAndNavMesh();
    }

    public bool LoadBakedMapAndNavMesh()
    {
        if (!_buildMapOnStart)
        {
            Debug.LogWarning(
                $"[{nameof(CombatMapSystem)}] Cannot load the baked map because map building is disabled.",
                this);
            return false;
        }

        if (!TryPrepareMap(_authoredMap, out _)) return false;
        return TryActivatePreparedMap(_authoredMap, out _);
    }

    public IEnumerator PrepareMapAsync(AuthoredMapDefinition definition)
    {
        if (IsMapReady(definition)) yield break;
        int requestVersion = ++_preparationRequestVersion;
        PreparationState = MapPreparationState.Loading;
        yield return null;
        CombatMapApplyFailure validationFailure = ValidateBakedDefinition(definition, requireHost: false);
        if (validationFailure != CombatMapApplyFailure.None)
        {
            FailPreparation(validationFailure);
            yield break;
        }

        MapData map = null;
        if (_mapSceneHost != null && TryPrepareMap(definition, out map)) yield break;
        if (!definition.HasValidBakedRuntimeScene)
        {
            FailPreparation(CombatMapApplyFailure.MissingBakedRuntimeScene);
            yield break;
        }

        if (map == null && !TryCreateRuntimeMap(definition, out map)) yield break;

        AsyncOperation load = SceneManager.LoadSceneAsync(
            definition.BakedRuntimeScenePath,
            LoadSceneMode.Additive);
        if (load == null)
        {
            FailPreparation(CombatMapApplyFailure.BakedRuntimeSceneLoadFailed);
            yield break;
        }

        while (!load.isDone) yield return null;
        Scene loadedScene = SceneManager.GetSceneByPath(definition.BakedRuntimeScenePath);
        if (!loadedScene.IsValid() || !loadedScene.isLoaded)
        {
            FailPreparation(CombatMapApplyFailure.BakedRuntimeSceneLoadFailed);
            yield break;
        }

        MapSceneHost loadedHost = FindMapSceneHost(loadedScene);
        int fingerprint = definition.ComputeBakeFingerprint();
        bool staleRequest = requestVersion != _preparationRequestVersion;
        bool validHost = loadedHost != null && loadedHost.HasBakedRenderDataFor(map, fingerprint);
        if (staleRequest || !validHost)
        {
            yield return SceneManager.UnloadSceneAsync(loadedScene);
            if (!staleRequest) FailPreparation(
                loadedHost == null
                    ? CombatMapApplyFailure.MissingMapSceneHost
                    : CombatMapApplyFailure.MissingBakedRenderData);
            yield break;
        }

        loadedHost.Config = definition.SharedConfig;
        if (!loadedHost.LoadBakedMap(map, definition.BakedNavMesh, fingerprint, setCurrentMap: false))
        {
            yield return SceneManager.UnloadSceneAsync(loadedScene);
            FailPreparation(CombatMapApplyFailure.RenderOrNavMeshLoadFailed);
            yield break;
        }

        MapSceneHost oldHost = _mapSceneHost;
        Scene oldRuntimeScene = _loadedRuntimeScene;
        if (oldHost != null && oldHost != loadedHost)
        {
            oldHost.ClearLoadedNavMesh();
            oldHost.SetBakedRenderVisible(false);
        }

        loadedHost.gameObject.SetActive(true);
        loadedHost.SetBakedRenderVisible(true);
        _mapSceneHost = loadedHost;
        _loadedRuntimeScene = loadedScene;
        CompletePreparation(definition, map, fingerprint);
        TryActivatePreparedMap(definition, out _);

        if (oldRuntimeScene.IsValid() && oldRuntimeScene.isLoaded && oldRuntimeScene != loadedScene)
            yield return SceneManager.UnloadSceneAsync(oldRuntimeScene);
    }

    public bool IsMapReady(AuthoredMapDefinition definition)
    {
        if (definition == null || PreparationState != MapPreparationState.Ready) return false;
        return ReferenceEquals(_preparedDefinition, definition) &&
            _preparedFingerprint == definition.ComputeBakeFingerprint() &&
            _preparedMap != null && _isNavMeshReady;
    }

    public bool TryActivatePreparedMap(
        AuthoredMapDefinition definition,
        out CombatMapApplyFailure failure)
    {
        using var _ = ActivateMapMarker.Auto();
        failure = CombatMapApplyFailure.None;
        if (!IsMapReady(definition))
        {
            failure = _preparationFailure != CombatMapApplyFailure.None
                ? _preparationFailure
                : CombatMapApplyFailure.RuntimeMapCreationFailed;
            return false;
        }

        _authoredMap = definition;
        if (!ReferenceEquals(CurrentMap, _preparedMap)) SetCurrentMap(_preparedMap);
        _isNavMeshReady = true;
        CombatAssaultRouteCache.EnsureBuilt(this);
        return true;
    }

    public bool ResetRuntimeMapState()
    {
        if (_authoredMap == null || CurrentMap == null || !_authoredMap.HasValidBakedMapData)
            return false;
        if (!_authoredMap.BakedMapData.RestoreRuntimeState(
                CurrentMap,
                _dirtyGroundCells,
                _dirtyBiomeCells))
            return false;

        _dirtyGroundCells.Clear();
        _dirtyBiomeCells.Clear();
        bool wasReversed = IsStonePositionReversed;
        IsStonePositionReversed = false;
        if (HasMagicStones(CurrentMap))
        {
            FeatureRenderer renderer = _mapSceneHost != null
                ? _mapSceneHost.GetComponent<FeatureRenderer>()
                : null;
            if (renderer == null || !renderer.TryRefreshMagicStonePositions(CurrentMap)) return false;
        }

        if (wasReversed) StonePositionsChanged?.Invoke();
        return true;
    }

    public bool TryGetInitialSpawnPositions(
        CombatTeam team,
        out IReadOnlyList<Vector3> worldPositions)
    {
        worldPositions = Array.Empty<Vector3>();
        if (_authoredMap == null || CurrentMap == null) return false;

        bool useOwnAnchor = team == CombatTeam.Ally;
        if (IsStonePositionReversed) useOwnAnchor = !useOwnAnchor;
        FeatureType anchorType = useOwnAnchor
            ? FeatureType.OwnMainStone
            : FeatureType.EnemyMainStone;
        if (!_authoredMap.BakedMapData.TryGetInitialSpawnPositions(
                anchorType,
                _authoredMap.ComputeBakeFingerprint(),
                out IReadOnlyList<Vector3> localPositions))
            return false;

        var transformed = new Vector3[localPositions.Count];
        Transform origin = MapOrigin;
        for (int i = 0; i < localPositions.Count; i++)
            transformed[i] = origin != null ? origin.TransformPoint(localPositions[i]) : localPositions[i];
        worldPositions = transformed;
        return transformed.Length > 0;
    }

    private bool TryPrepareMap(AuthoredMapDefinition definition, out MapData map)
    {
        using var _ = PrepareMapMarker.Auto();
        map = null;
        if (IsMapReady(definition))
        {
            map = _preparedMap;
            return true;
        }

        PreparationState = MapPreparationState.Loading;
        _preparationFailure = ValidateBakedDefinition(definition, requireHost: true);
        if (_preparationFailure != CombatMapApplyFailure.None)
        {
            PreparationState = MapPreparationState.Failed;
            return false;
        }

        if (!TryCreateRuntimeMap(definition, out map)) return false;

        int fingerprint = definition.ComputeBakeFingerprint();
        _mapSceneHost.Config = definition.SharedConfig;
        if (!_mapSceneHost.HasBakedRenderDataFor(map, fingerprint))
            return FailPreparation(CombatMapApplyFailure.MissingBakedRenderData);

        if (!_mapSceneHost.LoadBakedMap(map, definition.BakedNavMesh, fingerprint))
            return FailPreparation(CombatMapApplyFailure.RenderOrNavMeshLoadFailed);

        CompletePreparation(definition, map, fingerprint);
        return true;
    }

    private CombatMapApplyFailure ValidateBakedDefinition(
        AuthoredMapDefinition definition,
        bool requireHost)
    {
        if (definition == null) return CombatMapApplyFailure.MissingDefinition;
        if (definition.SharedConfig == null) return CombatMapApplyFailure.MissingSharedConfig;
        if (!definition.HasValidBakedMapData) return CombatMapApplyFailure.MissingBakedMapData;
        if (!definition.HasValidBakedNavMesh) return CombatMapApplyFailure.MissingBakedNavMesh;
        if (requireHost && _mapSceneHost == null) return CombatMapApplyFailure.MissingMapSceneHost;
        return CombatMapApplyFailure.None;
    }

    private bool TryCreateRuntimeMap(AuthoredMapDefinition definition, out MapData map)
    {
        try
        {
            map = definition.BakedMapData.CreateRuntimeMap();
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogException(ex, this);
            map = null;
            return FailPreparation(CombatMapApplyFailure.RuntimeMapCreationFailed);
        }
    }

    private void CompletePreparation(AuthoredMapDefinition definition, MapData map, int fingerprint)
    {
        _preparedDefinition = definition;
        _preparedMap = map;
        _preparedFingerprint = fingerprint;
        _preparationFailure = CombatMapApplyFailure.None;
        PreparationState = MapPreparationState.Ready;
        _isNavMeshReady = true;
    }

    private static MapSceneHost FindMapSceneHost(Scene scene)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            MapSceneHost host = roots[i].GetComponentInChildren<MapSceneHost>(includeInactive: true);
            if (host != null) return host;
        }

        return null;
    }

    private bool FailPreparation(CombatMapApplyFailure failure)
    {
        _preparedDefinition = null;
        _preparedMap = null;
        _preparedFingerprint = 0;
        _preparationFailure = failure;
        PreparationState = MapPreparationState.Failed;
        _isNavMeshReady = false;
        return false;
    }

    public void SetCurrentMap(MapData map)
    {
        bool isSameMap = map != null && ReferenceEquals(CurrentMap, map);
        CurrentMap = map;
        if (!isSameMap) _isNavMeshReady = false;
        if (!isSameMap)
        {
            IsStonePositionReversed = false;
            _dirtyGroundCells.Clear();
            _dirtyBiomeCells.Clear();
        }
        if (!isSameMap) CombatAssaultRouteCache.Invalidate();
        UpdateTerrainHeightRange(map);
        InitializeMagicStoneSystem(map);
        if (!isSameMap) CurrentMapChanged?.Invoke();
    }

    public bool TrySetStonePositionsReversed(bool reversed)
    {
        if (IsStonePositionReversed == reversed) return true;
        if (CurrentMap == null)
        {
            Debug.LogWarning($"[{nameof(CombatMapSystem)}] Cannot reverse stone positions before a map is ready.", this);
            return false;
        }

        if (!TrySwapStonePositions(CurrentMap))
        {
            Debug.LogWarning(
                $"[{nameof(CombatMapSystem)}] Cannot reverse stone positions because paired stone counts do not match.",
                this);
            return false;
        }

        if (HasMagicStones(CurrentMap))
        {
            FeatureRenderer featureRenderer = _mapSceneHost != null
                ? _mapSceneHost.GetComponent<FeatureRenderer>()
                : null;
            if (featureRenderer == null || !featureRenderer.TryRefreshMagicStonePositions(CurrentMap))
            {
                TrySwapStonePositions(CurrentMap);
                Debug.LogWarning(
                    $"[{nameof(CombatMapSystem)}] Cannot reverse stone positions because the map features are not rendered.",
                    this);
                return false;
            }
        }

        IsStonePositionReversed = reversed;
        StonePositionsChanged?.Invoke();
        return true;
    }

    private static bool HasMagicStones(MapData map)
    {
        for (int i = 0; i < map.Features.Count; i++)
        {
            FeatureType type = map.Features[i].Type;
            if (type == FeatureType.OwnMainStone ||
                type == FeatureType.EnemyMainStone)
            {
                return true;
            }
        }

        return false;
    }

    private static bool TrySwapStonePositions(MapData map)
    {
        var ownMainIndices = new List<int>();
        var enemyMainIndices = new List<int>();

        for (int i = 0; i < map.Features.Count; i++)
        {
            switch (map.Features[i].Type)
            {
                case FeatureType.OwnMainStone:
                    ownMainIndices.Add(i);
                    break;
                case FeatureType.EnemyMainStone:
                    enemyMainIndices.Add(i);
                    break;
            }
        }

        if (ownMainIndices.Count != enemyMainIndices.Count)
        {
            return false;
        }

        SwapFeaturePositions(map.Features, ownMainIndices, enemyMainIndices);
        return true;
    }

    private static void SwapFeaturePositions(
        List<PlacedFeature> features,
        List<int> firstIndices,
        List<int> secondIndices)
    {
        for (int i = 0; i < firstIndices.Count; i++)
        {
            int firstIndex = firstIndices[i];
            int secondIndex = secondIndices[i];
            PlacedFeature first = features[firstIndex];
            PlacedFeature second = features[secondIndex];
            features[firstIndex] = new PlacedFeature(
                first.Type,
                second.WorldPosition,
                first.Rotation,
                first.Scale);
            features[secondIndex] = new PlacedFeature(
                second.Type,
                first.WorldPosition,
                second.Rotation,
                second.Scale);
        }
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

    public MapData BuildAndSetCurrentMap()
    {
        return BuildAndSetCurrentMap(render3D: false);
    }

    public MapData BuildAndSetCurrentMap(bool render3D)
    {
        return BuildAuthoredAndSetCurrentMap(render3D);
    }

    public MapData ApplyAuthoredMap(AuthoredMapDefinition definition, bool render3D = true)
    {
        if (definition == null)
        {
            Debug.LogWarning($"[{nameof(CombatMapSystem)}] Authored map definition is null.");
            SetCurrentMap(null);
            return null;
        }

        _authoredMap = definition;
        if (render3D && _mapSceneHost != null)
            _mapSceneHost.Clear3D();

        return BuildAuthoredAndSetCurrentMap(render3D);
    }

    public bool TryApplyBakedAuthoredMap(
        AuthoredMapDefinition definition,
        out MapData map,
        out CombatMapApplyFailure failure)
    {
        if (!IsMapReady(definition) && !TryPrepareMap(definition, out map))
        {
            map = null;
            failure = _preparationFailure != CombatMapApplyFailure.None
                ? _preparationFailure
                : CombatMapApplyFailure.MapNotReady;
            return false;
        }

        if (!TryActivatePreparedMap(definition, out failure))
        {
            map = null;
            return false;
        }

        map = CurrentMap;
        return true;
    }

    private MapData BuildAuthoredAndSetCurrentMap(bool render3D)
    {
        if (_authoredMap == null)
        {
            Debug.LogWarning($"[{nameof(CombatMapSystem)}] Authored map is not assigned.");
            SetCurrentMap(null);
            return null;
        }

        if (_authoredMap.SharedConfig == null)
        {
            Debug.LogWarning(
                $"[{nameof(CombatMapSystem)}] Authored map '{_authoredMap.name}' has no SharedConfig.");
            SetCurrentMap(null);
            return null;
        }

        if (_mapSceneHost == null)
        {
            Debug.LogWarning($"[{nameof(CombatMapSystem)}] MapSceneHost is not assigned.");
            SetCurrentMap(null);
            return null;
        }

        _mapSceneHost.Config = _authoredMap.SharedConfig;

        MapData map;
        try
        {
            map = AuthoredMapBuilder.Build(_authoredMap);
        }
        catch (System.Exception ex)
        {
            Debug.LogException(ex);
            SetCurrentMap(null);
            return null;
        }

        UnityEngine.AI.NavMeshData prebakedNavMesh = null;
        if (render3D)
        {
            bool valid = _authoredMap.HasValidBakedNavMesh;
            int stored = _authoredMap.NavMeshBakeFingerprint;
            int current = _authoredMap.ComputeBakeFingerprint();
            if (valid)
            {
                prebakedNavMesh = _authoredMap.BakedNavMesh;
            }
            else
            {
                Debug.LogWarning(
                    $"[{nameof(CombatMapSystem)}] NavMesh: Build (runtime fallback). " +
                    $"bakedData={(_authoredMap.BakedNavMesh != null ? "set" : "null")} " +
                    $"storedFp={stored} currentFp={current}. " +
                    "MapAuthoring の「シーンへ3D反映」で再ベイクしてください。");
            }
        }

        bool navMeshReady = _mapSceneHost.ApplyMapData(
            map,
            render3D,
            bakeNavMesh: render3D,
            prebakedNavMesh);
        SetCurrentMap(map);
        _isNavMeshReady = render3D && navMeshReady;
        if (navMeshReady && render3D)
        {
            CombatAssaultRouteCache.EnsureBuilt(this);
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
        _dirtyGroundCells.Add(cell);
        CurrentMap.GroundStates.SetCell(cell.x, cell.y, state);
        return true;
    }

    public bool SetBiomeId(Vector2Int cell, string biomeId)
    {
        if (!IsValidCell(cell)) return false;
        _dirtyBiomeCells.Add(cell);
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
