using System.Collections.Generic;
using System.Collections;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using WarSimulation.Combat.Map;

public sealed class CombatFlow : MonoBehaviour
{
    private static readonly float[] BattleSpeedOptions = { 1f, 2f, 4f };
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
    private bool _mapModalOpen;
    private CombatPlayerMagicSystem _playerMagicSystem;
    private sealed class PlayerMagicCast
    {
        public Coroutine Routine;
        public long VisualId;
    }
    private readonly List<PlayerMagicCast> _playerMagicCasts = new();
    private bool _rockNavigationUpdating;
    private EditorStyleCameraController _placementCamera;
    private bool _placementCameraWasEnabled;
    public CombatRockPlacement? ConfirmedRockPlacement => _battleHudView != null
        ? _battleHudView.ConfirmedRockPlacement : null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneActivation()
    {
        SceneManager.sceneLoaded -= ActivateCombatCanvases;
        SceneManager.sceneLoaded += ActivateCombatCanvases;
    }

    private static void ActivateCombatCanvases(Scene scene, LoadSceneMode mode)
    {
        CombatFlow[] flows = Resources.FindObjectsOfTypeAll<CombatFlow>();
        for (int i = 0; i < flows.Length; i++)
        {
            CombatFlow flow = flows[i];
            if (flow == null || flow.gameObject.scene != scene) continue;

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
        _characterSystem.CandidatesReady += OnCandidatesReady;
        _battleFlow.BattleEnded += ShowResult;
        _characterSelection.StonePositionReversedChanged += OnStonePositionReversedChanged;
        _mapSelectionView.SelectionChanged += OnMapSelectionChanged;
        _backToSelectionButton?.onClick.AddListener(ShowSelection);
        _characterSelection.Initialize(_allyCandidates, _enemies, StartBattle);
        _mapSelectionView.Initialize(_mapSystem.AuthoredMap, _characterSelection.IsStonePositionReversed);
        BeginMapPreparation();
        ShowSelection();
    }

    private void OnDisable()
    {
        ResetRockPlacement();
    }

    private void OnDestroy()
    {
        if (_battleFlow != null)
        {
            _battleFlow.BattleEnded -= ShowResult;
        }
        if (_characterSystem != null)
            _characterSystem.CandidatesReady -= OnCandidatesReady;

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
        ResetRockPlacement();
        CombatMapAvailability availability = CombatMapAvailability.Evaluate(
            _mapSelectionView.SelectedMap,
            _characterSelection.IsStonePositionReversed);
        if (!availability.CanStartBattle)
        {
            _mapSelectionView.ShowFailure(availability.Message);
            RefreshStartAvailability();
            return;
        }

        if (!_mapSystem.IsMapReady(_mapSelectionView.SelectedMap))
        {
            _mapSelectionView.ShowPreparation("マップ準備中です");
            RefreshStartAvailability();
            BeginMapPreparation();
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

        if (!_mapSystem.ResetRuntimeMapState())
        {
            ShowStartFailure("戦闘マップの状態を初期化できませんでした");
            return;
        }

        if (!TryApplyStonePositionReversed(_characterSelection.IsStonePositionReversed))
        {
            ShowStartFailure("魔石位置の反転を適用できませんでした");
            return;
        }
        ApplyCombatCamera();

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
        _battleHudView?.SetDebugUiVisible(CombatPlaytestDebugSettings.UseDebugBattleUi);
        SetVisible(_resultPanel, false);
        SetBattleControlsVisible(true);
        SetPauseMenuVisible(false);
        ApplySelectedBattleSpeed();
    }

    private void ShowResult(CombatBattleState outcome)
    {
        ResetRockPlacement();
        CombatBattleResult result = _battleResultRecorder?.Complete(outcome);
        _battleResultView?.Show(result);

        if (_resultTitle != null)
        {
            _resultTitle.textWrappingMode = TextWrappingModes.NoWrap;
            _resultTitle.overflowMode = TextOverflowModes.Overflow;
            string outcomeText = outcome == CombatBattleState.Victory ? "勝利" : "敗北";
            _resultTitle.text = result == null
                ? outcomeText
                : string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}　戦闘時間 {1}",
                    outcomeText,
                    FormatBattleDuration(result.DurationSeconds));
        }

        RestoreNormalSpeed();
        SetPauseMenuVisible(false);
        SetVisible(_characterSelectionPanel, false);
        SetBattleUiVisible(false);
        SetBattleControlsVisible(false);
        SetVisible(_resultPanel, true);
    }

