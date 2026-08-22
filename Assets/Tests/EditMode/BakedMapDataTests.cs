using NUnit.Framework;
using UnityEngine;
using WarSimulation.Combat.Map;

namespace WarSimulation.Tests.EditMode
{
    public sealed class BakedMapDataTests
    {
        [Test]
        public void CaptureAndCreateRuntimeMapPreservesMapData()
        {
            MapData source = CreateMap();
            BakedMapData baked = ScriptableObject.CreateInstance<BakedMapData>();

            try
            {
                baked.Capture(source, 42);
                baked.CaptureAssaultRoutes(source.AssaultRoutes, 84);

                Assert.That(baked.IsValidFor(42), Is.True);
                Assert.That(baked.HasValidAssaultRoutes(84), Is.True);
                MapData loaded = baked.CreateRuntimeMap();

                Assert.That(loaded.Height.Width, Is.EqualTo(source.Height.Width));
                Assert.That(loaded.Height.Height, Is.EqualTo(source.Height.Height));
                Assert.That(loaded.Height.CellSize, Is.EqualTo(source.Height.CellSize));
                Assert.That(loaded.Seed, Is.EqualTo(source.Seed));
                Assert.That(loaded.BridgeFeatureExclusionMargin, Is.EqualTo(source.BridgeFeatureExclusionMargin));

                for (int z = 0; z < source.Height.Height; z++)
                {
                    for (int x = 0; x < source.Height.Width; x++)
                    {
                        Assert.That(loaded.Height.GetHeight(x, z), Is.EqualTo(source.Height.GetHeight(x, z)));
                        Assert.That(loaded.GroundStates.GetCell(x, z), Is.EqualTo(source.GroundStates.GetCell(x, z)));
                        Assert.That(loaded.Height.IsCliffFaceCell(x, z), Is.EqualTo(source.Height.IsCliffFaceCell(x, z)));
                        Assert.That(loaded.GetBiomeId(x, z), Is.EqualTo(source.GetBiomeId(x, z)));
                    }
                }

                Assert.That(loaded.Features.Count, Is.EqualTo(source.Features.Count));
                Assert.That(loaded.Features[0].Type, Is.EqualTo(source.Features[0].Type));
                Assert.That(loaded.Features[0].WorldPosition, Is.EqualTo(source.Features[0].WorldPosition));
                Assert.That(loaded.Rivers.Count, Is.EqualTo(source.Rivers.Count));
                Assert.That(loaded.Rivers[0].Cells, Is.EqualTo(source.Rivers[0].Cells));
                Assert.That(loaded.Rivers[0].WidthMeters, Is.EqualTo(source.Rivers[0].WidthMeters));
                Assert.That(loaded.Rivers[0].WaterTagRatio, Is.EqualTo(source.Rivers[0].WaterTagRatio));
                Assert.That(loaded.Lakes.Count, Is.EqualTo(source.Lakes.Count));
                Assert.That(loaded.Lakes[0].Center, Is.EqualTo(source.Lakes[0].Center));
                Assert.That(loaded.Lakes[0].WaterY, Is.EqualTo(source.Lakes[0].WaterY));
                Assert.That(loaded.Mountains.Count, Is.EqualTo(source.Mountains.Count));
                Assert.That(loaded.Mountains[0].Kind, Is.EqualTo(source.Mountains[0].Kind));
                Assert.That(loaded.ForestRegions.Count, Is.EqualTo(source.ForestRegions.Count));
                Assert.That(loaded.ForestRegions[0].Center, Is.EqualTo(source.ForestRegions[0].Center));
                Assert.That(loaded.AssaultRoutes.Count, Is.EqualTo(1));
                Assert.That(loaded.AssaultRoutes[0].RouteId, Is.EqualTo("route-main"));
                Assert.That(loaded.AssaultRoutes[0].Corners, Is.EqualTo(
                    new[] { Vector3.zero, Vector3.one }));
            }
            finally
            {
                Object.DestroyImmediate(baked);
            }
        }

