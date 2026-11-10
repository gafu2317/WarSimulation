using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using WarSimulation.CountryComparison.Editor;

public sealed class CountryMeshBatchTests
{
    GameObject city, building;
    Mesh mesh;
    Material material, otherMaterial;
    readonly List<Object> objects = new();

    [SetUp]
    public void SetUp()
    {
        city = new GameObject("City");
        building = new GameObject("Building");
        building.transform.SetParent(city.transform, false);
        mesh = new Mesh
        {
            vertices = new[] { Vector3.zero, Vector3.right, Vector3.up },
            triangles = new[] { 0, 1, 2 },
            uv = new[] { Vector2.zero, Vector2.right, Vector2.up }
        };
        mesh.RecalculateNormals();
        material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        otherMaterial = new Material(material);
        objects.AddRange(new Object[] { mesh, material, otherMaterial });
    }

    [TearDown]
    public void TearDown()
    {
        CountryMeshBatchBuilder.Restore(city.transform);
        Object.DestroyImmediate(city);
        foreach (var item in objects) Object.DestroyImmediate(item);
        objects.Clear();
    }

    [Test]
    public void Combining_PreservesWorldGeometryUvAndSourceAssets()
    {
        building.transform.SetPositionAndRotation(new Vector3(23, 7, -11), Quaternion.Euler(17, 51, 0));
        building.transform.localScale = new Vector3(2, 3, .7f);
        var first = Part("First", new Vector3(1, 2, 3));
        var second = Part("Second", new Vector3(-3, 1, 2));
        second.transform.localRotation = Quaternion.Euler(11, 31, 27);
        second.transform.localScale = new Vector3(.5f, 2, 3);
        var expected = new[] { first, second }.SelectMany(r => mesh.vertices.Select(r.transform.TransformPoint)).ToArray();
        var originalVertices = mesh.vertices;
        var originalTriangles = mesh.triangles;
        var originalUv = mesh.uv;

        Assert.That(CountryMeshBatchBuilder.BakeObject(building), Is.EqualTo(1));

        var renderer = Outputs().Single();
        var combined = renderer.GetComponent<MeshFilter>().sharedMesh;
        var actual = combined.vertices.Select(renderer.transform.TransformPoint).ToArray();
        Assert.That(actual.Length, Is.EqualTo(expected.Length));
        for (var i = 0; i < actual.Length; i++) Assert.That(Vector3.Distance(actual[i], expected[i]), Is.LessThan(.0001f));
        Assert.That(combined.uv, Is.EqualTo(originalUv.Concat(originalUv).ToArray()));
        Assert.That(renderer.sharedMaterials, Is.EqualTo(new[] { material }));
        Assert.That(mesh.vertices, Is.EqualTo(originalVertices));
        Assert.That(mesh.triangles, Is.EqualTo(originalTriangles));
        Assert.That(mesh.uv, Is.EqualTo(originalUv));
        Assert.That(first.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(mesh));
        Assert.That(first.sharedMaterial, Is.SameAs(material));
        Assert.That(first.enabled || second.enabled, Is.False);
    }

    [Test]
    public void HiddenCity_CombinesVisiblePartsAndKeepsExcludedPartsUnchanged()
    {
        var first = Part("First", Vector3.zero);
        var second = Part("Second", Vector3.right);
        var disabled = Part("Disabled", Vector3.up); disabled.enabled = false;
        var inactive = Part("Inactive", Vector3.down); inactive.gameObject.SetActive(false);
        var transparent = Part("Transparent", Vector3.forward, otherMaterial); otherMaterial.renderQueue = 3000;
        city.SetActive(false);

        CountryMeshBatchBuilder.BakeObject(building);

        Assert.That(building.GetComponentInChildren<CountryMeshBatch>(true).sources, Is.EquivalentTo(new[] { first, second }));
        Assert.That(Outputs().Length, Is.EqualTo(1));
        CountryMeshBatchBuilder.Restore(city.transform);
        Assert.That(first.enabled && second.enabled, Is.True);
        Assert.That(disabled.enabled, Is.False);
        Assert.That(inactive.gameObject.activeSelf, Is.False);
        Assert.That(transparent.enabled, Is.True);
        Assert.That(city.activeSelf, Is.False);
    }

