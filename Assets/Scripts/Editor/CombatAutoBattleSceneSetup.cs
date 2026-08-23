using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using WarSimulation.Combat.Map;

public static class CombatAutoBattleSceneSetup
{
    private const string RootName = "CombatAutoBattleRuntime";
    private const string MapDirectory = "Assets/Data/Map/Map/Authored";
    private const string WeaponDirectory = "Assets/Data/Map/Weapon";

    [MenuItem("Tools/War Simulation/Auto Battle/Setup Current Scene")]
    public static void SetupCurrentScene()
    {
        TrySetupCurrentScene();
    }

    public static bool TrySetupCurrentScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || string.IsNullOrEmpty(scene.path))
        {
            EditorUtility.DisplayDialog("設定失敗", "保存済みの戦闘シーンを開いてください。", "閉じる");
            return false;
        }

        if (!TryCollectMaps(out AuthoredMapDefinition[] maps, out string error) ||
            !TryCollectWeapons(out WeaponConfig[] weapons, out error) ||
            !TryValidateScene(scene, out error))
        {
            EditorUtility.DisplayDialog("設定失敗", error, "閉じる");
            return false;
        }

        CombatAutoBattleRunner[] existingRunners = FindAllInScene<CombatAutoBattleRunner>(scene);
        GameObject existing = FindRoot(scene, RootName);
        if (existing != null) Undo.DestroyObjectImmediate(existing);
        for (int i = 0; i < existingRunners.Length; i++)
        {
            CombatAutoBattleRunner existingRunner = existingRunners[i];
            if (existingRunner != null) Undo.DestroyObjectImmediate(existingRunner);
        }

        var root = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(root, "Setup Auto Battle Runtime");
        var runner = root.AddComponent<CombatAutoBattleRunner>();
        var serializedRunner = new SerializedObject(runner);
        AssignArray(serializedRunner.FindProperty("_mapCandidates"), maps);
        AssignArray(serializedRunner.FindProperty("_weaponConfigs"), weapons);
        serializedRunner.FindProperty("_timeoutSeconds").floatValue = 600f;
        serializedRunner.FindProperty("_timeScale").floatValue = 6f;
        serializedRunner.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log(
            $"[自動戦闘] {scene.path} の既存Runner {existingRunners.Length}件を専用ルートへ統合し、" +
            $"利用可能な{maps.Length}マップを持つ {RootName} を設定しました。");
        return true;
    }

    private static bool TryCollectMaps(out AuthoredMapDefinition[] maps, out string error)
    {
        string[] guids = AssetDatabase.FindAssets("t:AuthoredMapDefinition", new[] { MapDirectory });
        var collected = new List<AuthoredMapDefinition>(guids.Length);
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            AuthoredMapDefinition map = AssetDatabase.LoadAssetAtPath<AuthoredMapDefinition>(path);
            if (map == null) continue;

            CombatMapAvailability normal = CombatMapAvailability.Evaluate(map, stonePositionsReversed: false);
            CombatMapAvailability reversed = CombatMapAvailability.Evaluate(map, stonePositionsReversed: true);
            if (!normal.CanStartBattle && !reversed.CanStartBattle) continue;

            collected.Add(map);
        }

        collected.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        if (collected.Count == 0)
        {
            maps = null;
            error = $"利用可能なマップがありません: {MapDirectory}";
            return false;
        }

        maps = collected.ToArray();
        error = null;
        return true;
    }

    private static bool TryCollectWeapons(out WeaponConfig[] weapons, out string error)
    {
        string[] names = { "Sword", "Shield", "Wand", "Grimoire", "Bible", "Rosary" };
        var collected = new List<WeaponConfig>(names.Length);
        for (int i = 0; i < names.Length; i++)
        {
            string path = $"{WeaponDirectory}/{names[i]}WeaponConfig.asset";
            WeaponConfig weapon = AssetDatabase.LoadAssetAtPath<WeaponConfig>(path);
            if (weapon == null)
            {
                weapons = null;
                error = $"武器設定が見つかりません: {path}";
                return false;
            }
            collected.Add(weapon);
        }

        weapons = collected.ToArray();
        error = null;
        return true;
    }

    private static bool TryValidateScene(Scene scene, out string error)
    {
        CombatCharacterSystem characterSystem = FindInScene<CombatCharacterSystem>(scene);
        if (characterSystem == null)
        {
            error = "CombatCharacterSystem がありません。";
            return false;
        }

        bool hasSerializedPool = characterSystem.AllyCharacters.Count > 0 && characterSystem.EnemyCharacters.Count > 0;
        var serializedSystem = new SerializedObject(characterSystem);
        bool generatesRuntimePool = serializedSystem.FindProperty("_generateCandidatesAtRuntime").boolValue &&
            serializedSystem.FindProperty("_characterPrefab").objectReferenceValue != null;
        if (!hasSerializedPool && !generatesRuntimePool)
        {
            error = "味方・敵プールも、有効な実行時候補生成設定もありません。";
            return false;
        }

        if (FindInScene<CombatBattleFlow>(scene) == null || FindInScene<CombatMapSystem>(scene) == null)
        {
            error = "CombatBattleFlow または CombatMapSystem がありません。";
            return false;
        }

        error = null;
        return true;
    }

    private static void AssignArray<T>(SerializedProperty property, IReadOnlyList<T> values)
        where T : UnityEngine.Object
    {
        property.arraySize = values.Count;
        for (int i = 0; i < values.Count; i++)
            property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }

    private static GameObject FindRoot(Scene scene, string name)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
            if (roots[i].name == name) return roots[i];
        return null;
    }

    private static T FindInScene<T>(Scene scene) where T : Component
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            T component = roots[i].GetComponentInChildren<T>(true);
            if (component != null) return component;
        }
        return null;
    }

    private static T[] FindAllInScene<T>(Scene scene) where T : Component
    {
        var results = new List<T>();
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
            results.AddRange(roots[i].GetComponentsInChildren<T>(true));
        return results.ToArray();
    }
}
