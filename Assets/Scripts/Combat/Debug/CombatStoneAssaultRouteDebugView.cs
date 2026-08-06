using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using WarSimulation.Combat.Map;

[DisallowMultipleComponent]
[DefaultExecutionOrder(1000)]
public sealed class CombatStoneAssaultRouteDebugView : CombatDebugBehaviour
{
    private const string GeneratedRootName = "GeneratedStoneAssaultRoutes";
    private const int MaxVisibleRoutes = 3;

    public override string InspectorDescription =>
        "Play中のみ、進攻ルートと橋端点（理想=白 / Sample後=緑 / 入口=水色 / 出口=橙）を表示します。";

    [SerializeField] private CombatTeam _attackingTeam = CombatTeam.Ally;
    [SerializeField, Min(0.5f)] private float _stoneSampleRadius = 10f;
    [SerializeField, Min(0.5f)] private float _waypointSampleRadius = 6f;
    [SerializeField, Min(0f)] private float _bridgeEndpointMargin = 1f;
    [SerializeField] private bool _allowRiverCrossing;
    [SerializeField, Min(0f)] private float _surfaceOffset = 0.15f;
    [SerializeField] private float _lineWidth = 0.28f;
    [SerializeField] private Color _leftColor = new(0.1f, 0.85f, 1f, 0.95f);
    [SerializeField] private Color _centerColor = new(1f, 0.85f, 0.1f, 0.95f);
    [SerializeField] private Color _rightColor = new(1f, 0.2f, 0.75f, 0.95f);

    [SerializeField] private bool _showEndpointMarkers = true;
    [SerializeField, Min(0.1f)] private float _endpointMarkerScale = 1.1f;
    [SerializeField] private Color _idealEndpointColor = new(1f, 1f, 1f, 0.85f);
    [SerializeField] private Color _sampledEndpointColor = new(0.2f, 1f, 0.35f, 0.95f);
    [SerializeField] private Color _enterEndpointColor = new(0.15f, 0.9f, 1f, 1f);
    [SerializeField] private Color _exitEndpointColor = new(1f, 0.35f, 0.15f, 1f);

    private readonly List<LineRenderer> _lines = new();
    private readonly List<Transform> _endpointMarkers = new();
    private Transform _generatedRoot;
    private Transform _endpointRoot;
    private int _lastVisibleRouteCount = -1;
    private float _nextRetryTime;
    private string _lastFailureReason = "";

    private void OnEnable()
    {
        if (!Application.isPlaying)
        {
            DestroyGeneratedRoot();
            return;
        }

        if (!CombatPlaytestDebugSettings.ShowAssaultRoutes)
        {
            enabled = false;
            return;
        }

        CombatNavMeshBuilder.Built += OnNavMeshBuilt;
        CombatNavMeshBuilder.Cleared += OnNavMeshCleared;
        _nextRetryTime = 0f;
        ApplyPlaytestSettings();
        RefreshRoutes();
    }

    public void ApplyPlaytestSettings()
    {
        _attackingTeam = CombatPlaytestDebugSettings.AssaultAttackingTeam;
        _allowRiverCrossing = CombatPlaytestDebugSettings.AssaultAllowRiverCrossing;
        _showEndpointMarkers = CombatPlaytestDebugSettings.AssaultShowEndpointMarkers;
        if (isActiveAndEnabled && Application.isPlaying) RefreshRoutes();
    }

    private void OnDisable()
    {
        CombatNavMeshBuilder.Built -= OnNavMeshBuilt;
        CombatNavMeshBuilder.Cleared -= OnNavMeshCleared;
        DestroyGeneratedRoot();
    }

    private void OnDestroy()
    {
        CombatNavMeshBuilder.Built -= OnNavMeshBuilt;
        CombatNavMeshBuilder.Cleared -= OnNavMeshCleared;
        DestroyGeneratedRoot();
    }

    private void Update()
    {
        if (!Application.isPlaying || !isActiveAndEnabled) return;
        if (_lastVisibleRouteCount > 0) return;
        if (Time.unscaledTime < _nextRetryTime) return;

        _nextRetryTime = Time.unscaledTime + 0.5f;
        RefreshRoutes();
    }

