using UnityEngine;

[DefaultExecutionOrder(-100)]
public class CountryCycleSystem : MonoBehaviour
{
    private DateManager _date;
    private PlayDataManager _resources;
    private bool _firstDayChecked;
    private bool _showRestoredReport;
    private double _nextDaySave;

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
        if (gameObject.scene.path == PlaytestSceneNavigation.CountryScene &&
            FindAnyObjectByType<CountryExpeditionPanel>() is { } panel)
        {
            // The panels bind to the campaign in Start, so load before their subscriptions.
            _showRestoredReport = _resources.InitializeCountry(panel.WorldData, panel.RosterData, _date) &&
                _date.CurrentDay == 1 && _date.ElapsedMonths > 0;
        }
        Subscribe();
    }

    private void LateUpdate()
    {
        if (!_firstDayChecked)
        {
            _firstDayChecked = true;
            _resources.ApplyFunding(CountryDate.From(_date));
            ProcessEncounters();
        }
        double now = Time.realtimeSinceStartupAsDouble;
        if ((!_date.IsRunning || now >= _nextDaySave) && _resources.SaveCountryProgress())
            _nextDaySave = now + 4d;
    }

    private void OnEnable() { if (_date != null && _resources != null) Subscribe(); }
    private void Subscribe()
    {
        _date.OnDateChanged += HandleDateChanged;
        _date.OnProgressionChanged += GuardBattleTime;
        _date.OnMonthChanged += HandleMonthChanged;
    }

    private void OnDisable()
    {
        if (_date == null) return;
        _date.OnDateChanged -= HandleDateChanged;
        _date.OnProgressionChanged -= GuardBattleTime;
        _date.OnMonthChanged -= HandleMonthChanged;
    }

    private void HandleDateChanged(int month, int day)
    {
        var today = CountryDate.From(_date);
        _resources.ProcessConstruction(today);
        if (day == 1) _resources.ApplyFunding(today);
        _resources.Expeditions?.AdvanceTo(today);
        _resources.Roster?.AdvanceTo(today);
        if (day == _date.DaysInCurrentMonth) _resources.TryCollectMonthlyIncome(_date.ElapsedMonths);
        ProcessEncounters();
        _resources.RequestCountrySave();
    }

    private void ProcessEncounters()
    {
        if (_resources.Expeditions?.Arrived != null)
        {
            _date.Pause();
            if (!_resources.ExpeditionBattleNeedsRetry && _resources.Battle == null)
                StartExpeditionBattle();
            return;
        }

        var today = CountryDate.From(_date);
        if (_resources.ReturnedFromCountryBattle || _showRestoredReport)
        {
            _resources.ReturnedFromCountryBattle = false;
            _showRestoredReport = false;
            if (today.Day == 1 && _resources.Defense?.Pending == null)
                FindAnyObjectByType<CountryGovernmentPanel>()?.OpenMonthlyReport();
        }
        bool raid = _resources.Defense?.AdvanceTo(today) == true;
        if (_resources.PublishBattleNotices() || raid) _date.Pause();
    }

    private void GuardBattleTime()
    {
        _resources.RequestCountrySave();
        if (!_date.IsRunning ||
            (_resources.Battle == null && _resources.Expeditions?.Arrived == null && _resources.Defense?.Pending == null)) return;
        _date.Pause();
        if (_resources.ExpeditionBattleNeedsRetry) StartExpeditionBattle();
    }

    private void StartExpeditionBattle()
    {
        if (!_resources.TryOpenExpeditionBattle())
            FindAnyObjectByType<CountryExpeditionPanel>()?.ShowBattleLaunchFailure();
    }

    private void HandleMonthChanged(int newMonth)
    {
        // 毎月の初めに時間の進行を一時停止させる
        _date.Pause();
    }
}
