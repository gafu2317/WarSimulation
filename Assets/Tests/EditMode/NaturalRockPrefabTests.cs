using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using WarSimulation.Combat.Map;

public sealed class NaturalRockPrefabTests
{
    private const string PrefabDirectory = "Assets/Prefabs/Environment/NaturalRocks";

    [Test]
    public void NaturalRockPrefabs_ExposeGroundedCollisionAndNavMeshContract()
    {
        int visionObstacleLayer = LayerMask.NameToLayer("VisionObstacle");
        int notWalkableArea = UnityEngine.AI.NavMesh.GetAreaFromName("Not Walkable");
        for (int i = 0; i < 10; i++)
        {
            string path = $"{PrefabDirectory}/NaturalRock_{i + 1:00}.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(prefab, Is.Not.Null, path);
            Assert.That(prefab.GetComponent<MeshFilter>(), Is.Null, path);
            Assert.That(prefab.transform.Find("Geometry"), Is.Not.Null, path);

            Collider[] colliders = prefab.GetComponentsInChildren<Collider>(true);
            Assert.That(colliders, Is.Not.Empty, path);
            for (int c = 0; c < colliders.Length; c++)
            {
                Assert.That(colliders[c].isTrigger, Is.False, path);
                Assert.That(colliders[c].gameObject.layer, Is.EqualTo(visionObstacleLayer), path);
            }

            NavMeshModifier modifier = prefab.GetComponent<NavMeshModifier>();
            Assert.That(modifier, Is.Not.Null, path);
            Assert.That(modifier.overrideArea, Is.True, path);
            Assert.That(modifier.area, Is.EqualTo(notWalkableArea), path);

            Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>(true);
            Bounds bounds = renderers[0].bounds;
            for (int r = 1; r < renderers.Length; r++) bounds.Encapsulate(renderers[r].bounds);
            float vertexBottom = float.PositiveInfinity;
            foreach (MeshFilter mesh in prefab.GetComponentsInChildren<MeshFilter>(true))
            foreach (Vector3 vertex in mesh.sharedMesh.vertices)
                vertexBottom = Mathf.Min(vertexBottom,
                    prefab.transform.InverseTransformPoint(mesh.transform.TransformPoint(vertex)).y);
            Assert.That(vertexBottom, Is.EqualTo(0f).Within(0.00001f), path);
            Assert.That(Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z), Is.EqualTo(1f).Within(0.01f), path);
        }
    }

    [Test]
    public void NaturalRockPrefabs_BlockVisionObstacleRays()
    {
        int visionObstacleLayer = LayerMask.NameToLayer("VisionObstacle");
        for (int i = 0; i < 10; i++)
        {
            string path = $"{PrefabDirectory}/NaturalRock_{i + 1:00}.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            try
            {
                Renderer target = instance.GetComponentInChildren<Renderer>();
                Physics.SyncTransforms();
                Vector3 origin = target.bounds.center - Vector3.right * (target.bounds.extents.x + 1f);
                Assert.That(
                    Physics.Raycast(
                        origin,
                        Vector3.right,
                        out RaycastHit hit,
                        target.bounds.size.x + 2f,
                        1 << visionObstacleLayer),
                    Is.True,
                    path);
                Assert.That(hit.collider, Is.Not.Null, path);
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }
    }

    [Test]
    public void FeatureRenderer_UsesDeterministicRockPrefabVariantsAndTransforms()
    {
        var prefabs = new GameObject[10];
        for (int i = 0; i < prefabs.Length; i++)
            prefabs[i] = AssetDatabase.LoadAssetAtPath<GameObject>(
                $"{PrefabDirectory}/NaturalRock_{i + 1:00}.prefab");

        GameObject host = new GameObject("NaturalRockPrefabTestHost");
        try
        {
            FeatureRenderer renderer = host.AddComponent<FeatureRenderer>();
            typeof(FeatureRenderer)
                .GetField("_rockPrefabs", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(renderer, prefabs);
            MapData map = CreateRockMap();

            renderer.Render(map);
            Transform generated = host.transform.Find("GeneratedFeatures");
            List<string> firstMeshes = CaptureMeshes(generated);
            Quaternion[] firstRotations = CaptureRotations(generated);
            for (int i = 0; i < generated.childCount; i++)
            {
                Transform rock = generated.GetChild(i);
                Assert.That(rock.name, Is.EqualTo($"Rock_{i}"));
                Assert.That(rock.localPosition, Is.EqualTo(map.Features[i].WorldPosition));
                Assert.That(rock.GetComponent<MeshFilter>(), Is.Null);
                Assert.That(rock.Find("Geometry"), Is.Not.Null);
                Assert.That(rock.localScale.x, Is.InRange(4.42f, 5.98f));
            }

            renderer.Render(map);
            generated = host.transform.Find("GeneratedFeatures");
            Assert.That(CaptureMeshes(generated), Is.EqualTo(firstMeshes));
            Quaternion[] secondRotations = CaptureRotations(generated);
            for (int i = 0; i < firstRotations.Length; i++)
                Assert.That(Quaternion.Angle(firstRotations[i], secondRotations[i]), Is.LessThan(0.001f));
        }
        finally
        {
            Object.DestroyImmediate(host);
        }
    }

    [Test]
    public void MapScenes_ReferenceTheConfiguredRockPrefabs()
    {
        var paths = new List<string> { "Assets/Scenes/GafuTest.unity" };
        string[] guids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes/BakedMaps" });
        for (int i = 0; i < guids.Length; i++) paths.Add(AssetDatabase.GUIDToAssetPath(guids[i]));

        for (int i = 0; i < paths.Count; i++)
        {
            Scene scene = EditorSceneManager.OpenScene(paths[i], OpenSceneMode.Additive);
            try
            {
                FeatureRenderer renderer = FindFeatureRenderer(scene);
                Assert.That(renderer, Is.Not.Null, paths[i]);
                var serialized = new SerializedObject(renderer);
                SerializedProperty rockPrefabs = serialized.FindProperty("_rockPrefabs");
                Assert.That(rockPrefabs.arraySize, Is.EqualTo(10), paths[i]);
                for (int p = 0; p < rockPrefabs.arraySize; p++)
                    Assert.That(rockPrefabs.GetArrayElementAtIndex(p).objectReferenceValue, Is.Not.Null, paths[i]);
            }
            finally
            {
                if (scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            }
        }
    }

    private static MapData CreateRockMap()
    {
        var map = new MapData(new HeightMap(20, 20, 1f), new GroundStateGrid(20, 20, 1f), seed: 74);
        map.AddFeature(new PlacedFeature(FeatureType.Rock, new Vector3(2f, 0f, 3f)));
        map.AddFeature(new PlacedFeature(FeatureType.Rock, new Vector3(8f, 1f, 5f), Quaternion.Euler(0f, 25f, 0f)));
        map.AddFeature(new PlacedFeature(FeatureType.Rock, new Vector3(14f, 2f, 11f)));
        return map;
    }

    private static List<string> CaptureMeshes(Transform generated)
    {
        var values = new List<string>(generated.childCount);
        for (int i = 0; i < generated.childCount; i++)
        {
            MeshFilter[] meshes = generated.GetChild(i).GetComponentsInChildren<MeshFilter>(true);
            var names = new List<string>(meshes.Length);
            for (int m = 0; m < meshes.Length; m++) names.Add(meshes[m].sharedMesh.name);
            values.Add(string.Join("|", names));
        }
        return values;
    }

    private static Quaternion[] CaptureRotations(Transform generated)
    {
        var values = new Quaternion[generated.childCount];
        for (int i = 0; i < generated.childCount; i++) values[i] = generated.GetChild(i).localRotation;
        return values;
    }

    private static FeatureRenderer FindFeatureRenderer(Scene scene)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            FeatureRenderer renderer = roots[i].GetComponentInChildren<FeatureRenderer>(true);
            if (renderer != null) return renderer;
        }
        return null;
    }
}
