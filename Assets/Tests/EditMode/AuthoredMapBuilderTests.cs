using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using WarSimulation.Combat.Map;

public sealed class AuthoredMapBuilderTests
{
    [Test]
    public void Build_IsDeterministicForSameDefinition()
    {
        AuthoredMapDefinition definition = CreateDefinition();
        try
        {
            MapData a = AuthoredMapBuilder.Build(definition);
            MapData b = AuthoredMapBuilder.Build(definition);

            Assert.That(a.Mountains.Count, Is.EqualTo(b.Mountains.Count));
            Assert.That(a.Rivers.Count, Is.EqualTo(b.Rivers.Count));
            Assert.That(a.Lakes.Count, Is.EqualTo(b.Lakes.Count));
            Assert.That(a.Features.Count, Is.EqualTo(b.Features.Count));
            Assert.That(a.Height.GetHeight(5, 5), Is.EqualTo(b.Height.GetHeight(5, 5)).Within(0.0001f));
            Assert.That(CountWater(a), Is.EqualTo(CountWater(b)));
            Vector3[] rocks = a.Features.Where(f => f.Type == FeatureType.Rock).Select(f => f.WorldPosition).ToArray();
            Assert.That(rocks, Is.Not.Empty);
            Assert.That(b.Features.Where(f => f.Type == FeatureType.Rock).Select(f => f.WorldPosition), Is.EqualTo(rocks));
            definition.BuildSeed++;
            MapData c = AuthoredMapBuilder.Build(definition);
            Assert.That(c.Features.Where(f => f.Type == FeatureType.Rock).Select(f => f.WorldPosition), Is.Not.EqualTo(rocks));
        }
        finally
        {
            DestroyDefinition(definition);
        }
    }

    [Test]
    public void CaptureFeaturePlacements_PreservesGeneratedFeaturePositions()
    {
        AuthoredMapDefinition definition = CreateDefinition();
        var forest = ScriptableObject.CreateInstance<ForestClusterStampShape>();
        SetPrivateField(definition.SharedConfig, "_scatterTreeCount", 4);
        SetPrivateField(forest, "_radius", 3f);
        SetPrivateField(forest, "_treeCount", 3);
        definition.Forests.Add(new AuthoredForestPlacement
        {
            Shape = forest,
            Center = new Vector2(8f, 5f),
            Scale = Vector2.one,
        });
        try
        {
            MapData captured = AuthoredMapBuilder.CaptureFeaturePlacements(definition);
            Vector3[] expected = captured.Features.Select(f => f.WorldPosition).ToArray();

            MapData rebuilt = AuthoredMapBuilder.Build(definition);

            Assert.That(definition.HasFixedFeaturePlacements, Is.True);
            Assert.That(definition.Rocks, Has.Count.EqualTo(CountFeatures(captured, FeatureType.Rock)));
            Assert.That(definition.Trees, Has.Count.EqualTo(4));
            Assert.That(definition.Forests[0].Trees, Has.Count.EqualTo(3));
            Assert.That(rebuilt.Features.Select(f => f.WorldPosition), Is.EqualTo(expected));
        }
        finally
        {
            Object.DestroyImmediate(forest);
            DestroyDefinition(definition);
        }
    }

    [Test]
    public void Build_FixedFeaturePositionChangesGeometryFingerprint()
    {
        AuthoredMapDefinition definition = CreateDefinition();
        try
        {
            AuthoredMapBuilder.CaptureFeaturePlacements(definition);
            int before = definition.ComputeGeometryFingerprint();
            definition.Rocks[0].Center += Vector2.one;

            MapData rebuilt = AuthoredMapBuilder.Build(definition);

            Assert.That(definition.ComputeGeometryFingerprint(), Is.Not.EqualTo(before));
            Assert.That(rebuilt.Features.Any(f => f.Type == FeatureType.Rock &&
                new Vector2(f.WorldPosition.x, f.WorldPosition.z) == definition.Rocks[0].Center), Is.True);
        }
        finally
        {
            DestroyDefinition(definition);
        }
    }

