using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace WarSimulation.CountryComparison.Editor
{
    public static class CountryPavingCutout
    {
        struct Vertex
        {
            public Vector3 position;
            public Vector2 uv;
            public Vertex(Vector3 position, Vector2 uv) { this.position = position; this.uv = uv; }
        }

        public static Mesh Subtract(Mesh source, Matrix4x4 localToWorld, IReadOnlyList<Vector2[]> cutouts)
        {
            var positions = source.vertices;
            var uv = source.uv;
            var triangles = source.triangles;
            var vertices = new List<Vector3>(); var texture = new List<Vector2>(); var indices = new List<int>();
            var worldToLocal = localToWorld.inverse;
            for (var i = 0; i < triangles.Length; i += 3)
            {
                var pieces = new List<List<Vertex>> { new() {
                    new Vertex(localToWorld.MultiplyPoint3x4(positions[triangles[i]]), uv[triangles[i]]),
                    new Vertex(localToWorld.MultiplyPoint3x4(positions[triangles[i+1]]), uv[triangles[i+1]]),
                    new Vertex(localToWorld.MultiplyPoint3x4(positions[triangles[i+2]]), uv[triangles[i+2]]) } };
                foreach (var cutout in cutouts)
                {
                    var remaining = new List<List<Vertex>>();
                    foreach (var piece in pieces) Remove(piece, cutout, remaining);
                    pieces = remaining;
                }
                foreach (var piece in pieces)
                    for (var j = 1; j < piece.Count - 1; j++)
                    {
                        if (Vector3.Cross(piece[j].position-piece[0].position, piece[j+1].position-piece[0].position).sqrMagnitude < 1e-10f) continue;
                        foreach (var vertex in new[] {piece[0], piece[j], piece[j+1]})
                        {
                            indices.Add(vertices.Count);
                            vertices.Add(worldToLocal.MultiplyPoint3x4(vertex.position));
                            texture.Add(vertex.uv);
                        }
                    }
            }
            var mesh = new Mesh { name = source.name + " cutouts", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(vertices); mesh.SetUVs(0, texture); mesh.SetTriangles(indices,0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }

        static void Remove(List<Vertex> polygon, Vector2[] cutout, List<List<Vertex>> remaining)
        {
            var bounds = Bounds(polygon.Select(v => new Vector2(v.position.x,v.position.z)));
            if (!bounds.Overlaps(Bounds(cutout))) { remaining.Add(polygon); return; }
            var winding = 0f;
            for (var i=0;i<cutout.Length;i++) winding += Cross(cutout[i],cutout[(i+1)%cutout.Length]);
            var sign = Mathf.Sign(winding);
            for (var i=0;i<cutout.Length && polygon.Count>=3;i++)
            {
                var a = cutout[i]; var edge = cutout[(i+1)%cutout.Length]-a;
                Split(polygon,a,edge,sign,out var inside,out var outside);
                if (outside.Count>=3) remaining.Add(outside);
                polygon = inside;
            }
        }

        static void Split(List<Vertex> polygon, Vector2 a, Vector2 edge, float sign, out List<Vertex> inside, out List<Vertex> outside)
        {
            inside = new List<Vertex>(); outside = new List<Vertex>();
            var previous = polygon[^1]; var previousDistance = Distance(previous,a,edge,sign);
            foreach (var current in polygon)
            {
                var distance = Distance(current,a,edge,sign);
                if ((distance>=0)!=(previousDistance>=0))
                {
                    var t = previousDistance/(previousDistance-distance);
                    var intersection = new Vertex(Vector3.LerpUnclamped(previous.position,current.position,t),Vector2.LerpUnclamped(previous.uv,current.uv,t));
                    inside.Add(intersection); outside.Add(intersection);
                }
                (distance>=0 ? inside : outside).Add(current);
                previous = current; previousDistance = distance;
            }
        }

        static float Distance(Vertex vertex, Vector2 a, Vector2 edge, float sign) => sign*Cross(edge,new Vector2(vertex.position.x,vertex.position.z)-a);
        static float Cross(Vector2 a, Vector2 b) => a.x*b.y-a.y*b.x;
        static Rect Bounds(IEnumerable<Vector2> points)
        {
            var array = points.ToArray();
            return Rect.MinMaxRect(array.Min(v=>v.x),array.Min(v=>v.y),array.Max(v=>v.x),array.Max(v=>v.y));
        }
    }
}
