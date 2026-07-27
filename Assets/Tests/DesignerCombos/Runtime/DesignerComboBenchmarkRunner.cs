using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using WarSimulation.Combat.Map;

[DefaultExecutionOrder(-500)]
[DisallowMultipleComponent]
public sealed class DesignerComboBenchmarkRunner : MonoBehaviour
{
    public const int StoneAttackDiagnosticSeedCount = 5;
    public const int StoneAttackDiagnosticMatchCount = StoneAttackDiagnosticSeedCount * 2;

    [SerializeField] private DesignerComboKind _combo = DesignerComboKind.BindFollowUp;
    [SerializeField] private bool _runAllCombos;
    [SerializeField] private bool _disableRendering;
    [SerializeField] private bool _useStoneAttackDiagnosticMap;
    [SerializeField, Range(2, 3)] private int _diagnosticAttackRoleCount = 2;
    [SerializeField] private DesignerComboTestScope _scope = DesignerComboTestScope.BehaviorCheck;
    [SerializeField] private int _baseSeed = 12000;
    [SerializeField, Min(10f)] private float _battleTimeoutSeconds = DesignerComboRunSettings.DefaultBattleTimeoutSeconds;
    [SerializeField, Range(1f, 20f)] private float _timeScale = DesignerComboRunSettings.DefaultTimeScale;

    private readonly List<Character> _characterPool = new();
    private readonly List<UnityEngine.Object> _temporaryObjects = new();
    private readonly Dictionary<WeaponKind, WeaponConfig> _weapons = new();
    private readonly Dictionary<CombatAiPersonalityKind, CombatAiPersonalityProfile> _personalities = new();
    private readonly List<DesignerComboMatchResult> _results = new();
    private readonly List<Camera> _disabledCameras = new();
    private readonly List<Canvas> _disabledCanvases = new();
    private readonly List<CharacterAnimationController> _disabledAnimationControllers = new();
    private CombatCharacterSystem _characterSystem;
    private CombatBattleFlow _battleFlow;
    private MapGenerator _mapGenerator;
    private MapGenerationConfig _originalMapConfig;
    private GameObject _runtimeRoot;
    private bool _running;
    private int _previousVSyncCount;
    private int _previousTargetFrameRate;
    private bool _previousRunInBackground;
    private string _outputDirectory;
    private bool _quitWhenFinished;

    private void Awake()
    {
        HideInteractiveCombatUi();
    }

    private void Start()
    {
        HideInteractiveCombatUi();
        if (DesignerComboRunRequest.TryConsume(out DesignerComboRunSettings settings))
        {
            _combo = settings.Combo;
            _runAllCombos = settings.RunAllCombos;
            _disableRendering = settings.DisableRendering;
            _useStoneAttackDiagnosticMap = settings.UseStoneAttackDiagnosticMap;
            _diagnosticAttackRoleCount = Mathf.Clamp(settings.DiagnosticAttackRoleCount, 2, 3);
            _scope = settings.Scope;
            _baseSeed = settings.BaseSeed;
            _battleTimeoutSeconds = settings.BattleTimeoutSeconds;
            _timeScale = settings.TimeScale;
            _outputDirectory = settings.OutputDirectory;
            _quitWhenFinished = settings.QuitWhenFinished;
            if (_useStoneAttackDiagnosticMap)
            {
                _combo = DesignerComboKind.MagicStoneAssault;
                _runAllCombos = false;
                _disableRendering = false;
                _scope = DesignerComboTestScope.BehaviorCheck;
            }
            StartCoroutine(Run());
            return;
        }
    }

    private static void HideInteractiveCombatUi()
    {
        CombatFlow[] flows = FindObjectsByType<CombatFlow>(FindObjectsInactive.Include);
        for (int i = 0; i < flows.Length; i++)
        {
            flows[i].enabled = false;
            Canvas canvas = flows[i].GetComponentInParent<Canvas>(includeInactive: true);
            if (canvas != null) canvas.enabled = false;
        }
    }

    [ContextMenu("選択中のデザイナーズコンボテストを開始")]
    public void StartSelectedTest()
    {
        if (Application.isPlaying && !_running) StartCoroutine(Run());
    }

    private IEnumerator Run()
    {
        if (_useStoneAttackDiagnosticMap && Application.isEditor)
        {
            Debug.LogError("[デザイナーズコンボテスト] 地形診断はStandalone Player専用です。Editorでは実行しません。", this);
            yield break;
        }

        _running = true;
        float previousTimeScale = Time.timeScale;
        _previousRunInBackground = Application.runInBackground;
        Application.runInBackground = false;
        if (_disableRendering) EnableNoRenderingMode();

        if (!TryResolveDependencies(out string error))
        {
            Debug.LogError("[デザイナーズコンボテスト] " + error, this);
            Time.timeScale = previousTimeScale;
            DisableNoRenderingMode();
            Application.runInBackground = _previousRunInBackground;
            _running = false;
            QuitStandalonePlayer(1);
            yield break;
        }

        try
        {
            List<DesignerComboScenarioDefinition> scenarios = BuildScenariosToRun();
            Time.timeScale = _timeScale;
            Debug.Log($"[デザイナーズコンボテスト] {scenarios.Count}コンボを順番に実行します。", this);
            for (int i = 0; i < scenarios.Count; i++)
            {
                DesignerComboScenarioDefinition scenario = scenarios[i];
                Debug.Log($"[デザイナーズコンボテスト] コンボ {i + 1}/{scenarios.Count}: {scenario.DisplayName}", this);
                yield return RunScenario(scenario, _timeScale);
            }

            Debug.Log($"[デザイナーズコンボテスト] 全{scenarios.Count}コンボが完了しました。", this);
        }
        finally
        {
            Time.timeScale = previousTimeScale;
            DisableNoRenderingMode();
            Application.runInBackground = _previousRunInBackground;
            CleanupTemporaryObjects();
            _running = false;
            QuitStandalonePlayer(0);
        }
    }

    private void QuitStandalonePlayer(int exitCode)
    {
        if (!Application.isEditor && _quitWhenFinished)
        {
            Application.Quit(exitCode);
        }
    }

    private IEnumerator RunScenario(DesignerComboScenarioDefinition scenario, float timeScale)
    {
        _results.Clear();
        List<DesignerComboMatchPlan> plans = _useStoneAttackDiagnosticMap
            ? BuildStoneAttackDiagnosticPlans(_baseSeed)
            : BuildPlans(scenario, _scope, _baseSeed);
        int initialPlanCount = plans.Count;
        bool extensionChecked = false;
        Debug.Log($"[デザイナーズコンボテスト] {scenario.DisplayName}を{plans.Count}試合実行します。", this);

        for (int i = 0; i < plans.Count; i++)
        {
            if (plans.Count <= 10) Debug.Log($"[デザイナーズコンボテスト] {i + 1}/{plans.Count}試合目を開始します。", this);
            yield return RunMatchSafely(scenario, plans[i]);
            if (plans.Count <= 10 || (i + 1) % 10 == 0 || i + 1 == plans.Count)
            {
                Debug.Log($"[デザイナーズコンボテスト] {i + 1}/{plans.Count}試合完了", this);
            }

            if (!extensionChecked &&
                _scope == DesignerComboTestScope.Comparison &&
                i + 1 == initialPlanCount)
            {
                extensionChecked = true;
                if (ShouldExtendComparison(_results))
                {
                    plans.AddRange(BuildComparisonExtensionPlans(scenario, _baseSeed));
                    Debug.Log($"[デザイナーズコンボテスト] 基準付近のため各100試合まで延長します。合計{plans.Count}試合", this);
                }
            }
        }

        string reportPath = DesignerComboReportWriter.Write(
            scenario,
            _scope,
            timeScale,
            _results,
            _outputDirectory);
        Debug.Log($"[デザイナーズコンボテスト] {scenario.DisplayName} {timeScale:0.#}倍完了: {reportPath}", this);
    }