    [Test]
    public void RegenerateForestTrees_ChangesOnlySelectedForestTrees()
    {
        AuthoredMapDefinition definition = CreateDefinition();
        var forest = ScriptableObject.CreateInstance<ForestClusterStampShape>();
        SetPrivateField(forest, "_radius", 3f);
        SetPrivateField(forest, "_treeCount", 1);
        definition.Forests.Add(new AuthoredForestPlacement
        {
            Shape = forest,
            Center = new Vector2(8f, 5f),
            Scale = Vector2.one,
        });
        try
        {
            AuthoredMapBuilder.CaptureFeaturePlacements(definition);
            Vector2[] rocks = definition.Rocks.Select(p => p.Center).ToArray();
            Vector2[] scatteredTrees = definition.Trees.Select(p => p.Center).ToArray();
            SetPrivateField(forest, "_treeCount", 2);

            MapData rebuilt = AuthoredMapBuilder.RegenerateForestTrees(definition, 0);

            Assert.That(definition.Rocks.Select(p => p.Center), Is.EqualTo(rocks));
            Assert.That(definition.Trees.Select(p => p.Center), Is.EqualTo(scatteredTrees));
            Assert.That(definition.Forests[0].Trees, Has.Count.EqualTo(2));
            Assert.That(CountFeatures(rebuilt, FeatureType.Tree), Is.EqualTo(2));
        }
        finally
        {
            Object.DestroyImmediate(forest);
            DestroyDefinition(definition);
        }
    }

    [Test]
    public void Build_ReservesFixedObjectsAndSeparatesTreeAndRockFootprints()
    {
        AuthoredMapDefinition definition = CreateDefinition();
        var forest = ScriptableObject.CreateInstance<ForestClusterStampShape>();
        var radii = new FeaturePlacementRadii { Rock = 2f, Tree = 0.4f, TreeCanopy = 1f, MagicStone = 1f, Clearance = 0.25f };
        SetPrivateField(definition.SharedConfig, "_placementRadii", radii);
        SetPrivateField(definition.SharedConfig, "_scatterTreeCount", 4);
        SetPrivateField(forest, "_radius", 3f);
        SetPrivateField(forest, "_treeMinDistance", 0f);
        definition.Forests.Add(new AuthoredForestPlacement { Shape = forest, Center = new Vector2(5f, 4f), Scale = Vector2.one });
        try
        {
            MapData map = AuthoredMapBuilder.Build(definition);
            Assert.That(map.Features.Any(f => f.Type == FeatureType.Tree), Is.True);
            Assert.That(map.Features.Any(f => f.Type == FeatureType.Rock), Is.True);
            Assert.That(map.ForestRegions.Count, Is.EqualTo(definition.Forests.Count));
            Assert.That(map.Features.FindLastIndex(f => f.Type == FeatureType.Rock),
                Is.LessThan(map.Features.FindIndex(f => f.Type == FeatureType.Tree)));
            foreach (PlacedFeature feature in map.Features)
            {
                if (feature.Type != FeatureType.Tree && feature.Type != FeatureType.Rock) continue;
                var position = new Vector2(feature.WorldPosition.x, feature.WorldPosition.z);
                if (feature.Type == FeatureType.Rock)
                {
                    Assert.That(position.x, Is.InRange(radii.Rock, definition.SharedConfig.WorldSize - radii.Rock));
                    Assert.That(position.y, Is.InRange(radii.Rock, definition.SharedConfig.WorldSize - radii.Rock));
                    foreach (var region in map.ForestRegions)
                        Assert.That(Vector2.Distance(position, region.Center),
                            Is.GreaterThanOrEqualTo(region.OuterRadius + radii.Rock + radii.Clearance));
                }
                float fullRadius = feature.Type == FeatureType.Tree ? radii.TreeCanopy : radii.Rock;
                Assert.That(BridgePlacementUtility.IsNearAnyBridge(map, position,
                    map.BridgeFeatureExclusionMargin + fullRadius + radii.Clearance), Is.False);
                foreach (PlacedFeature other in map.Features)
                {
                    if (other.WorldPosition == feature.WorldPosition && other.Type == feature.Type) continue;
                    if (other.Type == FeatureType.Bridge) continue;
                    bool treePair = feature.Type == FeatureType.Tree && other.Type == FeatureType.Tree;
                    float a = treePair ? radii.Tree : fullRadius;
                    float b = other.Type == FeatureType.Tree ? (treePair ? radii.Tree : radii.TreeCanopy)
                        : other.Type == FeatureType.Rock ? radii.Rock : radii.MagicStone;
                    var center = new Vector2(other.WorldPosition.x, other.WorldPosition.z);
                    Assert.That(Vector2.Distance(position, center), Is.GreaterThanOrEqualTo(a + b + radii.Clearance));
                }
            }
            foreach (AuthoredMagicStonePlacement stone in definition.MagicStones)
                Assert.That(map.Features.Any(f => f.Type == stone.Type &&
                    new Vector2(f.WorldPosition.x, f.WorldPosition.z) == stone.Center), Is.True);
            MapData repeated = AuthoredMapBuilder.Build(definition);
            Assert.That(repeated.Features.Select(f => f.WorldPosition), Is.EqualTo(map.Features.Select(f => f.WorldPosition)));
            int fingerprint = definition.ComputeGeometryFingerprint();
            radii.Rock += 1f;
            SetPrivateField(definition.SharedConfig, "_placementRadii", radii);
            Assert.That(definition.ComputeGeometryFingerprint(), Is.Not.EqualTo(fingerprint));
        }
        finally
        {
            Object.DestroyImmediate(forest);
            DestroyDefinition(definition);
        }
    }

