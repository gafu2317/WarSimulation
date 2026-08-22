using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WarSimulation.Combat.Map;

public sealed class CombatFlow : MonoBehaviour
{
    private static readonly float[] BattleSpeedOptions = { 1f, 2f, 4f, 8f };
    private static readonly Vector3 LowSideCameraPosition = new Vector3(30f, 20f, -10f);
    private static readonly Vector3 HighSideCameraPosition = new Vector3(30f, 20f, 70f);

    [SerializeField] private CombatCharacterSystem _characterSystem;
    [SerializeField] private CombatBattleFlow _battleFlow;
    [SerializeField] private CombatCharacterSelection _characterSelection;
    [SerializeField] private CombatMapSelectionView _mapSelectionView;
    [SerializeField] private CombatMapSystem _mapSystem;
    [SerializeField] private GameObject _characterSelectionPanel;
    [SerializeField] private List<GameObject> _battleUiObjects = new();
    [SerializeField] private GameObject _resultPanel;
    [SerializeField] private TMP_Text _resultTitle;
    [SerializeField] private Button _backToSelectionButton;

    private readonly List<Character> _allyCandidates = new();
    private readonly List<Character> _enemies = new();
    private CombatBattleResultRecorder _battleResultRecorder;
    private CombatBattleResultView _battleResultView;
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
        if (_characterSystem == null || _battleFlow == null || _characterSelection == null ||
            _mapSelectionView == null || _mapSystem == null)
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
        _mapSelectionView.SelectionChanged += OnMapSelectionChanged;
        _backToSelectionButton?.onClick.AddListener(ShowSelection);
        _characterSelection.Initialize(_allyCandidates, _enemies, StartBattle);
        _mapSelectionView.Initialize(_mapSystem.AuthoredMap, _characterSelection.IsStonePositionReversed);
        RefreshStartAvailability();
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

        if (_mapSelectionView != null)
        {
            _mapSelectionView.SelectionChanged -= OnMapSelectionChanged;
        }

