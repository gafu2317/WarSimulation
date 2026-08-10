using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class CombatFlow : MonoBehaviour
{
    private static readonly float[] BattleSpeedOptions = { 1f, 2f, 4f, 8f };
    private static readonly Vector3 NormalCameraPosition = new Vector3(30f, 20f, -10f);
    private static readonly Vector3 ReversedCameraPosition = new Vector3(30f, 20f, 70f);

    [SerializeField] private CombatCharacterSystem _characterSystem;
    [SerializeField] private CombatBattleFlow _battleFlow;
    [SerializeField] private CombatCharacterSelection _characterSelection;
    [SerializeField] private CombatMapSystem _mapSystem;
    [SerializeField] private GameObject _characterSelectionPanel;
    [SerializeField] private List<GameObject> _battleUiObjects = new();
    [SerializeField] private GameObject _resultPanel;
    [SerializeField] private TMP_Text _resultTitle;
    [SerializeField] private Button _backToSelectionButton;

    private readonly List<Character> _allyCandidates = new();
    private readonly List<Character> _enemies = new();
    private CombatBattleHudView _battleHudView;
    private float _selectedBattleSpeed = 1f;
    private bool _isPaused;

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
        CombatPlaytestDebugSettings.ApplyToScene();
        EnsureBattleControls();
        _battleFlow.BattleEnded += ShowResult;
        _characterSelection.StonePositionReversedChanged += OnStonePositionReversedChanged;
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

        if (_characterSelection != null)
        {
            _characterSelection.StonePositionReversedChanged -= OnStonePositionReversedChanged;
        }

        _backToSelectionButton?.onClick.RemoveListener(ShowSelection);
        ClearBattleControlListeners();
        RestoreNormalSpeed();
    }

    private void StartBattle(
        IReadOnlyList<CombatParticipantSetup> selectedAllies,
        IReadOnlyList<CombatParticipantSetup> selectedEnemies)
    {
        if (!TryApplyStonePositionReversed(_characterSelection.IsStonePositionReversed))
        {
            ShowSelection();
            return;
        }
        ApplyCombatCamera(_characterSelection.IsStonePositionReversed);

        _characterSystem.SetParticipants(selectedAllies, selectedEnemies);
        SetVisible(_characterSelectionPanel, false);
        SetBattleUiVisible(true);
        SetVisible(_resultPanel, false);
        SetBattleControlsVisible(true);
        SetPauseMenuVisible(false);
        _battleFlow.StartBattleOnCurrentMap();

        if (_battleFlow.State != CombatBattleState.Running)
        {
            ShowSelection();
            return;
        }

        ApplySelectedBattleSpeed();
    }

    private void ShowResult(CombatBattleState outcome)
    {
        if (_resultTitle != null)
        {
            _resultTitle.text = outcome == CombatBattleState.Victory ? "勝利" : "敗北";
        }

        RestoreNormalSpeed();
        SetPauseMenuVisible(false);
        SetVisible(_characterSelectionPanel, false);
        SetBattleUiVisible(false);
        SetBattleControlsVisible(false);
        SetVisible(_resultPanel, true);
    }

    private void ShowSelection()
    {
        RestoreNormalSpeed();
        SetPauseMenuVisible(false);
        _battleFlow.AbortBattle();
        _characterSystem.SetParticipants(_allyCandidates, _enemies);
        SetVisible(_characterSelectionPanel, true);
        SetBattleUiVisible(false);
        SetBattleControlsVisible(false);
        SetVisible(_resultPanel, false);
    }

    private void OnStonePositionReversedChanged(bool reversed)
    {
        if (TryApplyStonePositionReversed(reversed)) return;

        _characterSelection.SetStonePositionReversedState(!reversed);
    }

    private void ApplyCombatCamera(bool reversed)
    {
        Camera camera = Camera.main;
        if (camera == null) return;

        camera.transform.position = reversed ? ReversedCameraPosition : NormalCameraPosition;
        camera.transform.rotation = Quaternion.Euler(40f, reversed ? 180f : 0f, 0f);
    }

    private bool TryApplyStonePositionReversed(bool reversed)
    {
        ResolveDependencies();
        if (_mapSystem == null)
        {
            Debug.LogWarning($"[{nameof(CombatFlow)}] Cannot change stone positions because CombatMapSystem is missing.", this);
            return false;
        }

        return _mapSystem.TrySetStonePositionsReversed(reversed);
    }

    private void EnsureBattleControls()
    {
        if (_battleHudView == null)
        {
            CombatPartyStatusPanel statusPanel = FindAnyObjectByType<CombatPartyStatusPanel>(FindObjectsInactive.Include);
            if (statusPanel != null)
            {
                _battleHudView = statusPanel.GetComponent<CombatBattleHudView>();
                _battleHudView ??= statusPanel.gameObject.AddComponent<CombatBattleHudView>();
            }
        }

        if (_battleHudView == null)
        {
            Debug.LogWarning($"[{nameof(CombatFlow)}] Kuen battle HUD was not found.", this);
            return;
        }

        _battleHudView.EnsureBuilt();
        _battleHudView.MenuRequested -= OpenPauseMenu;
        _battleHudView.MenuRequested += OpenPauseMenu;
        _battleHudView.ResumeRequested -= ResumeBattle;
        _battleHudView.ResumeRequested += ResumeBattle;
        _battleHudView.ReturnToSelectionRequested -= ReturnToSelection;
        _battleHudView.ReturnToSelectionRequested += ReturnToSelection;
        _battleHudView.SpeedRequested -= CycleBattleSpeed;
        _battleHudView.SpeedRequested += CycleBattleSpeed;
        RefreshBattleControlLabels();
        SetBattleControlsVisible(false);
    }

    private void SetBattleSpeed(float speed)
    {
        _selectedBattleSpeed = speed;
        _isPaused = false;
        ApplyEffectiveTimeScale();
        RefreshBattleControlLabels();
    }

    private void CycleBattleSpeed()
    {
        if (_battleFlow == null || _battleFlow.State != CombatBattleState.Running || _isPaused) return;

        int currentIndex = System.Array.IndexOf(BattleSpeedOptions, _selectedBattleSpeed);
        int nextIndex = currentIndex >= 0 ? (currentIndex + 1) % BattleSpeedOptions.Length : 0;
        SetBattleSpeed(BattleSpeedOptions[nextIndex]);
    }

    private void OpenPauseMenu()
    {
        if (_battleFlow == null || _battleFlow.State != CombatBattleState.Running || _isPaused) return;

        _isPaused = true;
        ApplyEffectiveTimeScale();
        SetPauseMenuVisible(true);
    }

    private void ResumeBattle()
    {
        if (!_isPaused) return;

        _isPaused = false;
        SetPauseMenuVisible(false);
        ApplyEffectiveTimeScale();
    }

    private void ReturnToSelection()
    {
        SetPauseMenuVisible(false);
        ShowSelection();
    }

    private void ApplySelectedBattleSpeed()
    {
        _isPaused = false;
        ApplyEffectiveTimeScale();
        RefreshBattleControlLabels();
    }

    private void ApplyEffectiveTimeScale()
    {
        if (_battleFlow != null && _battleFlow.State == CombatBattleState.Running)
        {
            Time.timeScale = _isPaused ? 0f : _selectedBattleSpeed;
            return;
        }

        Time.timeScale = 1f;
    }

    private void RestoreNormalSpeed()
    {
        _isPaused = false;
        Time.timeScale = 1f;
        RefreshBattleControlLabels();
    }

    private void RefreshBattleControlLabels()
    {
        _battleHudView?.SetSpeedLabel(_selectedBattleSpeed);
    }

    private void SetBattleControlsVisible(bool visible)
    {
        _battleHudView?.SetControlsVisible(visible);
    }

    private void SetPauseMenuVisible(bool visible)
    {
        _battleHudView?.SetMenuVisible(visible);
    }

    private void ClearBattleControlListeners()
    {
        if (_battleHudView == null)
        {
            return;
        }

        _battleHudView.MenuRequested -= OpenPauseMenu;
        _battleHudView.ResumeRequested -= ResumeBattle;
        _battleHudView.ReturnToSelectionRequested -= ReturnToSelection;
        _battleHudView.SpeedRequested -= CycleBattleSpeed;
    }

    private void ResolveDependencies()
    {
        CombatSceneContext context = CombatSceneContext.Instance;
        _characterSystem ??= context != null ? context.CharacterSystem : null;
        _battleFlow ??= context != null ? context.BattleFlow : null;
        _mapSystem ??= context != null ? context.MapSystem : null;
        _characterSystem ??= FindAnyObjectByType<CombatCharacterSystem>();
        _battleFlow ??= FindAnyObjectByType<CombatBattleFlow>();
        _mapSystem ??= FindAnyObjectByType<CombatMapSystem>();
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
