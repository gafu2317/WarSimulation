using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public sealed class CountrySettingsPanel : MonoBehaviour
{
    [SerializeField] private GameObject _window;
    [SerializeField] private TMP_InputField[] _fields;
    [SerializeField] private UnityEngine.UI.Graphic[] _frames;
    [SerializeField] private TextMeshProUGUI _error, _applyLabel;
    [SerializeField] private UnityEngine.UI.Graphic _applyFrame;
    [SerializeField] private UnityEngine.UI.Button _open, _apply, _cancelButton, _close, _dismiss;
    [SerializeField] private UnityEngine.UI.Button _combat;
    [SerializeField] private CountryGovernmentPanel _government;
    [SerializeField] private CountryPersonnelPanel _personnel;
    [SerializeField] private CountryExpeditionPanel _expeditions;
    [SerializeField] private CountryDefensePanel _defense;
    [SerializeField] private UnityEngine.UI.Toggle _raids;
    [SerializeField] private TextMeshProUGUI _raidsLabel;
    private UnityEngine.UI.Button _monthEnd;
    private PlayDataManager _resources;
    private DateManager _date;
    private InputAction _escape;
    private GameObject _returnFocus;
    private bool _cameraInput, _comparisonInput;
    public bool IsOpen => _window.activeSelf;

    private void Awake()
    {
        _open.onClick.AddListener(Open);
        _apply.gameObject.SetActive(false);
        _combat.onClick.AddListener(PlaytestSceneNavigation.OpenCombat);
        _monthEnd = Instantiate(_combat, _combat.transform.parent);
        _monthEnd.name = "MonthEnd";
        _monthEnd.onClick = new UnityEngine.UI.Button.ButtonClickedEvent();
        _monthEnd.onClick.AddListener(AdvanceMonth);
        _monthEnd.GetComponentInChildren<TextMeshProUGUI>().text = "月末に飛ばす";
        PlaceFooter(_cancelButton, 0, 264);
        _cancelButton.GetComponentInChildren<TextMeshProUGUI>().text = "閉じる";
        PlaceFooter(_combat, 288, 320);
        PlaceFooter(_monthEnd, 632, 320);
        _raids.onValueChanged.AddListener(value => { RefreshRaids(); if (_resources != null) _resources.SetRaidsEnabled(value); });
        foreach (var button in new[] { _cancelButton, _close, _dismiss }) button.onClick.AddListener(Close);
        for (int i = 0; i < _fields.Length; i++)
        {
            int index = i; var field = _fields[i];
            field.onValueChanged.AddListener(_ => { UpdateResource(index); Validate(); });
            field.onSelect.AddListener(_ => Validate());
            field.onDeselect.AddListener(_ => { RestoreInvalid(index); Validate(); });
        }
        _escape = new InputAction("Close settings", InputActionType.Button, "<Keyboard>/escape");
        _escape.performed += _ => Close();
    }

    private void Start()
    {
        _resources = PlayDataManager.Instance; _date = DateManager.Instance;
        if (_resources != null && _date != null) return;
        Debug.LogError("CountrySettingsPanel requires PlayDataManager and DateManager.", this);
        enabled = false;
    }

    public void Open()
    {
        if (!isActiveAndEnabled || _resources == null || IsOpen) return;
        _expeditions?.Close();
        _defense?.Close();
        _personnel.Close(); _government.Close(); _government.FacilityPopup?.Close(); _date.Pause();
        _returnFocus = EventSystem.current?.currentSelectedGameObject;
        _cameraInput = _government.CameraControls != null && _government.CameraControls.InputEnabled;
        _comparisonInput = _government.Comparison != null && _government.Comparison.InputEnabled;
        if (_government.CameraControls != null) _government.CameraControls.InputEnabled = false;
        if (_government.Comparison != null) _government.Comparison.InputEnabled = false;
        int[] values = { _resources.Population, _resources.Treasury, _resources.Wood, _resources.Ore };
        for (int i = 0; i < _fields.Length; i++) _fields[i].SetTextWithoutNotify(values[i].ToString(CultureInfo.InvariantCulture));
        _raids.SetIsOnWithoutNotify(_resources.RaidsEnabled);
        RefreshRaids();
        _window.SetActive(true);
        var canvas = _window.GetComponent<Canvas>(); canvas.overrideSorting = true; canvas.sortingOrder = 60;
        _escape.Enable(); Validate(); _cancelButton.Select();
    }

    private static void PlaceFooter(UnityEngine.UI.Button button, float left, float width)
    {
        var rect = (RectTransform)button.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(left, 0); rect.sizeDelta = new Vector2(width, 112);
        var label = button.GetComponentInChildren<TextMeshProUGUI>();
        label.fontSize = 26;
    }

    private void AdvanceMonth()
    {
        Close();
        _date.AdvanceToMonthEnd();
    }

    private bool Validate()
    {
        bool valid = true;
        for (int i = 0; i < _fields.Length; i++)
        {
            bool accepted = int.TryParse(_fields[i].text, NumberStyles.None, CultureInfo.InvariantCulture, out _);
            _frames[i].color = !accepted ? CountryGovernmentTheme.Error : _fields[i].isFocused ? CountryGovernmentTheme.Text : CountryGovernmentTheme.Border;
            valid &= accepted;
        }
        _error.gameObject.SetActive(!valid);
        return valid;
    }

    private void RefreshRaids()
    {
        _raidsLabel.text = _raids.isOn ? "ON" : "OFF";
        _raidsLabel.color = CountryGovernmentTheme.Text;
        _raids.targetGraphic.color = _raids.isOn ? CountryGovernmentTheme.PositiveFace : CountryGovernmentTheme.Recess;
    }

    private void UpdateResource(int index)
    {
        if (_resources == null || !int.TryParse(_fields[index].text, NumberStyles.None, CultureInfo.InvariantCulture, out int value)) return;
        int[] values = { _resources.Population, _resources.Treasury, _resources.Wood, _resources.Ore };
        values[index] = value;
        _resources.SetResources(values[0], values[1], values[2], values[3]);
    }

    private void RestoreInvalid(int index)
    {
        if (_resources == null || int.TryParse(_fields[index].text, NumberStyles.None, CultureInfo.InvariantCulture, out _)) return;
        int[] values = { _resources.Population, _resources.Treasury, _resources.Wood, _resources.Ore };
        _fields[index].SetTextWithoutNotify(values[index].ToString(CultureInfo.InvariantCulture));
    }

    public void Close()
    {
        if (!IsOpen) return;
        _window.SetActive(false); _escape?.Disable();
        if (_government.CameraControls != null) _government.CameraControls.InputEnabled = _cameraInput;
        if (_government.Comparison != null) _government.Comparison.InputEnabled = _comparisonInput;
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(_returnFocus != null && _returnFocus.activeInHierarchy ? _returnFocus : null);
    }

    private void OnDisable() => Close();
    private void OnDestroy()
    {
        _open?.onClick.RemoveListener(Open);
        _combat?.onClick.RemoveListener(PlaytestSceneNavigation.OpenCombat);
        _escape?.Dispose();
    }
}