    private void EnableNoRenderingMode()
    {
        _previousVSyncCount = QualitySettings.vSyncCount;
        _previousTargetFrameRate = Application.targetFrameRate;
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = -1;

        Camera[] cameras = FindObjectsByType<Camera>(FindObjectsInactive.Include);
        for (int i = 0; i < cameras.Length; i++)
        {
            if (!cameras[i].enabled) continue;
            cameras[i].enabled = false;
            _disabledCameras.Add(cameras[i]);
        }

        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include);
        for (int i = 0; i < canvases.Length; i++)
        {
            if (!canvases[i].enabled) continue;
            canvases[i].enabled = false;
            _disabledCanvases.Add(canvases[i]);
        }

        CharacterAnimationController[] animationControllers =
            FindObjectsByType<CharacterAnimationController>(FindObjectsInactive.Include);
        for (int i = 0; i < animationControllers.Length; i++)
        {
            if (!animationControllers[i].enabled) continue;
            animationControllers[i].enabled = false;
            _disabledAnimationControllers.Add(animationControllers[i]);
        }
        Debug.Log("[デザイナーズコンボテスト] 描画なし高速モードを有効にしました。進捗はConsoleで確認してください。", this);
    }

    private void DisableNoRenderingMode()
    {
        if (!_disableRendering) return;

        for (int i = 0; i < _disabledCameras.Count; i++)
        {
            if (_disabledCameras[i] != null) _disabledCameras[i].enabled = true;
        }
        for (int i = 0; i < _disabledCanvases.Count; i++)
        {
            if (_disabledCanvases[i] != null) _disabledCanvases[i].enabled = true;
        }
        for (int i = 0; i < _disabledAnimationControllers.Count; i++)
        {
            if (_disabledAnimationControllers[i] != null) _disabledAnimationControllers[i].enabled = true;
        }
        _disabledCameras.Clear();
        _disabledCanvases.Clear();
        _disabledAnimationControllers.Clear();
        QualitySettings.vSyncCount = _previousVSyncCount;
        Application.targetFrameRate = _previousTargetFrameRate;
    }

    private List<DesignerComboScenarioDefinition> BuildScenariosToRun()
    {
        if (!_runAllCombos) return new List<DesignerComboScenarioDefinition> { DesignerComboScenarioCatalog.Get(_combo) };

        var scenarios = new List<DesignerComboScenarioDefinition>();
        for (int i = 0; i < DesignerComboScenarioCatalog.All.Count; i++)
        {
            DesignerComboScenarioDefinition scenario = DesignerComboScenarioCatalog.All[i];
            if (_scope == DesignerComboTestScope.AddedMembers && scenario.ScalableRoleIndex < 0)
            {
                Debug.Log($"[デザイナーズコンボテスト] {scenario.DisplayName}は人数追加役が未定義のため除外します。", this);
                continue;
            }
            scenarios.Add(scenario);
        }
        return scenarios;
    }

    private IEnumerator RunMatchSafely(DesignerComboScenarioDefinition scenario, DesignerComboMatchPlan plan)
    {
        IEnumerator routine = RunMatch(scenario, plan);
        while (true)
        {
            bool moved;
            object current = null;
            try
            {
                moved = routine.MoveNext();
                if (moved) current = routine.Current;
            }
            catch (Exception exception)
            {
                _results.Add(new DesignerComboMatchResult
                {
                    Combo = scenario.DisplayName,
                    Variant = plan.Label,
                    Terrain = plan.Terrain.ToString(),
                    Seed = plan.Seed,
                    SidesSwapped = plan.SidesSwapped,
                    PrimaryMetricName = scenario.PrimaryMetricName,
                    Outcome = "実行失敗",
                    Error = exception.ToString(),
                });
                Debug.LogException(exception, this);
                break;
            }

            if (!moved) break;
            yield return current;
        }

        if (routine is IDisposable disposable) disposable.Dispose();
    }

    private IEnumerator RunMatch(DesignerComboScenarioDefinition scenario, DesignerComboMatchPlan plan)
    {
        DesignerComboMetricsCollector collector = null;
        MapGenerationConfig terrainConfig = null;
        try
        {
            UnityEngine.Random.InitState(plan.Seed);
            terrainConfig = CreateTerrainConfig(plan.Terrain);
            _mapGenerator.Config = terrainConfig;
            MapData map = _useStoneAttackDiagnosticMap
                ? DesignerComboDiagnosticMapFactory.CreateAsymmetricStoneDefenseMap(terrainConfig, plan.Seed)
                : _mapGenerator.Generate(plan.Seed);
            if (map == null) throw new InvalidOperationException("マップ生成に失敗しました。");
            if (_useStoneAttackDiagnosticMap)
            {
                CombatMapSystem mapSystem = CombatSceneContext.Instance != null
                    ? CombatSceneContext.Instance.MapSystem
                    : FindAnyObjectByType<CombatMapSystem>();
                if (mapSystem == null) throw new InvalidOperationException("診断マップを設定するCombatMapSystemがありません。");
                mapSystem.SetCurrentMap(map);
            }
            _mapGenerator.Clear3D();
            yield return null;
            _mapGenerator.Render3D(map);
            yield return null;

            BuildParticipants(scenario, plan, out List<Character> comboMembers, out List<Character> opponents);
            CombatTeam comboTeam = plan.SidesSwapped ? CombatTeam.Enemy : CombatTeam.Ally;
            AssignParticipants(comboMembers, opponents, plan.SidesSwapped);
            ConfigureRelationships(scenario, comboMembers);

            bool ended = false;
            CombatBattleState endState = CombatBattleState.WaitingToStart;
            void OnBattleEnded(CombatBattleState state)
            {
                ended = true;
                endState = state;
            }

            _battleFlow.BattleEnded += OnBattleEnded;
            _battleFlow.StartBattleOnCurrentMap();
            if (_battleFlow.State != CombatBattleState.Running)
            {
                _battleFlow.BattleEnded -= OnBattleEnded;
                throw new InvalidOperationException("戦闘を開始できませんでした。");
            }

            collector = new DesignerComboMetricsCollector(
                scenario,
                plan.Variant,
                plan.Terrain,
                plan.Seed,
                plan.SidesSwapped,
                comboMembers,
                opponents,
                comboTeam,
                _useStoneAttackDiagnosticMap ? _diagnosticAttackRoleCount : 1);
            collector.Begin();
            float startedAt = Time.time;
            float startedAtRealtime = Time.realtimeSinceStartup;
            int startedAtFrame = Time.frameCount;
            while (!ended &&
                _battleFlow.State == CombatBattleState.Running &&
                Time.time - startedAt < _battleTimeoutSeconds)
            {
                collector.Sample();
                yield return null;
            }

            if (!ended && (_battleFlow.State == CombatBattleState.Victory || _battleFlow.State == CombatBattleState.Defeat))
            {
                ended = true;
                endState = _battleFlow.State;
            }

            bool timedOut = !ended;
            _battleFlow.BattleEnded -= OnBattleEnded;
            if (timedOut)
            {
                Debug.LogWarning($"[デザイナーズコンボテスト] {plan.Label} seed={plan.Seed} は制限時間で終了しました。", this);
                _battleFlow.ResetBattle();
                StopParticipants(comboMembers);
                StopParticipants(opponents);
            }

            DesignerComboMatchResult result = collector.Complete(endState, timedOut);
            result.Variant = plan.Label;
            if (_useStoneAttackDiagnosticMap)
            {
                result.Terrain = DesignerComboDiagnosticMapFactory.AsymmetricStoneDefenseMapName;
                result.PrimaryMetricName = $"攻撃役{_diagnosticAttackRoleCount}人の魔石ダメージ";
            }
            result.RealSeconds = Mathf.Max(0f, Time.realtimeSinceStartup - startedAtRealtime);
            result.FrameCount = Mathf.Max(0, Time.frameCount - startedAtFrame);
            result.AverageFramesPerSecond = result.FrameCount / Mathf.Max(0.001f, result.RealSeconds);
            _results.Add(result);
        }
        finally
        {
            collector?.Dispose();
            if (terrainConfig != null) Destroy(terrainConfig);
            _mapGenerator.Config = _originalMapConfig;
        }
    }

    private bool TryResolveDependencies(out string error)
    {
        _characterSystem = CombatSceneContext.Instance != null ? CombatSceneContext.Instance.CharacterSystem : null;
        _battleFlow = CombatSceneContext.Instance != null ? CombatSceneContext.Instance.BattleFlow : null;
        _characterSystem ??= FindAnyObjectByType<CombatCharacterSystem>();
        _battleFlow ??= FindAnyObjectByType<CombatBattleFlow>();
        _mapGenerator = FindAnyObjectByType<MapGenerator>();
        if (_characterSystem == null || _battleFlow == null || _mapGenerator == null)
        {
            error = "CombatCharacterSystem、CombatBattleFlow、MapGeneratorが必要です。専用シーンを作り直してください。";
            return false;
        }

        if (_mapGenerator.Config == null)
        {
            error = "MapGeneratorの設定がありません。";
            return false;
        }

        _originalMapConfig = _mapGenerator.Config;
        _characterPool.Clear();
        AddUnique(_characterPool, _characterSystem.AllyCharacters);
        AddUnique(_characterPool, _characterSystem.EnemyCharacters);
        _characterPool.Sort(CompareCharactersByHierarchy);
        if (_characterPool.Count == 0)
        {
            error = "雛形にできるキャラクターがいません。";
            return false;
        }

        LoadWeapons();
        foreach (DesignerComboScenarioDefinition scenario in DesignerComboScenarioCatalog.All)
        {
            if (!ValidateRoleWeapons(scenario.Roles, out error) ||
                !ValidateRoleWeapons(scenario.CounterRoles, out error))
            {
                return false;
            }
        }

        _runtimeRoot = new GameObject("DesignerComboBenchmarkRuntime");
        _temporaryObjects.Add(_runtimeRoot);
        error = null;
        return true;
    }

    private bool ValidateRoleWeapons(IReadOnlyList<DesignerComboRoleDefinition> roles, out string error)
    {
        for (int i = 0; i < roles.Count; i++)
        {
            if (_weapons.ContainsKey(roles[i].Weapon)) continue;
            error = $"{roles[i].Weapon}のWeaponConfigがありません。";
            return false;
        }

        error = null;
        return true;
    }

    private void BuildParticipants(
        DesignerComboScenarioDefinition scenario,
        DesignerComboMatchPlan plan,
        out List<Character> comboMembers,
        out List<Character> opponents)
    {
        List<DesignerComboRoleDefinition> comboRoles = BuildComboRoles(scenario, plan);
        List<DesignerComboRoleDefinition> opponentRoles = BuildOpponentRoles(scenario, plan, comboRoles);
        EnsureCharacterPool(comboRoles.Count + opponentRoles.Count);
        comboMembers = new List<Character>(comboRoles.Count);
        opponents = new List<Character>(opponentRoles.Count);

        for (int i = 0; i < comboRoles.Count; i++)
        {
            Character character = _characterPool[i];
            ConfigureCharacter(character, comboRoles[i], $"連携_{comboRoles[i].Id}_{i + 1}");
            comboMembers.Add(character);
        }

        for (int i = 0; i < opponentRoles.Count; i++)
        {
            Character character = _characterPool[comboRoles.Count + i];
            ConfigureCharacter(character, opponentRoles[i], $"対戦相手_{opponentRoles[i].Id}_{i + 1}");
            opponents.Add(character);
        }
    }

    private List<DesignerComboRoleDefinition> BuildComboRoles(DesignerComboScenarioDefinition scenario, DesignerComboMatchPlan plan)
    {
        if (_useStoneAttackDiagnosticMap)
        {
            return DesignerComboDiagnosticMapFactory.CreateFiveCharacterBaselineRoles(_diagnosticAttackRoleCount);
        }

        var roles = new List<DesignerComboRoleDefinition>();
        for (int i = 0; i < scenario.Roles.Length; i++)
        {
            DesignerComboRoleDefinition source = scenario.Roles[i];
            CombatAiPersonalityKind personality = source.Personality;
            if (plan.Variant == DesignerComboVariantKind.Normal ||
                plan.Variant == DesignerComboVariantKind.Ablated && plan.AblatedRoleIndex == i)
            {
                personality = CombatAiPersonalityKind.Neutral;
            }

            roles.Add(new DesignerComboRoleDefinition(source.Id, source.Weapon, personality));
        }

        if (plan.AddedMembers > 0 && scenario.ScalableRoleIndex >= 0)
        {
            DesignerComboRoleDefinition scalable = scenario.Roles[scenario.ScalableRoleIndex];
            for (int i = 0; i < plan.AddedMembers; i++)
            {
                roles.Add(new DesignerComboRoleDefinition(scalable.Id + "追加", scalable.Weapon, scalable.Personality));
            }
        }

        return roles;
    }

    private static List<DesignerComboRoleDefinition> BuildOpponentRoles(
        DesignerComboScenarioDefinition scenario,
        DesignerComboMatchPlan plan,
        List<DesignerComboRoleDefinition> comboRoles)
    {
        int count = comboRoles.Count;
        var roles = new List<DesignerComboRoleDefinition>(count);
        DesignerComboRoleDefinition[] sources = plan.Variant == DesignerComboVariantKind.Counter ? scenario.CounterRoles : null;
        for (int i = 0; i < count; i++)
        {
            DesignerComboRoleDefinition source = sources != null ? sources[i % sources.Length] : comboRoles[i];
            CombatAiPersonalityKind personality = plan.Variant == DesignerComboVariantKind.Counter
                ? source.Personality
                : CombatAiPersonalityKind.Neutral;
            roles.Add(new DesignerComboRoleDefinition(source.Id, source.Weapon, personality));
        }

        return roles;
    }

    private void AssignParticipants(List<Character> combo, List<Character> opponents, bool swapped)
    {
        var allies = new List<CombatParticipantSetup>();
        var enemies = new List<CombatParticipantSetup>();
        AddSetups(swapped ? opponents : combo, allies);
        AddSetups(swapped ? combo : opponents, enemies);
        _characterSystem.SetParticipants(allies, enemies);
    }

    private static void AddSetups(List<Character> characters, List<CombatParticipantSetup> destination)
    {
        for (int i = 0; i < characters.Count; i++)
        {
            Character character = characters[i];
            destination.Add(new CombatParticipantSetup(character, character.EquippedWeaponConfig, character.PersonalityProfile));
        }
    }

    private void ConfigureCharacter(Character character, DesignerComboRoleDefinition role, string objectName)
    {
        character.gameObject.name = objectName;
        character.ConfigureForBattle(_weapons[role.Weapon], GetPersonality(role.Personality));
    }

    private void ConfigureRelationships(DesignerComboScenarioDefinition scenario, List<Character> members)
    {
        if (!scenario.RequiresLovers && !scenario.RequiresOppositeGenders) return;
        for (int i = 0; i < members.Count; i++)
        {
            Character member = members[i];
            if (member == null || member.CharacterData == null) continue;
            WeaponConfig weapon = member.EquippedWeaponConfig;
            CombatAiPersonalityProfile personality = member.PersonalityProfile;
            CharacterData data = Instantiate(member.CharacterData);
            data.name = member.CharacterData.name + "_DesignerComboTest";
            _temporaryObjects.Add(data);
            data.ConfigureIdentity(
                i % 2 == 0 ? CharacterGender.Male : CharacterGender.Female,
                null);
            member.SetCharacterDataForBattle(data);
            member.ConfigureForBattle(weapon, personality);
        }

        if (!scenario.RequiresLovers || members.Count < 2) return;
        SetLover(members[0].CharacterData, members[1].CharacterData);
        SetLover(members[1].CharacterData, members[0].CharacterData);
    }

    private static void SetLover(CharacterData owner, CharacterData lover)
    {
        if (owner == null) throw new InvalidOperationException("CharacterDataの恋人情報を設定できません。");
        owner.ConfigureIdentity(owner.Gender, lover);
    }

    private MapGenerationConfig CreateTerrainConfig(DesignerComboTerrainKind terrain)
    {
        MapGenerationConfig config = Instantiate(_originalMapConfig);
        config.name = _originalMapConfig.name + "_" + terrain;
        switch (terrain)
        {
            case DesignerComboTerrainKind.Production:
                break;
            case DesignerComboTerrainKind.Open:
                config.ConfigureFeatureCounts(0, 0, 0, 0, config.BridgesPerRiver);
                break;
            case DesignerComboTerrainKind.Forest:
                config.ConfigureFeatureCounts(6, 50, 0, config.LakeCount, config.BridgesPerRiver);
                break;
            case DesignerComboTerrainKind.ChokePoint:
                config.ConfigureFeatureCounts(0, 0, 2, config.LakeCount, 1);
                break;
        }

        return config;
    }

    private void EnsureCharacterPool(int required)
    {
        Character template = _characterPool[0];
        while (_characterPool.Count < required)
        {
            GameObject clone = Instantiate(template.gameObject, _runtimeRoot.transform);
            clone.name = "DesignerComboCharacter_" + (_characterPool.Count + 1);
            clone.SetActive(true);
            _temporaryObjects.Add(clone);
            _characterPool.Add(clone.GetComponent<Character>());
        }
    }

    private void LoadWeapons()
    {
        _weapons.Clear();
        CombatCharacterSelection selection =
            FindAnyObjectByType<CombatCharacterSelection>(FindObjectsInactive.Include);
        IReadOnlyList<WeaponConfig> options = selection != null
            ? selection.WeaponOptions
            : Array.Empty<WeaponConfig>();
        for (int i = 0; i < options.Count; i++)
        {
            WeaponConfig config = options[i];
            if (config == null || config.Kind == WeaponKind.Unarmed) continue;
            string canonicalName = config.Kind + "WeaponConfig";
            if (!_weapons.ContainsKey(config.Kind) || config.name == canonicalName) _weapons[config.Kind] = config;
        }
    }

    private CombatAiPersonalityProfile GetPersonality(CombatAiPersonalityKind kind)
    {
        if (_personalities.TryGetValue(kind, out CombatAiPersonalityProfile profile)) return profile;
        profile = CombatAiPersonalityProfile.CreateBuiltInProfile(kind);
        _personalities[kind] = profile;
        _temporaryObjects.Add(profile);
        return profile;
    }

    private void CleanupTemporaryObjects()
    {
        for (int i = _temporaryObjects.Count - 1; i >= 0; i--)
        {
            if (_temporaryObjects[i] != null) Destroy(_temporaryObjects[i]);
        }

        _temporaryObjects.Clear();
        _personalities.Clear();
    }

    private static void StopParticipants(IReadOnlyList<Character> characters)
    {
        for (int i = 0; i < characters.Count; i++) characters[i]?.GetComponent<CombatCharacterBody>()?.Stop();
    }

    private static void AddUnique(List<Character> destination, IReadOnlyList<Character> source)
    {
        if (source == null) return;
        for (int i = 0; i < source.Count; i++)
        {
            if (source[i] != null && !destination.Contains(source[i])) destination.Add(source[i]);
        }
    }

    private static int CompareCharactersByHierarchy(Character left, Character right)
    {
        return string.CompareOrdinal(
            BuildHierarchyKey(left != null ? left.transform : null),
            BuildHierarchyKey(right != null ? right.transform : null));
    }

    private static string BuildHierarchyKey(Transform current)
    {
        if (current == null) return string.Empty;

        string key = current.GetSiblingIndex().ToString("D4", CultureInfo.InvariantCulture);
        while (current.parent != null)
        {
            current = current.parent;
            key = current.GetSiblingIndex().ToString("D4", CultureInfo.InvariantCulture) + "/" + key;
        }

        return current.gameObject.scene.path + ":" + key;
    }

    private static List<DesignerComboMatchPlan> BuildPlans(
        DesignerComboScenarioDefinition scenario,
        DesignerComboTestScope scope,
        int baseSeed)
    {
        var plans = new List<DesignerComboMatchPlan>();
        int matches = scope == DesignerComboTestScope.ExtendedComparison ? 100 : 30;
        if (scope == DesignerComboTestScope.BehaviorCheck)
        {
            AddPlans(plans, DesignerComboVariantKind.Linked, "連携あり", GetTerrainForScope(scope), baseSeed, 5, includeSwapped: false);
            return plans;
        }

        DesignerComboTerrainKind terrain = GetTerrainForScope(scope);
        int terrainSeed = baseSeed;
        if (scope == DesignerComboTestScope.Comparison || scope == DesignerComboTestScope.ExtendedComparison)
        {
            AddPlans(plans, DesignerComboVariantKind.Linked, "連携あり", terrain, terrainSeed, matches, includeSwapped: true);
            for (int role = 0; role < scenario.Roles.Length; role++)
            {
                AddPlans(plans, DesignerComboVariantKind.Ablated, "片側解除:" + scenario.Roles[role].Id, terrain, terrainSeed, matches, includeSwapped: true, ablatedRole: role);
            }
            AddPlans(plans, DesignerComboVariantKind.Normal, "通常編成", terrain, terrainSeed, matches, includeSwapped: true);
        }
        else if (scope == DesignerComboTestScope.Counter)
        {
            AddPlans(plans, DesignerComboVariantKind.Linked, "対抗前", terrain, terrainSeed, matches, includeSwapped: true);
            AddPlans(plans, DesignerComboVariantKind.Counter, "対抗:シナリオ固有", terrain, terrainSeed, matches, includeSwapped: true);
        }
        else if (scope == DesignerComboTestScope.AddedMembers)
        {
            if (scenario.ScalableRoleIndex < 0) throw new InvalidOperationException($"{scenario.DisplayName}には人数追加役が定義されていません。");
            for (int added = 0; added <= 3; added++)
            {
                AddPlans(plans, DesignerComboVariantKind.Linked, $"追加{added}人", terrain, terrainSeed, matches, includeSwapped: true, addedMembers: added);
            }
        }

        return plans;
    }

    public static DesignerComboTerrainKind GetTerrainForScope(DesignerComboTestScope scope)
    {
        return scope == DesignerComboTestScope.BehaviorCheck
            ? DesignerComboTerrainKind.Open
            : DesignerComboTerrainKind.Production;
    }

    private static List<DesignerComboMatchPlan> BuildStoneAttackDiagnosticPlans(int baseSeed)
    {
        var plans = new List<DesignerComboMatchPlan>();
        AddPlans(
            plans,
            DesignerComboVariantKind.Linked,
            "連携あり",
            DesignerComboTerrainKind.Open,
            baseSeed,
            StoneAttackDiagnosticSeedCount,
            includeSwapped: true);
        return plans;
    }

    public static int EstimateMatchCount(DesignerComboScenarioDefinition scenario, DesignerComboTestScope scope)
    {
        return scope switch
        {
            DesignerComboTestScope.BehaviorCheck => 5,
            DesignerComboTestScope.Comparison => (scenario.Roles.Length + 2) * 30 * 2,
            DesignerComboTestScope.ExtendedComparison => (scenario.Roles.Length + 2) * 100 * 2,
            DesignerComboTestScope.AddedMembers => scenario.ScalableRoleIndex < 0 ? 0 : 4 * 30 * 2,
            DesignerComboTestScope.Counter => 2 * 30 * 2,
            _ => 0,
        };
    }

    private static void AddPlans(
        List<DesignerComboMatchPlan> plans,
        DesignerComboVariantKind variant,
        string label,
        DesignerComboTerrainKind terrain,
        int baseSeed,
        int count,
        bool includeSwapped,
        int ablatedRole = -1,
        int addedMembers = 0)
    {
        for (int i = 0; i < count; i++)
        {
            plans.Add(new DesignerComboMatchPlan(variant, label, terrain, baseSeed + i, false, ablatedRole, addedMembers));
            if (includeSwapped) plans.Add(new DesignerComboMatchPlan(variant, label, terrain, baseSeed + i, true, ablatedRole, addedMembers));
        }
    }

    private static List<DesignerComboMatchPlan> BuildComparisonExtensionPlans(DesignerComboScenarioDefinition scenario, int baseSeed)
    {
        List<DesignerComboMatchPlan> all = BuildPlans(scenario, DesignerComboTestScope.ExtendedComparison, baseSeed);
        var extension = new List<DesignerComboMatchPlan>();
        for (int i = 0; i < all.Count; i++)
        {
            DesignerComboMatchPlan plan = all[i];
            int terrainBase = baseSeed + (int)plan.Terrain * 10000;
            if (plan.Seed - terrainBase >= 30) extension.Add(plan);
        }
        return extension;
    }

    private static bool ShouldExtendComparison(List<DesignerComboMatchResult> results)
    {
        var labels = new HashSet<string>();
        for (int i = 0; i < results.Count; i++)
        {
            string label = results[i].Variant;
            if (label == "通常編成" || label.StartsWith("片側解除:", StringComparison.Ordinal)) labels.Add(label);
        }

        foreach (string label in labels)
        {
            if (!TryAggregatePaired(results, label, out float linkedMetric, out float comparisonMetric, out float winDifference)) continue;
            if (comparisonMetric > 0f)
            {
                float metricRatio = linkedMetric / comparisonMetric;
                if (metricRatio >= 1.10f && metricRatio <= 1.20f) return true;
            }
            if (winDifference >= -0.15f && winDifference <= -0.05f) return true;
        }
        return false;
    }

    private static bool TryAggregatePaired(
        List<DesignerComboMatchResult> results,
        string comparisonLabel,
        out float averageLinkedMetric,
        out float averageComparisonMetric,
        out float averageWinDifference)
    {
        var linkedByMatch = new Dictionary<string, DesignerComboMatchResult>();
        for (int i = 0; i < results.Count; i++)
        {
            DesignerComboMatchResult result = results[i];
            if (result.Variant == "連携あり" && string.IsNullOrEmpty(result.Error)) linkedByMatch[MatchKey(result)] = result;
        }

        int pairs = 0;
        float linkedMetric = 0f;
        float comparisonMetric = 0f;
        float winDifference = 0f;
        for (int i = 0; i < results.Count; i++)
        {
            DesignerComboMatchResult result = results[i];
            if (result.Variant != comparisonLabel || !string.IsNullOrEmpty(result.Error) ||
                !linkedByMatch.TryGetValue(MatchKey(result), out DesignerComboMatchResult linked)) continue;
            pairs++;
            linkedMetric += linked.PrimaryMetric;
            comparisonMetric += result.PrimaryMetric;
            winDifference += WinValue(linked) - WinValue(result);
        }

        averageLinkedMetric = pairs > 0 ? linkedMetric / pairs : 0f;
        averageComparisonMetric = pairs > 0 ? comparisonMetric / pairs : 0f;
        averageWinDifference = pairs > 0 ? winDifference / pairs : 0f;
        return pairs > 0;
    }

    private static string MatchKey(DesignerComboMatchResult result) => result.Terrain + ":" + result.Seed + ":" + result.SidesSwapped;
    private static float WinValue(DesignerComboMatchResult result) => result.Outcome == "勝利" ? 1f : 0f;
}

