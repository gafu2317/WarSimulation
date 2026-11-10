using UnityEngine;

[RequireComponent(typeof(CanvasRenderer))]
public sealed class CountryMinimapViewport : UnityEngine.UI.MaskableGraphic
{
    [SerializeField] private Camera _viewCamera;
    [SerializeField] private CountryComparisonView _country;
    [SerializeField] private Rect _countryBounds;
    [SerializeField] private float _lineWidth = 3f;
    private readonly Vector2[] _polygon = new Vector2[16];
    private readonly Vector2[] _scratch = new Vector2[16];
    private Matrix4x4 _lastMatrix;
    private Rect _lastBounds;
    private int _count;
    private Vector2 _center;
    private bool _showCenter;

    public void Bind(Camera viewCamera, CountryComparisonView country)
    {
        _viewCamera = viewCamera;
        _country = country;
        _lastMatrix = Matrix4x4.zero;
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        _lastMatrix = Matrix4x4.zero;
        Canvas.preWillRenderCanvases += RefreshViewport;
    }

    protected override void OnDisable()
    {
        Canvas.preWillRenderCanvases -= RefreshViewport;
        base.OnDisable();
    }

    private void RefreshViewport()
    {
        if (_viewCamera == null || _country == null) return;
        int index = _country.viewIndex < 2 ? _country.viewIndex : 0;
        Vector3 origin = _country.centers[index];
        Rect bounds = _countryBounds;
        bounds.position += new Vector2(origin.x, origin.z);
        Matrix4x4 matrix = _viewCamera.projectionMatrix * _viewCamera.worldToCameraMatrix;
        if (matrix == _lastMatrix && bounds == _lastBounds) return;
        _lastMatrix = matrix;
        _lastBounds = bounds;
        _count = ProjectViewport(_viewCamera, bounds, _polygon, _scratch);
        var ground = new Plane(Vector3.up, Vector3.zero);
        Ray ray = _viewCamera.ViewportPointToRay(new Vector3(.5f, .5f));
        _showCenter = ground.Raycast(ray, out float distance);
        if (_showCenter)
        {
            Vector3 point = ray.GetPoint(distance);
            _center = new Vector2((point.x - bounds.xMin) / bounds.width, (point.z - bounds.yMin) / bounds.height);
            _showCenter = _center.x >= 0 && _center.x <= 1 && _center.y >= 0 && _center.y <= 1;
        }
        SetVerticesDirty();
    }

    public static int ProjectViewport(Camera camera, Rect bounds, Vector2[] polygon, Vector2[] scratch)
    {
        if (camera == null || bounds.width <= 0 || bounds.height <= 0) return 0;
        var ground = new Plane(Vector3.up, Vector3.zero);
        for (int i = 0; i < 4; i++)
        {
            Ray ray = camera.ViewportPointToRay(new Vector3(i >= 2 ? 1 : 0, i is 1 or 2 ? 1 : 0));
            if (!ground.Raycast(ray, out float distance)) return 0;
            Vector3 point = ray.GetPoint(distance);
            polygon[i] = new Vector2((point.x - bounds.xMin) / bounds.width, (point.z - bounds.yMin) / bounds.height);
        }
        int count = 4;
        for (int edge = 0; edge < 4 && count > 0; edge++)
        {
            int output = 0;
            Vector2 previous = polygon[count - 1];
            float previousDistance = EdgeDistance(previous, edge);
            for (int i = 0; i < count; i++)
            {
                Vector2 current = polygon[i];
                float currentDistance = EdgeDistance(current, edge);
                if ((previousDistance >= 0) != (currentDistance >= 0))
                    scratch[output++] = Vector2.LerpUnclamped(previous, current, previousDistance / (previousDistance - currentDistance));
                if (currentDistance >= 0) scratch[output++] = current;
                previous = current;
                previousDistance = currentDistance;
            }
            count = output;
            System.Array.Copy(scratch, polygon, count);
        }
        return count;
    }

    private static float EdgeDistance(Vector2 point, int edge) => edge switch
    {
        0 => point.x,
        1 => 1 - point.x,
        2 => point.y,
        _ => 1 - point.y
    };

    protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper vh)
    {
        vh.Clear();
        if (_count < 3) return;
        Rect rect = GetPixelAdjustedRect();
        Vector2 Local(Vector2 point) => rect.min + Vector2.Scale(point, rect.size);
        for (int i = 0; i < _count; i++)
            vh.AddVert(Local(_polygon[i]), new Color(color.r, color.g, color.b, .1f * color.a), Vector2.zero);
        for (int i = 1; i < _count - 1; i++) vh.AddTriangle(0, i, i + 1);
        for (int i = 0; i < _count; i++)
            Segment(vh, Local(_polygon[i]), Local(_polygon[(i + 1) % _count]), _lineWidth + 3, new Color32(26, 39, 49, 235));
        for (int i = 0; i < _count; i++)
            Segment(vh, Local(_polygon[i]), Local(_polygon[(i + 1) % _count]), _lineWidth, color);
        if (!_showCenter) return;
        Vector2 center = Local(_center);
        Segment(vh, center + Vector2.down * 10, center + Vector2.down * 24, 3, color);
        Disc(vh, center, 10, color);
        Disc(vh, center, 7, new Color32(101, 172, 191, 255));
    }

    private static void Segment(UnityEngine.UI.VertexHelper vh, Vector2 a, Vector2 b, float width, Color tint)
    {
        Vector2 direction = (b - a).normalized;
        Vector2 normal = new(-direction.y * width * .5f, direction.x * width * .5f);
        int start = vh.currentVertCount;
        vh.AddVert(a - normal, tint, Vector2.zero);
        vh.AddVert(a + normal, tint, Vector2.zero);
        vh.AddVert(b + normal, tint, Vector2.zero);
        vh.AddVert(b - normal, tint, Vector2.zero);
        vh.AddTriangle(start, start + 1, start + 2);
        vh.AddTriangle(start, start + 2, start + 3);
    }

    private static void Disc(UnityEngine.UI.VertexHelper vh, Vector2 center, float radius, Color tint)
    {
        int start = vh.currentVertCount;
        vh.AddVert(center, tint, Vector2.zero);
        for (int i = 0; i <= 24; i++)
        {
            float angle = i * Mathf.PI / 12;
            vh.AddVert(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, tint, Vector2.zero);
            if (i > 0) vh.AddTriangle(start, start + i, start + i + 1);
        }
    }
}
