using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using WarSimulation.Combat.Map;

public sealed class CombatMapSystemTests
{
    [Test]
    public void TryGetTerrainInfo_ReturnsBaseTerrainInfoAtWorldPosition()
    {
        GameObject go = new GameObject("CombatMapSystem");
        try
        {
            CombatMapSystem system = go.AddComponent<CombatMapSystem>();
            MapData map = CreateTestMap();
            system.SetCurrentMap(map);

            bool found = system.TryGetTerrainInfo(new Vector3(1.25f, 99f, 1.25f), out TerrainInfo info);

            Assert.That(found, Is.True);
            Assert.That(info.IsInBounds, Is.True);
            Assert.That(info.Cell, Is.EqualTo(new Vector2Int(1, 1)));
            Assert.That(info.GroundState, Is.EqualTo(GroundState.Swamp));
            Assert.That(info.Height, Is.EqualTo(6.25f).Within(0.001f));
            Assert.That(info.SurfaceNormal.magnitude, Is.EqualTo(1f).Within(0.001f));
            Assert.That(info.IsWater, Is.False);
            Assert.That(info.IsForest, Is.True);
            Assert.That(info.BiomeId, Is.EqualTo(MapData.UnsetBiomeId));
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void TryGetTerrainInfo_ReportsOutOfBoundsButSamplesClampedEdge()
    {
        GameObject go = new GameObject("CombatMapSystem");
        try
        {
            CombatMapSystem system = go.AddComponent<CombatMapSystem>();
            MapData map = CreateTestMap();
            system.SetCurrentMap(map);

            bool found = system.TryGetTerrainInfo(new Vector3(-10f, 0f, 50f), out TerrainInfo info);

            Assert.That(found, Is.True);
            Assert.That(info.IsInBounds, Is.False);
            Assert.That(info.Cell, Is.EqualTo(new Vector2Int(0, 3)));
            Assert.That(info.GroundState, Is.EqualTo(GroundState.Water));
            Assert.That(info.Height, Is.EqualTo(12f).Within(0.001f));
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void TryGetTerrainInfo_UsesCurrentMapGroundStateAndBiome()
    {
        GameObject go = new GameObject("CombatMapSystem");
        try
        {
            CombatMapSystem system = go.AddComponent<CombatMapSystem>();
            MapData map = CreateTestMap();
            system.SetCurrentMap(map);

            Assert.That(system.SetGroundState(new Vector2Int(1, 1), GroundState.Snow), Is.True);
            Assert.That(system.SetBiomeId(new Vector2Int(1, 1), "snowstorm"), Is.True);

            bool found = system.TryGetTerrainInfo(new Vector3(1.25f, 0f, 1.25f), out TerrainInfo info);

            Assert.That(found, Is.True);
            Assert.That(info.GroundState, Is.EqualTo(GroundState.Snow));
            Assert.That(info.IsWater, Is.False);
            Assert.That(info.BiomeId, Is.EqualTo("snowstorm"));
            Assert.That(map.GroundStates.GetCell(1, 1), Is.EqualTo(GroundState.Snow));
            Assert.That(map.GetBiomeId(1, 1), Is.EqualTo("snowstorm"));
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void TryGetTerrainInfo_ReturnsCliffAndFrozenLakeFlags()
    {
        GameObject go = new GameObject("CombatMapSystem");
        try
        {
            CombatMapSystem system = go.AddComponent<CombatMapSystem>();
            MapData map = CreateTestMap();
            system.SetCurrentMap(map);

            bool found = system.TryGetTerrainInfo(new Vector3(2.5f, 0f, 2.5f), out TerrainInfo info);

            Assert.That(found, Is.True);
            Assert.That(info.Cell, Is.EqualTo(new Vector2Int(2, 2)));
            Assert.That(info.GroundState, Is.EqualTo(GroundState.Water));
            Assert.That(info.IsWater, Is.True);
            Assert.That(info.IsCliffFace, Is.True);
            Assert.That(info.IsFrozenLake, Is.True);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void TryGetTerrainInfo_ReturnsUpwardNormalOnFlatMap()
    {
        GameObject go = new GameObject("CombatMapSystem");
        try
        {
            CombatMapSystem system = go.AddComponent<CombatMapSystem>();
            var height = new HeightMap(4, 4, 1f);
            var ground = new GroundStateGrid(4, 4, 1f);
            var map = new MapData(height, ground, 123);
            system.SetCurrentMap(map);

            bool found = system.TryGetTerrainInfo(new Vector3(1.5f, 0f, 1.5f), out TerrainInfo info);

            Assert.That(found, Is.True);
            Assert.That(info.SurfaceNormal.x, Is.EqualTo(0f).Within(0.001f));
            Assert.That(info.SurfaceNormal.y, Is.EqualTo(1f).Within(0.001f));
            Assert.That(info.SurfaceNormal.z, Is.EqualTo(0f).Within(0.001f));
            Assert.That(info.SlopeDeg, Is.EqualTo(0f).Within(0.001f));
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void MapLocalToSurfaceWorldPosition_UsesHeightMapAtMapLocalXZ()
    {
        GameObject go = new GameObject("CombatMapSystem");
        try
        {
            CombatMapSystem system = go.AddComponent<CombatMapSystem>();
            var height = new HeightMap(12, 12, 1f);
            var ground = new GroundStateGrid(12, 12, 1f);
            height.SetHeight(6, 5, 4f);
            height.SetHeight(9, 9, 2f);
            system.SetCurrentMap(new MapData(height, ground, seed: 1));

            Assert.That(
                system.MapLocalToSurfaceWorldPosition(new Vector3(6f, 0f, 5f)),
                Is.EqualTo(new Vector3(6f, 4f, 5f)));
            Assert.That(
                system.MapLocalToSurfaceWorldPosition(new Vector3(9f, 0f, 9f)),
                Is.EqualTo(new Vector3(9f, 2f, 9f)));
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void TrySetStonePositionsReversed_SwapsPairedPositionsAndRestoresThem()
    {
        GameObject go = new GameObject("CombatMapSystem");
        GameObject hostObject = null;
        try
        {
            CombatMapSystem system = go.AddComponent<CombatMapSystem>();
            MapData map = CreateMapWithPairedStones();
            Quaternion ownMainRotation = Quaternion.Euler(0f, 15f, 0f);
            Vector3 ownMainScale = new Vector3(1.2f, 0.9f, 1.1f);
            map.Features[0] = new PlacedFeature(
                FeatureType.OwnMainStone,
                map.Features[0].WorldPosition,
                ownMainRotation,
                ownMainScale);
            hostObject = CreateRenderedStoneHost(map);
            SetPrivateField(system, "_mapSceneHost", hostObject.GetComponent<MapSceneHost>());
            system.SetCurrentMap(map);
            int notificationCount = 0;
            system.StonePositionsChanged += () => notificationCount++;

            Assert.That(system.IsStonePositionReversed, Is.False);
            Assert.That(system.TrySetStonePositionsReversed(true), Is.True);
            Assert.That(system.IsStonePositionReversed, Is.True);
            Assert.That(notificationCount, Is.EqualTo(1));
            Assert.That(map.Features[0].Type, Is.EqualTo(FeatureType.OwnMainStone));
            Assert.That(map.Features[1].Type, Is.EqualTo(FeatureType.EnemyMainStone));
            Assert.That(map.Features[0].WorldPosition, Is.EqualTo(new Vector3(9f, 0f, 9f)));
            Assert.That(map.Features[1].WorldPosition, Is.EqualTo(new Vector3(1f, 0f, 1f)));
            Assert.That(map.Features[0].Rotation, Is.EqualTo(ownMainRotation));
            Assert.That(map.Features[0].Scale, Is.EqualTo(ownMainScale));

            system.SetCurrentMap(map);
            Assert.That(system.IsStonePositionReversed, Is.True);
            Assert.That(map.Features[0].WorldPosition, Is.EqualTo(new Vector3(9f, 0f, 9f)));

            Assert.That(system.TrySetStonePositionsReversed(true), Is.True);
            Assert.That(notificationCount, Is.EqualTo(1));

            Assert.That(system.TrySetStonePositionsReversed(false), Is.True);
            Assert.That(system.IsStonePositionReversed, Is.False);
            Assert.That(notificationCount, Is.EqualTo(2));
            Assert.That(map.Features[0].WorldPosition, Is.EqualTo(new Vector3(1f, 0f, 1f)));
            Assert.That(map.Features[1].WorldPosition, Is.EqualTo(new Vector3(9f, 0f, 9f)));
        }
        finally
        {
            Object.DestroyImmediate(hostObject);
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void TrySetStonePositionsReversed_FailsWithoutRenderedStoneViews()
    {
        GameObject go = new GameObject("CombatMapSystem");
        try
        {
            CombatMapSystem system = go.AddComponent<CombatMapSystem>();
            MapData map = CreateMapWithPairedStones();
            system.SetCurrentMap(map);

            Assert.That(system.TrySetStonePositionsReversed(true), Is.False);
            Assert.That(system.IsStonePositionReversed, Is.False);
            Assert.That(map.Features[0].WorldPosition, Is.EqualTo(new Vector3(1f, 0f, 1f)));
            Assert.That(map.Features[1].WorldPosition, Is.EqualTo(new Vector3(9f, 0f, 9f)));
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void TrySetStonePositionsReversed_DoesNotPartiallySwapMismatchedStoneCounts()
    {
        GameObject go = new GameObject("CombatMapSystem");
        try
        {
            CombatMapSystem system = go.AddComponent<CombatMapSystem>();
            MapData map = new MapData(new HeightMap(12, 12, 1f), new GroundStateGrid(12, 12, 1f), 1);
            map.AddFeature(new PlacedFeature(FeatureType.OwnMainStone, new Vector3(1f, 0f, 1f)));
            map.AddFeature(new PlacedFeature(FeatureType.EnemyMainStone, new Vector3(9f, 0f, 9f)));
            map.AddFeature(new PlacedFeature(FeatureType.OwnMainStone, new Vector3(3f, 0f, 3f)));
            system.SetCurrentMap(map);

            Assert.That(system.TrySetStonePositionsReversed(true), Is.False);
            Assert.That(system.IsStonePositionReversed, Is.False);
            Assert.That(map.Features[0].WorldPosition, Is.EqualTo(new Vector3(1f, 0f, 1f)));
            Assert.That(map.Features[1].WorldPosition, Is.EqualTo(new Vector3(9f, 0f, 9f)));
            Assert.That(map.Features[2].WorldPosition, Is.EqualTo(new Vector3(3f, 0f, 3f)));
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void SetCurrentMap_NotifiesAfterAssigningChangedMapOnly()
    {
        GameObject go = new GameObject("CombatMapSystem");
        try
        {
            CombatMapSystem system = go.AddComponent<CombatMapSystem>();
            MapData firstMap = CreateTestMap();
            MapData secondMap = CreateTestMap();
            int notificationCount = 0;
            MapData observedMap = null;
            system.CurrentMapChanged += () =>
            {
                notificationCount++;
                observedMap = system.CurrentMap;
            };

            system.SetCurrentMap(firstMap);
            system.SetCurrentMap(secondMap);
            system.SetCurrentMap(secondMap);

            Assert.That(notificationCount, Is.EqualTo(2));
            Assert.That(observedMap, Is.SameAs(secondMap));
        }
        finally
        {
            CombatAssaultRouteCache.Invalidate();
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void AssaultRoutes_ReverseTeamAssignmentWithoutChangingBakedRoutes()
    {
        MapData map = new MapData(new HeightMap(4, 4, 1f), new GroundStateGrid(4, 4, 1f), 1);
        try
        {
            map.AddAssaultRoute(new AssaultRoute(
                "route-main",
                "Main",
                new[] { Vector3.zero, Vector3.right, Vector3.one }));
            CombatAssaultRouteCache.Invalidate();

            Assert.That(CombatAssaultRouteCache.TryHydrate(map, null), Is.True);
            IReadOnlyList<CombatAiAssaultRoute> normalAlly =
                CombatAssaultRouteCache.GetRoutes(CombatTeam.Ally, stonePositionReversed: false);
            IReadOnlyList<CombatAiAssaultRoute> reversedAlly =
                CombatAssaultRouteCache.GetRoutes(CombatTeam.Ally, stonePositionReversed: true);

            Assert.That(normalAlly[0].Corners[0], Is.EqualTo(Vector3.zero));
            Assert.That(reversedAlly[0].Corners[0], Is.EqualTo(Vector3.one));
            Assert.That(
                CombatAssaultRouteCache.GetRoutes(CombatTeam.Enemy, stonePositionReversed: false)[0].Corners[0],
                Is.EqualTo(Vector3.one));
            Assert.That(
                CombatAssaultRouteCache.GetRoutes(CombatTeam.Enemy, stonePositionReversed: true)[0].Corners[0],
                Is.EqualTo(Vector3.zero));
        }
        finally
        {
            CombatAssaultRouteCache.Invalidate();
        }
    }

    [Test]
    public void TryApplyBakedAuthoredMapRestoresFeaturesAndAssaultRoutesTogether()
    {
        GameObject systemObject = new GameObject("CombatMapSystem");
        GameObject hostObject = new GameObject("MapSceneHost");
        AuthoredMapDefinition definition = ScriptableObject.CreateInstance<AuthoredMapDefinition>();
        MapConfig config = ScriptableObject.CreateInstance<MapConfig>();
        BakedMapData bakedMap = ScriptableObject.CreateInstance<BakedMapData>();
        NavMeshData navMesh = new NavMeshData();
        try
        {
            definition.SharedConfig = config;
            definition.AssaultRoutes.Add(new AuthoredAssaultRoute(
                "route-main",
                "Main",
                AuthoredAssaultRouteSource.Manual));
            MapData source = new MapData(
                new HeightMap(4, 4, 1f),
                new GroundStateGrid(4, 4, 1f),
                seed: 7);
            source.AddFeature(new PlacedFeature(
                FeatureType.Tree,
                new Vector3(2f, 0f, 3f)));
            var bakedRoute = new AssaultRoute(
                "route-main",
                "Main",
                new[] { Vector3.zero, new Vector3(3f, 0f, 3f) });
            int geometryFingerprint = definition.ComputeGeometryFingerprint();
            bakedMap.Capture(source, geometryFingerprint);
            bakedMap.CaptureAssaultRoutes(
                new[] { bakedRoute },
                definition.ComputeAssaultRouteFingerprint());
            definition.SetBakedMapData(bakedMap);
            definition.SetBakedNavMesh(navMesh, geometryFingerprint);

            CombatMapSystem system = systemObject.AddComponent<CombatMapSystem>();
            MapSceneHost host = hostObject.AddComponent<MapSceneHost>();
            SetPrivateField(system, "_mapSceneHost", host);

            bool applied = system.TryApplyBakedAuthoredMap(
                definition,
                out MapData loaded,
                out CombatMapApplyFailure failure);

            Assert.That(applied, Is.True, failure.ToString());
            Assert.That(loaded.Features.Count, Is.EqualTo(1));
            Assert.That(loaded.Features[0].Type, Is.EqualTo(FeatureType.Tree));
            Assert.That(loaded.Features[0].WorldPosition, Is.EqualTo(new Vector3(2f, 0f, 3f)));
            Assert.That(loaded.AssaultRoutes.Count, Is.EqualTo(1));
            Assert.That(loaded.AssaultRoutes[0].Corners, Is.EqualTo(bakedRoute.Corners));
            Assert.That(
                CombatAssaultRouteCache.GetRoutes(CombatTeam.Ally, stonePositionReversed: false).Count,
                Is.EqualTo(1));
        }
        finally
        {
            CombatAssaultRouteCache.Invalidate();
            Object.DestroyImmediate(hostObject);
            Object.DestroyImmediate(systemObject);
            Object.DestroyImmediate(navMesh);
            Object.DestroyImmediate(bakedMap);
            Object.DestroyImmediate(config);
            Object.DestroyImmediate(definition);
        }
    }

    [Test]
    public void RefreshMagicStonePositions_MovesExistingViewsByFeatureIndex()
    {
        GameObject rendererObject = null;
        try
        {
            MapData map = CreateMapWithPairedStones();
            rendererObject = CreateRenderedStoneHost(map);
            FeatureRenderer renderer = rendererObject.GetComponent<FeatureRenderer>();
            MagicStone stone = rendererObject.transform
                .Find("GeneratedFeatures/Stone0")
                .GetComponent<MagicStone>();
            BoxCollider collider = stone.gameObject.AddComponent<BoxCollider>();
            CombatWorldHealthBar healthBar = stone.GetComponent<CombatWorldHealthBar>();

            map.Features[0] = new PlacedFeature(
                FeatureType.OwnMainStone,
                new Vector3(5f, 0f, 6f),
                Quaternion.Euler(0f, 30f, 0f),
                Vector3.one);

            renderer.RefreshMagicStonePositions(map);

            Assert.That(stone.transform.localPosition, Is.EqualTo(new Vector3(5f, 1.65f, 6f)));
            Assert.That(stone.transform.localRotation, Is.EqualTo(
                Quaternion.Euler(0f, 30f, 0f) * Quaternion.Euler(0f, 45f, 0f)));
            Assert.That(stone.FeatureType, Is.EqualTo(FeatureType.OwnMainStone));
            Assert.That(stone.FeatureIndex, Is.EqualTo(0));
            Assert.That(stone.GetComponent<BoxCollider>(), Is.SameAs(collider));
            Assert.That(stone.GetComponent<CombatWorldHealthBar>(), Is.SameAs(healthBar));
        }
        finally
        {
            if (rendererObject != null) Object.DestroyImmediate(rendererObject);
        }
    }

    private static MapData CreateTestMap()
    {
        var height = new HeightMap(4, 4, 1f);
        var ground = new GroundStateGrid(4, 4, 1f);
        for (int z = 0; z < 4; z++)
        {
            for (int x = 0; x < 4; x++)
            {
                height.SetHeight(x, z, x + z * 4);
            }
        }

        ground.SetCell(1, 1, GroundState.Swamp);
        ground.SetCell(0, 3, GroundState.Water);
        ground.SetCell(2, 2, GroundState.Water);
        height.CliffFaces.MarkCliff(2, 2);

        var map = new MapData(height, ground, 123);
        map.AddForestRegion(new ForestRegion(new Vector2(1.25f, 1.25f), 0.75f, 0f, 0.1f));
        map.AddLake(new LakeRegion(new Vector2(2.5f, 2.5f), 1f, 0f, isFrozen: true, waterTaggedRadius: 1f));
        return map;
    }

    private static MapData CreateMapWithPairedStones()
    {
        MapData map = new MapData(new HeightMap(12, 12, 1f), new GroundStateGrid(12, 12, 1f), 1);
        map.AddFeature(new PlacedFeature(FeatureType.OwnMainStone, new Vector3(1f, 0f, 1f)));
        map.AddFeature(new PlacedFeature(FeatureType.EnemyMainStone, new Vector3(9f, 0f, 9f)));
        return map;
    }

    private static GameObject CreateRenderedStoneHost(MapData map)
    {
        GameObject hostObject = new GameObject("MapSceneHost");
        hostObject.AddComponent<MapSceneHost>();
        hostObject.AddComponent<FeatureRenderer>();
        GameObject generatedFeatures = new GameObject("GeneratedFeatures");
        generatedFeatures.transform.SetParent(hostObject.transform, worldPositionStays: false);

        for (int i = 0; i < map.Features.Count; i++)
        {
            FeatureType type = map.Features[i].Type;
            GameObject stoneObject = new GameObject($"Stone{i}");
            stoneObject.transform.SetParent(generatedFeatures.transform, worldPositionStays: false);
            MagicStone stone = stoneObject.AddComponent<MagicStone>();
            stone.Setup(i, type, 3.2f);
        }

        return hostObject;
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null);
        field.SetValue(target, value);
    }
}
