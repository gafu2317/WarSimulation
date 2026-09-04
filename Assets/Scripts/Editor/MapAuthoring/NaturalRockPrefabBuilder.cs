#if UNITY_EDITOR
using System;
using System.IO;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace WarSimulation.Combat.Map.EditorOnly
{
    public static class NaturalRockPrefabBuilder
    {
        private const string SourceDirectory = "Assets/Models/Environment/NaturalRockVariants";
        private const string PrefabDirectory = "Assets/Prefabs/Environment/NaturalRocks";
        private const string VisionObstacleLayerName = "VisionObstacle";
        private const string NotWalkableAreaName = "Not Walkable";
        private static readonly string[] SelectedModels =
        {
            "NaturalRock_01_TallMonolith",
            "NaturalRock_02_BroadAngular",
            "NaturalRock_04_FracturedBoulder",
            "NaturalRock_08_TwinBoulder",
            "NaturalRock_07_LeaningShard",
            "NaturalRock_11_Trapezoid",
        };

        [MenuItem("WarSim/Map/Create Natural Rock Prefabs")]
        public static void BuildAll()
        {
            int visionObstacleLayer = LayerMask.NameToLayer(VisionObstacleLayerName);
            int notWalkableArea = NavMesh.GetAreaFromName(NotWalkableAreaName);
            if (visionObstacleLayer < 0 || notWalkableArea < 0)
                throw new InvalidOperationException("Required rock Layer or NavMesh Area is missing.");

            Directory.CreateDirectory(PrefabDirectory);
            NaturalEnvironmentMaterialLibrary.BuildAll();
            var prefabs = new GameObject[SelectedModels.Length];
            for (int i = 0; i < SelectedModels.Length; i++)
            {
                string prefabPath = $"{PrefabDirectory}/NaturalRock_{SelectedModels[i].Split('_')[1]}.prefab";
                BuildPrefab($"{SourceDirectory}/{SelectedModels[i]}.fbx", prefabPath, visionObstacleLayer, notWalkableArea);
                prefabs[i] = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            }

            AssignToLoadedFeatureRenderers(prefabs);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[{nameof(NaturalRockPrefabBuilder)}] Created {prefabs.Length} selected natural rock prefabs.");
        }

        private static void BuildPrefab(
            string sourcePath,
            string prefabPath,
            int visionObstacleLayer,
            int notWalkableArea)
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
            if (source == null) throw new InvalidOperationException($"Rock source was not found: {sourcePath}");

            GameObject root = null;
            try
            {
                GameObject imported = (GameObject)PrefabUtility.InstantiatePrefab(source);
                if (imported == null) throw new InvalidOperationException($"Could not instantiate: {sourcePath}");
                PrefabUtility.UnpackPrefabInstance(
                    imported,
                    PrefabUnpackMode.Completely,
                    InteractionMode.AutomatedAction);

                root = new GameObject(Path.GetFileNameWithoutExtension(prefabPath));
                Transform geometry = new GameObject("Geometry").transform;
                geometry.SetParent(root.transform, worldPositionStays: false);

                MeshRenderer[] renderers = imported.GetComponentsInChildren<MeshRenderer>(includeInactive: true);
                if (renderers.Length == 0)
                    throw new InvalidOperationException($"Rock source has no MeshRenderer: {sourcePath}");
                PrioritizePrimaryMesh(renderers);
                imported.transform.SetParent(geometry, worldPositionStays: true);
                NaturalEnvironmentMaterialLibrary.ApplyRockMaterials(root);

                NormalizeGeometry(root, geometry);
                SetLayerRecursively(root, visionObstacleLayer);
                AddMeshColliders(geometry);
                var modifier = root.AddComponent<NavMeshModifier>();
                modifier.overrideArea = true;
                modifier.area = notWalkableArea;

                if (PrefabUtility.SaveAsPrefabAsset(root, prefabPath) == null)
                    throw new InvalidOperationException($"Could not save prefab: {prefabPath}");
            }
            finally
            {
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void PrioritizePrimaryMesh(MeshRenderer[] renderers)
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                if (!renderers[i].name.StartsWith("Rock_Main", StringComparison.Ordinal)) continue;
                renderers[i].transform.SetSiblingIndex(0);
                return;
            }
        }

        private static void NormalizeGeometry(GameObject root, Transform geometry)
        {
            Bounds bounds = CalculateBounds(root);
            float longestSide = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            if (longestSide <= Mathf.Epsilon)
                throw new InvalidOperationException($"Rock source has no bounds: {root.name}");

            // 回転したMeshのBounds底面には頂点が存在せず、そこを原点にすると岩が浮く。
            float bottom = float.PositiveInfinity;
            foreach (MeshFilter mesh in root.GetComponentsInChildren<MeshFilter>(includeInactive: true))
            {
                if (mesh.sharedMesh == null) continue;
                foreach (Vector3 vertex in mesh.sharedMesh.vertices)
                    bottom = Mathf.Min(bottom, root.transform.InverseTransformPoint(mesh.transform.TransformPoint(vertex)).y);
            }
            if (float.IsPositiveInfinity(bottom))
                throw new InvalidOperationException($"Rock source has no vertices: {root.name}");

            float scale = 1f / longestSide;
            geometry.localScale = Vector3.one * scale;
            geometry.localPosition = new Vector3(-bounds.center.x, -bottom, -bounds.center.z) * scale;
        }

        private static Bounds CalculateBounds(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(includeInactive: true);
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        private static void AddMeshColliders(Transform root)
        {
            MeshFilter[] meshes = root.GetComponentsInChildren<MeshFilter>(includeInactive: true);
            for (int i = 0; i < meshes.Length; i++)
            {
                if (meshes[i].sharedMesh == null) continue;
                MeshCollider collider = meshes[i].gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = meshes[i].sharedMesh;
                collider.convex = false;
                collider.isTrigger = false;
            }
        }

        private static void SetLayerRecursively(GameObject target, int layer)
        {
            target.layer = layer;
            for (int i = 0; i < target.transform.childCount; i++)
                SetLayerRecursively(target.transform.GetChild(i).gameObject, layer);
        }

        private static void AssignToLoadedFeatureRenderers(GameObject[] prefabs)
        {
            FeatureRenderer[] renderers = UnityEngine.Object.FindObjectsByType<FeatureRenderer>(
                FindObjectsInactive.Include);
            for (int i = 0; i < renderers.Length; i++)
            {
                var serialized = new SerializedObject(renderers[i]);
                SerializedProperty property = serialized.FindProperty("_rockPrefabs");
                property.arraySize = prefabs.Length;
                for (int p = 0; p < prefabs.Length; p++)
                    property.GetArrayElementAtIndex(p).objectReferenceValue = prefabs[p];
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(renderers[i]);
            }
        }
    }
}
#endif
