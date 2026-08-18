using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using WarSimulation.Combat.Map;
using Object = UnityEngine.Object;

namespace WarSimulation.Tests.EditMode
{
    public sealed class AuthoredMapBakeStatusTests
    {
        [Test]
        public void MissingAssetsAreReportedPerBakeStage()
        {
            AuthoredMapDefinition definition = ScriptableObject.CreateInstance<AuthoredMapDefinition>();
            try
            {
                AuthoredMapBakeStatus status = AuthoredMapBakeStatus.Evaluate(definition, null);

                Assert.That(status.MapData, Is.EqualTo(AuthoredMapBakeStageState.Missing));
                Assert.That(status.NavMesh, Is.EqualTo(AuthoredMapBakeStageState.Missing));
                Assert.That(status.AssaultRoutes, Is.EqualTo(AuthoredMapBakeStageState.NotConfigured));
                Assert.That(status.Preview, Is.EqualTo(AuthoredMapBakeStageState.Missing));
                Assert.That(status.Scene3D, Is.EqualTo(AuthoredMapBakeStageState.Deferred));
            }
            finally
            {
                Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void RouteOnlyChangeKeepsMapAndNavMeshCurrent()
        {
            using var fixture = new BakeFixture();
            fixture.BakeAll();

            fixture.Definition.AssaultRoutes[0].Waypoints.Add(new Vector2(1f, 1f));
            AuthoredMapBakeStatus status = AuthoredMapBakeStatus.Evaluate(
                fixture.Definition,
                fixture.Host);

            Assert.That(status.MapData, Is.EqualTo(AuthoredMapBakeStageState.Current));
            Assert.That(status.NavMesh, Is.EqualTo(AuthoredMapBakeStageState.Current));
            Assert.That(status.AssaultRoutes, Is.EqualTo(AuthoredMapBakeStageState.Stale));
            Assert.That(status.Preview, Is.EqualTo(AuthoredMapBakeStageState.Stale));
            Assert.That(status.Scene3D, Is.EqualTo(AuthoredMapBakeStageState.Current));
        }

        [Test]
        public void GeometryChangeMakesDependentBakeStagesStale()
        {
            using var fixture = new BakeFixture();
            fixture.BakeAll();

            fixture.Definition.BuildSeed++;
            AuthoredMapBakeStatus status = AuthoredMapBakeStatus.Evaluate(
                fixture.Definition,
                fixture.Host);

            Assert.That(status.MapData, Is.EqualTo(AuthoredMapBakeStageState.Stale));
            Assert.That(status.NavMesh, Is.EqualTo(AuthoredMapBakeStageState.Stale));
            Assert.That(status.AssaultRoutes, Is.EqualTo(AuthoredMapBakeStageState.Stale));
            Assert.That(status.Preview, Is.EqualTo(AuthoredMapBakeStageState.Stale));
            Assert.That(status.Scene3D, Is.EqualTo(AuthoredMapBakeStageState.Deferred));
        }

        [Test]
        public void Scene3DDistinguishesUnappliedAndMissingGeneratedData()
        {
            using var fixture = new BakeFixture();
            fixture.BakeAssets();

            AuthoredMapBakeStatus unapplied = AuthoredMapBakeStatus.Evaluate(
                fixture.Definition,
                fixture.Host);
            fixture.Host.SetBakedRenderFingerprint(fixture.Definition.ComputeGeometryFingerprint());
            AuthoredMapBakeStatus missing = AuthoredMapBakeStatus.Evaluate(
                fixture.Definition,
                fixture.Host);

            Assert.That(unapplied.Scene3D, Is.EqualTo(AuthoredMapBakeStageState.Stale));
            Assert.That(missing.Scene3D, Is.EqualTo(AuthoredMapBakeStageState.MissingSceneData));
        }

        [Test]
        public void CurrentSavedNavMeshLoadsForRouteValidationWithoutGeneratedTerrain()
        {
            using var fixture = new BakeFixture();
            fixture.BakeAssets();
            Type bakeType = Type.GetType(
                "WarSimulation.Combat.Map.EditorOnly.AuthoredMapNavBake, WarSimulation.Editor",
                throwOnError: true);
            MethodInfo method = bakeType.GetMethod(
                "TryGetCurrentMapAndNavMesh",
                BindingFlags.Static | BindingFlags.NonPublic);
            object[] arguments = { fixture.Definition, fixture.Host, null, null };

            bool loaded = (bool)method.Invoke(null, arguments);

            Assert.That(loaded, Is.True);
            Assert.That(arguments[2], Is.TypeOf<MapData>());
            Assert.That(arguments[3], Is.Null);
            Assert.That(fixture.Host.transform.Find("GeneratedTerrain"), Is.Null);
        }

        [Test]
        public void StaleSavedMapStopsRouteValidationWithoutChangingBakedRoutes()
        {
            using var fixture = new BakeFixture();
            fixture.BakeAssets();
            int routeFingerprint = fixture.BakedMap.AssaultRouteFingerprint;
            fixture.Definition.BuildSeed++;
            Type bakeType = Type.GetType(
                "WarSimulation.Combat.Map.EditorOnly.AuthoredMapNavBake, WarSimulation.Editor",
                throwOnError: true);
            MethodInfo method = bakeType.GetMethod(
                "TryGetCurrentMapAndNavMesh",
                BindingFlags.Static | BindingFlags.NonPublic);
            object[] arguments = { fixture.Definition, fixture.Host, null, null };

            bool loaded = (bool)method.Invoke(null, arguments);

            Assert.That(loaded, Is.False);
            Assert.That(fixture.BakedMap.AssaultRouteFingerprint, Is.EqualTo(routeFingerprint));
        }

        [Test]
        public void CombatMapLoadStillRejectsMissingScene3D()
        {
            using var fixture = new BakeFixture();
            fixture.BakeAssets();
            LogAssert.Expect(LogType.Error, "[MapSceneHost] Scene 3D does not match the baked map.");

            bool loaded = fixture.Host.LoadBakedMap(
                fixture.BakedMap.CreateRuntimeMap(),
                fixture.NavMesh,
                fixture.Definition.ComputeGeometryFingerprint());

            Assert.That(loaded, Is.False);
        }

        private sealed class BakeFixture : IDisposable
        {
            public AuthoredMapDefinition Definition { get; } =
                ScriptableObject.CreateInstance<AuthoredMapDefinition>();
            public BakedMapData BakedMap { get; } = ScriptableObject.CreateInstance<BakedMapData>();
            public NavMeshData NavMesh { get; } = new();
            public MapSceneHost Host { get; }

            private readonly Texture2D _preview = new(2, 2);

            public BakeFixture()
            {
                Host = new GameObject("MapSceneHost-Test").AddComponent<MapSceneHost>();
            }

            public void BakeAssets()
            {
                int geometryFingerprint = Definition.ComputeGeometryFingerprint();
                BakedMap.Capture(CreateMap(), geometryFingerprint);
                Definition.SetBakedMapData(BakedMap);
                Definition.SetBakedNavMesh(NavMesh, geometryFingerprint);
                Definition.AssaultRoutes.Add(new AuthoredAssaultRoute(
                    "route-main",
                    "Main",
                    AuthoredAssaultRouteSource.Manual));
                BakedMap.CaptureAssaultRoutes(
                    new[]
                    {
                        new AssaultRoute(
                            "route-main",
                            "Main",
                            new[] { Vector3.zero, new Vector3(2f, 0f, 2f) }),
                    },
                    Definition.ComputeAssaultRouteFingerprint());
                Definition.SetBakedPreview(_preview, Definition.ComputeAssaultRouteFingerprint());
            }

            public void BakeAll()
            {
                BakeAssets();
                Host.SetBakedRenderFingerprint(Definition.ComputeGeometryFingerprint());
                var terrain = new GameObject("GeneratedTerrain");
                terrain.transform.SetParent(Host.transform, false);
            }

            public void Dispose()
            {
                Object.DestroyImmediate(Host.gameObject);
                Object.DestroyImmediate(_preview);
                Object.DestroyImmediate(NavMesh);
                Object.DestroyImmediate(BakedMap);
                Object.DestroyImmediate(Definition);
            }

            private static MapData CreateMap()
            {
                var height = new HeightMap(2, 2, 1f);
                var ground = new GroundStateGrid(2, 2, 1f);
                return new MapData(height, ground, 1);
            }
        }
    }
}
