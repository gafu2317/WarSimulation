using System.Collections.Generic;
using UnityEngine;

// One mesh keeps the separate strokes of a spell in one draw call.
public sealed class SkillVfxMesh
{
    private readonly List<Vector3> _vertices = new(4096);
    private readonly List<Color32> _colors = new(4096);
    private readonly List<Vector4> _uvs = new(4096);
    private readonly List<int> _indices = new(6144);
    public readonly Mesh Mesh = new() { name = "Skill strokes", hideFlags = HideFlags.DontSave };
    public Vector3 Right = Vector3.right;
    public Vector3 Up = Vector3.up;
    public float Opacity = 1f;
    public float MinimumStroke = .025f;
    private readonly float[] _heights = new float[81];
    private Vector3 _groundCenter;
    private float _groundRadius;
    private bool _terrain;

    public void SetGround(CombatMapSystem map, Vector3 center, float radius)
    {
        _terrain = map != null && map.CurrentMap != null;
        _groundCenter = center; _groundRadius = radius;
        if (!_terrain) return;
        for (int z = 0; z < 9; z++)
            for (int x = 0; x < 9; x++)
            {
                Vector3 p = center + new Vector3((x / 4f - 1) * radius, 0, (z / 4f - 1) * radius);
                _heights[z * 9 + x] = map.MapLocalToSurfaceWorldPosition(map.MapOrigin.InverseTransformPoint(p)).y;
            }
    }

    public Vector3 Surface(Vector3 p)
    {
        if (!_terrain) return p;
        float x = Mathf.Clamp((p.x - _groundCenter.x) / _groundRadius * 4 + 4, 0, 8);
        float z = Mathf.Clamp((p.z - _groundCenter.z) / _groundRadius * 4 + 4, 0, 8);
        int ix = Mathf.Min(7, (int)x), iz = Mathf.Min(7, (int)z);
        float a = Mathf.Lerp(_heights[iz * 9 + ix], _heights[iz * 9 + ix + 1], x - ix);
        float b = Mathf.Lerp(_heights[(iz + 1) * 9 + ix], _heights[(iz + 1) * 9 + ix + 1], x - ix);
        p.y = Mathf.Lerp(a, b, z - iz) + .1f;
        return p;
    }

    public SkillVfxMesh() => Mesh.MarkDynamic();
    public void Clear() { _vertices.Clear(); _colors.Clear(); _uvs.Clear(); _indices.Clear(); }
    public void Upload()
    {
        Mesh.Clear();
        Mesh.SetVertices(_vertices);
        Mesh.SetColors(_colors);
        Mesh.SetUVs(0, _uvs);
        Mesh.SetTriangles(_indices, 0, true);
    }

    private void Vertex(Vector3 p, Color c, Vector4 uv = default)
    {
        c.a *= Opacity;
        _vertices.Add(p);
        _colors.Add(c);
        _uvs.Add(uv);
    }

    public void Triangle(Vector3 a, Vector3 b, Vector3 c, Color color)
    {
        int start = _vertices.Count;
        Vertex(a, color); Vertex(b, color); Vertex(c, color);
        _indices.Add(start); _indices.Add(start + 1); _indices.Add(start + 2);
    }

