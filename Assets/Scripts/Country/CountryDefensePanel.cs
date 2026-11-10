using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public sealed class CountryDefensePanel : MonoBehaviour
{
    [SerializeField] private CountryWorldMapData _data;
    [SerializeField] private CountryGovernmentPanel _government;
    [SerializeField] private CountryPersonnelPanel _personnel;
    [SerializeField] private CountryExpeditionPanel _expeditions;
    [SerializeField] private CountrySettingsPanel _settings;
    [SerializeField] private GameObject _window, _formation, _alert;
    [SerializeField] private CanvasGroup _shell;
    [SerializeField] private CountryRosterView _view;
    [SerializeField] private RectTransform _members, _raidMembers;
    [SerializeField] private CountryRosterRow _tokenPrefab;
    [SerializeField] private GameObject _emptySlotPrefab;
    [SerializeField] private TextMeshProUGUI _context, _count, _summary, _saveLabel, _raidTitle, _raidBody, _raidSummary;
    [SerializeField] private Button _back, _save, _equipment, _edit, _fight, _personnelBack;
    [SerializeField] private Graphic _saveFrame;
    [SerializeField] private CountryRosterConfirm _confirmation;
    private CountryRosterRow[] _tokens, _raidTokens;
    private GameObject _raidEmpty;
    [SerializeField] private GameObject _raidEmptyMessage;
    private GameObject[] _emptySlots;
    private CountryRoster _roster;
    private CountryDefenseState _state;
    private DateManager _date;
    private CountryExpeditionDraft _draft;
    private InputAction _escape;
    private GameObject _returnFocus;
    private bool _cameraInput, _comparisonInput;
    private int _equipmentSoldier = -1;
    private CountryEquipmentPopup _equipmentPopup;
    public bool IsOpen => _window.activeSelf;

    private void Awake()
    {
        _back.onClick.AddListener(Back); _save.onClick.AddListener(Save);
        _equipment.onClick.AddListener(ChooseEquipment); _edit.onClick.AddListener(EditRaid);
        _fight.onClick.AddListener(Fight);
        _personnelBack.onClick.AddListener(() => { Close(); _personnel.OpenSoldiers(); });
        _view.SelectionChanged += Refresh; _view.ActionRequested += Choose;
        _escape = new InputAction("Close defense", InputActionType.Button, "<Keyboard>/escape");
        _escape.performed += _ => { if (_confirmation.IsOpen) _confirmation.Close(); else Back(); };
    }

    private void Start()
    {
        _date = DateManager.Instance;
        if (_date == null || PlayDataManager.Instance == null) { enabled = false; return; }
        _state = PlayDataManager.Instance.GetOrCreateDefense(_data, _view.Data, CountryDate.From(_date));
        _roster = PlayDataManager.Instance.Roster;
        _view.SetMemberSelection(id => _draft != null && _draft.Contains(id), SelectionReason, "防衛メンバー");
        _view.Initialize(_roster);
        _view.PlaceMemberControls(_equipment);
        _equipmentPopup = new CountryEquipmentPopup(_view, _equipment, _shell, _window.transform, ChooseEquipment);
        _tokens = new CountryRosterRow[_roster.Soldiers.Count];
        _emptySlots = new GameObject[_tokens.Length];
        for (int i = 0; i < _tokens.Length; i++)
        {
            int id = i;
            _tokens[i] = Instantiate(_tokenPrefab, _members); _tokens[i].name = $"Defender{i + 1:00}";
            _tokens[i].SetCaptionVisible(true); _tokens[i].Bind(() => _view.SelectSoldier(id));
            _tokens[i].gameObject.SetActive(false);
        }
        for (int i = 0; i < _emptySlots.Length; i++) _emptySlots[i] = Instantiate(_emptySlotPrefab, _members);
        _view.EnableMemberDrag((RectTransform)_members.parent);
        for (int i = 0; i < _tokens.Length; i++)
        {
            int id = i;
            _tokens[i].EnableDrag(_view.ListViewport, () => { if (_draft != null && _draft.Contains(id) && _equipmentSoldier < 0) { _view.SelectSoldier(id); Choose(); } });
        }
        _raidTokens = new CountryRosterRow[_tokens.Length];
        for (int i = 0; i < _raidTokens.Length; i++)
        {
            var token = Instantiate(_tokenPrefab, _raidMembers);
            token.SetCaptionVisible(true); token.GetComponent<Button>().enabled = false;
            foreach (var graphic in token.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
            token.gameObject.SetActive(false); _raidTokens[i] = token;
        }
        _raidEmpty = Instantiate(_emptySlotPrefab, _raidMembers);
        _state.RaidStarted += OpenRaid;
        if (_state.Pending != null) OpenRaid();
    }

    public void Open() => Show(true);
    private void OpenRaid() => Show(false);
    private void Show(bool formation)
    {
        if (_state == null || !isActiveAndEnabled) return;
        if (!IsOpen)
        {
            _expeditions.Close(); _settings.Close(); _personnel.Close(); _government.Close(); _government.FacilityPopup?.Close();
            _date.Pause();
            _returnFocus = EventSystem.current?.currentSelectedGameObject;
            _cameraInput = _government.CameraControls != null && _government.CameraControls.InputEnabled;
            _comparisonInput = _government.Comparison != null && _government.Comparison.InputEnabled;
            if (_government.CameraControls != null) _government.CameraControls.InputEnabled = false;
            if (_government.Comparison != null) _government.Comparison.InputEnabled = false;
            _draft = new CountryExpeditionDraft(_roster); _draft.Load(_state.Members, _state.Weapons);
            _window.SetActive(true); _escape.Enable();
            var canvas = _window.GetComponent<Canvas>(); canvas.overrideSorting = true; canvas.sortingOrder = 70;
        }
        _equipmentSoldier = -1; _formation.SetActive(formation); _alert.SetActive(!formation);
        _view.ShowSoldiers(); Refresh();
        if (formation) _back.Select(); else _edit.Select();
    }

    private void EditRaid() => Show(true);
    private void ChooseEquipment()
    {
        if (_equipmentSoldier >= 0)
        {
            _view.SelectSoldier(_equipmentSoldier); _equipmentSoldier = -1; _equipmentPopup.Close(); _view.ShowSoldiers(); Refresh(); return;
        }
        if (_roster.Soldiers[_view.SelectedSoldier].Expedition != null) return;
        _equipmentSoldier = _view.SelectedSoldier; _equipmentPopup.Open(); _view.ShowWeapons(); Refresh();
    }

    private void Choose()
    {
        if (_equipmentSoldier >= 0)
        {
            int weapon = _view.SelectedWeapon, owner = _roster.WeaponOwner(weapon);
            if (owner >= 0 && _roster.Soldiers[owner].Expedition != null) return;
            if (_roster.TryEquip(_equipmentSoldier, weapon)) ChooseEquipment(); else Refresh();
        }
        else
        {
            int soldier = _view.SelectedSoldier;
            if (!_draft.Contains(soldier) && !_roster.CanDeploy(soldier)) return;
            _draft.Toggle(soldier);
        }
        if (_state.Pending != null) _state.TryRegister(_draft.Members, _draft.Weapons(), out _);
        Refresh();
    }

    private string SelectionReason(int soldier)
    {
        if (_roster.CanDeploy(soldier)) return "";
        var state = _roster.Soldiers[soldier];
        if (state.CP == 0) return "復活待ち";
        if (state.Expedition != null) return "遠征中";
        return "訓練中";
    }

    private void Refresh()
    {
        if (!IsOpen || _draft == null) return;
        bool raid = _state.Pending != null;
        _context.text = _equipmentSoldier >= 0 ? $"{_roster.Data.Soldiers[_equipmentSoldier].Name}の武器を選ぶ"
            : raid ? $"{_data.Regions[_state.Pending.Region].Name}の防衛編成" : "防衛メンバーの登録";
        _count.text = $"防衛メンバー　{_draft.Members.Count} / 26人";
        _personnelBack.gameObject.SetActive(!raid);
        _back.GetComponentInChildren<TextMeshProUGUI>().text = raid ? "‹ 襲撃へ戻る" : "‹ 国へ戻る";
        _saveLabel.text = raid ? "この編成で防衛" : "登録する";
        for (int i = 0; i < _tokens.Length; i++)
        {
            bool selected = _draft.Contains(i); _tokens[i].gameObject.SetActive(selected);
            if (!selected) continue;
            int weapon = _draft.Weapon(i);
            _tokens[i].Refresh(_roster.Data.Soldiers[i].Name, _roster.Data.Soldiers[i].Icon,
                weapon < 0 ? "武器を選ぶ" : _roster.Data.Weapons[weapon].Name, _view.SelectedSoldier == i, !_roster.CanDeploy(i) || weapon < 0);
        }
        for (int i = 0; i < _emptySlots.Length; i++) _emptySlots[i].SetActive(i < _tokens.Length - _draft.Members.Count);
        _view.PreviewEquipment(_draft, "防衛メンバー");
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
            int soldier = _view.SelectedSoldier; bool selected = _draft.Contains(soldier), available = _roster.CanDeploy(soldier);
            _view.SetAction(selected ? "メンバーから外す" : "防衛メンバーに加える", selected || available,
                available ? "兵士と武器を組み合わせて登録する" : "遠征中・訓練中・復活待ちは参加できない", chooseMember: true);
        }
        bool valid = _state.Validate(_draft.Members, _draft.Weapons(), out string reason);
        bool canFight = valid && _draft.Members.Count > 0;
        _save.interactable = raid ? canFight : valid;
        _saveFrame.color = CountryGovernmentTheme.Border;
        _saveLabel.color = _save.interactable ? CountryGovernmentTheme.Ink : CountryGovernmentTheme.DisabledText;
        _summary.text = !valid ? reason : raid ? "変更は自動登録" : "登録した編成を次の防衛戦で呼び出す";
        _fight.interactable = canFight;
        if (!raid) return;
        _raidTitle.text = $"{_data.Regions[_state.Pending.Region].Name}に襲撃";
        _raidBody.text = $"{_state.Pending.Date}　領内に敵部隊が侵入\n防衛メンバーを確認し、迎撃せよ";
        _raidSummary.text = valid ? "" : reason;
        for (int i = 0; i < _raidTokens.Length; i++)
        {
            bool present = i < _draft.Members.Count; var token = _raidTokens[i];
            token.gameObject.SetActive(present);
            if (!present) continue;
            var soldier = _roster.Data.Soldiers[_draft.Members[i]];
            token.Refresh(soldier.Name, soldier.Icon, soldier.Name, false);
            token.transform.Find("State").GetComponent<TextMeshProUGUI>().color = new Color32(52, 38, 34, 255);
        }
        _raidEmpty.SetActive(_draft.Members.Count == 0);
        _raidEmptyMessage.SetActive(_draft.Members.Count == 0);
    }

    private void Save()
    {
        if (_state.Pending != null) { Fight(); return; }
        if (_state.TryRegister(_draft.Members, _draft.Weapons(), out _)) Close(); else Refresh();
    }

    private void Fight()
    {
        if (_state.Pending == null || !_state.Validate(_draft.Members, _draft.Weapons(), out _) || _draft.Members.Count == 0) { Refresh(); return; }
        string members = string.Join("\n", _draft.Members.Select(id =>
            $"{_roster.Data.Soldiers[id].Name}　{_roster.Data.Weapons[_draft.Weapon(id)].Name}"));
        string body = members + "\n\nこの編成を次回の防衛メンバーにも登録する。";
        _confirmation.Show("防衛に出撃", body, "防衛する", () => {
            string region = _data.Regions[_state.Pending.Region].Name;
            if (!_state.TryDefend(_draft.Members, _draft.Weapons(), out _)) { Refresh(); return; }
            _confirmation.Show("防衛成功", $"{region}への襲撃を退けた\n\n{members}", "国へ戻る", Close);
        });
    }

    private void Back()
    {
        if (_equipmentSoldier >= 0) { ChooseEquipment(); return; }
        if (_state.Pending != null) { Show(false); return; }
        Close();
    }

    public void Close()
    {
        _equipmentPopup?.Close();
        if (!IsOpen) return;
        _confirmation.Close(); _window.SetActive(false); _escape?.Disable(); _view.PreviewEquipment(null); _draft = null;
        if (_government.CameraControls != null) _government.CameraControls.InputEnabled = _cameraInput;
        if (_government.Comparison != null) _government.Comparison.InputEnabled = _comparisonInput;
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(_returnFocus != null && _returnFocus.activeInHierarchy ? _returnFocus : null);
    }

    private void OnEnable() { if (_state?.Pending != null) OpenRaid(); }
    private void OnDisable() => Close();
    private void OnDestroy() { _escape?.Dispose(); if (_state != null) _state.RaidStarted -= OpenRaid; }
}
