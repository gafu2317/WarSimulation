using UnityEngine;

[RequireComponent(typeof(CanvasRenderer))]
public sealed class CountrySoldierSilhouette : UnityEngine.UI.MaskableGraphic
{
    protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper mesh)
    {
        mesh.Clear();
        var rect = GetPixelAdjustedRect();
        float unit = Mathf.Min(rect.width, rect.height * .9f);
        var center = new Vector2(rect.center.x, rect.yMin + rect.height * .52f);
        Circle(center + Vector2.up * unit * .34f, unit * .17f);
        Quad(center + new Vector2(-.2f, -.02f) * unit, center + new Vector2(.2f, -.02f) * unit,
            center + new Vector2(.35f, -.46f) * unit, center + new Vector2(-.35f, -.46f) * unit);
        void Circle(Vector2 c, float radius)
        {
            for (int i = 0; i < 24; i++)
            {
                float a = i * Mathf.PI / 12, b = (i + 1) * Mathf.PI / 12;
                Triangle(c, c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius, c + new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * radius);
            }
        }
        void Triangle(Vector2 a, Vector2 b, Vector2 c)
        {
            int offset = mesh.currentVertCount;
            mesh.AddVert(a, color, Vector2.zero); mesh.AddVert(b, color, Vector2.zero); mesh.AddVert(c, color, Vector2.zero);
            mesh.AddTriangle(offset, offset + 1, offset + 2);
        }
        void Quad(Vector2 a, Vector2 b, Vector2 c, Vector2 d) { Triangle(a, b, c); Triangle(a, c, d); }
    }
}
