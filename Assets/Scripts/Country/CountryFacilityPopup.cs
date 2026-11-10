using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public sealed class CountryFacilityPopup : MonoBehaviour
{
    [SerializeField] private GameObject _modal;
    [SerializeField] private TextMeshProUGUI _title;
    [SerializeField] private TextMeshProUGUI _category;
    [SerializeField] private TextMeshProUGUI _description;
    [SerializeField] private UnityEngine.UI.Image _accent;
    [SerializeField] private UnityEngine.UI.Button _closeButton;
    [SerializeField] private CountryFacilitySymbol _symbol;
    private CountryCameraController _cameraControls;
    private CountryComparisonView _comparison;
    private CountryFacilityTarget _target;
    private GameObject _returnFocus;
    private Canvas _modalCanvas;
    private InputAction _cancel;
    private bool _cameraInput, _comparisonInput;
    public bool IsOpen => _modal != null && _modal.activeSelf;
    public CountryFacilityTarget Target => _target;

    private void Awake()
    {
        _modalCanvas = _modal.GetComponent<Canvas>();
        _cancel = new InputAction("Close facility", InputActionType.Button, "<Keyboard>/escape");
        _cancel.performed += _ => Close();
    }

    public void BindControls(CountryCameraController cameraControls, CountryComparisonView comparison)
    {
        _cameraControls = cameraControls;
        _comparison = comparison;
    }

    public void Open(CountryFacilityTarget target)
    {
        if (target == null || target.building == null || !target.building.gameObject.activeInHierarchy) return;
        if (!IsOpen)
        {
            _returnFocus = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            _cameraInput = _cameraControls != null && _cameraControls.InputEnabled;
            _comparisonInput = _comparison != null && _comparison.InputEnabled;
        }
        _target = target;
        _title.text = target.name;
        _symbol.SetFacility(target.name);
        _category.text = target.category;
        _description.text = target.description;
        _accent.color = target.accent;
        if (_cameraControls != null) _cameraControls.InputEnabled = false;
        if (_comparison != null) _comparison.InputEnabled = false;
        _modal.SetActive(true);
        _modalCanvas.overrideSorting = true;
        _modalCanvas.sortingOrder = 30;
        _cancel.Enable();
        _closeButton.Select();
    }

    public void Close()
    {
        if (!IsOpen) return;
        _modal.SetActive(false);
        _cancel?.Disable();
        _target = null;
        if (_cameraControls != null) _cameraControls.InputEnabled = _cameraInput;
        if (_comparison != null) _comparison.InputEnabled = _comparisonInput;
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(_returnFocus != null && _returnFocus.activeInHierarchy ? _returnFocus : null);
    }

    private void Update()
    {
        if (IsOpen && (_target == null || _target.building == null || !_target.building.gameObject.activeInHierarchy)) Close();
    }

    private void OnDisable() => Close();
    private void OnDestroy() => _cancel?.Dispose();
}