    [Test]
    public void Build_AppliesOrderedLayersAndRegistersMetadata()
    {
        AuthoredMapDefinition definition = CreateDefinition();
        try
        {
            MapData map = AuthoredMapBuilder.Build(definition);

            Assert.That(map.Height.GetHeight(0, 0), Is.EqualTo(0f).Within(0.0001f));
            Assert.That(map.Mountains.Count, Is.EqualTo(1));
            Assert.That(map.Mountains[0].Kind, Is.EqualTo(MountainKind.Large));
            Assert.That(map.Rivers.Count, Is.EqualTo(1));
            Assert.That(map.Rivers[0].Cells.Count, Is.GreaterThanOrEqualTo(2));
            Assert.That(map.Rivers[0].WaterTagRatio, Is.EqualTo(0.9f).Within(0.001f));
            Assert.That(map.Lakes.Count, Is.EqualTo(1));
            Assert.That(map.Lakes[0].IsFrozen, Is.True);
            Assert.That(CountWater(map), Is.GreaterThan(0));

            Assert.That(CountFeatures(map, FeatureType.Bridge), Is.EqualTo(1));
            Assert.That(CountFeatures(map, FeatureType.Tree), Is.GreaterThanOrEqualTo(0));
            Assert.That(CountFeatures(map, FeatureType.Rock), Is.GreaterThan(0));
            Assert.That(CountFeatures(map, FeatureType.OwnMainStone), Is.EqualTo(1));
            Assert.That(CountFeatures(map, FeatureType.EnemyMainStone), Is.EqualTo(1));
        }
        finally
        {
            DestroyDefinition(definition);
        }
    }

    [Test]
    public void Build_ThrowsWhenSharedConfigMissing()
    {
        var definition = ScriptableObject.CreateInstance<AuthoredMapDefinition>();
        try
        {
            Assert.That(() => AuthoredMapBuilder.Build(definition), Throws.InvalidOperationException);
        }
        finally
        {
            Object.DestroyImmediate(definition);
        }
    }

    [Test]
    public void RockPhase_UsesEqualWidthCandidates()
    {
        var config = ScriptableObject.CreateInstance<MapConfig>();
        try
        {
            SetPrivateField(config, "_worldSize", 10f);
            SetPrivateField(config, "_cellsPerSide", 10);
            SetPrivateField(config, "_rockCount", 1);
            SetPrivateField(config, "_rockPlacementMargin", 0f);
            SetPrivateField(config, "_rockMinDistance", 3.5f);
            MapData map = MapDataFactory.CreateFlatMap(config, 0);

            new RockPhase().Execute(map, new SequenceRandom(0.5f), config);

            Assert.That(map.Features.Count, Is.EqualTo(1));
            Assert.That(map.Features[0].WorldPosition.x, Is.EqualTo(10f / 3f * 0.5f).Within(0.0001f));
            Assert.That(map.Features[0].WorldPosition.z, Is.EqualTo(10f / 3f * 0.5f).Within(0.0001f));
        }
        finally
        {
            Object.DestroyImmediate(config);
        }
    }

