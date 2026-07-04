using UnityEngine;
using WarSimulation.Combat.Map;

public enum CombatBattleState
{
    WaitingToStart = 0,
    Running,
    Victory,
    Defeat,
}

public sealed class CombatBattleFlow : MonoBehaviour
{
    private static CombatBattleFlow s_instance;

    [SerializeField] private bool _ensureMagicStoneSystemOnAwake = true;
    [SerializeField] private CombatMagicStoneSystem _magicStoneSystem;
    [SerializeField] private CombatCharacterSystem _characterSystem;

    private CombatBattleState _state = CombatBattleState.WaitingToStart;

    public CombatBattleState State => _state;

    public static bool IsRunning => s_instance != null && s_instance._state == CombatBattleState.Running;
    public static bool AllowsCombatActions => s_instance == null || s_instance._state == CombatBattleState.Running;

    private void Awake()
    {
        s_instance = this;

        if (_ensureMagicStoneSystemOnAwake && _magicStoneSystem == null)
        {
            _magicStoneSystem = GetComponent<CombatMagicStoneSystem>();
            if (_magicStoneSystem == null)
            {
                _magicStoneSystem = gameObject.AddComponent<CombatMagicStoneSystem>();
            }
        }

        ResolveDependencies();
    }

    private void OnEnable()
    {
        SubscribeStoneEvents();
    }

    private void OnDisable()
    {
        UnsubscribeStoneEvents();
    }

    public void SetMagicStoneSystem(CombatMagicStoneSystem magicStoneSystem)
    {
        UnsubscribeStoneEvents();
        _magicStoneSystem = magicStoneSystem;
        SubscribeStoneEvents();
    }

    private void SubscribeStoneEvents()
    {
        ResolveDependencies();
        if (_magicStoneSystem != null)
        {
            _magicStoneSystem.MainStoneDestroyed -= OnMainStoneDestroyed;
            _magicStoneSystem.MainStoneDestroyed += OnMainStoneDestroyed;
        }
    }

    private void UnsubscribeStoneEvents()
    {
        if (_magicStoneSystem != null)
        {
            _magicStoneSystem.MainStoneDestroyed -= OnMainStoneDestroyed;
        }
    }

    private void OnDestroy()
    {
        if (s_instance == this)
        {
            s_instance = null;
        }
    }

    public void ResetBattle()
    {
        _state = CombatBattleState.WaitingToStart;
    }

    [ContextMenu("Start Battle On Current Map")]
    public void StartBattleOnCurrentMap()
    {
        if (!TryResolveCurrentMap(out MapData map))
        {
            return;
        }

        CancelTransientBattleArtifacts();
        _characterSystem?.SnapAllCharactersToNavMesh();
        _characterSystem?.CaptureCurrentPositionsAsInitialPositions();
        _characterSystem?.ResetCharactersForBattle();
        _magicStoneSystem?.Initialize(map);
        _state = CombatBattleState.Running;
    }

    [ContextMenu("Restart Battle On Current Map")]
    public void RestartBattleOnCurrentMap()
    {
        if (_state == CombatBattleState.WaitingToStart)
        {
            StartBattleOnCurrentMap();
            return;
        }

        if (!TryResolveCurrentMap(out MapData map))
        {
            return;
        }

        CancelTransientBattleArtifacts();
        _characterSystem?.ResetCharactersForBattle();
        _magicStoneSystem?.Initialize(map);
        _state = CombatBattleState.Running;
    }

    private static void CancelTransientBattleArtifacts()
    {
        RosaryHealingAreaZone[] zones = FindObjectsByType<RosaryHealingAreaZone>();
        for (int i = 0; i < zones.Length; i++)
        {
            zones[i]?.CancelImmediate();
        }
    }

    private void OnMainStoneDestroyed(FeatureType type)
    {
        if (_state != CombatBattleState.Running) return;

        if (type == FeatureType.EnemyMainStone)
        {
            EndBattle(CombatBattleState.Victory);
            return;
        }

        if (type == FeatureType.OwnMainStone)
        {
            EndBattle(CombatBattleState.Defeat);
        }
    }

    public void EndBattle(CombatBattleState outcome)
    {
        if (_state != CombatBattleState.Running) return;
        if (outcome == CombatBattleState.Running) return;

        _state = outcome;
        StopAllCharacters();
    }

    private void StopAllCharacters()
    {
        ResolveDependencies();
        if (_characterSystem == null) return;

        StopCharacterList(_characterSystem.AllyCharacters);
        StopCharacterList(_characterSystem.EnemyCharacters);
    }

    private static void StopCharacterList(System.Collections.Generic.List<Character> characters)
    {
        if (characters == null) return;

        for (int i = 0; i < characters.Count; i++)
        {
            Character character = characters[i];
            if (character == null) continue;

            CombatCharacterBody body = character.GetComponent<CombatCharacterBody>();
            body?.Stop();
        }
    }

    private void ResolveDependencies()
    {
        CombatSceneContext context = CombatSceneContext.Instance;

        if (_magicStoneSystem == null)
        {
            _magicStoneSystem = CombatMagicStoneSystemResolver.Resolve();
        }

        if (_characterSystem == null)
        {
            _characterSystem = context != null ? context.CharacterSystem : null;
            _characterSystem ??= FindAnyObjectByType<CombatCharacterSystem>();
        }
    }

    private bool TryResolveCurrentMap(out MapData map)
    {
        ResolveDependencies();

        CombatMapSystem mapSystem = CombatSceneContext.Instance?.MapSystem;
        map = mapSystem != null ? mapSystem.CurrentMap : null;
        if (map == null)
        {
            Debug.LogWarning($"[{nameof(CombatBattleFlow)}] CurrentMap is not set. Generate a map before starting the battle.");
            return false;
        }

        return true;
    }
}
