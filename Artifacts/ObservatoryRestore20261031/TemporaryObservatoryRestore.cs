using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace WarSimulation.CountryComparison.Editor
{
    public static class TemporaryObservatoryRestore
    {
        [MenuItem("WarSim/Country/Facility Identity/Restore Original Observatory")]
        public static void Restore()
        {
            var view = Object.FindAnyObjectByType<CountryComparisonView>();
            if (EditorApplication.isPlaying || view == null || view.gameObject.scene.path != "Assets/Scenes/Country.unity")
                throw new InvalidOperationException("Open Country in Edit mode.");
            const string modelPath = "Assets/Models/Kingdom/CityV2/Models/Observatory.fbx";
            const string prefabPath = "Assets/Prefabs/Kingdom/CityV2/Prefabs/Observatory.prefab";
            AssetDatabase.ImportAsset(modelPath, ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.ImportAsset(prefabPath, ImportAssetOptions.ForceSynchronousImport);
            var original = view.cities[1].GetComponentsInChildren<Transform>(true)
                .Single(t => t.name == "Observatory" && PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject));
            var bounds = CountryComparisonImporter.BoundsOf(original.gameObject);
            var center = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            var replacement = (GameObject)PrefabUtility.InstantiatePrefab(prefab, original.parent);
            replacement.name = "Observatory";
            replacement.transform.localPosition = Vector3.zero;
            replacement.transform.localRotation = Quaternion.identity;
            bounds = CountryComparisonImporter.BoundsOf(replacement);
            replacement.transform.localScale = Vector3.one * Mathf.Min(13 / bounds.size.x, 13 / bounds.size.z);
            replacement.transform.rotation = original.rotation;
            bounds = CountryComparisonImporter.BoundsOf(replacement);
            replacement.transform.position += center - new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            PrefabUtility.RecordPrefabInstancePropertyModifications(replacement.transform);
            CountryRoofPalette.BeginBuild();
            CountryRoofPalette.Apply(replacement, "Observatory", 83, 95);
            Undo.RegisterCreatedObjectUndo(replacement, "Restore original observatory");
            Undo.DestroyObjectImmediate(original.gameObject);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(view.gameObject.scene);
            EditorSceneManager.SaveScene(view.gameObject.scene);
            CountryRoleFacilities.Validate();
            Debug.Log("Restored the original observatory and saved Country.");
        }
    }
}