    [TestCase("forest")]
    [TestCase("lake")]
    [TestCase("river")]
    public void RockPhase_ExcludesCandidatesWhoseFootprintReachesReservedArea(string area)
    {
        var config = ScriptableObject.CreateInstance<MapConfig>();
        try
        {
            SetPrivateField(config, "_worldSize", 10f);
            SetPrivateField(config, "_cellsPerSide", 10);
            SetPrivateField(config, "_rockCount", 1);
            SetPrivateField(config, "_rockPlacementMargin", 0f);
            SetPrivateField(config, "_rockMinDistance", 3.5f);
            SetPrivateField(config, "_placementRadii", new FeaturePlacementRadii { Rock = 2f });
            MapData map = MapDataFactory.CreateFlatMap(config, 0);
            if (area == "forest")
                map.AddForestRegion(new ForestRegion(new Vector2(5f, 5f), 1f, 0f, 1f));
            else if (area == "lake")
                map.AddLake(new LakeRegion(new Vector2(5f, 5f), 1f, 0f));
            else
                map.AddRiver(new RiverPath(new List<Vector2Int> { new(0, 5), new(9, 5) },
                    widthMeters: 1f, depthMeters: 1f, waterTagRatio: 0f));

            new RockPhase().Execute(map, new SequenceRandom(0.5f), config);

            Assert.That(map.Features, Is.Empty);
        }
        finally
        {
            Object.DestroyImmediate(config);
        }
    }

    [Test]
    public void RockPhase_AllowsBaseHeightWhenDepressedTerrainSetsNegativeMinimum()
    {
        MapConfig config = ScriptableObject.CreateInstance<MapConfig>();
        try
        {
            SetPrivateField(config, "_worldSize", 10f);
            SetPrivateField(config, "_cellsPerSide", 10);
            SetPrivateField(config, "_rockCount", 1);
            SetPrivateField(config, "_rockMinDistance", 0f);
            SetPrivateField(config, "_rockPlacementMargin", 0f);
            SetPrivateField(config, "_rockTopHeightExclusionRatio", 0.3f);

            MapData map = new MapData(
                new HeightMap(10, 10, 1f),
                new GroundStateGrid(10, 10, 1f),
                seed: 1);
            map.Height.SetHeight(9, 9, -1f);

            new RockPhase().Execute(map, new SequenceRandom(0.25f, 0.25f), config);

            Assert.That(map.Features.Count, Is.EqualTo(1));
            Assert.That(map.Features[0].Type, Is.EqualTo(FeatureType.Rock));
            Assert.That(map.Features[0].WorldPosition.y, Is.EqualTo(0f).Within(0.0001f));
        }
        finally
        {
            Object.DestroyImmediate(config);
        }
    }

    [Test]
    public void Fingerprints_RouteChangeDoesNotInvalidateGeometry()
    {
        AuthoredMapDefinition definition = CreateDefinition();
        try
        {
            int geometry = definition.ComputeGeometryFingerprint();
            int routesBefore = definition.ComputeAssaultRouteFingerprint();
            definition.AssaultRoutes.Add(new AuthoredAssaultRoute(
                "manual-route", "Manual", AuthoredAssaultRouteSource.Manual));

            Assert.That(definition.ComputeGeometryFingerprint(), Is.EqualTo(geometry));
            Assert.That(definition.ComputeAssaultRouteFingerprint(), Is.Not.EqualTo(routesBefore));
        }
        finally
        {
            DestroyDefinition(definition);
        }
    }

    [Test]
    public void MigrateLegacyAssaultRoutes_RunsOnlyOnce()
    {
        AuthoredMapDefinition definition = CreateDefinition();
        try
        {
            definition.SetBakedAssaultRoutes(
                new List<AuthoredBakedAssaultRoute>
                {
                    new AuthoredBakedAssaultRoute(
                        2, true, new Vector3(3f, 0f, 4f), new Vector3(5f, 0f, 6f)),
                },
                new List<AuthoredBakedAssaultRoute>(),
                definition.ComputeGeometryFingerprint());

            Assert.That(definition.MigrateLegacyAssaultRoutes(), Is.True);
            Assert.That(definition.AssaultRoutes.Count, Is.EqualTo(1));
            Assert.That(definition.AssaultRoutes[0].Waypoints, Is.EqualTo(
                new[] { new Vector2(3f, 4f), new Vector2(5f, 6f) }));
            Assert.That(definition.MigrateLegacyAssaultRoutes(), Is.False);
            Assert.That(definition.AssaultRoutes.Count, Is.EqualTo(1));
        }
        finally
        {
            DestroyDefinition(definition);
        }
    }

    [Test]
    public void ReplaceAutomaticRoutesPreservesManualRoutes()
    {
        var manual = new AuthoredAssaultRoute(
            "manual-route", "Manual", AuthoredAssaultRouteSource.Manual);
        var oldAuto = new AuthoredAssaultRoute(
            "auto:direct", "Old", AuthoredAssaultRouteSource.Auto);
        var newAuto = new AuthoredAssaultRoute(
            "auto:bridge:0", "New", AuthoredAssaultRouteSource.Auto);

        List<AuthoredAssaultRoute> result = CombatAssaultRouteBaker.ReplaceAutomaticRoutes(
            new[] { oldAuto, manual },
            new[] { newAuto });

        Assert.That(result, Is.EqualTo(new[] { manual, newAuto }));
    }

