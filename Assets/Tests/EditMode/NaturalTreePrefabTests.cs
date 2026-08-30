using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Unity.AI.Navigation;
using WarSimulation.Combat.Map;

public sealed class NaturalTreePrefabTests
{
    private const string PrefabDirectory = "Assets/Prefabs/Environment/NaturalTrees";
    private const string VisionObstacleLayerName = "VisionObstacle";
    private const string IgnoreRaycastLayerName = "Ignore Raycast";

    [Test]
    public void NaturalTreePrefabs_ExposeRequiredCollisionAndNavMeshContract()
    {
        int visionObstacleLayer = LayerMask.NameToLayer(VisionObstacleLayerName);
        int ignoreRaycastLayer = LayerMask.NameToLayer(IgnoreRaycastLayerName);
        Assert.That(visionObstacleLayer, Is.GreaterThanOrEqualTo(0));
        Assert.That(ignoreRaycastLayer, Is.GreaterThanOrEqualTo(0));

        for (int i = 0; i < 10; i++)
        {
            string path = $"{PrefabDirectory}/NaturalTree_{i + 1:00}.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(prefab, Is.Not.Null, path);

            Transform trunk = FindDirectChild(prefab.transform, "Trunk");
            Transform foliage = FindDirectChild(prefab.transform, "Foliage");
            Assert.That(trunk, Is.Not.Null, path);
            Assert.That(foliage, Is.Not.Null, path);
            Assert.That(prefab.GetComponents<Collider>(), Is.Empty, path);
            Assert.That(prefab.GetComponentsInChildren<NavMeshObstacle>(true), Is.Empty, path);

            Collider[] trunkColliders = trunk.GetComponentsInChildren<Collider>(true);
            Assert.That(trunkColliders, Is.Not.Empty, path);
            for (int c = 0; c < trunkColliders.Length; c++)
            {
                Assert.That(trunkColliders[c].isTrigger, Is.False, path);
                Assert.That(trunkColliders[c].gameObject.layer, Is.EqualTo(visionObstacleLayer), path);
            }

            Assert.That(foliage.GetComponentsInChildren<Collider>(true), Is.Empty, path);
            Assert.That(foliage.gameObject.layer, Is.EqualTo(ignoreRaycastLayer), path);
            NavMeshModifier foliageModifier = foliage.GetComponent<NavMeshModifier>();
            Assert.That(foliageModifier, Is.Not.Null, path);
            Assert.That(foliageModifier.ignoreFromBuild, Is.True, path);

            Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>(true);
            Assert.That(renderers, Is.Not.Empty, path);
            Bounds bounds = renderers[0].bounds;
            for (int r = 1; r < renderers.Length; r++) bounds.Encapsulate(renderers[r].bounds);
            Assert.That(bounds.min.y, Is.EqualTo(0f).Within(0.01f), path);
            Assert.That(bounds.size.y, Is.EqualTo(2.4f).Within(0.02f), path);
        }
    }