    private void OnNavMeshCleared()
    {
        HideLines();
        ClearEndpointMarkers();
        _lastFailureReason = "";
    }

    private void OnNavMeshBuilt()
    {
        _lastVisibleRouteCount = -1;
        RefreshRoutes();
    }

    private void RefreshRoutes()
    {
        if (!Application.isPlaying || !isActiveAndEnabled) return;

        EnsureLines();
        if (!TryGetBuildContext(
                out CombatMapSystem mapSystem,
                out MapData map,
                out Vector3 start,
                out Vector3 goal,
                out int areaMask,
                out string failureReason))
        {
            if (failureReason != _lastFailureReason)
            {
                _lastFailureReason = failureReason;
            }

            HideLines();
            ClearEndpointMarkers();
            return;
        }

        var endpointDebug = new List<CombatStoneAssaultRoutes.BridgeEndpointDebug>();
        List<CombatStoneAssaultRoutes.Candidate> candidates = CombatStoneAssaultRoutes.BuildCandidates(
            map,
            mapSystem.MapOrigin,
            start,
            goal,
            areaMask,
            new CombatStoneAssaultRoutes.BuildSettings
            {
                WaypointSampleRadius = _waypointSampleRadius,
                BridgeEndpointMargin = _bridgeEndpointMargin,
            },
            endpointDebug);
        List<CombatStoneAssaultRoutes.Candidate> selected = CombatStoneAssaultRoutes.TakeUpTo(
            candidates,
            MaxVisibleRoutes);

        for (int i = 0; i < MaxVisibleRoutes; i++)
        {
            if (i >= selected.Count)
            {
                HideLine(_lines[i]);
                continue;
            }

            RenderLine(_lines[i], selected[i].Corners, mapSystem);
        }

        RefreshEndpointMarkers(endpointDebug, selected, mapSystem);

        if (selected.Count == 0)
        {
            string reason = $"候補0本 (橋Feature={CountBridges(map)})";
            if (reason != _lastFailureReason)
            {
                Debug.LogWarning("[魔石進攻ルート] " + reason, this);
                _lastFailureReason = reason;
            }
        }

        _lastVisibleRouteCount = selected.Count;
        if (selected.Count > 0) _lastFailureReason = "";
    }

    private static int CountBridges(MapData map)
    {
        int count = 0;
        for (int i = 0; i < map.Features.Count; i++)
        {
            if (map.Features[i].Type == FeatureType.Bridge) count++;
        }

        return count;
    }

    private bool TryGetBuildContext(
        out CombatMapSystem mapSystem,
        out MapData map,
        out Vector3 start,
        out Vector3 goal,
        out int areaMask,
        out string failureReason)
    {
        mapSystem = FindAnyObjectByType<CombatMapSystem>();
        map = mapSystem != null ? mapSystem.CurrentMap : null;
        start = default;
        goal = default;
        areaMask = CombatStoneAssaultRoutes.CreateAreaMask(_allowRiverCrossing);
        failureReason = null;

        if (map == null)
        {
            failureReason = "CurrentMap が未設定";
            return false;
        }

        if (!TryFindMainStones(out Vector3 ownStone, out Vector3 enemyStone))
        {
            failureReason = "メイン魔石が見つからない";
            return false;
        }

        if (!CombatStoneAssaultRoutes.TrySamplePosition(ownStone, _stoneSampleRadius, areaMask, out start))
        {
            failureReason = "自軍魔石付近の NavMesh を Sample できない";
            return false;
        }

        if (!CombatStoneAssaultRoutes.TrySamplePosition(enemyStone, _stoneSampleRadius, areaMask, out goal))
        {
            failureReason = "敵軍魔石付近の NavMesh を Sample できない";
            return false;
        }

        return true;
    }

