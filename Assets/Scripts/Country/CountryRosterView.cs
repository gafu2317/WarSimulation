using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public sealed class CountryRosterView : MonoBehaviour, IPointerClickHandler
{
    public const float StandingArtFrameRatio = 2f / 3f;
    private static readonly Vector2 StandingArtInset = new(.045f, .03f);
    private static readonly string[] StatSortOptions = { "標準順", "CP", "最大HP", "STR", "INT", "FAI", "AGI" };
    private static readonly WeaponKind[] SkillSortKinds = ((WeaponKind[])Enum.GetValues(typeof(WeaponKind)))
        .Where(kind => kind != WeaponKind.Unarmed).ToArray();
    [SerializeField] private CountryRosterData _data;
    [SerializeField] private CountryRosterRow _rowPrefab;
    [SerializeField] private Sprite[] _weaponCategoryIcons;
    [SerializeField] private Sprite _roundedPanel;
    [SerializeField] private UnityEngine.UI.ScrollRect _list, _details;
    [SerializeField] private TMP_Dropdown _sort;
    [SerializeField] private UnityEngine.UI.Image _portrait;
    [SerializeField] private UnityEngine.UI.AspectRatioFitter _portraitFrame;
    [SerializeField] private GameObject _cpRow;
    [SerializeField] private TextMeshProUGUI _cpValue;
    [SerializeField] private CountrySoldierSilhouette _placeholder;
    [SerializeField] private TextMeshProUGUI _listCaption, _name, _subtitle, _portraitLabel, _statsTitle, _skillsTitle, _skills, _hint, _actionLabel;
    [SerializeField] private TextMeshProUGUI[] _stats;
    [SerializeField] private GameObject _learnedSection;
    [SerializeField] private GameObject[] _skillGroups;
    [SerializeField] private TextMeshProUGUI[] _skillGroupNames, _skillGroupStates, _skillGroupSkills;
    [SerializeField] private UnityEngine.UI.Button _action;
    [SerializeField] private UnityEngine.UI.Image _actionIcon;
    [SerializeField] private Sprite _equipmentIcon, _memberIcon, _trainingIcon;
    private CountryRoster _roster;
    private CountryRosterRow[] _rows;
    private Transform _statRowTwo, _statRowThree;
    private float _statSpacing;
    private readonly Vector2[] _statValueOffsets = new Vector2[4], _statCaptionSizes = new Vector2[4];
    private readonly float[] _statValueFonts = new float[4], _statCaptionFonts = new float[4];
    private bool _statRowsCompact;
    private bool _weapons;
    private int _soldier, _weapon, _school = -1;
    private float _soldierScroll = 1, _weaponScroll = 1;
    private bool _shown;
    private CountryExpeditionDraft _expeditionDraft;
    private Func<int, bool> _memberSelected;
    private Func<int, string> _unavailableReason;
    private string _memberLabel = "遠征メンバー";
    private int _previewSoldier = -1, _previewWeapon = -1;
    private SkillId _expandedSkill;
    private List<GameObject>[] _skillRows;
    private UnityEngine.UI.Button _equipmentAction;
    private TextMeshProUGUI _equipmentActionLabel;
    private (Vector2 Position, Vector2 Size, TextAlignmentOptions Alignment, TextOverflowModes Overflow) _weaponNameLayout;
    private float _portraitTopInset;
    private (Vector2 Min, Vector2 Max) _portraitOffsets;
    private RectTransform _portraitClip;
    public CountryRosterData Data => _data;
    public Sprite RoundedPanel => _roundedPanel;
    public int SelectedSoldier => _soldier;
    public int SelectedWeapon => _weapon;
    public event Action SelectionChanged, ActionRequested;
    public void PreviewWeapon(int soldier, int weapon)
    { _previewSoldier = soldier; _previewWeapon = weapon; Refresh(); }
    public void PreviewEquipment(CountryExpeditionDraft draft, string memberLabel = "遠征メンバー")
    { _expeditionDraft = draft; _memberLabel = memberLabel; Refresh(); }
    public void SetMemberSelection(Func<int, bool> selected, Func<int, string> unavailable, string label)
    { _memberSelected = selected; _unavailableReason = unavailable; _memberLabel = label; }
    public void EnableMemberDrag(RectTransform target)
    {
        for (int i = 0; i < _rows.Length; i++)
        {
            int id = i;
            _rows[i].EnableDrag(target, () => { if (_memberSelected != null && !_memberSelected(id)) { SelectSoldier(id); ActionRequested?.Invoke(); } },
                () => !_weapons && id < _data.Soldiers.Length, _list.viewport);
        }
    }
    public RectTransform ListViewport => _list.viewport;
    public void PlaceMemberControls(UnityEngine.UI.Button equipment = null)
    {
        var actions = (RectTransform)_action.transform.parent;
        actions.anchorMin = new Vector2(.55f, 1);
        actions.anchorMax = new Vector2(actions.anchorMax.x, 1);
        actions.pivot = new Vector2(actions.pivot.x, 1);
        actions.anchoredPosition = new Vector2(actions.anchoredPosition.x, 0);
        actions.sizeDelta = new Vector2(actions.sizeDelta.x, 112);
        _hint.gameObject.SetActive(false);
        var details = (RectTransform)_details.transform;
        details.offsetMin = new Vector2(details.offsetMin.x, 0);
        if (equipment == null) return;
        var rect = (RectTransform)equipment.transform;
        rect.SetParent(_skillsTitle.transform, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1, .5f);
        rect.sizeDelta = new Vector2(240, 60);
        rect.anchoredPosition = new Vector2(-8, 0);
        var label = equipment.GetComponentInChildren<TextMeshProUGUI>();
        label.fontSize = 26;
    }
    public void CreatePersonnelEquipmentAction()
    {
        _equipmentAction = Instantiate(_action, _skillsTitle.transform);
        _equipmentAction.gameObject.SetActive(false);
        _equipmentAction.name = "EquipmentAction";
        _equipmentAction.onClick = new UnityEngine.UI.Button.ButtonClickedEvent();
        _equipmentAction.onClick.AddListener(() => ActionRequested?.Invoke());
        var rect = (RectTransform)_equipmentAction.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1, .5f);
        rect.sizeDelta = new Vector2(260, 64); rect.anchoredPosition = new Vector2(-8, 0);
        var icon = _equipmentAction.transform.Find("Icon");
        if (icon != null) icon.gameObject.SetActive(false);
        _equipmentActionLabel = _equipmentAction.GetComponentInChildren<TextMeshProUGUI>(true);
        _equipmentActionLabel.fontSize = 26;
        var labelRect = _equipmentActionLabel.rectTransform;
        labelRect.offsetMin = new Vector2(12, 8); labelRect.offsetMax = new Vector2(-12, -8);
        _skillsTitle.margin = new Vector4(0, 0, 280, 0);
    }
    public void ShowPersonnelEquipmentAction(bool show)
    {
        _equipmentAction.gameObject.SetActive(show);
        _action.gameObject.SetActive(!show);
    }
    private int Weapon(int soldier) => _expeditionDraft == null ? _roster.EquippedWeapon(soldier) : _expeditionDraft.Weapon(soldier);
    private int Owner(int weapon) => _expeditionDraft == null ? _roster.WeaponOwner(weapon) : _expeditionDraft.Owner(weapon);
    private WeaponKind Profession(int soldier) => Weapon(soldier) < 0 ? WeaponKind.Unarmed : _data.Weapons[Weapon(soldier)].Kind;

    public void Initialize(CountryRoster roster)
    {
        if (_roster != null) return;
        _roster = roster;
        var nameRect = _name.rectTransform;
        _weaponNameLayout = (nameRect.anchoredPosition, nameRect.sizeDelta, _name.alignment, _name.overflowMode);
        _portraitTopInset = ((RectTransform)_portraitFrame.transform.parent).offsetMax.y;
        var art = _portrait.rectTransform;
        _portraitOffsets = (art.offsetMin, art.offsetMax);
        _portraitClip = (RectTransform)new GameObject("Art Clip", typeof(RectTransform), typeof(UnityEngine.UI.RectMask2D)).transform;
        _portraitClip.SetParent(art.parent, false);
        _portraitClip.SetSiblingIndex(art.GetSiblingIndex());
        art.SetParent(_portraitClip, false);
        _skills.raycastTarget = true;
        foreach (var label in _skillGroupSkills) label.raycastTarget = true;
        _rows = new CountryRosterRow[Math.Max(_data.Soldiers.Length, _data.Weapons.Length)];
        for (int i = 0; i < _rows.Length; i++)
        {
            int id = i;
            _rows[i] = Instantiate(_rowPrefab, _list.content); _rows[i].name = $"Row{i + 1:00}";
            _rows[i].Bind(() => Select(id));
        }
        _action.onClick.AddListener(() => ActionRequested?.Invoke());
        _sort.AddOptions(StatSortOptions.Concat(SkillSortKinds.Select(kind => $"{CountryRoster.WeaponName(kind)}のスキル数")).ToList());
        _sort.onValueChanged.AddListener(_ =>
        {
            Refresh();
            _list.StopMovement();
            Canvas.ForceUpdateCanvases();
            _list.verticalNormalizedPosition = 1;
        });
        if (isActiveAndEnabled) _roster.Changed += RosterChanged;
        Refresh();
    }

    public void ShowSoldiers(int school = -1)
    {
        if (_shown) SaveScroll();
        _shown = true;
        _weapons = false; _school = school;
        Refresh(); Canvas.ForceUpdateCanvases(); _list.verticalNormalizedPosition = _soldierScroll;
    }
    public void ShowWeapons()
    {
        if (_shown) SaveScroll();
        _shown = true;
        _weapons = true; _school = -1;
        Refresh(); Canvas.ForceUpdateCanvases(); _list.verticalNormalizedPosition = _weaponScroll;
    }
    internal Vector2 ScrollOffsets
    {
        get => new Vector2(_list.content.anchoredPosition.y, _details.content.anchoredPosition.y);
        set
        {
            StopScrolling();
            _list.content.anchoredPosition = new Vector2(0, value.x);
            _details.content.anchoredPosition = new Vector2(0, value.y);
        }
    }
    internal void StopScrolling() { _list.StopMovement(); _details.StopMovement(); }
    public void SelectSoldier(int soldier) { _soldier = soldier; if (!_weapons) Select(soldier); }
    private void Select(int id)
    {
        var position = _details.content.anchoredPosition;
        _details.StopMovement();
        if (_weapons) _weapon = id; else _soldier = id;
        Refresh(); SelectionChanged?.Invoke();
        Canvas.ForceUpdateCanvases();
        position.y = Mathf.Clamp(position.y, 0, Mathf.Max(0, _details.content.rect.height - _details.viewport.rect.height));
        _details.content.anchoredPosition = position;
        KeepSelectionVisible();
    }
    public void SetAction(string label, bool enabled, string hint = "", bool chooseMember = false)
    {
        _actionLabel.text = label; _action.interactable = enabled; _hint.text = hint;
        if (_equipmentAction != null) { _equipmentActionLabel.text = label; _equipmentAction.interactable = enabled; }
        _actionIcon.sprite = _school >= 0 ? _trainingIcon : chooseMember ? _memberIcon : _equipmentIcon;
        _actionIcon.color = enabled ? Color.white : new Color(1, 1, 1, .4f);
    }
    private int SortValue(int soldier, int criterion)
    {
        if (criterion == 0) return 0;
        if (criterion >= StatSortOptions.Length)
        {
            var kind = SkillSortKinds[criterion - StatSortOptions.Length];
            return _data.Skills == null ? 0 : _roster.Soldiers[soldier].Learned.Count(id =>
                _data.Skills.TryGetDefinition(id, out var skill) && skill.RequiredWeaponKind == kind);
        }
        if (criterion == 1) return _roster.Soldiers[soldier].CP;
        var stats = Stats(soldier, Weapon(soldier));
        return criterion switch { 2 => stats.HP, 3 => stats.STR, 4 => stats.INT, 5 => stats.FAI, _ => stats.AGI };
    }
    public void Refresh()
    {
        if (_roster == null) return;
        _listCaption.text = "一覧";
        _sort.gameObject.SetActive(!_weapons);
        int criterion = _weapons ? 0 : _sort.value;
        int count = _weapons ? _data.Weapons.Length : _data.Soldiers.Length;
        var unavailable = new bool[count];
        var members = new bool[count];
        for (int i = 0; i < _rows.Length; i++)
        {
            _rows[i].gameObject.SetActive(i < count);
            if (i >= count) continue;
            if (_weapons)
            {
                _rows[i].SetWeaponIcon(null);
                var weapon = _data.Weapons[i]; int owner = Owner(i);
                _rows[i].Refresh(weapon.Name, _weaponCategoryIcons[(int)weapon.Kind - 1],
                    owner < 0 ? "未装備" : _data.Soldiers[owner].Name + "が装備", i == _weapon, categoryIcon: true);
                _rows[i].SetNameHighlighted(_previewSoldier >= 0 && Weapon(_previewSoldier) == i);
                _rows[i].SetDimmed(false);
            }
            else
            {
                string reason = _unavailableReason?.Invoke(i) ?? "";
                bool member = _memberSelected?.Invoke(i) ?? false;
                members[i] = member;
                unavailable[i] = reason.Length > 0;
                string status = member ? unavailable[i] ? "選択中・" + reason : _memberLabel : unavailable[i] ? reason : State(i);
                bool warning = _unavailableReason == null ? !_roster.CanDeploy(i) : unavailable[i];
                int equipped = Weapon(i);
                _rows[i].SetWeaponIcon(equipped < 0 ? null : _weaponCategoryIcons[(int)_data.Weapons[equipped].Kind - 1]);
                _rows[i].Refresh(_data.Soldiers[i].Name, _data.Soldiers[i].Icon, status, i == _soldier, warning, member);
                _rows[i].SetDimmed(unavailable[i]);
            }
        }
        int position = 0;
        bool reordered = false;
        foreach (int i in Enumerable.Range(0, count).OrderByDescending(i => SortValue(i, criterion))
                     .ThenByDescending(i => members[i]).ThenBy(i => unavailable[i]))
        {
            if (_rows[i].transform.GetSiblingIndex() != position) { _rows[i].transform.SetSiblingIndex(position); reordered = true; }
            position++;
        }
        if (_weapons) RefreshWeapon(); else RefreshSoldier();
        if (_previewSoldier >= 0 && _previewWeapon >= 0) RefreshEquipmentPreview();
        if (reordered)
            KeepSelectionVisible();
    }
    private void KeepSelectionVisible()
    {
        var row = _rows[_weapons ? _weapon : _soldier];
        if (!row.gameObject.activeSelf) return;
        Canvas.ForceUpdateCanvases(); _list.StopMovement();
        var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(_list.viewport, row.transform);
        var viewport = _list.viewport.rect;
        float shift = bounds.max.y > viewport.yMax ? viewport.yMax - bounds.max.y : bounds.min.y < viewport.yMin ? viewport.yMin - bounds.min.y : 0;
        var position = _list.content.anchoredPosition;
        position.y = Mathf.Clamp(position.y + shift, 0, Mathf.Max(0, _list.content.rect.height - viewport.height));
        _list.content.anchoredPosition = position;
    }
    private void RefreshSoldier()
    {
        var definition = _data.Soldiers[_soldier]; var state = _roster.Soldiers[_soldier];
        _name.text = definition.Name;
        _name.alignment = TextAlignmentOptions.MidlineGeoAligned;
        _name.overflowMode = TextOverflowModes.Ellipsis;
        _subtitle.text = "";
        Portrait(definition.StandingArt, "", true, definition.StandingArtBounds);
        _statsTitle.text = "ステータス";
        ConfigureStatRows(true);
        _cpRow.SetActive(true);
        _cpValue.text = $"{state.CP} / {definition.MaxCP}";
        _cpValue.color = state.CP == 0 ? CountryGovernmentTheme.Error : CountryGovernmentTheme.Text;
        int weapon = Weapon(_soldier);
        var current = Values(Stats(_soldier, weapon));
        for (int i = 0; i < _stats.Length; i++)
            _stats[i].text = current[i].ToString();
        var text = new StringBuilder();
        var training = state.Training;
        if (training != null)
        {
            string progress = _roster.IsTraining(_soldier) ? $"進捗 {training.Progress(_roster.Today):P0}　途中変更不可" : "訓練予約・変更不可／開始までは出撃可能";
            text.AppendLine($"{_data.Schools[training.School].Name}　Lv.{training.Level}\n{training.Start} → {training.Due}\n{progress}\n");
        }
        if (state.RevivalDue.HasValue) text.AppendLine($"復活予定　{state.RevivalDue.Value}\n");
        if (state.Expedition != null) text.AppendLine($"遠征中　帰還 {state.Expedition.Return}\n");
        _skillsTitle.text = weapon >= 0 ? $"装備　{_data.Weapons[weapon].Name}" : "装備　なし";
        if (_school >= 0)
        {
            int previewSchool = training?.School ?? _school;
            var school = _data.Schools[previewSchool];
            text.AppendLine($"{school.Name}　2か月");
            if (!school.Gains.Equals(default(CountrySoldierStats)))
                text.AppendLine($"成長 最大HP＋{school.Gains.HP} STR＋{school.Gains.STR} INT＋{school.Gains.INT} FAI＋{school.Gains.FAI} AGI＋{school.Gains.AGI}");
            AppendTraining(school.FirstWeapon, school.FirstSkills); AppendTraining(school.SecondWeapon, school.SecondSkills);
        }
        _skills.text = text.ToString();
        _statsTitle.transform.SetSiblingIndex(0); _cpRow.transform.parent.parent.SetSiblingIndex(1);
        _learnedSection.SetActive(true);
        RefreshLearnedSkills();
        void AppendTraining(WeaponKind kind, SkillId[] slots)
        {
            for (int i = 0; i < (training?.Level ?? _roster.SchoolLevel(_school)); i++)
            {
                var skill = i < slots.Length ? slots[i] : SkillId.None;
                if (skill == SkillId.None) continue;
                text.AppendLine($"{CountryRoster.WeaponName(kind)} Lv.{i + 1}　{SkillLine(skill, state.Learned.Contains(skill) ? "習得済み" : "習得予定")}\n");
            }
        }
    }
    private void RefreshLearnedSkills()
    {
        if (_skillRows == null)
        {
            _skillRows = new List<GameObject>[_skillGroups.Length];
            for (int i = 0; i < _skillGroups.Length; i++) _skillRows[i] = new List<GameObject> { _skillGroups[i] };
        }
        foreach (var rows in _skillRows)
            foreach (var row in rows) row.SetActive(false);

        var equipped = Profession(_soldier);
        int position = 1;
        var kinds = Enumerable.Range(1, _skillGroups.Length).Select(i => (WeaponKind)i)
            .OrderBy(kind => kind == equipped ? 0 : 1);
        foreach (var kind in kinds)
        {
            int group = (int)kind - 1, index = 0;
            foreach (var skill in _roster.Soldiers[_soldier].Learned)
            {
                if (!_data.Skills.TryGetDefinition(skill, out var definition) || definition.RequiredWeaponKind != kind) continue;
                var rows = _skillRows[group];
                if (index == rows.Count) rows.Add(Instantiate(_skillGroups[group], _learnedSection.transform));
                var row = rows[index++];
                row.SetActive(true);
                row.transform.SetSiblingIndex(position++);
                var header = row.transform.Find("Header");
                header.Find("WeaponIcon").gameObject.SetActive(index == 1);
                var name = header.Find("Name").GetComponent<TextMeshProUGUI>();
                name.text = SkillName(skill);
                name.color = kind == equipped ? CountryGovernmentTheme.AccentText : CountryGovernmentTheme.Text;
                var nameRect = name.rectTransform;
                nameRect.anchorMin = nameRect.anchorMax = nameRect.pivot = new Vector2(0, .5f);
                nameRect.anchoredPosition = new Vector2(76, 0); nameRect.sizeDelta = new Vector2(220, 72);
                name.textWrappingMode = TextWrappingModes.NoWrap;
                name.overflowMode = TextOverflowModes.Ellipsis;
                header.Find("Divider").gameObject.SetActive(false);
                var separator = header.Find("Separator");
                if (separator == null)
                {
                    var label = Instantiate(name, header); label.name = "Separator";
                    label.text = "│"; label.color = CountryGovernmentTheme.Muted;
                    label.alignment = TextAlignmentOptions.Center; label.raycastTarget = false;
                    label.rectTransform.anchoredPosition = new Vector2(296, 0);
                    label.rectTransform.sizeDelta = new Vector2(24, 72);
                }
                row.transform.Find("Skills").gameObject.SetActive(false);
                var description = SkillDescription(header);
                var ability = CombatSkillFactory.Create(skill);
                string summary = ability == null ? "" : ability.EffectDescription;
                if (description.text != summary)
                {
                    description.text = summary;
                    description.rectTransform.sizeDelta = new Vector2(Mathf.Max(1, description.GetPreferredValues(summary, Mathf.Infinity, 72).x + 8), 72);
                    description.transform.parent.parent.GetComponent<UnityEngine.UI.ScrollRect>().horizontalNormalizedPosition = 0;
                }
            }
        }
    }
    private TextMeshProUGUI SkillDescription(Transform header)
    {
        var existing = header.Find("DescriptionScroll");
        if (existing != null) return existing.Find("Viewport/State").GetComponent<TextMeshProUGUI>();

        var root = new GameObject("DescriptionScroll", typeof(RectTransform), typeof(UnityEngine.UI.ScrollRect));
        var rootRect = (RectTransform)root.transform; rootRect.SetParent(header, false);
        rootRect.anchorMin = Vector2.zero; rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = new Vector2(320, 0); rootRect.offsetMax = Vector2.zero;
        var viewportObject = new GameObject("Viewport", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.RectMask2D));
        var viewport = (RectTransform)viewportObject.transform; viewport.SetParent(rootRect, false);
        viewport.anchorMin = Vector2.zero; viewport.anchorMax = Vector2.one; viewport.offsetMin = viewport.offsetMax = Vector2.zero;
        viewportObject.GetComponent<UnityEngine.UI.Image>().color = Color.clear;
        var description = header.Find("State").GetComponent<TextMeshProUGUI>();
        description.transform.SetParent(viewport, false); description.gameObject.SetActive(true);
        description.text = ""; description.color = CountryGovernmentTheme.Muted;
        description.alignment = TextAlignmentOptions.MidlineLeft; description.textWrappingMode = TextWrappingModes.NoWrap;
        description.overflowMode = TextOverflowModes.Overflow; description.raycastTarget = false;
        var rect = description.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, .5f);
        rect.anchoredPosition = Vector2.zero; rect.sizeDelta = new Vector2(1, 72);
        var scroll = root.GetComponent<UnityEngine.UI.ScrollRect>();
        scroll.content = rect; scroll.viewport = viewport; scroll.horizontal = true; scroll.vertical = false;
        scroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped;
        viewportObject.AddComponent<CountryNestedScrollInput>().Configure(scroll, _details);
        return description;
    }
    private void ConfigureStatRows(bool compact)
    {
        if (_statRowTwo == null)
        {
            _statRowTwo = _stats[1].transform.parent.parent;
            _statRowThree = _stats[3].transform.parent.parent;
            _statSpacing = _statRowTwo.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>().spacing;
            for (int i = 0; i < 4; i++)
            {
                var value = _stats[i + 1];
                var caption = value.transform.parent.Find("Caption").GetComponent<TextMeshProUGUI>();
                _statValueOffsets[i] = value.rectTransform.offsetMin;
                _statCaptionSizes[i] = caption.rectTransform.sizeDelta;
                _statValueFonts[i] = value.fontSize; _statCaptionFonts[i] = caption.fontSize;
            }
        }
        if (_statRowsCompact == compact) return;
        _statRowsCompact = compact;
        _statRowTwo.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>().spacing = compact ? 8 : _statSpacing;
        for (int i = 0; i < 4; i++)
        {
            var value = _stats[i + 1];
            var cell = value.transform.parent;
            cell.SetParent(compact || i < 2 ? _statRowTwo : _statRowThree, false);
            cell.SetSiblingIndex(compact ? i : i % 2);
            var caption = cell.Find("Caption").GetComponent<TextMeshProUGUI>();
            value.rectTransform.offsetMin = compact ? new Vector2(64, _statValueOffsets[i].y) : _statValueOffsets[i];
            caption.rectTransform.sizeDelta = compact ? new Vector2(60, _statCaptionSizes[i].y) : _statCaptionSizes[i];
            value.fontSize = compact ? 30 : _statValueFonts[i];
            caption.fontSize = compact ? 26 : _statCaptionFonts[i];
        }
        _statRowThree.gameObject.SetActive(!compact);
    }
    private void RefreshWeapon()
    {
        var weapon = _data.Weapons[_weapon]; int owner = Owner(_weapon);
        var nameRect = _name.rectTransform;
        nameRect.anchoredPosition = _weaponNameLayout.Position;
        nameRect.sizeDelta = _weaponNameLayout.Size;
        _name.alignment = _weaponNameLayout.Alignment;
        _name.overflowMode = _weaponNameLayout.Overflow;
        var portraitColumn = (RectTransform)_portraitFrame.transform.parent;
        portraitColumn.offsetMax = new Vector2(portraitColumn.offsetMax.x, _portraitTopInset);
        _name.text = weapon.Name; _subtitle.text = CountryRoster.WeaponName(weapon.Kind);
        Portrait(weapon.Image, "", false); _statsTitle.text = "装備中の能力補正";
        ConfigureStatRows(false);
        _cpRow.SetActive(false);
        _learnedSection.SetActive(false);
        var values = Values(weapon.Bonus);
        for (int i = 0; i < _stats.Length; i++)
            _stats[i].text = Signed(values[i]);
        _skillsTitle.text = "装備情報";
        _skills.text = $"装備者　{(owner < 0 ? "なし" : _data.Soldiers[owner].Name)}\n\n装備中だけ能力補正が加わる。\n習得済みスキルは持ち替えても残る。\n\n同じ品を複数人へ同時に装備することはできない。";
    }
    private void RefreshEquipmentPreview()
    {
        int soldier = _previewSoldier, weapon = _previewWeapon;
        var before = Values(Stats(soldier, Weapon(soldier))); var after = Values(Stats(soldier, weapon));
        _statsTitle.text = "装備変更前 → 変更後";
        int previous = Weapon(soldier);
        _subtitle.text = _weapons
            ? $"{(previous < 0 ? "未装備" : CountryRoster.WeaponName(_data.Weapons[previous].Kind))} → {CountryRoster.WeaponName(_data.Weapons[weapon].Kind)}　使用可：{Summary(SkillNames(soldier, previous))} → {Summary(SkillNames(soldier, weapon))}"
            : "";
        for (int i = 0; i < _stats.Length; i++)
        {
            var color = after[i] > before[i] ? CountryGovernmentTheme.Positive : after[i] < before[i] ? CountryGovernmentTheme.Error : CountryGovernmentTheme.Text;
            _stats[i].text = $"{before[i]} → <color=#{ColorUtility.ToHtmlStringRGB(color)}>{after[i]}</color>";
        }
        _skillsTitle.text = _data.Soldiers[soldier].Name + "の装備変更";
        _skills.text = EquipmentSkills(soldier, weapon);
        string Summary(string[] names) => names.Length == 1 ? names[0] : $"{names.Length}技";
    }
    private CountrySoldierStats Stats(int soldier, int weapon) => _data.Soldiers[soldier].InitialStats + _roster.Soldiers[soldier].Growth + (weapon < 0 ? default : _data.Weapons[weapon].Bonus);
    private string EquipmentSkills(int soldier, int weapon)
    {
        int previous = Weapon(soldier);
        return $"{(previous < 0 ? "未装備" : _data.Weapons[previous].Name)} → {_data.Weapons[weapon].Name}\n\n使用可能な技\n{SkillsFor(previous)} → {SkillsFor(weapon)}";
        string SkillsFor(int item) => item < 0 ? "なし" : string.Join("、", SkillNames(soldier, item));
    }
    private string[] SkillNames(int soldier, int weapon) => weapon < 0 ? Array.Empty<string>() : _roster.Soldiers[soldier].Learned
        .Where(id => _data.Skills.TryGetDefinition(id, out var definition) && definition.RequiredWeaponKind == _data.Weapons[weapon].Kind).Select(SkillName).ToArray();
    public string EquipmentComparison(int soldier, int weapon)
    {
        var before = Values(Stats(soldier, Weapon(soldier))); var after = Values(Stats(soldier, weapon));
        var labels = new[] { "最大HP", "STR", "INT", "FAI", "AGI" };
        return string.Join("\n", labels.Select((label, i) => $"{label}　{before[i]} → {after[i]}")) + "\n\n" + EquipmentSkills(soldier, weapon);
    }
    private string SkillLine(SkillId id, string status)
    {
        var ability = CombatSkillFactory.Create(id);
        string summary = ability == null ? "" : ability.EffectDescription;
        string line = $"<link=\"{(int)id}\">{SkillName(id)} ›</link>　{status}\n<size=85%><color=#{ColorUtility.ToHtmlStringRGB(CountryGovernmentTheme.Muted)}>{summary}</color></size>";
        if (_expandedSkill != id || ability == null) return line;
        string target = ability.TargetKind switch { SkillTargetKind.Self => "自分", SkillTargetKind.Ally => "味方", SkillTargetKind.AllyOrSelf => "自分・味方", SkillTargetKind.Area => "範囲", SkillTargetKind.Point => "地点", SkillTargetKind.AllAllies => "味方全員", SkillTargetKind.EnemiesAroundSelf => "周囲の敵", _ => "敵" };
        return line + $"\n<size=85%>対象 {target}　{ability.PowerDescription}\n詠唱 {ability.CastTimeSeconds:0.##}秒 ／ 標準CT {ability.CooldownSeconds:0.##}秒</size>";
    }
    public void OnPointerClick(PointerEventData data)
    {
        foreach (var text in _skillGroupSkills.Prepend(_skills))
        {
            if (!text.isActiveAndEnabled) continue;
            int index = TMP_TextUtilities.FindIntersectingLink(text, data.position, data.pressEventCamera);
            if (index < 0 || !int.TryParse(text.textInfo.linkInfo[index].GetLinkID(), out int id)) continue;
            _expandedSkill = _expandedSkill == (SkillId)id ? SkillId.None : (SkillId)id;
            Refresh(); break;
        }
    }
    private void Portrait(Sprite image, string placeholder, bool standing, Rect bounds = default)
    {
        _portraitFrame.aspectMode = standing ? UnityEngine.UI.AspectRatioFitter.AspectMode.FitInParent : UnityEngine.UI.AspectRatioFitter.AspectMode.None;
        if (standing) _portraitFrame.aspectRatio = StandingArtFrameRatio;
        if (!standing)
        {
            var rect = (RectTransform)_portraitFrame.transform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
        _portrait.sprite = image; _portrait.enabled = image != null; _placeholder.enabled = standing && image == null;
        bool fitArtwork = standing && image != null && bounds.width > 0 && bounds.height > 0;
        _portrait.preserveAspect = !fitArtwork;
        var inset = fitArtwork ? StandingArtInset : Vector2.zero;
        _portraitClip.anchorMin = inset;
        _portraitClip.anchorMax = Vector2.one - inset;
        _portraitClip.offsetMin = _portraitClip.offsetMax = Vector2.zero;
        var art = _portrait.rectTransform;
        var anchorMin = Vector2.zero;
        var anchorMax = Vector2.one;
        if (fitArtwork)
        {
            var available = Vector2.one - 2 * inset;
            float clipRatio = StandingArtFrameRatio * available.x / available.y;
            float imageRatio = image.rect.width / image.rect.height / clipRatio;
            float height = 1 / bounds.height;
            var size = new Vector2(height * imageRatio, height);
            var visibleOrigin = new Vector2((1 - size.x * bounds.width) / 2, 0);
            anchorMin = visibleOrigin - Vector2.Scale(bounds.min, size);
            anchorMax = anchorMin + size;
        }
        art.anchorMin = anchorMin;
        art.anchorMax = anchorMax;
        art.offsetMin = fitArtwork ? Vector2.zero : _portraitOffsets.Min;
        art.offsetMax = fitArtwork ? Vector2.zero : _portraitOffsets.Max;
        _portraitLabel.text = image == null ? placeholder : "";
    }
    private static string Signed(int value) => value >= 0 ? $"+{value}" : value.ToString();
    private string State(int soldier)
    {
        var state = _roster.Soldiers[soldier];
        if (state.CP == 0) return "復活待ち";
        if (state.Expedition != null) return state.Expedition.Returning ? "遠征・帰路" : "遠征・往路";
        return state.Training == null ? "待機中" : _roster.IsTraining(soldier) ? _data.Schools[state.Training.School].Name + "で訓練中" : "訓練予約";
    }
    private string SkillName(SkillId skill) => _data.SkillName(skill);
    private static int[] Values(CountrySoldierStats stats) => new[] { stats.HP, stats.STR, stats.INT, stats.FAI, stats.AGI };
    private void RosterChanged() { Refresh(); SelectionChanged?.Invoke(); }
    private void SaveScroll()
    { if (_weapons) _weaponScroll = _list.verticalNormalizedPosition; else _soldierScroll = _list.verticalNormalizedPosition; }
    private void LateUpdate()
    {
        if (_roster == null || _weapons) return;
        var title = _name.rectTransform;
        var frame = (RectTransform)_portraitFrame.transform;
        var column = (RectTransform)frame.parent;
        var action = (RectTransform)_action.transform;
        var center = title.parent.InverseTransformPoint(frame.TransformPoint(frame.rect.center));
        center.y = title.parent.InverseTransformPoint(action.TransformPoint(action.rect.center)).y;
        if (Mathf.Abs(title.rect.width - column.rect.width) > .01f)
            title.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, column.rect.width);
        var position = title.localPosition;
        position.x = center.x - title.rect.center.x;
        position.y = center.y - title.rect.center.y;
        if (title.localPosition != position) title.localPosition = position;
        var body = (RectTransform)column.parent;
        float titleBottom = body.InverseTransformPoint(title.TransformPoint(new Vector3(title.rect.center.x, title.rect.yMin))).y;
        float topInset = Mathf.Min(_portraitTopInset, titleBottom - body.rect.yMax - 8);
        if (Mathf.Abs(column.offsetMax.y - topInset) > .01f)
            column.offsetMax = new Vector2(column.offsetMax.x, topInset);
    }
    private void Start()
    {
        Canvas.ForceUpdateCanvases();
        _list.verticalNormalizedPosition = 1; _details.verticalNormalizedPosition = 1;
        _soldierScroll = _weaponScroll = 1;
    }
    private void OnEnable() { if (_roster != null) { _roster.Changed += RosterChanged; Refresh(); } }
    private void OnDisable() { if (_roster != null) _roster.Changed -= RosterChanged; }
}