        [Test]
        public void InvalidateAssaultRoutesStopsRuntimeRestoration()
        {
            MapData source = CreateMap();
            BakedMapData baked = ScriptableObject.CreateInstance<BakedMapData>();
            try
            {
                baked.Capture(source, 42);
                baked.CaptureAssaultRoutes(source.AssaultRoutes, 84);

                baked.InvalidateAssaultRoutes();

                Assert.That(baked.HasValidAssaultRoutes(84), Is.False);
                Assert.That(baked.CreateRuntimeMap().AssaultRoutes, Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(baked);
            }
        }

        [Test]
        public void RiverPath_DefaultWaterTagRatioIsNinetyPercent()
        {
            var river = new RiverPath(
                new[] { new Vector2Int(0, 0), new Vector2Int(1, 1) },
                4f,
                0.75f);

            Assert.That(river.WaterTagRatio, Is.EqualTo(0.9f).Within(0.001f));
        }

        [Test]
        public void IsValidForRejectsDifferentFingerprint()
        {
            BakedMapData baked = ScriptableObject.CreateInstance<BakedMapData>();
            try
            {
                baked.Capture(CreateMap(), 42);

                Assert.That(baked.IsValidFor(43), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(baked);
            }
        }

        [Test]
        public void CaptureBakesOrderedSpacedInitialSpawnPositions()
        {
            var map = new MapData(
                new HeightMap(20, 20, 1f),
                new GroundStateGrid(20, 20, 1f),
                seed: 7);
            map.AddFeature(new PlacedFeature(FeatureType.OwnMainStone, new Vector3(2f, 0f, 2f)));
            map.AddFeature(new PlacedFeature(FeatureType.EnemyMainStone, new Vector3(17f, 0f, 17f)));
            BakedMapData baked = ScriptableObject.CreateInstance<BakedMapData>();
            try
            {
                baked.Capture(map, 42);

                Assert.That(baked.HasValidInitialSpawnPositions(42), Is.True);
                Assert.That(
                    baked.TryGetInitialSpawnPositions(
                        FeatureType.OwnMainStone,
                        42,
                        out System.Collections.Generic.IReadOnlyList<Vector3> positions),
                    Is.True);
                Assert.That(positions.Count, Is.EqualTo(InitialSpawnPositionBaker.PositionsPerTeam));
                for (int i = 1; i < positions.Count; i++)
                {
                    float previousDistance = HorizontalDistanceSqr(positions[i - 1], new Vector3(2f, 0f, 2f));
                    float distance = HorizontalDistanceSqr(positions[i], new Vector3(2f, 0f, 2f));
                    Assert.That(distance, Is.GreaterThanOrEqualTo(previousDistance));
                    for (int p = 0; p < i; p++)
                    {
                        Assert.That(
                            HorizontalDistanceSqr(positions[i], positions[p]),
                            Is.GreaterThanOrEqualTo(
                                InitialSpawnPositionBaker.CharacterSpacingDistance *
                                InitialSpawnPositionBaker.CharacterSpacingDistance));
                    }
                }
            }
            finally
            {
                Object.DestroyImmediate(baked);
            }
        }

        [Test]
        public void RestoreRuntimeStateRestoresOnlyDirtyCellsAndAllFeatures()
        {
            MapData source = CreateMap();
            BakedMapData baked = ScriptableObject.CreateInstance<BakedMapData>();
            try
            {
                baked.Capture(source, 42);
                MapData runtime = baked.CreateRuntimeMap();
                runtime.GroundStates.SetCell(0, 1, GroundState.Swamp);
                runtime.GroundStates.SetCell(1, 1, GroundState.Water);
                runtime.SetBiomeId(2, 1, "changed");
                runtime.Features[0] = new PlacedFeature(FeatureType.Bridge, Vector3.zero);

                bool restored = baked.RestoreRuntimeState(
                    runtime,
                    new[] { new Vector2Int(0, 1) },
                    new[] { new Vector2Int(2, 1) });

                Assert.That(restored, Is.True);
                Assert.That(runtime.GroundStates.GetCell(0, 1), Is.EqualTo(GroundState.Snow));
                Assert.That(runtime.GroundStates.GetCell(1, 1), Is.EqualTo(GroundState.Water));
                Assert.That(runtime.GetBiomeId(2, 1), Is.EqualTo("test-biome"));
                Assert.That(runtime.Features[0].WorldPosition, Is.EqualTo(source.Features[0].WorldPosition));
            }
            finally
            {
                Object.DestroyImmediate(baked);
            }
        }

        private static float HorizontalDistanceSqr(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return dx * dx + dz * dz;
        }

        private static MapData CreateMap()
        {
            var height = new HeightMap(3, 2, 2f);
            var ground = new GroundStateGrid(3, 2, 2f);
            var map = new MapData(height, ground, 7)
            {
                BridgeFeatureExclusionMargin = 3.5f,
            };

            height.SetHeight(0, 0, 1.25f);
            height.SetHeight(1, 1, 8.75f);
            height.CliffFaces.MarkCliff(1, 0);
            ground.SetCell(0, 1, GroundState.Snow);
            map.SetBiomeId(2, 1, "test-biome");
            map.AddFeature(new PlacedFeature(
                FeatureType.Bridge,
                new Vector3(4f, 2f, 6f),
                Quaternion.Euler(0f, 15f, 0f),
                new Vector3(1f, 2f, 1f)));
            map.AddRiver(new RiverPath(
                new[] { new Vector2Int(0, 0), new Vector2Int(1, 1) },
                4f,
                0.75f,
                0.6f));
            map.AddLake(new LakeRegion(new Vector2(5f, 6f), 3f, 1.5f, true, 2.4f, 0.2f, 0.3f));
            map.AddMountain(new MountainRegion(
                MountainKind.Large,
                new Vector2(7f, 8f),
                9f,
                new Vector2(1.2f, 0.8f),
                0.25f,
                null));
            map.AddForestRegion(new ForestRegion(new Vector2(2f, 4f), 5f, 0.15f, 0.2f));
            map.AddAssaultRoute(new AssaultRoute(
                "route-main", "Main", new[] { Vector3.zero, Vector3.one }));
            return map;
        }
    }
}
