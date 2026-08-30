#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;

namespace WarSimulation.Combat.Map.EditorOnly
{
    public static class NaturalTreePrefabBuilder
    {
        private const string SourceDirectory = "Assets/Models/Environment/NaturalTreeVariants";
        private const string PrefabDirectory = "Assets/Prefabs/Environment/NaturalTrees";
        private const string VisionObstacleLayerName = "VisionObstacle";
        private const string IgnoreRaycastLayerName = "Ignore Raycast";
        private const float TargetHeight = 2.4f;
        private const int ExpectedTreeCount = 10;

        [MenuItem("WarSim/Map/Create Natural Tree Prefabs")]
        public static void BuildAll()
        {
            string[] guids = AssetDatabase.FindAssets("t:Model", new[] { SourceDirectory });
            var sourcePaths = new List<string>(guids.Length);
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)) sourcePaths.Add(path);
            }

            sourcePaths.Sort(StringComparer.Ordinal);
            if (sourcePaths.Count != ExpectedTreeCount)
            {
                Debug.LogError(
                    $"[{nameof(NaturalTreePrefabBuilder)}] Expected {ExpectedTreeCount} FBX files in " +
                    $"{SourceDirectory}, found {sourcePaths.Count}.");
                return;
            }

            Directory.CreateDirectory(PrefabDirectory);
            int visionObstacleLayer = LayerMask.NameToLayer(VisionObstacleLayerName);
            int ignoreRaycastLayer = LayerMask.NameToLayer(IgnoreRaycastLayerName);
            if (visionObstacleLayer < 0 || ignoreRaycastLayer < 0)
            {
                Debug.LogError(
                    $"[{nameof(NaturalTreePrefabBuilder)}] Required layers are missing: " +
                    $"{VisionObstacleLayerName}, {IgnoreRaycastLayerName}.");
                return;
            }

            for (int i = 0; i < sourcePaths.Count; i++)
            {
                string prefabPath = $"{PrefabDirectory}/NaturalTree_{i + 1:00}.prefab";
                BuildPrefab(sourcePaths[i], prefabPath, visionObstacleLayer, ignoreRaycastLayer);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[{nameof(NaturalTreePrefabBuilder)}] Created {sourcePaths.Count} natural tree prefabs.");
        }

        private static void BuildPrefab(
            string sourcePath,
            string prefabPath,
            int visionObstacleLayer,
            int ignoreRaycastLayer)
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
            if (source == null) throw new InvalidOperationException($"Tree source was not found: {sourcePath}");

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
                root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                root.transform.localScale = Vector3.one;

                var trunk = new GameObject("Trunk").transform;
                trunk.SetParent(root.transform, worldPositionStays: false);
                var foliage = new GameObject("Foliage").transform;
                foliage.SetParent(root.transform, worldPositionStays: false);

                imported.transform.SetParent(root.transform, worldPositionStays: false);
                MeshRenderer[] renderers = imported.GetComponentsInChildren<MeshRenderer>(includeInactive: true);
                if (renderers.Length == 0)
                    throw new InvalidOperationException($"Tree source has no MeshRenderer: {sourcePath}");

                for (int i = 0; i < renderers.Length; i++)
                {
                    Transform meshTransform = renderers[i].transform;
                    bool isFoliage = meshTransform.name.StartsWith("Foliage", StringComparison.OrdinalIgnoreCase);
                    meshTransform.SetParent(isFoliage ? foliage : trunk, worldPositionStays: true);
                }

                UnityEngine.Object.DestroyImmediate(imported);
                NormalizeGroundPivot(root, trunk, foliage);
                SetLayerRecursively(root, visionObstacleLayer);
                SetLayerRecursively(foliage.gameObject, ignoreRaycastLayer);
                RemoveColliders(foliage);
                AddTrunkColliders(trunk);

                var foliageNavModifier = foliage.gameObject.AddComponent<NavMeshModifier>();
                foliageNavModifier.ignoreFromBuild = true;

                if (PrefabUtility.SaveAsPrefabAsset(root, prefabPath) == null)
                    throw new InvalidOperationException($"Could not save prefab: {prefabPath}");
            }
            finally
            {
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void NormalizeGroundPivot(GameObject root, Transform trunk, Transform foliage)
        {
            Bounds bounds = CalculateBounds(root);
            if (bounds.size.y <= Mathf.Epsilon)
                throw new InvalidOperationException($"Tree source has no vertical bounds: {root.name}");

            Vector3 offset = new Vector3(0f, -bounds.min.y, 0f);
            trunk.localPosition += offset;
            foliage.localPosition += offset;

            bounds = CalculateBounds(root);
            root.transform.localScale = Vector3.one * (TargetHeight / bounds.size.y);
        }

        private static Bounds CalculateBounds(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(includeInactive: true);
            if (renderers.Length == 0) return new Bounds(root.transform.position, Vector3.zero);

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        private static void SetLayerRecursively(GameObject target, int layer)
        {
            target.layer = layer;
            for (int i = 0; i < target.transform.childCount; i++)
                SetLayerRecursively(target.transform.GetChild(i).gameObject, layer);
        }

        private static void RemoveColliders(Transform root)
        {
            Collider[] colliders = root.GetComponentsInChildren<Collider>(includeInactive: true);
            for (int i = 0; i < colliders.Length; i++) UnityEngine.Object.DestroyImmediate(colliders[i]);
        }

        private static void AddTrunkColliders(Transform root)
        {
            MeshFilter[] meshes = root.GetComponentsInChildren<MeshFilter>(includeInactive: true);
            for (int i = 0; i < meshes.Length; i++)
            {
                MeshFilter mesh = meshes[i];
                if (mesh.sharedMesh == null) continue;

                MeshCollider collider = mesh.GetComponent<MeshCollider>();
                if (collider == null) collider = mesh.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = mesh.sharedMesh;
                collider.convex = false;
                collider.isTrigger = false;
            }
        }
    }
}
#endif
