#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using WarSimulation.Combat.Map;

[DisallowMultipleComponent]
public sealed class CombatBattleEventLogger : CombatDebugBehaviour
{
    private sealed class ActivePlanLog
    {
        public CombatAiPlan Plan;
        public int PlanId;
        public int SuppressedCount;
        public int DestinationUpdates;
        public float FirstSeenTime;
        public string LastDestination;
        public bool HasPlan;
    }

    public override string InspectorDescription => "戦闘開始から終了まで、AI判断・HP変化・定期状態をログファイルへ記録します。";

    [SerializeField] private bool _enabled = true;
    [SerializeField, Min(1f)] private float _snapshotIntervalSeconds = 10f;
    [SerializeField, Min(1)] private int _maxRetainedLogFiles = 50;

    private readonly CombatBattleLogFormatter _formatter = new CombatBattleLogFormatter();
    private readonly List<CombatHealth> _subscribedHealth = new List<CombatHealth>();
    private readonly Dictionary<Character, ActivePlanLog> _activePlans = new Dictionary<Character, ActivePlanLog>();

    private StreamWriter _writer;
    private CombatBattleState _lastBattleState = CombatBattleState.WaitingToStart;
    private float _battleStartTime;
    private float _nextSnapshotTime;
    private string _logFilePath;
    private int _nextPlanId;
    private bool _applicationQuitting;
    private CombatMagicStoneSystem _magicStoneSystem;
    private CombatCharacterSystem _characterSystem;
    private CombatBattleFlow _battleFlow;
    private CombatMapSystem _mapSystem;
    private CombatAutoBattleRunner _autoBattleRunner;

    public string CurrentLogFilePath => _logFilePath;

    private void Awake()
    {
        if (!IsDebugAllowed())
        {
            enabled = false;
        }
    }

    private void OnEnable()
    {
        if (!IsDebugAllowed()) return;
        SubscribeEvents();
    }

    private void OnDisable()
    {
        UnsubscribeEvents();
        if (_writer == null)
        {
            FlushVisionDiagnostics();
            return;
        }

        float duration = Mathf.Max(0f, Time.time - _battleStartTime);
        FlushVisionDiagnostics();
        FlushPlanRepeats(duration);
        WriteAiTimingSummary(duration);
        WriteLine(_formatter.FormatBattleAborted(
            duration,
            _applicationQuitting ? "ApplicationQuit" : "ComponentDisabled"));
        string closedPath = _logFilePath;
        CloseLog();
        if (!string.IsNullOrEmpty(closedPath))
        {
            Debug.Log($"[診断ログ] 書き込み中断: {Path.GetFileName(closedPath)}", this);
        }
    }

    private void OnApplicationQuit()
    {
        _applicationQuitting = true;
    }

    private void Update()
    {
        if (!IsDebugAllowed())
        {
            AbortOpenLog("DebugBuildUnavailable");
            return;
        }

        if (!_enabled)
        {
            AbortOpenLog("LoggerDisabled");
            return;
        }

        PollBattleState();
        MaybeWriteSnapshot();
    }

    private void PollBattleState()
    {
        ResolveDependencies();
        if (_battleFlow == null) return;

        CombatBattleState state = _battleFlow.State;
        if (_lastBattleState != CombatBattleState.Running && state == CombatBattleState.Running)
        {
            StartLog();
        }
        else if (_lastBattleState == CombatBattleState.Running && state != CombatBattleState.Running)
        {
            if (state == CombatBattleState.Victory || state == CombatBattleState.Defeat)
            {
                EndLog(state);
            }
            else
            {
                EndAbortedLog("BattleReset");
            }
        }

        _lastBattleState = state;
    }

