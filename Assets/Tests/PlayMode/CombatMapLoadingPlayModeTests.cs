using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Unity.Profiling;
using WarSimulation.Combat.Map;

public sealed class CombatMapLoadingPlayModeTests
{
    [UnityTearDown]
    public IEnumerator RestoreEmptySceneAfterMapLoading()
    {
        Scene cleanup = SceneManager.CreateScene("CombatMapLoadingPlayModeCleanup");
        SceneManager.SetActiveScene(cleanup);
        int sceneCount = SceneManager.sceneCount;
        var loadedScenes = new List<Scene>(sceneCount);
        for (int i = 0; i < sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (scene != cleanup) loadedScenes.Add(scene);
        }
        for (int i = 0; i < loadedScenes.Count; i++)
            yield return SceneManager.UnloadSceneAsync(loadedScenes[i]);

        yield return null;
    }

    [UnityTest]
    public IEnumerator PrepareMapAsync_SwitchesAdditiveBakedScenesWithoutKeepingTheOldScene()
    {
        using var renderRecorder = ProfilerRecorder.StartNew(
            ProfilerCategory.Scripts,
            "CombatLoading.Render3D",
            128);
        using var navBuildRecorder = ProfilerRecorder.StartNew(
            ProfilerCategory.Scripts,
            "CombatLoading.NavMeshBuild",
            128);
        long allocatedBefore = System.GC.GetAllocatedBytesForCurrentThread();
        var stopwatch = Stopwatch.StartNew();
        yield return SceneManager.LoadSceneAsync("GafuTest", LoadSceneMode.Single);

        CombatMapSystem mapSystem = Object.FindFirstObjectByType<CombatMapSystem>();
        CombatMapSelectionView selection = FindSelectionView();
        Assert.That(mapSystem, Is.Not.Null);
        Assert.That(selection, Is.Not.Null);

        while (mapSystem.PreparationState == MapPreparationState.Loading) yield return null;
        AuthoredMapDefinition first = mapSystem.AuthoredMap;
        AuthoredMapDefinition second = FindDifferentMap(selection, first);
        Assert.That(first, Is.Not.Null);
        Assert.That(second, Is.Not.Null);

        yield return mapSystem.PrepareMapAsync(first);
        Assert.That(mapSystem.IsMapReady(first), Is.True);
        Assert.That(SceneManager.GetSceneByPath(first.BakedRuntimeScenePath).isLoaded, Is.True);
        AssertRendererSettingsMatch(
            FindMapHost(SceneManager.GetSceneByName("GafuTest")),
            FindMapHost(SceneManager.GetSceneByPath(first.BakedRuntimeScenePath)));

        yield return mapSystem.PrepareMapAsync(second);

        Assert.That(mapSystem.IsMapReady(second), Is.True);
        Assert.That(mapSystem.AuthoredMap, Is.SameAs(second));
        Assert.That(SceneManager.GetSceneByPath(second.BakedRuntimeScenePath).isLoaded, Is.True);
        Assert.That(SceneManager.GetSceneByPath(first.BakedRuntimeScenePath).isLoaded, Is.False);
        stopwatch.Stop();
        long allocatedBytes = System.GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        TestContext.WriteLine(
            $"Map load and switch: {stopwatch.Elapsed.TotalMilliseconds:F1} ms, " +
            $"main-thread allocation delta: {allocatedBytes} bytes");
        Assert.That(GetInvocationCount(renderRecorder), Is.Zero);
        Assert.That(GetInvocationCount(navBuildRecorder), Is.Zero);
    }

    private static CombatMapSelectionView FindSelectionView()
    {
        CombatMapSelectionView[] views = Resources.FindObjectsOfTypeAll<CombatMapSelectionView>();
        for (int i = 0; i < views.Length; i++)
        {
            if (views[i] != null && views[i].gameObject.scene.IsValid()) return views[i];
        }
        return null;
    }

    private static MapSceneHost FindMapHost(Scene scene)
    {
        Assert.That(scene.IsValid() && scene.isLoaded, Is.True);
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            MapSceneHost host = roots[i].GetComponentInChildren<MapSceneHost>(includeInactive: true);
            if (host != null) return host;
        }
        Assert.Fail($"MapSceneHost was not found in {scene.path}.");
        return null;
    }

    private static void AssertRendererSettingsMatch(MapSceneHost expected, MapSceneHost actual)
    {
        AssertSerializedSettingsMatch<TerrainRenderer>(expected, actual);
        AssertSerializedSettingsMatch<TerrainSkirtRenderer>(expected, actual);
        AssertSerializedSettingsMatch<RiverRenderer>(expected, actual);
        AssertSerializedSettingsMatch<LakeRenderer>(expected, actual);
        AssertSerializedSettingsMatch<BridgeRenderer>(expected, actual);
        AssertSerializedSettingsMatch<FeatureRenderer>(expected, actual);
    }

    private static void AssertSerializedSettingsMatch<T>(MapSceneHost expected, MapSceneHost actual)
        where T : Component
    {
        T expectedComponent = expected.GetComponent<T>();
        T actualComponent = actual.GetComponent<T>();
        Assert.That(expectedComponent, Is.Not.Null, $"Configured {typeof(T).Name} is missing.");
        Assert.That(actualComponent, Is.Not.Null, $"Baked {typeof(T).Name} is missing.");

        FieldInfo[] fields = typeof(T).GetFields(BindingFlags.Instance | BindingFlags.NonPublic);
        for (int i = 0; i < fields.Length; i++)
        {
            FieldInfo field = fields[i];
            if (field.GetCustomAttribute<SerializeField>() == null) continue;
            if (typeof(Component).IsAssignableFrom(field.FieldType) ||
                field.FieldType == typeof(GameObject) ||
                field.FieldType == typeof(Transform))
            {
                continue;
            }

            Assert.That(
                field.GetValue(actualComponent),
                Is.EqualTo(field.GetValue(expectedComponent)),
                $"{typeof(T).Name}.{field.Name} differs from the configured map renderer.");
        }
    }

    private static long GetInvocationCount(ProfilerRecorder recorder)
    {
        ProfilerRecorderSample[] samples = recorder.ToArray();
        long count = 0;
        for (int i = 0; i < samples.Length; i++) count += samples[i].Count;
        return count;
    }

    private static AuthoredMapDefinition FindDifferentMap(
        CombatMapSelectionView selection,
        AuthoredMapDefinition current)
    {
        FieldInfo field = typeof(CombatMapSelectionView).GetField(
            "_mapOptions",
            BindingFlags.Instance | BindingFlags.NonPublic);
        var options = field?.GetValue(selection) as List<AuthoredMapDefinition>;
        if (options == null) return null;
        for (int i = 0; i < options.Count; i++)
        {
            if (options[i] != null && options[i] != current) return options[i];
        }
        return null;
    }
}
