using TMPro;
using UnityEngine;

[DisallowMultipleComponent, RequireComponent(typeof(UnityEngine.UI.Toggle))]
public sealed class CountryToggleFeedback : MonoBehaviour
{
    [SerializeField] private UnityEngine.UI.Image _track;
    [SerializeField] private RectTransform _thumb;
    [SerializeField] private TextMeshProUGUI _state;
    private UnityEngine.UI.Toggle _toggle;
    private float _amount;

    private void Awake() => _toggle = GetComponent<UnityEngine.UI.Toggle>();
    private void OnEnable() { _amount = _toggle.isOn ? 1 : 0; Draw(); }
    private void LateUpdate()
    {
        float target = _toggle.isOn ? 1 : 0;
        if (_amount == target) return;
        _amount = Mathf.MoveTowards(_amount, target, Time.unscaledDeltaTime / .12f);
        Draw();
    }
    private void Draw()
    {
        float eased = Mathf.SmoothStep(0, 1, _amount);
        _thumb.anchoredPosition = new Vector2(Mathf.Lerp(-29, 29, eased), 0);
        _track.color = Color.Lerp(CountryGovernmentTheme.Recess, CountryGovernmentTheme.PositiveFace, eased);
        _state.text = _toggle.isOn ? "ON" : "OFF";
        _state.rectTransform.anchoredPosition = new Vector2(_toggle.isOn ? -24 : 24, 0);
    }
}
