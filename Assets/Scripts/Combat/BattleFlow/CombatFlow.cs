using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class CombatFlow : MonoBehaviour
{
    [SerializeField] private CombatCharacterSystem _characterSystem;
    [SerializeField] private CombatBattleFlow _battleFlow;
    [SerializeField] private CombatCharacterSelection _characterSelection;
    [SerializeField] private GameObject _characterSelectionPanel;
    [SerializeField] private List<GameObject> _battleUiObjects = new();
    [SerializeField] private GameObject _resultPanel;
    [SerializeField] private TMP_Text _resultTitle;
    [SerializeField] private Button _backToSelectionButton;

    private readonly List<Character> _allyCandidates = new();
    private readonly List<Character> _enemies = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void ActivateCombatCanvases()
    {
        CombatFlow[] flows = Resources.FindObjectsOfTypeAll<CombatFlow>();
        for (int i = 0; i < flows.Length; i++)
        {
            CombatFlow flow = flows[i];
            if (flow == null || !flow.gameObject.scene.IsValid()) continue;

            Canvas canvas = flow.GetComponentInParent<Canvas>(includeInactive: true);
            if (canvas == null) continue;

            canvas.gameObject.SetActive(true);
            canvas.enabled = true;
            flow.gameObject.SetActive(true);
        }
    }

    private void Start()
    {
        ResolveDependencies();
        if (_characterSystem == null || _battleFlow == null || _characterSelection == null)
        {
            Debug.LogError($"[{nameof(CombatFlow)}] Required references are not configured.", this);
            enabled = false;
            return;
        }

        CopyCharacters(_characterSystem.AllyCharacters, _allyCandidates);
        CopyCharacters(_characterSystem.EnemyCharacters, _enemies);
        _battleFlow.BattleEnded += ShowResult;
        _backToSelectionButton?.onClick.AddListener(ShowSelection);
        _characterSelection.Initialize(_allyCandidates, _enemies, StartBattle);
        ShowSelection();
    }

    private void OnDestroy()
    {
        if (_battleFlow != null)
        {
            _battleFlow.BattleEnded -= ShowResult;
        }

        _backToSelectionButton?.onClick.RemoveListener(ShowSelection);
    }

    private void StartBattle(
        IReadOnlyList<CombatParticipantSetup> selectedAllies,
        IReadOnlyList<CombatParticipantSetup> selectedEnemies)
    {
        _characterSystem.SetParticipants(selectedAllies, selectedEnemies);
        SetVisible(_characterSelectionPanel, false);
        SetBattleUiVisible(true);
        SetVisible(_resultPanel, false);
        _battleFlow.StartBattleOnCurrentMap();

        if (_battleFlow.State != CombatBattleState.Running)
        {
            ShowSelection();
        }
    }

    private void ShowResult(CombatBattleState outcome)
    {
        if (_resultTitle != null)
        {
            _resultTitle.text = outcome == CombatBattleState.Victory ? "勝利" : "敗北";
        }

        SetVisible(_characterSelectionPanel, false);
        SetBattleUiVisible(false);
        SetVisible(_resultPanel, true);
    }

    private void ShowSelection()
    {
        _battleFlow.ResetBattle();
        _characterSystem.SetParticipants(_allyCandidates, _enemies);
        _characterSelection.ResetSelection();
        SetVisible(_characterSelectionPanel, true);
        SetBattleUiVisible(false);
        SetVisible(_resultPanel, false);
    }

    private void ResolveDependencies()
    {
        CombatSceneContext context = CombatSceneContext.Instance;
        _characterSystem ??= context != null ? context.CharacterSystem : null;
        _battleFlow ??= context != null ? context.BattleFlow : null;
        _characterSystem ??= FindAnyObjectByType<CombatCharacterSystem>();
        _battleFlow ??= FindAnyObjectByType<CombatBattleFlow>();
    }

    private static void CopyCharacters(List<Character> source, List<Character> destination)
    {
        destination.Clear();
        if (source == null) return;

        for (int i = 0; i < source.Count; i++)
        {
            Character character = source[i];
            if (character != null && !destination.Contains(character))
            {
                destination.Add(character);
            }
        }
    }

    private static void SetVisible(GameObject target, bool visible)
    {
        if (target != null)
        {
            target.SetActive(visible);
        }
    }

    private void SetBattleUiVisible(bool visible)
    {
        for (int i = 0; i < _battleUiObjects.Count; i++)
        {
            SetVisible(_battleUiObjects[i], visible);
        }
    }
}
