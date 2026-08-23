using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using WarSimulation.Combat.Map;

[DefaultExecutionOrder(-50)]
public sealed class CombatAutoBattleRunner : MonoBehaviour
{
    [SerializeField] private AuthoredMapDefinition[] _mapCandidates = Array.Empty<AuthoredMapDefinition>();
    [SerializeField] private WeaponConfig[] _weaponConfigs = Array.Empty<WeaponConfig>();
    [SerializeField] private CombatAutoBattleRole[] _allies = CreateDefaultParty();
    [SerializeField] private CombatAutoBattleRole[] _enemies = CreateDefaultParty();
    [SerializeField, Min(1)] private int _matchCount = 10;
    [SerializeField] private int _baseSeed = 1;
    [SerializeField, Min(1f)] private float _timeoutSeconds = 600f;
    [SerializeField, Min(0.1f)] private float _timeScale = 6f;

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
    private readonly Dictionary<CombatAiPersonalityKind, CombatAiPersonalityProfile> _personalityCache = new();
    private readonly List<Character> _allyPool = new();
    private readonly List<Character> _enemyPool = new();
    private readonly List<UnityEngine.Object> _temporaryObjects = new();

    private CombatCharacterSystem _characterSystem;
    private CombatBattleFlow _battleFlow;
    private CombatMapSystem _mapSystem;
    private AuthoredMapDefinition _lastAppliedMap;
    private bool _running;
    private bool _audioPaused;
    private bool _diagnosticsEnabled = true;
    private bool _fixedSeed;
    private bool _preserveFixedDeltaTime;
    private float _previousFixedDeltaTime;

    private void Start()
    {
        if (CombatAutoBattleConfigLoader.TryLoadSweepFromCommandLine(
                out CombatCompositionSweepConfig sweepConfig,
                out string sweepPath))
        {
            try
            {
                if (sweepConfig.TimeoutSeconds > 0f) _timeoutSeconds = sweepConfig.TimeoutSeconds;
                if (sweepConfig.TimeScale > 0f) _timeScale = sweepConfig.TimeScale;
                _preserveFixedDeltaTime = sweepConfig.PreserveFixedDeltaTime;
                Debug.Log($"[自動戦闘] 編成探索設定を読み込みました: {sweepPath}", this);
                StartCoroutine(RunSweep(sweepConfig));
            }
            catch (Exception ex)
            {
                Debug.LogException(ex, this);
                QuitIfStandalone(1);
            }

            return;
        }

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
        if (!BeginRun(out float previousTimeScale, out int previousVSync, out int previousTargetFrameRate,
                out bool previousRunInBackground, out string error))
        {
            Debug.LogError("[自動戦闘] " + error, this);
            EndRun(previousTimeScale, previousVSync, previousTargetFrameRate, previousRunInBackground, 1);
            yield break;
        }

        if (!TryValidateSettings(out error))
        {
            Debug.LogError("[自動戦闘] " + error, this);
            EndRun(previousTimeScale, previousVSync, previousTargetFrameRate, previousRunInBackground, 1);
            yield break;
        }

        yield return null;

        try
        {
            CaptureCharacterPools(Mathf.Max(_allies.Length, _enemies.Length));
            LoadWeapons();
            ValidateRoles(_allies);
            ValidateRoles(_enemies);
            ValidateMapAvailability(evaluateBothStonePositions: false);
            if (_diagnosticsEnabled) EnsureBattleEventLogger();
            LogPreflight(Mathf.Max(_allies.Length, _enemies.Length));
        }
        catch (Exception ex)
        {
            Debug.LogException(ex, this);
            EndRun(previousTimeScale, previousVSync, previousTargetFrameRate, previousRunInBackground, 1);
            yield break;
        }

        Time.timeScale = _timeScale;
        _results.Clear();
        string reportPath = CombatAutoBattleReportWriter.CreateReportPath();
        int exitCode = 0;

        Debug.Log($"[自動戦闘] {_matchCount}試合を開始します。診断ログ: Logs/CombatBattles/", this);
        for (int i = 0; i < _matchCount; i++)
        {
            Exception matchError = null;
            int seed = ResolveSingleSeed(_baseSeed, i, _fixedSeed);
            yield return Drive(RunMatch(i, _allies, _enemies, seed), error => matchError = error);
            if (matchError != null)
            {
                exitCode = 1;
                Debug.LogException(matchError, this);
                break;
            }

            try
            {
                CombatAutoBattleReportWriter.Write(_results, reportPath);
            }
            catch (Exception ex)
            {
                exitCode = 1;
                Debug.LogException(ex, this);
                break;
            }

            Debug.Log($"[自動戦闘] {i + 1}/{_matchCount}試合完了", this);
        }

        if (exitCode == 0)
            Debug.Log($"[自動戦闘] 完了: {reportPath}", this);

        EndRun(previousTimeScale, previousVSync, previousTargetFrameRate, previousRunInBackground, exitCode);
    }

