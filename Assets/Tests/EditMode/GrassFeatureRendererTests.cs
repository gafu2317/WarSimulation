using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using WarSimulation.Combat.Map;

public sealed class GrassFeatureRendererTests
{
    private const string PrefabDirectory = "Assets/Prefabs/Kingdom/City/Prefabs";

    [Test]
    public void FeatureRenderer_ScattersGrassOutsideWaterRegionsAndReusesTheLayout()
    {
        GameObject[] prefabs = LoadGrassPrefabs();
        GameObject host = new GameObject("GrassFeatureRendererTestHost");
        try
        {
            FeatureRenderer renderer = host.AddComponent<FeatureRenderer>();
            SetField(renderer, "_grassPrefabs", prefabs);
            SetField(renderer, "_grassCount", 40);
            SetField(renderer, "_grassMinDistance", 1.2f);
            SetField(renderer, "_grassPlacementMargin", 1f);
            SetField(renderer, "_grassPlacementRadius", 0.4f);

            MapData map = CreateMap();
            renderer.Render(map);

            Transform generated = host.transform.Find("GeneratedGrass");
            Assert.That(generated, Is.Not.Null);
            Assert.That(generated.childCount, Is.GreaterThan(0));
            NavMeshModifier modifier = generated.GetComponent<NavMeshModifier>();
            Assert.That(modifier, Is.Not.Null);
            Assert.That(modifier.ignoreFromBuild, Is.True);
            Assert.That(modifier.applyToChildren, Is.True);

            List<Vector3> firstPositions = CapturePositions(generated);
            List<Quaternion> firstRotations = CaptureRotations(generated);
            List<Vector3> firstScales = CaptureScales(generated);
            AssertGrassSites(map, generated);

            renderer.Render(map);
            generated = host.transform.Find("GeneratedGrass");
            Assert.That(CapturePositions(generated), Is.EqualTo(firstPositions));
            Assert.That(CaptureRotations(generated), Is.EqualTo(firstRotations));
            Assert.That(CaptureScales(generated), Is.EqualTo(firstScales));
        }
        finally
        {
            Object.DestroyImmediate(host);
        }
    }

    private static GameObject[] LoadGrassPrefabs()
    {
        string[] names = { "GrassShort", "GrassTuft", "GrassTall", "FernPatch" };
        var prefabs = new GameObject[names.Length];
        for (int i = 0; i < names.Length; i++)
        {
            prefabs[i] = AssetDatabase.LoadAssetAtPath<GameObject>(
                $"{PrefabDirectory}/GroundPlant_{names[i]}.prefab");
            Assert.That(prefabs[i], Is.Not.Null, names[i]);
        }

        return prefabs;
    }

    private static MapData CreateMap()
    {
        var map = new MapData(
            new HeightMap(20, 20, 1f),
            new GroundStateGrid(20, 20, 1f),
            seed: 91);
        for (int z = 5; z <= 7; z++)
        for (int x = 4; x <= 15; x++)
            map.GroundStates.SetCell(x, z, GroundState.Water);

        map.AddRiver(new RiverPath(
            new List<Vector2Int> { new(0, 11), new(19, 11) },
            widthMeters: 2.4f,
            depthMeters: 1f));
        map.AddLake(new LakeRegion(new Vector2(15f, 15f), radius: 2.5f, waterY: 0f));
        return map;
    }

    private static void AssertGrassSites(MapData map, Transform generated)
    {
        for (int i = 0; i < generated.childCount; i++)
        {
            Vector3 position = generated.GetChild(i).localPosition;
            Vector2 xz = new Vector2(position.x, position.z);
            Assert.That(map.GroundStates.SampleAt(position), Is.Not.EqualTo(GroundState.Water));
            Assert.That(RiverCorridorUtility.Contains(map, xz), Is.False);
            for (int lake = 0; lake < map.Lakes.Count; lake++)
                Assert.That(map.Lakes[lake].ContainsCarve(xz), Is.False);
            Assert.That(position.y, Is.EqualTo(map.Height.SampleAt(position)).Within(0.0001f));
        }
    }

    private static List<Vector3> CapturePositions(Transform generated)
    {
        var values = new List<Vector3>(generated.childCount);
        for (int i = 0; i < generated.childCount; i++)
            values.Add(generated.GetChild(i).localPosition);
        return values;
    }

    private static List<Quaternion> CaptureRotations(Transform generated)
    {
        var values = new List<Quaternion>(generated.childCount);
        for (int i = 0; i < generated.childCount; i++)
            values.Add(generated.GetChild(i).localRotation);
        return values;
    }

    private static List<Vector3> CaptureScales(Transform generated)
    {
        var values = new List<Vector3>(generated.childCount);
        for (int i = 0; i < generated.childCount; i++)
            values.Add(generated.GetChild(i).localScale);
        return values;
    }

    private static void SetField<T>(FeatureRenderer renderer, string fieldName, T value)
    {
        FieldInfo field = typeof(FeatureRenderer).GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, fieldName);
        field.SetValue(renderer, value);
    }
}
