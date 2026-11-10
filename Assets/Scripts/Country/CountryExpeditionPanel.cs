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
    [SerializeField] private ScrollRect _page;
    [SerializeField] private GameObject _emptySlotPrefab;
    [SerializeField] private TextMeshProUGUI _formationTarget;
    [SerializeField] private CountryExpeditionRow _rowPrefab;
    [SerializeField] private CountryRosterConfirm _confirmation, _logDetails;
    [SerializeField] private Button _open, _back, _depart, _equipment, _log, _logClose;
    [SerializeField] private TextMeshProUGUI _dateText, _context, _targetName, _ownerName, _days, _schedule, _targetNotice, _count, _summary, _departLabel, _logLabel, _logEmpty;
    [SerializeField] private Graphic _departFrame;
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
    private CountryEquipmentPopup _equipmentPopup;
    private TextMeshProUGUI _targetStep, _memberStep;
    [SerializeField] private TextMeshProUGUI _hudCount, _departureNotice;
    private float _noticeUntil;
    public bool IsOpen => _window.activeSelf;
    public CountryExpeditionDraft Draft => _draft;
    public CountryExpeditionState State => _state;

    private void Awake()
    {
        _open.onClick.AddListener(Open); _back.onClick.AddListener(Back); _depart.onClick.AddListener(ConfirmDeparture);
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
        _formationTarget.fontSize = 44;
        var mapTitle = Instantiate(_formationTarget, _page.content.Find("Map"));
        mapTitle.name = "DestinationHeading"; mapTitle.text = "遠征先を選ぶ";
        mapTitle.margin = Vector4.zero; mapTitle.raycastTarget = false;
        var mapTitleRect = mapTitle.rectTransform;
        mapTitleRect.anchorMin = mapTitleRect.anchorMax = mapTitleRect.pivot = new Vector2(0, 1);
        mapTitleRect.anchoredPosition = new Vector2(0, 72); mapTitleRect.sizeDelta = new Vector2(480, 64);
        var header = _shell.transform.Find("Header");
        var background = header.GetComponent<Image>();
        background.enabled = false;
        var band = new GameObject("HeaderBackground", typeof(RectTransform), typeof(Image));
        var bandRect = (RectTransform)band.transform; bandRect.SetParent(header, false); bandRect.SetAsFirstSibling();
        bandRect.anchorMin = Vector2.zero; bandRect.anchorMax = Vector2.one;
        var safeArea = UnityEngine.Device.Screen.safeArea;
        float scale = header.GetComponentInParent<Canvas>().rootCanvas.scaleFactor;
        bandRect.offsetMin = new Vector2(-safeArea.xMin / scale, 0);
        bandRect.offsetMax = new Vector2((UnityEngine.Device.Screen.width - safeArea.xMax) / scale, 0);
        var bandImage = band.GetComponent<Image>(); bandImage.color = CountryGovernmentTheme.Panel; bandImage.raycastTarget = true;
        var headerRect = (RectTransform)header;
        headerRect.sizeDelta = new Vector2(headerRect.sizeDelta.x, 144);
        var title = HeaderLabel(header, "ExpeditionTitle", "遠征計画", 200, 220);
        title.fontSize = 36;
        _targetStep = HeaderLabel(header, "DestinationStep", "遠征先の決定", 450, 260);
        _targetStep.alignment = TextAlignmentOptions.MidlineRight;
        float arrowX = 450 + 260 + 24;
        var arrow = HeaderLabel(header, "StepArrow", "→", arrowX, 48);
        arrow.alignment = TextAlignmentOptions.Center; arrow.raycastTarget = false;
        _memberStep = HeaderLabel(header, "MemberStep", "メンバーの決定", arrowX + 72, 340);
        AddStep(_targetStep, false); AddStep(_memberStep, true);
        var backRect = (RectTransform)_back.transform; backRect.SetParent(header, false);
        backRect.anchorMin = backRect.anchorMax = backRect.pivot = new Vector2(0, .5f);
        backRect.anchoredPosition = new Vector2(24, 0); backRect.sizeDelta = new Vector2(150, 64);
        var backFeedback = _back.GetComponent<CountryButtonFeedback>();
        if (backFeedback != null) backFeedback.enabled = false;
        foreach (string part in new[] { "Shadow", "Focus", "Frame", "Outline" })
        {
            var child = _back.transform.Find(part);
            if (child != null) child.gameObject.SetActive(false);
        }
        var backSurface = _back.transform.Find("Surface").GetComponent<Image>();
        backSurface.sprite = null; backSurface.type = Image.Type.Simple;
        backSurface.color = CountryGovernmentTheme.Background; _back.targetGraphic = backSurface;
        var backLabel = _back.GetComponentInChildren<TextMeshProUGUI>();
        backLabel.text = "‹ 戻る"; backLabel.fontSize = 28; backLabel.color = CountryGovernmentTheme.Text;
        var departRect = (RectTransform)_depart.transform; departRect.SetParent(header, false);
        departRect.anchorMin = departRect.anchorMax = departRect.pivot = new Vector2(1, .5f);
        departRect.anchoredPosition = new Vector2(-24, 0); departRect.sizeDelta = new Vector2(352, 96);
        _page.content.GetComponent<VerticalLayoutGroup>().padding.top = 248;
        _summary.gameObject.SetActive(false);
        var bar = (RectTransform)_page.verticalScrollbar.transform;
        bar.offsetMin = new Vector2(bar.offsetMin.x, 24);
        bar.offsetMax = new Vector2(bar.offsetMax.x, -168);
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
        _window.SetActive(true); _escape.Enable(); _view.ShowSoldiers(); Refresh();
        _page.GetComponent<CountryExpeditionPageLayout>().Refresh();
        Canvas.ForceUpdateCanvases(); _page.StopMovement(); _page.verticalNormalizedPosition = 1;
        _view.ResetDetailScroll();
        _back.Select();
    }

    public void SelectRegion(int region)
    {
        if (!IsOpen) return;
        _draft.Target = region; Refresh();
    }
    private void ChooseEquipment()
    {
        int soldier = _view.SelectedSoldier;
        if (_equipmentSoldier >= 0) { _view.SelectSoldier(_equipmentSoldier); _equipmentSoldier = -1; _equipmentPopup.Close(); _page.GetComponent<CountryExpeditionPageLayout>().enabled = true; _view.ShowSoldiers(); Refresh(); return; }
        if (_roster.Soldiers[soldier].Expedition != null) return;
        _equipmentSoldier = soldier; _page.GetComponent<CountryExpeditionPageLayout>().enabled = false; _equipmentPopup.Open(); _view.ShowWeapons(); Refresh();
    }
    private void Choose()
    {
        if (_equipmentSoldier >= 0)
        {
            int weapon = _view.SelectedWeapon, owner = _roster.WeaponOwner(weapon);
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
        string targetCaption = _draft.Target < 0 ? "遠征先を選ぶ" : $"遠征先　{_data.Regions[_draft.Target].Name}";
        string timing = _draft.Target >= 0 && _state.CanTarget(_draft.Target)
            ? $"　到着 {_state.Today.AddDays(_data.TravelDays(_draft.Target))}　帰還 {_state.Today.AddDays(_data.TravelDays(_draft.Target) * 2)}" : "";
        _context.text = $"{targetCaption}{timing}　{_draft.Members.Count}人";
        _formationTarget.text = _equipmentSoldier >= 0 ? $"{_roster.Data.Soldiers[_equipmentSoldier].Name}の武器を選ぶ" : "遠征メンバーを選ぶ";
        _logLabel.text = $"遠征中 {_state.Active.Count}隊";
        _count.text = $"遠征メンバー　{_draft.Members.Count} / 26人";
        _map.Refresh(_draft.Target);
        if (_draft.Target >= 0)
        {
            int target = _draft.Target, days = _data.TravelDays(target);
            _targetName.text = _data.Regions[target].Name;
            _ownerName.text = _data.Nations[_state.Owner(target)].Name;
            _days.text = _state.Owner(target) == 0 ? "—" : $"{days}日";
            _schedule.text = !_state.CanTarget(target) ? "" : $"出発　{_state.Today}\n到着　{_state.Today.AddDays(days)}\n帰還　{_state.Today.AddDays(days * 2)}";
            _targetNotice.text = _state.CanTarget(target) ? "遠征可能・到着すると領地を獲得" : _state.Owner(target) == 0 ? "自国の領地" : _state.Active.Any(trip => trip.Target == target) ? "遠征不可・すでに部隊が遠征中" : "遠征不可・自領に隣接していない";
            _targetNotice.color = _state.CanTarget(target) ? CountryGovernmentTheme.PositiveInk : _state.Owner(target) == 0 ? CountryGovernmentTheme.PaperMuted : CountryGovernmentTheme.ErrorInk;
        }
        else { _targetName.text = "領地を選ぶ"; _ownerName.text = ""; _days.text = "—"; _schedule.text = ""; _targetNotice.text = "明るい境界の領地へ遠征できる"; }
        _schedule.gameObject.SetActive(_schedule.text.Length > 0);
        for (int i = 0; i < _tokens.Length; i++)
        {
            bool selected = _draft.Contains(i); _tokens[i].gameObject.SetActive(selected);
            if (!selected) continue;
            int weapon = _draft.Weapon(i);
            _tokens[i].Refresh(_roster.Data.Soldiers[i].Name, _roster.Data.Soldiers[i].Icon, weapon < 0 ? "武器なし" : _roster.Data.Weapons[weapon].Name, _view.SelectedSoldier == i, weapon < 0);
        }
        for (int i = 0; i < _emptySlots.Length; i++) _emptySlots[i].SetActive(i < 26 - _draft.Members.Count);
        _view.PreviewEquipment(_draft);
        _view.PreviewWeapon(_equipmentSoldier, _view.SelectedWeapon);
        _equipment.GetComponentInChildren<TextMeshProUGUI>().text = _equipmentSoldier >= 0 ? "‹ 兵士に戻る" : "武器を変更";
        _equipment.interactable = _equipmentSoldier >= 0 || _roster.Soldiers[_view.SelectedSoldier].Expedition == null;
        if (_equipmentSoldier >= 0)
        {
            int owner = _roster.WeaponOwner(_view.SelectedWeapon);
            bool available = owner < 0 || _roster.Soldiers[owner].Expedition == null;
            _view.SetAction("この武器を装備", available, available ? "選択すると装備を変更する" : "帰還まで持ち出し中");
        }
        else
        {
            int soldier = _view.SelectedSoldier;
            bool selected = _draft.Contains(soldier), available = _state.CanSelect(soldier, _draft.Target, out string reason);
            _view.SetAction(selected ? "メンバーから外す" : "メンバーに加える", selected || available, available ? "出発までメンバーと武器を変更できる" : reason, chooseMember: true);
        }
        bool valid = _state.ValidateDeparture(_draft.Target, _draft.Members, _draft.Weapons(), out string notice);
        bool targetReady = _state.CanTarget(_draft.Target), membersReady = _draft.Members.Count > 0;
        _targetStep.color = targetReady ? CountryGovernmentTheme.Positive : CountryGovernmentTheme.Text;
        _memberStep.color = membersReady ? CountryGovernmentTheme.Positive : CountryGovernmentTheme.Text;
        bool missingWeapon = _draft.Members.Any(id => _roster.EquippedWeapon(id) < 0);
        bool canWarn = targetReady && membersReady && missingWeapon && _draft.Members.All(id => _state.CanSelect(id, _draft.Target, out _));
        _depart.interactable = valid || canWarn; _departLabel.color = valid || canWarn ? CountryGovernmentTheme.Ink : CountryGovernmentTheme.DisabledText;
        _departFrame.color = CountryGovernmentTheme.Border;

    }

    private TextMeshProUGUI HeaderLabel(Transform parent, string name, string text, float x, float width)
    {
        var label = Instantiate(_formationTarget, parent);
        label.name = name; label.text = text; label.fontSize = 32; label.margin = Vector4.zero;
        label.alignment = TextAlignmentOptions.MidlineLeft; label.raycastTarget = false;
        var rect = label.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, .5f);
        rect.anchoredPosition = new Vector2(x, 0); rect.sizeDelta = new Vector2(width, 96);
        return label;
    }

    private void AddStep(TextMeshProUGUI label, bool formation)
    {
        label.raycastTarget = true;
        var button = label.gameObject.AddComponent<Button>();
        button.targetGraphic = label; button.transition = Selectable.Transition.None;
        button.onClick.AddListener(() => ScrollToStep(formation));
    }

    private void ScrollToStep(bool formation)
    {
        Canvas.ForceUpdateCanvases(); _page.StopMovement();
        if (!formation) { _page.verticalNormalizedPosition = 1; return; }
        var target = (RectTransform)_page.content.Find("Formation");
        var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(_page.content, target);
        float extent = Mathf.Max(0, _page.content.rect.height - _page.viewport.rect.height);
        float offset = Mathf.Clamp(_page.content.rect.yMax - bounds.max.y - 160, 0, extent);
        _page.verticalNormalizedPosition = extent <= 0 ? 1 : 1 - offset / extent;
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
            if (unarmed.Length > 0) _confirmation.ShowDetails("武器が未設定", string.Join("、", unarmed) + "の武器を設定する");
            else Refresh();
            return;
        }
        int target = _draft.Target; int[] members = _draft.Members.ToArray(), weapons = _draft.Weapons();
        var trip = new CountryExpeditionTrip(target, _state.Today, _data.TravelDays(target), members, weapons);
        string body = CountryExpeditionRow.Details(_state, trip, _roster.Data, false) + "\n\n帰還までメンバーと装備は変更できない。";
        _confirmation.Show("遠征に出発", body, "出発する", () => {
            if (_state.TryDepart(target, members, weapons, out _))
            {
                Close();
                _departureNotice.text = $"{_data.Regions[target].Name}へ出発\n到着 {trip.Arrival} ／ 帰還 {trip.Return}";
                _departureNotice.transform.parent.gameObject.SetActive(true); _departureNotice.gameObject.SetActive(true); _noticeUntil = Time.unscaledTime + 5;
            }
            else Refresh();
        });
    }
    private void OpenLog()
    {
        _shell.interactable = _shell.blocksRaycasts = false; _logWindow.SetActive(true);
        _logEmpty.gameObject.SetActive(_state.Active.Count == 0);
        foreach (var row in _logRows) { row.gameObject.SetActive(false); Destroy(row.gameObject); } _logRows.Clear();
        foreach (var trip in _state.Active)
        {
            var row = Instantiate(_rowPrefab, _logContent); _logRows.Add(row);
            row.Bind(_state, trip, false, () => _logDetails.ShowDetails("遠征の状況", CountryExpeditionRow.Details(_state, trip, _roster.Data, false)));
        }
        _logClose.Select();
    }
    private void CloseLog()
    {
        _logDetails.Close(); _logWindow.SetActive(false); _shell.interactable = _shell.blocksRaycasts = true;
    }
    private void Back() { if (_equipmentSoldier >= 0) ChooseEquipment(); else Close(); }
    public void Close()
    {
        _equipmentPopup?.Close();
        if (_page != null) _page.GetComponent<CountryExpeditionPageLayout>().enabled = true;
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
        if (_departureNotice.transform.parent.gameObject.activeSelf && Time.unscaledTime >= _noticeUntil) _departureNotice.transform.parent.gameObject.SetActive(false);
    }
    private void OnDisable() => Close();
    private void OnDestroy()
    {
        _open?.onClick.RemoveListener(Open); _escape?.Dispose();
        if (_state != null) _state.Changed -= StateChanged;
    }
}