internal readonly struct DesignerComboMatchPlan
{
    public DesignerComboVariantKind Variant { get; }
    public string Label { get; }
    public DesignerComboTerrainKind Terrain { get; }
    public int Seed { get; }
    public bool SidesSwapped { get; }
    public int AblatedRoleIndex { get; }
    public int AddedMembers { get; }

    public DesignerComboMatchPlan(DesignerComboVariantKind variant, string label, DesignerComboTerrainKind terrain, int seed, bool sidesSwapped, int ablatedRoleIndex, int addedMembers)
    {
        Variant = variant;
        Label = label;
        Terrain = terrain;
        Seed = seed;
        SidesSwapped = sidesSwapped;
        AblatedRoleIndex = ablatedRoleIndex;
        AddedMembers = addedMembers;
    }
}

[Serializable]
public sealed class DesignerComboRunSettings
{
    public const float DefaultBattleTimeoutSeconds = 180f;
    public const float DefaultTimeScale = 16f;

    public DesignerComboKind Combo;
    public bool RunAllCombos;
    public bool DisableRendering;
    public bool UseStoneAttackDiagnosticMap;
    public int DiagnosticAttackRoleCount = 2;
    public DesignerComboTestScope Scope;
    public int BaseSeed = 12000;
    public float BattleTimeoutSeconds = DefaultBattleTimeoutSeconds;
    public float TimeScale = DefaultTimeScale;
    public string OutputDirectory;
    public bool QuitWhenFinished;
}

