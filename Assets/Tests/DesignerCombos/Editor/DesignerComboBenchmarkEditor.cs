using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using WarSimulation.Combat.Map;

public sealed class DesignerComboBenchmarkWindow : EditorWindow
{
    private DesignerComboKind _combo = DesignerComboKind.BindFollowUp;
    private DesignerComboTestScope _scope = DesignerComboTestScope.BehaviorCheck;
    private int _baseSeed = 12000;
    private float _timeoutSeconds = 120f;
    private float _timeScale = 4f;

    [MenuItem("Tools/War Simulation/Designer Combo Tests/Open Test Window")]
    public static void Open()
    {
        GetWindow<DesignerComboBenchmarkWindow>("デザイナーズコンボ試験");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("試験条件", EditorStyles.boldLabel);
        _combo = (DesignerComboKind)EditorGUILayout.EnumPopup("コンボ", _combo);
        _scope = (DesignerComboTestScope)EditorGUILayout.EnumPopup("試験範囲", _scope);
        _baseSeed = EditorGUILayout.IntField("基準シード", _baseSeed);
        _timeoutSeconds = EditorGUILayout.FloatField("一試合の制限秒数", _timeoutSeconds);
        _timeScale = EditorGUILayout.Slider("時間倍率", _timeScale, 1f, 20f);

        DesignerComboScenarioDefinition scenario = DesignerComboScenarioCatalog.Get(_combo);
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("主指標", scenario.PrimaryMetricName);
        EditorGUILayout.HelpBox(GetScopeDescription(_scope), MessageType.Info);
        if (_scope == DesignerComboTestScope.AddedMembers && scenario.ScalableRoleIndex < 0)
        {
            EditorGUILayout.HelpBox("このコンボには人数追加役が定義されていません。", MessageType.Warning);
        }

        EditorGUILayout.Space();
        if (GUILayout.Button("専用シーンを作成または修復")) DesignerComboBenchmarkSceneTool.CreateOrRepair();

        using (new EditorGUI.DisabledScope(EditorApplication.isPlaying ||
            _scope == DesignerComboTestScope.AddedMembers && scenario.ScalableRoleIndex < 0))
        {
            if (GUILayout.Button("試験を開始")) StartTest();
        }
    }

