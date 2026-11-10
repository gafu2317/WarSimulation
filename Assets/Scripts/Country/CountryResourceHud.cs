using TMPro;
using UnityEngine;

public sealed class CountryResourceHud : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _population, _taxIncome, _wood, _ore;
    private PlayDataManager _resources;

    private void Start()
    {
        _resources = PlayDataManager.Instance;
        if (_resources == null)
        {
            Debug.LogError("CountryResourceHud requires PlayDataManager.", this);
            enabled = false;
            return;
        }
        Subscribe();
    }

    private void OnEnable() { if (_resources != null) Subscribe(); }
    private void Subscribe()
    {
        _resources.OnResourcesChanged += Refresh;
        Refresh();
    }

    private void Refresh()
    {
        _population.text = CountryResourceDisplay.Format(_resources.Population);
        _taxIncome.text = CountryResourceDisplay.Format(_resources.Treasury);
        _wood.text = CountryResourceDisplay.Format(_resources.Wood);
        _ore.text = CountryResourceDisplay.Format(_resources.Ore);
    }

    private void OnDisable()
    {
        if (_resources != null) _resources.OnResourcesChanged -= Refresh;
    }
}
