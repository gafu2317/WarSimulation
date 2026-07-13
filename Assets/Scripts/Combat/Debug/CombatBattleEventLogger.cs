#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using WarSimulation.Combat.Map;

[DisallowMultipleComponent]
public sealed class CombatBattleEventLogger : CombatDebugBehaviour
{
    public override string InspectorDescription => "戦闘開始から終了まで、AI判断・HP変化・定期状態をログファイルへ記録します。";

    private const string LogDirectoryName = "Logs/CombatBattles";

    [SerializeField] private bool _enabled = true;
    [SerializeField, Min(1f)] private float _snapshotIntervalSeconds = 10f;

    private readonly CombatBattleLogFormatter _formatter = new CombatBattleLogFormatter();
    private readonly List<CombatHealth> _subscribedHealth = new List<CombatHealth>();

    private StreamWriter _writer;
    private CombatBattleState _lastBattleState = CombatBattleState.WaitingToStart;
    private float _battleStartTime;
    private float _nextSnapshotTime;
    private string _logFilePath;
    private CombatMagicStoneSystem _magicStoneSystem;
    private CombatCharacterSystem _characterSystem;
    private CombatBattleFlow _battleFlow;

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
        CloseLog();
    }

    private void Update()
    {
        if (!_enabled || !IsDebugAllowed()) return;

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
            EndLog(state);
        }

        _lastBattleState = state;
    }

    private void StartLog()
    {
        CloseLog();
        _formatter.Reset();
        _battleStartTime = Time.time;
        _nextSnapshotTime = _battleStartTime + _snapshotIntervalSeconds;

        string directoryPath = GetLogDirectoryPath();
        Directory.CreateDirectory(directoryPath);
        string fileName = "battle_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".log";
        _logFilePath = Path.Combine(directoryPath, fileName);
        _writer = new StreamWriter(_logFilePath, append: false, Encoding.UTF8);

        string weatherLabel = ResolveWeatherLabel();
        WriteLine(_formatter.FormatBattleHeader(_logFilePath, weatherLabel));
        WriteLine("[t=0.0s] BATTLE_START");
        SubscribeCharacterHealth();
        WriteSnapshotIfPossible(force: true);
    }

    private void EndLog(CombatBattleState outcome)
    {
        if (_writer == null) return;

        float duration = Mathf.Max(0f, Time.time - _battleStartTime);
        TryGetBattleSnapshot(out int ownStoneHp, out int ownStoneMaxHp, out int enemyStoneHp, out int enemyStoneMaxHp, out int allyAlive, out int enemyAlive);
        WriteLine(_formatter.FormatBattleEnd(duration, outcome, ownStoneHp, enemyStoneHp, allyAlive, enemyAlive));
        UnsubscribeCharacterHealth();
        CloseLog();
    }

    private void MaybeWriteSnapshot()
    {
        if (_writer == null || Time.time < _nextSnapshotTime) return;
        _nextSnapshotTime = Time.time + _snapshotIntervalSeconds;
        WriteSnapshotIfPossible(force: false);
    }

    private void WriteSnapshotIfPossible(bool force)
    {
        if (_writer == null) return;
        if (!TryGetBattleSnapshot(out int ownStoneHp, out int ownStoneMaxHp, out int enemyStoneHp, out int enemyStoneMaxHp, out int allyAlive, out int enemyAlive))
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
        CombatSkillUseEvents.SkillUsed -= OnSkillUsed;
        CombatSkillUseEvents.SkillUsed += OnSkillUsed;
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
        CombatSkillUseEvents.SkillUsed -= OnSkillUsed;
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

    private void OnObjectiveChanged(Character owner, CombatObjective previous, CombatObjective next)
    {
        if (_writer == null || owner == null) return;

        IReadOnlyList<string> reasonLabels = BuildObjectiveReasonLabels(owner, previous, next);
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

    private void OnSkillUsed(Character user, string skillName)
    {
        if (_writer == null || user == null) return;

        float battleTime = Mathf.Max(0f, Time.time - _battleStartTime);
        string line = _formatter.FormatSkillUsed(battleTime, user.name, skillName, targetName: null);
        if (!string.IsNullOrEmpty(line))
        {
            WriteLine(line);
        }
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

    private List<string> BuildObjectiveReasonLabels(Character owner, CombatObjective previous, CombatObjective next)
    {
        var labels = new List<string>();
        CombatAiContextCollector collector = owner.GetComponent<CombatAiContextCollector>();
        if (collector == null) return labels;

        CombatAiContext context = collector.Collect(owner);
        CombatAiBrain brain = owner.GetComponent<CombatAiBrain>();
        CombatAiWeaponWeightsProfile weaponWeightsProfile = brain != null
            ? brain.WeaponWeightsProfile
            : CombatSceneContext.Instance != null
                ? CombatSceneContext.Instance.AiWeaponWeightsProfile
                : null;

        CombatAiDebugSnapshot snapshot = CombatAiPlanner.BuildDebugSnapshot(
            context,
            owner.PersonalityProfile,
            weaponWeightsProfile,
            focusEnemy: null,
            focusCommitmentRemainingSeconds: 0f,
            previousObjective: previous);
        if (snapshot == null) return labels;

        for (int i = 0; i < snapshot.ObjectiveEntries.Count; i++)
        {
            CombatAiObjectiveScoreEntry entry = snapshot.ObjectiveEntries[i];
            if (entry.Objective != next) continue;

            CombatAiScoreBreakdown breakdown = entry.Breakdown;
            if (breakdown == null) break;

            for (int j = 0; j < breakdown.ReasonCodes.Count; j++)
            {
                labels.Add(CombatAiDebugLabels.Reason(breakdown.ReasonCodes[j]));
            }

            break;
        }

        return labels;
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

    private string ResolveWeatherLabel()
    {
        CombatMapSystem mapSystem = CombatSceneContext.Instance != null ? CombatSceneContext.Instance.MapSystem : null;
        mapSystem ??= FindAnyObjectByType<CombatMapSystem>();
        if (mapSystem == null) return string.Empty;
        return mapSystem.CurrentWeather.ToString();
    }

    private void ResolveDependencies()
    {
        _battleFlow ??= FindAnyObjectByType<CombatBattleFlow>();
        _magicStoneSystem ??= CombatMagicStoneSystemResolver.Resolve();
        if (_characterSystem == null)
        {
            CombatSceneContext context = CombatSceneContext.Instance;
            _characterSystem = context != null ? context.CharacterSystem : null;
            _characterSystem ??= FindAnyObjectByType<CombatCharacterSystem>();
        }
    }

    private static string GetLogDirectoryPath()
    {
        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
        return Path.Combine(projectRoot, LogDirectoryName.Replace('/', Path.DirectorySeparatorChar));
    }

    private void WriteLine(string line)
    {
        if (_writer == null || string.IsNullOrEmpty(line)) return;
        _writer.WriteLine(line);
        _writer.Flush();
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
