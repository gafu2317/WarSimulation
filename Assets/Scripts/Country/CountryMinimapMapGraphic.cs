using UnityEngine;

[RequireComponent(typeof(CanvasRenderer))]
public sealed class CountryMinimapMapGraphic : UnityEngine.UI.MaskableGraphic
{
    [SerializeField] private Mesh _layout;
    [SerializeField] private Mesh _modernLayout;
    [SerializeField] private CountryComparisonView _country;
    private Mesh _activeLayout;
    private Vector3[] _vertices;
    private Color32[] _colors;
    private int[] _triangles;

    public void SetLayout(Mesh layout)
    {
        _layout = layout;
        UseLayout(CurrentLayout);
    }

    public void BindModernLayout(Mesh layout, CountryComparisonView country)
    {
        _modernLayout = layout;
        _country = country;
        UseLayout(CurrentLayout);
    }

    private Mesh CurrentLayout => _modernLayout != null && _country != null && _country.viewIndex == 1 ? _modernLayout : _layout;

    private void RefreshLayout()
    {
        var layout = CurrentLayout;
        if (layout != _activeLayout) UseLayout(layout);
    }

    private void UseLayout(Mesh layout)
    {
        _activeLayout = layout;
        _vertices = layout != null ? layout.vertices : null;
        _colors = layout != null ? layout.colors32 : null;
        _triangles = layout != null ? layout.triangles : null;
        SetVerticesDirty();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        RefreshLayout();
        Canvas.preWillRenderCanvases += RefreshLayout;
    }

    protected override void OnDisable()
    {
        Canvas.preWillRenderCanvases -= RefreshLayout;
        base.OnDisable();
    }

    protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper vh)
    {
        vh.Clear();
        if (_vertices == null) return;
        Rect rect = GetPixelAdjustedRect();
        for (int i = 0; i < _vertices.Length; i++)
            vh.AddVert(new Vector3(rect.xMin + _vertices[i].x * rect.width,
                rect.yMin + _vertices[i].y * rect.height, 0), _colors[i], Vector2.zero);
        for (int i = 0; i < _triangles.Length; i += 3)
            vh.AddTriangle(_triangles[i], _triangles[i + 1], _triangles[i + 2]);
    }
}
