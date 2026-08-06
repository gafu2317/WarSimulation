using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class CombatFlow : MonoBehaviour
{
    private static readonly float[] BattleSpeedOptions = { 1f, 2f, 4f, 8f };

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
    private readonly List<Button> _speedButtons = new();

    private GameObject _battleControlRoot;
    private Button _pauseButton;
    private Button _returnToSelectionButton;
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
        ClearBattleControlListeners();
        RestoreNormalSpeed();
    }

    private void StartBattle(
        IReadOnlyList<CombatParticipantSetup> selectedAllies,
        IReadOnlyList<CombatParticipantSetup> selectedEnemies)
    {
        _characterSystem.SetParticipants(selectedAllies, selectedEnemies);
        SetVisible(_characterSelectionPanel, false);
        SetBattleUiVisible(true);
        SetVisible(_resultPanel, false);
        SetBattleControlsVisible(true);
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
        SetVisible(_characterSelectionPanel, false);
        SetBattleUiVisible(false);
        SetBattleControlsVisible(false);
        SetVisible(_resultPanel, true);
    }

    private void ShowSelection()
    {
        RestoreNormalSpeed();
        _battleFlow.AbortBattle();
        _characterSystem.SetParticipants(_allyCandidates, _enemies);
        _characterSelection.ResetSelection();
        SetVisible(_characterSelectionPanel, true);
        SetBattleUiVisible(false);
        SetBattleControlsVisible(false);
        SetVisible(_resultPanel, false);
    }

    private void EnsureBattleControls()
    {
        if (_battleControlRoot != null) return;

        Canvas canvas = GetComponentInParent<Canvas>(includeInactive: true);
        if (canvas == null && _characterSelectionPanel != null)
        {
            canvas = _characterSelectionPanel.GetComponentInParent<Canvas>(includeInactive: true);
        }

        if (canvas == null)
        {
            Debug.LogWarning($"[{nameof(CombatFlow)}] Battle controls require a Canvas.", this);
            return;
        }

        GameObject rootObject = new GameObject(
            "BattlePlaytestControls",
            typeof(RectTransform),
            typeof(HorizontalLayoutGroup),
            typeof(Image));
        _battleControlRoot = rootObject;
        RectTransform root = rootObject.GetComponent<RectTransform>();
        root.SetParent(canvas.transform, false);
        root.anchorMin = new Vector2(0.5f, 1f);
        root.anchorMax = new Vector2(0.5f, 1f);
        root.pivot = new Vector2(0.5f, 1f);
        root.anchoredPosition = new Vector2(0f, -24f);
        root.sizeDelta = new Vector2(640f, 56f);

        Canvas controlCanvas = rootObject.AddComponent<Canvas>();
        controlCanvas.overrideSorting = true;
        controlCanvas.sortingOrder = 200;
        rootObject.AddComponent<GraphicRaycaster>();

        Image background = rootObject.GetComponent<Image>();
        background.color = new Color(0.06f, 0.08f, 0.12f, 0.82f);
        background.raycastTarget = true;

        HorizontalLayoutGroup layout = rootObject.GetComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(8, 8, 8, 8);
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;

        _speedButtons.Clear();
        for (int i = 0; i < BattleSpeedOptions.Length; i++)
        {
            float speed = BattleSpeedOptions[i];
            Button button = CreateBattleControlButton(root, $"Speedx{speed:0}", 72f);
            float captured = speed;
            button.onClick.AddListener(() => SetBattleSpeed(captured));
            _speedButtons.Add(button);
        }

        _pauseButton = CreateBattleControlButton(root, "Pause", 120f);
        _pauseButton.onClick.AddListener(TogglePause);

        _returnToSelectionButton = CreateBattleControlButton(root, "ReturnToSelection", 160f);
        _returnToSelectionButton.onClick.AddListener(ShowSelection);
        SetBattleControlLabel(_returnToSelectionButton, "編成に戻る");

        RefreshBattleControlLabels();
        SetBattleControlsVisible(false);
    }

    private Button CreateBattleControlButton(Transform parent, string objectName, float width)
    {
        GameObject buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        buttonObject.transform.SetParent(parent, false);

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.16f, 0.2f, 0.28f, 0.95f);

        LayoutElement layout = buttonObject.GetComponent<LayoutElement>();
        layout.preferredWidth = width;
        layout.preferredHeight = 40f;
        layout.flexibleWidth = 0f;

        GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.SetParent(buttonObject.transform, false);
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(4f, 2f);
        labelRect.offsetMax = new Vector2(-4f, -2f);

        TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
        if (_resultTitle != null)
        {
            label.font = _resultTitle.font;
            label.fontSharedMaterial = _resultTitle.fontSharedMaterial;
        }

        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = 20f;
        label.enableAutoSizing = false;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.color = Color.white;
        label.raycastTarget = false;

        return buttonObject.GetComponent<Button>();
    }

    private void SetBattleSpeed(float speed)
    {
        _selectedBattleSpeed = speed;
        _isPaused = false;
        ApplyEffectiveTimeScale();
        RefreshBattleControlLabels();
    }

    private void TogglePause()
    {
        if (_battleFlow == null || _battleFlow.State != CombatBattleState.Running) return;

        _isPaused = !_isPaused;
        ApplyEffectiveTimeScale();
        RefreshBattleControlLabels();
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
        RefreshSpeedButtonLabels();
        RefreshPauseButtonLabel();
    }

    private void RefreshSpeedButtonLabels()
    {
        for (int i = 0; i < _speedButtons.Count && i < BattleSpeedOptions.Length; i++)
        {
            float speed = BattleSpeedOptions[i];
            bool selected = !_isPaused && Mathf.Approximately(_selectedBattleSpeed, speed);
            string mark = selected ? "■" : "□";
            SetBattleControlLabel(_speedButtons[i], $"{mark} ×{speed:0}");
            Image image = _speedButtons[i] != null ? _speedButtons[i].GetComponent<Image>() : null;
            if (image != null)
            {
                image.color = selected
                    ? new Color(0.22f, 0.42f, 0.72f, 0.95f)
                    : new Color(0.16f, 0.2f, 0.28f, 0.95f);
            }
        }
    }

    private void RefreshPauseButtonLabel()
    {
        if (_pauseButton == null) return;

        SetBattleControlLabel(_pauseButton, _isPaused ? "再開" : "一時停止");
        Image image = _pauseButton.GetComponent<Image>();
        if (image != null)
        {
            image.color = _isPaused
                ? new Color(0.72f, 0.42f, 0.18f, 0.95f)
                : new Color(0.16f, 0.2f, 0.28f, 0.95f);
        }
    }

    private static void SetBattleControlLabel(Button button, string value)
    {
        TMP_Text label = button != null ? button.GetComponentInChildren<TMP_Text>(true) : null;
        if (label != null) label.text = value;
    }

    private void SetBattleControlsVisible(bool visible)
    {
        if (_battleControlRoot != null)
        {
            _battleControlRoot.SetActive(visible);
        }
    }

    private void ClearBattleControlListeners()
    {
        for (int i = 0; i < _speedButtons.Count; i++)
        {
            if (_speedButtons[i] != null) _speedButtons[i].onClick.RemoveAllListeners();
        }

        if (_pauseButton != null)
        {
            _pauseButton.onClick.RemoveListener(TogglePause);
        }

        if (_returnToSelectionButton != null)
        {
            _returnToSelectionButton.onClick.RemoveListener(ShowSelection);
        }
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
