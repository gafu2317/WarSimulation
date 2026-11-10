using TMPro;
using UnityEngine;

[RequireComponent(typeof(CanvasRenderer))]
public sealed class CountrySliderScale : UnityEngine.UI.MaskableGraphic
{
    [SerializeField] private UnityEngine.UI.Slider _slider;
    [SerializeField] private TextMeshProUGUI[] _labels;
    private float _value = float.NaN;
    private float _minimum, _maximum, _basis;

    public void SetRange(int minimum, int maximum)
    {
        _minimum = minimum; _maximum = maximum;
        Refresh();
        SetVerticesDirty();
    }

    protected override void OnEnable() { base.OnEnable(); _value = float.NaN; Refresh(); }
    private void LateUpdate() => Refresh();
    private void Refresh()
    {
        if (_slider == null || _labels == null) return;
        float value = _slider.normalizedValue;
        if (_value == value && _basis == _slider.maxValue) return;
        bool initialize = float.IsNaN(_value);
        _value = value;
        _basis = _slider.maxValue;
        for (int i = 0; i < _labels.Length; i++)
        {
            var label = _labels[i];
            if (initialize) label.gameObject.SetActive(i < 5);
            if (i >= 5) continue;
            float fraction = i * .25f;
            if (initialize)
            {
                label.text = $"{i * 25}%";
                var rect = label.rectTransform; rect.anchorMin = rect.anchorMax = new Vector2(fraction, .5f);
                rect.pivot = new Vector2(fraction == 0 ? 0 : fraction == 1 ? 1 : .5f, .5f);
                rect.anchoredPosition = new Vector2(0, -28);
                label.alignment = fraction == 0 ? TextAlignmentOptions.MidlineLeft : fraction == 1 ? TextAlignmentOptions.MidlineRight : TextAlignmentOptions.Center;
            }
            label.color = CountryGovernmentTheme.Muted;
        }
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper mesh)
    {
        mesh.Clear();
        if (_slider == null || float.IsNaN(_value)) return;
        var bounds = GetPixelAdjustedRect();
        float low = _basis > 0 ? Mathf.Clamp01(_minimum / _basis) : 0;
        float high = _basis > 0 ? Mathf.Clamp01(_maximum / _basis) : 0;
        Band(0, low, CountryGovernmentTheme.Recess);
        Band(low, high, CountryGovernmentTheme.AccentText);
        Band(high, 1, CountryGovernmentTheme.Recess);
        Tick(low, true); Tick(high, true);
        for (int tick = 0; tick <= 20; tick++) Tick(tick / 20f, tick % 5 == 0);
        void Band(float start, float end, Color tint)
        {
            int offset = mesh.currentVertCount;
            float left = Mathf.Lerp(bounds.xMin, bounds.xMax, start), right = Mathf.Lerp(bounds.xMin, bounds.xMax, end);
            mesh.AddVert(new Vector2(left, bounds.center.y - 18), tint, Vector2.zero); mesh.AddVert(new Vector2(left, bounds.center.y - 15), tint, Vector2.zero);
            mesh.AddVert(new Vector2(right, bounds.center.y - 15), tint, Vector2.zero); mesh.AddVert(new Vector2(right, bounds.center.y - 18), tint, Vector2.zero);
            mesh.AddTriangle(offset, offset + 1, offset + 2); mesh.AddTriangle(offset + 2, offset + 3, offset);
        }
        void Tick(float fraction, bool major)
        {
            var rect = GetPixelAdjustedRect();
            float x = Mathf.Lerp(rect.xMin, rect.xMax, fraction);
            var tint = CountryGovernmentTheme.Muted;
            tint.a = major ? .9f : .45f;
            int offset = mesh.currentVertCount; float half = major ? 1.25f : .75f;
            float top = rect.center.y - 4, bottom = rect.center.y - (major ? 13 : 9);
            mesh.AddVert(new Vector2(x - half, bottom), tint, Vector2.zero); mesh.AddVert(new Vector2(x - half, top), tint, Vector2.zero);
            mesh.AddVert(new Vector2(x + half, top), tint, Vector2.zero); mesh.AddVert(new Vector2(x + half, bottom), tint, Vector2.zero);
            mesh.AddTriangle(offset, offset + 1, offset + 2); mesh.AddTriangle(offset + 2, offset + 3, offset);
        }
    }
}