    public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color)
    {
        int start = _vertices.Count;
        Vertex(a, color); Vertex(b, color); Vertex(c, color); Vertex(d, color);
        QuadIndices(start);
    }

    private void QuadIndices(int start)
    {
        _indices.Add(start); _indices.Add(start + 1); _indices.Add(start + 2);
        _indices.Add(start); _indices.Add(start + 2); _indices.Add(start + 3);
    }

    public void TextureQuad(Vector3 center, Vector3 right, Vector3 up, Color color, float dissolve = 0)
        => TextureQuad(center, right, up, color, dissolve, new Rect(0, 0, 1, 1), .06f);

    public void TextureQuad(Vector3 center, Vector3 right, Vector3 up, Color color,
        float dissolve, Rect region, float alphaFloor)
        => TextureQuad(center - right - up, center + right - up, center + right + up,
            center - right + up, color, dissolve, region, alphaFloor);

    public void TextureQuad(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Color color,
        float dissolve, Rect region, float alphaFloor)
    {
        Vector4 a = new(region.xMin, region.yMin, 1 + alphaFloor, dissolve);
        Vector4 b = new(region.xMax, region.yMin, 1 + alphaFloor, dissolve);
        Vector4 c = new(region.xMax, region.yMax, 1 + alphaFloor, dissolve);
        Vector4 d = new(region.xMin, region.yMax, 1 + alphaFloor, dissolve);
        int start = _vertices.Count;
        Vertex(p0, color, a); Vertex(p1, color, b); Vertex(p2, color, c); Vertex(p3, color, d);
        QuadIndices(start);
    }

    public void Stroke(Vector3 a, Vector3 b, float width, Color color, float end = 1f)
    {
        width = Mathf.Max(width, MinimumStroke * Mathf.Clamp01(width / .06f));
        Vector3 side = Vector3.Cross(b - a, Vector3.Cross(Right, Up)).normalized * width * .5f;
        Quad(a - side, a + side, b + side * end, b - side * end, color);
    }

    public void Gem(Vector3 p, float width, float height, Color color, float angle = 0)
    {
        width = Mathf.Max(width, MinimumStroke * .7f * Mathf.Clamp01(width / .05f));
        float r = angle * Mathf.Deg2Rad;
        Vector3 x = (Right * Mathf.Cos(r) + Up * Mathf.Sin(r)) * width;
        Vector3 y = (-Right * Mathf.Sin(r) + Up * Mathf.Cos(r)) * height;
        Triangle(p - x, p + y, p + x, color);
        Triangle(p - x, p + x, p - y, Color.Lerp(color, new Color(.08f, .09f, .18f, color.a), .35f));
        Triangle(p - x * .35f, p + y * .75f, p + x * .35f, Color.Lerp(color, Color.white, .7f));
    }

    public void Ring(Vector3 p, float radius, float width, Color color, float angle = 0,
        float span = 360, bool ground = true, int segments = 32, bool terrain = false)
    {
        width = Mathf.Clamp(Mathf.Max(width, MinimumStroke * Mathf.Clamp01(width / .04f)), 0, Mathf.Max(0, radius));
        Vector3 x = ground ? Vector3.right : Right;
        Vector3 y = ground ? Vector3.forward : Up;
        float a = angle * Mathf.Deg2Rad;
        Vector3 u = x * Mathf.Cos(a) + y * Mathf.Sin(a);
        for (int i = 0; i < segments; i++)
        {
            float b = (angle + span * (i + 1) / segments) * Mathf.Deg2Rad;
            Vector3 v = x * Mathf.Cos(b) + y * Mathf.Sin(b);
            Vector3 a0 = p + u * radius, b0 = p + v * radius, c0 = p + v * (radius - width), d0 = p + u * (radius - width);
            if (terrain) { a0 = Surface(a0); b0 = Surface(b0); c0 = Surface(c0); d0 = Surface(d0); }
            Quad(a0, b0, c0, d0, color);
            u = v;
        }
    }

    public void Crescent(Vector3 p, Vector3 x, Vector3 y, float radius, float width, float angle, float span, Color color)
    {
        const int count = 24;
        float a = angle * Mathf.Deg2Rad;
        Vector3 da = x * Mathf.Cos(a) + y * Mathf.Sin(a);
        float wa = 0;
        for (int i = 0; i < count; i++)
        {
            float v = (i + 1) / (float)count;
            float b = (angle + span * v) * Mathf.Deg2Rad;
            Vector3 db = x * Mathf.Cos(b) + y * Mathf.Sin(b);
            float wb = width * Mathf.Sin(v * Mathf.PI);
            Quad(p + da * radius, p + db * radius, p + db * (radius - wb), p + da * (radius - wa), color);
            da = db;
            wa = wb;
        }
    }

    public void Chevron(Vector3 p, float size, Color color, bool down = false)
    {
        Vector3 y = Up * (down ? -size : size);
        Stroke(p - Right * size, p + y, size * .23f, color);
        Stroke(p + y, p + Right * size, size * .23f, color);
    }

    public void Shield(Vector3 p, float size, Color color)
    {
        Vector3 a = p + Right * size * .65f + Up * size * .65f;
        Vector3 b = p - Right * size * .65f + Up * size * .65f;
        Vector3 c = p - Right * size * .55f - Up * size * .25f;
        Vector3 d = p - Up * size * .8f;
        Vector3 e = p + Right * size * .55f - Up * size * .25f;
        Color fill = color; fill.a *= .18f;
        Triangle(a, b, p, fill); Triangle(b, c, p, fill); Triangle(c, d, p, fill); Triangle(d, e, p, fill); Triangle(e, a, p, fill);
        Stroke(a, b, .09f * size, color); Stroke(b, c, .09f * size, color); Stroke(c, d, .09f * size, color);
        Stroke(d, e, .09f * size, color); Stroke(e, a, .09f * size, color);
        Stroke(p + Up * size * .4f, p - Up * size * .4f, .06f * size, Color.white);
        Stroke(p - Right * size * .3f, p + Right * size * .3f, .06f * size, Color.white);
    }

    public void Petal(Vector3 p, Vector3 direction, float length, float width, Color color)
    {
        Vector3 d = direction.normalized;
        Vector3 side = Vector3.Cross(d, Vector3.Cross(Right, Up)).normalized * width;
        Vector3 middle = p + d * length * .55f;
        Triangle(p, middle - side, p + d * length, color);
        Triangle(p, p + d * length, middle + side, Color.Lerp(color, Color.white, .35f));
    }
}