    private IEnumerator RunSweep(CombatCompositionSweepConfig config)
    {
        AuthoredMapDefinition[] filteredMaps = null;
        try
        {
            if (config.MapNames != null && config.MapNames.Length > 0)
                filteredMaps = FilterMapsByName(config.MapNames);
        }
        catch (Exception ex)
        {
            Debug.LogException(ex, this);
            QuitIfStandalone(1);
            yield break;
        }

        _diagnosticsEnabled = !config.DisableDiagnostics;
        if (!BeginRun(out float previousTimeScale, out int previousVSync, out int previousTargetFrameRate,
                out bool previousRunInBackground, out string error))
        {
            Debug.LogError("[自動戦闘] " + error, this);
            EndRun(previousTimeScale, previousVSync, previousTargetFrameRate, previousRunInBackground, 1);
            yield break;
        }

        if (filteredMaps != null)
            _mapCandidates = filteredMaps;

        if (!TryValidateMapCandidates(out error))
        {
            Debug.LogError("[自動戦闘] " + error, this);
            EndRun(previousTimeScale, previousVSync, previousTargetFrameRate, previousRunInBackground, 1);
            yield break;
        }

        CombatAutoBattleRole[] enemies = config.Enemy != null && config.Enemy.Length > 0
            ? config.Enemy
            : CreateDefaultParty();
        List<CombatCompositionCandidate> candidates = CombatCompositionSweepGenerator.Generate(config);
        if (candidates.Count == 0)
        {
            Debug.LogError("[自動戦闘] 編成候補が空です。", this);
            EndRun(previousTimeScale, previousVSync, previousTargetFrameRate, previousRunInBackground, 1);
            yield break;
        }

        int maxParty = enemies.Length;
        for (int i = 0; i < candidates.Count; i++)
            maxParty = Mathf.Max(maxParty, candidates[i].Roles.Length);

        yield return null;

        try
        {
            CaptureCharacterPools(maxParty);
            LoadWeapons();
            ValidateRoles(enemies);
            for (int i = 0; i < candidates.Count; i++)
                ValidateRoles(candidates[i].Roles);
            ValidateMapAvailability(
                config.EvaluateBothStonePositions,
                config.UseFixedStonePosition,
                config.StonePositionsReversed);
            if (_diagnosticsEnabled) EnsureBattleEventLogger();
            LogPreflight(maxParty);
        }
        catch (Exception ex)
        {
            Debug.LogException(ex, this);
            EndRun(previousTimeScale, previousVSync, previousTargetFrameRate, previousRunInBackground, 1);
            yield break;
        }

        Time.timeScale = _timeScale;
        int matchesPerCandidate = Mathf.Max(1, config.MatchesPerCandidate);
        string reportPath = CombatAutoBattleReportWriter.CreateReportPath("sweep");
        var report = new CombatCompositionSweepReport
        {
            CandidateCount = candidates.Count,
            MatchesPerCandidate = matchesPerCandidate,
            CompletedCandidates = 0,
            TimeScale = _timeScale,
            FixedDeltaTime = Time.fixedDeltaTime,
            PreserveFixedDeltaTime = _preserveFixedDeltaTime,
            PlayerBuildGuid = Application.buildGUID,
            UnityVersion = Application.unityVersion,
        };

        int exitCode = 0;
        Debug.Log($"[自動戦闘] 編成探索 {candidates.Count}候補 × {matchesPerCandidate}試合を開始します。", this);

        var candidateResults = new List<CombatCompositionCandidateResult>(candidates.Count);
        for (int c = 0; c < candidates.Count; c++)
        {
            CombatCompositionCandidate candidate = candidates[c];
            candidateResults.Add(new CombatCompositionCandidateResult
            {
                Index = c,
                CandidateKey = CombatCompositionSweepGenerator.BuildKey(candidate.Roles),
                Roles = candidate.Roles,
            });
        }

        int positionCount = config.UseFixedStonePosition || !config.EvaluateBothStonePositions ? 1 : 2;
        int totalMatchesPerCandidate = config.TotalMatchesPerCandidate > 0
            ? config.TotalMatchesPerCandidate
            : matchesPerCandidate;
        for (int c = 0; c < candidates.Count; c++)
        {
            CombatCompositionCandidate candidate = candidates[c];
            CombatCompositionCandidateResult candidateResult = candidateResults[c];
            for (int p = 0; p < positionCount; p++)
            {
                bool stonePositionsReversed = config.UseFixedStonePosition
                    ? config.StonePositionsReversed
                    : p == 1;
                for (int m = 0; m < matchesPerCandidate; m++)
                {
                    int seed = ResolveSweepSeed(config, c, m, totalMatchesPerCandidate);
                    _results.Clear();
                    Exception matchError = null;
                    yield return Drive(
                        RunMatch(m, candidate.Roles, enemies, seed, null, stonePositionsReversed),
                        error => matchError = error);
                    if (matchError != null)
                    {
                        exitCode = 1;
                        Debug.LogException(matchError, this);
                        break;
                    }

                    CombatAutoBattleMatchResult matchResult = _results[_results.Count - 1];
                    RecordResult(candidateResult, matchResult);
                }

                if (exitCode != 0) break;
            }

            if (exitCode != 0) break;

            CompleteResult(candidateResult);
            report.Ranking.Add(candidateResult);
            report.CompletedCandidates = report.Ranking.Count;

            try
            {
                CombatCompositionSweepReportWriter.Write(report, reportPath);
            }
            catch (Exception ex)
            {
                exitCode = 1;
                Debug.LogException(ex, this);
                break;
            }

            Debug.Log(
                $"[自動戦闘] 候補 {c + 1}/{candidates.Count} 完了 WinRate={candidateResult.WinRate:P0} ({candidateResult.Wins}/{candidateResult.MatchCount})",
                this);
        }

        if (exitCode == 0)
            Debug.Log($"[自動戦闘] 編成探索完了: {reportPath}", this);

        EndRun(previousTimeScale, previousVSync, previousTargetFrameRate, previousRunInBackground, exitCode);
    }