internal sealed class CountryEquipmentPopup
{
    private readonly CountryRosterView _view;
    private readonly UnityEngine.UI.Button _equipment;
    private readonly CanvasGroup _shell;
    private readonly GameObject _overlay;
    private readonly RectTransform _safe, _viewRect;
    private Transform _parent;
    private int _sibling;
    private Vector2 _min, _max, _offsetMin, _offsetMax, _pivot;

    public CountryEquipmentPopup(CountryRosterView view, UnityEngine.UI.Button equipment, CanvasGroup shell, Transform window, Action close)
    {
        _view = view; _equipment = equipment; _shell = shell;
        _viewRect = (RectTransform)view.transform;
        _overlay = new GameObject("EquipmentPopup", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button));
        var overlayRect = (RectTransform)_overlay.transform;
        overlayRect.SetParent(window, false); overlayRect.anchorMin = Vector2.zero; overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = overlayRect.offsetMax = Vector2.zero;
        _overlay.GetComponent<UnityEngine.UI.Image>().color = new Color(0, 0, 0, .7f);
        var backdrop = _overlay.GetComponent<UnityEngine.UI.Button>();
        backdrop.transition = UnityEngine.UI.Selectable.Transition.None;
        backdrop.onClick.AddListener(() => close());
        var safe = new GameObject("SafeArea", typeof(RectTransform));
        _safe = (RectTransform)safe.transform; _safe.SetParent(overlayRect, false);
        _safe.offsetMin = _safe.offsetMax = Vector2.zero;
        var panel = new GameObject("Panel", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        var panelRect = (RectTransform)panel.transform; panelRect.SetParent(_safe, false);
        panelRect.anchorMin = Vector2.zero; panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = new Vector2(16, 24); panelRect.offsetMax = new Vector2(-16, -64);
        var panelImage = panel.GetComponent<UnityEngine.UI.Image>();
        panelImage.sprite = view.RoundedPanel; panelImage.type = UnityEngine.UI.Image.Type.Sliced;
        panelImage.color = CountryGovernmentTheme.Panel;
        var closeButton = UnityEngine.Object.Instantiate(equipment, _safe);
        closeButton.name = "Close"; closeButton.onClick.RemoveAllListeners(); closeButton.onClick.AddListener(() => close());
        closeButton.GetComponentInChildren<TextMeshProUGUI>(true).text = "閉じる";
        var buttonRect = (RectTransform)closeButton.transform;
        buttonRect.anchorMin = buttonRect.anchorMax = buttonRect.pivot = new Vector2(1, 1);
        buttonRect.sizeDelta = new Vector2(200, 72); buttonRect.anchoredPosition = new Vector2(-32, -76);
        _overlay.SetActive(false);
    }

