using System.Linq;
using NUnit.Framework;
using UnityEngine;
using WarSimulation.CountryComparison.Editor;

public sealed class CountryStructureRepairsTests
{
    [Test]
    public void ArenaPassage_OpensFrontAndClosesExposedSeatSides()
    {
        var source = Seats();
        var repaired = CountryStructureRepairs.OpenArenaPassage(source, Matrix4x4.identity);
        try
        {
            Assert.That(Hit(source, new Vector3(0, 1, -6), Vector3.forward, 6), Is.True);
            Assert.That(Hit(repaired, new Vector3(0, 1, -6), Vector3.forward, 6), Is.False);
            Assert.That(Hit(repaired, new Vector3(0, 1, 0), Vector3.forward, 6), Is.True);
            Assert.That(Hit(repaired, new Vector3(0, 1, -4), Vector3.right, 3), Is.True);
            Assert.That(Hit(repaired, new Vector3(0, 1, -4), Vector3.left, 3), Is.True);
            Assert.That(source.vertexCount, Is.EqualTo(16));
            Assert.That(repaired.uv.Length, Is.EqualTo(repaired.vertexCount));
        }
        finally { Object.DestroyImmediate(source); Object.DestroyImmediate(repaired); }
    }

    static Mesh Seats()
    {
        var footprint = new[] { new Vector3(-5, 0, -5), new Vector3(5, 0, -5),
            new Vector3(5, 0, 5), new Vector3(-5, 0, 5), new Vector3(-3, 0, -3),
            new Vector3(3, 0, -3), new Vector3(3, 0, 3), new Vector3(-3, 0, 3) };
        var vertices = footprint.Concat(footprint.Select(p => p + Vector3.up * 2)).ToArray();
        var mesh = new Mesh { vertices = vertices, uv = vertices.Select(p => new Vector2(p.x / 2, p.z / 2)).ToArray() };
        var triangles = new System.Collections.Generic.List<int>();
        void Face(int a, int b, int c, int d) => triangles.AddRange(new[] { a, b, c, a, c, d });
        for (var i = 0; i < 4; i++)
        {
            var next = (i + 1) % 4;
            Face(i, next, next + 8, i + 8);
            Face(i + 4, i + 12, next + 12, next + 4);
            Face(i, i + 4, next + 4, next);
            Face(i + 8, next + 8, next + 12, i + 12);
        }
        mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
        return mesh;
    }

    static bool Hit(Mesh mesh, Vector3 origin, Vector3 direction, float distance)
    {
        var vertices = mesh.vertices; var indices = mesh.triangles;
        for (var i = 0; i < indices.Length; i += 3)
        {
            var a = vertices[indices[i]]; var b = vertices[indices[i + 1]]; var c = vertices[indices[i + 2]];
            var first = b - a; var second = c - a; var cross = Vector3.Cross(direction, second);
            var determinant = Vector3.Dot(first, cross);
            if (Mathf.Abs(determinant) < .000001f) continue;
            var offset = origin - a; var u = Vector3.Dot(offset, cross) / determinant;
            if (u < 0 || u > 1) continue;
            var q = Vector3.Cross(offset, first); var v = Vector3.Dot(direction, q) / determinant;
            if (v < 0 || u + v > 1) continue;
            var t = Vector3.Dot(second, q) / determinant;
            if (t >= 0 && t <= distance) return true;
        }
        return false;
    }
}