    private bool TryFindMainStones(out Vector3 ownStone, out Vector3 enemyStone)
    {
        FeatureType ownType = _attackingTeam == CombatTeam.Ally
            ? FeatureType.OwnMainStone
            : FeatureType.EnemyMainStone;
        FeatureType enemyType = _attackingTeam == CombatTeam.Ally
            ? FeatureType.EnemyMainStone
            : FeatureType.OwnMainStone;
        ownStone = default;
        enemyStone = default;
        bool hasOwn = false;
        bool hasEnemy = false;

        MagicStone[] stones = FindObjectsByType<MagicStone>(FindObjectsInactive.Exclude);
        for (int i = 0; i < stones.Length; i++)
        {
            MagicStone stone = stones[i];
            if (stone.FeatureType == ownType)
            {
                ownStone = stone.transform.position;
                hasOwn = true;
            }
            else if (stone.FeatureType == enemyType)
            {
                enemyStone = stone.transform.position;
                hasEnemy = true;
            }
        }

        return hasOwn && hasEnemy;
    }

    private void EnsureLines()
    {
        if (_generatedRoot == null)
        {
            var root = new GameObject(GeneratedRootName);
            root.transform.SetParent(transform, worldPositionStays: false);
            _generatedRoot = root.transform;
            _lines.Clear();
        }

        while (_lines.Count < MaxVisibleRoutes)
        {
            int laneIndex = _lines.Count;
            var go = new GameObject(GetLaneName(laneIndex), typeof(LineRenderer));
            go.transform.SetParent(_generatedRoot, worldPositionStays: false);
            LineRenderer line = go.GetComponent<LineRenderer>();
            ApplyStyle(line, GetLaneColor(laneIndex));
            line.enabled = false;
            _lines.Add(line);
        }
    }

