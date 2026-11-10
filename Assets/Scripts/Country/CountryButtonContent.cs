using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class CountryButtonContent : MonoBehaviour
{
    [SerializeField] private RectTransform _icon;
    [SerializeField] private TextMeshProUGUI _label;
    [SerializeField] private Vector2 _sidePadding = new(24, 24);
    private string _text;
    private float _width = -1;

    private void OnEnable()
    {
        _text = null; _width = -1;
        Align();
    }
    private void LateUpdate() => Align();
    private void Align()
    {
        if (_icon == null || _label == null) return;
        float width = ((RectTransform)transform).rect.width;
        if (width <= 0 || _width == width && _text == _label.text) return;
        _width = width; _text = _label.text;
        float textWidth = _label.GetPreferredValues(_text).x;
        float start = _sidePadding.x + Mathf.Max(0, (width - _sidePadding.x - _sidePadding.y - _icon.rect.width - 16 - textWidth) * .5f);
        _icon.anchorMin = _icon.anchorMax = new Vector2(0, .5f);
        _icon.pivot = new Vector2(0, .5f); _icon.anchoredPosition = new Vector2(start, 0);
        var label = _label.rectTransform;
        label.anchorMin = Vector2.zero; label.anchorMax = Vector2.up; label.pivot = new Vector2(0, .5f);
        label.sizeDelta = new Vector2(textWidth, -24);
        label.anchoredPosition = new Vector2(start + _icon.rect.width + 16, 0);
        if (TryGetComponent<CountryButtonFeedback>(out var feedback)) feedback.RefreshContentPositions();
    }
}
