using System;
using TMPro;
using UnityEngine;

public sealed class CountryGovernmentAllocationRow : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _name, _minimum, _amount, _maximum, _effect;
    [SerializeField] private UnityEngine.UI.Slider _slider;
    [SerializeField] private UnityEngine.UI.Button _details;
    [SerializeField] private CountryFacilitySymbol _symbol;
    private CountryGovernmentFacility _facility;
    private CountrySliderScale _scale;

    public void Bind(CountryGovernmentFacility facility, Action<int> changed, Action details)
    {
        _facility = facility;
        _scale = _slider.GetComponentInChildren<CountrySliderScale>();
        _name.text = facility.Name;
        _symbol.SetFacility(facility.Name);
        _minimum.text = $"最低必要額 {CountryResourceDisplay.Format(facility.Minimum)}";
        _slider.minValue = 0;
        _slider.onValueChanged.AddListener(value => changed(Mathf.RoundToInt(value / 10) * 10));
        _details.onClick.AddListener(() => details());
    }

    public void Refresh(int amount, int maximum, int basis, string effect)
    {
        _amount.text = CountryResourceDisplay.Format(amount);
        _maximum.text = $"調整 {CountryResourceDisplay.Format(_facility.FundingFloor)}〜{CountryResourceDisplay.Format(maximum)}　100%＝{CountryResourceDisplay.Format(basis)}";
        _slider.maxValue = Mathf.Max(0, basis);
        _slider.SetValueWithoutNotify(amount);
        _scale.SetRange(_facility.FundingFloor, maximum);
        _slider.interactable = basis > 0 && (maximum > _facility.FundingFloor || amount > _facility.FundingFloor);
        _effect.text = effect;
    }
}
