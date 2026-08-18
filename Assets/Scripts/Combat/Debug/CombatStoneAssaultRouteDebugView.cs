using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
[DefaultExecutionOrder(1000)]
public sealed class CombatStoneAssaultRouteDebugView : CombatDebugBehaviour
{
    private const string GeneratedRootName = "GeneratedStoneAssaultRoutes";

    public override string InspectorDescription => "Play中のみ、MapDataに保存された検証済み進攻ルートを表示します。";

    [SerializeField, Min(0f)] private float _surfaceOffset = 0.15f;
    [SerializeField] private float _lineWidth = 0.28f;
    [SerializeField] private Color _leftColor = new(0.1f, 0.85f, 1f, 0.95f);
    [SerializeField] private Color _centerColor = new(1f, 0.85f, 0.1f, 0.95f);
    [SerializeField] private Color _rightColor = new(1f, 0.2f, 0.75f, 0.95f);

    private readonly List<LineRenderer> _lines = new();
    private Transform _generatedRoot;

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

        CombatNavMeshBuilder.Built += RefreshRoutes;
        CombatNavMeshBuilder.Cleared += HideLines;
        RefreshRoutes();
    }

    private void OnDisable()
    {
        CombatNavMeshBuilder.Built -= RefreshRoutes;
        CombatNavMeshBuilder.Cleared -= HideLines;
        DestroyGeneratedRoot();
    }

    private void OnDestroy()
    {
        CombatNavMeshBuilder.Built -= RefreshRoutes;
        CombatNavMeshBuilder.Cleared -= HideLines;
        DestroyGeneratedRoot();
    }

    private void RefreshRoutes()
    {
        if (!Application.isPlaying || !isActiveAndEnabled) return;
        CombatMapSystem mapSystem = CombatSceneContext.Instance?.MapSystem;
        mapSystem ??= FindAnyObjectByType<CombatMapSystem>();
        if (mapSystem == null || mapSystem.CurrentMap == null)
        {
            HideLines();
            return;
        }

        CombatAssaultRouteCache.EnsureBuilt(mapSystem);
        IReadOnlyList<CombatAiAssaultRoute> routes = CombatAssaultRouteCache.GetRoutes(
            CombatTeam.Ally,
            stonePositionReversed: false);
        EnsureLines(routes.Count);
        for (int i = 0; i < _lines.Count; i++)
        {
            if (i < routes.Count) RenderLine(_lines[i], routes[i].Corners, mapSystem);
            else HideLine(_lines[i]);
        }
    }

    private void EnsureLines(int count)
    {
        if (_generatedRoot == null)
        {
            var root = new GameObject(GeneratedRootName);
            root.transform.SetParent(transform, worldPositionStays: false);
            _generatedRoot = root.transform;
            _lines.Clear();
        }

        while (_lines.Count < count)
        {
            int routeIndex = _lines.Count;
            var go = new GameObject($"Route{routeIndex + 1}", typeof(LineRenderer));
            go.transform.SetParent(_generatedRoot, worldPositionStays: false);
            LineRenderer line = go.GetComponent<LineRenderer>();
            ApplyStyle(line, GetRouteColor(routeIndex));
            line.enabled = false;
            _lines.Add(line);
        }
    }

    private void DestroyGeneratedRoot()
    {
        _lines.Clear();
        if (_generatedRoot != null)
        {
            DestroyRouteObject(_generatedRoot.gameObject);
            _generatedRoot = null;
        }

        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child != null && child.name == GeneratedRootName)
                DestroyRouteObject(child.gameObject);
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
        line.enabled = true;
        line.positionCount = corners.Count;
        for (int i = 0; i < corners.Count; i++)
            line.SetPosition(i, GetVisibleRoutePosition(corners[i], mapSystem));
    }

    private Vector3 GetVisibleRoutePosition(Vector3 routePosition, CombatMapSystem mapSystem)
    {
        if (mapSystem.MapOrigin == null) return routePosition + Vector3.up * _surfaceOffset;
        Vector3 mapLocal = mapSystem.MapOrigin.InverseTransformPoint(routePosition);
        mapLocal.y = 0f;
        Vector3 surface = mapSystem.MapLocalToSurfaceWorldPosition(mapLocal);
        routePosition.y = Mathf.Max(routePosition.y, surface.y);
        return routePosition + Vector3.up * _surfaceOffset;
    }

    private void HideLines()
    {
        for (int i = 0; i < _lines.Count; i++) HideLine(_lines[i]);
    }

    private static void HideLine(LineRenderer line)
    {
        if (line == null) return;
        line.enabled = false;
        line.positionCount = 0;
    }

    private Color GetRouteColor(int routeIndex) => (routeIndex % 3) switch
    {
        0 => _leftColor,
        1 => _centerColor,
        _ => _rightColor,
    };
}
