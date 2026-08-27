using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class CombatCharacterSelection : MonoBehaviour
{
    private const int MaxPartyDisplayCount = 10;
    private const int PartyGridRowCount = 5;
    private const float CharacterListWidth = 1600f;
    private static readonly float[] MovementSpeedMultipliers = { 1f, 2f, 4f, 6f };

    private static readonly WeaponKind[] DefaultPartyWeapons =
    {
        WeaponKind.Sword,
        WeaponKind.Sword,
        WeaponKind.Wand,
        WeaponKind.Rosary,
        WeaponKind.Shield,
    };

    private static readonly WeaponKind[] DefaultEnemyWeapons =
    {
        WeaponKind.Sword,
        WeaponKind.Wand,
        WeaponKind.Wand,
        WeaponKind.Shield,
        WeaponKind.Rosary,
    };

    private static readonly CombatAiPersonalityKind[] DefaultEnemyPersonalities =
    {
        CombatAiPersonalityKind.Reckless,
        CombatAiPersonalityKind.Reckless,
        CombatAiPersonalityKind.BattleJunkie,
        CombatAiPersonalityKind.Devoted,
        CombatAiPersonalityKind.Devoted,
    };

    private static readonly EnemyPresetDefinition EnemyStandardPreset = new(
        "テテSnレ",
        new[]
        {
            WeaponKind.Wand,
            WeaponKind.Wand,
            WeaponKind.Bible,
            WeaponKind.Rosary,
            WeaponKind.Grimoire,
        },
        new[]
        {
            CombatAiPersonalityKind.Neutral,
            CombatAiPersonalityKind.Neutral,
            CombatAiPersonalityKind.Devoted,
            CombatAiPersonalityKind.Devoted,
            CombatAiPersonalityKind.BattleJunkie,
        });

    private static readonly EnemyPresetDefinition[] EnemyTopPresets =
    {
        new(
            "0通常",
            new[] { WeaponKind.Sword, WeaponKind.Sword, WeaponKind.Sword, WeaponKind.Wand, WeaponKind.Rosary },
            new[]
            {
                CombatAiPersonalityKind.Reckless,
                CombatAiPersonalityKind.Reckless,
                CombatAiPersonalityKind.Reckless,
                CombatAiPersonalityKind.BattleJunkie,
                CombatAiPersonalityKind.Lonely,
            }),
        new(
            "0逆",
            new[] { WeaponKind.Wand, WeaponKind.Wand, WeaponKind.Wand, WeaponKind.Grimoire, WeaponKind.Rosary },
            new[]
            {
                CombatAiPersonalityKind.BattleJunkie,
                CombatAiPersonalityKind.BattleJunkie,
                CombatAiPersonalityKind.BattleJunkie,
                CombatAiPersonalityKind.Lonely,
                CombatAiPersonalityKind.Lonely,
            }),
        new(
            "3通常",
            new[] { WeaponKind.Sword, WeaponKind.Sword, WeaponKind.Rosary, WeaponKind.Rosary, WeaponKind.Rosary },
            new[]
            {
                CombatAiPersonalityKind.Cunning,
                CombatAiPersonalityKind.Cunning,
                CombatAiPersonalityKind.Devoted,
                CombatAiPersonalityKind.Devoted,
                CombatAiPersonalityKind.Devoted,
            }),
        new(
            "3逆",
            new[] { WeaponKind.Sword, WeaponKind.Sword, WeaponKind.Sword, WeaponKind.Grimoire, WeaponKind.Grimoire },
            new[]
            {
                CombatAiPersonalityKind.Reckless,
                CombatAiPersonalityKind.Reckless,
                CombatAiPersonalityKind.Reckless,
                CombatAiPersonalityKind.Devoted,
                CombatAiPersonalityKind.Devoted,
            }),
        new(
            "5通常",
            new[] { WeaponKind.Sword, WeaponKind.Sword, WeaponKind.Bible, WeaponKind.Bible, WeaponKind.Rosary },
            new[]
            {
                CombatAiPersonalityKind.Cunning,
                CombatAiPersonalityKind.Reckless,
                CombatAiPersonalityKind.Devoted,
                CombatAiPersonalityKind.Devoted,
                CombatAiPersonalityKind.Devoted,
            }),
        new(
            "5逆",
            new[] { WeaponKind.Wand, WeaponKind.Wand, WeaponKind.Grimoire, WeaponKind.Rosary, WeaponKind.Rosary },
            new[]
            {
                CombatAiPersonalityKind.BattleJunkie,
                CombatAiPersonalityKind.BattleJunkie,
                CombatAiPersonalityKind.Devoted,
                CombatAiPersonalityKind.Devoted,
                CombatAiPersonalityKind.Devoted,
            }),
        new(
            "6通常",
            new[] { WeaponKind.Sword, WeaponKind.Sword, WeaponKind.Sword, WeaponKind.Shield, WeaponKind.Shield },
            new[]
            {
                CombatAiPersonalityKind.Reckless,
                CombatAiPersonalityKind.Reckless,
                CombatAiPersonalityKind.Reckless,
                CombatAiPersonalityKind.Lonely,
                CombatAiPersonalityKind.Lonely,
            }),
        new(
            "6逆",
            new[] { WeaponKind.Sword, WeaponKind.Wand, WeaponKind.Wand, WeaponKind.Bible, WeaponKind.Rosary },
            new[]
            {
                CombatAiPersonalityKind.Cunning,
                CombatAiPersonalityKind.Cunning,
                CombatAiPersonalityKind.Reckless,
                CombatAiPersonalityKind.Lonely,
                CombatAiPersonalityKind.Devoted,
            }),
        new(
            "9通常",
            new[] { WeaponKind.Wand, WeaponKind.Wand, WeaponKind.Wand, WeaponKind.Grimoire, WeaponKind.Rosary },
            new[]
            {
                CombatAiPersonalityKind.BattleJunkie,
                CombatAiPersonalityKind.BattleJunkie,
                CombatAiPersonalityKind.BattleJunkie,
                CombatAiPersonalityKind.Lonely,
                CombatAiPersonalityKind.Lonely,
            }),
        new(
            "9逆",
            new[] { WeaponKind.Sword, WeaponKind.Wand, WeaponKind.Wand, WeaponKind.Wand, WeaponKind.Bible },
            new[]
            {
                CombatAiPersonalityKind.Reckless,
                CombatAiPersonalityKind.Cunning,
                CombatAiPersonalityKind.Reckless,
                CombatAiPersonalityKind.Reckless,
                CombatAiPersonalityKind.Devoted,
            }),
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
    private Button _movementSpeedButton;
    private Button _bulkWeaponButton;
    private Button _bulkPersonalityButton;
    private Button _enemyFormationButton;
    private Button _stonePositionButton;
    private Button _battleUiModeButton;
    private Button _enemyPresetDefaultButton;
    private Button _enemyPresetNeutralButton;
    private Button _enemyPresetTopButton;
    private Button _formationCodeApplyButton;
    private Button _formationCodeCopyButton;
    private Button _formationCodePasteButton;
    private TMP_InputField _formationCodeInput;
    private TMP_Text _formationCodeStatus;
    private GameObject _enemyPresetRowRoot;
    private GameObject _debugRowRoot;
    private Button _debugCharacterRoutesButton;
    private Button _debugAssaultRoutesButton;
    private Button _debugAiLabelsButton;
    private Button _debugVisionButton;
    private Button _debugCharacterRoutesSettingsButton;
    private Button _debugAiLabelsSettingsButton;
    private Button _debugVisionSettingsButton;
    private RectTransform _headerRoot;
    private enum DebugSettingsKind
    {
        CharacterRoutes,
        AiLabels,
        Vision,
    }
    private GameObject _allyColumnRoot;
    private GameObject _enemyColumnRoot;
    private LayoutElement _teamSelectionsLayout;
    private RectTransform _pickerRoot;
    private RectTransform _pickerPanel;
    private RectTransform _pickerDetailsRoot;
    private TMP_Text _pickerDescription;
    private TMP_Text _pickerTitle;
    private Transform _pickerContent;
    private Transform _pickerDetailsContent;
    private GridLayoutGroup _pickerGridLayout;
    private LayoutElement _pickerOptionsLayout;
    private LayoutElement _pickerDetailsLayout;
    private bool _detailSettingsOpen;
    private bool _stonePositionReversed;
    private bool _externalStartAllowed = true;
    private int _movementSpeedMultiplierIndex;

    public IReadOnlyList<WeaponConfig> WeaponOptions => _weaponOptions;
    public bool IsStonePositionReversed => _stonePositionReversed;
    public float MovementSpeedMultiplier => MovementSpeedMultipliers[_movementSpeedMultiplierIndex];
    public event Action<bool> StonePositionReversedChanged;

    public void SetExternalStartAllowed(bool allowed)
    {
        if (_externalStartAllowed == allowed) return;
        _externalStartAllowed = allowed;
        Refresh();
    }

    public void Initialize(
        IReadOnlyList<Character> allyCandidates,
        IReadOnlyList<Character> enemyCandidates,
        Action<IReadOnlyList<CombatParticipantSetup>, IReadOnlyList<CombatParticipantSetup>> confirmed)
    {
        _confirmed = confirmed;
        RemoveNullAndDuplicateOptions(_weaponOptions);
        NormalizePersonalityOptions();
        AddBuiltInPersonalityOptions();
        RebuildLayout(allyCandidates, enemyCandidates);
        ConfigureSelectionCountText();
        ApplyDefaultParty(_allyRows, useEnemyPersonalities: false, useEnemyWeapons: false);
        ApplyDefaultParty(_enemyRows, useEnemyPersonalities: true, useEnemyWeapons: true);
        Refresh();
    }

    private void ApplyDefaultParty(
        List<SelectionRow> rows,
        bool useEnemyPersonalities,
        bool useEnemyWeapons)
    {
        WeaponKind[] defaultWeapons = useEnemyWeapons ? DefaultEnemyWeapons : DefaultPartyWeapons;
        for (int i = 0; i < rows.Count; i++)
        {
            SelectionRow row = rows[i];
            bool selected = i < defaultWeapons.Length;
            row.Selected = selected;
            if (!selected) continue;

            int weaponIndex = FindWeaponIndex(defaultWeapons[i]);
            if (weaponIndex >= 0)
            {
                row.WeaponIndex = weaponIndex;
            }

            CombatAiPersonalityKind personalityKind = useEnemyPersonalities && i < DefaultEnemyPersonalities.Length
                ? DefaultEnemyPersonalities[i]
                : CombatAiPersonalityKind.Neutral;
            int personalityIndex = FindOrAddPersonalityIndex(personalityKind);
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

    private int FindOrAddPersonalityIndex(CombatAiPersonalityKind kind)
    {
        int index = FindPersonalityIndex(kind);
        if (index >= 0) return index;

        CombatAiPersonalityProfile created = CombatAiPersonalityProfile.CreateBuiltInProfile(kind);
        _builtInPersonalityOptions.Add(created);
        _personalityOptions.Add(created);
        return _personalityOptions.Count - 1;
    }

    private void Awake()
    {
        _startBattleButton?.onClick.AddListener(ConfirmSelection);
    }

    private void OnDestroy()
    {
        _startBattleButton?.onClick.RemoveListener(ConfirmSelection);
        ClearToolbarButtons();
        ClosePicker();
        for (int i = 0; i < _builtInPersonalityOptions.Count; i++)
        {
            DestroyGeneratedObject(_builtInPersonalityOptions[i]);
        }
    }

    private void AddBuiltInPersonalityOptions()
    {
        if (_builtInPersonalityOptions.Count > 0) return;

        List<CombatAiPersonalityProfile> builtIns = CombatAiPersonalityProfile.CreateBuiltInProfiles();
        for (int i = 0; i < builtIns.Count; i++)
        {
            CombatAiPersonalityProfile profile = builtIns[i];
            if (FindPersonalityIndex(profile.Kind) >= 0)
            {
                DestroyGeneratedObject(profile);
                continue;
            }

            _builtInPersonalityOptions.Add(profile);
            _personalityOptions.Add(profile);
        }
    }

    private void NormalizePersonalityOptions()
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
            DestroyGeneratedObject(_characterList.GetChild(i).gameObject);
        }

        _characterList.sizeDelta = new Vector2(CharacterListWidth, 700f);
        CreateHeaderControls(_characterList);

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
        _teamSelectionsLayout = teamsObject.GetComponent<LayoutElement>();
        _teamSelectionsLayout.preferredHeight = 500f;

        RectTransform allyColumn = CreateTeamColumn(teams, "AllySelection", "味方");
        RectTransform enemyColumn = CreateTeamColumn(teams, "EnemySelection", "敵");
        _allyColumnRoot = allyColumn.gameObject;
        _enemyColumnRoot = enemyColumn.gameObject;
        RectTransform allyRows = CreateRowsGrid(allyColumn, "AllyRows");
        RectTransform enemyRows = CreateRowsGrid(enemyColumn, "EnemyRows");
        BuildRows(allyRows, allyCandidates, _allyRows);
        BuildRows(enemyRows, enemyCandidates, _enemyRows);
        SetDetailSettingsOpen(false);
    }

    private void ClearToolbarButtons()
    {
        if (_movementSpeedButton != null)
        {
            _movementSpeedButton.onClick.RemoveListener(CycleMovementSpeed);
            _movementSpeedButton = null;
        }

        if (_bulkWeaponButton != null)
        {
            _bulkWeaponButton.onClick.RemoveListener(OpenBulkWeaponPicker);
            _bulkWeaponButton = null;
        }

        if (_bulkPersonalityButton != null)
        {
            _bulkPersonalityButton.onClick.RemoveListener(OpenBulkPersonalityPicker);
            _bulkPersonalityButton = null;
        }

        if (_enemyFormationButton != null)
        {
            _enemyFormationButton.onClick.RemoveListener(ToggleEnemyFormation);
            _enemyFormationButton = null;
        }

        if (_stonePositionButton != null)
        {
            _stonePositionButton.onClick.RemoveListener(ToggleStonePositionReversed);
            _stonePositionButton = null;
        }

        if (_battleUiModeButton != null)
        {
            _battleUiModeButton.onClick.RemoveListener(ToggleBattleUiMode);
            _battleUiModeButton = null;
        }

        if (_enemyPresetDefaultButton != null)
        {
            _enemyPresetDefaultButton.onClick.RemoveListener(ApplyEnemyPresetDefault);
            _enemyPresetDefaultButton = null;
        }

        if (_enemyPresetNeutralButton != null)
        {
            _enemyPresetNeutralButton.onClick.RemoveListener(ApplyEnemyPresetStandard);
            _enemyPresetNeutralButton = null;
        }

        if (_enemyPresetTopButton != null)
        {
            _enemyPresetTopButton.onClick.RemoveListener(OpenEnemyTopPresetPicker);
            _enemyPresetTopButton = null;
        }

        if (_formationCodeApplyButton != null)
        {
            _formationCodeApplyButton.onClick.RemoveListener(ApplyFormationCode);
            _formationCodeApplyButton = null;
        }

        if (_formationCodeCopyButton != null)
        {
            _formationCodeCopyButton.onClick.RemoveListener(CopyFormationCode);
            _formationCodeCopyButton = null;
        }

        if (_formationCodePasteButton != null)
        {
            _formationCodePasteButton.onClick.RemoveListener(PasteFormationCode);
            _formationCodePasteButton = null;
        }

        _formationCodeInput = null;
        _formationCodeStatus = null;

        _enemyPresetRowRoot = null;
        _debugRowRoot = null;
        _teamSelectionsLayout = null;

        ClearDebugToggle(_debugCharacterRoutesButton);
        ClearDebugToggle(_debugAssaultRoutesButton);
        ClearDebugToggle(_debugAiLabelsButton);
        ClearDebugToggle(_debugVisionButton);
        ClearDebugToggle(_debugCharacterRoutesSettingsButton);
        ClearDebugToggle(_debugAiLabelsSettingsButton);
        ClearDebugToggle(_debugVisionSettingsButton);
        _debugCharacterRoutesButton = null;
        _debugAssaultRoutesButton = null;
        _debugAiLabelsButton = null;
        _debugVisionButton = null;
        _debugCharacterRoutesSettingsButton = null;
        _debugAiLabelsSettingsButton = null;
        _debugVisionSettingsButton = null;
        _headerRoot = null;
    }

    private static void ClearDebugToggle(Button button)
    {
        if (button == null) return;
        button.onClick.RemoveAllListeners();
    }

    private void CreateHeaderControls(RectTransform parent)
    {
        GameObject headerObject = new GameObject(
            "SelectionHeader",
            typeof(RectTransform),
            typeof(VerticalLayoutGroup),
            typeof(LayoutElement));
        _headerRoot = headerObject.GetComponent<RectTransform>();
        _headerRoot.SetParent(parent, false);
        VerticalLayoutGroup headerLayout = headerObject.GetComponent<VerticalLayoutGroup>();
        headerLayout.spacing = 8f;
        headerLayout.childControlWidth = true;
        headerLayout.childControlHeight = true;
        headerLayout.childForceExpandWidth = true;
        headerLayout.childForceExpandHeight = false;
        headerObject.GetComponent<LayoutElement>().preferredHeight = 148f;

        RectTransform actionRow = CreateHorizontalRow(_headerRoot, "ActionRow", 48f, spacing: 12f);
        _movementSpeedButton = CreateButton(actionRow, "MovementSpeedButton", 220f, 48f, CycleMovementSpeed);
        _bulkWeaponButton = CreateButton(actionRow, "BulkWeaponButton", 220f, 48f, OpenBulkWeaponPicker);
        _bulkPersonalityButton = CreateButton(actionRow, "BulkPersonalityButton", 220f, 48f, OpenBulkPersonalityPicker);
        _stonePositionButton = CreateButton(actionRow, "StonePositionButton", 220f, 48f, ToggleStonePositionReversed);
        _battleUiModeButton = CreateButton(actionRow, "BattleUiModeButton", 220f, 48f, ToggleBattleUiMode);
        _enemyFormationButton = CreateButton(actionRow, "EnemyFormationButton", 200f, 48f, ToggleEnemyFormation);
        RefreshMovementSpeedButton();
        SetButtonLabel(_bulkWeaponButton, "武器一括変更");
        SetButtonLabel(_bulkPersonalityButton, "性格一括変更");
        ConfigureToolbarLabel(_movementSpeedButton, 24f);
        ConfigureToolbarLabel(_bulkWeaponButton, 24f);
        ConfigureToolbarLabel(_bulkPersonalityButton, 24f);
        ConfigureToolbarLabel(_enemyFormationButton, 24f);
        ConfigureToolbarLabel(_stonePositionButton, 24f);
        ConfigureToolbarLabel(_battleUiModeButton, 24f);
        RefreshEnemyFormationButton();
        RefreshStonePositionButton();
        RefreshBattleUiModeButton();

        RectTransform codeRow = CreateHorizontalRow(_headerRoot, "FormationCodeRow", 48f, spacing: 10f);
        _formationCodeInput = CreateFormationCodeInput(codeRow);
        _formationCodeCopyButton = CreateButton(codeRow, "FormationCodeCopyButton", 225f, 48f, CopyFormationCode);
        _formationCodePasteButton = CreateButton(codeRow, "FormationCodePasteButton", 180f, 48f, PasteFormationCode);
        _formationCodeApplyButton = CreateButton(codeRow, "FormationCodeApplyButton", 180f, 48f, ApplyFormationCode);
        _formationCodeStatus = Instantiate(_selectionCountText, codeRow);
        _formationCodeStatus.name = "FormationCodeStatus";
        _formationCodeStatus.fontSize = 18f;
        _formationCodeStatus.alignment = TextAlignmentOptions.Left;
        _formationCodeStatus.enableAutoSizing = false;
        _formationCodeStatus.textWrappingMode = TextWrappingModes.NoWrap;
        _formationCodeStatus.overflowMode = TextOverflowModes.Ellipsis;
        _formationCodeStatus.text = "味方編成コード";
        LayoutElement statusLayout = _formationCodeStatus.gameObject.GetComponent<LayoutElement>() ??
                                      _formationCodeStatus.gameObject.AddComponent<LayoutElement>();
        statusLayout.preferredWidth = 300f;
        SetButtonLabel(_formationCodeApplyButton, "適用");
        SetButtonLabel(_formationCodeCopyButton, "コードコピー");
        SetButtonLabel(_formationCodePasteButton, "ペースト");
        ConfigureToolbarLabel(_formationCodeApplyButton, 20f);
        ConfigureToolbarLabel(_formationCodeCopyButton, 20f);
        ConfigureToolbarLabel(_formationCodePasteButton, 20f);

        RectTransform presetRow = CreateHorizontalRow(_headerRoot, "EnemyPresetRow", 48f, spacing: 12f);
        _enemyPresetRowRoot = presetRow.gameObject;
        _enemyPresetDefaultButton = CreateButton(
            presetRow, "EnemyPresetDefaultButton", 0f, 48f, ApplyEnemyPresetDefault, flexibleWidth: 1f);
        _enemyPresetNeutralButton = CreateButton(
            presetRow, "EnemyPresetNeutralButton", 0f, 48f, ApplyEnemyPresetStandard, flexibleWidth: 1f);
        _enemyPresetTopButton = CreateButton(
            presetRow, "EnemyPresetTopButton", 0f, 48f, OpenEnemyTopPresetPicker, flexibleWidth: 1f);
        SetButtonLabel(_enemyPresetDefaultButton, "最強編成");
        SetButtonLabel(_enemyPresetNeutralButton, EnemyStandardPreset.Label);
        SetButtonLabel(_enemyPresetTopButton, "マップ別トップ");
        ConfigureToolbarLabel(_enemyPresetDefaultButton, 24f);
        ConfigureToolbarLabel(_enemyPresetNeutralButton, 24f);
        ConfigureToolbarLabel(_enemyPresetTopButton, 24f);
        _enemyPresetRowRoot.SetActive(false);

        RectTransform debugRow = CreateHorizontalRow(_headerRoot, "DebugRow", 92f, spacing: 10f);
        _debugRowRoot = debugRow.gameObject;
        CreateDebugControl(
            debugRow,
            "CharacterRoutes",
            () =>
            {
                CombatPlaytestDebugSettings.SetShowCharacterRoutes(!CombatPlaytestDebugSettings.ShowCharacterRoutes);
                RefreshDebugToggleButtons();
            },
            () => OpenDebugSettings(DebugSettingsKind.CharacterRoutes),
            out _debugCharacterRoutesButton,
            out _debugCharacterRoutesSettingsButton);
        CreateDebugControl(
            debugRow,
            "AssaultRoutes",
            () =>
            {
                CombatPlaytestDebugSettings.SetShowAssaultRoutes(!CombatPlaytestDebugSettings.ShowAssaultRoutes);
                RefreshDebugToggleButtons();
            },
            null,
            out _debugAssaultRoutesButton,
            out _);
        CreateDebugControl(
            debugRow,
            "AiLabels",
            () =>
            {
                CombatPlaytestDebugSettings.SetShowAiLabels(!CombatPlaytestDebugSettings.ShowAiLabels);
                RefreshDebugToggleButtons();
            },
            () => OpenDebugSettings(DebugSettingsKind.AiLabels),
            out _debugAiLabelsButton,
            out _debugAiLabelsSettingsButton);
        CreateDebugControl(
            debugRow,
            "Vision",
            () =>
            {
                CombatPlaytestDebugSettings.SetShowVision(!CombatPlaytestDebugSettings.ShowVision);
                RefreshDebugToggleButtons();
            },
            () => OpenDebugSettings(DebugSettingsKind.Vision),
            out _debugVisionButton,
            out _debugVisionSettingsButton);
        RefreshDebugToggleButtons();
    }

    private void CreateDebugControl(
        RectTransform parent,
        string objectName,
        UnityEngine.Events.UnityAction onToggle,
        UnityEngine.Events.UnityAction onSettings,
        out Button toggleButton,
        out Button settingsButton)
    {
        GameObject cellObject = new GameObject(
            objectName + "Cell",
            typeof(RectTransform),
            typeof(VerticalLayoutGroup),
            typeof(LayoutElement));
        RectTransform cell = cellObject.GetComponent<RectTransform>();
        cell.SetParent(parent, false);
        VerticalLayoutGroup layout = cellObject.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 4f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        LayoutElement cellLayout = cellObject.GetComponent<LayoutElement>();
        cellLayout.flexibleWidth = 1f;
        cellLayout.minWidth = 140f;

        float toggleHeight = onSettings != null ? 44f : 84f;
        toggleButton = CreateButton(cell, objectName + "Toggle", 0f, toggleHeight, onToggle, flexibleWidth: 1f);
        settingsButton = onSettings != null
            ? CreateButton(cell, objectName + "Settings", 0f, 36f, onSettings, flexibleWidth: 1f)
            : null;
        ConfigureToolbarLabel(toggleButton, 20f);
        if (settingsButton != null)
        {
            SetButtonLabel(settingsButton, "設定");
            ConfigureToolbarLabel(settingsButton, 20f);
        }
    }

    private static RectTransform CreateHorizontalRow(
        RectTransform parent,
        string objectName,
        float height,
        float spacing)
    {
        GameObject rowObject = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(HorizontalLayoutGroup),
            typeof(LayoutElement));
        RectTransform row = rowObject.GetComponent<RectTransform>();
        row.SetParent(parent, false);
        HorizontalLayoutGroup layout = rowObject.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = spacing;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;
        rowObject.GetComponent<LayoutElement>().preferredHeight = height;
        return row;
    }

    private void RefreshDebugToggleButtons()
    {
        SetButtonLabel(
            _debugCharacterRoutesButton,
            FormatDebugToggle("移動の線", CombatPlaytestDebugSettings.ShowCharacterRoutes));
        SetButtonLabel(
            _debugAssaultRoutesButton,
            FormatDebugToggle("侵攻ルート", CombatPlaytestDebugSettings.ShowAssaultRoutes));
        SetButtonLabel(
            _debugAiLabelsButton,
            FormatDebugToggle("頭上テキスト", CombatPlaytestDebugSettings.ShowAiLabels));
        SetButtonLabel(
            _debugVisionButton,
            FormatDebugToggle("視界表示", CombatPlaytestDebugSettings.ShowVision));
    }

    private void OpenDebugSettings(DebugSettingsKind kind)
    {
        EnsurePicker();
        SetPickerLayout(twoColumns: false);
        ClearPickerOptions();
        RebuildDebugSettingsPicker(kind);
        ShowPicker();
    }

    private void RebuildDebugSettingsPicker(DebugSettingsKind kind)
    {
        ClearPickerOptions();
        AddPickerOption(
            "デフォルトに戻す",
            () =>
            {
                ResetDebugSettingsToDefault(kind);
                RebuildDebugSettingsPicker(kind);
                RefreshDebugToggleButtons();
            },
            () => SetPickerDescription("この項目の詳細設定だけ、最初の状態に戻します。"));

        switch (kind)
        {
            case DebugSettingsKind.CharacterRoutes:
                SetPickerTitle("移動の線 — 詳細設定");
                SetPickerDescription("キャラがマップ上でどこへ向かっているかを、線で見られます。");
                AddDebugBoolOption(
                    "味方キャラの行き先を線で出す",
                    CombatPlaytestDebugSettings.CharacterRoutesShowAlly,
                    "味方一人ひとりが、今向かっている場所までの道を線で描きます。",
                    value =>
                    {
                        CombatPlaytestDebugSettings.SetCharacterRoutesShowAlly(value);
                        RebuildDebugSettingsPicker(kind);
                    });
                AddDebugBoolOption(
                    "敵キャラの行き先を線で出す",
                    CombatPlaytestDebugSettings.CharacterRoutesShowEnemy,
                    "敵一人ひとりが、今向かっている場所までの道を線で描きます。",
                    value =>
                    {
                        CombatPlaytestDebugSettings.SetCharacterRoutesShowEnemy(value);
                        RebuildDebugSettingsPicker(kind);
                    });
                break;

            case DebugSettingsKind.AiLabels:
                SetPickerTitle("頭上テキスト — 詳細設定");
                SetPickerDescription("キャラの頭上に出す文字の種類を選べます。");
                AddDebugBoolOption(
                    "AIが今やろうとしていることを出す",
                    CombatPlaytestDebugSettings.LabelShowObjective,
                    "攻撃・回復・魔石破壊など、いま選んでいる行動の目的を出します。",
                    value =>
                    {
                        CombatPlaytestDebugSettings.SetLabelShowObjective(value);
                        RebuildDebugSettingsPicker(kind);
                    });
                AddDebugBoolOption(
                    "持っている武器の名前を出す",
                    CombatPlaytestDebugSettings.LabelShowWeapon,
                    "剣・杖・盾など、装備している武器を出します。",
                    value =>
                    {
                        CombatPlaytestDebugSettings.SetLabelShowWeapon(value);
                        RebuildDebugSettingsPicker(kind);
                    });
                AddDebugBoolOption(
                    "性格の名前を出す",
                    CombatPlaytestDebugSettings.LabelShowPersonality,
                    "標準・戦闘狂など、そのキャラの性格名を出します。",
                    value =>
                    {
                        CombatPlaytestDebugSettings.SetLabelShowPersonality(value);
                        RebuildDebugSettingsPicker(kind);
                    });
                break;

            case DebugSettingsKind.Vision:
                SetPickerTitle("視界表示 — 詳細設定");
                SetPickerDescription("誰が何を見えているかを、線や扇形で見られます。");
                AddDebugBoolOption(
                    "見えている相手まで線を引く",
                    CombatPlaytestDebugSettings.VisionShowLines,
                    "キャラから、いま視認できている相手へ線を引きます。",
                    value =>
                    {
                        CombatPlaytestDebugSettings.SetVisionShowLines(value);
                        RebuildDebugSettingsPicker(kind);
                    });
                AddDebugBoolOption(
                    "木や壁に視線が遮られるか確認する",
                    CombatPlaytestDebugSettings.VisionShowObstructionRays,
                    "障害物で視線が止まるかを短い線で確認します。線が増えるので、普段はOFFのままで大丈夫です。",
                    value =>
                    {
                        CombatPlaytestDebugSettings.SetVisionShowObstructionRays(value);
                        RebuildDebugSettingsPicker(kind);
                    });
                AddDebugBoolOption(
                    "左右にどれくらい見えているか（扇形）を出す",
                    CombatPlaytestDebugSettings.VisionShowFieldOfView,
                    "キャラの正面から、左右どれくらいの範囲が見えるかを扇形で出します。",
                    value =>
                    {
                        CombatPlaytestDebugSettings.SetVisionShowFieldOfView(value);
                        RebuildDebugSettingsPicker(kind);
                    });
                AddDebugBoolOption(
                    "視界の遮蔽物をログに記録する",
                    CombatPlaytestDebugSettings.LogVisionObstructions,
                    "太い視線判定で遮られたColliderを戦闘ログへ集約して記録します。",
                    value =>
                    {
                        CombatPlaytestDebugSettings.SetLogVisionObstructions(value);
                        RebuildDebugSettingsPicker(kind);
                    });
                break;
        }
    }

    private static void ResetDebugSettingsToDefault(DebugSettingsKind kind)
    {
        switch (kind)
        {
            case DebugSettingsKind.CharacterRoutes:
                CombatPlaytestDebugSettings.ResetCharacterRouteDetailsToDefault();
                break;
            case DebugSettingsKind.AiLabels:
                CombatPlaytestDebugSettings.ResetLabelDetailsToDefault();
                break;
            case DebugSettingsKind.Vision:
                CombatPlaytestDebugSettings.ResetVisionDetailsToDefault();
                break;
        }
    }

    private void AddDebugBoolOption(string label, bool current, string description, Action<bool> setValue)
    {
        AddPickerOption(
            FormatDebugToggle(label, current),
            () => setValue(!current),
            () => SetPickerDescription(description));
    }

    private static string FormatDebugToggle(string label, bool on)
    {
        return $"{(on ? "■" : "□")} {label}";
    }

    private void ToggleEnemyFormation()
    {
        SetDetailSettingsOpen(!_detailSettingsOpen);
    }

    private void SetDetailSettingsOpen(bool open)
    {
        _detailSettingsOpen = open;
        if (_allyColumnRoot != null) _allyColumnRoot.SetActive(!open);
        if (_enemyColumnRoot != null) _enemyColumnRoot.SetActive(open);
        if (_enemyPresetRowRoot != null) _enemyPresetRowRoot.SetActive(open);
        if (_debugRowRoot != null) _debugRowRoot.SetActive(!open);
        RefreshFormationPanelHeights();
        RefreshEnemyFormationButton();
        RefreshFormationCodeContext();
        RefreshSelectionCountText();
    }

    private void RefreshFormationCodeContext()
    {
        string teamLabel = _detailSettingsOpen ? "敵" : "味方";
        if (_formationCodeStatus != null)
        {
            _formationCodeStatus.text = $"{teamLabel}編成コード（{GetVisibleRows().Count}文字）";
        }
    }

    private void RefreshFormationPanelHeights()
    {
        if (_headerRoot != null)
        {
            LayoutElement headerLayout = _headerRoot.GetComponent<LayoutElement>();
            if (headerLayout != null)
            {
                headerLayout.preferredHeight = _detailSettingsOpen ? 160f : 204f;
            }
        }

        if (_teamSelectionsLayout != null)
        {
            _teamSelectionsLayout.preferredHeight = _detailSettingsOpen ? 532f : 488f;
        }
    }

    private void RefreshEnemyFormationButton()
    {
        SetButtonLabel(_enemyFormationButton, _detailSettingsOpen ? "閉じる" : "敵編成");
    }

    private void ToggleStonePositionReversed()
    {
        SetStonePositionReversedState(!_stonePositionReversed);
        StonePositionReversedChanged?.Invoke(_stonePositionReversed);
    }

    public void SetStonePositionReversedState(bool reversed)
    {
        if (_stonePositionReversed == reversed) return;

        _stonePositionReversed = reversed;
        RefreshStonePositionButton();
    }

    private void RefreshStonePositionButton()
    {
        SetButtonLabel(_stonePositionButton, $"位置逆転: {(_stonePositionReversed ? "ON" : "OFF")}");
    }

    private void ToggleBattleUiMode()
    {
        CombatPlaytestDebugSettings.SetUseDebugBattleUi(!CombatPlaytestDebugSettings.UseDebugBattleUi);
        RefreshBattleUiModeButton();
    }

    private void RefreshBattleUiModeButton()
    {
        SetButtonLabel(
            _battleUiModeButton,
            $"UI:{(CombatPlaytestDebugSettings.UseDebugBattleUi ? "デバッグ" : "本番")}");
    }

    private void ApplyEnemyPresetDefault()
    {
        ClosePicker();
        ApplyDefaultParty(_enemyRows, useEnemyPersonalities: true, useEnemyWeapons: true);
        Refresh();
    }

    private void ApplyEnemyPresetStandard()
    {
        ApplyEnemyPreset(EnemyStandardPreset);
    }

    private void OpenEnemyTopPresetPicker()
    {
        EnsurePicker();
        SetPickerLayout(twoColumns: true);
        ClearPickerOptions();
        SetPickerTitle("マップ別トップ編成");
        SetPickerDescription("評価結果から選んだ編成です。位置逆転は現在の設定を維持します。");

        for (int i = 0; i < EnemyTopPresets.Length; i++)
        {
            EnemyPresetDefinition preset = EnemyTopPresets[i];
            AddPickerOption(preset.Label, () => ApplyEnemyTopPreset(preset));
        }

        ShowPicker();
    }

    private void ApplyEnemyTopPreset(EnemyPresetDefinition preset)
    {
        ApplyEnemyPreset(preset);
    }

    private void ApplyEnemyPreset(EnemyPresetDefinition preset)
    {
        ClosePicker();
        for (int i = 0; i < _enemyRows.Count; i++)
        {
            SelectionRow row = _enemyRows[i];
            bool selected = i < preset.Weapons.Length;
            row.Selected = selected;
            if (!selected) continue;

            row.WeaponIndex = FindWeaponIndex(preset.Weapons[i]);
            row.PersonalityIndex = FindOrAddPersonalityIndex(preset.Personalities[i]);
        }

        Refresh();
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
        columnObject.GetComponent<LayoutElement>().preferredWidth = CharacterListWidth;

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

    private static RectTransform CreateRowsGrid(RectTransform parent, string objectName)
    {
        GameObject rowsObject = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(GridLayoutGroup),
            typeof(ContentSizeFitter),
            typeof(LayoutElement));
        RectTransform rows = rowsObject.GetComponent<RectTransform>();
        rows.SetParent(parent, false);

        GridLayoutGroup grid = rowsObject.GetComponent<GridLayoutGroup>();
        grid.spacing = new Vector2(8f, 8f);
        grid.cellSize = new Vector2((CharacterListWidth - grid.spacing.x) * 0.5f, 72f);
        grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        grid.startAxis = GridLayoutGroup.Axis.Vertical;
        grid.childAlignment = TextAnchor.UpperLeft;
        grid.constraint = GridLayoutGroup.Constraint.FixedRowCount;
        grid.constraintCount = PartyGridRowCount;

        ContentSizeFitter fitter = rowsObject.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        LayoutElement layout = rowsObject.GetComponent<LayoutElement>();
        layout.flexibleWidth = 1f;
        return rows;
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
            CharacterButton = CreateButton(rowObject.transform, null, 300f, 64f),
            WeaponButton = CreateButton(rowObject.transform, null, 240f, 64f),
            PersonalityButton = CreateButton(rowObject.transform, null, 240f, 64f),
        };

        ConfigureToolbarLabel(row.CharacterButton, 24f);
        ConfigureToolbarLabel(row.WeaponButton, 24f);
        ConfigureToolbarLabel(row.PersonalityButton, 24f);

        row.CharacterButton.onClick.AddListener(() => Toggle(row));
        row.WeaponButton.onClick.AddListener(() => OpenWeaponPicker(row));
        row.PersonalityButton.onClick.AddListener(() => OpenPersonalityPickerForRow(row));
        return row;
    }

    private void ConfigureSelectionCountText()
    {
        if (_selectionCountText == null) return;

        _selectionCountText.textWrappingMode = TextWrappingModes.NoWrap;
        _selectionCountText.overflowMode = TextOverflowModes.Overflow;
        _selectionCountText.rectTransform.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Horizontal,
            420f);
    }

    private int FindPersonalityIndex(CombatAiPersonalityProfile profile)
    {
        CombatAiPersonalityKind kind = profile != null
            ? profile.Kind
            : CombatAiPersonalityKind.Neutral;
        int index = FindPersonalityIndex(kind);
        return index >= 0 ? index : FindOrAddPersonalityIndex(CombatAiPersonalityKind.Neutral);
    }

    private int FindPersonalityIndex(CombatAiPersonalityKind kind)
    {
        for (int i = 0; i < _personalityOptions.Count; i++)
        {
            CombatAiPersonalityProfile option = _personalityOptions[i];
            if (option != null && option.Kind == kind) return i;
        }

        return -1;
    }

    private Button CreateButton(
        Transform parent,
        string objectName,
        float width,
        float height = -1f,
        UnityEngine.Events.UnityAction onClick = null,
        float flexibleWidth = 0f)
    {
        Button button = Instantiate(_characterItemPrefab, parent);
        if (!string.IsNullOrEmpty(objectName)) button.name = objectName;
        LayoutElement layout = button.GetComponent<LayoutElement>() ?? button.gameObject.AddComponent<LayoutElement>();
        layout.preferredWidth = width;
        layout.minWidth = flexibleWidth > 0f ? 120f : 0f;
        layout.flexibleWidth = flexibleWidth;
        if (height > 0f) layout.preferredHeight = height;
        HideIndicator(button);
        if (onClick != null) button.onClick.AddListener(onClick);
        return button;
    }

    private TMP_InputField CreateFormationCodeInput(Transform parent)
    {
        GameObject inputObject = new GameObject(
            "FormationCodeInput",
            typeof(RectTransform),
            typeof(Image),
            typeof(TMP_InputField),
            typeof(LayoutElement));
        inputObject.transform.SetParent(parent, false);
        inputObject.GetComponent<Image>().color = new Color(0.05f, 0.06f, 0.08f, 0.95f);
        LayoutElement inputLayout = inputObject.GetComponent<LayoutElement>();
        inputLayout.minWidth = 280f;
        inputLayout.flexibleWidth = 1f;

        TMP_Text text = Instantiate(_selectionCountText, inputObject.transform);
        text.name = "Text";
        text.text = string.Empty;
        text.fontSize = 18f;
        text.alignment = TextAlignmentOptions.Left;
        text.enableAutoSizing = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
        Stretch(text.rectTransform);
        text.margin = new Vector4(12f, 0f, 12f, 0f);

        TMP_Text placeholder = Instantiate(_selectionCountText, inputObject.transform);
        placeholder.name = "Placeholder";
        placeholder.text = "コード表示欄（コードをコピーで生成）";
        placeholder.fontSize = 18f;
        placeholder.alignment = TextAlignmentOptions.Left;
        placeholder.enableAutoSizing = false;
        placeholder.textWrappingMode = TextWrappingModes.NoWrap;
        placeholder.overflowMode = TextOverflowModes.Ellipsis;
        placeholder.color = new Color(1f, 1f, 1f, 0.45f);
        Stretch(placeholder.rectTransform);
        placeholder.margin = new Vector4(12f, 0f, 12f, 0f);

        TMP_InputField input = inputObject.GetComponent<TMP_InputField>();
        input.textComponent = text;
        input.placeholder = placeholder;
        input.textViewport = inputObject.GetComponent<RectTransform>();
        input.targetGraphic = inputObject.GetComponent<Image>();
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.contentType = TMP_InputField.ContentType.Standard;
        input.characterValidation = TMP_InputField.CharacterValidation.None;
        input.caretColor = Color.white;
        input.onValueChanged.AddListener(OnFormationCodeInputChanged);
        return input;
    }

    private void OnFormationCodeInputChanged(string _)
    {
        SetButtonLabel(_formationCodeCopyButton, "コードコピー");
        SetFormationCodeStatus(string.Empty);
    }

    private void ApplyFormationCode()
    {
        List<SelectionRow> rows = GetVisibleRows();
        if (!CombatFormationCode.TryDecode(
                _formationCodeInput != null ? _formationCodeInput.text : string.Empty,
                rows.Count,
                out CombatFormationCodeData data,
                out string error))
        {
            SetFormationCodeStatus(error);
            return;
        }

        if (!TryApplyFormationEntries(rows, data.Entries, out error))
        {
            SetFormationCodeStatus(error);
            return;
        }

        Refresh();
        if (_formationCodeInput != null)
        {
            _formationCodeInput.text = string.Empty;
        }

        SetFormationCodeStatus("編成コードを適用しました。");
    }

    private bool TryApplyFormationEntries(
        List<SelectionRow> rows,
        IReadOnlyList<CombatFormationCodeEntry> entries,
        out string error)
    {
        error = string.Empty;
        for (int i = 0; i < entries.Count; i++)
        {
            if (!entries[i].Selected) continue;

            if (FindWeaponIndex(entries[i].Weapon) < 0)
            {
                error = $"武器「{entries[i].Weapon}」がこの画面で選択できません。";
                return false;
            }

            if (FindPersonalityIndex(entries[i].Personality) < 0)
            {
                error = $"性格「{entries[i].Personality}」がこの画面で選択できません。";
                return false;
            }
        }

        for (int i = 0; i < entries.Count; i++)
        {
            CombatFormationCodeEntry entry = entries[i];
            SelectionRow row = rows[i];
            row.Selected = entry.Selected;
            if (!entry.Selected) continue;

            row.WeaponIndex = FindWeaponIndex(entry.Weapon);
            row.PersonalityIndex = FindOrAddPersonalityIndex(entry.Personality);
        }

        return true;
    }

    private void CopyFormationCode()
    {
        List<SelectionRow> rows = GetVisibleRows();
        string code = CombatFormationCode.Encode(
            BuildFormationEntries(rows));
        if (_formationCodeInput != null)
        {
            _formationCodeInput.text = code;
        }

        GUIUtility.systemCopyBuffer = code;
        SetButtonLabel(_formationCodeCopyButton, "コピーしました");
        SetFormationCodeStatus("編成コードをコピーしました。");
    }

    private void PasteFormationCode()
    {
        if (_formationCodeInput != null)
        {
            _formationCodeInput.text = GUIUtility.systemCopyBuffer ?? string.Empty;
        }

        SetFormationCodeStatus("コードをペーストしました。");
    }

    private List<CombatFormationCodeEntry> BuildFormationEntries(List<SelectionRow> rows)
    {
        var entries = new List<CombatFormationCodeEntry>(rows.Count);
        for (int i = 0; i < rows.Count; i++)
        {
            SelectionRow row = rows[i];
            WeaponConfig weapon = GetOption(_weaponOptions, row.WeaponIndex);
            CombatAiPersonalityProfile personality = GetOption(_personalityOptions, row.PersonalityIndex);
            entries.Add(new CombatFormationCodeEntry(
                row.Selected,
                weapon != null ? weapon.Kind : WeaponKind.Unarmed,
                personality != null ? personality.Kind : CombatAiPersonalityKind.Neutral));
        }

        return entries;
    }

    private void SetFormationCodeStatus(string message)
    {
        if (_formationCodeStatus != null)
        {
            _formationCodeStatus.text = message ?? string.Empty;
        }

        if (!string.IsNullOrEmpty(message) &&
            message != "編成コードを適用しました。" &&
            message != "編成コードをコピーしました。" &&
            message != "コードをペーストしました。")
        {
            Debug.LogWarning($"[{nameof(CombatCharacterSelection)}] {message}", this);
        }
    }

    private void Toggle(SelectionRow row)
    {
        row.Selected = !row.Selected;
        Refresh();
    }

    private void CycleMovementSpeed()
    {
        _movementSpeedMultiplierIndex =
            (_movementSpeedMultiplierIndex + 1) % MovementSpeedMultipliers.Length;
        RefreshMovementSpeedButton();
    }

    private void RefreshMovementSpeedButton()
    {
        SetButtonLabel(_movementSpeedButton, $"移動速度: {MovementSpeedMultiplier:0}x");
    }

    private void OpenWeaponPicker(SelectionRow row)
    {
        OpenWeaponPicker(
            "武器を選択",
            row.WeaponIndex,
            index =>
            {
                row.WeaponIndex = index;
                RefreshRow(row);
            });
    }

    private void OpenBulkWeaponPicker()
    {
        if (_weaponOptions.Count == 0) return;

        List<SelectionRow> rows = GetVisibleRows();
        OpenWeaponPicker(
            "武器一括変更",
            rows.Count > 0 ? rows[0].WeaponIndex : 0,
            index =>
            {
                for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
                {
                    rows[rowIndex].WeaponIndex = index;
                }

                RefreshRows(rows);
            });
    }

    private void OpenWeaponPicker(string title, int selectedIndex, Action<int> onSelected)
    {
        if (_weaponOptions.Count == 0) return;

        EnsurePicker();
        SetWeaponPickerLayout();
        ClearPickerOptions();
        ClearPickerDetails();
        SetPickerTitle(title);
        int currentIndex = Mathf.Clamp(selectedIndex, 0, _weaponOptions.Count - 1);

        for (int i = 0; i < _weaponOptions.Count; i++)
        {
            int index = i;
            AddPickerOption(
                index == currentIndex
                    ? "■ " + GetWeaponName(_weaponOptions[index])
                    : GetWeaponName(_weaponOptions[index]),
                () =>
                {
                    onSelected?.Invoke(index);
                    ClosePicker();
                });
        }

        RefreshAllWeaponDetails();
        ShowPicker();
    }

    private void OpenPersonalityPickerForRow(SelectionRow row)
    {
        OpenPersonalityPicker(
            "性格を選択",
            row.PersonalityIndex,
            onSelected: index =>
            {
                row.PersonalityIndex = index;
                RefreshRow(row);
            });
    }

    private void OpenBulkPersonalityPicker()
    {
        List<SelectionRow> rows = GetVisibleRows();
        OpenPersonalityPicker(
            "性格一括変更",
            rows.Count > 0 ? rows[0].PersonalityIndex : -1,
            onSelected: index =>
            {
                for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
                {
                    rows[rowIndex].PersonalityIndex = index;
                }

                RefreshRows(rows);
            });
    }

    private List<SelectionRow> GetVisibleRows()
    {
        return _detailSettingsOpen ? _enemyRows : _allyRows;
    }

    private void OpenPersonalityPicker(
        string title,
        int selectedIndex,
        Action<int> onSelected)
    {
        if (_personalityOptions.Count == 0) return;

        EnsurePicker();
        SetDetailsPickerLayout();
        ClearPickerOptions();
        ClearPickerDetails();
        SetPickerTitle(title);

        for (int i = 0; i < _personalityOptions.Count; i++)
        {
            int index = i;
            CombatAiPersonalityProfile personality = _personalityOptions[i];
            if (personality == null) continue;

            string name = personality.DisplayNameJapanese;
            bool selected = index == selectedIndex;
            AddPickerOption(
                selected ? "■ " + name : name,
                () =>
                {
                    onSelected?.Invoke(index);
                    ClosePicker();
                });
        }

        RefreshAllPersonalityDetails();
        ShowPicker();
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
        panel.sizeDelta = new Vector2(960f, 720f);
        _pickerPanel = panel;
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
        _pickerDescription.overflowMode = TextOverflowModes.Overflow;
        _pickerDescription.textWrappingMode = TextWrappingModes.Normal;
        _pickerDescription.text = string.Empty;
        LayoutElement descriptionLayout = _pickerDescription.gameObject.GetComponent<LayoutElement>() ??
                                          _pickerDescription.gameObject.AddComponent<LayoutElement>();
        descriptionLayout.preferredHeight = 72f;

        GameObject bodyObject = new GameObject(
            "PickerBody",
            typeof(RectTransform),
            typeof(HorizontalLayoutGroup),
            typeof(LayoutElement));
        RectTransform body = bodyObject.GetComponent<RectTransform>();
        body.SetParent(panel, false);
        HorizontalLayoutGroup bodyLayout = bodyObject.GetComponent<HorizontalLayoutGroup>();
        bodyLayout.spacing = 10f;
        bodyLayout.childControlWidth = true;
        bodyLayout.childControlHeight = true;
        bodyLayout.childForceExpandWidth = false;
        bodyLayout.childForceExpandHeight = true;
        LayoutElement bodyElement = bodyObject.GetComponent<LayoutElement>();
        bodyElement.flexibleHeight = 1f;

        GameObject scrollObject = new GameObject(
            "Scroll",
            typeof(RectTransform),
            typeof(Image),
            typeof(ScrollRect),
            typeof(LayoutElement));
        RectTransform scrollRectTransform = scrollObject.GetComponent<RectTransform>();
        scrollRectTransform.SetParent(body, false);
        scrollObject.GetComponent<Image>().color = new Color(0.05f, 0.06f, 0.08f, 0.9f);
        LayoutElement scrollLayout = scrollObject.GetComponent<LayoutElement>();
        scrollLayout.flexibleWidth = 1f;
        _pickerOptionsLayout = scrollLayout;

        GameObject viewportObject = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
        RectTransform viewport = viewportObject.GetComponent<RectTransform>();
        viewport.SetParent(scrollRectTransform, false);
        Stretch(viewport);
        viewportObject.GetComponent<Image>().color = Color.white;
        viewportObject.GetComponent<Mask>().showMaskGraphic = false;

        GameObject contentObject = new GameObject(
            "Content",
            typeof(RectTransform),
            typeof(GridLayoutGroup),
            typeof(ContentSizeFitter));
        RectTransform content = contentObject.GetComponent<RectTransform>();
        content.SetParent(viewport, false);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.offsetMin = Vector2.zero;
        content.offsetMax = Vector2.zero;
        _pickerGridLayout = contentObject.GetComponent<GridLayoutGroup>();
        _pickerGridLayout.cellSize = new Vector2(900f, 64f);
        _pickerGridLayout.spacing = new Vector2(8f, 6f);
        _pickerGridLayout.padding = new RectOffset(8, 8, 8, 8);
        _pickerGridLayout.startAxis = GridLayoutGroup.Axis.Horizontal;
        _pickerGridLayout.startCorner = GridLayoutGroup.Corner.UpperLeft;
        _pickerGridLayout.childAlignment = TextAnchor.UpperCenter;
        _pickerGridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        _pickerGridLayout.constraintCount = 1;
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

        GameObject detailsObject = new GameObject(
            "SkillDetails",
            typeof(RectTransform),
            typeof(Image),
            typeof(ScrollRect),
            typeof(LayoutElement));
        _pickerDetailsRoot = detailsObject.GetComponent<RectTransform>();
        _pickerDetailsRoot.SetParent(body, false);
        detailsObject.GetComponent<Image>().color = new Color(0.05f, 0.06f, 0.08f, 0.9f);
        _pickerDetailsLayout = detailsObject.GetComponent<LayoutElement>();
        _pickerDetailsLayout.flexibleWidth = 4f;

        GameObject detailsViewportObject = new GameObject(
            "Viewport",
            typeof(RectTransform),
            typeof(Image),
            typeof(Mask));
        RectTransform detailsViewport = detailsViewportObject.GetComponent<RectTransform>();
        detailsViewport.SetParent(_pickerDetailsRoot, false);
        Stretch(detailsViewport);
        detailsViewportObject.GetComponent<Image>().color = Color.white;
        detailsViewportObject.GetComponent<Mask>().showMaskGraphic = false;

        GameObject detailsContentObject = new GameObject(
            "Content",
            typeof(RectTransform),
            typeof(VerticalLayoutGroup),
            typeof(ContentSizeFitter));
        RectTransform detailsContent = detailsContentObject.GetComponent<RectTransform>();
        detailsContent.SetParent(detailsViewport, false);
        detailsContent.anchorMin = new Vector2(0f, 1f);
        detailsContent.anchorMax = new Vector2(1f, 1f);
        detailsContent.pivot = new Vector2(0.5f, 1f);
        detailsContent.offsetMin = Vector2.zero;
        detailsContent.offsetMax = Vector2.zero;
        VerticalLayoutGroup detailsLayout = detailsContentObject.GetComponent<VerticalLayoutGroup>();
        detailsLayout.spacing = 8f;
        detailsLayout.padding = new RectOffset(8, 8, 8, 8);
        detailsLayout.childControlWidth = true;
        detailsLayout.childControlHeight = true;
        detailsLayout.childForceExpandWidth = true;
        detailsLayout.childForceExpandHeight = false;
        ContentSizeFitter detailsFitter = detailsContentObject.GetComponent<ContentSizeFitter>();
        detailsFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        detailsFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        _pickerDetailsContent = detailsContent;

        ScrollRect detailsScroll = detailsObject.GetComponent<ScrollRect>();
        detailsScroll.viewport = detailsViewport;
        detailsScroll.content = detailsContent;
        detailsScroll.horizontal = false;
        detailsScroll.vertical = true;
        detailsScroll.movementType = ScrollRect.MovementType.Clamped;
        _pickerDetailsRoot.gameObject.SetActive(false);

        Button closeButton = CreateButton(panel, "ClosePickerButton", 0f, 44f, ClosePicker);
        closeButton.GetComponent<LayoutElement>().flexibleWidth = 1f;
        SetButtonLabel(closeButton, "閉じる");

        _pickerRoot.gameObject.SetActive(false);
    }

    private void SetPickerLayout(bool twoColumns)
    {
        if (_pickerGridLayout == null) return;

        if (_pickerDescription != null)
        {
            _pickerDescription.gameObject.SetActive(true);
        }
        if (_pickerPanel != null)
        {
            _pickerPanel.sizeDelta = new Vector2(960f, 720f);
        }

        if (_pickerDetailsRoot != null)
        {
            _pickerDetailsRoot.gameObject.SetActive(false);
        }

        if (_pickerOptionsLayout != null)
        {
            _pickerOptionsLayout.flexibleWidth = 1f;
        }

        _pickerGridLayout.constraintCount = twoColumns ? 2 : 1;
        _pickerGridLayout.cellSize = new Vector2(twoColumns ? 450f : 900f, 64f);
    }

    private void SetDetailsPickerLayout()
    {
        if (_pickerGridLayout == null) return;

        if (_pickerDescription != null)
        {
            _pickerDescription.gameObject.SetActive(false);
        }
        if (_pickerPanel != null)
        {
            _pickerPanel.sizeDelta = new Vector2(1440f, 720f);
        }

        if (_pickerOptionsLayout != null)
        {
            _pickerOptionsLayout.flexibleWidth = 2f;
        }

        if (_pickerDetailsLayout != null)
        {
            _pickerDetailsLayout.flexibleWidth = 8f;
        }

        if (_pickerDetailsRoot != null)
        {
            _pickerDetailsRoot.gameObject.SetActive(true);
        }

        _pickerGridLayout.constraintCount = 1;
        _pickerGridLayout.cellSize = new Vector2(260f, 64f);
    }

    private void SetWeaponPickerLayout()
    {
        SetDetailsPickerLayout();
    }

    private void ClearPickerOptions()
    {
        if (_pickerContent == null) return;
        for (int i = _pickerContent.childCount - 1; i >= 0; i--)
        {
            DestroyGeneratedObject(_pickerContent.GetChild(i).gameObject);
        }
    }

    private Button AddPickerOption(string label, Action onClick, Action onHighlight = null)
    {
        if (_pickerContent == null || _characterItemPrefab == null) return null;

        Button button = CreateButton(_pickerContent, null, 0f, 64f, () => onClick?.Invoke());
        button.GetComponent<LayoutElement>().flexibleWidth = 1f;
        SetButtonLabel(button, label);
        if (onHighlight != null)
        {
            button.gameObject.AddComponent<EventTriggerProxy>().OnHighlighted = onHighlight;
        }

        return button;
    }

    private void ClearPickerDetails()
    {
        if (_pickerDetailsContent == null) return;

        for (int i = _pickerDetailsContent.childCount - 1; i >= 0; i--)
        {
            DestroyGeneratedObject(_pickerDetailsContent.GetChild(i).gameObject);
        }
    }

    private void RefreshAllWeaponDetails()
    {
        ClearPickerDetails();
        if (_pickerDetailsContent == null) return;

        CombatSkillCatalog catalog = CombatSceneContext.Instance != null
            ? CombatSceneContext.Instance.SkillCatalog
            : null;
        bool ownsCatalog = catalog == null;
        catalog ??= CombatSkillCatalog.CreateDefaultRuntimeCatalog();

        for (int i = 0; i < _weaponOptions.Count; i++)
        {
            AddWeaponDetails(_weaponOptions[i], catalog);
        }

        if (ownsCatalog)
        {
            for (int i = 0; i < catalog.Definitions.Count; i++)
            {
                DestroyGeneratedObject(catalog.Definitions[i]);
            }

            DestroyGeneratedObject(catalog);
        }

        if (_pickerDetailsContent.childCount == 0)
        {
            AddPickerDetailText("武器がありません。", 24f, TextAlignmentOptions.Center, 48f);
        }

        ResetPickerDetailsScroll();
    }

    private void AddWeaponDetails(WeaponConfig weapon, CombatSkillCatalog catalog)
    {
        if (weapon == null) return;

        WeaponBase weaponRuntime = weapon.CreateWeapon();
        AddPickerDetailText(GetWeaponName(weapon), 28f, TextAlignmentOptions.Center, 42f);
        AddPickerDetailText(
            $"通常攻撃　射程: {FormatRange(weapon.Range)}　CT: {FormatSeconds(weapon.CooldownSeconds)}　" +
            $"{FormatCombatStat(weaponRuntime.ScalingStat)}補正: +{weapon.PrimaryStatBonus}",
            18f,
            TextAlignmentOptions.Center,
            42f);

        IReadOnlyList<SkillDefinition> definitions = catalog.GetDefinitionsForKind(weapon.Kind);
        bool hasSkill = false;
        for (int i = 0; i < definitions.Count; i++)
        {
            SkillDefinition definition = definitions[i];
            if (definition == null) continue;

            SkillBase skill = CombatSkillFactory.Create(definition.SkillId);
            if (skill == null) continue;

            hasSkill = true;
            AddSkillDetailCard(skill);
        }

        if (!hasSkill)
        {
            AddPickerDetailText("スキルなし", 22f, TextAlignmentOptions.Center, 48f);
        }
    }

    private void RefreshAllPersonalityDetails()
    {
        ClearPickerDetails();
        if (_pickerDetailsContent == null) return;

        for (int i = 0; i < _personalityOptions.Count; i++)
        {
            CombatAiPersonalityProfile personality = _personalityOptions[i];
            if (personality == null) continue;

            AddPersonalityDetailRow(personality);
        }

        if (_pickerDetailsContent.childCount == 0)
        {
            AddPickerDetailText("性格がありません。", 24f, TextAlignmentOptions.Center, 48f);
        }

        ResetPickerDetailsScroll();
    }

    private void AddPersonalityDetailRow(CombatAiPersonalityProfile personality)
    {
        GameObject row = new GameObject(
            $"PersonalityDetail_{personality.DisplayNameJapanese}",
            typeof(RectTransform),
            typeof(HorizontalLayoutGroup),
            typeof(LayoutElement));
        row.transform.SetParent(_pickerDetailsContent, false);

        HorizontalLayoutGroup layout = row.GetComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(10, 10, 2, 2);
        layout.spacing = 12f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;
        row.GetComponent<LayoutElement>().preferredHeight = 40f;

        TMP_Text name = AddPickerDetailText(
            personality.DisplayNameJapanese,
            24f,
            TextAlignmentOptions.Left,
            36f,
            row.transform);
        LayoutElement nameLayout = name.gameObject.GetComponent<LayoutElement>();
        nameLayout.preferredWidth = 220f;
        nameLayout.flexibleWidth = 0f;

        TMP_Text description = AddPickerDetailText(
            personality.BehaviorDescriptionJapanese,
            20f,
            TextAlignmentOptions.Left,
            36f,
            row.transform);
        LayoutElement descriptionLayout = description.gameObject.GetComponent<LayoutElement>();
        descriptionLayout.minWidth = 0f;
        descriptionLayout.flexibleWidth = 1f;
    }

    private void ResetPickerDetailsScroll()
    {
        ScrollRect detailsScroll = _pickerDetailsRoot != null
            ? _pickerDetailsRoot.GetComponent<ScrollRect>()
            : null;
        if (detailsScroll != null)
        {
            detailsScroll.verticalNormalizedPosition = 1f;
        }
    }

    private void AddSkillDetailCard(SkillBase skill)
    {
        GameObject card = new GameObject(
            $"SkillCard_{skill.Id}",
            typeof(RectTransform),
            typeof(Image),
            typeof(HorizontalLayoutGroup),
            typeof(LayoutElement));
        card.transform.SetParent(_pickerDetailsContent, false);
        card.GetComponent<Image>().color = new Color(0.12f, 0.15f, 0.2f, 1f);

        HorizontalLayoutGroup layout = card.GetComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(10, 10, 8, 8);
        layout.spacing = 8f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;
        card.GetComponent<LayoutElement>().preferredHeight = 84f;

        string effect = string.IsNullOrEmpty(skill.EffectDescription)
            ? "—"
            : skill.EffectDescription;
        if (skill.SelfHpCost > 0)
        {
            effect += $"。自身のHPを {skill.SelfHpCost} 消費";
        }

        AddSkillDetailCell(card.transform, skill.Name, 1.5f, 19f);
        AddSkillDetailCell(card.transform, $"射程: {FormatRange(skill.MaxRange)}", 1f, 15f);
        AddSkillDetailCell(
            card.transform,
            $"威力: {(string.IsNullOrEmpty(skill.PowerDescription) ? "—" : skill.PowerDescription)}",
            1.5f,
            15f);
        AddSkillDetailCell(card.transform, $"効果: {effect}", 3.5f, 15f);
        AddSkillDetailCell(
            card.transform,
            $"CT: {FormatSeconds(skill.CooldownSeconds)} / 詠唱: {FormatSeconds(skill.CastTimeSeconds)}",
            1.8f,
            15f);
        AddSkillDetailCell(
            card.transform,
            $"対象: {FormatTargetKind(skill.TargetKind)}" +
            (skill.AreaRadius > 0f ? $" / 範囲: {FormatRange(skill.AreaRadius)}" : string.Empty),
            1.8f,
            15f);
    }

    private void AddSkillDetailCell(Transform parent, string value, float flexibleWidth, float fontSize)
    {
        TMP_Text text = AddPickerDetailText(
            value,
            fontSize,
            TextAlignmentOptions.Left,
            68f,
            parent);
        LayoutElement layout = text.gameObject.GetComponent<LayoutElement>();
        layout.flexibleWidth = flexibleWidth;
        layout.minWidth = 0f;
    }

    private TMP_Text AddPickerDetailText(
        string value,
        float fontSize,
        TextAlignmentOptions alignment,
        float preferredHeight,
        Transform parent = null)
    {
        Transform target = parent != null ? parent : _pickerDetailsContent;
        TMP_Text text = Instantiate(_selectionCountText, target);
        text.text = value;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.enableAutoSizing = false;
        text.overflowMode = TextOverflowModes.Overflow;
        text.textWrappingMode = TextWrappingModes.Normal;
        LayoutElement layout = text.gameObject.GetComponent<LayoutElement>() ??
                               text.gameObject.AddComponent<LayoutElement>();
        layout.preferredHeight = preferredHeight;
        return text;
    }

    private static string FormatRange(float range)
    {
        return float.IsPositiveInfinity(range) ? "無制限" : $"{range:0.##}m";
    }

    private static string FormatSeconds(float seconds)
    {
        return seconds <= 0f ? "なし" : $"{seconds:0.##}秒";
    }

    private static string FormatCombatStat(CombatStat stat)
    {
        return stat switch
        {
            CombatStat.STR => "STR",
            CombatStat.INT => "INT",
            CombatStat.FAI => "FAI",
            CombatStat.AGI => "AGI",
            _ => stat.ToString(),
        };
    }

    private static string FormatTargetKind(SkillTargetKind targetKind)
    {
        return targetKind switch
        {
            SkillTargetKind.Self => "自身",
            SkillTargetKind.Enemy => "敵単体",
            SkillTargetKind.Ally => "味方単体",
            SkillTargetKind.AllyOrSelf => "味方単体/自身",
            SkillTargetKind.Point => "地点",
            SkillTargetKind.Area => "範囲",
            SkillTargetKind.RecognizedEnemies => "認識済みの敵",
            SkillTargetKind.AllAllies => "味方全体",
            _ => "なし",
        };
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
        ClearPickerDetails();
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
                GetOption(_personalityOptions, row.PersonalityIndex),
                MovementSpeedMultiplier));
        }

        return setups;
    }

    private void Refresh()
    {
        RefreshRows(_allyRows);
        RefreshRows(_enemyRows);
        RefreshEnemyFormationButton();
        RefreshStonePositionButton();
        RefreshDebugToggleButtons();
        RefreshSelectionCountText();

        int allyCount = CountSelected(_allyRows);
        int enemyCount = CountSelected(_enemyRows);
        if (_startBattleButton != null)
        {
            _startBattleButton.interactable = allyCount > 0 && enemyCount > 0 &&
                                              _weaponOptions.Count > 0 && _personalityOptions.Count > 0 &&
                                              _externalStartAllowed;
        }
    }

    private void RefreshSelectionCountText()
    {
        if (_selectionCountText == null) return;

        int allyCount = CountSelected(_allyRows);
        int enemyCount = CountSelected(_enemyRows);
        if (_detailSettingsOpen)
        {
            _selectionCountText.text = $"敵 {enemyCount}/{MaxPartyDisplayCount}人";
        }
        else
        {
            _selectionCountText.text =
                $"味方 {allyCount}/{MaxPartyDisplayCount}人 / 敵 {enemyCount}/{MaxPartyDisplayCount}人";
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
        if (row == null || row.Character == null) return;

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
        if (label == null) return;

        label.text = value;
        label.enableAutoSizing = false;
        if (label.fontSize < 1f) label.fontSize = 26f;
    }

    private static void ConfigureToolbarLabel(Button button, float fontSize)
    {
        TMP_Text label = button != null ? button.GetComponentInChildren<TMP_Text>(includeInactive: true) : null;
        if (label == null) return;

        label.fontSize = fontSize;
        label.enableAutoSizing = false;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.alignment = TextAlignmentOptions.Center;
        label.margin = Vector4.zero;
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

    private static void DestroyGeneratedObject(UnityEngine.Object target)
    {
        if (target == null) return;

        if (Application.isPlaying)
        {
            UnityEngine.Object.Destroy(target);
        }
        else
        {
            UnityEngine.Object.DestroyImmediate(target);
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

    private sealed class EnemyPresetDefinition
    {
        public EnemyPresetDefinition(
            string label,
            WeaponKind[] weapons,
            CombatAiPersonalityKind[] personalities)
        {
            Label = label;
            Weapons = weapons;
            Personalities = personalities;
        }

        public string Label { get; }
        public WeaponKind[] Weapons { get; }
        public CombatAiPersonalityKind[] Personalities { get; }
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
