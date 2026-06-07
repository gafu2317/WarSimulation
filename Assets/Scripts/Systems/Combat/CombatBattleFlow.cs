using UnityEngine;
using WarSimulation.Combat.Map;

public enum CombatBattleState
{
    Running = 0,
    Victory,
    Defeat,
}

public sealed class CombatBattleFlow : MonoBehaviour
{
    private static CombatBattleFlow s_instance;

    [SerializeField] private bool _ensureMagicStoneSystemOnAwake = true;
    [SerializeField] private CombatMagicStoneSystem _magicStoneSystem;
    [SerializeField] private CombatCharacterSystem _characterSystem;

    private CombatBattleState _state = CombatBattleState.Running;

    public CombatBattleState State => _state;

    public static bool IsRunning => s_instance == null || s_instance._state == CombatBattleState.Running;

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
        _state = CombatBattleState.Running;
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
}
