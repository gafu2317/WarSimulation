using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public sealed class CountryWorldRouteGraphic : MaskableGraphic
{
    private Vector2 _from, _to;
    public void SetRoute(Vector2 from, Vector2 to) { _from = from; _to = to; SetVerticesDirty(); }
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear(); var rect = rectTransform.rect;
        var a = rect.min + Vector2.Scale(_from, rect.size); var b = rect.min + Vector2.Scale(_to, rect.size);
        var side = new Vector2(-(b - a).y, (b - a).x).normalized * 2.5f;
        int count = Mathf.CeilToInt(Vector2.Distance(a, b) / 22);
        for (int i = 0; i < count; i++)
        {
            var start = Vector2.Lerp(a, b, (float)i / count); var end = Vector2.Lerp(a, b, (i + .55f) / count);
            int index = vh.currentVertCount;
            foreach (var point in new[] { start - side, start + side, end + side, end - side }) vh.AddVert(point, color, Vector2.zero);
            vh.AddTriangle(index, index + 1, index + 2); vh.AddTriangle(index, index + 2, index + 3);
        }
    }
}
