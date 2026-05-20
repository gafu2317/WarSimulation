using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using WarSimulation.Combat.Map;

public sealed class MountainPhaseTests
{
    [Test]
    public void Execute_PlacesLargeMountainAndSmallMountainsWithMetadata()
    {
        HeightStampShape large = CreateHeightStamp(radius: 1f, peakDelta: 3f);
        HeightStampShape small = CreateHeightStamp(radius: 1f, peakDelta: 1f);
        MapGenerationConfig config = CreateConfig(large, small);

        try
        {
            var map = new MapData(new HeightMap(20, 20, 1f), new GroundStateGrid(20, 20, 1f), seed: 1);

            new MountainPhase().Execute(map, new SystemRandom(123), config);

            Assert.That(map.Mountains.Count, Is.EqualTo(3));
            Assert.That(map.Mountains.FindAll(m => m.Kind == MountainKind.Large).Count, Is.EqualTo(1));
            Assert.That(map.Mountains.FindAll(m => m.Kind == MountainKind.Small).Count, Is.EqualTo(2));
            Assert.That(map.StructureStampPlacedCount, Is.EqualTo(3));

            MountainRegion largeMountain = map.Mountains.Find(m => m.Kind == MountainKind.Large);
            Assert.That(largeMountain.Center.x, Is.EqualTo(5f).Within(0.001f));
            Assert.That(largeMountain.Center.y, Is.EqualTo(15f).Within(0.001f));

            for (int i = 0; i < map.Mountains.Count; i++)
            {
                for (int j = i + 1; j < map.Mountains.Count; j++)
                {
                    float minDistance = map.Mountains[i].Extent + map.Mountains[j].Extent;
                    Assert.That(
                        Vector2.Distance(map.Mountains[i].Center, map.Mountains[j].Center),
                        Is.GreaterThanOrEqualTo(minDistance - 0.001f));
                }
            }
        }
        finally
        {
            Object.DestroyImmediate(config);
            Object.DestroyImmediate(large);
            Object.DestroyImmediate(small);
        }
    }

    private static MapGenerationConfig CreateConfig(HeightStampShape large, HeightStampShape small)
    {
        var config = ScriptableObject.CreateInstance<MapGenerationConfig>();
        SetPrivateField(config, "_worldSize", 20f);
        SetPrivateField(config, "_largeMountainShape", large);
        SetPrivateField(config, "_largeMountainCandidatePositionsNormalized", new List<Vector2>
        {
            new Vector2(0.25f, 0.75f),
        });
        SetPrivateField(config, "_smallMountainStampEntries", new List<StructureStampEntry>
        {
            new StructureStampEntry { Count = 2, Shape = small },
        });
        SetPrivateField(config, "_mountainPlacementMargin", 2f);
        SetPrivateField(config, "_mountainMaxPlacementAttempts", 200);
        SetPrivateField(config, "_mountainMaxGlobalSearchIterations", 1000);
        SetPrivateField(config, "_mountainMinCenterSeparation", 0f);
        SetPrivateField(config, "_mountainMinCenterDistanceFactor", 1f);
        return config;
    }

    private static HeightStampShape CreateHeightStamp(float radius, float peakDelta)
    {
        var shape = ScriptableObject.CreateInstance<HeightStampShape>();
        SetPrivateField(shape, "_radius", radius);
        SetPrivateField(shape, "_peakDelta", peakDelta);
        SetPrivateField(shape, "_noiseAmplitude", 0f);
        SetPrivateField(shape, "_flatTopRatio", 0f);
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
