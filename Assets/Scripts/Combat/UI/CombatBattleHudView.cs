using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WarSimulation.Combat.Map;

[DisallowMultipleComponent]
public sealed class CombatBattleHudView : MonoBehaviour
{
    private const string TemporaryControlsName = "TemporaryBattleControls";
    private const string DebugPanelName = "DebugBattlePanel";
    private const float DebugSidePanelWidth = 260f;
    private const float DebugSidePanelTopMargin = 24f;
    private const float DebugSidePanelBottomMargin = 24f;
    private const float DebugControlsWidth = 460f;
    private const float DebugControlsHeight = 60f;
    private const float DebugSkillDisplaySeconds = 2.2f;
    private static readonly Color BuffWeaponTextColor = new(1f, 0.58f, 0.12f, 1f);
    private static readonly Color DebuffWeaponTextColor = new(0.8f, 0.4f, 1f, 1f);

    private sealed class DebugTeamUi
    {
        public TMP_Text StoneTitle;
        public TMP_Text StoneHp;
        public Image StoneFill;
        public RectTransform CharacterContent;
        public readonly List<DebugCharacterUi> Characters = new();
    }

    private sealed class DebugCharacterUi
    {
        public GameObject Root;
        public Image Background;
        public TMP_Text Weapon;
        public TMP_Text Hp;
        public Image HpFill;
        public TMP_Text Objective;
        public TMP_Text Skills;
        public Character Character;
        public Color IdleColor;
        public float SkillHideAt = float.NegativeInfinity;
    }

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
    private GameObject _debugPanel;
    private DebugTeamUi _debugAllies;
    private DebugTeamUi _debugEnemies;
    private CombatCharacterSystem _debugCharacterSystem;
    private CombatMagicStoneSystem _debugMagicStoneSystem;
    private bool _debugUiVisible = true;
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
        CombatPartyFocus.Changed -= RefreshDebugFocusVisuals;
        CombatPartyFocus.Changed += RefreshDebugFocusVisuals;
        CombatSkillUseEvents.SkillUsed -= OnDebugSkillUsed;
        CombatSkillUseEvents.SkillUsed += OnDebugSkillUsed;
    }

    private void OnDisable()
    {
        DetachListeners();
        CombatPartyFocus.Changed -= RefreshDebugFocusVisuals;
        CombatSkillUseEvents.SkillUsed -= OnDebugSkillUsed;
    }

    private void Update()
    {
        ApplyDebugPanelVisibility();
        if (_debugUiVisible && _debugPanel != null && _debugPanel.activeSelf)
        {
            RefreshDebugUi();
        }
    }

    public void EnsureBuilt()
    {
        _controlPanel ??= transform.Find("ControlPanel")?.gameObject;
        _menuButton ??= transform.Find("ControlPanel/Menu/Image")?.GetComponent<Button>();
        _speedButton ??= transform.Find("ControlPanel/Speed/Image")?.GetComponent<Button>();
        _speedText ??= transform.Find("ControlPanel/Speed/Text")?.GetComponent<TMP_Text>();

        DisableUnavailableCommands();
        EnsureTemporaryControls();
        EnsureDebugPanel();
        SetProductionUiVisible(!_debugUiVisible);
        ConfigureTemporaryControls(_debugUiVisible);
        ApplyDebugPanelVisibility();
        if (isActiveAndEnabled)
        {
            AttachListeners();
        }
    }

    public void SetDebugUiVisible(bool visible)
    {
        EnsureBuilt();
        _debugUiVisible = visible;
        SetProductionUiVisible(!visible);
        ConfigureTemporaryControls(visible);
        ApplyDebugPanelVisibility();
        if (visible)
        {
            RefreshDebugUi();
        }
    }

    public void SetControlsVisible(bool visible)
    {
        if (_debugUiVisible)
        {
            if (_temporaryControls != null)
            {
                _temporaryControls.SetActive(visible);
            }

            if (!visible)
            {
                SetMenuVisible(false);
            }

            return;
        }

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

    private void EnsureDebugPanel()
    {
        if (_debugPanel != null && _debugAllies != null && _debugEnemies != null)
        {
            return;
        }

        Transform existing = transform.Find(DebugPanelName);
        if (existing != null)
        {
            _debugPanel = existing.gameObject;
        }

        if (_debugPanel == null)
        {
            _debugPanel = new GameObject(DebugPanelName, typeof(RectTransform));
            RectTransform panelRect = _debugPanel.GetComponent<RectTransform>();
            panelRect.SetParent(transform, false);
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
        }
        TMP_FontAsset font = GetComponentInChildren<TMP_Text>(includeInactive: true)?.font;
        _debugAllies = CreateDebugTeamUi(
            "DebugAlliesPanel",
            "味方魔石",
            CombatTeam.Ally,
            font);
        _debugEnemies = CreateDebugTeamUi(
            "DebugEnemiesPanel",
            "敵魔石",
            CombatTeam.Enemy,
            font);
    }

    private DebugTeamUi CreateDebugTeamUi(
        string objectName,
        string stoneLabel,
        CombatTeam team,
        TMP_FontAsset font)
    {
        GameObject rootObject = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(Image),
            typeof(VerticalLayoutGroup));
        RectTransform root = rootObject.GetComponent<RectTransform>();
        root.SetParent(_debugPanel.transform, false);
        root.anchorMin = team == CombatTeam.Ally ? new Vector2(0f, 0f) : new Vector2(1f, 0f);
        root.anchorMax = team == CombatTeam.Ally ? new Vector2(0f, 1f) : new Vector2(1f, 1f);
        root.pivot = team == CombatTeam.Ally ? new Vector2(0f, 0.5f) : new Vector2(1f, 0.5f);
        root.offsetMin = team == CombatTeam.Ally
            ? new Vector2(0f, DebugSidePanelBottomMargin)
            : new Vector2(-DebugSidePanelWidth, DebugSidePanelTopMargin);
        root.offsetMax = team == CombatTeam.Ally
            ? new Vector2(DebugSidePanelWidth, -DebugSidePanelTopMargin)
            : new Vector2(0f, -DebugSidePanelTopMargin);

        Image rootImage = rootObject.GetComponent<Image>();
        rootImage.color = Color.clear;
        rootImage.raycastTarget = false;

        VerticalLayoutGroup layout = rootObject.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(0, 0, 10, 10);
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        DebugTeamUi teamUi = new DebugTeamUi();
        GameObject stoneObject = new GameObject(
            "StoneStatus",
            typeof(RectTransform),
            typeof(Image),
            typeof(VerticalLayoutGroup),
            typeof(LayoutElement));
        RectTransform stoneRect = stoneObject.GetComponent<RectTransform>();
        stoneRect.SetParent(root, false);
        stoneObject.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.32f);
        stoneObject.GetComponent<LayoutElement>().preferredHeight = 56f;

        VerticalLayoutGroup stoneLayout = stoneObject.GetComponent<VerticalLayoutGroup>();
        stoneLayout.padding = new RectOffset(10, 10, 6, 6);
        stoneLayout.spacing = 2f;
        stoneLayout.childControlWidth = true;
        stoneLayout.childControlHeight = true;
        stoneLayout.childForceExpandWidth = true;
        stoneLayout.childForceExpandHeight = false;

        GameObject stoneHeader = new GameObject(
            "StoneHeader",
            typeof(RectTransform),
            typeof(HorizontalLayoutGroup),
            typeof(LayoutElement));
        RectTransform stoneHeaderRect = stoneHeader.GetComponent<RectTransform>();
        stoneHeaderRect.SetParent(stoneRect, false);
        stoneHeader.GetComponent<LayoutElement>().preferredHeight = 28f;

        HorizontalLayoutGroup stoneHeaderLayout = stoneHeader.GetComponent<HorizontalLayoutGroup>();
        stoneHeaderLayout.childControlWidth = true;
        stoneHeaderLayout.childControlHeight = true;
        stoneHeaderLayout.childForceExpandWidth = true;
        stoneHeaderLayout.childForceExpandHeight = false;

        teamUi.StoneTitle = CreateDebugText(
            stoneHeaderRect,
            "Title",
            stoneLabel,
            font,
            18f,
            TextAlignmentOptions.Left,
            28f);
        teamUi.StoneHp = CreateDebugText(
            stoneHeaderRect,
            "Hp",
            "HP -/-",
            font,
            16f,
            TextAlignmentOptions.Right,
            28f);
        CreateDebugBar(stoneRect, "HpBar", 10f, out Image stoneFill);
        stoneFill.color = team == CombatTeam.Ally
            ? new Color(0.2f, 0.7f, 1f, 1f)
            : new Color(1f, 0.3f, 0.25f, 1f);
        teamUi.StoneFill = stoneFill;

        teamUi.CharacterContent = CreateDebugScroll(root, "CharacterList");
        return teamUi;
    }

    private static void CreateDebugBar(
        Transform parent,
        string objectName,
        float height,
        out Image fill)
    {
        GameObject barObject = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(Image),
            typeof(LayoutElement));
        RectTransform barRect = barObject.GetComponent<RectTransform>();
        barRect.SetParent(parent, false);
        barObject.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.5f);
        barObject.GetComponent<LayoutElement>().preferredHeight = height;

        GameObject fillObject = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        RectTransform fillRect = fillObject.GetComponent<RectTransform>();
        fillRect.SetParent(barRect, false);
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        fill = fillObject.GetComponent<Image>();
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = 0;
    }

    private static RectTransform CreateDebugScroll(Transform parent, string objectName)
    {
        GameObject scrollObject = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(Image),
            typeof(ScrollRect),
            typeof(LayoutElement));
        RectTransform scrollRect = scrollObject.GetComponent<RectTransform>();
        scrollRect.SetParent(parent, false);
        scrollObject.GetComponent<Image>().color = Color.clear;
        scrollObject.GetComponent<LayoutElement>().flexibleHeight = 1f;

        GameObject viewportObject = new GameObject(
            "Viewport",
            typeof(RectTransform),
            typeof(Image),
            typeof(RectMask2D));
        RectTransform viewport = viewportObject.GetComponent<RectTransform>();
        viewport.SetParent(scrollRect, false);
        viewport.anchorMin = Vector2.zero;
        viewport.anchorMax = Vector2.one;
        viewport.offsetMin = Vector2.zero;
        viewport.offsetMax = Vector2.zero;
        viewportObject.GetComponent<Image>().color = Color.clear;

        GameObject contentObject = new GameObject(
            "Content",
            typeof(RectTransform),
            typeof(VerticalLayoutGroup),
            typeof(ContentSizeFitter));
        RectTransform content = contentObject.GetComponent<RectTransform>();
        content.SetParent(viewport, false);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.offsetMin = Vector2.zero;
        content.offsetMax = Vector2.zero;

        VerticalLayoutGroup contentLayout = contentObject.GetComponent<VerticalLayoutGroup>();
        contentLayout.spacing = 6f;
        contentLayout.childControlWidth = true;
        contentLayout.childControlHeight = true;
        contentLayout.childForceExpandWidth = true;
        contentLayout.childForceExpandHeight = false;

        ContentSizeFitter contentFitter = contentObject.GetComponent<ContentSizeFitter>();
        contentFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        ScrollRect scroll = scrollObject.GetComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        return content;
    }

    private DebugCharacterUi CreateDebugCharacterUi(
        Transform parent,
        bool isAlly,
        TMP_FontAsset font)
    {
        Color teamColor = isAlly
            ? new Color(0.08f, 0.25f, 0.45f, 0.96f)
            : new Color(0.45f, 0.12f, 0.12f, 0.96f);
        GameObject cardObject = new GameObject(
            isAlly ? "AllyCharacter" : "EnemyCharacter",
            typeof(RectTransform),
            typeof(Image),
            typeof(Button),
            typeof(VerticalLayoutGroup),
            typeof(LayoutElement));
        RectTransform cardRect = cardObject.GetComponent<RectTransform>();
        cardRect.SetParent(parent, false);

        Image background = cardObject.GetComponent<Image>();
        background.color = teamColor;
        Button button = cardObject.GetComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = background;

        VerticalLayoutGroup layout = cardObject.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(8, 8, 6, 6);
        layout.spacing = 2f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        cardObject.GetComponent<LayoutElement>().preferredHeight = 168f;
        DebugCharacterUi card = new DebugCharacterUi
        {
            Root = cardObject,
            Background = background,
            IdleColor = teamColor,
        };

        GameObject weaponHpRow = new GameObject(
            "WeaponHpRow",
            typeof(RectTransform),
            typeof(HorizontalLayoutGroup),
            typeof(LayoutElement));
        RectTransform weaponHpRect = weaponHpRow.GetComponent<RectTransform>();
        weaponHpRect.SetParent(cardRect, false);
        weaponHpRow.GetComponent<LayoutElement>().preferredHeight = 48f;

        HorizontalLayoutGroup weaponHpLayout = weaponHpRow.GetComponent<HorizontalLayoutGroup>();
        weaponHpLayout.spacing = 4f;
        weaponHpLayout.childControlWidth = true;
        weaponHpLayout.childControlHeight = true;
        weaponHpLayout.childForceExpandWidth = true;
        weaponHpLayout.childForceExpandHeight = false;

        card.Weapon = CreateDebugText(weaponHpRect, "Weapon", string.Empty, font, 30f, TextAlignmentOptions.Left, 48f);
        card.Hp = CreateDebugText(weaponHpRect, "Hp", string.Empty, font, 30f, TextAlignmentOptions.Right, 48f);

        CreateDebugBar(cardRect, "HpBar", 8f, out Image hpFill);
        hpFill.color = new Color(0.35f, 0.95f, 0.4f, 1f);
        card.HpFill = hpFill;
        card.Objective = CreateDebugText(
            cardRect,
            "Objective",
            string.Empty,
            font,
            28f,
            TextAlignmentOptions.Left,
            44f);
        card.Objective.textWrappingMode = TextWrappingModes.NoWrap;
        card.Objective.overflowMode = TextOverflowModes.Ellipsis;
        card.Skills = CreateDebugText(
            cardRect,
            "Skills",
            string.Empty,
            font,
            28f,
            TextAlignmentOptions.Left,
            44f);
        card.Skills.textWrappingMode = TextWrappingModes.Normal;
        card.Skills.overflowMode = TextOverflowModes.Ellipsis;

        button.onClick.AddListener(() => OnDebugCharacterClicked(card));
        cardObject.SetActive(false);
        return card;
    }

    private static TMP_Text CreateDebugText(
        Transform parent,
        string objectName,
        string value,
        TMP_FontAsset font,
        float fontSize,
        TextAlignmentOptions alignment,
        float height)
    {
        GameObject textObject = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(TextMeshProUGUI),
            typeof(LayoutElement));
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.SetParent(parent, false);

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.font = font;
        if (font != null)
        {
            text.fontSharedMaterial = font.material;
        }
        text.text = value;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = Color.white;
        text.raycastTarget = false;
        text.overflowMode = TextOverflowModes.Ellipsis;
        textObject.GetComponent<LayoutElement>().preferredHeight = height;
        return text;
    }

    private void SetProductionUiVisible(bool visible)
    {
        SetUiObjectVisible("Environment", visible);
        SetUiObjectVisible("MagicStoneStatusRoot", visible);
        SetUiObjectVisible("AlliesColumn", visible);
        SetUiObjectVisible("EnemiesColumn", visible);
        SetUiObjectVisible("UserCommandPanel", visible);
        SetUiObjectVisible("ControlPanel", visible);
    }

    private void SetUiObjectVisible(string objectName, bool visible)
    {
        Transform child = transform.Find(objectName);
        if (child != null)
        {
            child.gameObject.SetActive(visible);
        }
    }

    private void ConfigureTemporaryControls(bool debug)
    {
        if (_temporaryControls == null)
        {
            return;
        }

        RectTransform rect = _temporaryControls.GetComponent<RectTransform>();
        if (debug)
        {
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -DebugSidePanelTopMargin);
            rect.sizeDelta = new Vector2(DebugControlsWidth, DebugControlsHeight);
        }
        else
        {
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -150f);
            rect.sizeDelta = new Vector2(620f, 76f);
        }

        HorizontalLayoutGroup layout = _temporaryControls.GetComponent<HorizontalLayoutGroup>();
        layout.padding = debug ? new RectOffset(6, 6, 6, 6) : new RectOffset(8, 8, 8, 8);
        layout.spacing = debug ? 6f : 8f;

        TMP_Text[] labels = _temporaryControls.GetComponentsInChildren<TMP_Text>(includeInactive: true);
        for (int i = 0; i < labels.Length; i++)
        {
            labels[i].fontSize = debug ? 20f : 28f;
        }
    }

    private void ApplyDebugPanelVisibility()
    {
        if (_debugPanel != null)
        {
            bool visible = _debugUiVisible && CombatBattleFlow.IsRunning;
            if (_debugPanel.activeSelf != visible)
            {
                _debugPanel.SetActive(visible);
            }
        }
    }

    private void RefreshDebugUi()
    {
        ResolveDebugDependencies();
        RefreshDebugStone(_debugAllies, CombatTeam.Ally);
        RefreshDebugStone(_debugEnemies, CombatTeam.Enemy);
        RefreshDebugCharacters(
            _debugAllies,
            _debugCharacterSystem != null ? _debugCharacterSystem.AllyCharacters : null,
            isAlly: true);
        RefreshDebugCharacters(
            _debugEnemies,
            _debugCharacterSystem != null ? _debugCharacterSystem.EnemyCharacters : null,
            isAlly: false);
        RefreshDebugFocusVisuals();
    }

    private void ResolveDebugDependencies()
    {
        _debugCharacterSystem ??= FindAnyObjectByType<CombatCharacterSystem>();
        _debugMagicStoneSystem ??= CombatMagicStoneSystemResolver.Resolve();
    }

    private void RefreshDebugStone(DebugTeamUi teamUi, CombatTeam team)
    {
        if (teamUi == null)
        {
            return;
        }

        FeatureType type = team == CombatTeam.Ally
            ? FeatureType.OwnMainStone
            : FeatureType.EnemyMainStone;
        if (_debugMagicStoneSystem != null &&
            _debugMagicStoneSystem.TryGetState(type, out MagicStoneRuntimeState state))
        {
            teamUi.StoneHp.text = $"{state.HP}/{state.MaxHP}";
            teamUi.StoneFill.fillAmount = Mathf.Clamp01(state.HP / (float)Mathf.Max(1, state.MaxHP));
        }
        else
        {
            teamUi.StoneHp.text = "HP -/-";
            teamUi.StoneFill.fillAmount = 0f;
        }
    }

    private void RefreshDebugCharacters(
        DebugTeamUi teamUi,
        IReadOnlyList<Character> characters,
        bool isAlly)
    {
        if (teamUi == null)
        {
            return;
        }

        int count = characters != null ? characters.Count : 0;
        TMP_FontAsset font = GetComponentInChildren<TMP_Text>(includeInactive: true)?.font;
        while (teamUi.Characters.Count < count)
        {
            teamUi.Characters.Add(CreateDebugCharacterUi(teamUi.CharacterContent, isAlly, font));
        }

        for (int i = 0; i < teamUi.Characters.Count; i++)
        {
            DebugCharacterUi card = teamUi.Characters[i];
            Character character = characters != null && i < count ? characters[i] : null;
            if (card.Character != character)
            {
                ClearDebugSkill(card);
            }

            card.Character = character;
            card.Root.SetActive(character != null);
            if (character == null)
            {
                continue;
            }

            int maxHp = Mathf.Max(1, character.MaxHP);
            card.Weapon.text = CombatAiDebugLabels.WeaponShort(character.EquippedWeapon);
            card.Weapon.color = ResolveWeaponTextColor(character);
            card.Hp.text = $"{character.HP}/{maxHp}";
            card.HpFill.fillAmount = Mathf.Clamp01(character.HP / (float)maxHp);
            CombatAiBrain aiBrain = character.GetComponent<CombatAiBrain>();
            CombatObjective objective = aiBrain != null
                ? aiBrain.LastPlan.Objective
                : CombatObjective.Search;
            card.Objective.text = $"AI目的: {CombatAiDebugLabels.ObjectiveShort(objective)}";
            if (card.SkillHideAt != float.NegativeInfinity && Time.unscaledTime >= card.SkillHideAt)
            {
                ClearDebugSkill(card);
            }
        }
    }

    private void OnDebugSkillUsed(Character user, string skillName)
    {
        ShowDebugSkill(_debugAllies, user, skillName);
        ShowDebugSkill(_debugEnemies, user, skillName);
    }

    private static void ShowDebugSkill(DebugTeamUi teamUi, Character user, string skillName)
    {
        if (teamUi == null || user == null || string.IsNullOrWhiteSpace(skillName))
        {
            return;
        }

        for (int i = 0; i < teamUi.Characters.Count; i++)
        {
            DebugCharacterUi card = teamUi.Characters[i];
            if (card.Character != user)
            {
                continue;
            }

            card.Skills.text = $"スキル: {skillName}";
            card.SkillHideAt = Time.unscaledTime + DebugSkillDisplaySeconds;
            return;
        }
    }

    private static void ClearDebugSkill(DebugCharacterUi card)
    {
        card.Skills.text = string.Empty;
        card.SkillHideAt = float.NegativeInfinity;
    }

    private static Color ResolveWeaponTextColor(Character character)
    {
        if (character?.StatusEffects == null)
        {
            return Color.white;
        }

        IReadOnlyList<CombatStatusEffectSnapshot> effects = character.StatusEffects.GetActiveEffectSnapshots();
        int buffCount = 0;
        int debuffCount = 0;
        for (int i = 0; i < effects.Count; i++)
        {
            if (effects[i].IsBuff)
            {
                buffCount++;
            }

            if (effects[i].IsDebuff)
            {
                debuffCount++;
            }
        }

        if (buffCount == 0 && debuffCount == 0)
        {
            return Color.white;
        }

        Color color;
        if (buffCount > 0 && debuffCount > 0)
        {
            color = Color.Lerp(
                BuffWeaponTextColor,
                DebuffWeaponTextColor,
                debuffCount / (float)(buffCount + debuffCount));
        }
        else
        {
            color = buffCount > 0 ? BuffWeaponTextColor : DebuffWeaponTextColor;
        }

        int effectCount = buffCount + debuffCount;
        if (effectCount > 1)
        {
            float brightenRatio = (effectCount - 1f) / (effectCount + 1f);
            color = Color.Lerp(color, Color.white, brightenRatio);
        }

        return color;
    }

    private void OnDebugCharacterClicked(DebugCharacterUi card)
    {
        if (card == null || card.Character == null)
        {
            return;
        }

        CombatPartyFocus.Toggle(card.Character);
        CombatCharacterFocusMarker.EnsureFor(card.Character);
    }

    private void RefreshDebugFocusVisuals()
    {
        RefreshDebugFocusVisuals(_debugAllies);
        RefreshDebugFocusVisuals(_debugEnemies);
    }

    private static void RefreshDebugFocusVisuals(DebugTeamUi teamUi)
    {
        if (teamUi == null)
        {
            return;
        }

        Color focusColor = new Color(1f, 0.85f, 0.1f, 0.96f);
        for (int i = 0; i < teamUi.Characters.Count; i++)
        {
            DebugCharacterUi card = teamUi.Characters[i];
            if (card?.Background == null)
            {
                continue;
            }

            card.Background.color = card.Character != null && card.Character == CombatPartyFocus.Selected
                ? focusColor
                : card.IdleColor;
        }
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
        if (font != null)
        {
            label.fontSharedMaterial = font.material;
        }
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
