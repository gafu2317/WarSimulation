using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;

[DisallowMultipleComponent, RequireComponent(typeof(UnityEngine.UI.Button))]
public sealed class CountryButtonFeedback : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler, ISubmitHandler
{
    [SerializeField] private RectTransform _face;
    [SerializeField] private RectTransform[] _contents;
    [SerializeField] private GameObject _shadow;
    [SerializeField] private UnityEngine.UI.Graphic _focus;
    [SerializeField] private float _pressDepth = 3, _hoverScale = .015f, _hoverTilt;
    private UnityEngine.UI.Button _button;
    private Vector2 _facePosition;
    private Vector3 _faceScale;
    private Vector2[] _positions;
    private Vector3[] _scales, _rotations;
    private Color _focusColor;
    private UnityEngine.UI.Graphic[] _graphics;
    private Color[] _contentColors;
    private float _pressAmount, _hoverAmount;
    private bool _ready, _interactable;
    private bool _pressed, _hovered, _focused, _dimmed;

    private void Awake()
    {
        _button = GetComponent<UnityEngine.UI.Button>();
        var colors = _button.colors; colors.disabledColor = CountryGovernmentTheme.DisabledFace; _button.colors = colors;
        _positions = new Vector2[_contents.Length];
        _scales = new Vector3[_contents.Length]; _rotations = new Vector3[_contents.Length];
        var graphics = new List<UnityEngine.UI.Graphic>();
        foreach (var content in _contents)
            foreach (var graphic in content.GetComponentsInChildren<UnityEngine.UI.Graphic>(true))
                if (!graphics.Contains(graphic)) graphics.Add(graphic);
        _graphics = graphics.ToArray(); _contentColors = new Color[_graphics.Length];
    }

    private void OnEnable()
    {
        _facePosition = _face.anchoredPosition; _faceScale = _face.localScale;
        for (int i = 0; i < _contents.Length; i++)
        {
            _positions[i] = _contents[i].anchoredPosition;
            _scales[i] = _contents[i].localScale; _rotations[i] = _contents[i].localEulerAngles;
        }
        if (_focus != null) _focusColor = _focus.color;
        for (int i = 0; i < _graphics.Length; i++) _contentColors[i] = _graphics[i].color;
        _interactable = _button.IsInteractable(); _ready = true;
        UpdateDisabledAppearance();
    }

    public void RefreshContentPositions()
    {
        if(!_ready)return;
        for(int i=0;i<_contents.Length;i++)_positions[i]=_contents[i].anchoredPosition;
    }

    private void LateUpdate()
    {
        UpdateDisabledAppearance();
        bool interactable = _button.IsInteractable();
        if (!interactable) _pressed = false;
        float press = Mathf.MoveTowards(_pressAmount, _pressed ? 1 : 0, Time.unscaledDeltaTime / (_pressed ? .06f : .14f));
        float hover = Mathf.MoveTowards(_hoverAmount, interactable && (_hovered || _focused) ? 1 : 0, Time.unscaledDeltaTime / .12f);
        if (press == _pressAmount && hover == _hoverAmount && interactable == _interactable) return;
        _pressAmount = press; _hoverAmount = hover; _interactable = interactable;
        float eased = Mathf.SmoothStep(0, 1, press), raised = Mathf.SmoothStep(0, 1, hover);
        _face.anchoredPosition = _facePosition + Vector2.down * (_pressDepth * eased);
        _face.localScale = _faceScale * (1 - .025f * eased);
        for (int i = 0; i < _contents.Length; i++)
        {
            _contents[i].anchoredPosition = _positions[i] + Vector2.down * (_pressDepth * .5f * eased);
            _contents[i].localScale = _scales[i] * (1 + _hoverScale * raised - .025f * eased);
            if (_contents[i].name == "Icon") _contents[i].localEulerAngles = _rotations[i] + Vector3.forward * (_hoverTilt * raised);
        }
        if (_shadow != null) _shadow.SetActive(eased < .95f);
        if (_focus != null)
        {
            _focus.enabled = hover > 0 && interactable;
            _focus.color = new Color(_focusColor.r, _focusColor.g, _focusColor.b, _focusColor.a * raised);
        }
    }

    public void OnPointerDown(PointerEventData data)
    {
        if (data.button == PointerEventData.InputButton.Left) Press();
    }
    public void OnPointerUp(PointerEventData data) => Release();
    public void OnPointerEnter(PointerEventData data) { _hovered = true; RefreshFocus(); }
    public void OnPointerExit(PointerEventData data) { _hovered = false; Release(); RefreshFocus(); }
    public void OnSelect(BaseEventData data) { _focused = true; RefreshFocus(); }
    public void OnDeselect(BaseEventData data) { _focused = false; RefreshFocus(); }
    public void OnSubmit(BaseEventData data)
    {
        if (!isActiveAndEnabled || !_button.IsInteractable()) return;
        Press();
        StartCoroutine(ReleaseAfterSubmit());
    }
    private IEnumerator ReleaseAfterSubmit() { yield return new WaitForSecondsRealtime(.12f); Release(); }

    private void Press()
    {
        if (_pressed || !_button.IsInteractable() || !isActiveAndEnabled) return;
        _pressed = true;
    }
    private void Release()
    {
        _pressed = false;
    }
    private void RefreshFocus()
    {
        if (!_button.IsInteractable()) _pressed = false;
    }
    private void UpdateDisabledAppearance()
    {
        if (!_button.IsInteractable())
        {
            for (int i = 0; i < _graphics.Length; i++)
                _graphics[i].color = _graphics[i] is TMPro.TMP_Text
                    ? CountryGovernmentTheme.DisabledText : Color.Lerp(_contentColors[i], CountryGovernmentTheme.DisabledText, .35f);
            _dimmed = true;
        }
        else
        {
            for (int i = 0; i < _graphics.Length; i++)
                if (_dimmed) _graphics[i].color = _contentColors[i];
                else _contentColors[i] = _graphics[i].color;
            _dimmed = false;
        }
    }
    private void OnDisable()
    {
        StopAllCoroutines(); Release(); _hovered = _focused = false;
        if (!_ready) return;
        _face.anchoredPosition = _facePosition; _face.localScale = _faceScale;
        for (int i = 0; i < _contents.Length; i++)
        {
            _contents[i].anchoredPosition = _positions[i]; _contents[i].localScale = _scales[i]; _contents[i].localEulerAngles = _rotations[i];
        }
        _pressAmount = _hoverAmount = 0;
        if (_dimmed)
        {
            for (int i = 0; i < _graphics.Length; i++) _graphics[i].color = _contentColors[i];
            _dimmed = false;
        }
        if (_shadow != null) _shadow.SetActive(true);
        if (_focus != null) { _focus.enabled = false; _focus.color = _focusColor; }
    }
}
