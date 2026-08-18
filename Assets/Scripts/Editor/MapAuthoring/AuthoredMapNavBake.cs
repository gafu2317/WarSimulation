#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using WarSimulation.Combat.Map;

namespace WarSimulation.Combat.Map.EditorOnly
{
    /// <summary>
    /// Authored マップの NavMesh / 侵攻ルートを Editor でベイクしてアセット保存する。
    /// </summary>
    internal static class AuthoredMapNavBake
    {
        public static bool BakeAndSave(AuthoredMapDefinition definition, MapSceneHost host, out string status)
        {
            status = null;
            if (definition == null || definition.SharedConfig == null)
            {
                status = "マップと共通設定が必要です";
                return false;
            }

            if (host == null)
            {
                status = "シーンに MapSceneHost がありません";
                return false;
            }

            host.Config = definition.SharedConfig;

            MapData map = AuthoredMapBuilder.Build(definition);
            EnsureRenderComponents(host);
            bool navOk = host.ApplyMapData(map, render3D: true, bakeNavMesh: true, prebakedNavMesh: null);
            if (!navOk)
            {
                status = "NavMesh ベイクに失敗しました";
                return false;
            }

            CombatNavMeshBuilder builder = host.GetComponent<CombatNavMeshBuilder>();
            if (builder == null || builder.Surface == null || builder.Surface.navMeshData == null)
            {
                status = "NavMeshData を取得できませんでした";
                return false;
            }

            bool migrated = definition.MigrateLegacyAssaultRoutes();
            int fingerprint = definition.ComputeGeometryFingerprint();

            if (!SaveNavMeshAsset(definition, builder.Surface.navMeshData, fingerprint, out string navError))
            {
                status = navError;
                return false;
            }

            if (!SaveBakedMapAsset(definition, map, fingerprint, out string mapError))
            {
                status = mapError;
                return false;
            }
            definition.BakedMapData.InvalidateAssaultRoutes();
            EditorUtility.SetDirty(definition.BakedMapData);

            if (!builder.Load(definition.BakedNavMesh))
            {
                status = "保存済みNavMeshDataの再ロードに失敗しました";
                return false;
            }

            host.SetBakedRenderFingerprint(fingerprint);
            EditorUtility.SetDirty(definition);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(host.gameObject.scene);
            status = "シーンへ3D反映完了 / MapData・NavMeshを保存し、侵攻ルートを未検証に戻しました（シーンを保存してください）";
            if (migrated) status += " / 旧侵攻ルートを移行しました";
            return true;
        }

        public static bool AutoGenerateAndSave(
            AuthoredMapDefinition definition,
            MapSceneHost host,
            out string status)
        {
            status = null;
            if (!TryGetCurrentMapAndNavMesh(definition, host, out MapData map, out status)) return false;
            if (!CombatAssaultRouteBaker.TryBuildAutomaticRoutes(
                    map,
                    host.transform,
                    definition.Bridges,
                    out List<AuthoredAssaultRoute> generated,
                    out _,
                    out string error))
            {
                status = error;
                return false;
            }

            List<AuthoredAssaultRoute> combined = CombatAssaultRouteBaker.ReplaceAutomaticRoutes(
                definition.AssaultRoutes,
                generated);

            if (!CombatAssaultRouteBaker.TryValidateRoutes(
                    map, host.transform, combined, out List<AssaultRoute> baked, out _))
            {
                status = "自動候補を含む侵攻ルートの検証に失敗しました";
                return false;
            }

            Undo.RecordObject(definition, "侵攻ルートを自動設定");
            definition.AssaultRoutes.Clear();
            definition.AssaultRoutes.AddRange(combined);
            return SaveRoutesAndPreview(definition, map, baked, out status);
        }

