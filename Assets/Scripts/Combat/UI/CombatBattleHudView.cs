using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class CombatBattleHudView : MonoBehaviour
{
    private const string PauseMenuName = "BattlePauseMenu";

    private Button _menuButton;
    private Button _speedButton;
    private TMP_Text _speedText;
    private GameObject _controlPanel;
    private GameObject _pauseMenu;
    private Button _resumeButton;
    private Button _returnButton;
    private bool _listenersAttached;

    public event Action MenuRequested;
    public event Action ResumeRequested;
    public event Action ReturnToSelectionRequested;
    public event Action SpeedRequested;

    public bool IsMenuVisible => _pauseMenu != null && _pauseMenu.activeSelf;

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
        EnsurePauseMenu();
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

        if (!visible)
        {
            SetMenuVisible(false);
        }
    }

    public void SetMenuVisible(bool visible)
    {
        if (_pauseMenu != null)
        {
            _pauseMenu.SetActive(visible);
            if (visible)
            {
                _pauseMenu.transform.SetAsLastSibling();
            }
        }
    }

    public void SetSpeedLabel(float speed)
    {
        if (_speedText != null)
        {
            _speedText.text = $"{speed:0}x";
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

    private void EnsurePauseMenu()
    {
        Transform existing = transform.Find(PauseMenuName);
        if (existing != null)
        {
            _pauseMenu = existing.gameObject;
            _resumeButton = existing.Find("Panel/ResumeButton")?.GetComponent<Button>();
            _returnButton = existing.Find("Panel/ReturnToSelectionButton")?.GetComponent<Button>();
            return;
        }

        TMP_FontAsset font = GetComponentInChildren<TMP_Text>(includeInactive: true)?.font;
        _pauseMenu = new GameObject(PauseMenuName, typeof(RectTransform), typeof(Image));
        RectTransform menuRect = _pauseMenu.GetComponent<RectTransform>();
        menuRect.SetParent(transform, false);
        menuRect.anchorMin = Vector2.zero;
        menuRect.anchorMax = Vector2.one;
        menuRect.offsetMin = Vector2.zero;
        menuRect.offsetMax = Vector2.zero;

        Image backdrop = _pauseMenu.GetComponent<Image>();
        backdrop.color = new Color(0f, 0f, 0f, 0.72f);
        backdrop.raycastTarget = true;

        GameObject panelObject = new GameObject("Panel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
        RectTransform panel = panelObject.GetComponent<RectTransform>();
        panel.SetParent(menuRect, false);
        panel.anchorMin = new Vector2(0.5f, 0.5f);
        panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.pivot = new Vector2(0.5f, 0.5f);
        panel.sizeDelta = new Vector2(420f, 220f);
        panelObject.GetComponent<Image>().color = new Color(0.08f, 0.1f, 0.16f, 0.98f);

        VerticalLayoutGroup layout = panelObject.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(48, 48, 44, 44);
        layout.spacing = 20f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;

        _resumeButton = CreateButton(panel, "ResumeButton", "戦闘に戻る", font);
        _returnButton = CreateButton(panel, "ReturnToSelectionButton", "編成に戻る", font);
        _pauseMenu.SetActive(false);
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
        _resumeButton?.onClick.AddListener(OnResumeClicked);
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
        _resumeButton?.onClick.RemoveListener(OnResumeClicked);
        _returnButton?.onClick.RemoveListener(OnReturnClicked);
        _listenersAttached = false;
    }

    private void OnMenuClicked() => MenuRequested?.Invoke();
    private void OnSpeedClicked() => SpeedRequested?.Invoke();
    private void OnResumeClicked() => ResumeRequested?.Invoke();
    private void OnReturnClicked() => ReturnToSelectionRequested?.Invoke();
}