    private static string FormatBattleDuration(float durationSeconds)
    {
        int totalSeconds = Mathf.Max(0, Mathf.FloorToInt(durationSeconds));
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;
        return string.Format(
            CultureInfo.InvariantCulture,
            "{0}分{1:00}秒",
            minutes,
            seconds);
    }

    private void ShowSelection()
    {
        ShowSelectionInternal(clearMapFailure: true);
    }

    private void ShowSelectionInternal(bool clearMapFailure)
    {
        ResetRockPlacement();
        RestoreNormalSpeed();
        SetPauseMenuVisible(false);
        _battleResultRecorder?.Clear();
        _battleResultView?.Clear();
        if (_mapSystem != null && _mapSystem.CurrentMap != null)
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
            ApplyCombatCamera();
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
            ApplyCombatCamera();
        }
        RefreshStartAvailability();
    }

    private void OnMapSelectionChanged()
    {
        ResetRockPlacement();
        _characterSelection.SetStonePositionReversedState(false);
        _mapSelectionView.SetStonePositionsReversed(false);
        BeginMapPreparation();
    }

    private void OnCandidatesReady()
    {
        CopyCharacters(_characterSystem.AllyCharacters, _allyCandidates);
        CopyCharacters(_characterSystem.EnemyCharacters, _enemies);
        _characterSelection.Initialize(_allyCandidates, _enemies, StartBattle);
        RefreshStartAvailability();
    }

    private void RefreshStartAvailability()
    {
        if (_characterSelection == null || _mapSelectionView == null) return;
        bool ready = _mapSystem != null && _mapSystem.IsMapReady(_mapSelectionView.SelectedMap);
        _characterSelection.SetExternalStartAllowed(_mapSelectionView.CanStartBattle && ready);
    }

    private void BeginMapPreparation()
    {
        if (_mapSystem == null || _mapSelectionView == null) return;
        AuthoredMapDefinition selected = _mapSelectionView.SelectedMap;
        _mapSelectionView.ClearFailure();
        if (selected == null)
        {
            _mapSelectionView.ClearPreparation();
            RefreshStartAvailability();
            return;
        }

        _mapSelectionView.ShowPreparation("マップ準備中です");
        RefreshStartAvailability();
        StartCoroutine(PrepareSelectedMap(selected));
    }

    private IEnumerator PrepareSelectedMap(AuthoredMapDefinition selected)
    {
        yield return _mapSystem.PrepareMapAsync(selected);
        if (_mapSelectionView == null || _mapSelectionView.SelectedMap != selected) yield break;

        _mapSelectionView.ClearPreparation();
        if (!_mapSystem.IsMapReady(selected))
            _mapSelectionView.ShowFailure(MapFailureMessage(_mapSystem.PreparationFailure));
        else if (_mapSystem.TrySetStonePositionsReversed(false))
            ApplyCombatCamera();
        RefreshStartAvailability();
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
        CombatMapApplyFailure.MissingBakedRenderData => "シーンのベイク済み3Dがマップと一致しません。再ベイクしてください",
        CombatMapApplyFailure.MissingMapSceneHost => "MapSceneHostが設定されていません",
        CombatMapApplyFailure.RuntimeMapCreationFailed => "ベイク済みMapDataの復元に失敗しました",
        CombatMapApplyFailure.RenderOrNavMeshLoadFailed => "マップ描画またはNavMesh読込に失敗しました",
        CombatMapApplyFailure.MapNotReady => "マップ準備中です",
        CombatMapApplyFailure.MissingBakedRuntimeScene => "ランタイム用マップSceneの再ベイクが必要です",
        CombatMapApplyFailure.BakedRuntimeSceneLoadFailed => "ランタイム用マップSceneを読み込めません",
        _ => "戦闘マップを適用できませんでした",
    };

    private void ApplyCombatCamera()
    {
        Camera camera = Camera.main;
        if (camera == null || _mapSystem == null ||
            !_mapSystem.TryGetMainStonePosition(CombatTeam.Ally, out Vector3 allyStone) ||
            !_mapSystem.TryGetMainStonePosition(CombatTeam.Enemy, out Vector3 enemyStone))
            return;

        Transform origin = _mapSystem.MapOrigin;
        if (origin != null)
        {
            allyStone = origin.InverseTransformPoint(allyStone);
            enemyStone = origin.InverseTransformPoint(enemyStone);
        }

        bool allyOnLowSide = IsAllyOnLowSide(allyStone, enemyStone);
        camera.transform.position = allyOnLowSide ? LowSideCameraPosition : HighSideCameraPosition;
        camera.transform.rotation = Quaternion.Euler(40f, allyOnLowSide ? 0f : 180f, 0f);
        camera.GetComponent<EditorStyleCameraController>()?.SyncStateFromTransform();
    }

    private static bool IsAllyOnLowSide(Vector3 allyStone, Vector3 enemyStone) =>
        allyStone.z < enemyStone.z;

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
        if (_mapSystem != null)
        {
            _mapSystem.CurrentMapChanged -= ResetRockPlacement;
            _mapSystem.CurrentMapChanged += ResetRockPlacement;
        }
        _battleHudView.RockPlacementConfirmed -= PlaceRock;
        _battleHudView.RockRemovalRequested -= RemoveRock;
        _battleHudView.RockPlacementRequested -= OpenRockPlacement;
        _battleHudView.RockPlacementConfirmed += PlaceRock;
        _battleHudView.RockRemovalRequested += RemoveRock;
        _battleHudView.RockPlacementRequested += OpenRockPlacement;
        _battleHudView.RockPlacementClosed -= CloseMapModal;
        _battleHudView.RockPlacementClosed += CloseMapModal;
        _battleHudView.PlayerMagicRequested -= OpenPlayerMagic;
        _battleHudView.PlayerMagicRequested += OpenPlayerMagic;
        _battleHudView.PlayerMagicClosed -= CloseMapModal;
        _battleHudView.PlayerMagicClosed += CloseMapModal;
        _battleHudView.PlayerMagicCastRequested -= CastPlayerMagic;
        _battleHudView.PlayerMagicCastRequested += CastPlayerMagic;
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
            FindObjectsInactive.Include);
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

    private bool BeginMapModal()
    {
        if (_mapModalOpen || _rockNavigationUpdating || _battleFlow == null || _battleFlow.State != CombatBattleState.Running ||
            _mapSystem == null || _mapSystem.CurrentMap == null) return false;
        _mapModalOpen = true;
        ApplyEffectiveTimeScale();
        _placementCamera = Camera.main != null ? Camera.main.GetComponent<EditorStyleCameraController>() : null;
        _placementCameraWasEnabled = _placementCamera != null && _placementCamera.enabled;
        if (_placementCamera != null) _placementCamera.enabled = false;
        return true;
    }

    private void OpenRockPlacement(CombatRockShape shape)
    {
        if (BeginMapModal() && !_battleHudView.OpenRockPlacement(shape, _mapSystem, _characterSystem)) CloseMapModal();
    }

    private void OpenPlayerMagic(CombatPlayerMagicKind kind)
    {
        if (BeginMapModal() && !_battleHudView.OpenPlayerMagic(kind, _mapSystem, _characterSystem)) CloseMapModal();
    }

    private void CastPlayerMagic(CombatPlayerMagicRequest request)
    {
        if (_battleFlow == null || _battleFlow.State != CombatBattleState.Running || !request.IsValid) return;
        _battleHudView.ShowPlayerMagicCutIn(request.SkillId);
        float seconds = CombatPlayerMagicSettings.CastSeconds(request.SkillId);
        if (seconds <= 0) { ApplyPlayerMagic(request); return; }
        var cast = new PlayerMagicCast { VisualId = SkillVfxRuntimeBridge.BeginPlayerMagicCast(request) };
        _playerMagicCasts.Add(cast);
        cast.Routine = StartCoroutine(CompletePlayerMagicCast(cast, request, seconds));
    }

    private IEnumerator CompletePlayerMagicCast(PlayerMagicCast cast, CombatPlayerMagicRequest request, float seconds)
    {
        yield return new WaitForSeconds(seconds);
        _playerMagicCasts.Remove(cast);
        SkillVfxRuntimeBridge.CancelPlayerMagicCast(cast.VisualId);
        if (_battleFlow != null && _battleFlow.State == CombatBattleState.Running) ApplyPlayerMagic(request);
    }

    private void ApplyPlayerMagic(CombatPlayerMagicRequest request)
    {
        _playerMagicSystem ??= new CombatPlayerMagicSystem(_characterSystem);
        CombatAudioPlayer.Instance?.PlayPlayerMagicSound(request.SkillId);
        List<Character> targets = _playerMagicSystem.Execute(request);
        SkillVfxRuntimeBridge.PlayPlayerMagic(request, targets);
    }

    private void PlaceRock(CombatRockPlacement placement, System.Action<bool, string> completed)
    {
        _rockNavigationUpdating = true;
        ApplyEffectiveTimeScale();
        if (!_mapSystem.TryPlaceRuntimeRock(placement, _characterSystem, (success, error) =>
            {
                _rockNavigationUpdating = false;
                completed(success, error);
                ApplyEffectiveTimeScale();
            }, out string failure))
        {
            _rockNavigationUpdating = false;
            completed(false, failure);
            ApplyEffectiveTimeScale();
        }
    }

    private void RemoveRock() => _mapSystem?.RemoveRuntimeRock();

    private void CloseMapModal()
    {
        if (!_mapModalOpen) return;
        _mapModalOpen = false;
        if (_placementCamera != null) _placementCamera.enabled = _placementCameraWasEnabled;
        _placementCamera = null;
        ApplyEffectiveTimeScale();
    }

    private void ResetRockPlacement()
    {
        foreach (PlayerMagicCast cast in _playerMagicCasts)
        {
            if (cast.Routine != null) StopCoroutine(cast.Routine);
            SkillVfxRuntimeBridge.CancelPlayerMagicCast(cast.VisualId);
        }
        _playerMagicCasts.Clear();
        _mapSystem?.RemoveRuntimeRock();
        _playerMagicSystem?.Clear();
        _rockNavigationUpdating = false;
        _battleHudView?.ResetRockPlacement();
        CloseMapModal();
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
        if (_battleFlow == null || _battleFlow.State != CombatBattleState.Running || _isPaused || _mapModalOpen) return;

        int currentIndex = System.Array.IndexOf(BattleSpeedOptions, _selectedBattleSpeed);
        int nextIndex = currentIndex >= 0 ? (currentIndex + 1) % BattleSpeedOptions.Length : 0;
        SetBattleSpeed(BattleSpeedOptions[nextIndex]);
    }

    private void OpenPauseMenu()
    {
        if (_battleFlow == null || _battleFlow.State != CombatBattleState.Running || _isPaused || _mapModalOpen) return;

        _isPaused = true;
        ApplyEffectiveTimeScale();
        SetPauseMenuVisible(true);
    }

    private void ResumeBattle()
    {
        if (!_isPaused || _mapModalOpen) return;

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
            Time.timeScale = _isPaused || _mapModalOpen || _rockNavigationUpdating ? 0f : _selectedBattleSpeed;
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
        if (_mapSystem != null) _mapSystem.CurrentMapChanged -= ResetRockPlacement;
        if (_battleHudView == null)
        {
            return;
        }

        _battleHudView.RockPlacementConfirmed -= PlaceRock;
        _battleHudView.RockRemovalRequested -= RemoveRock;
        _battleHudView.RockPlacementRequested -= OpenRockPlacement;
        _battleHudView.RockPlacementClosed -= CloseMapModal;
        _battleHudView.PlayerMagicRequested -= OpenPlayerMagic;
        _battleHudView.PlayerMagicClosed -= CloseMapModal;
        _battleHudView.PlayerMagicCastRequested -= CastPlayerMagic;
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
