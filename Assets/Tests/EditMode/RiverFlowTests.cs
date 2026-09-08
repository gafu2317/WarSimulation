using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using WarSimulation.Combat.Map;

public sealed class RiverFlowTests
{
    [Test]
    public void RiverEndpoints_FlowCoordinatesContinueBeyondPath()
    {
        var cells = new List<Vector2Int> { new(2, 5), new(7, 5), new(12, 5) };
        Mesh mesh = RiverMeshBuilder.Build(new RiverPath(cells, 2f, 1f), new HeightMap(16, 16, 1f));
        try
        {
            Assert.That(FlowAt(mesh, 1, 5).x, Is.EqualTo(-1.5f).Within(0.001f));
            Assert.That(FlowAt(mesh, 2, 5).x - FlowAt(mesh, 1, 5).x, Is.EqualTo(1f).Within(0.001f));
            Assert.That(FlowAt(mesh, 14, 5).x, Is.EqualTo(11.5f).Within(0.001f));
            Assert.That(FlowAt(mesh, 14, 5).x - FlowAt(mesh, 13, 5).x, Is.EqualTo(1f).Within(0.001f));
        }
        finally { Object.DestroyImmediate(mesh); }
    }

    [Test]
    public void CurvedRiver_DownstreamCoordinateIncreasesAfterBend()
    {
        var cells = new List<Vector2Int> { new(2, 2), new(10, 2), new(10, 12) };
        Mesh mesh = RiverMeshBuilder.Build(new RiverPath(cells, 2f, 1f), new HeightMap(20, 20, 1f));
        try
        {
            Vector2 before = FlowAt(mesh, 6, 3);
            Vector2 after = FlowAt(mesh, 10, 8);
            Assert.That(before.x, Is.EqualTo(3.5f).Within(0.001f));
            Assert.That(after.x, Is.EqualTo(13.5f).Within(0.001f));
            Assert.That(before.y, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(after.y, Is.EqualTo(0.5f).Within(0.001f));
        }
        finally { Object.DestroyImmediate(mesh); }
    }

    [Test]
    public void ReversedRiver_DownstreamCoordinateReversesWithoutChangingSurface()
    {
        var cells = new List<Vector2Int> { new(2, 5), new(12, 5) };
        var height = new HeightMap(16, 16, 1f);
        Mesh forward = RiverMeshBuilder.Build(new RiverPath(cells, 2f, 1f), height);
        cells.Reverse();
        Mesh reverse = RiverMeshBuilder.Build(new RiverPath(cells, 2f, 1f), height);
        try
        {
            Assert.That(reverse.vertices, Is.EqualTo(forward.vertices));
            Assert.That(FlowAt(forward, 4, 5).x, Is.LessThan(FlowAt(forward, 10, 5).x));
            Assert.That(FlowAt(reverse, 4, 5).x, Is.GreaterThan(FlowAt(reverse, 10, 5).x));
        }
        finally { Object.DestroyImmediate(forward); Object.DestroyImmediate(reverse); }
    }

    [Test]
    public void RiverMaterial_IsPersistentOpaqueAndCompiles()
    {
        Material material = Resources.Load<Material>("Combat/Map/StylizedRiver");
        Assert.That(material, Is.Not.Null);
        Assert.That(AssetDatabase.Contains(material), Is.True);
        Assert.That(material.GetTag("RenderType", false), Is.EqualTo("Opaque"));
        Assert.That(ShaderUtil.ShaderHasError(material.shader), Is.False);
        Assert.That(material.GetFloat("_FlowSpeed"), Is.Not.Zero);
    }

    private static Vector2 FlowAt(Mesh mesh, float x, float z)
    {
        var flow = new List<Vector2>();
        mesh.GetUVs(2, flow);
        Vector3[] vertices = mesh.vertices;
        for (int i = 0; i < vertices.Length; i++)
            if (vertices[i].x == x && vertices[i].z == z) return flow[i];
        Assert.Fail("Expected river surface vertex was absent.");
        return default;
    }
}