    private void StartTest()
    {
        if (!AssetDatabase.LoadAssetAtPath<SceneAsset>(DesignerComboBenchmarkSceneTool.TestScenePath))
        {
            EditorUtility.DisplayDialog("専用シーンがありません", "先に専用シーンを作成してください。", "閉じる");
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Scene scene = EditorSceneManager.OpenScene(DesignerComboBenchmarkSceneTool.TestScenePath, OpenSceneMode.Single);
        if (FindInScene<DesignerComboBenchmarkRunner>(scene) == null)
        {
            EditorUtility.DisplayDialog("実行器がありません", "専用シーンを修復してください。", "閉じる");
            return;
        }

        DesignerComboRunRequest.Store(new DesignerComboRunSettings
        {
            Combo = _combo,
            Scope = _scope,
            BaseSeed = _baseSeed,
            BattleTimeoutSeconds = Mathf.Max(10f, _timeoutSeconds),
            TimeScale = Mathf.Clamp(_timeScale, 1f, 20f),
        });
        EditorApplication.EnterPlaymode();
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

    private static string GetScopeDescription(DesignerComboTestScope scope)
    {
        return scope switch
        {
            DesignerComboTestScope.BehaviorCheck => "開けた地形で連携ありを5試合実行します。画面と記録で成立を確認します。",
            DesignerComboTestScope.Comparison => "3地形、30シード、陣営入替で連携あり・一人ずつ片側解除・通常編成を比較します。",
            DesignerComboTestScope.ExtendedComparison => "比較試合を各100シードまで増やして再判定します。",
            DesignerComboTestScope.AddedMembers => "最小構成から指定役を3人まで追加し、比較側も同人数にそろえます。",
            DesignerComboTestScope.Counter => "通常の対戦相手と弱点を狙う対抗編成を同条件で比較します。",
            _ => string.Empty,
        };
    }
}

public static class DesignerComboBenchmarkSceneTool
{
    public const string SourceScenePath = "Assets/Scenes/GafuTest.unity";
    public const string TestScenePath = "Assets/Tests/DesignerCombos/Scenes/DesignerComboBenchmark.unity";
    private const string OwnedRootName = "DesignerComboBenchmarkRoot";

    [MenuItem("Tools/War Simulation/Designer Combo Tests/Create Or Repair Test Scene")]
    public static void CreateOrRepair()
    {
        ValidateSourceScene();
        EnsureFolders();

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(TestScenePath) == null)
        {
            if (!AssetDatabase.CopyAsset(SourceScenePath, TestScenePath))
            {
                throw new InvalidOperationException("専用シーンの複製に失敗しました。");
            }
            AssetDatabase.SaveAssets();
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Scene scene = EditorSceneManager.OpenScene(TestScenePath, OpenSceneMode.Single);
        ValidateTestSceneBase(scene);

        GameObject ownedRoot = FindDirectRoot(scene, OwnedRootName);
        if (ownedRoot == null)
        {
            ownedRoot = new GameObject(OwnedRootName);
            SceneManager.MoveGameObjectToScene(ownedRoot, scene);
        }

        if (ownedRoot.GetComponent<DesignerComboBenchmarkRunner>() == null)
        {
            ownedRoot.AddComponent<DesignerComboBenchmarkRunner>();
        }

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("専用シーンを保存できませんでした。");
        Selection.activeGameObject = ownedRoot;
        Debug.Log($"[デザイナーズコンボテスト] 専用シーンを準備しました: {TestScenePath}");
    }

    private static void ValidateSourceScene()
    {
        Scene loaded = SceneManager.GetSceneByPath(SourceScenePath);
        bool openedForValidation = !loaded.IsValid() || !loaded.isLoaded;
        Scene scene = openedForValidation
            ? EditorSceneManager.OpenScene(SourceScenePath, OpenSceneMode.Additive)
            : loaded;
        try
        {
            ValidateTestSceneBase(scene);
            CombatCharacterSystem characterSystem = FindInScene<CombatCharacterSystem>(scene);
            int characters = characterSystem != null
                ? characterSystem.AllyCharacters.Count + characterSystem.EnemyCharacters.Count
                : 0;
            if (characters == 0) throw new InvalidOperationException("元シーンにキャラクターがいません。");
        }
        finally
        {
            if (openedForValidation) EditorSceneManager.CloseScene(scene, removeScene: true);
        }
    }

    private static void ValidateTestSceneBase(Scene scene)
    {
        if (FindInScene<CombatCharacterSystem>(scene) == null) throw new InvalidOperationException("CombatCharacterSystemがありません。");
        if (FindInScene<CombatBattleFlow>(scene) == null) throw new InvalidOperationException("CombatBattleFlowがありません。");
        if (FindInScene<MapGenerator>(scene) == null) throw new InvalidOperationException("MapGeneratorがありません。");
        if (FindInScene<CombatSceneContext>(scene) == null) throw new InvalidOperationException("CombatSceneContextがありません。");
    }

    private static void EnsureFolders()
    {
        EnsureFolder("Assets/Tests", "DesignerCombos");
        EnsureFolder("Assets/Tests/DesignerCombos", "Scenes");
    }

    private static void EnsureFolder(string parent, string child)
    {
        string path = parent + "/" + child;
        if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, child);
    }

    private static GameObject FindDirectRoot(Scene scene, string name)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i].name == name) return roots[i];
        }
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
        var found = new System.Collections.Generic.List<T>();
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++) found.AddRange(roots[i].GetComponentsInChildren<T>(true));
        return found.ToArray();
    }
}
