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
}
