using UnityEngine;

[RequireComponent(typeof(CombatCharacterBody))]
public sealed class CombatCharacterRouteVisualizer : MonoBehaviour
{
    private const string RouteLineName = "GeneratedRouteLine";

    [SerializeField] private LineRenderer _lineRenderer;
    [SerializeField, Min(0f)] private float _surfaceOffset = 0.08f;
    [SerializeField, Min(0.01f)] private float _lineWidth = 0.16f;
    [SerializeField] private Color _routeColor = new Color(0.15f, 0.75f, 1f, 0.9f);

    private CombatCharacterBody _body;

    private void Awake()
    {
        _body = GetComponent<CombatCharacterBody>();
        EnsureLineRenderer();
        ApplyStyle();
        HideRoute();
    }

    private void OnEnable()
    {
        if (_body == null) _body = GetComponent<CombatCharacterBody>();
        if (_body != null)
        {
            _body.RouteChanged += RenderRoute;
            RenderRoute(_body.CurrentRouteCorners);
        }
    }

    private void OnDisable()
    {
        if (_body != null)
        {
            _body.RouteChanged -= RenderRoute;
        }
    }

    private void OnValidate()
    {
        if (_lineRenderer != null)
        {
            ApplyStyle();
        }
    }

    private void RenderRoute(Vector3[] corners)
    {
        EnsureLineRenderer();
        if (corners == null || corners.Length < 2)
        {
            HideRoute();
            return;
        }

        _lineRenderer.enabled = true;
        _lineRenderer.positionCount = corners.Length;
        for (int i = 0; i < corners.Length; i++)
        {
            _lineRenderer.SetPosition(i, corners[i] + Vector3.up * _surfaceOffset);
        }
    }

    private void HideRoute()
    {
        if (_lineRenderer == null) return;

        _lineRenderer.positionCount = 0;
        _lineRenderer.enabled = false;
    }

    private void EnsureLineRenderer()
    {
        if (_lineRenderer != null) return;

        Transform existing = transform.Find(RouteLineName);
        if (existing != null)
        {
            _lineRenderer = existing.GetComponent<LineRenderer>();
            if (_lineRenderer != null) return;
        }

        var go = new GameObject(RouteLineName, typeof(LineRenderer));
        go.transform.SetParent(transform, worldPositionStays: false);
        _lineRenderer = go.GetComponent<LineRenderer>();
        ApplyStyle();
    }

    private void ApplyStyle()
    {
        if (_lineRenderer == null) return;

        _lineRenderer.useWorldSpace = true;
        _lineRenderer.widthMultiplier = _lineWidth;
        _lineRenderer.numCornerVertices = 3;
        _lineRenderer.numCapVertices = 3;
        _lineRenderer.textureMode = LineTextureMode.Stretch;
        _lineRenderer.startColor = _routeColor;
        _lineRenderer.endColor = _routeColor;

        if (_lineRenderer.sharedMaterial == null)
        {
            _lineRenderer.sharedMaterial = CreateDefaultMaterial(_routeColor);
        }
        else
        {
            ApplyColor(_lineRenderer.sharedMaterial, _routeColor);
        }
    }

    private static Material CreateDefaultMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) return null;

        var material = new Material(shader) { name = "DefaultRouteLineMaterial" };
        ApplyColor(material, color);
        return material;
    }

    private static void ApplyColor(Material material, Color color)
    {
        if (material == null) return;

        material.color = color;
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
    }
}
