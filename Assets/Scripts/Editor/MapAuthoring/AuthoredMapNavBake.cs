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
    /// Authored マップの NavMesh / 進攻ルートを Editor でベイクしてアセット保存する。
    /// </summary>
    internal static class AuthoredMapNavBake
    {
        private const float StoneSampleRadius = 8f;

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

            int fingerprint = definition.ComputeBakeFingerprint();
            if (!TryBuildAssaultRoutes(
                    map,
                    host.transform,
                    out List<AuthoredBakedAssaultRoute> allyRoutes,
                    out List<AuthoredBakedAssaultRoute> enemyRoutes,
                    out string routeError))
            {
                status = routeError;
                return false;
            }

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

            if (!builder.Load(definition.BakedNavMesh))
            {
                status = "保存済みNavMeshDataの再ロードに失敗しました";
                return false;
            }

            definition.SetBakedAssaultRoutes(allyRoutes, enemyRoutes, fingerprint);
            host.SetBakedRenderFingerprint(fingerprint);
            EditorUtility.SetDirty(definition);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(host.gameObject.scene);
            status = "シーンへ3D反映完了 / MapData・NavMesh・進攻ルートを保存しました（シーンを保存してください）";
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

        private static bool TryBuildAssaultRoutes(
            MapData map,
            Transform mapOrigin,
            out List<AuthoredBakedAssaultRoute> allyRoutes,
            out List<AuthoredBakedAssaultRoute> enemyRoutes,
            out string error)
        {
            allyRoutes = new List<AuthoredBakedAssaultRoute>();
            enemyRoutes = new List<AuthoredBakedAssaultRoute>();
            error = null;
            int areaMask = CombatStoneAssaultRoutes.CreateAreaMask(allowRiverCrossing: false);

            if (!CombatAssaultRouteCache.TryFindMainStoneWorld(
                    map, mapOrigin, FeatureType.OwnMainStone, out Vector3 ownStone) ||
                !CombatAssaultRouteCache.TryFindMainStoneWorld(
                    map, mapOrigin, FeatureType.EnemyMainStone, out Vector3 enemyStone))
            {
                return true;
            }

            if (!CombatStoneAssaultRoutes.TrySamplePosition(ownStone, StoneSampleRadius, areaMask, out Vector3 allyStart) ||
                !CombatStoneAssaultRoutes.TrySamplePosition(enemyStone, StoneSampleRadius, areaMask, out Vector3 allyGoal) ||
                !CombatStoneAssaultRoutes.TrySamplePosition(enemyStone, StoneSampleRadius, areaMask, out Vector3 enemyStart) ||
                !CombatStoneAssaultRoutes.TrySamplePosition(ownStone, StoneSampleRadius, areaMask, out Vector3 enemyGoal))
            {
                error = "進攻ルート用の NavMesh Sample に失敗しました";
                return false;
            }

            allyRoutes = CombatAssaultRouteCache.BuildBakedRoutesForTeam(
                map, mapOrigin, allyStart, allyGoal, areaMask);
            enemyRoutes = CombatAssaultRouteCache.BuildBakedRoutesForTeam(
                map, mapOrigin, enemyStart, enemyGoal, areaMask);
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
