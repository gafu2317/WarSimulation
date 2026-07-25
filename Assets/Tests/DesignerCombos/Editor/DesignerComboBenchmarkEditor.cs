using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using WarSimulation.Combat.Map;

public sealed class DesignerComboBenchmarkWindow : EditorWindow
{
    private DesignerComboKind _combo = DesignerComboKind.BindFollowUp;
    private bool _runAllCombos;
    private DesignerComboTestScope _scope = DesignerComboTestScope.BehaviorCheck;
    private int _baseSeed = 12000;
    private float _timeoutSeconds = 120f;
    private float _timeScale = 4f;
    private bool _disableRendering;

    [MenuItem("Tools/War Simulation/Designer Combo Tests/Open Test Window")]
    public static void Open()
    {
        GetWindow<DesignerComboBenchmarkWindow>("デザイナーズコンボ試験");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("試験条件", EditorStyles.boldLabel);
        _runAllCombos = EditorGUILayout.Toggle("全コンボを連続実行", _runAllCombos);
        using (new EditorGUI.DisabledScope(_runAllCombos))
        {
            _combo = (DesignerComboKind)EditorGUILayout.EnumPopup("コンボ", _combo);
        }
        _scope = (DesignerComboTestScope)EditorGUILayout.EnumPopup("試験範囲", _scope);
        _baseSeed = EditorGUILayout.IntField("基準シード", _baseSeed);
        _timeoutSeconds = EditorGUILayout.FloatField("一試合の最大戦闘秒数", _timeoutSeconds);
        _disableRendering = EditorGUILayout.Toggle("描画なし高速モード", _disableRendering);
        _timeScale = EditorGUILayout.Slider("時間倍率", _timeScale, 1f, 20f);

        DesignerComboScenarioDefinition scenario = DesignerComboScenarioCatalog.Get(_combo);
        int comboCount = GetRunnableComboCount(_scope, _runAllCombos);
        int matchCount = GetPlannedMatchCount(_scope, _runAllCombos, scenario);
        EditorGUILayout.Space();
        if (_runAllCombos)
        {
            EditorGUILayout.LabelField("対象", $"{comboCount}コンボ / 約{matchCount}試合");
            EditorGUILayout.HelpBox("対象コンボを順番に実行し、結果ファイルをコンボごとに保存します。", MessageType.Info);
        }
        else
        {
            EditorGUILayout.LabelField("主指標", scenario.PrimaryMetricName);
            EditorGUILayout.LabelField("予定試合数", $"約{matchCount}試合");
        }
        EditorGUILayout.LabelField(
            "全試合が時間切れの場合",
            $"理論上約{FormatDuration(matchCount * _timeoutSeconds / Mathf.Max(1f, _timeScale))}");
        EditorGUILayout.HelpBox(
            $"最大戦闘秒数はゲーム内時間です。{_timeoutSeconds:0.#}秒・{_timeScale:0.#}倍速なら、時間切れまでの実時間は理論上約{_timeoutSeconds / Mathf.Max(1f, _timeScale):0.#}秒です。低FPS時は長くなります。",
            MessageType.Info);
        if (_disableRendering)
        {
            EditorGUILayout.HelpBox("実行中はGame画面が真っ暗になります。進捗はConsoleへ出力します。", MessageType.None);
        }
        EditorGUILayout.HelpBox(GetScopeDescription(_scope), MessageType.Info);
        if (_scope == DesignerComboTestScope.AddedMembers && _runAllCombos)
        {
            EditorGUILayout.HelpBox("人数追加役が未定義のコンボは一括実行から除外します。", MessageType.Warning);
        }
        else if (_scope == DesignerComboTestScope.AddedMembers && scenario.ScalableRoleIndex < 0)
        {
            EditorGUILayout.HelpBox("このコンボには人数追加役が定義されていません。", MessageType.Warning);
        }

        EditorGUILayout.Space();
        if (GUILayout.Button("専用シーンを作成または修復")) DesignerComboBenchmarkSceneTool.CreateOrRepair();

        using (new EditorGUI.DisabledScope(EditorApplication.isPlaying ||
            !_runAllCombos && _scope == DesignerComboTestScope.AddedMembers && scenario.ScalableRoleIndex < 0))
        {
            if (GUILayout.Button(_runAllCombos ? "全コンボの試験を開始" : "試験を開始")) StartTest();
        }
    }

    private void StartTest()
    {
        StartTest(new DesignerComboRunSettings
        {
            Combo = _combo,
            RunAllCombos = _runAllCombos,
            DisableRendering = _disableRendering,
            Scope = _scope,
            BaseSeed = _baseSeed,
            BattleTimeoutSeconds = Mathf.Max(10f, _timeoutSeconds),
            TimeScale = Mathf.Clamp(_timeScale, 1f, 20f),
        });
    }

    private static void StartTest(DesignerComboRunSettings settings)
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

        DesignerComboRunRequest.Store(settings);
        EditorApplication.EnterPlaymode();
    }

    private static int GetRunnableComboCount(DesignerComboTestScope scope, bool runAllCombos)
    {
        if (!runAllCombos) return 1;

        int count = 0;
        for (int i = 0; i < DesignerComboScenarioCatalog.All.Count; i++)
        {
            DesignerComboScenarioDefinition scenario = DesignerComboScenarioCatalog.All[i];
            if (scope != DesignerComboTestScope.AddedMembers || scenario.ScalableRoleIndex >= 0) count++;
        }
        return count;
    }

    private static int GetPlannedMatchCount(
        DesignerComboTestScope scope,
        bool runAllCombos,
        DesignerComboScenarioDefinition selectedScenario)
    {
        if (!runAllCombos) return DesignerComboBenchmarkRunner.EstimateMatchCount(selectedScenario, scope);

        int count = 0;
        for (int i = 0; i < DesignerComboScenarioCatalog.All.Count; i++)
        {
            count += DesignerComboBenchmarkRunner.EstimateMatchCount(DesignerComboScenarioCatalog.All[i], scope);
        }
        return count;
    }

    private static string FormatDuration(float seconds)
    {
        TimeSpan duration = TimeSpan.FromSeconds(Mathf.Max(0f, seconds));
        if (duration.TotalHours >= 1d) return $"{(int)duration.TotalHours}時間{duration.Minutes}分";
        if (duration.TotalMinutes >= 1d) return $"{duration.Minutes}分{duration.Seconds}秒";
        return $"{duration.Seconds}秒";
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
            DesignerComboTestScope.Comparison => "3地形、30シード、陣営入替で連携あり・一人ずつ片側解除・通常編成を比較します。基準付近の結果は100シードまで自動延長します。",
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

        DesignerComboBenchmarkRunner runner = ownedRoot.GetComponent<DesignerComboBenchmarkRunner>();
        if (runner == null)
        {
            runner = ownedRoot.AddComponent<DesignerComboBenchmarkRunner>();
        }

        if (runner == null) throw new InvalidOperationException("DesignerComboBenchmarkRunnerを追加できませんでした。Consoleのコンパイルエラーを確認してください。");

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