    private void StartLog()
    {
        if (_writer != null)
        {
            CloseBattleLog("Restarted", aborted: true);
        }
        else
        {
            CloseLog();
        }

        _formatter.Reset();
        _activePlans.Clear();
        _nextPlanId = 0;
        _applicationQuitting = false;
        CombatVisionObstructionDiagnostics.BeginBattle();
        _battleStartTime = Time.time;
        _nextSnapshotTime = _battleStartTime + _snapshotIntervalSeconds;

        ResolveDependencies();
        string directoryPath = GetLogDirectoryPath();
        Directory.CreateDirectory(directoryPath);
        PruneOldLogFiles(directoryPath);
        string fileName = "battle_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".log";
        _logFilePath = Path.Combine(directoryPath, fileName);
        _writer = new StreamWriter(_logFilePath, append: false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        Debug.Log($"[診断ログ] 書き込み開始: {fileName}\n{_logFilePath}", this);

        WriteLine(_formatter.FormatBattleHeader(_logFilePath, BuildHeaderMetadata()));
        WriteLine("[t=0.0s] BATTLE_START");
        SubscribeCharacterHealth();
        WriteSnapshotIfPossible(force: true);
        FlushWriter();
    }

    private void EndLog(CombatBattleState outcome)
    {
        if (outcome == CombatBattleState.WaitingToStart)
        {
            EndAbortedLog("BattleReset");
            return;
        }

        CloseBattleLog(outcome.ToString(), aborted: false);
    }

    private void EndAbortedLog(string reason)
    {
        CloseBattleLog(reason, aborted: true);
    }

    private void CloseBattleLog(string outcomeOrReason, bool aborted)
    {
        if (_writer == null) return;

        float duration = Mathf.Max(0f, Time.time - _battleStartTime);
        FlushVisionDiagnostics();
        FlushPlanRepeats(duration);
        WriteAiTimingSummary(duration);
        if (aborted)
        {
            WriteLine(_formatter.FormatBattleAborted(duration, outcomeOrReason));
        }
        else
        {
            TryGetBattleSnapshot(
                out int ownStoneHp,
                out _,
                out int enemyStoneHp,
                out _,
                out int allyAlive,
                out int enemyAlive);
            WriteLine(_formatter.FormatBattleEnd(
                duration,
                outcomeOrReason,
                ownStoneHp,
                enemyStoneHp,
                allyAlive,
                enemyAlive));
        }

        UnsubscribeCharacterHealth();
        string closedPath = _logFilePath;
        CloseLog();
        if (!string.IsNullOrEmpty(closedPath))
        {
            string marker = aborted ? "aborted=" + outcomeOrReason : "outcome=" + outcomeOrReason;
            Debug.Log($"[診断ログ] 書き込み終了: {Path.GetFileName(closedPath)} {marker}", this);
        }
    }

    public void FlushTimeoutEnd()
    {
        if (_writer == null) return;
        CloseBattleLog("Timeout", aborted: false);
        _lastBattleState = CombatBattleState.WaitingToStart;
    }

    private void AbortOpenLog(string reason)
    {
        if (_writer == null) return;
        UnsubscribeEvents();
        EndAbortedLog(reason);
        _lastBattleState = CombatBattleState.WaitingToStart;
    }

    private void FlushVisionDiagnostics()
    {
        CombatVisionObstructionDiagnostics.WriteTo(WriteLine);
    }

    private void MaybeWriteSnapshot()
    {
        if (_writer == null || Time.time < _nextSnapshotTime) return;
        _nextSnapshotTime = Time.time + _snapshotIntervalSeconds;
        WriteSnapshotIfPossible(force: false);
        FlushWriter();
    }

    private void WriteSnapshotIfPossible(bool force)
    {
        if (_writer == null) return;
        if (!TryGetBattleSnapshot(
                out int ownStoneHp,
                out int ownStoneMaxHp,
                out int enemyStoneHp,
                out int enemyStoneMaxHp,
                out int allyAlive,
                out int enemyAlive))
        {
            if (!force) return;
            ownStoneHp = 0;
            ownStoneMaxHp = 0;
            enemyStoneHp = 0;
            enemyStoneMaxHp = 0;
            allyAlive = CountAlive(_characterSystem != null ? _characterSystem.AllyCharacters : null);
            enemyAlive = CountAlive(_characterSystem != null ? _characterSystem.EnemyCharacters : null);
        }

        float battleTime = Mathf.Max(0f, Time.time - _battleStartTime);
        WriteLine(_formatter.FormatSnapshot(
            battleTime,
            ownStoneHp,
            ownStoneMaxHp,
            enemyStoneHp,
            enemyStoneMaxHp,
            allyAlive,
            enemyAlive));
    }

    private void SubscribeEvents()
    {
        CombatAiDecisionEvents.ObjectiveChanged -= OnObjectiveChanged;
        CombatAiDecisionEvents.ObjectiveChanged += OnObjectiveChanged;
        CombatAiDecisionEvents.PlanSelected -= OnPlanSelected;
        CombatAiDecisionEvents.PlanSelected += OnPlanSelected;
        CombatAiDecisionEvents.PlanExecuted -= OnPlanExecuted;
        CombatAiDecisionEvents.PlanExecuted += OnPlanExecuted;
        CombatSkillActionEvents.Completed -= OnSkillCompleted;
        CombatSkillActionEvents.Completed += OnSkillCompleted;
        CombatSkillActionEvents.Cancelled -= OnSkillCancelled;
        CombatSkillActionEvents.Cancelled += OnSkillCancelled;
        ResolveDependencies();
        if (_magicStoneSystem != null)
        {
            _magicStoneSystem.MainStoneDestroyed -= OnMainStoneDestroyed;
            _magicStoneSystem.MainStoneDestroyed += OnMainStoneDestroyed;
        }
    }

    private void UnsubscribeEvents()
    {
        CombatAiDecisionEvents.ObjectiveChanged -= OnObjectiveChanged;
        CombatAiDecisionEvents.PlanSelected -= OnPlanSelected;
        CombatAiDecisionEvents.PlanExecuted -= OnPlanExecuted;
        CombatSkillActionEvents.Completed -= OnSkillCompleted;
        CombatSkillActionEvents.Cancelled -= OnSkillCancelled;
        if (_magicStoneSystem != null)
        {
            _magicStoneSystem.MainStoneDestroyed -= OnMainStoneDestroyed;
        }

        UnsubscribeCharacterHealth();
    }

    private void SubscribeCharacterHealth()
    {
        UnsubscribeCharacterHealth();
        Character[] characters = FindObjectsByType<Character>(FindObjectsInactive.Exclude);
        for (int i = 0; i < characters.Length; i++)
        {
            Character character = characters[i];
            if (character == null || character.Health == null) continue;
            character.Health.Defeated += OnCharacterDefeated;
            _subscribedHealth.Add(character.Health);
        }
    }

    private void UnsubscribeCharacterHealth()
    {
        for (int i = 0; i < _subscribedHealth.Count; i++)
        {
            CombatHealth health = _subscribedHealth[i];
            if (health != null)
            {
                health.Defeated -= OnCharacterDefeated;
            }
        }

        _subscribedHealth.Clear();
    }

    private void OnObjectiveChanged(
        Character owner,
        CombatObjective previous,
        CombatObjective next,
        IReadOnlyList<CombatAiReasonCode> reasonCodes)
    {
        if (!CanLogActiveAiEvent(owner)) return;

        var reasonLabels = new List<string>();
        if (reasonCodes != null)
        {
            for (int i = 0; i < reasonCodes.Count; i++)
            {
                reasonLabels.Add(CombatAiDebugLabels.Reason(reasonCodes[i]));
            }
        }

        string weaponLabel = CombatAiDebugLabels.WeaponShort(owner.EquippedWeapon);
        float battleTime = Mathf.Max(0f, Time.time - _battleStartTime);
        WriteLine(_formatter.FormatObjectiveChange(
            battleTime,
            owner.name,
            weaponLabel,
            previous,
            next,
            reasonLabels));
    }

    private void OnPlanSelected(Character owner, CombatAiPlan previous, CombatAiPlan next)
    {
        if (!CanLogActiveAiEvent(owner)) return;

        float battleTime = Mathf.Max(0f, Time.time - _battleStartTime);
        if (!_activePlans.TryGetValue(owner, out ActivePlanLog active))
        {
            active = new ActivePlanLog();
            _activePlans.Add(owner, active);
        }

        if (!active.HasPlan || CombatBattleLogFormatter.HasMeaningfulPlanChange(active.Plan, next))
        {
            FlushPlanRepeat(owner, active, battleTime);
            active.Plan = next;
            active.PlanId = ++_nextPlanId;
            active.SuppressedCount = 0;
            active.DestinationUpdates = 0;
            active.FirstSeenTime = battleTime;
            active.LastDestination = ResolveDestination(next);
            active.HasPlan = true;
            WriteLine(_formatter.FormatAiPlan(
                battleTime,
                owner.name,
                previous.Objective,
                next,
                active.PlanId,
                CombatBattleRandom.GetDecisionTick(owner)));
            return;
        }

        active.SuppressedCount++;
        string destination = ResolveDestination(next);
        if (destination != active.LastDestination)
        {
            active.DestinationUpdates++;
            active.LastDestination = destination;
        }
    }

    private void OnPlanExecuted(
        Character owner,
        CombatAiPlan plan,
        bool movementStarted,
        bool skillStarted,
        string failureReason)
    {
        if (_writer == null || owner == null) return;

        _activePlans.TryGetValue(owner, out ActivePlanLog active);
        int planId = active != null && active.HasPlan ? active.PlanId : 0;
        int decisionTick = CombatBattleRandom.GetDecisionTick(owner);
        bool actorDefeated = owner.Health != null && !owner.Health.IsAlive;
        bool battleEnded = _battleFlow != null && _battleFlow.State != CombatBattleState.Running;
        float battleTime = Mathf.Max(0f, Time.time - _battleStartTime);

        if (!movementStarted && !skillStarted && (actorDefeated || battleEnded))
        {
            WriteLine(_formatter.FormatAiCancelled(
                battleTime,
                owner.name,
                plan,
                planId,
                decisionTick,
                actorDefeated ? "ActorDefeated" : "BattleEnded"));
            return;
        }

        if (!CombatBattleLogFormatter.ShouldLogAiExecution(movementStarted, skillStarted, failureReason))
        {
            return;
        }

        WriteLine(_formatter.FormatAiExecution(
            battleTime,
            owner.name,
            plan,
            planId,
            decisionTick,
            movementStarted,
            skillStarted,
            failureReason));
    }

    private void OnSkillCompleted(CombatSkillActionResult result)
    {
        LogSkillAction(result);
    }

    private void OnSkillCancelled(CombatSkillActionResult result)
    {
        LogSkillAction(result);
    }

    private void LogSkillAction(CombatSkillActionResult result)
    {
        Character user = result?.Action.Actor;
        if (_writer == null || user == null || result == null) return;

        float battleTime = Mathf.Max(0f, Time.time - _battleStartTime);
        if (result.Effects.Count > 0)
        {
            WriteMagicStoneTargetDiagnostics(result, user, battleTime);
        }

        string targetName = ResolveSkillTargetName(result);
        string line = result.Outcome == CombatSkillActionOutcome.Completed
            ? _formatter.FormatSkillUsed(
                battleTime,
                user.name,
                result.Action.SkillName,
                targetName,
                result.Action.ActionId,
                result.Action.DecisionTick,
                result.Action.SkillId)
            : _formatter.FormatSkillResult(
                battleTime,
                user.name,
                result.Action.SkillName,
                targetName,
                result.Outcome,
                result.Action.ActionId,
                result.Action.DecisionTick,
                result.Action.SkillId);
        WriteLine(line);
    }

    private void WriteMagicStoneTargetDiagnostics(
        CombatSkillActionResult result,
        Character user,
        float battleTime)
    {
        for (int i = 0; i < result.Effects.Count; i++)
        {
            CombatActionEffect effect = result.Effects[i];
            if (effect.Kind != CombatActionEffectKind.MagicStoneDamage) continue;

            WriteLine(_formatter.FormatStoneTarget(
                battleTime,
                user.name,
                effect.MagicStoneFeatureIndex,
                effect.Amount,
                result.Action.ActionId,
                result.Action.DecisionTick));
        }
    }

    private static string ResolveSkillTargetName(CombatSkillActionResult result)
    {
        Character target = result.Action.Context.PrimaryTarget;
        if (target == null)
        {
            for (int i = 0; i < result.Effects.Count; i++)
            {
                if (result.Effects[i].Target == null) continue;
                target = result.Effects[i].Target;
                break;
            }
        }

        if (target != null) return target.name;

        MagicStone stone = result.Action.Context.PrimaryStone;
        if (stone == null && result.Action.Context.ResolvedStones != null &&
            result.Action.Context.ResolvedStones.Count > 0)
        {
            stone = result.Action.Context.ResolvedStones[0];
        }

        return stone != null ? "stone#" + stone.FeatureIndex : null;
    }

    private void FlushPlanRepeats(float battleTime)
    {
        foreach (KeyValuePair<Character, ActivePlanLog> pair in _activePlans)
        {
            FlushPlanRepeat(pair.Key, pair.Value, battleTime);
        }

        _activePlans.Clear();
    }

    private void FlushPlanRepeat(Character owner, ActivePlanLog active, float battleTime)
    {
        if (active == null || !active.HasPlan || active.SuppressedCount == 0) return;

        WriteLine(_formatter.FormatAiPlanRepeat(
            battleTime,
            owner != null ? owner.name : "unknown",
            active.PlanId,
            active.SuppressedCount,
            Mathf.Max(0f, battleTime - active.FirstSeenTime),
            active.DestinationUpdates,
            active.LastDestination));
        active.SuppressedCount = 0;
    }

    private void WriteAiTimingSummary(float battleTime)
    {
        if (_characterSystem == null || _characterSystem.AiDecisionBatchSampleCount == 0) return;

        WriteLine(string.Format(
            CultureInfo.InvariantCulture,
            "[t={0:0.0}s] AI_TIMING samples={1} avgMs={2:0.000} medianMs={3:0.000} stdDevMs={4:0.000} minMs={5:0.000} maxMs={6:0.000} skipped={7}",
            battleTime,
            _characterSystem.AiDecisionBatchSampleCount,
            _characterSystem.AiDecisionBatchAverageDurationMilliseconds,
            _characterSystem.AiDecisionBatchMedianDurationMilliseconds,
            _characterSystem.AiDecisionBatchStandardDeviationMilliseconds,
            _characterSystem.AiDecisionBatchMinimumDurationMilliseconds,
            _characterSystem.AiDecisionBatchMaximumDurationMilliseconds,
            _characterSystem.TotalSkippedAiDecisionCount));
        WriteAiPhaseTiming("participants", _characterSystem.AiParticipantTiming, battleTime);
        WriteAiPhaseTiming("visionScan", _characterSystem.AiVisionScanTiming, battleTime);
        WriteAiPhaseTiming("visionShare", _characterSystem.AiVisionShareTiming, battleTime);
        WriteAiPhaseTiming("worldSnapshot", _characterSystem.AiWorldSnapshotTiming, battleTime);
        WriteAiPhaseTiming("context", _characterSystem.AiContextTiming, battleTime);
        WriteAiPhaseTiming("planning", _characterSystem.AiPlanningTiming, battleTime);
        WriteAiPhaseTiming("execution", _characterSystem.AiExecutionTiming, battleTime);
    }

    private void WriteAiPhaseTiming(
        string phase,
        CombatAiTimingAccumulator timing,
        float battleTime)
    {
        if (timing == null || timing.Count == 0) return;

        WriteLine(string.Format(
            CultureInfo.InvariantCulture,
            "[t={0:0.0}s] AI_TIMING_PHASE phase={1} samples={2} avgMs={3:0.000} medianMs={4:0.000} stdDevMs={5:0.000} minMs={6:0.000} maxMs={7:0.000}",
            battleTime,
            phase,
            timing.Count,
            timing.AverageMilliseconds,
            timing.MedianMilliseconds,
            timing.StandardDeviationMilliseconds,
            timing.MinimumMilliseconds,
            timing.MaximumMilliseconds));
    }

    private static string ResolveDestination(CombatAiPlan plan)
    {
        return plan.MoveTarget.HasDestination
            ? CombatBattleLogFormatter.FormatPosition(plan.MoveTarget.Destination)
            : string.Empty;
    }

    private bool CanLogActiveAiEvent(Character owner)
    {
        if (_writer == null || owner == null) return false;
        if (_battleFlow != null && _battleFlow.State != CombatBattleState.Running) return false;
        return owner.Health == null || owner.Health.IsAlive;
    }

    private void OnCharacterDefeated(Character victim, Character killer)
    {
        if (_writer == null || victim == null) return;

        float battleTime = Mathf.Max(0f, Time.time - _battleStartTime);
        WriteLine(_formatter.FormatDefeated(
            battleTime,
            victim.name,
            killer != null ? killer.name : null));
    }

    private void OnMainStoneDestroyed(FeatureType stoneType)
    {
        if (_writer == null) return;

        float battleTime = Mathf.Max(0f, Time.time - _battleStartTime);
        WriteLine(_formatter.FormatStoneDestroyed(battleTime, stoneType));
    }

    private bool TryGetBattleSnapshot(
        out int ownStoneHp,
        out int ownStoneMaxHp,
        out int enemyStoneHp,
        out int enemyStoneMaxHp,
        out int allyAlive,
        out int enemyAlive)
    {
        ownStoneHp = 0;
        ownStoneMaxHp = 0;
        enemyStoneHp = 0;
        enemyStoneMaxHp = 0;
        allyAlive = 0;
        enemyAlive = 0;

        ResolveDependencies();
        MagicStoneRuntimeState ownState = null;
        MagicStoneRuntimeState enemyState = null;
        bool hasOwn = _magicStoneSystem != null &&
            _magicStoneSystem.TryGetState(FeatureType.OwnMainStone, out ownState);
        bool hasEnemy = _magicStoneSystem != null &&
            _magicStoneSystem.TryGetState(FeatureType.EnemyMainStone, out enemyState);
        if (!hasOwn && !hasEnemy && _characterSystem == null) return false;

        if (hasOwn && ownState != null)
        {
            ownStoneHp = ownState.HP;
            ownStoneMaxHp = ownState.MaxHP;
        }

        if (hasEnemy && enemyState != null)
        {
            enemyStoneHp = enemyState.HP;
            enemyStoneMaxHp = enemyState.MaxHP;
        }

        allyAlive = CountAlive(_characterSystem != null ? _characterSystem.AllyCharacters : null);
        enemyAlive = CountAlive(_characterSystem != null ? _characterSystem.EnemyCharacters : null);
        return true;
    }

    private static int CountAlive(List<Character> characters)
    {
        if (characters == null) return 0;

        int count = 0;
        for (int i = 0; i < characters.Count; i++)
        {
            Character character = characters[i];
            if (character != null && character.Health != null && character.Health.IsAlive)
            {
                count++;
            }
        }

        return count;
    }

    private CombatBattleLogMetadata BuildHeaderMetadata()
    {
        string mapName = _mapSystem != null && _mapSystem.AuthoredMap != null
            ? _mapSystem.AuthoredMap.name
            : "runtime";
        string preserveFixedDeltaTime = _autoBattleRunner != null
            ? (_autoBattleRunner.IsPreservingFixedDeltaTime ? "true" : "false")
            : "n/a";
        return new CombatBattleLogMetadata(
            mapName,
            CombatBattleRandom.CurrentSeed,
            _mapSystem != null && _mapSystem.IsStonePositionReversed,
            ResolveWeatherLabel(),
            Time.timeScale,
            Time.fixedDeltaTime,
            preserveFixedDeltaTime,
            Application.unityVersion,
            Application.buildGUID,
            BuildParticipantSummary());
    }

    private string BuildParticipantSummary()
    {
        if (_characterSystem == null) return "unknown";

        var entries = new List<string>();
        AppendParticipantSummary(entries, "A", _characterSystem.AllyCharacters);
        AppendParticipantSummary(entries, "E", _characterSystem.EnemyCharacters);
        return entries.Count > 0 ? string.Join("|", entries) : "none";
    }

    private static void AppendParticipantSummary(
        List<string> entries,
        string teamLabel,
        List<Character> characters)
    {
        if (characters == null) return;
        for (int i = 0; i < characters.Count; i++)
        {
            Character character = characters[i];
            if (character == null) continue;
            entries.Add(
                teamLabel + ":" + character.name + "[" +
                CombatAiDebugLabels.WeaponShort(character.EquippedWeapon) + "/" +
                CombatAiDebugLabels.PersonalityShort(character.PersonalityProfile) + "]");
        }
    }

    private string ResolveWeatherLabel()
    {
        ResolveDependencies();
        return _mapSystem != null ? _mapSystem.CurrentWeather.ToString() : string.Empty;
    }

    private void ResolveDependencies()
    {
        _battleFlow ??= FindAnyObjectByType<CombatBattleFlow>();
        _magicStoneSystem ??= CombatMagicStoneSystemResolver.Resolve();
        _mapSystem ??= CombatSceneContext.Instance != null
            ? CombatSceneContext.Instance.MapSystem
            : null;
        _mapSystem ??= FindAnyObjectByType<CombatMapSystem>();
        _autoBattleRunner ??= FindAnyObjectByType<CombatAutoBattleRunner>();
        if (_characterSystem == null)
        {
            CombatSceneContext context = CombatSceneContext.Instance;
            _characterSystem = context != null ? context.CharacterSystem : null;
            _characterSystem ??= FindAnyObjectByType<CombatCharacterSystem>();
        }
    }

    private static string GetLogDirectoryPath()
    {
        return CombatDebugPaths.GetLogsDirectory("CombatBattles");
    }

    private void PruneOldLogFiles(string directoryPath)
    {
        if (_maxRetainedLogFiles < 1 || !Directory.Exists(directoryPath)) return;

        string[] files = Directory.GetFiles(directoryPath, "battle_*.log");
        if (files.Length < _maxRetainedLogFiles) return;

        Array.Sort(files, (left, right) => File.GetLastWriteTimeUtc(left).CompareTo(File.GetLastWriteTimeUtc(right)));
        int removeCount = files.Length - _maxRetainedLogFiles + 1;
        for (int i = 0; i < removeCount; i++)
        {
            try
            {
                File.Delete(files[i]);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[診断ログ] 古いログを削除できませんでした: {files[i]}\n{ex.Message}", this);
            }
        }
    }

    private void WriteLine(string line)
    {
        if (_writer == null || string.IsNullOrEmpty(line)) return;
        _writer.WriteLine(line);
    }

    private void FlushWriter()
    {
        _writer?.Flush();
    }

    private void CloseLog()
    {
        if (_writer == null) return;
        _writer.Flush();
        _writer.Dispose();
        _writer = null;
    }

    private static bool IsDebugAllowed()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        return true;
#else
        return false;
#endif
    }
}
#endif