    [Test]
    public void NonUniformScale_PreservesLightingVectorsAndCombinedBounds()
    {
        mesh.vertices = new[] { Vector3.zero, new Vector3(1, .2f, .3f), new Vector3(-.1f, 1, .4f) };
        mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
        building.transform.SetPositionAndRotation(new Vector3(17, 3, -5), Quaternion.Euler(12, 34, 9));
        building.transform.localScale = new Vector3(2, .7f, 3);
        var first = Part("First", Vector3.zero);
        var second = Part("Second", Vector3.right * 20);
        second.transform.localRotation = Quaternion.Euler(19, 37, 28);
        second.transform.localScale = new Vector3(.4f, 3, 1.7f);
        first.localBounds = new Bounds(Vector3.zero, Vector3.one * .1f);
        var sources = new[] { first, second };
        var normals = sources.SelectMany(r => mesh.normals.Select(n => r.transform.worldToLocalMatrix.transpose.MultiplyVector(n).normalized)).ToArray();
        var tangents = sources.SelectMany(r => mesh.tangents.Select(t => r.transform.localToWorldMatrix.MultiplyVector(new Vector3(t.x, t.y, t.z)).normalized)).ToArray();
        var positions = sources.SelectMany(r => mesh.vertices.Select(r.transform.TransformPoint)).ToArray();

        CountryMeshBatchBuilder.BakeObject(building);

        var output = Outputs().Single();
        var combined = output.GetComponent<MeshFilter>().sharedMesh;
        for (var i = 0; i < combined.vertexCount; i++)
        {
            var normal = output.transform.worldToLocalMatrix.transpose.MultiplyVector(combined.normals[i]).normalized;
            var t = combined.tangents[i];
            var tangent = output.transform.localToWorldMatrix.MultiplyVector(new Vector3(t.x, t.y, t.z)).normalized;
            Assert.That(Vector3.Dot(normal, normals[i]), Is.GreaterThan(.9999f));
            Assert.That(Vector3.Dot(tangent, tangents[i]), Is.GreaterThan(.9999f));
            var bounds = output.bounds; bounds.Expand(.001f);
            Assert.That(bounds.Contains(positions[i]), Is.True);
        }
    }

    [Test]
    public void RendererOverridesAndMirroredParts_StayOutsideTheBatch()
    {
        var first = Part("First", Vector3.zero);
        var second = Part("Second", Vector3.right);
        var overridden = Part("Overrides", Vector3.up);
        var properties = new MaterialPropertyBlock(); properties.SetColor("_BaseColor", Color.red);
        overridden.SetPropertyBlock(properties);
        var mirrored = Part("Mirrored", Vector3.down); mirrored.transform.localScale = new Vector3(-1, 1, 1);
        var lightmapped = Part("Lightmapped", Vector3.forward); lightmapped.lightmapIndex = 0;

        CountryMeshBatchBuilder.BakeObject(building);

        Assert.That(building.GetComponentInChildren<CountryMeshBatch>().sources, Is.EquivalentTo(new[] { first, second }));
        Assert.That(overridden.enabled && mirrored.enabled && lightmapped.enabled, Is.True);
        var actual = new MaterialPropertyBlock(); overridden.GetPropertyBlock(actual);
        Assert.That(actual.GetColor("_BaseColor"), Is.EqualTo(Color.red));
    }