    public void Open()
    {
        var area = UnityEngine.Device.Screen.safeArea;
        float width = UnityEngine.Device.Screen.width, height = UnityEngine.Device.Screen.height;
        _safe.anchorMin = width > 0 && height > 0 ? new Vector2(area.xMin / width, area.yMin / height) : Vector2.zero;
        _safe.anchorMax = width > 0 && height > 0 ? new Vector2(area.xMax / width, area.yMax / height) : Vector2.one;
        _parent = _viewRect.parent; _sibling = _viewRect.GetSiblingIndex();
        _min = _viewRect.anchorMin; _max = _viewRect.anchorMax; _offsetMin = _viewRect.offsetMin; _offsetMax = _viewRect.offsetMax; _pivot = _viewRect.pivot;
        _overlay.SetActive(true); _overlay.transform.SetAsLastSibling();
        _shell.interactable = false; _shell.blocksRaycasts = false;
        _viewRect.SetParent(_safe, false); _viewRect.SetSiblingIndex(1);
        _viewRect.anchorMin = Vector2.zero; _viewRect.anchorMax = Vector2.one;
        _viewRect.offsetMin = new Vector2(32, 32); _viewRect.offsetMax = new Vector2(-32, -156);
        _equipment.gameObject.SetActive(false);
    }

    public void Close()
    {
        if (!_overlay.activeSelf) return;
        _viewRect.SetParent(_parent, false); _viewRect.SetSiblingIndex(_sibling);
        _viewRect.anchorMin = _min; _viewRect.anchorMax = _max; _viewRect.pivot = _pivot;
        _viewRect.offsetMin = _offsetMin; _viewRect.offsetMax = _offsetMax;
        _equipment.gameObject.SetActive(true);
        _shell.interactable = true; _shell.blocksRaycasts = true;
        _overlay.SetActive(false);
    }
}