    private bool BeginRun(
        out float previousTimeScale,
        out int previousVSync,
        out int previousTargetFrameRate,
        out bool previousRunInBackground,
        out string error)
    {
        HideCombatUi();
        _running = true;
        previousTimeScale = Time.timeScale;
        previousVSync = QualitySettings.vSyncCount;
        previousTargetFrameRate = Application.targetFrameRate;
        previousRunInBackground = Application.runInBackground;
        error = null;

        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = -1;
        Application.runInBackground = true;
        _previousFixedDeltaTime = Time.fixedDeltaTime;
        Time.fixedDeltaTime = _preserveFixedDeltaTime
            ? _previousFixedDeltaTime
            : _previousFixedDeltaTime * _timeScale;
        if (AudioListener.pause == false)
        {
            AudioListener.pause = true;
            _audioPaused = true;
        }

        if (!TryResolveDependencies(out error))
            return false;

        ConfigureDiagnostics();

        return true;
    }

    private void EndRun(
        float previousTimeScale,
        int previousVSync,
        int previousTargetFrameRate,
        bool previousRunInBackground,
        int exitCode)
    {
        RestoreRuntimeSettings(previousTimeScale, previousVSync, previousTargetFrameRate, previousRunInBackground);
        Time.fixedDeltaTime = _previousFixedDeltaTime;
        CleanupTemporaryObjects();
        _lastAppliedMap = null;
        _running = false;
        QuitIfStandalone(exitCode);
    }