    private void DestroyGeneratedRoot()
    {
        _lines.Clear();
        _endpointMarkers.Clear();
        _endpointRoot = null;
        _lastVisibleRouteCount = -1;

        if (_generatedRoot != null)
        {
            DestroyRouteObject(_generatedRoot.gameObject);
            _generatedRoot = null;
        }

        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child != null && child.name == GeneratedRootName)
            {
                DestroyRouteObject(child.gameObject);
            }
        }
    }

    private void HideLines()
    {
        for (int i = 0; i < _lines.Count; i++) HideLine(_lines[i]);
        _lastVisibleRouteCount = -1;
    }

    private void RefreshEndpointMarkers(
        List<CombatStoneAssaultRoutes.BridgeEndpointDebug> endpoints,
        List<CombatStoneAssaultRoutes.Candidate> selected,
        CombatMapSystem mapSystem)
    {
        ClearEndpointMarkers();
        if (!_showEndpointMarkers || endpoints == null) return;

        EnsureEndpointRoot();
        for (int i = 0; i < endpoints.Count; i++)
        {
            CombatStoneAssaultRoutes.BridgeEndpointDebug endpoint = endpoints[i];
            // 理想位置（計算上の端点）= 白、Sample 後 = 緑
            CreateEndpointMarker(
                $"Bridge{endpoint.FeatureIndex}_IdealA",
                GetVisibleRoutePosition(endpoint.IdealA, mapSystem),
                _idealEndpointColor,
                _endpointMarkerScale * 0.7f);
            CreateEndpointMarker(
                $"Bridge{endpoint.FeatureIndex}_IdealB",
                GetVisibleRoutePosition(endpoint.IdealB, mapSystem),
                _idealEndpointColor,
                _endpointMarkerScale * 0.7f);
            if (!endpoint.Sampled) continue;
            CreateEndpointMarker(
                $"Bridge{endpoint.FeatureIndex}_SampledA",
                GetVisibleRoutePosition(endpoint.SampledA, mapSystem),
                _sampledEndpointColor,
                _endpointMarkerScale);
            CreateEndpointMarker(
                $"Bridge{endpoint.FeatureIndex}_SampledB",
                GetVisibleRoutePosition(endpoint.SampledB, mapSystem),
                _sampledEndpointColor,
                _endpointMarkerScale);
        }

        for (int i = 0; i < selected.Count; i++)
        {
            CombatStoneAssaultRoutes.Candidate route = selected[i];
            if (!route.HasBridgeWaypoints) continue;
            CreateEndpointMarker(
                $"{route.Label}_Enter",
                GetVisibleRoutePosition(route.EnterWorld, mapSystem),
                _enterEndpointColor,
                _endpointMarkerScale * 1.25f);
            CreateEndpointMarker(
                $"{route.Label}_Exit",
                GetVisibleRoutePosition(route.ExitWorld, mapSystem),
                _exitEndpointColor,
                _endpointMarkerScale * 1.25f);
        }
    }

    private void EnsureEndpointRoot()
    {
        if (_generatedRoot == null) EnsureLines();
        if (_endpointRoot != null) return;
        var root = new GameObject("EndpointMarkers");
        root.transform.SetParent(_generatedRoot, worldPositionStays: false);
        _endpointRoot = root.transform;
    }

    private void CreateEndpointMarker(string name, Vector3 worldPosition, Color color, float scale)
    {
        var marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        marker.name = name;
        marker.transform.SetParent(_endpointRoot, worldPositionStays: true);
        marker.transform.position = worldPosition;
        marker.transform.localScale = Vector3.one * scale;
        Collider collider = marker.GetComponent<Collider>();
        if (collider != null) Destroy(collider);
        Renderer renderer = marker.GetComponent<Renderer>();
        if (renderer != null)
        {
            Material material = CreateMaterial(color);
            renderer.sharedMaterial = material;
        }

        _endpointMarkers.Add(marker.transform);
    }

    private void ClearEndpointMarkers()
    {
        for (int i = 0; i < _endpointMarkers.Count; i++)
        {
            if (_endpointMarkers[i] != null) DestroyRouteObject(_endpointMarkers[i].gameObject);
        }

        _endpointMarkers.Clear();
        if (_endpointRoot != null)
        {
            DestroyRouteObject(_endpointRoot.gameObject);
            _endpointRoot = null;
        }
    }

    private static void DestroyRouteObject(GameObject routeObject)
    {
        if (routeObject == null) return;
        if (Application.isPlaying) Destroy(routeObject);
        else DestroyImmediate(routeObject);
    }

    private void ApplyStyle(LineRenderer line, Color color)
    {
        if (line == null) return;
        line.useWorldSpace = true;
        line.widthMultiplier = _lineWidth;
        line.numCornerVertices = 4;
        line.numCapVertices = 4;
        line.startColor = color;
        line.endColor = color;
        if (line.sharedMaterial == null) line.sharedMaterial = CreateMaterial(color);
        ApplyColor(line.sharedMaterial, color);
    }

    private static Material CreateMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
            ?? Shader.Find("Sprites/Default")
            ?? Shader.Find("Unlit/Color");
        if (shader == null) return null;
        var material = new Material(shader) { name = "StoneAssaultRouteDebugMaterial" };
        ApplyColor(material, color);
        return material;
    }

    private static void ApplyColor(Material material, Color color)
    {
        if (material == null) return;
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        if (material.HasProperty("_ZTest")) material.SetInt("_ZTest", (int)CompareFunction.Always);
    }

    private void RenderLine(LineRenderer line, IReadOnlyList<Vector3> corners, CombatMapSystem mapSystem)
    {
        if (line == null) return;
        line.enabled = true;
        line.positionCount = corners.Count;
        for (int i = 0; i < corners.Count; i++)
        {
            line.SetPosition(i, GetVisibleRoutePosition(corners[i], mapSystem));
        }
    }

    private Vector3 GetVisibleRoutePosition(Vector3 routePosition, CombatMapSystem mapSystem)
    {
        if (mapSystem == null || mapSystem.MapOrigin == null)
        {
            return routePosition + Vector3.up * _surfaceOffset;
        }

        Vector3 mapLocal = mapSystem.MapOrigin.InverseTransformPoint(routePosition);
        mapLocal.y = 0f;
        Vector3 surface = mapSystem.MapLocalToSurfaceWorldPosition(mapLocal);
        routePosition.y = Mathf.Max(routePosition.y, surface.y);
        return routePosition + Vector3.up * _surfaceOffset;
    }

    private static void HideLine(LineRenderer line)
    {
        if (line == null) return;
        line.enabled = false;
        line.positionCount = 0;
    }

    private Color GetLaneColor(int laneIndex) => laneIndex switch
    {
        0 => _leftColor,
        1 => _centerColor,
        _ => _rightColor,
    };

    private static string GetLaneName(int laneIndex) => laneIndex switch
    {
        0 => "LeftRoute",
        1 => "CenterRoute",
        _ => "RightRoute",
    };
}
