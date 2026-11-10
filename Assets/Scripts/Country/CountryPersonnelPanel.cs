using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public sealed class CountryPersonnelPanel : MonoBehaviour
{
    [SerializeField] private GameObject _window;
    [SerializeField] private CountryGovernmentPanel _government;
    [SerializeField] private CountryExpeditionPanel _expeditions;
    [SerializeField] private CountryDefensePanel _defense;
    [SerializeField] private UnityEngine.UI.Button _defenseTab;
    [SerializeField] private CountryRosterView _view;
    [SerializeField] private CountryRosterConfirm _confirmation;
    [SerializeField] private CanvasGroup _shell;
    [SerializeField] private RectTransform _headerBackground;
    [SerializeField] private UnityEngine.UI.Button _soldiersTab, _weaponsTab, _back;
    [SerializeField] private TextMeshProUGUI _context, _backLabel, _dataState;
    private CountryRoster _roster;
    private DateManager _date;
    private InputAction _cancel;
    private GameObject _returnFocus;
    private bool _weapons, _cameraInput, _comparisonInput;
    private int _equipSoldier = -1, _equipWeapon = -1;
    private CountryEquipmentPopup _equipmentPopup;
    public bool IsOpen => _window.activeSelf;
    private void Awake()
    {
        _soldiersTab.onClick.AddListener(() => Switch(false)); _weaponsTab.onClick.AddListener(() => Switch(true));
        _back.onClick.AddListener(Back);
        _defenseTab.onClick.AddListener(() => _defense.Open());
        _view.SelectionChanged += RefreshAction; _view.ActionRequested += Action;
        _cancel = new InputAction("Close soldiers", InputActionType.Button, "<Keyboard>/escape");
        _cancel.performed += _ => { if (_confirmation.IsOpen) _confirmation.Close(); else Back(); };
    }
    private void Start()
    {
        _date = DateManager.Instance;
        if (_date == null || PlayDataManager.Instance == null) { enabled = false; return; }
        _roster = PlayDataManager.Instance.GetOrCreateRoster(_view.Data, CountryDate.From(_date));
        _view.Initialize(_roster);
        _view.PlaceMemberControls();
        _view.CreatePersonnelEquipmentAction();
        CountryGovernmentPanel.StylePageButton(_back, _view.RoundedPanel, CountryGovernmentTheme.Face);
        _equipmentPopup = new CountryEquipmentPopup(_view, _back, _shell, _window.transform, CloseEquipment);
        _dataState.gameObject.SetActive(false);
    }
    public void OpenSoldiers()
    {
        if (_roster == null) return;
        if (!IsOpen)
        {
            _defense?.Close();
            _expeditions?.Close();
            _government.Close(); _government.FacilityPopup?.Close(); _date.Pause();
            _returnFocus = EventSystem.current?.currentSelectedGameObject;
            _cameraInput = _government.CameraControls != null && _government.CameraControls.InputEnabled;
            _comparisonInput = _government.Comparison != null && _government.Comparison.InputEnabled;
            if (_government.CameraControls != null) _government.CameraControls.InputEnabled = false;
            if (_government.Comparison != null) _government.Comparison.InputEnabled = false;
            _window.SetActive(true); _cancel.Enable();
            var canvas = _window.GetComponent<Canvas>(); canvas.overrideSorting = true; canvas.sortingOrder = 50;
        }
        Switch(false); _weaponsTab.Select();
    }
    private void Switch(bool weapons)
    {
        _confirmation.Close(); _equipSoldier = _equipWeapon = -1; _weapons = weapons;
        Show();
    }
    private void Show()
    {
        if (_weapons) _view.ShowWeapons(); else _view.ShowSoldiers();
        bool choosingEquipment = _equipSoldier >= 0 || _equipWeapon >= 0;
        _soldiersTab.gameObject.SetActive(_weapons && !choosingEquipment);
        _weaponsTab.gameObject.SetActive(!_weapons && !choosingEquipment);
        _soldiersTab.GetComponentInChildren<TextMeshProUGUI>(true).text = "‹ 兵士一覧へ";
        _weaponsTab.GetComponentInChildren<TextMeshProUGUI>(true).text = "武器一覧へ ›";
        _view.ShowPersonnelEquipmentAction(!_weapons && !choosingEquipment);
        RefreshAction();
    }
    private void RefreshAction()
    {
        if (_roster == null) return;
        _context.text = _equipSoldier >= 0 ? $"{_view.Data.Soldiers[_equipSoldier].Name}の装備を選ぶ"
            : _equipWeapon >= 0 ? $"{_view.Data.Weapons[_equipWeapon].Name}の装備先を選ぶ" : _weapons ? "武器一覧" : "兵士一覧";
        if (_weapons) _context.text += $"　合計 {_view.Data.Weapons.Length}点";
        _backLabel.text = "‹ 戻る";
        int weapon = _weapons ? _view.SelectedWeapon : _equipWeapon;
        int soldier = _weapons ? _equipSoldier : _view.SelectedSoldier;
        _view.PreviewWeapon(_equipSoldier >= 0 || _equipWeapon >= 0 ? soldier : -1, weapon);
        if (_equipSoldier >= 0 || _equipWeapon >= 0)
        {
            int owner = _roster.WeaponOwner(weapon);
            string hint = owner >= 0 && owner != soldier ? $"{_view.Data.Soldiers[owner].Name}の装備から外して付け替える" : "装備補正と使用可能なスキルが切り替わる";
            bool away = _roster.Soldiers[soldier].Expedition != null || (owner >= 0 && _roster.Soldiers[owner].Expedition != null);
            _view.SetAction(away ? "遠征中・変更不可" : owner == soldier ? "装備中" : "この武器を装備", !away && owner != soldier, away ? "帰還まで装備を変更できない" : hint);
        }
        else
        {
            int owner = _weapons ? _roster.WeaponOwner(_view.SelectedWeapon) : _view.SelectedSoldier;
            bool away = owner >= 0 && _roster.Soldiers[owner].Expedition != null;
            _view.SetAction(away ? "遠征中・変更不可" : _weapons ? "装備先を選ぶ" : "装備を変更", !away, away ? "帰還まで装備を変更できない" : "", chooseMember: _weapons);
        }
    }
    private void Action()
    {
        if (_equipSoldier >= 0 || _equipWeapon >= 0)
        {
            int soldier = _weapons ? _equipSoldier : _view.SelectedSoldier;
            int weapon = _weapons ? _view.SelectedWeapon : _equipWeapon;
            int owner = _roster.WeaponOwner(weapon), old = _roster.EquippedWeapon(soldier);
            string body = $"{_view.Data.Soldiers[soldier].Name}\n{(old < 0 ? "未装備" : _view.Data.Weapons[old].Name)} → {_view.Data.Weapons[weapon].Name}";
            body += "\n\n" + _view.EquipmentComparison(soldier, weapon);
            if (owner >= 0 && owner != soldier) body += $"\n\n{_view.Data.Soldiers[owner].Name}はこの武器の装備が外れる。";
            _confirmation.Show("装備の変更", body, "付け替える", () => {
                _roster.TryEquip(soldier, weapon);
                CloseEquipment();
            });
            return;
        }
        if (_weapons) { _equipWeapon = _view.SelectedWeapon; _weapons = false; }
        else { _equipSoldier = _view.SelectedSoldier; _weapons = true; }
        _equipmentPopup.Open();
        Show();
    }
    private void CloseEquipment()
    {
        if (_equipSoldier < 0 && _equipWeapon < 0) return;
        bool fromSoldier = _equipSoldier >= 0;
        _confirmation.Close();
        _equipmentPopup.Close();
        _equipSoldier = _equipWeapon = -1;
        _weapons = !fromSoldier;
        Show();
    }
    private void Back()
    {
        if (_equipSoldier >= 0 || _equipWeapon >= 0) { CloseEquipment(); return; }
        if (_weapons) { Switch(false); return; }
        Close();
    }
    public void Close()
    {
        if (!IsOpen) return;
        _confirmation.Close(); _equipmentPopup?.Close();
        _equipSoldier = _equipWeapon = -1;
        _window.SetActive(false); _cancel?.Disable();
        if (_government.CameraControls != null) _government.CameraControls.InputEnabled = _cameraInput;
        if (_government.Comparison != null) _government.Comparison.InputEnabled = _comparisonInput;
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(_returnFocus != null && _returnFocus.activeInHierarchy ? _returnFocus : null);
    }
    private void LateUpdate()
    {
        if (!IsOpen) return;
        var shell = (RectTransform)_shell.transform;
        _headerBackground.anchorMin = new Vector2(0, shell.anchorMax.y);
        _headerBackground.anchorMax = new Vector2(1, shell.anchorMax.y);
        _headerBackground.anchoredPosition = new Vector2(0, shell.offsetMax.y);
    }
    private void OnDisable() => Close();
    private void OnDestroy() => _cancel?.Dispose();
}
