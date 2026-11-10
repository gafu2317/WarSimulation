#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace WarSimulation.Kingdom.Schools
{
    public static class SchoolLevelUnityMigration
    {
        const string CatalogScenePath = "Assets/Scenes/KingdomAssetCatalog.unity";

        [MenuItem("WarSim/Kingdom/Replace Schools And Rebuild Catalog")]
        public static void Run()
        {
            SchoolLevelAssetImporter.BuildAll();
            DeleteOldSchoolAssets();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            WarSimulation.Kingdom.EditorOnly.KingdomAssetCatalogBuilder.Build();
            var catalog = SceneManager.GetActiveScene();
            if (!catalog.IsValid() || catalog.path != CatalogScenePath)
                throw new InvalidOperationException("Kingdom asset catalog was not rebuilt.");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            UnityEngine.Debug.Log("School level migration completed: 15 models imported and catalog rebuilt.");
        }

        static void DeleteOldSchoolAssets()
        {
            foreach (var path in new[]
            {
                "Assets/Models/Kingdom/City/Models/WarriorAcademy.fbx",
                "Assets/Models/Kingdom/City/Models/ArcaneAcademy.fbx",
                "Assets/Models/Kingdom/City/Models/SpiritAcademy.fbx",
                "Assets/Prefabs/Kingdom/City/Prefabs/WarriorAcademy.prefab",
                "Assets/Prefabs/Kingdom/City/Prefabs/ArcaneAcademy.prefab",
                "Assets/Prefabs/Kingdom/City/Prefabs/SpiritAcademy.prefab"
            })
            {
                if (AssetDatabase.LoadMainAssetAtPath(path) == null)
                    continue;
                if (!AssetDatabase.DeleteAsset(path))
                    throw new InvalidOperationException("Could not delete old school asset: " + path);
            }
        }
    }
}
#endif