        _battleResultRecorder?.Clear();
        _battleResultView?.Clear();
        _backToSelectionButton?.onClick.RemoveListener(ShowSelection);
        ClearBattleControlListeners();
        RestoreNormalSpeed();
    }

    private void StartBattle(
        IReadOnlyList<CombatParticipantSetup> selectedAllies,
        IReadOnlyList<CombatParticipantSetup> selectedEnemies)
    {
        CombatMapAvailability availability = CombatMapAvailability.Evaluate(
            _mapSelectionView.SelectedMap,
            _characterSelection.IsStonePositionReversed);
        if (!availability.CanStartBattle)
        {
            _mapSelectionView.ShowFailure(availability.Message);
            RefreshStartAvailability();
            return;
        }

        _mapSelectionView.ClearFailure();
        _mapSelectionView.SetInteractionEnabled(false);
        _characterSelection.SetExternalStartAllowed(false);
        if (!_mapSystem.TryApplyBakedAuthoredMap(
                _mapSelectionView.SelectedMap,
                out _,
                out CombatMapApplyFailure mapFailure))
        {
            ShowStartFailure(MapFailureMessage(mapFailure));
            return;
        }

        if (!TryApplyStonePositionReversed(_characterSelection.IsStonePositionReversed))
        {
            ShowStartFailure("魔石位置の反転を適用できませんでした");
            return;
        }
        ApplyCombatCamera(_characterSelection != null && _characterSelection.IsStonePositionReversed);

        _characterSystem.SetParticipants(selectedAllies, selectedEnemies);
        _battleResultRecorder?.Begin(
            _characterSystem.AllyCharacters,
            _characterSystem.EnemyCharacters);
        _battleFlow.StartBattleOnCurrentMap();

        if (_battleFlow.State != CombatBattleState.Running)
        {
            ShowStartFailure("戦闘を開始できませんでした");
            return;
        }

        SetVisible(_characterSelectionPanel, false);
        SetBattleUiVisible(true);
        SetVisible(_resultPanel, false);
        SetBattleControlsVisible(true);
        SetPauseMenuVisible(false);
        ApplySelectedBattleSpeed();
    }

    private void ShowResult(CombatBattleState outcome)
    {
        CombatBattleResult result = _battleResultRecorder?.Complete(outcome);
        _battleResultView?.Show(result);

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
        ShowSelectionInternal(clearMapFailure: true);
    }

    private void ShowSelectionInternal(bool clearMapFailure)
    {
        RestoreNormalSpeed();
        SetPauseMenuVisible(false);
        _battleResultRecorder?.Clear();
        _battleResultView?.Clear();
        _battleFlow.AbortBattle();
        _characterSystem.SetParticipants(_allyCandidates, _enemies);
        if (_mapSelectionView != null)
        {
            _mapSelectionView.SetInteractionEnabled(true);
            if (clearMapFailure) _mapSelectionView.ClearFailure();
        }
        bool reversed = _characterSelection != null && _characterSelection.IsStonePositionReversed;
        if (TryApplySelectionStonePositionReversed(reversed))
        {
            ApplyCombatCamera(reversed);
        }
        RefreshStartAvailability();
        SetVisible(_characterSelectionPanel, true);
        SetBattleUiVisible(false);
        SetBattleControlsVisible(false);
        SetVisible(_resultPanel, false);
    }

    private void OnStonePositionReversedChanged(bool reversed)
    {
        _mapSelectionView.SetStonePositionsReversed(reversed);
        if (TryApplySelectionStonePositionReversed(reversed))
        {
            ApplyCombatCamera(reversed);
        }
        RefreshStartAvailability();
    }

    private void OnMapSelectionChanged()
    {
        RefreshStartAvailability();
    }

    private void RefreshStartAvailability()
    {
        if (_characterSelection == null || _mapSelectionView == null) return;
        _characterSelection.SetExternalStartAllowed(_mapSelectionView.CanStartBattle);
    }

    private void ShowStartFailure(string message)
    {
        ShowSelectionInternal(clearMapFailure: false);
        _mapSelectionView.ShowFailure(message);
    }

    private static string MapFailureMessage(CombatMapApplyFailure failure) => failure switch
    {
        CombatMapApplyFailure.MissingDefinition => "戦闘マップが選択されていません",
        CombatMapApplyFailure.MissingSharedConfig => "共通マップ設定がありません",
        CombatMapApplyFailure.MissingBakedMapData => "ベイク済みMapDataを読み込めません",
        CombatMapApplyFailure.MissingBakedNavMesh => "ベイク済みNavMeshを読み込めません",
        CombatMapApplyFailure.MissingMapSceneHost => "MapSceneHostが設定されていません",
        CombatMapApplyFailure.RuntimeMapCreationFailed => "ベイク済みMapDataの復元に失敗しました",
        CombatMapApplyFailure.RenderOrNavMeshLoadFailed => "マップ描画またはNavMesh読込に失敗しました",
        _ => "戦闘マップを適用できませんでした",
    };

    private void ApplyCombatCamera(bool reversed)
    {
        Camera camera = Camera.main;
        if (camera == null) return;

        camera.transform.position = reversed ? LowSideCameraPosition : HighSideCameraPosition;
        camera.transform.rotation = Quaternion.Euler(40f, reversed ? 0f : 180f, 0f);
        camera.GetComponent<EditorStyleCameraController>()?.SyncStateFromTransform();
    }

    private bool TryApplySelectionStonePositionReversed(bool reversed)
    {
        ResolveDependencies();
        if (_mapSystem == null || _mapSystem.CurrentMap == null || _mapSelectionView == null) return false;
        if (_mapSelectionView.SelectedMap != _mapSystem.AuthoredMap) return false;
        return _mapSystem.TrySetStonePositionsReversed(reversed);
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
            _battleHudView = FindKuenBattleHud();
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

    private static CombatBattleHudView FindKuenBattleHud()
    {
        CombatBattleHudView[] huds = FindObjectsByType<CombatBattleHudView>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < huds.Length; i++)
        {
            CombatBattleHudView hud = huds[i];
            if (hud != null && hud.transform.Find("ControlPanel") != null)
            {
                return hud;
            }
        }

        return null;
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
        _battleResultRecorder ??= GetComponent<CombatBattleResultRecorder>();
        _battleResultRecorder ??= gameObject.AddComponent<CombatBattleResultRecorder>();
        if (_resultPanel != null)
        {
            _battleResultView ??= _resultPanel.GetComponent<CombatBattleResultView>();
            _battleResultView ??= _resultPanel.AddComponent<CombatBattleResultView>();
            _battleResultView.EnsureBuilt();
        }
        if (_mapSelectionView == null && _characterSelectionPanel != null)
            _mapSelectionView = _characterSelectionPanel.GetComponentInChildren<CombatMapSelectionView>(true);
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
