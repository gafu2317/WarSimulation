using UnityEngine;

[RequireComponent(typeof(CanvasRenderer))]
public sealed class CountryCircleGraphic : UnityEngine.UI.MaskableGraphic
{
    [SerializeField, Min(0)] private float _stroke;
    [SerializeField] private bool _dashed;

    public void Configure(float stroke = 0, bool dashed = false)
    { _stroke = stroke; _dashed = dashed; SetVerticesDirty(); }

    protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper mesh)
    {
        mesh.Clear();
        var rect = GetPixelAdjustedRect();
        float radius = Mathf.Min(rect.width, rect.height) * .5f;
        if (radius <= 0) return;
        float inner = Mathf.Max(0, radius - _stroke);
        const int segments = 144;
        for (int i = 0; i < segments; i++)
        {
            if (_dashed && i % 6 >= 3) continue;
            float a = i * Mathf.PI * 2 / segments, b = (i + 1) * Mathf.PI * 2 / segments;
            Vector2 first = new(Mathf.Cos(a), Mathf.Sin(a)), second = new(Mathf.Cos(b), Mathf.Sin(b));
            int offset = mesh.currentVertCount;
            if (_stroke <= 0)
            {
                Vertex(rect.center); Vertex(rect.center + first * radius); Vertex(rect.center + second * radius);
                mesh.AddTriangle(offset, offset + 1, offset + 2);
            }
            else
            {
                Vertex(rect.center + first * inner); Vertex(rect.center + first * radius);
                Vertex(rect.center + second * radius); Vertex(rect.center + second * inner);
                mesh.AddTriangle(offset, offset + 1, offset + 2); mesh.AddTriangle(offset, offset + 2, offset + 3);
            }
        }
        void Vertex(Vector2 point) => mesh.AddVert(point, color, Vector2.zero);
    }
}
