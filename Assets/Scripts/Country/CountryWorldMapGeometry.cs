using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class CountryWorldMapGeometry
{
    private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

    public static int[] Triangulate(Vector2[] points)
    {
        if (points.Length < 3) throw new ArgumentException("Map polygon requires three points.");
        var indices = Enumerable.Range(0, points.Length).ToList(); var triangles = new List<int>();
        float area = 0;
        for (int i = 0; i < points.Length; i++) area += Cross(points[i], points[(i + 1) % points.Length]);
        if (Mathf.Abs(area) < .00000001f) throw new ArgumentException("Map polygon must have nonzero area.");
        float sign = Mathf.Sign(area);
        while (indices.Count > 3)
        {
            bool clipped = false;
            for (int i = 0; i < indices.Count; i++)
            {
                int a = indices[(i + indices.Count - 1) % indices.Count], b = indices[i], c = indices[(i + 1) % indices.Count];
                if (Cross(points[b] - points[a], points[c] - points[b]) * sign <= .00000001f) continue;
                if (indices.Any(id => id != a && id != b && id != c && Inside(points[id], points[a], points[b], points[c], sign))) continue;
                triangles.AddRange(new[] { a, b, c }); indices.RemoveAt(i); clipped = true; break;
            }
            if (!clipped) throw new ArgumentException("Map polygon must be simple with nonzero area.");
        }
        triangles.AddRange(indices); return triangles.ToArray();
    }
    private static bool Inside(Vector2 point, Vector2 a, Vector2 b, Vector2 c, float sign) =>
        Cross(b - a, point - a) * sign >= -.00000001f && Cross(c - b, point - b) * sign >= -.00000001f && Cross(a - c, point - c) * sign >= -.00000001f;
}