    private IEnumerator RunMatch(
        int index,
        CombatAutoBattleRole[] allies,
        CombatAutoBattleRole[] enemies,
        int seed,
        AuthoredMapDefinition forcedMap = null,
        bool stonePositionsReversed = false)
    {
        // Separate map-pick stream from battle RNG so map loading side effects stay reproducible.
        int mapSeed = seed * 397 + 17;
        UnityEngine.Random.InitState(mapSeed);
        AuthoredMapDefinition mapDefinition = forcedMap != null
            ? forcedMap
            : _mapCandidates[UnityEngine.Random.Range(0, _mapCandidates.Length)];

        bool mapChanged = _lastAppliedMap != mapDefinition;
        bool orientationChanged = !mapChanged &&
            _mapSystem.IsStonePositionReversed != stonePositionsReversed;
        if (mapChanged || orientationChanged)
        {
            yield return _mapSystem.PrepareMapAsync(mapDefinition);
            if (!_mapSystem.TryApplyBakedAuthoredMap(
                    mapDefinition,
                    out MapData map,
                    out CombatMapApplyFailure failure))
            {
                throw new InvalidOperationException(
                    $"マップ '{mapDefinition.name}' の適用に失敗しました: {failure}");
            }
            if (!_mapSystem.ResetRuntimeMapState())
                throw new InvalidOperationException($"マップ '{mapDefinition.name}' の状態復元に失敗しました。");
            _lastAppliedMap = mapDefinition;
            yield return null;
        }

        if (!_mapSystem.TrySetStonePositionsReversed(stonePositionsReversed))
        {
            if (!_mapSystem.TryApplyBakedAuthoredMap(
                    mapDefinition,
                    out MapData reloadedMap,
                    out CombatMapApplyFailure reloadFailure))
            {
                throw new InvalidOperationException(
                    $"マップ '{mapDefinition.name}' の位置反転前再適用に失敗しました: {reloadFailure}");
            }
            _lastAppliedMap = mapDefinition;
            yield return null;
            if (!_mapSystem.TrySetStonePositionsReversed(stonePositionsReversed))
            {
                throw new InvalidOperationException(
                    $"マップ '{mapDefinition.name}' の魔石位置を reversed={stonePositionsReversed} に設定できませんでした。");
            }
        }

        UnityEngine.Random.InitState(seed);
        CombatBattleRandom.Initialize(seed);

        List<CombatParticipantSetup> allySetups = BuildSetups(allies, _allyPool);
        List<CombatParticipantSetup> enemySetups = BuildSetups(enemies, _enemyPool);
        _characterSystem.SetParticipants(allySetups, enemySetups);
        _characterSystem.TryRelocateCharactersNearMainStones();

        bool ended = false;
        CombatBattleState endState = CombatBattleState.WaitingToStart;
        void OnBattleEnded(CombatBattleState state)
        {
            ended = true;
            endState = state;
        }

        _battleFlow.BattleEnded += OnBattleEnded;
        _battleFlow.StartBattleOnCurrentMap(seed);
        if (_battleFlow.State != CombatBattleState.Running)
        {
            _battleFlow.BattleEnded -= OnBattleEnded;
            throw new InvalidOperationException("戦闘を開始できませんでした。");
        }

        Time.timeScale = _timeScale;

        yield return null;
        string diagnosticLogPath = GetCurrentDiagnosticLogPath();

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
            FlushTimeoutDiagnosticLog();
            _battleFlow.AbortBattle();
        }

