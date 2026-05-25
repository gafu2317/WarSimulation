using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using WarSimulation.Combat.Map;

public sealed class BridgePhaseTests
{
    [Test]
    public void Execute_SetsBridgeScaleFromRiverWidth()
    {
        MapGenerationConfig config = CreateBridgeConfig(
            bridgesPerRiver: 1,
            bridgeLengthExtraMargin: 1f,
            bridgeWidth: 1.2f,
            bridgeThickness: 0.15f);

        try
        {
            MapData map = CreateBridgeTestMap();
            map.AddRiver(new RiverPath(CreatePath(x: 3), widthMeters: 1f, depthMeters: 1f));
            map.AddRiver(new RiverPath(CreatePath(x: 7), widthMeters: 4f, depthMeters: 1f));

            new BridgePhase().Execute(map, rng: null, config);

            Assert.That(map.Features.Count, Is.EqualTo(2));
            Assert.That(map.Features[0].Type, Is.EqualTo(FeatureType.Bridge));
            Assert.That(map.Features[1].Type, Is.EqualTo(FeatureType.Bridge));

            Assert.That(map.Features[0].Scale.x, Is.EqualTo(1.2f).Within(0.001f));
            Assert.That(map.Features[0].Scale.y, Is.EqualTo(0.15f).Within(0.001f));
            Assert.That(map.Features[0].Scale.z, Is.EqualTo(2f).Within(0.001f));

            Assert.That(map.Features[1].Scale.x, Is.EqualTo(1.2f).Within(0.001f));
            Assert.That(map.Features[1].Scale.y, Is.EqualTo(0.15f).Within(0.001f));
            Assert.That(map.Features[1].Scale.z, Is.EqualTo(5f).Within(0.001f));
            Assert.That(map.Features[1].Scale.z, Is.GreaterThan(map.Features[0].Scale.z));
        }
        finally
        {
            Object.DestroyImmediate(config);
        }
    }

    [Test]
    public void ExistingPlacedFeatureConstructors_DefaultScaleToOne()
    {
        var feature = new PlacedFeature(FeatureType.Tree, new Vector3(1f, 2f, 3f));

        Assert.That(feature.Scale, Is.EqualTo(Vector3.one));
    }

    [Test]
    public void NavAreaVolumes_BridgeCutsRiverGridCellBackToWalkable()
    {
        var go = new GameObject("NavMeshBuilderTest");
        try
        {
            CombatNavMeshBuilder builder = go.AddComponent<CombatNavMeshBuilder>();
            MapData map = CreateBridgeTestMap();
            map.AddFeature(new PlacedFeature(
                FeatureType.Bridge,
                new Vector3(2.5f, 0f, 2.5f),
                Quaternion.identity,
                new Vector3(1f, 0.2f, 1f)));
            map.AddRiver(new RiverPath(
                new[] { new Vector2Int(0, 2), new Vector2Int(4, 2) },
                widthMeters: 1f,
                depthMeters: 1f));

            Transform root = RebuildAreaVolumesForTest(builder, map);

            Assert.That(root.Find("River_0_2_1"), Is.Not.Null);
            Assert.That(root.Find("River_3_2_4"), Is.Not.Null);
            Assert.That(root.Find("River_0_2_4"), Is.Null);
            Assert.That(root.Find("BridgeWalkable_0"), Is.Null);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void NavAreaVolumes_RiverOverridesForestAndSnowOverridesForest()
    {
        var go = new GameObject("NavMeshBuilderTest");
        try
        {
            CombatNavMeshBuilder builder = go.AddComponent<CombatNavMeshBuilder>();
            MapData map = CreateBridgeTestMap();
            map.AddForestRegion(new ForestRegion(
                new Vector2(2.5f, 2.5f),
                radius: 4f,
                noiseAmplitude: 0f,
                noiseFrequency: 0.18f));
            map.GroundStates.SetCell(1, 1, GroundState.Snow);
            map.AddRiver(new RiverPath(
                new[] { new Vector2Int(0, 2), new Vector2Int(4, 2) },
                widthMeters: 1f,
                depthMeters: 1f));

            Transform root = RebuildAreaVolumesForTest(builder, map);

            Assert.That(root.Find("Snow_1_1_1"), Is.Not.Null);
            Assert.That(root.Find("River_0_2_4"), Is.Not.Null);
            Assert.That(root.Find("Forest_1_1_1"), Is.Null);
            Assert.That(root.Find("Forest_0_2_4"), Is.Null);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void NavAreaVolumes_EmitsLakeAndFrozenLakeAreas()
    {
        var go = new GameObject("NavMeshBuilderTest");
        try
        {
            CombatNavMeshBuilder builder = go.AddComponent<CombatNavMeshBuilder>();
            MapData map = CreateBridgeTestMap();
            map.AddLake(new LakeRegion(
                new Vector2(2.5f, 2.5f),
                radius: 0.6f,
                waterY: 0f,
                isFrozen: false,
                waterTaggedRadius: 0.6f));
            map.AddLake(new LakeRegion(
                new Vector2(6.5f, 2.5f),
                radius: 0.6f,
                waterY: 0f,
                isFrozen: true,
                waterTaggedRadius: 0.6f));

            Transform root = RebuildAreaVolumesForTest(builder, map);

            Assert.That(root.Find("Lake_2_2_2"), Is.Not.Null);
            Assert.That(root.Find("FrozenLake_6_2_6"), Is.Not.Null);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    private static MapData CreateBridgeTestMap()
    {
        var height = new HeightMap(12, 12, 1f);
        var ground = new GroundStateGrid(12, 12, 1f);
        return new MapData(height, ground, seed: 123);
    }

    private static Vector2Int[] CreatePath(int x) =>
        new[]
        {
            new Vector2Int(x, 1),
            new Vector2Int(x, 3),
            new Vector2Int(x, 5),
            new Vector2Int(x, 7),
        };

    private static MapGenerationConfig CreateBridgeConfig(
        int bridgesPerRiver,
        float bridgeLengthExtraMargin,
        float bridgeWidth,
        float bridgeThickness)
    {
        var config = ScriptableObject.CreateInstance<MapGenerationConfig>();
        SetPrivateField(config, "_bridgesPerRiver", bridgesPerRiver);
        SetPrivateField(config, "_bridgeLengthExtraMargin", bridgeLengthExtraMargin);
        SetPrivateField(config, "_bridgeWidth", bridgeWidth);
        SetPrivateField(config, "_bridgeThickness", bridgeThickness);
        SetPrivateField(config, "_bridgeHeightAboveWater", 0.3f);
        return config;
    }

    private static void SetPrivateField<T>(MapGenerationConfig config, string fieldName, T value)
    {
        FieldInfo field = typeof(MapGenerationConfig).GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.That(field, Is.Not.Null, $"Missing field {fieldName}");
        field.SetValue(config, value);
    }

    private static Transform RebuildAreaVolumesForTest(CombatNavMeshBuilder builder, MapData map)
    {
        MethodInfo method = typeof(CombatNavMeshBuilder).GetMethod(
            "RebuildAreaVolumes",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.That(method, Is.Not.Null);
        method.Invoke(builder, new object[] { map });

        Transform root = builder.transform.Find("GeneratedNavAreaVolumes");
        Assert.That(root, Is.Not.Null);
        return root;
    }

}
