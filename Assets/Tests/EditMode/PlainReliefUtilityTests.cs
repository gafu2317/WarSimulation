using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using WarSimulation.Combat.Map;

public sealed class PlainReliefUtilityTests
{
    [Test]
    public void Apply_UsesBuildSeedForDeterministicHeights()
    {
        MapData first = CreateMap(48, 48, 1f);
        MapData repeated = CreateMap(48, 48, 1f);
        MapData changed = CreateMap(48, 48, 1f);

        PlainReliefUtility.Apply(first, 0.05f, 0.08f, 17);
        PlainReliefUtility.Apply(repeated, 0.05f, 0.08f, 17);
        PlainReliefUtility.Apply(changed, 0.05f, 0.08f, 18);

        bool seedChangedHeight = false;
        for (int z = 0; z < first.Height.Height; z++)
        {
            for (int x = 0; x < first.Height.Width; x++)
            {
                Assert.That(
                    repeated.Height.GetHeight(x, z),
                    Is.EqualTo(first.Height.GetHeight(x, z)).Within(0.000001f));
                if (Mathf.Abs(first.Height.GetHeight(x, z) - changed.Height.GetHeight(x, z)) > 0.000001f)
                    seedChangedHeight = true;
            }
        }

        Assert.That(seedChangedHeight, Is.True);
    }

    [Test]
    public void Apply_StaysWithinConfiguredAmplitude()
    {
        MapData map = CreateMap(48, 48, 1f);
        const float baseHeight = 2f;
        const float amplitude = 0.05f;
        for (int z = 0; z < map.Height.Height; z++)
        {
            for (int x = 0; x < map.Height.Width; x++)
                map.Height.SetHeight(x, z, baseHeight);
        }

        PlainReliefUtility.Apply(map, amplitude, 0.08f, 17);

        bool changed = false;
        for (int z = 0; z < map.Height.Height; z++)
        {
            for (int x = 0; x < map.Height.Width; x++)
            {
                float delta = map.Height.GetHeight(x, z) - baseHeight;
                Assert.That(Mathf.Abs(delta), Is.LessThanOrEqualTo(amplitude + 0.000001f));
                if (Mathf.Abs(delta) > 0.000001f) changed = true;
            }
        }

        Assert.That(changed, Is.True);
    }

    [Test]
    public void Apply_LeavesProtectedTerrainUnchanged()
    {
        MapData map = CreateMap(24, 24, 1f);
        map.GroundStates.SetCell(1, 1, GroundState.Swamp);
        map.GroundStates.SetCell(2, 1, GroundState.Snow);
        map.GroundStates.SetCell(3, 1, GroundState.Water);
        map.Height.CliffFaces.MarkCliff(4, 1);
        map.AddRiver(new RiverPath(
            new List<Vector2Int> { new(1, 15), new(20, 15) },
            widthMeters: 4f,
            depthMeters: 1f));
        map.AddLake(new LakeRegion(new Vector2(18.5f, 18.5f), 2f, 0f));
        map.AddMountain(new MountainRegion(
            MountainKind.Small,
            new Vector2(6.5f, 18.5f),
            2f,
            Vector2.one,
            0f,
            null));
        map.AddForestRegion(new ForestRegion(new Vector2(5.5f, 5.5f), 1.5f, 0f, 0.1f));
        map.AddFeature(new PlacedFeature(
            FeatureType.Bridge,
            new Vector3(12.5f, 0f, 12.5f),
            Quaternion.identity,
            new Vector3(2f, 1f, 4f)));

        Vector2Int[] protectedCells =
        {
            new(1, 1), new(2, 1), new(3, 1), new(4, 1),
            new(10, 14), new(18, 18), new(6, 18), new(5, 5), new(12, 11),
        };
        float[] before = new float[protectedCells.Length];
        for (int i = 0; i < protectedCells.Length; i++)
        {
            Vector2Int cell = protectedCells[i];
            before[i] = map.Height.GetHeight(cell.x, cell.y);
        }

        PlainReliefUtility.Apply(map, 0.05f, 0.08f, 17);

        for (int i = 0; i < protectedCells.Length; i++)
        {
            Vector2Int cell = protectedCells[i];
            Assert.That(map.Height.GetHeight(cell.x, cell.y), Is.EqualTo(before[i]).Within(0.000001f));
        }
    }

    [Test]
    public void Apply_KeepsRelievedPlainWithinFlatSlopeAndSpawnCount()
    {
        MapData flat = CreateMap(64, 64, 1f);
        MapData relieved = CreateMap(64, 64, 1f);
        Vector3 anchor = new(32f, 0f, 32f);
        flat.AddFeature(new PlacedFeature(FeatureType.OwnMainStone, anchor));
        relieved.AddFeature(new PlacedFeature(FeatureType.OwnMainStone, anchor));

        PlainReliefUtility.Apply(relieved, 0.05f, 0.08f, 17);

        float maxSlope = 0f;
        for (int z = 0; z < relieved.Height.Height; z++)
        {
            for (int x = 0; x < relieved.Height.Width; x++)
            {
                float slope = relieved.Height.SampleSlopeDeg(new Vector3(x + 0.5f, 0f, z + 0.5f));
                maxSlope = Mathf.Max(maxSlope, slope);
            }
        }

        Assert.That(maxSlope, Is.LessThanOrEqualTo(InitialSpawnPositionBaker.FlatCellMaxSlopeDeg));
        Assert.That(
            InitialSpawnPositionBaker.Build(flat, FeatureType.OwnMainStone).Length,
            Is.EqualTo(InitialSpawnPositionBaker.PositionsPerTeam));
        Assert.That(
            InitialSpawnPositionBaker.Build(relieved, FeatureType.OwnMainStone).Length,
            Is.EqualTo(InitialSpawnPositionBaker.PositionsPerTeam));
    }

    private static MapData CreateMap(int width, int height, float cellSize)
    {
        var map = new MapData(
            new HeightMap(width, height, cellSize),
            new GroundStateGrid(width, height, cellSize),
            seed: 1);
        for (int z = 0; z < height; z++)
        {
            for (int x = 0; x < width; x++)
                map.Height.SetHeight(x, z, 2f);
        }

        return map;
    }
}
