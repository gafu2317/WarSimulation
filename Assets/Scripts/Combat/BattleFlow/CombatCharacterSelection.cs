using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class CombatCharacterSelection : MonoBehaviour
{
    private static readonly WeaponKind[] DefaultPartyWeapons =
    {
        WeaponKind.Sword,
        WeaponKind.Sword,
        WeaponKind.Wand,
        WeaponKind.Rosary,
        WeaponKind.Shield,
    };

    [SerializeField] private RectTransform _characterList;
    [SerializeField] private Button _characterItemPrefab;
    [SerializeField] private TMP_Text _selectionCountText;
    [SerializeField] private Button _startBattleButton;
    [SerializeField] private List<WeaponConfig> _weaponOptions = new();
    [SerializeField] private List<CombatAiPersonalityProfile> _personalityOptions = new();

    private readonly List<SelectionRow> _allyRows = new();
    private readonly List<SelectionRow> _enemyRows = new();
    private readonly List<CombatAiPersonalityProfile> _builtInPersonalityOptions = new();
    private Action<IReadOnlyList<CombatParticipantSetup>, IReadOnlyList<CombatParticipantSetup>> _confirmed;
    private Button _highlightButton;
    private Button _detailSettingsButton;
    private GameObject _allyColumnRoot;
    private GameObject _enemyColumnRoot;
    private RectTransform _pickerRoot;
    private TMP_Text _pickerDescription;
    private TMP_Text _pickerTitle;
    private Transform _pickerContent;
    private bool _detailSettingsOpen;

    public IReadOnlyList<WeaponConfig> WeaponOptions => _weaponOptions;

    public void Initialize(
        IReadOnlyList<Character> allyCandidates,
        IReadOnlyList<Character> enemyCandidates,
        Action<IReadOnlyList<CombatParticipantSetup>, IReadOnlyList<CombatParticipantSetup>> confirmed)
    {
        _confirmed = confirmed;
        RemoveNullAndDuplicateOptions(_weaponOptions);
        RemoveNullAndDuplicateOptions(_personalityOptions);
        DeduplicatePersonalityOptionsByKind();
        AddBuiltInPersonalityOptions();
        RebuildLayout(allyCandidates, enemyCandidates);
        ResetSelection();
    }

    public void ResetSelection()
    {
        ClosePicker();
        SetDetailSettingsOpen(false);
        ApplyDefaultParty(_allyRows);
        ApplyDefaultParty(_enemyRows);
        Refresh();
    }

    private void ApplyDefaultParty(List<SelectionRow> rows)
    {
        for (int i = 0; i < rows.Count; i++)
        {
            SelectionRow row = rows[i];
            bool selected = i < DefaultPartyWeapons.Length;
            row.Selected = selected;
            if (!selected) continue;

            int weaponIndex = FindWeaponIndex(DefaultPartyWeapons[i]);
            if (weaponIndex >= 0)
            {
                row.WeaponIndex = weaponIndex;
            }

            int personalityIndex = FindStandardPersonalityIndex();
            if (personalityIndex >= 0)
            {
                row.PersonalityIndex = personalityIndex;
            }
        }
    }

    private int FindWeaponIndex(WeaponKind kind)
    {
        for (int i = 0; i < _weaponOptions.Count; i++)
        {
            WeaponConfig weapon = _weaponOptions[i];
            if (weapon != null && weapon.Kind == kind) return i;
        }

        return -1;
    }

    private int FindStandardPersonalityIndex()
    {
        for (int i = 0; i < _personalityOptions.Count; i++)
        {
            CombatAiPersonalityProfile personality = _personalityOptions[i];
            if (personality != null && personality.Kind == CombatAiPersonalityKind.Neutral)
            {
                return i;
            }
        }

        CombatAiPersonalityProfile created =
            CombatAiPersonalityProfile.CreateBuiltInProfile(CombatAiPersonalityKind.Neutral);
        _builtInPersonalityOptions.Add(created);
        _personalityOptions.Insert(0, created);
        return 0;
    }

    private void Awake()
    {
        _startBattleButton?.onClick.AddListener(ConfirmSelection);
    }

    private void OnDestroy()
    {
        _startBattleButton?.onClick.RemoveListener(ConfirmSelection);
        if (_highlightButton != null)
        {
            _highlightButton.onClick.RemoveListener(OpenHighlightPicker);
        }

        if (_detailSettingsButton != null)
        {
            _detailSettingsButton.onClick.RemoveListener(ToggleDetailSettings);
        }

        ClosePicker();
        for (int i = 0; i < _builtInPersonalityOptions.Count; i++)
        {
            if (_builtInPersonalityOptions[i] != null) Destroy(_builtInPersonalityOptions[i]);
        }
    }

    private void AddBuiltInPersonalityOptions()
    {
        if (_builtInPersonalityOptions.Count > 0) return;

        List<CombatAiPersonalityProfile> builtIns = CombatAiPersonalityProfile.CreateBuiltInProfiles();
        for (int i = 0; i < builtIns.Count; i++)
        {
            CombatAiPersonalityProfile profile = builtIns[i];
            if (FindPersonalityByKind(profile.Kind) != null)
            {
                Destroy(profile);
                continue;
            }

            _builtInPersonalityOptions.Add(profile);
            _personalityOptions.Add(profile);
        }
    }

    private void DeduplicatePersonalityOptionsByKind()
    {
        var seen = new HashSet<CombatAiPersonalityKind>();
        for (int i = 0; i < _personalityOptions.Count;)
        {
            CombatAiPersonalityProfile personality = _personalityOptions[i];
            if (personality == null || !seen.Add(personality.Kind))
            {
                _personalityOptions.RemoveAt(i);
                continue;
            }

            i++;
        }
    }

    private void RebuildLayout(
        IReadOnlyList<Character> allyCandidates,
        IReadOnlyList<Character> enemyCandidates)
    {
        _allyRows.Clear();
        _enemyRows.Clear();
        _allyColumnRoot = null;
        _enemyColumnRoot = null;
        if (_characterList == null || _characterItemPrefab == null || _selectionCountText == null) return;

        ConfigureListLayout(_characterList);
        ClearToolbarButtons();
        ClosePicker();

        for (int i = _characterList.childCount - 1; i >= 0; i--)
        {
            Destroy(_characterList.GetChild(i).gameObject);
        }

        _characterList.sizeDelta = new Vector2(1080f, 560f);
        CreateToolbar(_characterList);

        GameObject teamsObject = new GameObject(
            "TeamSelections",
            typeof(RectTransform),
            typeof(HorizontalLayoutGroup),
            typeof(LayoutElement));
        RectTransform teams = teamsObject.GetComponent<RectTransform>();
        teams.SetParent(_characterList, false);
        HorizontalLayoutGroup layout = teamsObject.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 24f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        teamsObject.GetComponent<LayoutElement>().preferredHeight = 520f;

        RectTransform allyColumn = CreateTeamColumn(teams, "AllySelection", "味方");
        RectTransform enemyColumn = CreateTeamColumn(teams, "EnemySelection", "敵");
        _allyColumnRoot = allyColumn.gameObject;
        _enemyColumnRoot = enemyColumn.gameObject;
        BuildRows(allyColumn, allyCandidates, _allyRows);
        BuildRows(enemyColumn, enemyCandidates, _enemyRows);
        SetDetailSettingsOpen(false);
    }

    private void ClearToolbarButtons()
    {
        if (_highlightButton != null)
        {
            _highlightButton.onClick.RemoveListener(OpenHighlightPicker);
            _highlightButton = null;
        }

        if (_detailSettingsButton != null)
        {
            _detailSettingsButton.onClick.RemoveListener(ToggleDetailSettings);
            _detailSettingsButton = null;
        }
    }

    private void CreateToolbar(RectTransform parent)
    {
        GameObject toolbarObject = new GameObject(
            "SelectionToolbar",
            typeof(RectTransform),
            typeof(HorizontalLayoutGroup),
            typeof(LayoutElement));
        RectTransform toolbar = toolbarObject.GetComponent<RectTransform>();
        toolbar.SetParent(parent, false);
        HorizontalLayoutGroup layout = toolbarObject.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 12f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        toolbarObject.GetComponent<LayoutElement>().preferredHeight = 44f;

        _highlightButton = CreateButton(toolbar, "PersonalityHighlightButton", 400f, 48f, OpenHighlightPicker);
        _detailSettingsButton = CreateButton(toolbar, "DetailSettingsButton", 220f, 48f, ToggleDetailSettings);
        RefreshHighlightButton();
        RefreshDetailSettingsButton();
    }

    private void ToggleDetailSettings()
    {
        SetDetailSettingsOpen(!_detailSettingsOpen);
    }

    private void SetDetailSettingsOpen(bool open)
    {
        _detailSettingsOpen = open;
        if (_allyColumnRoot != null) _allyColumnRoot.SetActive(!open);
        if (_enemyColumnRoot != null) _enemyColumnRoot.SetActive(open);
        if (_highlightButton != null) _highlightButton.gameObject.SetActive(!open);
        RefreshDetailSettingsButton();
        RefreshSelectionCountText();
    }

    private void RefreshDetailSettingsButton()
    {
        SetButtonLabel(_detailSettingsButton, _detailSettingsOpen ? "閉じる" : "詳細設定");
    }

    private void OpenHighlightPicker()
    {
        OpenPersonalityPicker(
            "ハイライト性格を選択",
            selectedIndex: -1,
            includeNone: true,
            onSelected: index =>
            {
                if (index < 0)
                {
                    CombatAiPersonalityHighlight.Set(null);
                }
                else
                {
                    CombatAiPersonalityProfile profile = GetOption(_personalityOptions, index);
                    CombatAiPersonalityHighlight.Set(profile != null ? profile.Kind : null);
                }

                RefreshHighlightButton();
            });
    }

    private void RefreshHighlightButton()
    {
        SetButtonLabel(_highlightButton, $"ハイライト: {CombatAiPersonalityHighlight.DisplayLabel}");
    }

    private RectTransform CreateTeamColumn(RectTransform parent, string objectName, string title)
    {
        GameObject columnObject = new GameObject(objectName, typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        RectTransform column = columnObject.GetComponent<RectTransform>();
        column.SetParent(parent, false);

        VerticalLayoutGroup layout = columnObject.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 8f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        columnObject.GetComponent<LayoutElement>().preferredWidth = 1080f;

        TMP_Text header = Instantiate(_selectionCountText, column);
        header.name = $"{objectName}Title";
        header.text = title;
        header.alignment = TextAlignmentOptions.Center;
        header.fontSize = 28f;
        header.enableAutoSizing = false;
        LayoutElement headerLayout = header.gameObject.GetComponent<LayoutElement>() ??
                                     header.gameObject.AddComponent<LayoutElement>();
        headerLayout.preferredHeight = 48f;
        return column;
    }

    private void BuildRows(
        RectTransform parent,
        IReadOnlyList<Character> candidates,
        List<SelectionRow> destination)
    {
        if (candidates == null) return;

        var added = new HashSet<Character>();
        for (int i = 0; i < candidates.Count; i++)
        {
            Character character = candidates[i];
            if (character == null || !added.Add(character)) continue;

            SelectionRow row = CreateRow(parent, character);
            destination.Add(row);
        }
    }

    private SelectionRow CreateRow(RectTransform parent, Character character)
    {
        GameObject rowObject = new GameObject(character.name, typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        rowObject.transform.SetParent(parent, false);
        HorizontalLayoutGroup layout = rowObject.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 8f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        rowObject.GetComponent<LayoutElement>().preferredHeight = 72f;

        var row = new SelectionRow
        {
            Character = character,
            WeaponIndex = FindOptionIndex(_weaponOptions, character.EquippedWeaponConfig),
            PersonalityIndex = FindPersonalityIndex(character.PersonalityProfile),
            CharacterButton = CreateButton(rowObject.transform, null, 320f, 64f),
            WeaponButton = CreateButton(rowObject.transform, null, 200f, 64f),
            PersonalityButton = CreateButton(rowObject.transform, null, 240f, 64f),
        };

        row.CharacterButton.onClick.AddListener(() => Toggle(row));
        row.WeaponButton.onClick.AddListener(() => OpenWeaponPicker(row));
        row.PersonalityButton.onClick.AddListener(() => OpenPersonalityPickerForRow(row));
        return row;
    }

    private int FindPersonalityIndex(CombatAiPersonalityProfile profile)
    {
        if (profile == null) return FindStandardPersonalityIndex();

        for (int i = 0; i < _personalityOptions.Count; i++)
        {
            CombatAiPersonalityProfile option = _personalityOptions[i];
            if (option != null && option.Kind == profile.Kind) return i;
        }

        return FindStandardPersonalityIndex();
    }

    private Button CreateButton(
        Transform parent,
        string objectName,
        float width,
        float height = -1f,
        UnityEngine.Events.UnityAction onClick = null)
    {
        Button button = Instantiate(_characterItemPrefab, parent);
        if (!string.IsNullOrEmpty(objectName)) button.name = objectName;
        LayoutElement layout = button.GetComponent<LayoutElement>() ?? button.gameObject.AddComponent<LayoutElement>();
        layout.preferredWidth = width;
        layout.flexibleWidth = 0f;
        if (height > 0f) layout.preferredHeight = height;
        HideIndicator(button);
        if (onClick != null) button.onClick.AddListener(onClick);
        return button;
    }

    private void Toggle(SelectionRow row)
    {
        row.Selected = !row.Selected;
        Refresh();
    }

    private void OpenWeaponPicker(SelectionRow row)
    {
        if (_weaponOptions.Count == 0) return;

        EnsurePicker();
        ClearPickerOptions();
        SetPickerTitle("武器を選択");
        SetPickerDescription("一覧から武器を選んでください。");

        for (int i = 0; i < _weaponOptions.Count; i++)
        {
            int index = i;
            WeaponConfig weapon = _weaponOptions[i];
            string label = GetWeaponName(weapon);
            if (index == row.WeaponIndex) label = "■ " + label;
            AddPickerOption(label, () =>
            {
                row.WeaponIndex = index;
                RefreshRow(row);
                ClosePicker();
            });
        }

        ShowPicker();
    }

    private void OpenPersonalityPickerForRow(SelectionRow row)
    {
        OpenPersonalityPicker(
            "性格を選択",
            row.PersonalityIndex,
            includeNone: false,
            onSelected: index =>
            {
                row.PersonalityIndex = index;
                RefreshRow(row);
            });
    }

    private void OpenPersonalityPicker(
        string title,
        int selectedIndex,
        bool includeNone,
        Action<int> onSelected)
    {
        if (!includeNone && _personalityOptions.Count == 0) return;

        EnsurePicker();
        ClearPickerOptions();
        SetPickerTitle(title);

        if (includeNone)
        {
            bool isNone = !CombatAiPersonalityHighlight.HasHighlight;
            const string noneDescription = "性格ハイライトを使いません。";
            AddPickerOption(
                isNone ? "■ なし" : "なし",
                () =>
                {
                    onSelected?.Invoke(-1);
                    ClosePicker();
                },
                () => SetPickerDescription(noneDescription));
        }

        CombatAiPersonalityKind? highlightKind = CombatAiPersonalityHighlight.Kind;
        for (int i = 0; i < _personalityOptions.Count; i++)
        {
            int index = i;
            CombatAiPersonalityProfile personality = _personalityOptions[i];
            if (personality == null) continue;

            string name = personality.DisplayNameJapanese;
            bool selected = includeNone
                ? highlightKind.HasValue && personality.Kind == highlightKind.Value
                : index == selectedIndex;
            string description = personality.BehaviorDescriptionJapanese;
            AddPickerOption(
                selected ? "■ " + name : name,
                () =>
                {
                    onSelected?.Invoke(index);
                    ClosePicker();
                },
                () => SetPickerDescription(description));
        }

        CombatAiPersonalityProfile initial = includeNone
            ? (highlightKind.HasValue ? FindPersonalityByKind(highlightKind.Value) : null)
            : GetOption(_personalityOptions, selectedIndex);
        SetPickerDescription(initial != null
            ? initial.BehaviorDescriptionJapanese
            : includeNone
                ? "性格ハイライトを使いません。"
                : "性格を選ぶと、ここで挙動の説明が表示されます。");
        ShowPicker();
    }

    private CombatAiPersonalityProfile FindPersonalityByKind(CombatAiPersonalityKind kind)
    {
        for (int i = 0; i < _personalityOptions.Count; i++)
        {
            CombatAiPersonalityProfile option = _personalityOptions[i];
            if (option != null && option.Kind == kind) return option;
        }

        return null;
    }

    private void EnsurePicker()
    {
        if (_pickerRoot != null) return;

        RectTransform host = transform as RectTransform;
        GameObject overlay = new GameObject(
            "SelectionOptionPicker",
            typeof(RectTransform),
            typeof(Image),
            typeof(Button));
        _pickerRoot = overlay.GetComponent<RectTransform>();
        _pickerRoot.SetParent(host != null ? host : transform, false);
        Stretch(_pickerRoot);
        Image dim = overlay.GetComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.55f);
        Button dismiss = overlay.GetComponent<Button>();
        dismiss.transition = Selectable.Transition.None;
        dismiss.onClick.AddListener(ClosePicker);

        GameObject panelObject = new GameObject(
            "Panel",
            typeof(RectTransform),
            typeof(Image),
            typeof(VerticalLayoutGroup),
            typeof(Button));
        RectTransform panel = panelObject.GetComponent<RectTransform>();
        panel.SetParent(_pickerRoot, false);
        panel.anchorMin = new Vector2(0.5f, 0.5f);
        panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.pivot = new Vector2(0.5f, 0.5f);
        panel.sizeDelta = new Vector2(720f, 560f);
        panelObject.GetComponent<Image>().color = new Color(0.08f, 0.1f, 0.14f, 0.96f);
        panelObject.GetComponent<Button>().transition = Selectable.Transition.None;
        VerticalLayoutGroup panelLayout = panelObject.GetComponent<VerticalLayoutGroup>();
        panelLayout.padding = new RectOffset(16, 16, 16, 16);
        panelLayout.spacing = 10f;
        panelLayout.childControlWidth = true;
        panelLayout.childControlHeight = true;
        panelLayout.childForceExpandWidth = true;
        panelLayout.childForceExpandHeight = false;

        TMP_Text title = Instantiate(_selectionCountText, panel);
        title.name = "PickerTitle";
        title.text = "選択";
        title.alignment = TextAlignmentOptions.Center;
        title.fontSize = 30f;
        LayoutElement titleLayout = title.gameObject.GetComponent<LayoutElement>() ??
                                    title.gameObject.AddComponent<LayoutElement>();
        titleLayout.preferredHeight = 36f;
        _pickerTitle = title;

        _pickerDescription = Instantiate(_selectionCountText, panel);
        _pickerDescription.name = "PickerDescription";
        _pickerDescription.alignment = TextAlignmentOptions.Left;
        _pickerDescription.fontSize = 24f;
        _pickerDescription.enableWordWrapping = true;
        _pickerDescription.text = string.Empty;
        LayoutElement descriptionLayout = _pickerDescription.gameObject.GetComponent<LayoutElement>() ??
                                          _pickerDescription.gameObject.AddComponent<LayoutElement>();
        descriptionLayout.preferredHeight = 72f;

        GameObject scrollObject = new GameObject(
            "Scroll",
            typeof(RectTransform),
            typeof(Image),
            typeof(ScrollRect),
            typeof(LayoutElement));
        RectTransform scrollRectTransform = scrollObject.GetComponent<RectTransform>();
        scrollRectTransform.SetParent(panel, false);
        scrollObject.GetComponent<Image>().color = new Color(0.05f, 0.06f, 0.08f, 0.9f);
        LayoutElement scrollLayout = scrollObject.GetComponent<LayoutElement>();
        scrollLayout.preferredHeight = 380f;
        scrollLayout.flexibleHeight = 1f;

        GameObject viewportObject = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
        RectTransform viewport = viewportObject.GetComponent<RectTransform>();
        viewport.SetParent(scrollRectTransform, false);
        Stretch(viewport);
        viewportObject.GetComponent<Image>().color = Color.white;
        viewportObject.GetComponent<Mask>().showMaskGraphic = false;

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
        contentLayout.padding = new RectOffset(8, 8, 8, 8);
        contentLayout.childControlWidth = true;
        contentLayout.childControlHeight = true;
        contentLayout.childForceExpandWidth = true;
        contentLayout.childForceExpandHeight = false;
        ContentSizeFitter fitter = contentObject.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        _pickerContent = content;

        ScrollRect scroll = scrollObject.GetComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;

        Button closeButton = CreateButton(panel, "ClosePickerButton", 0f, 44f, ClosePicker);
        closeButton.GetComponent<LayoutElement>().flexibleWidth = 1f;
        SetButtonLabel(closeButton, "閉じる");

        _pickerRoot.gameObject.SetActive(false);
    }

    private void ClearPickerOptions()
    {
        if (_pickerContent == null) return;
        for (int i = _pickerContent.childCount - 1; i >= 0; i--)
        {
            Destroy(_pickerContent.GetChild(i).gameObject);
        }
    }

    private void AddPickerOption(string label, Action onClick, Action onHighlight = null)
    {
        if (_pickerContent == null || _characterItemPrefab == null) return;

        Button button = CreateButton(_pickerContent, null, 0f, 56f, () => onClick?.Invoke());
        button.GetComponent<LayoutElement>().flexibleWidth = 1f;
        SetButtonLabel(button, label);
        if (onHighlight != null)
        {
            button.gameObject.AddComponent<EventTriggerProxy>().OnHighlighted = onHighlight;
        }
    }

    private void SetPickerTitle(string title)
    {
        if (_pickerTitle != null) _pickerTitle.text = title;
    }

    private void SetPickerDescription(string description)
    {
        if (_pickerDescription != null)
        {
            _pickerDescription.text = description ?? string.Empty;
        }
    }

    private void ShowPicker()
    {
        if (_pickerRoot == null) return;
        _pickerRoot.SetAsLastSibling();
        _pickerRoot.gameObject.SetActive(true);
    }

    private void ClosePicker()
    {
        if (_pickerRoot != null)
        {
            _pickerRoot.gameObject.SetActive(false);
        }

        ClearPickerOptions();
    }

    private void ConfirmSelection()
    {
        ClosePicker();
        SetDetailSettingsOpen(false);
        List<CombatParticipantSetup> allies = BuildSetups(_allyRows);
        List<CombatParticipantSetup> enemies = BuildSetups(_enemyRows);
        if (allies.Count == 0 || enemies.Count == 0) return;

        _confirmed?.Invoke(allies, enemies);
    }

    private List<CombatParticipantSetup> BuildSetups(List<SelectionRow> rows)
    {
        var setups = new List<CombatParticipantSetup>();
        for (int i = 0; i < rows.Count; i++)
        {
            SelectionRow row = rows[i];
            if (!row.Selected) continue;

            setups.Add(new CombatParticipantSetup(
                row.Character,
                GetOption(_weaponOptions, row.WeaponIndex),
                GetOption(_personalityOptions, row.PersonalityIndex)));
        }

        return setups;
    }

    private void Refresh()
    {
        RefreshRows(_allyRows);
        RefreshRows(_enemyRows);
        RefreshHighlightButton();
        RefreshDetailSettingsButton();
        RefreshSelectionCountText();

        int allyCount = CountSelected(_allyRows);
        int enemyCount = CountSelected(_enemyRows);
        if (_startBattleButton != null)
        {
            _startBattleButton.interactable = allyCount > 0 && enemyCount > 0 &&
                                              _weaponOptions.Count > 0 && _personalityOptions.Count > 0;
        }
    }

    private void RefreshSelectionCountText()
    {
        if (_selectionCountText == null) return;

        int allyCount = CountSelected(_allyRows);
        int enemyCount = CountSelected(_enemyRows);
        if (_detailSettingsOpen)
        {
            _selectionCountText.text = $"詳細設定（敵） 敵 {enemyCount}人";
        }
        else
        {
            _selectionCountText.text = $"味方 {allyCount}人 / 敵 {enemyCount}人（標準編成）";
        }
    }

    private void RefreshRows(List<SelectionRow> rows)
    {
        for (int i = 0; i < rows.Count; i++)
        {
            RefreshRow(rows[i]);
        }
    }

    private void RefreshRow(SelectionRow row)
    {
        SetButtonLabel(row.CharacterButton, $"{(row.Selected ? "■" : "□")} {row.Character.DisplayName}");
        SetButtonLabel(row.WeaponButton, $"武器: {GetWeaponName(GetOption(_weaponOptions, row.WeaponIndex))}");
        CombatAiPersonalityProfile personality = GetOption(_personalityOptions, row.PersonalityIndex);
        SetButtonLabel(row.PersonalityButton, $"性格: {(personality != null ? personality.DisplayNameJapanese : "未設定")}");

        Transform indicator = row.CharacterButton.transform.Find("SelectedIndicator");
        if (indicator != null)
        {
            indicator.gameObject.SetActive(row.Selected);
        }
    }

    private static void SetButtonLabel(Button button, string value)
    {
        TMP_Text label = button != null ? button.GetComponentInChildren<TMP_Text>(includeInactive: true) : null;
        if (label != null)
        {
            label.text = value;
            label.fontSize = 26f;
            label.enableAutoSizing = false;
        }
    }

    private static void HideIndicator(Button button)
    {
        Transform indicator = button.transform.Find("SelectedIndicator");
        if (indicator != null)
        {
            indicator.gameObject.SetActive(false);
        }
    }

    private static string GetWeaponName(WeaponConfig weapon)
    {
        if (weapon == null) return "未設定";
        return weapon.Kind switch
        {
            WeaponKind.Sword => "剣",
            WeaponKind.Shield => "盾",
            WeaponKind.Wand => "杖",
            WeaponKind.Grimoire => "魔導書",
            WeaponKind.Bible => "聖書",
            WeaponKind.Rosary => "ロザリオ",
            _ => "素手",
        };
    }

    private static int CountSelected(List<SelectionRow> rows)
    {
        int count = 0;
        for (int i = 0; i < rows.Count; i++)
        {
            if (rows[i].Selected) count++;
        }

        return count;
    }

    private static int FindOptionIndex<T>(List<T> options, T value) where T : UnityEngine.Object
    {
        int index = value != null ? options.IndexOf(value) : -1;
        return index >= 0 ? index : 0;
    }

    private static T GetOption<T>(List<T> options, int index) where T : UnityEngine.Object
    {
        return index >= 0 && index < options.Count ? options[index] : null;
    }

    private static void RemoveNullAndDuplicateOptions<T>(List<T> options) where T : UnityEngine.Object
    {
        var unique = new HashSet<T>();
        for (int i = options.Count - 1; i >= 0; i--)
        {
            if (options[i] == null || !unique.Add(options[i]))
            {
                options.RemoveAt(i);
            }
        }
    }

    private static void ConfigureListLayout(RectTransform target)
    {
        VerticalLayoutGroup layout = target.GetComponent<VerticalLayoutGroup>();
        if (layout != null)
        {
            layout.enabled = true;
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
        }

        ContentSizeFitter fitter = target.GetComponent<ContentSizeFitter>();
        if (fitter != null)
        {
            fitter.enabled = false;
        }
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private sealed class SelectionRow
    {
        public Character Character;
        public Button CharacterButton;
        public Button WeaponButton;
        public Button PersonalityButton;
        public int WeaponIndex;
        public int PersonalityIndex;
        public bool Selected;
    }

    private sealed class EventTriggerProxy : MonoBehaviour, IPointerEnterHandler
    {
        public Action OnHighlighted;

        public void OnPointerEnter(PointerEventData eventData)
        {
            OnHighlighted?.Invoke();
        }
    }
}
