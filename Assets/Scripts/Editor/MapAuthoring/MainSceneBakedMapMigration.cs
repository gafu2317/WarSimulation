#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WarSimulation.Combat.Map.EditorOnly
{
    internal static class MainSceneBakedMapMigration
    {
        private static readonly string[] GeneratedRootNames =
        {
            "GeneratedTerrain",
            "GeneratedTerrainSkirt",
            "GeneratedRivers",
            "GeneratedLakes",
            "GeneratedBridges",
            "GeneratedFeatures",
            "GeneratedNavAreaVolumes",
        };

        [MenuItem("WarSim/Map/メインSceneの検証済み生成ルートを除去")]
        private static void MigrateActiveScene()
        {
            Scene mainScene = SceneManager.GetActiveScene();
            if (!TryResolveMainMap(mainScene, out CombatMapSystem mapSystem, out string error))
            {
                Debug.LogError($"[MainSceneBakedMapMigration] {error}");
                return;
            }

            AuthoredMapDefinition definition = mapSystem.AuthoredMap;
            MapSceneHost mainHost = mapSystem.SceneHost;
            MapData map = definition.BakedMapData.CreateRuntimeMap();
            int fingerprint = definition.ComputeBakeFingerprint();
            if (!mainHost.HasBakedRenderDataFor(map, fingerprint) ||
                !ValidateRuntimeScene(definition, map, fingerprint, out error))
            {
                Debug.LogError($"[MainSceneBakedMapMigration] {error}");
                return;
            }

            int removed = 0;
            for (int i = 0; i < GeneratedRootNames.Length; i++)
            {
                Transform generated = mainHost.transform.Find(GeneratedRootNames[i]);
                if (generated == null) continue;
                UnityEngine.Object.DestroyImmediate(generated.gameObject);
                removed++;
            }

            mainHost.ClearLoadedNavMesh();
            for (int i = 0; i < GeneratedRootNames.Length; i++)
            {
                if (mainHost.transform.Find(GeneratedRootNames[i]) != null)
                {
                    Debug.LogError("[MainSceneBakedMapMigration] 生成ルート除去後の検証に失敗したため保存しません。");
                    return;
                }
            }

            EditorSceneManager.MarkSceneDirty(mainScene);
            if (!EditorSceneManager.SaveScene(mainScene))
            {
                Debug.LogError("[MainSceneBakedMapMigration] メインSceneを保存できませんでした。");
                return;
            }

            Debug.Log($"[MainSceneBakedMapMigration] 検証済み生成ルート {removed} 個を除去しました。");
        }

        private static bool TryResolveMainMap(
            Scene scene,
            out CombatMapSystem mapSystem,
            out string error)
        {
            mapSystem = null;
            error = null;
            if (!scene.IsValid() || !scene.isLoaded)
            {
                error = "有効なメインSceneが開かれていません。";
                return false;
            }

            CombatMapSystem[] systems = UnityEngine.Object.FindObjectsByType<CombatMapSystem>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int i = 0; i < systems.Length; i++)
            {
                if (systems[i] != null && systems[i].gameObject.scene == scene)
                {
                    if (mapSystem != null)
                    {
                        error = "メインScene内にCombatMapSystemが複数あります。";
                        return false;
                    }
                    mapSystem = systems[i];
                }
            }

            if (mapSystem == null || mapSystem.SceneHost == null || mapSystem.AuthoredMap == null)
            {
                error = "CombatMapSystem、MapSceneHost、AuthoredMapの参照が不足しています。";
                return false;
            }
            if (!mapSystem.AuthoredMap.HasValidBakedRuntimeScene)
            {
                error = "ランタイム用マップSceneの再ベイクが必要です。";
                return false;
            }
            return true;
        }

        private static bool ValidateRuntimeScene(
            AuthoredMapDefinition definition,
            MapData map,
            int fingerprint,
            out string error)
        {
            error = null;
            Scene scene = EditorSceneManager.OpenScene(
                definition.BakedRuntimeScenePath,
                OpenSceneMode.Additive);
            try
            {
                MapSceneHost host = FindHost(scene);
                if (host == null || !host.HasBakedRenderDataFor(map, fingerprint))
                {
                    error = "ランタイム用マップSceneの内容がメインSceneと一致しません。";
                    return false;
                }
                return true;
            }
            finally
            {
                if (scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, removeScene: true);
            }
        }

        private static MapSceneHost FindHost(Scene scene)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                MapSceneHost host = roots[i].GetComponentInChildren<MapSceneHost>(includeInactive: true);
                if (host != null) return host;
            }
            return null;
        }
    }
}
#endif
