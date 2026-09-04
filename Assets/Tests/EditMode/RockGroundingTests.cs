using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using WarSimulation.Combat.Map;

public sealed class RockGroundingTests
{
    private const float TreeGroundSinkDepth = 0.05f;

    [TestCase(0f, 0f)]
    [TestCase(0f, 3f)]
    [TestCase(1f, 3f)]
    public void GeneratedRocks_StayOnFlatGroundAndOnlyCompensateTerrainDrop(float slope, float elevation)
    {
        var host = new GameObject("RockGroundingTests");
        host.transform.position = new Vector3(17f, -3f, 11f);
        var terrainRenderer = host.AddComponent<TerrainRenderer>();
        var renderer = host.AddComponent<FeatureRenderer>();
        MapData map = CreateMap(slope, elevation);
        terrainRenderer.Render(map);
        TerrainCollider ground = terrainRenderer.Terrain.GetComponent<TerrainCollider>();
        try
        {
            foreach (int variant in new[] { 1, 2, 4, 8, 7, 11 })
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    $"Assets/Prefabs/Environment/NaturalRocks/NaturalRock_{variant:00}.prefab");
                SetField(renderer, "_rockPrefabs", Enumerable.Repeat(prefab, 6).ToArray());
                SetField(renderer, "_enableRockGrounding", false);
                renderer.Render(map);
                Transform rock = host.transform.Find("GeneratedFeatures/Rock_0");
                Vector3 original = rock.localPosition;
                Quaternion rotation = rock.localRotation;
                Vector3 scale = rock.localScale;
                string[] meshes = MeshNames(rock);
                Physics.SyncTransforms();
                float[] drops = MeasureTerrainDrops(rock, ground, host.transform, map.Height.CellSize);
                Vector3[] colliderCenters = rock.GetComponentsInChildren<Collider>().Select(c => c.bounds.center).ToArray();

                SetField(renderer, "_enableRockGrounding", true);
                renderer.Render(map);
                rock = host.transform.Find("GeneratedFeatures/Rock_0");
                Vector3 grounded = rock.localPosition;
                Assert.That(rock.parent.childCount, Is.EqualTo(1));
                Assert.That(grounded.x, Is.EqualTo(original.x));
                Assert.That(grounded.z, Is.EqualTo(original.z));
                Assert.That(grounded.y, Is.LessThanOrEqualTo(original.y));
                Assert.That(rock.localRotation, Is.EqualTo(rotation));
                Assert.That(rock.localScale, Is.EqualTo(scale));
                Assert.That(MeshNames(rock), Is.EqualTo(meshes));
                Assert.That(map.Features[0].WorldPosition, Is.EqualTo(original));
                float sink = original.y - grounded.y;
                if (slope == 0f)
                    Assert.That(sink, Is.EqualTo(TreeGroundSinkDepth), $"Flat ground: variant {variant}");
                else Assert.That(sink, Is.GreaterThan(0f), $"Slope: variant {variant}");
                Assert.That(sink, Is.EqualTo(Mathf.Max(TreeGroundSinkDepth, drops.Max())).Within(0.001f), $"Variant {variant}");
                Assert.That(drops.All(drop => drop - sink <= 0.001f), Is.True, $"Variant {variant}");
                Collider[] moved = rock.GetComponentsInChildren<Collider>();
                for (int i = 0; i < moved.Length; i++)
                {
                    Assert.That(Vector3.Distance(moved[i].bounds.center, colliderCenters[i] - Vector3.up * sink), Is.LessThan(0.001f));
                    Bounds bounds = moved[i].bounds;
                    var ray = new Ray(new Vector3(bounds.center.x, Mathf.Max(bounds.max.y, ground.bounds.max.y) + map.Height.CellSize, bounds.center.z), Vector3.down);
                    float length = ray.origin.y - Mathf.Min(bounds.min.y, ground.bounds.min.y) + map.Height.CellSize;
                    if (!moved[i].Raycast(ray, out var surface, length) ||
                        !ground.Raycast(ray, out var terrainSurface, length) || surface.point.y <= terrainSurface.point.y) continue;
                    Assert.That(Physics.Raycast(ray, out var visionHit, length, LayerMask.GetMask("VisionObstacle"), QueryTriggerInteraction.Ignore), Is.True);
                    Assert.That(visionHit.transform.IsChildOf(rock), Is.True);
                }

                renderer.Render(map);
                rock = host.transform.Find("GeneratedFeatures/Rock_0");
                Assert.That(rock.localPosition, Is.EqualTo(grounded));
                Assert.That(rock.localRotation, Is.EqualTo(rotation));
                Assert.That(rock.localScale, Is.EqualTo(scale));
                Assert.That(MeshNames(rock), Is.EqualTo(meshes));
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(terrainRenderer.Terrain.terrainData);
            UnityEngine.Object.DestroyImmediate(host);
        }
    }

    [TestCase(0f, 0f)]
    [TestCase(0f, 3f)]
    [TestCase(1f, 3f)]
    public void GeneratedTrees_SinkSlightlyOnFlatGroundAndCompensateTerrainDrop(float slope, float elevation)
    {
        var host = new GameObject("TreeGroundingTests");
        host.transform.position = new Vector3(17f, -3f, 11f);
        var terrainRenderer = host.AddComponent<TerrainRenderer>();
        var renderer = host.AddComponent<FeatureRenderer>();
        MapData map = CreateMap(slope, elevation, FeatureType.Tree);
        terrainRenderer.Render(map);
        TerrainCollider ground = terrainRenderer.Terrain.GetComponent<TerrainCollider>();
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/Environment/NaturalTrees/NaturalTree_01.prefab");
        SetField(renderer, "_treePrefabs", Enumerable.Repeat(prefab, 10).ToArray());
        try
        {
            renderer.Render(map);
            Transform tree = host.transform.Find("GeneratedFeatures/Tree_0");
            float[] drops = MeasureTerrainDrops(tree.Find("Trunk"), ground, host.transform, map.Height.CellSize);
            float sink = map.Features[0].WorldPosition.y - tree.localPosition.y;

            Assert.That(tree.localPosition.x, Is.EqualTo(map.Features[0].WorldPosition.x));
            Assert.That(tree.localPosition.z, Is.EqualTo(map.Features[0].WorldPosition.z));
            Assert.That(sink, Is.EqualTo(Mathf.Max(TreeGroundSinkDepth, drops.Max())).Within(0.001f));
            Assert.That(drops.All(drop => drop - sink <= 0.001f), Is.True);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(terrainRenderer.Terrain.terrainData);
            UnityEngine.Object.DestroyImmediate(host);
        }
    }

    [Test]
    public void RockOutsideTerrain_KeepsItsOriginalPositionAndWarns()
    {
        var host = new GameObject("RockOutsideTerrain");
        var terrainRenderer = host.AddComponent<TerrainRenderer>();
        var renderer = host.AddComponent<FeatureRenderer>();
        MapData map = CreateMap(0f);
        map.Features.Clear();
        map.AddFeature(new PlacedFeature(FeatureType.Rock, new Vector3(0f, 0f, 6f)));
        terrainRenderer.Render(map);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/Environment/NaturalRocks/NaturalRock_02.prefab");
        SetField(renderer, "_rockPrefabs", Enumerable.Repeat(prefab, 6).ToArray());
        SetField(renderer, "_enableRockGrounding", true);
        try
        {
            LogAssert.Expect(LogType.Warning,
                "[RockGrounding] Rock_0: 底面直下にTerrainがありません。位置を保持します。");
            renderer.Render(map);
            Assert.That(host.transform.Find("GeneratedFeatures/Rock_0").localPosition,
                Is.EqualTo(map.Features[0].WorldPosition));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(terrainRenderer.Terrain.terrainData);
            UnityEngine.Object.DestroyImmediate(host);
        }
    }

    private static float[] MeasureTerrainDrops(Transform rock, TerrainCollider ground, Transform mapSpace, float step)
    {
        Collider[] colliders = rock.GetComponentsInChildren<Collider>();
        Bounds bounds = colliders[0].bounds;
        foreach (Collider collider in colliders)
        {
            bounds.Encapsulate(collider.bounds);
            Assert.That(collider.isTrigger, Is.False);
            Assert.That(collider.gameObject.layer, Is.EqualTo(LayerMask.NameToLayer("VisionObstacle")));
        }
        float bottom = Mathf.Min(bounds.min.y, ground.bounds.min.y) - step;
        float top = Mathf.Max(bounds.max.y, ground.bounds.max.y) + step;
        Vector3 min = mapSpace.InverseTransformPoint(bounds.min);
        Vector3 max = mapSpace.InverseTransformPoint(bounds.max);
        var xs = new SortedSet<float> { max.x };
        var zs = new SortedSet<float> { max.z };
        for (int i = 0; i < Mathf.CeilToInt((max.x - min.x) / step); i++) xs.Add(min.x + i * step);
        for (int i = 0; i < Mathf.CeilToInt((max.z - min.z) / step); i++) zs.Add(min.z + i * step);
        foreach (Collider collider in colliders)
        {
            Vector3 center = mapSpace.InverseTransformPoint(collider.bounds.center);
            xs.Add(center.x);
            zs.Add(center.z);
        }
        Assert.That(ground.Raycast(new Ray(new Vector3(rock.position.x, top, rock.position.z), Vector3.down), out var reference, top - bottom), Is.True);
        var drops = new List<float>();
        foreach (float x in xs)
        foreach (float z in zs)
        {
            Vector3 point = mapSpace.TransformPoint(new Vector3(x, 0f, z));
            float lowest = float.PositiveInfinity;
            foreach (Collider collider in colliders)
            {
                if (collider.Raycast(new Ray(new Vector3(point.x, bottom, point.z), Vector3.up), out var hit, top - bottom))
                    lowest = Mathf.Min(lowest, hit.point.y);
            }
            if (float.IsPositiveInfinity(lowest)) continue;
            Assert.That(ground.Raycast(new Ray(new Vector3(point.x, top, point.z), Vector3.down), out var groundHit, top - bottom), Is.True);
            drops.Add(reference.point.y - groundHit.point.y);
        }
        Assert.That(drops, Is.Not.Empty);
        return drops.ToArray();
    }

    private static MapData CreateMap(
        float slope,
        float elevation = 0f,
        FeatureType featureType = FeatureType.Rock)
    {
        var height = new HeightMap(129, 129, 0.1f);
        for (int z = 0; z < height.Height; z++)
        for (int x = 0; x < height.Width; x++) height.SetHeight(x, z, elevation + x * height.CellSize * slope);
        var map = new MapData(height, new GroundStateGrid(129, 129, 0.1f), 74);
        map.AddFeature(new PlacedFeature(featureType, new Vector3(6.4f, elevation + 6.4f * slope, 6.4f)));
        return map;
    }

    private static string[] MeshNames(Transform rock) =>
        rock.GetComponentsInChildren<MeshFilter>().Select(filter => filter.sharedMesh.name).ToArray();

    private static void SetField(FeatureRenderer renderer, string name, object value) =>
        typeof(FeatureRenderer).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(renderer, value);
}
