using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using WarSimulation.Combat.Map;

public sealed class LakePhaseTests
{
    [Test]
    public void Execute_SkipsLakeWhenFootprintIsNotHeightZero()
    {
        LakeStampShape lake = CreateLakeStamp();
        MapGenerationConfig config = CreateConfig(lake);

        try
        {
            var height = new HeightMap(16, 16, 1f);
            for (int z = 0; z < height.Height; z++)
            {
                for (int x = 0; x < height.Width; x++)
                    height.SetHeight(x, z, 1f);
            }
            var map = new MapData(height, new GroundStateGrid(16, 16, 1f), seed: 1);

            new LakePhase().Execute(map, new SystemRandom(1), config);

            Assert.That(map.Lakes.Count, Is.EqualTo(0));
        }
        finally
        {
            Object.DestroyImmediate(config);
            Object.DestroyImmediate(lake);
        }
    }

    [Test]
    public void Execute_PlacesLakeOnHeightZeroFootprint()
    {
        LakeStampShape lake = CreateLakeStamp();
        MapGenerationConfig config = CreateConfig(lake);

        try
        {
            var map = new MapData(new HeightMap(16, 16, 1f), new GroundStateGrid(16, 16, 1f), seed: 1);

            new LakePhase().Execute(map, new SystemRandom(1), config);

            Assert.That(map.Lakes.Count, Is.EqualTo(1));
        }
        finally
        {
            Object.DestroyImmediate(config);
            Object.DestroyImmediate(lake);
        }
    }

    private static MapGenerationConfig CreateConfig(LakeStampShape lake)
    {
        var config = ScriptableObject.CreateInstance<MapGenerationConfig>();
        SetPrivateField(config, "_lakeStamps", new List<LakeStampShape> { lake });
        SetPrivateField(config, "_lakeCount", 1);
        SetPrivateField(config, "_lakePlacementMargin", 3f);
        SetPrivateField(config, "_lakeRiverClearance", 0f);
        SetPrivateField(config, "_lakeMaxPlacementAttempts", 10);
        return config;
    }

    private static LakeStampShape CreateLakeStamp()
    {
        var shape = ScriptableObject.CreateInstance<LakeStampShape>();
        SetPrivateField(shape, "_radius", 1f);
        SetPrivateField(shape, "_depthMeters", 0.5f);
        SetPrivateField(shape, "_noiseAmplitude", 0f);
        return shape;
    }

    private static void SetPrivateField<T>(Object target, string fieldName, T value)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.That(field, Is.Not.Null, $"Missing field {fieldName}");
        field.SetValue(target, value);
    }
}
