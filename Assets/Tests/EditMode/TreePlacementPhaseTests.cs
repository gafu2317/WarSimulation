using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using WarSimulation.Combat.Map;

public sealed class TreePlacementPhaseTests
{
    [Test]
    public void TreeScatter_SkipsRiverCorridorOutsideWaterTag()
    {
        MapConfig config = CreateScatterConfig(treeCount: 100);

        try
        {
            MapData map = CreateMap(size: 10);
            AddHorizontalRiver(map, zCell: 5, widthMeters: 4f);

            new TreeScatterPhase().Execute(
                map,
                new SequenceRandom(0.5f, 0.55f, 0.1f, 0.1f),
                config);

            Assert.That(map.Features.Count, Is.InRange(1, 99));
            foreach (PlacedFeature feature in map.Features)
            {
                var position = feature.WorldPosition;
                Assert.That(RiverCorridorUtility.Contains(map, new Vector2(position.x, position.z)), Is.False);
            }
        }
        finally
        {
            Object.DestroyImmediate(config);
        }
    }

    [Test]
    public void TreeScatter_SkipsCliffFaceCell()
    {
        MapConfig config = CreateScatterConfig(treeCount: 100);

        try
        {
            MapData map = CreateMap(size: 10);
            map.Height.CliffFaces.MarkCliff(5, 5);

            new TreeScatterPhase().Execute(
                map,
                new SequenceRandom(0.55f, 0.55f, 0.1f, 0.1f),
                config);

            Assert.That(map.Features.Count, Is.EqualTo(99));
            foreach (PlacedFeature feature in map.Features)
                Assert.That(map.Height.SampleCliffFace(feature.WorldPosition), Is.False);
        }
        finally
        {
            Object.DestroyImmediate(config);
        }
    }

    [Test]
    public void TreeScatter_AllowsNormalNonCliffCell()
    {
        MapConfig config = CreateScatterConfig(treeCount: 1);

        try
        {
            MapData map = CreateMap(size: 10);

            new TreeScatterPhase().Execute(
                map,
                new SequenceRandom(0.25f, 0.25f),
                config);

            Assert.That(map.Features.Count, Is.EqualTo(1));
            Assert.That(map.Features[0].Type, Is.EqualTo(FeatureType.Tree));
            Assert.That(map.GroundStates.SampleAt(map.Features[0].WorldPosition), Is.EqualTo(GroundState.Normal));
            Assert.That(map.Height.SampleCliffFace(map.Features[0].WorldPosition), Is.False);
        }
        finally
        {
            Object.DestroyImmediate(config);
        }
    }

    [Test]
    public void ForestCluster_SkipsRiverCorridorCandidates()
    {
        ForestClusterStampShape forest = CreateForestStamp(
            radius: 1f,
            treeCount: 8,
            treeMinDistance: 0.5f);

        try
        {
            MapData map = CreateMap(size: 10);
            AddHorizontalRiver(map, zCell: 5, widthMeters: 4f);

            forest.Apply(map, new StampPlacement(new Vector2(5.5f, 5.5f)));

            Assert.That(map.Features, Is.Empty);
        }
        finally
        {
            Object.DestroyImmediate(forest);
        }
    }


    [TestCase(FeatureType.Tree)]
    [TestCase(FeatureType.Rock)]
    public void Scatter_UsesRemainingCandidatesWithoutRetryingOccupiedOrRejectedPositions(FeatureType type)
    {
        MapConfig config = CreateScatterConfig(treeCount: 3);
        try
        {
            SetPrivateField(config, "_rockCount", 3);
            SetPrivateField(config, "_rockMinDistance", 0f);
            SetPrivateField(config, "_rockPlacementMargin", 0f);
            MapData map = CreateMap(size: 10);
            for (int z = 0; z < 10; z++)
            for (int x = 0; x < 10; x++)
                map.GroundStates.SetCell(x, z, GroundState.Water);
            map.GroundStates.SetCell(8, 9, GroundState.Normal);
            map.GroundStates.SetCell(9, 9, GroundState.Normal);

            IMapGenerationPhase phase = type == FeatureType.Tree ? new TreeScatterPhase() : new RockPhase();
            phase.Execute(map, new SequenceRandom(0.25f), config);

            Assert.That(map.Features.Count, Is.EqualTo(2));
            Assert.That(map.Features[0].WorldPosition, Is.Not.EqualTo(map.Features[1].WorldPosition));
            foreach (PlacedFeature feature in map.Features)
            {
                Assert.That(feature.Type, Is.EqualTo(type));
                Assert.That(map.GroundStates.SampleAt(feature.WorldPosition), Is.EqualTo(GroundState.Normal));
                Assert.That(feature.WorldPosition.z, Is.EqualTo(9.25f));
            }
        }
        finally
        {
            Object.DestroyImmediate(config);
        }
    }

    private static MapData CreateMap(int size)
    {
        return new MapData(
            new HeightMap(size, size, 1f),
            new GroundStateGrid(size, size, 1f),
            seed: 1);
    }

    private static void AddHorizontalRiver(MapData map, int zCell, float widthMeters)
    {
        map.AddRiver(new RiverPath(
            new List<Vector2Int>
            {
                new(0, zCell),
                new(map.Height.Width - 1, zCell),
            },
            widthMeters,
            depthMeters: 1f,
            waterTagRatio: 0f));
    }

    private static MapConfig CreateScatterConfig(int treeCount)
    {
        var config = ScriptableObject.CreateInstance<MapConfig>();
        SetPrivateField(config, "_worldSize", 10f);
        SetPrivateField(config, "_scatterTreeCount", treeCount);
        SetPrivateField(config, "_scatterTreeMinDistance", 0f);
        SetPrivateField(config, "_scatterTreePlacementMargin", 0f);
        return config;
    }

    private static ForestClusterStampShape CreateForestStamp(
        float radius,
        int treeCount,
        float treeMinDistance)
    {
        var shape = ScriptableObject.CreateInstance<ForestClusterStampShape>();
        SetPrivateField(shape, "_radius", radius);
        SetPrivateField(shape, "_treeCount", treeCount);
        SetPrivateField(shape, "_treeMinDistance", treeMinDistance);
        SetPrivateField(shape, "_maxHeight", 0f);
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

    private sealed class SequenceRandom : IRandom
    {
        private readonly float[] _floats;
        private int _floatIndex;

        public SequenceRandom(params float[] floats)
        {
            _floats = floats;
        }

        public int NextInt(int minInclusive, int maxExclusive) => minInclusive;

        public float NextFloat()
        {
            if (_floats == null || _floats.Length == 0)
                return 0f;

            float value = _floats[Mathf.Min(_floatIndex, _floats.Length - 1)];
            _floatIndex++;
            return value;
        }
    }
}