    [Test]
    public void Build_RiverPathMeandersAwayFromStraightChord()
    {
        AuthoredMapDefinition definition = CreateDefinition();
        try
        {
            SetPrivateField(definition.SharedConfig, "_flatRiverMeanderAmplitude", 10f);
            SetPrivateField(definition.SharedConfig, "_flatRiverMeanderFrequency", 0.08f);

            MapData map = AuthoredMapBuilder.Build(definition);
            Assert.That(map.Rivers.Count, Is.EqualTo(1));

            IReadOnlyList<Vector2Int> cells = map.Rivers[0].Cells;
            Assert.That(cells.Count, Is.GreaterThan(2));

            Vector2Int a = cells[0];
            Vector2Int b = cells[cells.Count - 1];
            Vector2 ab = new Vector2(b.x - a.x, b.y - a.y);
            float abLenSq = ab.sqrMagnitude;
            Assert.That(abLenSq, Is.GreaterThan(0f));

            bool foundOffChord = false;
            for (int i = 1; i < cells.Count - 1; i++)
            {
                Vector2 ap = new Vector2(cells[i].x - a.x, cells[i].y - a.y);
                float t = Vector2.Dot(ap, ab) / abLenSq;
                Vector2 closest = new Vector2(a.x, a.y) + ab * t;
                float dist = Vector2.Distance(new Vector2(cells[i].x, cells[i].y), closest);
                if (dist > 0.75f)
                {
                    foundOffChord = true;
                    break;
                }
            }

            Assert.That(foundOffChord, Is.True, "Expected meander to leave the straight chord between endpoints.");
        }
        finally
        {
            DestroyDefinition(definition);
        }
    }

    [Test]
    public void Build_BezierControlBendsRiverAwayFromChordMidpoint()
    {
        AuthoredMapDefinition definition = CreateDefinition();
        try
        {
            // くねりを切って、ベジェ制御点だけの効果を見る
            SetPrivateField(definition.SharedConfig, "_flatRiverMeanderAmplitude", 0f);
            AuthoredRiverPlacement river = definition.Rivers[0];
            river.SetBezier(new Vector2(2f, 12f), new Vector2(12f, 20f), new Vector2(22f, 12f));

            MapData map = AuthoredMapBuilder.Build(definition);
            IReadOnlyList<Vector2Int> cells = map.Rivers[0].Cells;
            Assert.That(cells.Count, Is.GreaterThan(2));

            Vector2Int a = cells[0];
            Vector2Int b = cells[cells.Count - 1];
            Vector2 ab = new Vector2(b.x - a.x, b.y - a.y);
            float abLenSq = ab.sqrMagnitude;
            Assert.That(abLenSq, Is.GreaterThan(0f));

            bool foundNorthOfChord = false;
            for (int i = 1; i < cells.Count - 1; i++)
            {
                Vector2 ap = new Vector2(cells[i].x - a.x, cells[i].y - a.y);
                float t = Vector2.Dot(ap, ab) / abLenSq;
                Vector2 closest = new Vector2(a.x, a.y) + ab * t;
                // 制御点は +Z 側なので、経路も弦より上側に寄るはず
                if (cells[i].y - closest.y > 1.5f)
                {
                    foundNorthOfChord = true;
                    break;
                }
            }

            Assert.That(foundNorthOfChord, Is.True, "Expected bezier control to pull the river off the chord.");
        }
        finally
        {
            DestroyDefinition(definition);
        }
    }

