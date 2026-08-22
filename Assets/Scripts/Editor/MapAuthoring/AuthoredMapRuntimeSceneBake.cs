#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WarSimulation.Combat.Map.EditorOnly
{
    internal static class AuthoredMapRuntimeSceneBake
    {
        private const string RuntimeSceneDirectory = "Assets/Scenes/BakedMaps";

        [MenuItem("WarSim/Map/全ランタイム用マップSceneを再ベイク")]
        private static void BakeAll()
        {
            string[] guids = AssetDatabase.FindAssets("t:AuthoredMapDefinition");
            Array.Sort(guids, StringComparer.Ordinal);
            int baked = 0;
            try
            {
                for (int i = 0; i < guids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    AuthoredMapDefinition definition =
                        AssetDatabase.LoadAssetAtPath<AuthoredMapDefinition>(path);
                    EditorUtility.DisplayProgressBar(
                        "ランタイム用マップSceneをベイク",
                        definition != null ? definition.name : path,
                        guids.Length > 0 ? (float)i / guids.Length : 1f);
                    if (!TryBake(definition, out string error))
                        throw new InvalidOperationException($"{path}: {error}");
                    baked++;
                }

                AssetDatabase.SaveAssets();
                Debug.Log($"[AuthoredMapRuntimeSceneBake] {baked} Sceneをベイクしました。");
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        public static bool TryBake(AuthoredMapDefinition definition, out string error)
        {
            error = null;
            if (definition == null || !definition.HasValidBakedMapData ||
                !definition.HasValidBakedNavMesh)
            {
                error = "有効なMapDataとNavMeshのベイクが必要です";
                return false;
            }
            if (!TryFindRendererSettingsSource(out MapSceneHost settingsSource, out error))
                return false;

            Directory.CreateDirectory(RuntimeSceneDirectory);
            string definitionPath = AssetDatabase.GetAssetPath(definition);
            string guid = AssetDatabase.AssetPathToGUID(definitionPath);
            string scenePath = $"{RuntimeSceneDirectory}/{Sanitize(definition.name)}_{guid[..8]}.unity";
            Scene previousActiveScene = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            bool saved = false;
            try
            {
                var root = new GameObject($"BakedMap_{definition.name}");
                SceneManager.MoveGameObjectToScene(root, scene);
                MapSceneHost host = root.AddComponent<MapSceneHost>();
                host.Config = definition.SharedConfig;
                if (!TryCopyRendererSettings(settingsSource, root, out error)) return false;
                MapData map = definition.BakedMapData.CreateRuntimeMap();
                int fingerprint = definition.ComputeBakeFingerprint();
                definition.BakedMapData.CaptureInitialSpawnPositions(map, fingerprint);
                EditorUtility.SetDirty(definition.BakedMapData);
                if (!host.Render3D(map, bakeNavMesh: false, definition.BakedNavMesh))
                {
                    error = "保存済みNavMeshを読み込めませんでした";
                    return false;
                }

                host.SetBakedRenderFingerprint(fingerprint);
                host.ClearLoadedNavMesh();
                root.SetActive(false);
                saved = EditorSceneManager.SaveScene(scene, scenePath, saveAsCopy: false);
                if (!saved)
                {
                    error = "Sceneを保存できませんでした";
                    return false;
                }

                definition.SetBakedRuntimeScene(scenePath, fingerprint);
                EditorUtility.SetDirty(definition);
                AddToBuildSettings(scenePath);
                return true;
            }
            finally
            {
                if (previousActiveScene.IsValid() && previousActiveScene.isLoaded)
                    SceneManager.SetActiveScene(previousActiveScene);
                if (scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, removeScene: true);
                if (!saved && File.Exists(scenePath)) AssetDatabase.ImportAsset(scenePath);
            }
        }

        private static bool TryFindRendererSettingsSource(
            out MapSceneHost source,
            out string error)
        {
            source = null;
            error = null;
            Scene activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid() || !activeScene.isLoaded)
            {
                error = "表示設定を取得するための有効なSceneが開かれていません";
                return false;
            }

            GameObject[] roots = activeScene.GetRootGameObjects();
            for (int i = 0; i < roots.Length && source == null; i++)
            {
                MapSceneHost[] hosts = roots[i].GetComponentsInChildren<MapSceneHost>(includeInactive: true);
                for (int h = 0; h < hosts.Length; h++)
                {
                    if (HasAllRendererSettings(hosts[h]))
                    {
                        source = hosts[h];
                        break;
                    }
                }
            }

            if (source != null) return true;
            error = "開いているSceneに表示設定済みのMapSceneHostがありません";
            return false;
        }

        private static bool HasAllRendererSettings(MapSceneHost host) =>
            host != null &&
            host.GetComponent<TerrainRenderer>() != null &&
            host.GetComponent<TerrainSkirtRenderer>() != null &&
            host.GetComponent<RiverRenderer>() != null &&
            host.GetComponent<LakeRenderer>() != null &&
            host.GetComponent<BridgeRenderer>() != null &&
            host.GetComponent<FeatureRenderer>() != null;

        private static bool TryCopyRendererSettings(
            MapSceneHost source,
            GameObject target,
            out string error)
        {
            error = null;
            return TryCopyRendererSettings<TerrainRenderer>(source, target, out error) &&
                TryCopyRendererSettings<TerrainSkirtRenderer>(source, target, out error) &&
                TryCopyRendererSettings<RiverRenderer>(source, target, out error) &&
                TryCopyRendererSettings<LakeRenderer>(source, target, out error) &&
                TryCopyRendererSettings<BridgeRenderer>(source, target, out error) &&
                TryCopyRendererSettings<FeatureRenderer>(source, target, out error);
        }

        private static bool TryCopyRendererSettings<T>(
            MapSceneHost source,
            GameObject target,
            out string error)
            where T : Component
        {
            error = null;
            T sourceComponent = source.GetComponent<T>();
            if (sourceComponent == null)
            {
                error = $"表示設定元に{nameof(T)}がありません";
                return false;
            }

            T targetComponent = target.AddComponent<T>();
            var sourceSerialized = new SerializedObject(sourceComponent);
            var targetSerialized = new SerializedObject(targetComponent);
            SerializedProperty property = sourceSerialized.GetIterator();
            bool enterChildren = true;
            while (property.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (!property.propertyPath.StartsWith("_", StringComparison.Ordinal)) continue;
                if (property.propertyType == SerializedPropertyType.ObjectReference)
                {
                    UnityEngine.Object referencedObject = property.objectReferenceValue;
                    if (referencedObject != null && !AssetDatabase.Contains(referencedObject)) continue;
                }

                targetSerialized.CopyFromSerializedProperty(property);
            }
            targetSerialized.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        private static void AddToBuildSettings(string scenePath)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            for (int i = 0; i < scenes.Count; i++)
            {
                if (!string.Equals(scenes[i].path, scenePath, StringComparison.Ordinal)) continue;
                scenes[i] = new EditorBuildSettingsScene(scenePath, enabled: true);
                EditorBuildSettings.scenes = scenes.ToArray();
                return;
            }

            scenes.Add(new EditorBuildSettingsScene(scenePath, enabled: true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static string Sanitize(string value)
        {
            foreach (char invalid in Path.GetInvalidFileNameChars())
                value = value.Replace(invalid, '_');
            return value;
        }
    }
}
#endif