    [Test]
    public void MultipleSubmeshes_PreserveEachMaterialWithoutExtraPasses()
    {
        var split = Object.Instantiate(mesh);
        split.subMeshCount = 2;
        split.SetTriangles(new[] { 0, 1, 2 }, 0);
        split.SetTriangles(new[] { 2, 1, 0 }, 1);
        objects.Add(split);
        for (var i = 0; i < 3; i++)
        {
            var renderer = Part("Split " + i, Vector3.right * i);
            renderer.GetComponent<MeshFilter>().sharedMesh = split;
            renderer.sharedMaterials = new[] { material, otherMaterial };
        }

        CountryMeshBatchBuilder.BakeObject(building);

        var outputs = Outputs();
        Assert.That(outputs.Length, Is.EqualTo(2));
        Assert.That(outputs.Select(r => r.sharedMaterial), Is.EquivalentTo(new[] { material, otherMaterial }));
        Assert.That(outputs.All(r => r.sharedMaterials.Length == 1), Is.True);
        Assert.That(outputs.All(r => r.GetComponent<MeshFilter>().sharedMesh.GetIndexCount(0) == 9), Is.True);
        Assert.That(split.subMeshCount, Is.EqualTo(2));
        Assert.That(split.GetTriangles(1), Is.EqualTo(new[] { 2, 1, 0 }));
    }

    [Test]
    public void DifferentMaterialsAndShadowSettings_KeepTheirOwnRenderers()
    {
        for (var i = 0; i < 2; i++) Part("Solid " + i, Vector3.right * i);
        for (var i = 0; i < 2; i++) Part("Other " + i, Vector3.up * i, otherMaterial);
        for (var i = 0; i < 2; i++) Part("No shadow " + i, Vector3.forward * i).shadowCastingMode = ShadowCastingMode.Off;

        CountryMeshBatchBuilder.BakeObject(building);

        var outputs = Outputs();
        Assert.That(outputs.Length, Is.EqualTo(3));
        Assert.That(outputs.Count(r => r.sharedMaterial == material && r.shadowCastingMode == ShadowCastingMode.On), Is.EqualTo(1));
        Assert.That(outputs.Count(r => r.sharedMaterial == material && r.shadowCastingMode == ShadowCastingMode.Off), Is.EqualTo(1));
        Assert.That(outputs.Count(r => r.sharedMaterial == otherMaterial), Is.EqualTo(1));
        Assert.That(outputs.All(r => r.sharedMaterials.Length == 1), Is.True);
    }

    [Test]
    public void ReapplyingAndRestoring_NeverDuplicatesGeometryOrEnablesOtherSources()
    {
        var first = Part("First", Vector3.zero);
        var second = Part("Second", Vector3.right);
        var disabled = Part("Disabled", Vector3.up); disabled.enabled = false;
        CountryMeshBatchBuilder.BakeObject(building);
        var previous = Outputs().Single().GetComponent<MeshFilter>().sharedMesh;

        CountryMeshBatchBuilder.BakeObject(building);

        Assert.That(previous == null, Is.True);
        Assert.That(building.GetComponentsInChildren<CountryMeshBatch>().Length, Is.EqualTo(1));
        Assert.That(Outputs().Single().GetComponent<MeshFilter>().sharedMesh.triangles.Length, Is.EqualTo(6));
        Assert.That(first.enabled || second.enabled || disabled.enabled, Is.False);
        CountryMeshBatchBuilder.Restore(city.transform);
        CountryMeshBatchBuilder.Restore(city.transform);
        Assert.That(first.enabled && second.enabled, Is.True);
        Assert.That(disabled.enabled, Is.False);
        Assert.That(building.GetComponentInChildren<CountryMeshBatch>(), Is.Null);
        Assert.That(mesh.vertexCount, Is.EqualTo(3));
    }

    MeshRenderer Part(string name, Vector3 position, Material surface = null)
    {
        var part = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        part.transform.SetParent(building.transform, false);
        part.transform.localPosition = position;
        part.GetComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = part.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = surface ?? material;
        return renderer;
    }

    MeshRenderer[] Outputs() => building.GetComponentInChildren<CountryMeshBatch>(true).GetComponentsInChildren<MeshRenderer>(true);
}
