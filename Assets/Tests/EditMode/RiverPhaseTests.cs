using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using WarSimulation.Combat.Map;

    public sealed class RiverPhaseTests
    {
    [Test]
    public void Execute_CreatesRiverThroughHeightZeroCellsAndAvoidsMountainExtent()
    {
        RiverShape riverShape = CreateRiverShape();
        MapGenerationConfig config = CreateConfig(riverShape, minPathLength: 8);

        try
        {
            var map = new MapData(new HeightMap(24, 24, 1f), new GroundStateGrid(24, 24, 1f), seed: 1);
            map.AddMountain(new MountainRegion(
                MountainKind.Large,
                new Vector2(12f, 12f),
                extent: 4f,
                scale: Vector2.one,
                rotationRad: 0f,
                shape: null));

            new RiverPhase().Execute(map, new SystemRandom(2), config);

            Assert.That(map.Rivers.Count, Is.EqualTo(1));
            Assert.That(map.Rivers[0].Cells.Count, Is.GreaterThanOrEqualTo(8));
            Assert.That(CountWaterCells(map), Is.GreaterThan(0));

            foreach (Vector2Int cell in map.Rivers[0].Cells)
            {
                Vector2 world = new Vector2(cell.x + 0.5f, cell.y + 0.5f);
                Assert.That(Vector2.Distance(world, new Vector2(12f, 12f)), Is.GreaterThan(4f));
            }
        }
        finally
        {
            Object.DestroyImmediate(config);
            Object.DestroyImmediate(riverShape);
        }
        }

        [Test]
        public void Execute_TriesMultipleAnglesForSameCenterCandidate()
        {
            RiverShape riverShape = CreateRiverShape();
            MapGenerationConfig config = CreateConfig(
                riverShape,
                minPathLength: 8,
                maxAttempts: 2,
                meanderAmplitude: 0f);

            try
            {
                MapData map = CreateHorizontalHeightZeroCorridorMap(24, z: 12);

                new RiverPhase().Execute(
                    map,
                    new SequenceRandom(0.25f, 0f, 0f),
                    config);

                Assert.That(map.Rivers.Count, Is.EqualTo(1));
                Assert.That(map.Rivers[0].Cells.Count, Is.GreaterThanOrEqualTo(8));
            }
            finally
            {
                Object.DestroyImmediate(config);
                Object.DestroyImmediate(riverShape);
            }
        }

        [Test]
        public void Execute_DoesNotFallBackToStraightPathWhenMeanderLeavesHeightZeroCells()
        {
            RiverShape riverShape = CreateRiverShape();
            MapGenerationConfig config = CreateConfig(
                riverShape,
                minPathLength: 8,
                maxAttempts: 1,
                meanderAmplitude: 100f);

            try
            {
                MapData map = CreateHorizontalHeightZeroCorridorMap(24, z: 12);

                new RiverPhase().Execute(
                    map,
                    new SequenceRandom(0f, 0f),
                    config);

                Assert.That(map.Rivers.Count, Is.EqualTo(0));
                Assert.That(CountWaterCells(map), Is.EqualTo(0));
            }
            finally
            {
                Object.DestroyImmediate(config);
                Object.DestroyImmediate(riverShape);
            }
        }

        [Test]
        public void Execute_SkipsRiverWhenNoHeightZeroEndpointExists()
        {
        RiverShape riverShape = CreateRiverShape();
        MapGenerationConfig config = CreateConfig(riverShape, minPathLength: 8);

        try
        {
            var height = new HeightMap(16, 16, 1f);
            var ground = new GroundStateGrid(16, 16, 1f);
            for (int i = 0; i < 16; i++)
            {
                height.SetHeight(i, 0, 1f);
                height.SetHeight(i, 15, 1f);
                height.SetHeight(0, i, 1f);
                height.SetHeight(15, i, 1f);
            }
            var map = new MapData(height, ground, seed: 1);

            new RiverPhase().Execute(map, new SystemRandom(2), config);

            Assert.That(map.Rivers.Count, Is.EqualTo(0));
            Assert.That(CountWaterCells(map), Is.EqualTo(0));
        }
        finally
        {
            Object.DestroyImmediate(config);
            Object.DestroyImmediate(riverShape);
        }
    }

    private static int CountWaterCells(MapData map)
    {
        int count = 0;
        for (int z = 0; z < map.GroundStates.Height; z++)
        {
            for (int x = 0; x < map.GroundStates.Width; x++)
            {
                if (map.GroundStates.GetCell(x, z) == GroundState.Water)
                    count++;
            }
        }
            return count;
        }

        private static MapData CreateHorizontalHeightZeroCorridorMap(int size, int z)
        {
            var height = new HeightMap(size, size, 1f);
            var ground = new GroundStateGrid(size, size, 1f);
            for (int cy = 0; cy < size; cy++)
            {
                for (int cx = 0; cx < size; cx++)
                    height.SetHeight(cx, cy, 1f);
            }

            for (int x = 0; x < size; x++)
                height.SetHeight(x, z, 0f);

            return new MapData(height, ground, seed: 1);
        }

        private static MapGenerationConfig CreateConfig(
            RiverShape riverShape,
            int minPathLength,
            int maxAttempts = 24,
            float centerAreaRatio = 0.8f,
            float meanderAmplitude = 0f)
        {
            var config = ScriptableObject.CreateInstance<MapGenerationConfig>();
            SetPrivateField(config, "_riverShape", riverShape);
            SetPrivateField(config, "_crossMapRiverCount", 1);
            SetPrivateField(config, "_riverMinPathLengthMeters", (float)minPathLength);
            SetPrivateField(config, "_riverMaxPathSearchAttempts", maxAttempts);
            SetPrivateField(config, "_riverExistingWaterClearance", 0f);
            SetPrivateField(config, "_riverCenterCandidateAreaRatio", centerAreaRatio);
            SetPrivateField(config, "_flatRiverMeanderAmplitude", meanderAmplitude);
            SetPrivateField(config, "_flatRiverSpineCurveBend", 0f);
            return config;
        }

    private static RiverShape CreateRiverShape()
    {
        var shape = ScriptableObject.CreateInstance<RiverShape>();
        SetPrivateField(shape, "_widthMeters", 1f);
        SetPrivateField(shape, "_depthMeters", 0.5f);
        SetPrivateField(shape, "_waterTagRatio", 1f);
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