        public static bool ValidateAndSave(
            AuthoredMapDefinition definition,
            MapSceneHost host,
            IReadOnlyList<AuthoredAssaultRoute> routes,
            out List<CombatAssaultRouteValidationFailure> failures,
            out string status)
        {
            failures = new List<CombatAssaultRouteValidationFailure>();
            status = null;
            if (!TryGetCurrentMapAndNavMesh(definition, host, out MapData map, out status)) return false;
            CombatAssaultRouteBaker.TryValidateRoutes(
                map, host.transform, routes, out List<AssaultRoute> baked, out failures);
            if (baked.Count == 0)
            {
                status = failures.Count > 0 ? failures[0].Message : "有効な侵攻ルートがありません";
                return false;
            }

            if (!SaveRoutesAndPreview(definition, map, baked, out status)) return false;
            if (failures.Count > 0) status += $" / {failures.Count} 本は検証失敗";
            return true;
        }

        private static bool TryGetCurrentMapAndNavMesh(
            AuthoredMapDefinition definition,
            MapSceneHost host,
            out MapData map,
            out string status)
        {
            map = null;
            status = null;
            if (definition == null)
            {
                status = "マップが選択されていません";
                return false;
            }

            if (host == null)
            {
                status = "シーンに MapSceneHost がありません";
                return false;
            }

            if (!definition.HasValidBakedMapData || !definition.HasValidBakedNavMesh)
            {
                status = "地形・NavMeshの再ベイクが必要です。「シーンへ3D反映」を実行してください";
                return false;
            }

            map = definition.BakedMapData.CreateRuntimeMap();
            if (!host.LoadBakedNavMeshForValidation(definition.BakedNavMesh))
            {
                status = "保存済みNavMeshDataを検証用にロードできませんでした";
                map = null;
                return false;
            }

            return true;
        }

        private static bool SaveRoutesAndPreview(
            AuthoredMapDefinition definition,
            MapData map,
            List<AssaultRoute> routes,
            out string status)
        {
            map.AssaultRoutes.Clear();
            map.AssaultRoutes.AddRange(routes);
            int fingerprint = definition.ComputeAssaultRouteFingerprint();
            definition.BakedMapData.CaptureAssaultRoutes(routes, fingerprint);
            EditorUtility.SetDirty(definition.BakedMapData);
            if (!SavePreviewAsset(definition, map, fingerprint, out string error))
            {
                status = error;
                return false;
            }

            EditorUtility.SetDirty(definition);
            AssetDatabase.SaveAssets();
            status = $"侵攻ルート {routes.Count} 本を検証・保存しました";
            return true;
        }

        internal static bool SavePreviewAsset(
            AuthoredMapDefinition definition,
            MapData map,
            int fingerprint,
            out string error)
        {
            error = null;
            string definitionPath = AssetDatabase.GetAssetPath(definition);
            if (string.IsNullOrEmpty(definitionPath))
            {
                error = "AuthoredMap のアセットパスを取得できません";
                return false;
            }

            Texture2D preview = MapAuthoringPreview2D.BuildBackground(map);
            if (preview == null)
            {
                error = "プレビュー画像を生成できませんでした";
                return false;
            }

            string dir = Path.GetDirectoryName(definitionPath)?.Replace('\\', '/') ?? "Assets";
            string assetPath = $"{dir}/{definition.name}_Preview.png";
            string absolutePath = Path.GetFullPath(assetPath);
            try
            {
                File.WriteAllBytes(absolutePath, preview.EncodeToPNG());
            }
            catch (System.Exception ex)
            {
                error = $"プレビュー画像を保存できませんでした: {ex.Message}";
                return false;
            }
            finally
            {
                Object.DestroyImmediate(preview);
            }

            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            if (AssetImporter.GetAtPath(assetPath) is TextureImporter importer)
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = true;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Point;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.textureCompression = TextureImporterCompression.Compressed;
                importer.SaveAndReimport();
            }

            Texture2D saved = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (saved == null)
            {
                error = "保存したプレビュー画像を読み込めませんでした";
                return false;
            }

            definition.SetBakedPreview(saved, fingerprint);
            EditorUtility.SetDirty(definition);
            return true;
        }

