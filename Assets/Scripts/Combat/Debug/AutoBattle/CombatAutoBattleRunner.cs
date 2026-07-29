using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using WarSimulation.Combat.Map;

[DefaultExecutionOrder(-50)]
public sealed class CombatAutoBattleRunner : MonoBehaviour
{
    [SerializeField] private AuthoredMapDefinition[] _mapCandidates = Array.Empty<AuthoredMapDefinition>();
    [SerializeField] private CombatAutoBattleRole[] _allies = CreateDefaultParty();
    [SerializeField] private CombatAutoBattleRole[] _enemies = CreateDefaultParty();
    [SerializeField, Min(1)] private int _matchCount = 10;
    [SerializeField] private int _baseSeed = 1;
    [SerializeField, Min(1f)] private float _timeoutSeconds = 180f;
    [SerializeField, Min(0.1f)] private float _timeScale = 16f;

    private static CombatAutoBattleRole[] CreateDefaultParty()
    {
        return new[]
        {
            new CombatAutoBattleRole { Weapon = WeaponKind.Sword, Personality = CombatAiPersonalityKind.Neutral },
            new CombatAutoBattleRole { Weapon = WeaponKind.Sword, Personality = CombatAiPersonalityKind.Neutral },
            new CombatAutoBattleRole { Weapon = WeaponKind.Shield, Personality = CombatAiPersonalityKind.Neutral },
            new CombatAutoBattleRole { Weapon = WeaponKind.Wand, Personality = CombatAiPersonalityKind.Neutral },
            new CombatAutoBattleRole { Weapon = WeaponKind.Rosary, Personality = CombatAiPersonalityKind.Neutral },
        };
    }

    private readonly List<CombatAutoBattleMatchResult> _results = new();
    private readonly Dictionary<WeaponKind, WeaponConfig> _weapons = new();
    private readonly List<Character> _allyPool = new();
    private readonly List<Character> _enemyPool = new();
    private readonly List<UnityEngine.Object> _temporaryObjects = new();

    private CombatCharacterSystem _characterSystem;
    private CombatBattleFlow _battleFlow;
    private CombatMapSystem _mapSystem;
    private bool _running;

    private void Awake()
    {
        HideCombatUi();
    }

    private void Start()
    {
        HideCombatUi();
        if (!CombatAutoBattleConfigLoader.TryLoadFromCommandLine(out CombatAutoBattleConfig config, out string path))
            return;

        try
        {
            ApplyConfig(config);
            Debug.Log($"[自動戦闘] 設定を読み込みました: {path}", this);
            StartCoroutine(Run());
        }
        catch (Exception ex)
        {
            Debug.LogException(ex, this);
            QuitIfStandalone(1);
        }
    }

    [ContextMenu("自動戦闘を開始")]
    public void StartAutoBattle()
    {
        if (!Application.isPlaying || _running) return;
        StartCoroutine(Run());
    }

    private IEnumerator Run()
    {
        _running = true;
        float previousTimeScale = Time.timeScale;
        int previousVSync = QualitySettings.vSyncCount;
        int previousTargetFrameRate = Application.targetFrameRate;
        bool previousRunInBackground = Application.runInBackground;

        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = -1;
        Application.runInBackground = true;

        if (!TryResolveDependencies(out string error))
        {
            Debug.LogError("[自動戦闘] " + error, this);
            RestoreRuntimeSettings(previousTimeScale, previousVSync, previousTargetFrameRate, previousRunInBackground);
            _running = false;
            QuitIfStandalone(1);
            yield break;
        }

        if (!TryValidateSettings(out error))
        {
            Debug.LogError("[自動戦闘] " + error, this);
            RestoreRuntimeSettings(previousTimeScale, previousVSync, previousTargetFrameRate, previousRunInBackground);
            _running = false;
            QuitIfStandalone(1);
            yield break;
        }

        try
        {
            CaptureCharacterPools();
            LoadWeapons();
        }
        catch (Exception ex)
        {
            Debug.LogException(ex, this);
            RestoreRuntimeSettings(previousTimeScale, previousVSync, previousTargetFrameRate, previousRunInBackground);
            _running = false;
            QuitIfStandalone(1);
            yield break;
        }

        Time.timeScale = _timeScale;
        _results.Clear();

        int exitCode = 0;
        Debug.Log($"[自動戦闘] {_matchCount}試合を開始します。", this);
        for (int i = 0; i < _matchCount; i++)
        {
            Exception matchError = null;
            IEnumerator match = RunMatch(i);
            while (true)
            {
                object current = null;
                bool moved;
                try
                {
                    moved = match.MoveNext();
                    if (moved) current = match.Current;
                }
                catch (Exception ex)
                {
                    matchError = ex;
                    break;
                }

                if (!moved) break;
                yield return current;
            }

            if (matchError != null)
            {
                exitCode = 1;
                Debug.LogException(matchError, this);
                break;
            }

            Debug.Log($"[自動戦闘] {i + 1}/{_matchCount}試合完了", this);
        }

        if (exitCode == 0)
        {
            try
            {
                string reportPath = CombatAutoBattleReportWriter.Write(_results);
                Debug.Log($"[自動戦闘] 完了: {reportPath}", this);
            }
            catch (Exception ex)
            {
                exitCode = 1;
                Debug.LogException(ex, this);
            }
        }

        RestoreRuntimeSettings(previousTimeScale, previousVSync, previousTargetFrameRate, previousRunInBackground);
        CleanupTemporaryObjects();
        _running = false;
        QuitIfStandalone(exitCode);
    }

