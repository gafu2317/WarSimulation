using TMPro;
using UnityEngine;
using UnityEngine.Events;

public sealed class CountryGovernmentChoice : MonoBehaviour
{
    [SerializeField] private UnityEngine.UI.Button _button;
    [SerializeField] private UnityEngine.UI.Image _frame;
    [SerializeField] private TextMeshProUGUI _name, _value;
    [SerializeField] private GameObject _selected;
    [SerializeField] private CountryFacilitySymbol _symbol;

    public void Bind(string label, UnityAction clicked)
    {
        _name.text = label;
        _symbol.SetFacility(label);
        _button.onClick.AddListener(clicked);
    }

    public void Refresh(string value, bool selected)
    {
        _value.text = value;
        _selected.SetActive(selected);
        var marker = _selected.GetComponent<UnityEngine.UI.Graphic>();
        marker.enabled = true; marker.color = CountryGovernmentTheme.Accent;
        _frame.color = Color.clear;
        _symbol.color = _symbol.HasImage ? Color.white : selected ? CountryGovernmentTheme.Accent : CountryGovernmentTheme.Muted;
        _name.color = CountryGovernmentTheme.Text;
        CountryGovernmentTheme.Colors(_button, selected ? CountryGovernmentTheme.Selected : CountryGovernmentTheme.Panel);
    }

    public void Select() => _button.Select();
}
