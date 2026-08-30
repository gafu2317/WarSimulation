#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace WarSimulation.Combat.Map.EditorOnly
{
    public static class RefinedMagicStonePrefabBuilder
    {
        private const string SourcePath = "Assets/Models/Environment/Refined/Refined_MagicStone_Diamond.fbx";
        private const string PrefabPath = "Assets/Resources/Combat/Map/RefinedMagicStone.prefab";
        private const string OwnCoreMaterialPath = "Assets/Resources/Combat/Map/MagicStoneCoreBlue.mat";
        private const string EnemyCoreMaterialPath = "Assets/Resources/Combat/Map/MagicStoneCoreRed.mat";
        private const float ModelHeight = 2.43f;

        [MenuItem("WarSim/Map/Create Refined Magic Stone Prefab")]
        public static void Build()
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePath);
            if (source == null)
            {
                Debug.LogError($"Refined magic stone source was not found: {SourcePath}");
                return;
            }

            string directory = Path.GetDirectoryName(PrefabPath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            GameObject root = new GameObject("RefinedMagicStone");
            GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(source);
            model.name = "Model";
            PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            model.transform.SetParent(root.transform, worldPositionStays: false);
            model.transform.localPosition = new Vector3(0f, -ModelHeight * 0.5f, 0f);
            RenameImportedParts(model);
            CreateTeamCoreMaterials(model);

            BoxCollider collider = root.AddComponent<BoxCollider>();
            collider.center = Vector3.zero;
            collider.size = new Vector3(1.76f, ModelHeight, 1.62f);
            collider.isTrigger = false;

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Created refined magic stone prefab: {PrefabPath}");
        }

        private static void RenameImportedParts(GameObject model)
        {
            Transform[] transforms = model.GetComponentsInChildren<Transform>(includeInactive: true);
            for (int i = 0; i < transforms.Length; i++)
            {
                if (transforms[i].name.StartsWith("Core.", StringComparison.Ordinal))
                    transforms[i].name = "Core";
                else if (transforms[i].name.StartsWith("Pedestal_Lower.", StringComparison.Ordinal))
                    transforms[i].name = "Pedestal_Lower";
                else if (transforms[i].name.StartsWith("Pedestal_Upper.", StringComparison.Ordinal))
                    transforms[i].name = "Pedestal_Upper";
            }
        }

        private static void CreateTeamCoreMaterials(GameObject model)
        {
            Transform core = FindChildByName(model.transform, "Core");
            Renderer renderer = core != null ? core.GetComponentInChildren<Renderer>() : null;
            Material source = renderer != null ? renderer.sharedMaterial : null;
            if (source == null) throw new InvalidOperationException("Magic stone Core material was not found.");

            CreateOrUpdateMaterial(OwnCoreMaterialPath, source, new Color(0.08f, 0.42f, 1f));
            CreateOrUpdateMaterial(EnemyCoreMaterialPath, source, new Color(1f, 0.08f, 0.08f));
        }

        private static void CreateOrUpdateMaterial(string path, Material source, Color color)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(source);
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                EditorUtility.CopySerialized(source, material);
            }

            material.name = Path.GetFileNameWithoutExtension(path);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_EmissionColor"))
            {
                material.SetColor("_EmissionColor", color * 0.35f);
                material.EnableKeyword("_EMISSION");
            }
            EditorUtility.SetDirty(material);
        }

        private static Transform FindChildByName(Transform root, string name)
        {
            if (root.name == name || root.name.StartsWith(name + ".", StringComparison.Ordinal)) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform match = FindChildByName(root.GetChild(i), name);
                if (match != null) return match;
            }
            return null;
        }
    }
}
#endif