    private IEnumerator RunMatch(int index)
    {
        int seed = _baseSeed + index;
        UnityEngine.Random.InitState(seed);

        AuthoredMapDefinition mapDefinition = _mapCandidates[UnityEngine.Random.Range(0, _mapCandidates.Length)];
        MapData map = _mapSystem.ApplyAuthoredMap(mapDefinition, render3D: true);
        if (map == null)
            throw new InvalidOperationException($"マップ '{mapDefinition.name}' の適用に失敗しました。");

        yield return null;

        List<CombatParticipantSetup> allies = BuildSetups(_allies, _allyPool);
        List<CombatParticipantSetup> enemies = BuildSetups(_enemies, _enemyPool);
        _characterSystem.SetParticipants(allies, enemies);
        _characterSystem.TryRelocateCharactersNearMainStones();

        bool ended = false;
        CombatBattleState endState = CombatBattleState.WaitingToStart;
        void OnBattleEnded(CombatBattleState state)
        {
            ended = true;
            endState = state;
        }

        _battleFlow.BattleEnded += OnBattleEnded;
        _battleFlow.StartBattleOnCurrentMap();
        CombatBattleRandom.Initialize(seed);
        if (_battleFlow.State != CombatBattleState.Running)
        {
            _battleFlow.BattleEnded -= OnBattleEnded;
            throw new InvalidOperationException("戦闘を開始できませんでした。");
        }

        float startedAt = Time.time;
        float startedAtRealtime = Time.realtimeSinceStartup;
        while (!ended &&
               _battleFlow.State == CombatBattleState.Running &&
               Time.time - startedAt < _timeoutSeconds)
        {
            yield return null;
        }

        if (!ended &&
            (_battleFlow.State == CombatBattleState.Victory || _battleFlow.State == CombatBattleState.Defeat))
        {
            ended = true;
            endState = _battleFlow.State;
        }

        bool timedOut = !ended;
        _battleFlow.BattleEnded -= OnBattleEnded;
        if (timedOut)
        {
            _battleFlow.ResetBattle();
            StopCharacters(allies);
            StopCharacters(enemies);
        }

        _results.Add(new CombatAutoBattleMatchResult
        {
            Index = index,
            Seed = seed,
            MapName = mapDefinition.name,
            Outcome = timedOut
                ? "時間切れ"
                : endState == CombatBattleState.Victory
                    ? "勝利"
                    : "敗北",
            GameSeconds = Mathf.Max(0f, Time.time - startedAt),
            RealSeconds = Mathf.Max(0f, Time.realtimeSinceStartup - startedAtRealtime),
        });
    }

    private void ApplyConfig(CombatAutoBattleConfig config)
    {
        if (config.MapNames != null && config.MapNames.Length > 0)
            _mapCandidates = FilterMapsByName(config.MapNames);
        if (config.Allies != null && config.Allies.Length > 0)
            _allies = config.Allies;
        if (config.Enemies != null && config.Enemies.Length > 0)
            _enemies = config.Enemies;
        if (config.MatchCount > 0)
            _matchCount = config.MatchCount;
        _baseSeed = config.BaseSeed;
        if (config.TimeoutSeconds > 0f)
            _timeoutSeconds = config.TimeoutSeconds;
        if (config.TimeScale > 0f)
            _timeScale = config.TimeScale;
    }

    private AuthoredMapDefinition[] FilterMapsByName(string[] names)
    {
        var filtered = new List<AuthoredMapDefinition>();
        for (int i = 0; i < names.Length; i++)
        {
            string name = names[i];
            AuthoredMapDefinition found = null;
            for (int j = 0; j < _mapCandidates.Length; j++)
            {
                AuthoredMapDefinition candidate = _mapCandidates[j];
                if (candidate != null && candidate.name == name)
                {
                    found = candidate;
                    break;
                }
            }

            if (found == null)
                throw new InvalidOperationException($"マップ候補に '{name}' がありません。シーンの Runner に割り当ててビルドしてください。");
            if (!filtered.Contains(found))
                filtered.Add(found);
        }

        return filtered.ToArray();
    }

