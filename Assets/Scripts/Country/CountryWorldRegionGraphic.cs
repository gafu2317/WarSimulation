using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public sealed class CountryWorldRegionGraphic : MaskableGraphic, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    private Vector2[] _polygon;
    private int[] _triangles;
    private float[] _edgeWidths;
    private Color _line;
    private float _width;
    private bool _hover;
    private Action _select;
    public void Bind(Vector2[] polygon, Action select) { _polygon = polygon; _triangles = CountryWorldMapGeometry.Triangulate(polygon); _select = select; SetVerticesDirty(); }
    public void SetState(Color fill, Color line, float width, float[] edgeWidths = null) { color = fill; _line = line; _width = width; _edgeWidths = edgeWidths; SetVerticesDirty(); }
    public override bool Raycast(Vector2 screen, Camera camera)
    {
        if (_polygon == null || !base.Raycast(screen, camera) || !RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screen, camera, out var point)) return false;
        var rect = rectTransform.rect;
        return CountryWorldMapData.Contains(_polygon, new Vector2((point.x - rect.xMin) / rect.width, (point.y - rect.yMin) / rect.height));
    }
    public void OnPointerClick(PointerEventData data) { if (data.button == PointerEventData.InputButton.Left) _select?.Invoke(); }
    public void OnPointerEnter(PointerEventData data) { _hover = true; SetVerticesDirty(); }
    public void OnPointerExit(PointerEventData data) { _hover = false; SetVerticesDirty(); }
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear(); if (_polygon == null) return;
        var rect = rectTransform.rect;
        var points = new Vector2[_polygon.Length];
        for (int i = 0; i < points.Length; i++) points[i] = rect.min + Vector2.Scale(_polygon[i], rect.size);
        var fill = _hover ? Color.Lerp(color, Color.white, .12f) : color;
        foreach (var point in points) vh.AddVert(point, fill, Vector2.zero);
        for (int i = 0; i < _triangles.Length; i += 3) vh.AddTriangle(_triangles[i], _triangles[i + 1], _triangles[i + 2]);
        for (int i = 0; i < points.Length; i++)
        {
            var a = points[i]; var b = points[(i + 1) % points.Length];
            float width = _edgeWidths == null ? _width : _edgeWidths[i];
            var side = new Vector2(-(b - a).y, (b - a).x).normalized * (_hover ? width + .5f : width) * .5f;
            int start = vh.currentVertCount;
            foreach (var point in new[] { a - side, a + side, b + side, b - side }) vh.AddVert(point, _line, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2); vh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