public static class DesignerComboDiagnosticMapFactory
{
    public const string FlatStoneAttackMapName = "DiagnosticFlatStoneAttack";
    public const string AsymmetricStoneDefenseMapName = "DiagnosticAsymmetricStoneDefense";

    public static MapData CreateFlatStoneAttackMap(MapGenerationConfig config, int seed)
    {
        if (config == null) throw new ArgumentNullException(nameof(config));

        int resolution = config.HeightMapResolution;
        float cellSize = config.HeightMapCellSize;
        var height = new HeightMap(resolution, resolution, cellSize);
        var ground = new GroundStateGrid(resolution, resolution, cellSize);
        for (int z = 0; z < resolution; z++)
        {
            for (int x = 0; x < resolution; x++)
            {
                height.SetHeight(x, z, config.BaseHeight);
            }
        }

        float worldSize = resolution * cellSize;
        float centerX = worldSize * 0.5f;
        var map = new MapData(height, ground, seed);
        map.AddFeature(new PlacedFeature(
            FeatureType.OwnMainStone,
            new Vector3(centerX, config.BaseHeight, worldSize * 0.25f)));
        map.AddFeature(new PlacedFeature(
            FeatureType.EnemyMainStone,
            new Vector3(centerX, config.BaseHeight, worldSize * 0.75f)));
        return map;
    }