        yield return null;
        diagnosticLogPath = GetCurrentDiagnosticLogPath() ?? diagnosticLogPath;

        string outcome = CombatAutoBattleOutcomes.FromBattleState(endState, timedOut);
        _results.Add(new CombatAutoBattleMatchResult
        {
            Index = index,
            Seed = seed,
            MapName = mapDefinition.name,
            StonePositionsReversed = stonePositionsReversed,
            Outcome = outcome,
            GameSeconds = Mathf.Max(0f, Time.time - startedAt),
            RealSeconds = Mathf.Max(0f, Time.realtimeSinceStartup - startedAtRealtime),
            TimeScale = _timeScale,
            FixedDeltaTime = Time.fixedDeltaTime,
            PreserveFixedDeltaTime = _preserveFixedDeltaTime,
            FixedSeed = _fixedSeed,
            SkippedAiDecisionCount = _characterSystem.TotalSkippedAiDecisionCount,
            PlayerBuildGuid = Application.buildGUID,
            UnityVersion = Application.unityVersion,
            DiagnosticLogPath = diagnosticLogPath,
        });

        if (!string.IsNullOrEmpty(diagnosticLogPath))
            Debug.Log($"[自動戦闘] 試合{index + 1} 診断ログ: {diagnosticLogPath}", this);
        else if (_diagnosticsEnabled)
            Debug.LogWarning($"[自動戦闘] 試合{index + 1} の診断ログファイルが見つかりません。CombatBattleEventLogger を確認してください。", this);
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
        _diagnosticsEnabled = !config.DisableDiagnostics;
        _fixedSeed = config.FixedSeed;
        _preserveFixedDeltaTime = config.PreserveFixedDeltaTime;
    }

    private static void RecordResult(
        CombatCompositionCandidateResult candidateResult,
        CombatAutoBattleMatchResult matchResult)
    {
        if (matchResult.Outcome == CombatAutoBattleOutcomes.Victory) candidateResult.Wins++;
        else if (matchResult.Outcome == CombatAutoBattleOutcomes.Defeat) candidateResult.Losses++;
        else candidateResult.Timeouts++;
        candidateResult.TotalGameSeconds += matchResult.GameSeconds;
        candidateResult.TotalRealSeconds += matchResult.RealSeconds;
        candidateResult.TotalSkippedAiDecisionCount += matchResult.SkippedAiDecisionCount;
        candidateResult.GameSecondsSamples.Add(matchResult.GameSeconds);
        if (matchResult.Outcome != CombatAutoBattleOutcomes.Timeout)
            candidateResult.DecidedGameSecondsSamples.Add(matchResult.GameSeconds);

        CombatCompositionScenarioResult scenario = null;
        for (int i = 0; i < candidateResult.Scenarios.Count; i++)
        {
            CombatCompositionScenarioResult existing = candidateResult.Scenarios[i];
            if (existing.MapName != matchResult.MapName ||
                existing.StonePositionsReversed != matchResult.StonePositionsReversed) continue;
            scenario = existing;
            break;
        }

        if (scenario == null)
        {
            scenario = new CombatCompositionScenarioResult
            {
                MapName = matchResult.MapName,
                StonePositionsReversed = matchResult.StonePositionsReversed,
            };
            candidateResult.Scenarios.Add(scenario);
        }

        if (matchResult.Outcome == CombatAutoBattleOutcomes.Victory) scenario.Wins++;
        else if (matchResult.Outcome == CombatAutoBattleOutcomes.Defeat) scenario.Losses++;
        else scenario.Timeouts++;
        scenario.TotalGameSeconds += matchResult.GameSeconds;
        scenario.TotalRealSeconds += matchResult.RealSeconds;
        scenario.TotalSkippedAiDecisionCount += matchResult.SkippedAiDecisionCount;
        scenario.GameSecondsSamples.Add(matchResult.GameSeconds);
        if (matchResult.Outcome != CombatAutoBattleOutcomes.Timeout)
            scenario.DecidedGameSecondsSamples.Add(matchResult.GameSeconds);
        scenario.MatchCount = scenario.Wins + scenario.Losses + scenario.Timeouts;
        scenario.WinRate = scenario.MatchCount > 0 ? (float)scenario.Wins / scenario.MatchCount : 0f;
        scenario.AverageGameSeconds = scenario.MatchCount > 0
            ? scenario.TotalGameSeconds / scenario.MatchCount
            : 0f;
        scenario.AverageRealSeconds = scenario.MatchCount > 0
            ? scenario.TotalRealSeconds / scenario.MatchCount
            : 0f;
    }

    public static int ResolveSweepSeed(
        CombatCompositionSweepConfig config,
        int localCandidateIndex,
        int localMatchIndex,
        int totalMatchesPerCandidate)
    {
        int matchIndex = config.MatchOffset + localMatchIndex;
        if (config.UseCommonSeeds) return config.BaseSeed + matchIndex;

        int candidateIndex = config.EnumerateAllCandidates
            ? config.CandidateOffset + localCandidateIndex
            : localCandidateIndex;
        return config.BaseSeed + candidateIndex * totalMatchesPerCandidate + matchIndex;
    }

    public static int ResolveSingleSeed(int baseSeed, int matchIndex, bool fixedSeed)
    {
        return fixedSeed ? baseSeed : baseSeed + matchIndex;
    }

    private static void CompleteResult(CombatCompositionCandidateResult result)
    {
        result.MatchCount = result.Wins + result.Losses + result.Timeouts;
        result.WinRate = result.MatchCount > 0 ? (float)result.Wins / result.MatchCount : 0f;
        result.AverageGameSeconds = result.MatchCount > 0
            ? result.TotalGameSeconds / result.MatchCount
            : 0f;
        result.AverageRealSeconds = result.MatchCount > 0
            ? result.TotalRealSeconds / result.MatchCount
            : 0f;
        result.MedianGameSeconds = CombatAutoBattleStatistics.Median(result.GameSecondsSamples);
        result.MinGameSeconds = CombatAutoBattleStatistics.Min(result.GameSecondsSamples);
        result.MaxGameSeconds = CombatAutoBattleStatistics.Max(result.GameSecondsSamples);
        result.MedianDecidedGameSeconds = CombatAutoBattleStatistics.Median(result.DecidedGameSecondsSamples);
        for (int i = 0; i < result.Scenarios.Count; i++)
            CompleteDurationStatistics(result.Scenarios[i]);
    }

    private static void CompleteDurationStatistics(CombatCompositionScenarioResult result)
    {
        result.MedianGameSeconds = CombatAutoBattleStatistics.Median(result.GameSecondsSamples);
        result.MinGameSeconds = CombatAutoBattleStatistics.Min(result.GameSecondsSamples);
        result.MaxGameSeconds = CombatAutoBattleStatistics.Max(result.GameSecondsSamples);
        result.MedianDecidedGameSeconds = CombatAutoBattleStatistics.Median(result.DecidedGameSecondsSamples);
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
            {
                string available = DescribeMapCandidates();
                throw new InvalidOperationException(
                    $"マップ候補に '{name}' がありません。シーンの Runner に割り当ててビルドしてください。利用可能: {available}");
            }

            if (!filtered.Contains(found))
                filtered.Add(found);
        }

        return filtered.ToArray();
    }

    private string DescribeMapCandidates()
    {
        if (_mapCandidates == null || _mapCandidates.Length == 0) return "(なし)";
        var names = new List<string>();
        for (int i = 0; i < _mapCandidates.Length; i++)
        {
            AuthoredMapDefinition map = _mapCandidates[i];
            names.Add(map != null ? map.name : "null");
        }

        return string.Join(", ", names);
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

    private void EnsureBattleEventLogger()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        CombatBattleEventLogger logger = FindAnyObjectByType<CombatBattleEventLogger>(FindObjectsInactive.Include);
        if (logger == null)
        {
            logger = gameObject.AddComponent<CombatBattleEventLogger>();
            Debug.Log("[自動戦闘] CombatBattleEventLogger を追加しました。", this);
        }

        logger.enabled = true;
#else
        throw new InvalidOperationException(
            "自動戦闘の診断ログには Development Build が必要です。Build Player は Development でビルドしてください。");
#endif
    }

    private void ConfigureDiagnostics()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (_diagnosticsEnabled) return;
        CombatBattleEventLogger logger = FindAnyObjectByType<CombatBattleEventLogger>(FindObjectsInactive.Include);
        if (logger != null) logger.enabled = false;
