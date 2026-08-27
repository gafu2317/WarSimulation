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
    }
}
#endif