    public static MapData CreateAsymmetricStoneDefenseMap(MapGenerationConfig config, int seed)
    {
        MapData map = CreateFlatStoneAttackMap(config, seed);
        float worldSize = config.HeightMapResolution * config.HeightMapCellSize;
        float centerX = worldSize * 0.5f;
        float barrierZ = worldSize * 0.38f;
        const int rockCount = 9;
        const float rockSpacing = 2.7f;
        float firstX = centerX - (rockCount - 1) * rockSpacing * 0.5f;
        for (int i = 0; i < rockCount; i++)
        {
            map.AddFeature(new PlacedFeature(
                FeatureType.Rock,
                new Vector3(firstX + i * rockSpacing, config.BaseHeight, barrierZ)));
        }
        return map;
    }

    public static List<DesignerComboRoleDefinition> CreateFiveCharacterBaselineRoles(int attackRoleCount)
    {
        if (attackRoleCount == 2)
        {
            return new List<DesignerComboRoleDefinition>
            {
                new("近接攻撃役", WeaponKind.Sword, CombatAiPersonalityKind.Neutral),
                new("遠隔攻撃役", WeaponKind.Wand, CombatAiPersonalityKind.Neutral),
                new("防御役", WeaponKind.Shield, CombatAiPersonalityKind.Neutral),
                new("妨害役", WeaponKind.Grimoire, CombatAiPersonalityKind.Neutral),
                new("回復役", WeaponKind.Rosary, CombatAiPersonalityKind.Neutral),
            };
        }

        if (attackRoleCount == 3)
        {
            return new List<DesignerComboRoleDefinition>
            {
                new("近接攻撃役1", WeaponKind.Sword, CombatAiPersonalityKind.Neutral),
                new("遠隔攻撃役", WeaponKind.Wand, CombatAiPersonalityKind.Neutral),
                new("近接攻撃役2", WeaponKind.Sword, CombatAiPersonalityKind.Neutral),
                new("防御役", WeaponKind.Shield, CombatAiPersonalityKind.Neutral),
                new("回復役", WeaponKind.Rosary, CombatAiPersonalityKind.Neutral),
            };
        }

        throw new ArgumentOutOfRangeException(nameof(attackRoleCount));
    }
}

