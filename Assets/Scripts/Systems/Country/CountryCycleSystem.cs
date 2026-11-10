using UnityEngine;

public class CountryCycleSystem : MonoBehaviour
{
    private DateManager _date;
    private PlayDataManager _resources;

    private void Start()
    {
        _date = DateManager.Instance;
        _resources = PlayDataManager.Instance;
        if (_date == null || _resources == null)
        {
            Debug.LogError("CountryCycleSystem requires DateManager and PlayDataManager.", this);
            enabled = false;
            return;
        }
        Subscribe();
    }

    private void OnEnable() { if (_date != null && _resources != null) Subscribe(); }
    private void Subscribe()
    {
        _date.OnDateChanged += HandleDateChanged;
        _date.OnMonthChanged += HandleMonthChanged;
    }

    private void OnDisable()
    {
        if (_date == null) return;
        _date.OnDateChanged -= HandleDateChanged;
        _date.OnMonthChanged -= HandleMonthChanged;
    }

    private void HandleDateChanged(int month, int day)
    {
        _resources.Expeditions?.AdvanceTo(CountryDate.From(_date));
        _resources.Roster?.AdvanceTo(CountryDate.From(_date));
        if (day == _date.DaysInCurrentMonth && _resources.TryCollectMonthlyIncome(_date.ElapsedMonths))
            _resources.SaveCurrentDate(month, day);
        if (_resources.Defense?.AdvanceTo(CountryDate.From(_date)) == true) _date.Pause();
    }

    private void HandleMonthChanged(int newMonth)
    {
        // 毎月の初めに時間の進行を一時停止させる
        _date.Pause();
        _resources.SaveCurrentDate(_date.CurrentMonth, _date.CurrentDay);
    }
}
