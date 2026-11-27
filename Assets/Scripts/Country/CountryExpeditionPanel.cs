using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public sealed class CountryExpeditionPanel : MonoBehaviour
{
    [SerializeField] private CountryWorldMapData _data;
    [SerializeField] private CountryGovernmentPanel _government;
    [SerializeField] private CountryPersonnelPanel _personnel;
    [SerializeField] private CountrySettingsPanel _settings;
    [SerializeField] private CountryDefensePanel _defense;
    [SerializeField] private GameObject _window, _logWindow;
    [SerializeField] private CanvasGroup _shell;
    [SerializeField] private CountryWorldMapView _map;
    [SerializeField] private CountryRosterView _view;
    [SerializeField] private CountryRosterRow _tokenPrefab;
    [SerializeField] private RectTransform _members, _logContent;
    [SerializeField] private GameObject _destinationPage, _formationPage, _route;
    [SerializeField] private GameObject _emptySlotPrefab;
    [SerializeField] private CountryExpeditionRow _rowPrefab;
    [SerializeField] private CountryRosterConfirm _confirmation, _logDetails;
    [SerializeField] private Button _open, _back, _next, _depart, _equipment, _log, _logClose;
    [SerializeField] private TextMeshProUGUI _dateText, _context, _targetName, _ownerName, _days, _schedule, _targetNotice, _count, _logLabel, _logEmpty;
    [SerializeField] private TextMeshProUGUI _targetStep, _memberStep, _departureStatus;
    private readonly List<CountryExpeditionRow> _logRows = new();
    private CountryRosterRow[] _tokens;
    private GameObject[] _emptySlots;
    private CountryRoster _roster;
    private CountryExpeditionState _state;
    private DateManager _date;
    private CountryExpeditionDraft _draft;
    private InputAction _escape;
    private GameObject _returnFocus;
    private bool _cameraInput, _comparisonInput;
    private int _equipmentSoldier = -1;
    private Vector2 _formationScrollOffsets;
    private CountryEquipmentPopup _equipmentPopup;
    private RectTransform _pageBand;
    private bool _showingMembers;
    [SerializeField] private TextMeshProUGUI _hudCount, _departureNotice;
    private float _noticeUntil;
    public bool IsOpen => _window.activeSelf;
    public CountryExpeditionDraft Draft => _draft;
    public CountryExpeditionState State => _state;
    internal CountryWorldMapData WorldData => _data;
    internal CountryRosterData RosterData => _view.Data;

    private void Awake()
    {
        _open.onClick.AddListener(Open); _back.onClick.AddListener(Back); _depart.onClick.AddListener(ConfirmDeparture);
        _next.onClick.AddListener(() => ShowPage(true));
        _equipment.onClick.AddListener(ChooseEquipment); _log.onClick.AddListener(OpenLog); _logClose.onClick.AddListener(CloseLog);
        _view.SelectionChanged += Refresh; _view.ActionRequested += Choose;
        _escape = new InputAction("Close expedition", InputActionType.Button, "<Keyboard>/escape");
        _escape.performed += _ => {
            if (_logDetails.IsOpen) _logDetails.Close(); else if (_confirmation.IsOpen) _confirmation.Close();
            else if (_logWindow.activeSelf) CloseLog(); else Back();
        };
    }

    private void Start()
    {
        _date = DateManager.Instance;
        if (_date == null || PlayDataManager.Instance == null) { enabled = false; return; }
        _state = PlayDataManager.Instance.GetOrCreateExpeditions(_data, _view.Data, CountryDate.From(_date));
        _roster = PlayDataManager.Instance.Roster;
        _view.SetMemberSelection(id => _draft != null && _draft.Contains(id), SelectionReason, "遠征メンバー");
        _view.Initialize(_roster); _map.Initialize(_state, SelectRegion);
        _view.PlaceMemberControls(_equipment);
        var band = new GameObject("PageBarBackground", typeof(RectTransform), typeof(Image));
        _pageBand = (RectTransform)band.transform; _pageBand.SetParent(_window.transform, false); _pageBand.SetAsFirstSibling();
        var bandImage = band.GetComponent<Image>(); bandImage.color = CountryGovernmentTheme.Panel; bandImage.raycastTarget = false;
        _equipmentPopup = new CountryEquipmentPopup(_view, _equipment, _shell, _window.transform, ChooseEquipment);
        _tokens = new CountryRosterRow[_roster.Soldiers.Count];
        for (int i = 0; i < _tokens.Length; i++)
        {
            int id = i;
            _tokens[i] = Instantiate(_tokenPrefab, _members); _tokens[i].name = $"Member{i + 1:00}";
            _tokens[i].SetCaptionVisible(true);
            _tokens[i].Bind(() => _view.SelectSoldier(id)); _tokens[i].gameObject.SetActive(false);
        }
        _emptySlots = new GameObject[26];
        _view.EnableMemberDrag((RectTransform)_members.parent);
        for (int i = 0; i < _tokens.Length; i++)
        {
            int id = i;
            _view.BindMemberClicks(_tokens[i], id, memberSlot: true);
            _tokens[i].EnableDrag(_view.ListViewport, () => { if (_draft != null && _draft.Contains(id) && _equipmentSoldier < 0) { _view.SelectSoldier(id); Choose(); } });
        }
        for (int i = 0; i < _emptySlots.Length; i++) _emptySlots[i] = Instantiate(_emptySlotPrefab, _members);
        _state.Changed += StateChanged;
        RefreshHud();
    }

    public void Open()
    {
        if (_state == null || IsOpen || !isActiveAndEnabled) return;
        _defense?.Close();
        _settings.Close(); _personnel.Close(); _government.Close(); _government.FacilityPopup?.Close(); _date.Pause();
        _returnFocus = EventSystem.current?.currentSelectedGameObject;
        _cameraInput = _government.CameraControls != null && _government.CameraControls.InputEnabled;
        _comparisonInput = _government.Comparison != null && _government.Comparison.InputEnabled;
        if (_government.CameraControls != null) _government.CameraControls.InputEnabled = false;
        if (_government.Comparison != null) _government.Comparison.InputEnabled = false;
        _draft = new CountryExpeditionDraft(_roster); _equipmentSoldier = -1;
        _departureNotice.transform.parent.gameObject.SetActive(false);
        _window.SetActive(true); _escape.Enable(); _view.ShowSoldiers();
        _view.ScrollOffsets = Vector2.zero;
        ShowPage(false);
    }

    public void SelectRegion(int region)
    {
        if (!IsOpen) return;
        _draft.Target = region; Refresh();
    }
    private void ChooseEquipment()
    {
        int soldier = _view.SelectedSoldier;
        if (_equipmentSoldier >= 0)
        {
            _view.SelectSoldier(_equipmentSoldier);
            _equipmentSoldier = -1;
            _view.ShowSoldiers();
            _equipmentPopup.Close();
            _view.ScrollOffsets = _formationScrollOffsets;
            Refresh();
            return;
        }
        if (_roster.Soldiers[soldier].Expedition != null) return;
        _equipmentSoldier = soldier;
        _formationScrollOffsets = _view.ScrollOffsets;
        _view.StopScrolling();
        _equipmentPopup.Open();
        _view.ShowWeapons(); Refresh();
    }
    private void Choose()
    {
        if (_equipmentSoldier >= 0)
        {
            int weapon = _view.SelectedWeapon;
            if (!_roster.IsUsableWeapon(weapon)) return;
            int owner = _roster.WeaponOwner(weapon);
            if (owner >= 0 && _roster.Soldiers[owner].Expedition != null) return;
            if (_roster.TryEquip(_equipmentSoldier, weapon)) ChooseEquipment(); else Refresh();
            return;
        }
        int soldier = _view.SelectedSoldier;
        if (!_draft.Contains(soldier) && (_draft.Members.Count >= 26 || !_state.CanSelect(soldier, _draft.Target, out _))) return;
        _draft.Toggle(soldier); Refresh();
    }

    private void Refresh()
    {
        if (!IsOpen || _draft == null) return;
        _dateText.text = $"{_date.CurrentMonth}月{_date.CurrentDay}日";
        _context.text = "";
        _logLabel.text = $"遠征中 {_state.Active.Count}隊";
        _count.text = $"遠征メンバー　{_draft.Members.Count} / 26人";
        _map.Refresh(_draft.Target);
        int target = _draft.Target;
        bool hasTarget = target >= 0, targetReady = _state.CanTarget(target);
        bool ownTerritory = hasTarget && _state.Owner(target) == 0;
        int days = hasTarget ? _data.TravelDays(target) : 0;
        _route.SetActive(hasTarget && !ownTerritory);
        _ownerName.gameObject.SetActive(hasTarget);
        var ownerRect = _ownerName.rectTransform;
        var noticeRect = _targetNotice.rectTransform;
        noticeRect.anchoredPosition = new Vector2(noticeRect.anchoredPosition.x,
            ownerRect.anchoredPosition.y - (hasTarget ? ownerRect.rect.height + 12 : 0));
        if (hasTarget)
        {
            _context.text = $"遠征先　{_data.Regions[target].Name}";
            if (targetReady)
                _context.text += $"　到着 {_state.Today.AddDays(days)}　帰還 {_state.Today.AddDays(days * 2)}";
            _targetName.text = _data.Regions[target].Name;
            _ownerName.text = _data.Nations[_state.Owner(target)].Name;
            _days.text = $"{days}日";
            _targetNotice.text = targetReady ? "遠征可能・到着すると戦闘" : ownTerritory ? "自国の領地" : _state.Active.Any(trip => trip.Target == target) ? "遠征不可・すでに部隊が遠征中" : "遠征不可・自領に隣接していない";
        }
        else { _targetName.text = "領地を選ぶ"; _targetNotice.text = "明るい境界の領地へ遠征できる"; }
        _targetNotice.color = targetReady ? CountryGovernmentTheme.PositiveInk :
            hasTarget && !ownTerritory ? CountryGovernmentTheme.ErrorInk : CountryGovernmentTheme.PaperMuted;
        _schedule.text = targetReady ? $"出発　{_state.Today}\n到着　{_state.Today.AddDays(days)}\n帰還　{_state.Today.AddDays(days * 2)}" : "";
        _schedule.gameObject.SetActive(targetReady);
        for (int i = 0; i < _tokens.Length; i++)
        {
            bool selected = _draft.Contains(i); _tokens[i].gameObject.SetActive(selected);
            if (!selected) continue;
            int weapon = _draft.Weapon(i);
            _tokens[i].Refresh(_roster.Data.Soldiers[i].Name, _roster.Data.Soldiers[i].Icon, weapon < 0 ? "武器なし" : _roster.Weapon(weapon).Name, _view.SelectedSoldier == i, weapon < 0);
        }
        for (int i = 0; i < _emptySlots.Length; i++) _emptySlots[i].SetActive(i < 26 - _draft.Members.Count);
        _view.PreviewEquipment(_draft);
        _view.PreviewWeapon(_equipmentSoldier, _view.SelectedWeapon);
        _equipment.GetComponentInChildren<TextMeshProUGUI>().text = _equipmentSoldier >= 0 ? "‹ 兵士に戻る" : "武器を変更";
        _equipment.interactable = _equipmentSoldier >= 0 || _roster.Soldiers[_view.SelectedSoldier].Expedition == null;
        if (_equipmentSoldier >= 0)
        {
            int owner = _roster.WeaponOwner(_view.SelectedWeapon);
            bool available = _roster.IsUsableWeapon(_view.SelectedWeapon) && (owner < 0 || _roster.Soldiers[owner].Expedition == null);
            _view.SetAction("この武器を装備", available, available ? "選択すると装備を変更する" : "帰還まで持ち出し中");
        }
        else
        {
            int soldier = _view.SelectedSoldier;
            bool selected = _draft.Contains(soldier), available = _state.CanSelect(soldier, _draft.Target, out string reason);
            _view.SetAction(selected ? "メンバーから外す" : "メンバーに加える", selected || available, available ? "出発までメンバーと武器を変更できる" : reason, chooseMember: true);
        }
        bool valid = _state.ValidateDeparture(_draft.Target, _draft.Members, _draft.Weapons(), out string departureReason);
        bool membersReady = _draft.Members.Count > 0;
        UpdateStep(_targetStep, "領地選択", !_showingMembers, targetReady);
        UpdateStep(_memberStep, "編成", _showingMembers, valid);
        bool missingWeapon = _draft.Members.Any(id => _roster.EquippedWeapon(id) < 0);
        bool canWarn = targetReady && membersReady && missingWeapon && _draft.Members.All(id => _state.CanSelect(id, _draft.Target, out _));
        _depart.interactable = valid || canWarn;
        _departureStatus.gameObject.SetActive(_showingMembers);
        _departureStatus.text = valid ? $"{_draft.Members.Count}人で出発" : departureReason;
        _departureStatus.color = valid ? CountryGovernmentTheme.Text : CountryGovernmentTheme.Error;
    }

    private static void UpdateStep(TextMeshProUGUI label, string name, bool current, bool ready)
    {
        label.text = $"{(current ? "› " : "")}{name}{(ready ? " ✓" : "")}";
        label.color = current ? CountryGovernmentTheme.AccentText : CountryGovernmentTheme.Muted;
    }

    private void ShowPage(bool formation)
    {
        if (!IsOpen || _draft == null || !_shell.interactable) return;
        _showingMembers = formation;
        _view.StopScrolling();
        _destinationPage.SetActive(!formation);
        _formationPage.SetActive(formation);
        _next.gameObject.SetActive(!formation);
        _depart.gameObject.SetActive(formation);
        _back.GetComponentInChildren<TextMeshProUGUI>().text = formation ? "‹ 領地を選ぶ" : CountryGovernmentTheme.CountryReturnText;
        Refresh();
        _back.Select();
    }

    private string SelectionReason(int soldier)
    {
        if (_draft == null || _state.CanSelect(soldier, _draft.Target, out string reason)) return "";
        return reason.StartsWith("訓練開始") ? "帰還が間に合わない" : reason;
    }

    private void ConfirmDeparture()
    {
        if (!_state.ValidateDeparture(_draft.Target, _draft.Members, _draft.Weapons(), out _))
        {
            var unarmed = _draft.Members.Where(id => _roster.EquippedWeapon(id) < 0).Select(id => _roster.Data.Soldiers[id].Name).ToArray();
            if (unarmed.Length > 0) _confirmation.ShowDetails("武器を選ぶ", string.Join("、", unarmed) + "の武器を設定する");
            else Refresh();
            return;
        }
        int target = _draft.Target; int[] members = _draft.Members.ToArray(), weapons = _draft.Weapons();
        var trip = new CountryExpeditionTrip(target, _state.Today, _data.TravelDays(target), members, weapons);
        string body = $"1人につきCP{_data.BattleCPCost}・武器耐久1を消費する。\n\n" +
            CountryExpeditionRow.Details(_state, trip, _roster, false) + "\n\n帰還までメンバーと装備は変更できない。";
        string warnings = string.Join("\n", members.Select((id, i) => _roster.BattleWarning(id, weapons[i], _data.BattleCPCost)).Where(text => text.Length > 0));
        if (warnings.Length > 0) body += "\n\n" + warnings;
        _confirmation.Show("遠征に出発", body, "出発する", () => {
            if (_state.TryDepart(target, members, weapons, out _))
            {
                Close();
                ShowHudNotice($"{_data.Regions[target].Name}へ出発\n到着 {trip.Arrival} ／ 帰還 {trip.Return}");
            }
            else Refresh();
        });
    }

    internal void ShowBattleLaunchFailure() => ShowHudNotice("戦闘画面を開けなかった\n時間を進めると再試行できる");

    private void ShowHudNotice(string message)
    {
        _departureNotice.text = message;
        _departureNotice.transform.parent.gameObject.SetActive(true);
        _departureNotice.gameObject.SetActive(true);
        _noticeUntil = Time.unscaledTime + 5;
    }
    private void OpenLog()
    {
        _shell.interactable = _shell.blocksRaycasts = false; _logWindow.SetActive(true);
        _logEmpty.gameObject.SetActive(_state.Active.Count == 0);
        foreach (var row in _logRows) { row.gameObject.SetActive(false); Destroy(row.gameObject); } _logRows.Clear();
        foreach (var trip in _state.Active)
        {
            var row = Instantiate(_rowPrefab, _logContent); _logRows.Add(row);
            row.Bind(_state, trip, false, () => _logDetails.ShowDetails("遠征の状況", CountryExpeditionRow.Details(_state, trip, _roster, false)));
        }
        _logClose.Select();
    }
    private void CloseLog()
    {
        _logDetails.Close(); _logWindow.SetActive(false); _shell.interactable = _shell.blocksRaycasts = true;
    }
    private void Back()
    {
        if (_equipmentSoldier >= 0) ChooseEquipment();
        else if (_showingMembers) ShowPage(false);
        else Close();
    }
    public void Close()
    {
        _equipmentPopup?.Close();
        if (!IsOpen) return;
        _confirmation.Close(); CloseLog(); _window.SetActive(false); _escape.Disable(); _view.PreviewEquipment(null); _draft = null;
        if (_government.CameraControls != null) _government.CameraControls.InputEnabled = _cameraInput;
        if (_government.Comparison != null) _government.Comparison.InputEnabled = _comparisonInput;
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(_returnFocus != null && _returnFocus.activeInHierarchy ? _returnFocus : null);
    }
    private void RefreshHud()
    {
        _hudCount.text = $"遠征中 {_state.Active.Count}隊";
        _hudCount.gameObject.SetActive(true); _hudCount.transform.parent.gameObject.SetActive(_state.Active.Count > 0);
    }
    private void StateChanged() { RefreshHud(); if (IsOpen) Refresh(); }
    private void Update()
    {
        if (IsOpen && _pageBand != null)
            CountrySafeArea.FitBottomBand(_pageBand, (RectTransform)_shell.transform,
                CountryGovernmentTheme.PageBarHeight + CountryGovernmentTheme.PageBarNoticeHeight);
        if (_departureNotice.transform.parent.gameObject.activeSelf && Time.unscaledTime >= _noticeUntil) _departureNotice.transform.parent.gameObject.SetActive(false);
    }
    private void OnDisable() => Close();
    private void OnDestroy()
    {
        _open?.onClick.RemoveListener(Open); _escape?.Dispose();
        if (_state != null) _state.Changed -= StateChanged;
    }
}