    private bool TryResolveDependencies(out string error)
    {
        CombatSceneContext context = CombatSceneContext.Instance;
        _characterSystem = context != null ? context.CharacterSystem : null;
        _battleFlow = context != null ? context.BattleFlow : null;
        _mapSystem = context != null ? context.MapSystem : null;
        _characterSystem ??= FindAnyObjectByType<CombatCharacterSystem>();
        _battleFlow ??= FindAnyObjectByType<CombatBattleFlow>();
        _mapSystem ??= FindAnyObjectByType<CombatMapSystem>();

        if (_characterSystem == null)
        {
            error = "CombatCharacterSystem がありません。";
            return false;
        }

        if (_battleFlow == null)
        {
            error = "CombatBattleFlow がありません。";
            return false;
        }

        if (_mapSystem == null)
        {
            error = "CombatMapSystem がありません。";
            return false;
        }

        error = null;
        return true;
    }

    private bool TryValidateSettings(out string error)
    {
        if (_mapCandidates == null || _mapCandidates.Length == 0)
        {
            error = "マップ候補が空です。";
            return false;
        }

        for (int i = 0; i < _mapCandidates.Length; i++)
        {
            if (_mapCandidates[i] == null)
            {
                error = "マップ候補に null があります。";
                return false;
            }
        }

        if (_allies == null || _allies.Length == 0)
        {
            error = "味方編成が空です。";
            return false;
        }

        if (_enemies == null || _enemies.Length == 0)
        {
            error = "敵編成が空です。";
            return false;
        }

        error = null;
        return true;
    }

    private void CaptureCharacterPools()
    {
        _allyPool.Clear();
        _enemyPool.Clear();
        AddUnique(_allyPool, _characterSystem.AllyCharacters);
        AddUnique(_enemyPool, _characterSystem.EnemyCharacters);
        if (_allyPool.Count < _allies.Length)
            throw new InvalidOperationException($"味方キャラクターが不足しています。必要{_allies.Length} / 所持{_allyPool.Count}");
        if (_enemyPool.Count < _enemies.Length)
            throw new InvalidOperationException($"敵キャラクターが不足しています。必要{_enemies.Length} / 所持{_enemyPool.Count}");
    }

    private void LoadWeapons()
    {
        _weapons.Clear();
        CombatCharacterSelection selection = FindAnyObjectByType<CombatCharacterSelection>(FindObjectsInactive.Include);
        if (selection == null)
            throw new InvalidOperationException("CombatCharacterSelection が無く、武器を解決できません。");

        IReadOnlyList<WeaponConfig> options = selection.WeaponOptions;
        for (int i = 0; i < options.Count; i++)
        {
            WeaponConfig weapon = options[i];
            if (weapon == null) continue;
            _weapons[weapon.Kind] = weapon;
        }
    }

    private List<CombatParticipantSetup> BuildSetups(CombatAutoBattleRole[] roles, List<Character> pool)
    {
        var setups = new List<CombatParticipantSetup>(roles.Length);
        for (int i = 0; i < roles.Length; i++)
        {
            CombatAutoBattleRole role = roles[i];
            if (!_weapons.TryGetValue(role.Weapon, out WeaponConfig weapon))
                throw new InvalidOperationException($"武器 {role.Weapon} の WeaponConfig がありません。");

            setups.Add(new CombatParticipantSetup(
                pool[i],
                weapon,
                CreatePersonality(role.Personality)));
        }

        return setups;
    }

    private CombatAiPersonalityProfile CreatePersonality(CombatAiPersonalityKind kind)
    {
        CombatAiPersonalityProfile profile = CombatAiPersonalityProfile.CreateBuiltInProfile(kind);
        _temporaryObjects.Add(profile);
        return profile;
    }

    private void CleanupTemporaryObjects()
    {
        for (int i = 0; i < _temporaryObjects.Count; i++)
        {
            if (_temporaryObjects[i] != null)
                Destroy(_temporaryObjects[i]);
        }

        _temporaryObjects.Clear();
    }

    private static void HideCombatUi()
    {
        CombatFlow[] flows = FindObjectsByType<CombatFlow>(FindObjectsInactive.Include);
        for (int i = 0; i < flows.Length; i++)
        {
            flows[i].enabled = false;
            Canvas canvas = flows[i].GetComponentInParent<Canvas>(includeInactive: true);
            if (canvas != null) canvas.enabled = false;
        }
    }

    private static void StopCharacters(IReadOnlyList<CombatParticipantSetup> setups)
    {
        for (int i = 0; i < setups.Count; i++)
        {
            Character character = setups[i]?.Character;
            if (character == null) continue;
            character.GetComponent<CombatCharacterBody>()?.Stop();
        }
    }

    private static void AddUnique(List<Character> destination, IReadOnlyList<Character> source)
    {
        for (int i = 0; i < source.Count; i++)
        {
            Character character = source[i];
            if (character != null && !destination.Contains(character))
                destination.Add(character);
        }
    }

    private static void RestoreRuntimeSettings(
        float timeScale,
        int vSync,
        int targetFrameRate,
        bool runInBackground)
    {
        Time.timeScale = timeScale;
        QualitySettings.vSyncCount = vSync;
        Application.targetFrameRate = targetFrameRate;
        Application.runInBackground = runInBackground;
    }

    private static void QuitIfStandalone(int exitCode)
    {
        if (!Application.isEditor)
            Application.Quit(exitCode);
    }
}
