using UnityEngine;

[RequireComponent(typeof(CanvasRenderer))]
public sealed class CountryGovernmentFold : UnityEngine.UI.MaskableGraphic
{
    protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper mesh)
    {
        mesh.Clear();
        var rect = GetPixelAdjustedRect();
        var top = new Vector2(rect.xMin, rect.yMax);
        var right = new Vector2(rect.xMax, rect.yMax);
        var bottom = new Vector2(rect.xMax, rect.yMin);
        var center = (top + bottom) * .5f + Vector2.one * rect.width * .08f;
        mesh.AddVert(top, color * .65f, Vector2.zero);
        mesh.AddVert(right, color * 1.1f, Vector2.zero);
        mesh.AddVert(bottom, color * .45f, Vector2.zero);
        mesh.AddVert(center, color, Vector2.zero);
        mesh.AddTriangle(0, 1, 3);
        mesh.AddTriangle(1, 2, 3);
    }
}
