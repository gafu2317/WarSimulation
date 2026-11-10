using System;
using UnityEngine;

public class DateManager : ManagerBase<DateManager>
{
    [SerializeField, Range(1, 12)] private int _currentMonth = 1;
    [SerializeField, Range(1, 31)] private int _currentDay = 1;
    [SerializeField, Min(.05f), Tooltip("通常進行で1日が経過する秒数")]
    private float _secondsPerDay = 1;
    [SerializeField, Min(.05f), Tooltip("月末早送りの基準秒数（実際の進行は5倍速）")]
    private float _monthEndSecondsPerDay = .15f;
    [SerializeField, HideInInspector] private int _elapsedMonths;

    private enum Progression { Paused, Running, DoubleSpeed, MonthEnd }
    private Progression _progression;
    private double _dayProgress;
    private static readonly int[] DaysPerMonth = { 31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31 };

    public int CurrentMonth => _currentMonth;
    public int CurrentDay => _currentDay;
    public int DaysInCurrentMonth => DaysPerMonth[_currentMonth - 1];
    public int ElapsedMonths => _elapsedMonths;
    public bool IsRunning => _progression != Progression.Paused;
    public int SpeedMultiplier => !IsRunning ? 0 : _progression == Progression.DoubleSpeed ? 2 : 1;
    public bool IsAdvancingToMonthEnd => _progression == Progression.MonthEnd;
    public static int GetDaysInMonth(int month) => DaysPerMonth[month - 1];
    public event Action<int, int> OnDateChanged;
    public event Action<int> OnMonthChanged;
    public event Action OnProgressionChanged;

    protected override void OnAwakeInitialize() => ValidateSettings();
    private void OnValidate() => ValidateSettings();
    private void Update() => AdvanceTime(Time.unscaledDeltaTime);

    public void Resume()
    {
        if (!isActiveAndEnabled || _progression == Progression.Running) return;
        SetProgression(Progression.Running);
    }

    public void Pause() => SetProgression(Progression.Paused);

    public void ResumeDoubleSpeed()
    {
        if (!isActiveAndEnabled) return;
        SetProgression(Progression.DoubleSpeed);
    }

    public void AdvanceToMonthEnd()
    {
        if (!isActiveAndEnabled || IsAdvancingToMonthEnd) return;
        _dayProgress = 0;
        SetProgression(Progression.MonthEnd);
    }

    public void AdvanceTime(double seconds)
    {
        if (!IsRunning || !isActiveAndEnabled || seconds <= 0 || double.IsNaN(seconds) || double.IsInfinity(seconds)) return;
        while (IsRunning)
        {
            double interval = IsAdvancingToMonthEnd ? _monthEndSecondsPerDay / 5d : _secondsPerDay / (double)SpeedMultiplier;
            double remaining = (1 - _dayProgress) * interval;
            if (seconds < remaining)
            {
                _dayProgress += seconds / interval;
                break;
            }
            seconds -= remaining;
            _dayProgress = 0;
            AddDay();
        }
    }

    private void AddDay()
    {
        bool monthChanged = ++_currentDay > DaysInCurrentMonth;
        if (monthChanged)
        {
            _currentDay = 1;
            _currentMonth = _currentMonth % 12 + 1;
            _elapsedMonths++;
            if (IsAdvancingToMonthEnd) Pause();
        }
        OnDateChanged?.Invoke(CurrentMonth, CurrentDay);
        if (monthChanged) OnMonthChanged?.Invoke(CurrentMonth);
    }

    private void SetProgression(Progression progression)
    {
        if (_progression == progression) return;
        _progression = progression;
        OnProgressionChanged?.Invoke();
    }

    private void ValidateSettings()
    {
        _currentMonth = Mathf.Clamp(_currentMonth, 1, 12);
        _currentDay = Mathf.Clamp(_currentDay, 1, DaysInCurrentMonth);
        _secondsPerDay = Mathf.Max(.05f, _secondsPerDay);
        _monthEndSecondsPerDay = Mathf.Max(.05f, _monthEndSecondsPerDay);
    }

    private void OnDisable() => Pause();
    private void OnApplicationPause(bool paused) { if (paused) Pause(); }
}