    [Test]
    public void FeatureRenderer_ScalesTreePrefabsByRequestedSizeMultiplier()
    {
        var prefabs = new GameObject[10];
        for (int i = 0; i < prefabs.Length; i++)
        {
            prefabs[i] = AssetDatabase.LoadAssetAtPath<GameObject>(
                $"{PrefabDirectory}/NaturalTree_{i + 1:00}.prefab");
            Assert.That(prefabs[i], Is.Not.Null);
        }

        GameObject host = new GameObject("NaturalTreeScaleTestHost");
        try
        {
            FeatureRenderer renderer = host.AddComponent<FeatureRenderer>();
            typeof(FeatureRenderer)
                .GetField("_treePrefabs", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(renderer, prefabs);

            MapData map = CreateTreeMap();
            renderer.Render(map);
            Transform generated = host.transform.Find("GeneratedFeatures");
            Assert.That(generated, Is.Not.Null);

            MethodInfo heightScaleMethod = typeof(FeatureRenderer)
                .GetMethod("GetTreeHeightScale", BindingFlags.Instance | BindingFlags.NonPublic);
            float variation = (float)heightScaleMethod.Invoke(renderer, new object[] { map.Features[0].WorldPosition });
            Assert.That(generated.GetChild(0).localScale, Is.EqualTo(Vector3.one * (variation * 1.5f)));
        }
        finally
        {
            Object.DestroyImmediate(host);
        }
    }

    [Test]
    public void FeatureRenderer_UsesDeterministicPrefabVariantsPositionsAndRotations()
    {
        var prefabs = new GameObject[10];
        for (int i = 0; i < prefabs.Length; i++)
        {
            prefabs[i] = AssetDatabase.LoadAssetAtPath<GameObject>(
                $"{PrefabDirectory}/NaturalTree_{i + 1:00}.prefab");
            Assert.That(prefabs[i], Is.Not.Null);
        }

        GameObject host = new GameObject("NaturalTreePrefabTestHost");
        try
        {
            FeatureRenderer renderer = host.AddComponent<FeatureRenderer>();
            typeof(FeatureRenderer)
                .GetField("_treePrefabs", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(renderer, prefabs);

            MapData map = CreateTreeMap();
            renderer.Render(map);
            Transform generated = host.transform.Find("GeneratedFeatures");
            Assert.That(generated, Is.Not.Null);
            Assert.That(generated.childCount, Is.EqualTo(3));

            var firstFingerprints = CaptureTreeFingerprints(generated);
            Quaternion[] firstRotations = CaptureTreeRotations(generated);
            for (int i = 0; i < generated.childCount; i++)
            {
                Transform tree = generated.GetChild(i);
                Assert.That(tree.name, Is.EqualTo($"Tree_{i}"));
                Assert.That(tree.GetComponent<MeshFilter>(), Is.Null);
                Assert.That(FindDirectChild(tree, "Trunk"), Is.Not.Null);
                Assert.That(FindDirectChild(tree, "Foliage"), Is.Not.Null);
            }

            Assert.That(generated.GetChild(0).localPosition, Is.EqualTo(map.Features[0].WorldPosition));
            Assert.That(generated.GetChild(1).localPosition, Is.EqualTo(map.Features[1].WorldPosition));
            Assert.That(generated.GetChild(2).localPosition, Is.EqualTo(map.Features[2].WorldPosition));
            for (int i = 0; i < firstRotations.Length; i++)
                Assert.That(Quaternion.Angle(firstRotations[i], map.Features[i].Rotation), Is.GreaterThan(1f));

            renderer.Render(map);
            generated = host.transform.Find("GeneratedFeatures");
            Assert.That(CaptureTreeFingerprints(generated), Is.EqualTo(firstFingerprints));
            Quaternion[] secondRotations = CaptureTreeRotations(generated);
            for (int i = 0; i < firstRotations.Length; i++)
                Assert.That(Quaternion.Angle(secondRotations[i], firstRotations[i]), Is.LessThan(0.001f));
        }
        finally
        {
            Object.DestroyImmediate(host);
        }
    }

    [Test]
    public void BakedMapScenes_ReferenceTheConfiguredTreePrefabs()
    {
        string[] guids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes/BakedMaps" });
        Assert.That(guids, Is.Not.Empty);
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                AssertSceneReferencesTreePrefabs(scene, path);
            }
            finally
            {
                if (scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            }
        }
    }

    [Test]
    public void SourceMapScene_ReferencesTheConfiguredTreePrefabs()
    {
        const string path = "Assets/Scenes/GafuTest.unity";
        Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        try
        {
            AssertSceneReferencesTreePrefabs(scene, path);
        }
        finally
        {
            if (scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static MapData CreateTreeMap()
    {
        var map = new MapData(
            new HeightMap(20, 20, 1f),
            new GroundStateGrid(20, 20, 1f),
            seed: 42);
        map.AddFeature(new PlacedFeature(FeatureType.Tree, new Vector3(2f, 0f, 3f)));
        map.AddFeature(new PlacedFeature(
            FeatureType.Tree,
            new Vector3(8f, 1f, 5f),
            Quaternion.Euler(0f, 35f, 0f)));
        map.AddFeature(new PlacedFeature(FeatureType.Tree, new Vector3(14f, 2f, 11f)));
        return map;
    }

    private static List<string> CaptureTreeFingerprints(Transform generated)
    {
        var fingerprints = new List<string>(generated.childCount);
        for (int i = 0; i < generated.childCount; i++)
        {
            MeshFilter[] meshes = generated.GetChild(i).GetComponentsInChildren<MeshFilter>(true);
            var names = new List<string>(meshes.Length);
            for (int m = 0; m < meshes.Length; m++)
                names.Add(meshes[m].sharedMesh != null ? meshes[m].sharedMesh.name : string.Empty);
            fingerprints.Add(string.Join("|", names));
        }

        return fingerprints;
    }

    private static Quaternion[] CaptureTreeRotations(Transform generated)
    {
        var rotations = new Quaternion[generated.childCount];
        for (int i = 0; i < generated.childCount; i++)
            rotations[i] = generated.GetChild(i).localRotation;
        return rotations;
    }

    private static Transform FindDirectChild(Transform root, string name)
    {
        for (int i = 0; i < root.childCount; i++)
        {
            if (root.GetChild(i).name == name) return root.GetChild(i);
        }

        return null;
    }

    private static MapSceneHost FindMapHost(Scene scene)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            MapSceneHost host = roots[i].GetComponentInChildren<MapSceneHost>(true);
            if (host != null) return host;
        }

        return null;
    }

    private static void AssertSceneReferencesTreePrefabs(Scene scene, string path)
    {
        MapSceneHost host = FindMapHost(scene);
        Assert.That(host, Is.Not.Null, path);
        FeatureRenderer renderer = host.GetComponent<FeatureRenderer>();
        Assert.That(renderer, Is.Not.Null, path);
        SerializedObject serialized = new SerializedObject(renderer);
        SerializedProperty treePrefabs = serialized.FindProperty("_treePrefabs");
        Assert.That(treePrefabs, Is.Not.Null, path);
        Assert.That(treePrefabs.arraySize, Is.EqualTo(10), path);
        for (int p = 0; p < treePrefabs.arraySize; p++)
            Assert.That(treePrefabs.GetArrayElementAtIndex(p).objectReferenceValue, Is.Not.Null, path);

        Transform generated = host.transform.Find("GeneratedFeatures");
        if (generated == null) return;
        for (int i = 0; i < generated.childCount; i++)
        {
            Transform tree = generated.GetChild(i);
            if (!tree.name.StartsWith("Tree_")) continue;
            Assert.That(tree.GetComponent<MeshFilter>(), Is.Null, path);
            Assert.That(FindDirectChild(tree, "Trunk"), Is.Not.Null, path);
            Assert.That(FindDirectChild(tree, "Foliage"), Is.Not.Null, path);
        }
    }
}
