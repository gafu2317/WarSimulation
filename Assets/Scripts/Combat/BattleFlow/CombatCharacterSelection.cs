using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class CombatCharacterSelection : MonoBehaviour
{
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

    public IReadOnlyList<WeaponConfig> WeaponOptions => _weaponOptions;

    public void Initialize(
        IReadOnlyList<Character> allyCandidates,
        IReadOnlyList<Character> enemyCandidates,
        Action<IReadOnlyList<CombatParticipantSetup>, IReadOnlyList<CombatParticipantSetup>> confirmed)
    {
        _confirmed = confirmed;
        RemoveNullAndDuplicateOptions(_weaponOptions);
        RemoveNullAndDuplicateOptions(_personalityOptions);
        AddBuiltInPersonalityOptions();
        RebuildLayout(allyCandidates, enemyCandidates);
        ResetSelection();
    }

    public void ResetSelection()
    {
        SetAllSelected(_allyRows, true);
        SetAllSelected(_enemyRows, true);
        Refresh();
    }

    private void Awake()
    {
        _startBattleButton?.onClick.AddListener(ConfirmSelection);
    }

    private void OnDestroy()
    {
        _startBattleButton?.onClick.RemoveListener(ConfirmSelection);
        for (int i = 0; i < _builtInPersonalityOptions.Count; i++)
        {
            if (_builtInPersonalityOptions[i] != null) Destroy(_builtInPersonalityOptions[i]);
        }
    }

    private void AddBuiltInPersonalityOptions()
    {
        if (_builtInPersonalityOptions.Count > 0) return;

        _builtInPersonalityOptions.AddRange(CombatAiPersonalityProfile.CreateBuiltInProfiles());
        _personalityOptions.AddRange(_builtInPersonalityOptions);
    }

    private void RebuildLayout(
        IReadOnlyList<Character> allyCandidates,
        IReadOnlyList<Character> enemyCandidates)
    {
        _allyRows.Clear();
        _enemyRows.Clear();
        if (_characterList == null || _characterItemPrefab == null || _selectionCountText == null) return;

        ConfigureListLayout(_characterList);
        for (int i = _characterList.childCount - 1; i >= 0; i--)
        {
            Destroy(_characterList.GetChild(i).gameObject);
        }

        _characterList.sizeDelta = new Vector2(1080f, 520f);
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
        BuildRows(allyColumn, allyCandidates, _allyRows);
        BuildRows(enemyColumn, enemyCandidates, _enemyRows);
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
        columnObject.GetComponent<LayoutElement>().preferredWidth = 520f;

        TMP_Text header = Instantiate(_selectionCountText, column);
        header.name = $"{objectName}Title";
        header.text = title;
        header.alignment = TextAlignmentOptions.Center;
        LayoutElement headerLayout = header.gameObject.GetComponent<LayoutElement>() ??
                                     header.gameObject.AddComponent<LayoutElement>();
        headerLayout.preferredHeight = 40f;
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
        rowObject.GetComponent<LayoutElement>().preferredHeight = 56f;

        var row = new SelectionRow
        {
            Character = character,
            WeaponIndex = FindOptionIndex(_weaponOptions, character.EquippedWeaponConfig),
            PersonalityIndex = FindOptionIndex(_personalityOptions, character.PersonalityProfile),
            CharacterButton = CreateButton(rowObject.transform, 200f),
            WeaponButton = CreateButton(rowObject.transform, 140f),
            PersonalityButton = CreateButton(rowObject.transform, 160f),
        };

        row.CharacterButton.onClick.AddListener(() => Toggle(row));
        row.WeaponButton.onClick.AddListener(() => CycleWeapon(row));
        row.PersonalityButton.onClick.AddListener(() => CyclePersonality(row));
        HideIndicator(row.WeaponButton);
        HideIndicator(row.PersonalityButton);
        return row;
    }

    private Button CreateButton(Transform parent, float width)
    {
        Button button = Instantiate(_characterItemPrefab, parent);
        LayoutElement layout = button.GetComponent<LayoutElement>() ?? button.gameObject.AddComponent<LayoutElement>();
        layout.preferredWidth = width;
        layout.flexibleWidth = 0f;
        return button;
    }

    private void Toggle(SelectionRow row)
    {
        row.Selected = !row.Selected;
        Refresh();
    }

    private void CycleWeapon(SelectionRow row)
    {
        if (_weaponOptions.Count == 0) return;
        row.WeaponIndex = (row.WeaponIndex + 1) % _weaponOptions.Count;
        RefreshRow(row);
    }

    private void CyclePersonality(SelectionRow row)
    {
        if (_personalityOptions.Count == 0) return;
        row.PersonalityIndex = (row.PersonalityIndex + 1) % _personalityOptions.Count;
        RefreshRow(row);
    }

    private void ConfirmSelection()
    {
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
        int allyCount = CountSelected(_allyRows);
        int enemyCount = CountSelected(_enemyRows);

        if (_selectionCountText != null)
        {
            _selectionCountText.text = $"味方 {allyCount}人 / 敵 {enemyCount}人";
        }

        if (_startBattleButton != null)
        {
            _startBattleButton.interactable = allyCount > 0 && enemyCount > 0 &&
                                              _weaponOptions.Count > 0 && _personalityOptions.Count > 0;
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
        SetButtonLabel(row.PersonalityButton, $"性格: {GetPersonalityName(GetOption(_personalityOptions, row.PersonalityIndex))}");

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
            label.fontSize = 18f;
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

    private static string GetPersonalityName(CombatAiPersonalityProfile personality)
    {
        return personality != null ? personality.DisplayNameJapanese : "未設定";
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

    private static void SetAllSelected(List<SelectionRow> rows, bool selected)
    {
        for (int i = 0; i < rows.Count; i++)
        {
            rows[i].Selected = selected;
        }
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
            layout.spacing = 0f;
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
}