    private static AuthoredMapDefinition CreateDefinition()
    {
        HeightStampShape mountain = CreateHeightStamp();
        LakeStampShape lake = CreateLakeStamp();
        RiverShape river = CreateRiverShape();
        MapConfig config = ScriptableObject.CreateInstance<MapConfig>();
        SetPrivateField(config, "_worldSize", 24f);
        SetPrivateField(config, "_cellsPerSide", 24);
        SetPrivateField(config, "_baseHeight", 0f);
        SetPrivateField(config, "_riverShape", river);
        SetPrivateField(config, "_bridgeWidth", 2f);
        SetPrivateField(config, "_bridgeThickness", 0.25f);
        SetPrivateField(config, "_bridgeLengthExtraMargin", 1f);
        SetPrivateField(config, "_bridgeHeightAboveWater", 0.3f);
        SetPrivateField(config, "_bridgeFeatureExclusionMargin", 1f);
        SetPrivateField(config, "_mainStonesPerSide", 1);
        SetPrivateField(config, "_scatterTreeCount", 0);
        SetPrivateField(config, "_rockCount", 8);
        SetPrivateField(config, "_rockPlacementMargin", 1f);
        SetPrivateField(config, "_rockMinDistance", 1f);

        var definition = ScriptableObject.CreateInstance<AuthoredMapDefinition>();
        definition.SharedConfig = config;
        definition.BuildSeed = 7;
        definition.Mountains.Add(new AuthoredMountainPlacement
        {
            Shape = mountain,
            Kind = MountainKind.Large,
            Center = new Vector2(6f, 18f),
            Scale = Vector2.one,
        });
        definition.Rivers.Add(new AuthoredRiverPlacement
        {
            Shape = river,
            ControlPoints = new List<Vector2>
            {
                new Vector2(2f, 12f),
                new Vector2(12f, 12f),
                new Vector2(22f, 12f),
            },
        });
        definition.Lakes.Add(new AuthoredLakePlacement
        {
            Shape = lake,
            Center = new Vector2(18f, 6f),
            Scale = Vector2.one,
            IsFrozen = true,
        });
        definition.Bridges.Add(new AuthoredBridgePlacement
        {
            Center = new Vector2(12f, 12f),
            RotationDeg = 0f,
            Scale = new Vector3(2f, 0.25f, 3f),
        });
        definition.MagicStones.Add(new AuthoredMagicStonePlacement
        {
            Type = FeatureType.OwnMainStone,
            Center = new Vector2(4f, 3f),
        });
        definition.MagicStones.Add(new AuthoredMagicStonePlacement
        {
            Type = FeatureType.EnemyMainStone,
            Center = new Vector2(20f, 20f),
        });
        return definition;
    }

    private static void DestroyDefinition(AuthoredMapDefinition definition)
    {
        if (definition == null) return;
        MapConfig config = definition.SharedConfig;
        for (int i = 0; i < definition.Mountains.Count; i++)
        {
            if (definition.Mountains[i]?.Shape != null)
                Object.DestroyImmediate(definition.Mountains[i].Shape);
        }

        for (int i = 0; i < definition.Lakes.Count; i++)
        {
            if (definition.Lakes[i]?.Shape != null)
                Object.DestroyImmediate(definition.Lakes[i].Shape);
        }

        HashSet<Object> destroyed = new();
        for (int i = 0; i < definition.Rivers.Count; i++)
        {
            RiverShape shape = definition.Rivers[i]?.Shape;
            if (shape != null && destroyed.Add(shape))
                Object.DestroyImmediate(shape);
        }

        if (config != null)
        {
            if (config.RiverShape != null && destroyed.Add(config.RiverShape))
                Object.DestroyImmediate(config.RiverShape);
            Object.DestroyImmediate(config);
        }

        Object.DestroyImmediate(definition);
    }

    private static HeightStampShape CreateHeightStamp()
    {
        var shape = ScriptableObject.CreateInstance<HeightStampShape>();
        SetPrivateField(shape, "_radius", 2f);
        SetPrivateField(shape, "_peakDelta", 2f);
        SetPrivateField(shape, "_noiseAmplitude", 0f);
        return shape;
    }

    private static LakeStampShape CreateLakeStamp()
    {
        var shape = ScriptableObject.CreateInstance<LakeStampShape>();
        SetPrivateField(shape, "_radius", 2f);
        SetPrivateField(shape, "_depthMeters", 1f);
        SetPrivateField(shape, "_noiseAmplitude", 0f);
        return shape;
    }

    private static RiverShape CreateRiverShape()
    {
        var shape = ScriptableObject.CreateInstance<RiverShape>();
        SetPrivateField(shape, "_widthMeters", 1.5f);
        SetPrivateField(shape, "_depthMeters", 0.8f);
        SetPrivateField(shape, "_waterTagRatio", 0.9f);
        return shape;
    }

    private static int CountWater(MapData map)
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

    private static int CountFeatures(MapData map, FeatureType type)
    {
        int count = 0;
        for (int i = 0; i < map.Features.Count; i++)
        {
            if (map.Features[i].Type == type)
                count++;
        }

        return count;
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