#endif
    }

    private static IEnumerator Drive(IEnumerator match, Action<Exception> onError)
    {
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
                onError?.Invoke(ex);
                yield break;
            }

            if (!moved) yield break;
            yield return current;
        }
    }

    private static void FlushTimeoutDiagnosticLog()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        CombatBattleEventLogger logger = FindAnyObjectByType<CombatBattleEventLogger>(FindObjectsInactive.Include);
        logger?.FlushTimeoutEnd();
#endif
    }

    private static string GetCurrentDiagnosticLogPath()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        CombatBattleEventLogger logger = FindAnyObjectByType<CombatBattleEventLogger>(FindObjectsInactive.Include);
        return logger != null ? logger.CurrentLogFilePath : null;
#else
        return null;
#endif
    }

    private bool TryValidateSettings(out string error)
    {
        if (!TryValidateMapCandidates(out error)) return false;

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

    private bool TryValidateMapCandidates(out string error)
    {
        if (_mapCandidates == null || _mapCandidates.Length == 0)
        {
            error = "マップ候補が空です。CombatAutoBattleRunner の Map Candidates に AuthoredMap を割り当ててください。";
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

        error = null;
        return true;
    }

    private void CaptureCharacterPools(int requiredPerSide)
    {
        _allyPool.Clear();
        _enemyPool.Clear();
        AddUnique(_allyPool, _characterSystem.AllyCharacters);
        AddUnique(_enemyPool, _characterSystem.EnemyCharacters);
        if (_allyPool.Count < requiredPerSide)
            throw new InvalidOperationException($"味方キャラクターが不足しています。必要{requiredPerSide} / 所持{_allyPool.Count}");
        if (_enemyPool.Count < requiredPerSide)
            throw new InvalidOperationException($"敵キャラクターが不足しています。必要{requiredPerSide} / 所持{_enemyPool.Count}");
    }

    private void LoadWeapons()
    {
        _weapons.Clear();
        AddWeapons(_weaponConfigs, overwrite: true);

        CombatCharacterSelection selection = FindAnyObjectByType<CombatCharacterSelection>(FindObjectsInactive.Include);
        if (selection != null)
            AddWeapons(selection.WeaponOptions, overwrite: false);

        if (_weapons.Count == 0)
        {
            throw new InvalidOperationException(
                "WeaponConfig を解決できません。Runner の Weapon Configs に割り当てるか、CombatCharacterSelection の Weapon Options を設定してください。");
        }
    }

    private void ValidateRoles(IReadOnlyList<CombatAutoBattleRole> roles)
    {
        if (roles == null || roles.Count == 0)
            throw new InvalidOperationException("編成が空です。");

        for (int i = 0; i < roles.Count; i++)
        {
            CombatAutoBattleRole role = roles[i];
            if (role == null)
                throw new InvalidOperationException($"編成の位置{i}がnullです。");
            if (!_weapons.ContainsKey(role.Weapon))
                throw new InvalidOperationException($"編成の位置{i}で使用する武器 {role.Weapon} のWeaponConfigがありません。");
        }
    }

    private void ValidateMapAvailability(
        bool evaluateBothStonePositions,
        bool useFixedStonePosition = false,
        bool stonePositionsReversed = false)
    {
        bool validateNormal = !useFixedStonePosition || !stonePositionsReversed;
        bool validateReversed = useFixedStonePosition
            ? stonePositionsReversed
            : evaluateBothStonePositions;
        for (int i = 0; i < _mapCandidates.Length; i++)
        {
            AuthoredMapDefinition map = _mapCandidates[i];
            if (validateNormal)
            {
                CombatMapAvailability normal = CombatMapAvailability.Evaluate(map, stonePositionsReversed: false);
                if (!normal.CanStartBattle)
                    throw new InvalidOperationException($"マップ '{map.name}' の通常配置を開始できません: {normal.Reason}");
            }

            if (validateReversed)
            {
                CombatMapAvailability reversed = CombatMapAvailability.Evaluate(map, stonePositionsReversed: true);
                if (!reversed.CanStartBattle)
                    throw new InvalidOperationException($"マップ '{map.name}' の反転配置を開始できません: {reversed.Reason}");
            }
        }
    }

    private void LogPreflight(int requiredPerSide)
    {
        Debug.Log(
            $"[自動戦闘][Preflight] maps=[{DescribeMapCandidates()}] " +
            $"allies={_allyPool.Count}/{requiredPerSide} enemies={_enemyPool.Count}/{requiredPerSide} " +
            $"weapons=[{string.Join(", ", _weapons.Keys)}] timeScale={_timeScale} " +
            $"fixedDeltaTime={Time.fixedDeltaTime} preserveFixedDeltaTime={_preserveFixedDeltaTime} " +
            $"buildGuid={Application.buildGUID} unity={Application.unityVersion}",
            this);
    }

    private void AddWeapons(IReadOnlyList<WeaponConfig> options, bool overwrite)
    {
        if (options == null) return;
        for (int i = 0; i < options.Count; i++)
        {
            WeaponConfig weapon = options[i];
            if (weapon == null) continue;
            if (!overwrite && _weapons.ContainsKey(weapon.Kind)) continue;
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
                GetOrCreatePersonality(role.Personality)));
        }

        return setups;
    }

    private CombatAiPersonalityProfile GetOrCreatePersonality(CombatAiPersonalityKind kind)
    {
        if (_personalityCache.TryGetValue(kind, out CombatAiPersonalityProfile cached) && cached != null)
            return cached;

        CombatAiPersonalityProfile profile = CombatAiPersonalityProfile.CreateBuiltInProfile(kind);
        _personalityCache[kind] = profile;
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
        _personalityCache.Clear();
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

    private static void AddUnique(List<Character> destination, IReadOnlyList<Character> source)
    {
        for (int i = 0; i < source.Count; i++)
        {
            Character character = source[i];
            if (character != null && !destination.Contains(character))
                destination.Add(character);
        }
    }

    private void RestoreRuntimeSettings(
        float timeScale,
        int vSync,
        int targetFrameRate,
        bool runInBackground)
    {
        Time.timeScale = timeScale;
        QualitySettings.vSyncCount = vSync;
        Application.targetFrameRate = targetFrameRate;
        Application.runInBackground = runInBackground;
        if (_audioPaused)
        {
            AudioListener.pause = false;
            _audioPaused = false;
        }
    }

    private static void QuitIfStandalone(int exitCode)
    {
        if (!Application.isEditor)
            Application.Quit(exitCode);
    }
}