public static class DesignerComboRunRequest
{
    public const string CommandLineArgument = "-designerComboSettings";
#if UNITY_EDITOR
    private const string Key = "WarSimulation.DesignerComboRunRequest";
#endif

    public static void Store(DesignerComboRunSettings settings)
    {
#if UNITY_EDITOR
        SessionState.SetString(Key, JsonUtility.ToJson(settings));
#else
        throw new InvalidOperationException("Playerではコマンドライン引数を使用してください。");
#endif
    }

    public static bool TryConsume(out DesignerComboRunSettings settings)
    {
#if UNITY_EDITOR
        string json = SessionState.GetString(Key, string.Empty);
        SessionState.EraseString(Key);
        settings = string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<DesignerComboRunSettings>(json);
        return settings != null;
#else
        return TryReadCommandLine(Environment.GetCommandLineArgs(), out settings);
#endif
    }

    public static string Encode(DesignerComboRunSettings settings)
    {
        if (settings == null) throw new ArgumentNullException(nameof(settings));
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonUtility.ToJson(settings)));
    }

    public static bool TryDecode(string encoded, out DesignerComboRunSettings settings)
    {
        settings = null;
        if (string.IsNullOrWhiteSpace(encoded)) return false;

        try
        {
            string json = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
            settings = JsonUtility.FromJson<DesignerComboRunSettings>(json);
            return settings != null;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    public static bool TryReadCommandLine(IReadOnlyList<string> arguments, out DesignerComboRunSettings settings)
    {
        settings = null;
        if (arguments == null) return false;

        for (int i = 0; i + 1 < arguments.Count; i++)
        {
            if (arguments[i] == CommandLineArgument)
            {
                return TryDecode(arguments[i + 1], out settings);
            }
        }

        return false;
    }
}

public static class DesignerComboReportWriter
{
    [Serializable]
    private sealed class Report
    {
        public string Combo;
        public string Scope;
        public float TimeScale;
        public string CreatedAt;
        public List<DesignerComboMatchResult> Matches;
        public List<Summary> Summaries;
        public List<PairedComparison> PairedComparisons;
        public List<string> Evaluations;
    }

    [Serializable]
    public sealed class Summary
    {
        public string Variant;
        public int Matches;
        public int Wins;
        public int Failures;
        public float WinRate;
        public float AveragePrimaryMetric;
        public float AveragePrimaryMetricPerSecond;
        public float ComboOccurrenceRate;
    }

    [Serializable]
    public sealed class PairedComparison
    {
        public string BaselineVariant;
        public string ComparisonVariant;
        public int Pairs;
        public float AverageBaselineMetric;
        public float AverageComparisonMetric;
        public float AverageDifference;
        public float MedianDifference;
        public float StandardDeviation;
        public float ConfidenceIntervalLow;
        public float ConfidenceIntervalHigh;
        public float AverageDifferencePerSecond;
        public float AverageWinDifference;
    }

    public static string Write(
        DesignerComboScenarioDefinition scenario,
        DesignerComboTestScope scope,
        float timeScale,
        List<DesignerComboMatchResult> results,
        string outputDirectory = null)
    {
        string directory = ResolveOutputDirectory(outputDirectory);
        Directory.CreateDirectory(directory);
        string scaleLabel = timeScale.ToString("0.##", CultureInfo.InvariantCulture);
        string stem = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture) + "_" + scenario.Kind + "_" + scaleLabel + "x";
        string jsonPath = Path.Combine(directory, stem + ".json");
        string csvPath = Path.Combine(directory, stem + ".csv");
        List<Summary> summaries = BuildSummaries(results);
        List<PairedComparison> pairedComparisons = BuildPairedComparisons(scope, results);
        var report = new Report
        {
            Combo = scenario.DisplayName,
            Scope = scope.ToString(),
            TimeScale = timeScale,
            CreatedAt = DateTime.Now.ToString("O", CultureInfo.InvariantCulture),
            Matches = results,
            Summaries = summaries,
            PairedComparisons = pairedComparisons,
            Evaluations = BuildEvaluations(scope, summaries, pairedComparisons, results),
        };
        File.WriteAllText(jsonPath, JsonUtility.ToJson(report, prettyPrint: true), Encoding.UTF8);
        File.WriteAllText(csvPath, BuildCsv(results), Encoding.UTF8);
        return jsonPath;
    }

    private static string ResolveOutputDirectory(string outputDirectory)
    {
        if (!string.IsNullOrWhiteSpace(outputDirectory)) return Path.GetFullPath(outputDirectory);
        if (!Application.isEditor) return Path.Combine(Application.persistentDataPath, "DesignerComboTests");

        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
        return Path.Combine(projectRoot, "Logs", "DesignerComboTests");
    }

    public static List<Summary> BuildSummaries(List<DesignerComboMatchResult> results)
    {
        var grouped = new Dictionary<string, List<DesignerComboMatchResult>>();
        for (int i = 0; i < results.Count; i++)
        {
            string key = results[i].Variant;
            if (!grouped.TryGetValue(key, out List<DesignerComboMatchResult> group))
            {
                group = new List<DesignerComboMatchResult>();
                grouped[key] = group;
            }
            group.Add(results[i]);
        }

        var summaries = new List<Summary>();
        foreach (KeyValuePair<string, List<DesignerComboMatchResult>> pair in grouped)
        {
            int wins = 0;
            int failures = 0;
            int occurrences = 0;
            float metric = 0f;
            float metricPerSecond = 0f;
            for (int i = 0; i < pair.Value.Count; i++)
            {
                DesignerComboMatchResult result = pair.Value[i];
                if (!string.IsNullOrEmpty(result.Error))
                {
                    failures++;
                    continue;
                }
                if (result.Outcome == "勝利") wins++;
                if (result.ComboOccurred) occurrences++;
                metric += result.PrimaryMetric;
                metricPerSecond += result.PrimaryMetricPerSecond;
            }

            int valid = Mathf.Max(1, pair.Value.Count - failures);
            summaries.Add(new Summary
            {
                Variant = pair.Key,
                Matches = pair.Value.Count,
                Wins = wins,
                Failures = failures,
                WinRate = wins / (float)valid,
                AveragePrimaryMetric = metric / valid,
                AveragePrimaryMetricPerSecond = metricPerSecond / valid,
                ComboOccurrenceRate = occurrences / (float)valid,
            });
        }

        summaries.Sort((a, b) => string.CompareOrdinal(a.Variant, b.Variant));
        return summaries;
    }

    public static List<PairedComparison> BuildPairedComparisons(
        DesignerComboTestScope scope,
        List<DesignerComboMatchResult> results)
    {
        string baselineLabel = scope switch
        {
            DesignerComboTestScope.Comparison => "連携あり",
            DesignerComboTestScope.ExtendedComparison => "連携あり",
            DesignerComboTestScope.Counter => "対抗前",
            DesignerComboTestScope.AddedMembers => "追加0人",
            _ => null,
        };
        var comparisons = new List<PairedComparison>();
        if (baselineLabel == null) return comparisons;

        var baselines = new Dictionary<string, DesignerComboMatchResult>();
        var comparisonLabels = new HashSet<string>();
        for (int i = 0; i < results.Count; i++)
        {
            DesignerComboMatchResult result = results[i];
            if (!string.IsNullOrEmpty(result.Error)) continue;
            if (result.Variant == baselineLabel) baselines[PairKey(result)] = result;
            else comparisonLabels.Add(result.Variant);
        }

        foreach (string comparisonLabel in comparisonLabels)
        {
            var baselineMetrics = new List<float>();
            var comparisonMetrics = new List<float>();
            var differences = new List<float>();
            var differencesPerSecond = new List<float>();
            var winDifferences = new List<float>();
            for (int i = 0; i < results.Count; i++)
            {
                DesignerComboMatchResult result = results[i];
                if (result.Variant != comparisonLabel || !string.IsNullOrEmpty(result.Error) ||
                    !baselines.TryGetValue(PairKey(result), out DesignerComboMatchResult baseline)) continue;
                baselineMetrics.Add(baseline.PrimaryMetric);
                comparisonMetrics.Add(result.PrimaryMetric);
                differences.Add(baseline.PrimaryMetric - result.PrimaryMetric);
                differencesPerSecond.Add(baseline.PrimaryMetricPerSecond - result.PrimaryMetricPerSecond);
                winDifferences.Add(WinValue(baseline) - WinValue(result));
            }

            if (differences.Count == 0) continue;
            float averageDifference = Average(differences);
            float standardDeviation = StandardDeviation(differences, averageDifference);
            float confidenceRadius = 1.96f * standardDeviation / Mathf.Sqrt(differences.Count);
            comparisons.Add(new PairedComparison
            {
                BaselineVariant = baselineLabel,
                ComparisonVariant = comparisonLabel,
                Pairs = differences.Count,
                AverageBaselineMetric = Average(baselineMetrics),
                AverageComparisonMetric = Average(comparisonMetrics),
                AverageDifference = averageDifference,
                MedianDifference = Median(differences),
                StandardDeviation = standardDeviation,
                ConfidenceIntervalLow = averageDifference - confidenceRadius,
                ConfidenceIntervalHigh = averageDifference + confidenceRadius,
                AverageDifferencePerSecond = Average(differencesPerSecond),
                AverageWinDifference = Average(winDifferences),
            });
        }

        comparisons.Sort((a, b) => string.CompareOrdinal(a.ComparisonVariant, b.ComparisonVariant));
        return comparisons;
    }

    private static string BuildCsv(List<DesignerComboMatchResult> results)
    {
        var builder = new StringBuilder();
        builder.AppendLine("コンボ,編成,地形,シード,陣営入替,結果,戦闘秒数,実時間秒数,処理フレーム数,平均FPS,主指標,主指標毎秒,連携成立,主指標名,魔石ダメージ,与ダメージ,有効回復量,有効防御量,対象変更,連携秒数,拘束秒数,毒秒数,強化秒数,連携崩壊時刻,連携崩壊理由,必要役離脱時刻,崩壊前主指標率,崩壊後主指標率,生存時間,エラー");
        for (int i = 0; i < results.Count; i++)
        {
            DesignerComboMatchResult r = results[i];
            builder.Append(Csv(r.Combo)).Append(',').Append(Csv(r.Variant)).Append(',').Append(Csv(r.Terrain)).Append(',')
                .Append(r.Seed).Append(',').Append(r.SidesSwapped).Append(',').Append(Csv(r.Outcome)).Append(',')
                .Append(Number(r.BattleSeconds)).Append(',').Append(Number(r.RealSeconds)).Append(',')
                .Append(r.FrameCount).Append(',').Append(Number(r.AverageFramesPerSecond)).Append(',')
                .Append(Number(r.PrimaryMetric)).Append(',').Append(Number(r.PrimaryMetricPerSecond)).Append(',')
                .Append(r.ComboOccurred).Append(',').Append(Csv(r.PrimaryMetricName)).Append(',')
                .Append(r.MagicStoneDamage).Append(',').Append(r.DamageDealt).Append(',').Append(r.EffectiveHealing).Append(',').Append(r.EffectiveDefense).Append(',')
                .Append(r.TargetChanges).Append(',').Append(Number(r.LinkedSeconds)).Append(',').Append(Number(r.BindSeconds)).Append(',')
                .Append(Number(r.PoisonSeconds)).Append(',').Append(Number(r.BuffSeconds)).Append(',').Append(Number(r.ComboBrokenAt)).Append(',')
                .Append(Csv(r.ComboBrokenReason)).Append(',').Append(Number(r.RequiredRoleLostAt)).Append(',').Append(Number(r.MetricRateBeforeBreak)).Append(',').Append(Number(r.MetricRateAfterBreak)).Append(',')
                .Append(Csv(r.SurvivalTimes)).Append(',').Append(Csv(r.Error)).AppendLine();
        }
        return builder.ToString();
    }

    public static List<string> BuildEvaluations(
        DesignerComboTestScope scope,
        List<Summary> summaries,
        List<PairedComparison> pairedComparisons,
        List<DesignerComboMatchResult> results)
    {
        var evaluations = new List<string>();
        Summary linked = FindSummary(summaries, scope == DesignerComboTestScope.Counter ? "対抗前" : "連携あり");
        if (scope == DesignerComboTestScope.BehaviorCheck && linked != null)
        {
            evaluations.Add((linked.ComboOccurrenceRate >= 0.8f ? "合格" : "不合格") + ": 5試合中4試合以上で連携が発生する");
        }

        if ((scope == DesignerComboTestScope.Comparison || scope == DesignerComboTestScope.ExtendedComparison) && linked != null)
        {
            Summary normal = FindSummary(summaries, "通常編成");
            for (int i = 0; i < summaries.Count; i++)
            {
                Summary ablated = summaries[i];
                if (!ablated.Variant.StartsWith("片側解除:", StringComparison.Ordinal)) continue;
                PairedComparison paired = FindPairedComparison(pairedComparisons, ablated.Variant);
                bool metricPassed = paired != null && paired.AverageBaselineMetric > 0f &&
                    paired.AverageBaselineMetric >= paired.AverageComparisonMetric * 1.15f;
                bool winPassed = paired != null && paired.AverageWinDifference >= -0.1f;
                evaluations.Add((metricPassed ? "合格" : "不合格") + $": 主指標が{ablated.Variant}より15%以上高い");
                evaluations.Add((winPassed ? "合格" : "不合格") + $": 勝率が{ablated.Variant}より10ポイント以上低くない");
            }
            if (normal != null)
            {
                PairedComparison paired = FindPairedComparison(pairedComparisons, normal.Variant);
                evaluations.Add((paired != null && paired.AverageWinDifference >= -0.1f ? "合格" : "不合格") + ": 勝率が通常編成より10ポイント以上低くない");
            }
        }

        if (scope == DesignerComboTestScope.Counter)
        {
            bool anyPassed = false;
            for (int i = 0; i < summaries.Count; i++)
            {
                Summary counter = summaries[i];
                if (!counter.Variant.StartsWith("対抗:", StringComparison.Ordinal) || linked == null) continue;
                PairedComparison paired = FindPairedComparison(pairedComparisons, counter.Variant);
                bool passed = paired != null && paired.AverageComparisonMetric <= paired.AverageBaselineMetric * 0.8f;
                anyPassed |= passed;
                evaluations.Add((passed ? "合格" : "不合格") + $": {counter.Variant}が主指標を20%以上下げる");
            }
            evaluations.Add((anyPassed ? "合格" : "不合格") + ": コンボ固有の対抗編成が有効である");
        }

        float before = 0f;
        float after = 0f;
        int broken = 0;
        for (int i = 0; i < results.Count; i++)
        {
            DesignerComboMatchResult result = results[i];
            if (!result.IsLinkedVariant || !result.RequiredRoleLost || result.RequiredRoleLostAt < 0f || result.MetricRateBeforeBreak <= 0f) continue;
            before += result.MetricRateBeforeBreak;
            after += result.MetricRateAfterBreak;
            broken++;
        }
        if (broken > 0)
        {
            bool passed = DesignerComboMetricRules.IsClearMetricRateDrop(before / broken, after / broken);
            evaluations.Add((passed ? "合格" : "不合格") + ": 必要役の離脱後に主指標の時間当たり発生量が15%以上下がる");
        }
        else
        {
            evaluations.Add("未判定: 中核役または補助役の離脱を観測できなかった");
        }

        return evaluations;
    }

    private static PairedComparison FindPairedComparison(List<PairedComparison> comparisons, string variant)
    {
        for (int i = 0; i < comparisons.Count; i++)
        {
            if (comparisons[i].ComparisonVariant == variant) return comparisons[i];
        }
        return null;
    }

    private static string PairKey(DesignerComboMatchResult result)
    {
        return result.Terrain + ":" + result.Seed + ":" + result.SidesSwapped;
    }

    private static float WinValue(DesignerComboMatchResult result) => result.Outcome == "勝利" ? 1f : 0f;

    private static float Average(List<float> values)
    {
        float total = 0f;
        for (int i = 0; i < values.Count; i++) total += values[i];
        return values.Count > 0 ? total / values.Count : 0f;
    }

    private static float Median(List<float> values)
    {
        var sorted = new List<float>(values);
        sorted.Sort();
        int middle = sorted.Count / 2;
        return sorted.Count % 2 == 0 ? (sorted[middle - 1] + sorted[middle]) * 0.5f : sorted[middle];
    }

    private static float StandardDeviation(List<float> values, float average)
    {
        if (values.Count < 2) return 0f;
        float squaredDifferences = 0f;
        for (int i = 0; i < values.Count; i++)
        {
            float difference = values[i] - average;
            squaredDifferences += difference * difference;
        }
        return Mathf.Sqrt(squaredDifferences / (values.Count - 1));
    }

    private static Summary FindSummary(List<Summary> summaries, string variant)
    {
        for (int i = 0; i < summaries.Count; i++)
        {
            if (summaries[i].Variant == variant) return summaries[i];
        }
        return null;
    }

    private static string Number(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);
    private static string Csv(string value) => "\"" + (value ?? string.Empty).Replace("\"", "\"\"") + "\"";
}
