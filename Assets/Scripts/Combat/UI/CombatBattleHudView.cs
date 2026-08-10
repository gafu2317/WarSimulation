using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class CombatBattleHudView : MonoBehaviour
{
    private const string TemporaryControlsName = "TemporaryBattleControls";

    private Button _menuButton;
    private Button _speedButton;
    private TMP_Text _speedText;
    private GameObject _controlPanel;
    private GameObject _temporaryControls;
    private Button _temporarySpeedButton;
    private TMP_Text _temporarySpeedText;
    private Button _pauseButton;
    private TMP_Text _pauseButtonText;
    private Button _returnButton;
    private bool _listenersAttached;
    private bool _isPaused;

    public event Action MenuRequested;
    public event Action ResumeRequested;
    public event Action ReturnToSelectionRequested;
    public event Action SpeedRequested;

    public bool IsMenuVisible => _isPaused;

    private void Awake()
    {
        EnsureBuilt();
    }

    private void OnEnable()
    {
        EnsureBuilt();
        AttachListeners();
    }

    private void OnDisable()
    {
        DetachListeners();
    }

    public void EnsureBuilt()
    {
        _controlPanel ??= transform.Find("ControlPanel")?.gameObject;
        _menuButton ??= transform.Find("ControlPanel/Menu/Image")?.GetComponent<Button>();
        _speedButton ??= transform.Find("ControlPanel/Speed/Image")?.GetComponent<Button>();
        _speedText ??= transform.Find("ControlPanel/Speed/Text")?.GetComponent<TMP_Text>();

        DisableUnavailableCommands();
        EnsureTemporaryControls();
        if (isActiveAndEnabled)
        {
            AttachListeners();
        }
    }

    public void SetControlsVisible(bool visible)
    {
        if (_controlPanel != null)
        {
            _controlPanel.SetActive(visible);
        }

        if (_temporaryControls != null)
        {
            _temporaryControls.SetActive(visible);
        }

        if (!visible)
        {
            SetMenuVisible(false);
        }
    }

    public void SetMenuVisible(bool visible)
    {
        _isPaused = visible;
        if (_pauseButtonText != null)
        {
            _pauseButtonText.text = visible ? "再開" : "一時停止";
        }
    }

    public void SetSpeedLabel(float speed)
    {
        if (_speedText != null)
        {
            _speedText.text = $"{speed:0}x";
        }

        if (_temporarySpeedText != null)
        {
            _temporarySpeedText.text = $"速度 {speed:0}x";
        }
    }

    private void DisableUnavailableCommands()
    {
        DisableButtonsUnder(transform.Find("UserCommandPanel"));
    }

    private static void DisableButtonsUnder(Transform root)
    {
        if (root == null)
        {
            return;
        }

        Button[] buttons = root.GetComponentsInChildren<Button>(includeInactive: true);
        for (int i = 0; i < buttons.Length; i++)
        {
            buttons[i].interactable = false;
        }
    }

    private void EnsureTemporaryControls()
    {
        Transform existing = transform.Find(TemporaryControlsName);
        if (existing != null)
        {
            _temporaryControls = existing.gameObject;
            ResolveTemporaryControls(existing);
            return;
        }

        TMP_FontAsset font = GetComponentInChildren<TMP_Text>(includeInactive: true)?.font;
        _temporaryControls = new GameObject(
            TemporaryControlsName,
            typeof(RectTransform),
            typeof(Image),
            typeof(HorizontalLayoutGroup));
        RectTransform controlsRect = _temporaryControls.GetComponent<RectTransform>();
        controlsRect.SetParent(transform, false);
        controlsRect.anchorMin = new Vector2(0.5f, 1f);
        controlsRect.anchorMax = new Vector2(0.5f, 1f);
        controlsRect.pivot = new Vector2(0.5f, 1f);
        controlsRect.anchoredPosition = new Vector2(0f, -150f);
        controlsRect.sizeDelta = new Vector2(620f, 76f);
        _temporaryControls.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.68f);

        HorizontalLayoutGroup layout = _temporaryControls.GetComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(8, 8, 8, 8);
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;

        _temporarySpeedButton = CreateButton(controlsRect, "SpeedButton", "速度 1x", font);
        _pauseButton = CreateButton(controlsRect, "PauseButton", "一時停止", font);
        _returnButton = CreateButton(controlsRect, "ReturnToSelectionButton", "編成に戻る", font);
        _temporarySpeedText = _temporarySpeedButton.GetComponentInChildren<TMP_Text>();
        _pauseButtonText = _pauseButton.GetComponentInChildren<TMP_Text>();
        _temporaryControls.SetActive(false);
    }

    private void ResolveTemporaryControls(Transform controls)
    {
        _temporarySpeedButton = controls.Find("SpeedButton")?.GetComponent<Button>();
        _pauseButton = controls.Find("PauseButton")?.GetComponent<Button>();
        _returnButton = controls.Find("ReturnToSelectionButton")?.GetComponent<Button>();
        _temporarySpeedText = _temporarySpeedButton?.GetComponentInChildren<TMP_Text>();
        _pauseButtonText = _pauseButton?.GetComponentInChildren<TMP_Text>();
    }

    private static Button CreateButton(Transform parent, string objectName, string labelValue, TMP_FontAsset font)
    {
        GameObject buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.18f, 0.24f, 0.36f, 1f);

        GameObject labelObject = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.SetParent(rect, false);
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(8f, 4f);
        labelRect.offsetMax = new Vector2(-8f, -4f);

        TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
        label.font = font;
        label.text = labelValue;
        label.fontSize = 28f;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.raycastTarget = false;
        return buttonObject.GetComponent<Button>();
    }

    private void AttachListeners()
    {
        if (_listenersAttached)
        {
            return;
        }

        _menuButton?.onClick.AddListener(OnMenuClicked);
        _speedButton?.onClick.AddListener(OnSpeedClicked);
        _temporarySpeedButton?.onClick.AddListener(OnSpeedClicked);
        _pauseButton?.onClick.AddListener(OnPauseClicked);
        _returnButton?.onClick.AddListener(OnReturnClicked);
        _listenersAttached = true;
    }

    private void DetachListeners()
    {
        if (!_listenersAttached)
        {
            return;
        }

        _menuButton?.onClick.RemoveListener(OnMenuClicked);
        _speedButton?.onClick.RemoveListener(OnSpeedClicked);
        _temporarySpeedButton?.onClick.RemoveListener(OnSpeedClicked);
        _pauseButton?.onClick.RemoveListener(OnPauseClicked);
        _returnButton?.onClick.RemoveListener(OnReturnClicked);
        _listenersAttached = false;
    }

    private void OnMenuClicked() => MenuRequested?.Invoke();
    private void OnSpeedClicked() => SpeedRequested?.Invoke();
    private void OnPauseClicked()
    {
        if (_isPaused)
        {
            ResumeRequested?.Invoke();
            return;
        }

        MenuRequested?.Invoke();
    }
    private void OnReturnClicked() => ReturnToSelectionRequested?.Invoke();
}