        private static bool SaveBakedMapAsset(
            AuthoredMapDefinition definition,
            MapData map,
            int fingerprint,
            out string error)
        {
            error = null;
            string definitionPath = AssetDatabase.GetAssetPath(definition);
            if (string.IsNullOrEmpty(definitionPath))
            {
                error = "AuthoredMap のアセットパスを取得できません";
                return false;
            }

            string dir = Path.GetDirectoryName(definitionPath)?.Replace('\\', '/') ?? "Assets";
            string assetPath = $"{dir}/{definition.name}_BakedMapData.asset";
            if (definition.BakedMapData != null)
            {
                string existingPath = AssetDatabase.GetAssetPath(definition.BakedMapData);
                if (!string.IsNullOrEmpty(existingPath)) assetPath = existingPath;
            }

            string assetName = Path.GetFileNameWithoutExtension(assetPath);
            BakedMapData saved = AssetDatabase.LoadAssetAtPath<BakedMapData>(assetPath);
            if (saved == null)
            {
                saved = ScriptableObject.CreateInstance<BakedMapData>();
                saved.name = assetName;
                AssetDatabase.CreateAsset(saved, assetPath);
            }

            saved.Capture(map, fingerprint);
            EditorUtility.SetDirty(saved);
            definition.SetBakedMapData(saved);
            return true;
        }

        private static bool SaveNavMeshAsset(
            AuthoredMapDefinition definition,
            NavMeshData source,
            int fingerprint,
            out string error)
        {
            error = null;
            string definitionPath = AssetDatabase.GetAssetPath(definition);
            if (string.IsNullOrEmpty(definitionPath))
            {
                error = "AuthoredMap のアセットパスを取得できません";
                return false;
            }

            string dir = Path.GetDirectoryName(definitionPath)?.Replace('\\', '/') ?? "Assets";
            string fileName = $"{definition.name}_NavMesh.asset";
            string assetPath = $"{dir}/{fileName}";

            NavMeshData existing = definition.BakedNavMesh;
            if (existing != null)
            {
                string existingPath = AssetDatabase.GetAssetPath(existing);
                if (!string.IsNullOrEmpty(existingPath))
                    assetPath = existingPath;
            }

            string assetName = Path.GetFileNameWithoutExtension(assetPath);
            NavMeshData saved = AssetDatabase.LoadAssetAtPath<NavMeshData>(assetPath);
            if (saved != null)
            {
                EditorUtility.CopySerialized(source, saved);
                saved.name = assetName;
                EditorUtility.SetDirty(saved);
            }
            else
            {
                saved = Object.Instantiate(source);
                saved.name = assetName;
                AssetDatabase.CreateAsset(saved, assetPath);
            }

            definition.SetBakedNavMesh(saved, fingerprint);
            return true;
        }

        internal static void EnsureRenderComponents(MapSceneHost gen)
        {
            if (gen.GetComponent<TerrainRenderer>() == null) Undo.AddComponent<TerrainRenderer>(gen.gameObject);
            if (gen.GetComponent<TerrainSkirtRenderer>() == null) Undo.AddComponent<TerrainSkirtRenderer>(gen.gameObject);
            if (gen.GetComponent<RiverRenderer>() == null) Undo.AddComponent<RiverRenderer>(gen.gameObject);
            if (gen.GetComponent<LakeRenderer>() == null) Undo.AddComponent<LakeRenderer>(gen.gameObject);
            if (gen.GetComponent<BridgeRenderer>() == null) Undo.AddComponent<BridgeRenderer>(gen.gameObject);
            if (gen.GetComponent<FeatureRenderer>() == null) Undo.AddComponent<FeatureRenderer>(gen.gameObject);
            if (gen.GetComponent<CombatNavMeshBuilder>() == null) Undo.AddComponent<CombatNavMeshBuilder>(gen.gameObject);
        }
    }
}
#endif
