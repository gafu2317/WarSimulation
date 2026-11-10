using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using WarSimulation.Combat.Map;

public static class CombatAutoBattleSearchBuild
{
    private const string SourceScene = "Assets/Scenes/GafuTest.unity";
    private const string SearchScene = "Assets/_Recovery/AutoBattleBalanceSearch.unity";
    private const string BuildDirectory = ".unity/CombatAutoBattleSearch";
    private const string MapDirectory = "Assets/Data/Map/Map/Authored";

    [MenuItem("Tools/War Simulation/Auto Battle/Build Search Player")]
    public static void Build()
    {
        var result = new BuildResultData
        {
            UnityVersion = Application.unityVersion,
            CreatedAtUtc = DateTime.UtcNow.ToString("O"),
            SourceScene = SourceScene,
            SearchScene = SearchScene,
        };
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string outputDirectory = Path.Combine(projectRoot, BuildDirectory);
        Directory.CreateDirectory(outputDirectory);
        try
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Play Mode中は探索Playerをビルドできません。");

            Directory.CreateDirectory(Path.GetDirectoryName(SearchScene));
            AssetDatabase.Refresh();
            Scene source = EditorSceneManager.OpenScene(SourceScene, OpenSceneMode.Single);
            if (!EditorSceneManager.SaveScene(source, SearchScene, saveAsCopy: true))
                throw new InvalidOperationException("探索用シーンの複製に失敗しました。");
            Scene scene = EditorSceneManager.OpenScene(SearchScene, OpenSceneMode.Single);
            result.CharacterPool = ValidateCharacterPool(scene);
            if (!CombatAutoBattleSceneSetup.TrySetupCurrentScene())
                throw new InvalidOperationException("探索用Runnerの設定に失敗しました。");

            var buildScenes = new List<string> { SearchScene };
            foreach (string guid in AssetDatabase.FindAssets("t:AuthoredMapDefinition", new[] { MapDirectory }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var map = AssetDatabase.LoadAssetAtPath<AuthoredMapDefinition>(path);
                if (map == null) continue;
                CombatMapAvailability normal = CombatMapAvailability.Evaluate(map, false);
                CombatMapAvailability reversed = CombatMapAvailability.Evaluate(map, true);
                result.Maps.Add(new MapValidation
                {
                    Name = map.name,
                    AssetPath = path,
                    NormalAvailable = normal.CanStartBattle,
                    ReversedAvailable = reversed.CanStartBattle,
                    NormalReason = normal.Reason.ToString(),
                    ReversedReason = reversed.Reason.ToString(),
                    BakedScene = map.BakedRuntimeScenePath,
                });
                if (map.HasValidBakedRuntimeScene &&
                    AssetDatabase.LoadAssetAtPath<SceneAsset>(map.BakedRuntimeScenePath) != null &&
                    !buildScenes.Contains(map.BakedRuntimeScenePath))
                    buildScenes.Add(map.BakedRuntimeScenePath);
            }
            result.Maps.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
            result.BuildScenes = buildScenes.ToArray();
            result.Executable = Path.Combine(outputDirectory, "CombatAutoBattleSearch.app");

            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneOSX &&
                !EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneOSX))
                throw new InvalidOperationException("StandaloneOSXへの切替に失敗しました。");
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = result.BuildScenes,
                locationPathName = result.Executable,
                target = BuildTarget.StandaloneOSX,
                options = BuildOptions.None,
            });
            result.BuildResult = report.summary.result.ToString();
            result.BuildGuid = report.summary.guid.ToString();
            result.Success = report.summary.result == BuildResult.Succeeded;
            if (!result.Success)
                throw new InvalidOperationException($"探索Playerのビルドに失敗しました: {result.BuildResult}");
            Debug.Log($"[編成探索] Playerをビルドしました: {result.Executable}");
        }
        catch (Exception exception)
        {
            result.Error = exception.ToString();
            Debug.LogException(exception);
        }
        File.WriteAllText(Path.Combine(outputDirectory, "build.json"), JsonUtility.ToJson(result, true));
        if (Application.isBatchMode) EditorApplication.Exit(result.Success ? 0 : 1);
    }

    private static CharacterPoolValidation ValidateCharacterPool(Scene scene)
    {
        CombatCharacterSystem system = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            system = root.GetComponentInChildren<CombatCharacterSystem>(true);
            if (system != null) break;
        }
        if (system == null) throw new InvalidOperationException("CombatCharacterSystemがありません。");
        var settings = new SerializedObject(system);
        SerializedProperty generates = settings.FindProperty("_generateCandidatesAtRuntime");
        SerializedProperty prefabProperty = settings.FindProperty("_characterPrefab");
        bool normalized = !generates.boolValue || prefabProperty.objectReferenceValue == null;
        if (normalized)
        {
            generates.boolValue = true;
            prefabProperty.objectReferenceValue = AssetDatabase.LoadAssetAtPath<Character>(
                "Assets/Prefabs/Combat/CombatCharacter.prefab");
            settings.ApplyModifiedPropertiesWithoutUndo();
        }
        var prefab = prefabProperty.objectReferenceValue as Character;
        if (prefab == null) throw new InvalidOperationException("キャラクターPrefabがありません。");
        var character = new SerializedObject(prefab);
        CombatHealth health = prefab.GetComponent<CombatHealth>();
        CombatAiBrain brain = prefab.GetComponent<CombatAiBrain>();
        NavMeshAgent agent = prefab.GetComponent<NavMeshAgent>();
        if (health == null || brain == null || agent == null)
            throw new InvalidOperationException("キャラクターPrefabの戦闘コンポーネントが不足しています。");
        var ai = new SerializedObject(brain);
        var result = new CharacterPoolValidation
        {
            GeneratesBothTeamsFromSamePrefab = true,
            NormalizedCopyScene = normalized,
            PrefabAssetPath = AssetDatabase.GetAssetPath(prefab),
            CandidatesPerTeam = 10,
            MaxHP = health.MaxHP,
            STR = character.FindProperty("_baseSTR").intValue,
            INT = character.FindProperty("_baseINT").intValue,
            FAI = character.FindProperty("_baseFAI").intValue,
            AGI = character.FindProperty("_baseAGI").intValue,
            LearnedSkillCount = character.FindProperty("_learnedSkillIds").arraySize,
            UnlockAllWeaponSkills = character.FindProperty("_unlockAllCatalogSkillsForKindWhenLearnedEmpty").boolValue,
            SkillCatalog = AssetDatabase.GetAssetPath(character.FindProperty("_skillCatalogOverride").objectReferenceValue),
            AgentSpeed = agent.speed,
            AiDecisionIntervalSeconds = settings.FindProperty("_aiDecisionIntervalSeconds").floatValue,
            AiEnabled = ai.FindProperty("_enabled").boolValue,
            ExecuteMovement = ai.FindProperty("_executeMovement").boolValue,
            ExecuteSkills = ai.FindProperty("_executeSkills").boolValue,
        };
        if (result.MaxHP < 1 || result.STR < 1 || result.INT < 1 || result.FAI < 1 || result.AGI < 1 ||
            !result.AiEnabled || !result.ExecuteMovement || !result.ExecuteSkills)
            throw new InvalidOperationException("共通キャラクターPrefabの能力またはAI設定が無効です。");
        EditorSceneManager.MarkSceneDirty(scene);
        return result;
    }

    [Serializable]
    private sealed class BuildResultData
    {
        public bool Success;
        public string CreatedAtUtc;
        public string UnityVersion;
        public string BuildGuid;
        public string BuildResult;
        public string Error;
        public string SourceScene;
        public string SearchScene;
        public string Executable;
        public string[] BuildScenes;
        public CharacterPoolValidation CharacterPool;
        public List<MapValidation> Maps = new();
    }

    [Serializable]
    private sealed class CharacterPoolValidation
    {
        public bool GeneratesBothTeamsFromSamePrefab;
        public bool NormalizedCopyScene;
        public string PrefabAssetPath;
        public int CandidatesPerTeam;
        public int MaxHP;
        public int STR;
        public int INT;
        public int FAI;
        public int AGI;
        public int LearnedSkillCount;
        public bool UnlockAllWeaponSkills;
        public string SkillCatalog;
        public float AgentSpeed;
        public float AiDecisionIntervalSeconds;
        public bool AiEnabled;
        public bool ExecuteMovement;
        public bool ExecuteSkills;
    }

    [Serializable]
    private sealed class MapValidation
    {
        public string Name;
        public string AssetPath;
        public bool NormalAvailable;
        public bool ReversedAvailable;
        public string NormalReason;
        public string ReversedReason;
        public string BakedScene;
    }
}
