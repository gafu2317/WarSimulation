using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class DateManagerTests
{
    private GameObject _root;
    private DateManager _date;

    [SetUp]
    public void SetUp()
    {
        _root = new GameObject("Calendar test");
        _date = _root.AddComponent<DateManager>();
    }

    [TearDown]
    public void TearDown() => Object.DestroyImmediate(_root);

    [Test]
    public void StartsPausedAndDoesNotAdvance()
    {
        _date.AdvanceTime(100);
        Assert.That(_date.IsRunning, Is.False);
        Assert.That(_date.CurrentDay, Is.EqualTo(1));
    }

    [Test]
    public void PauseAndResumePreserveTheUnfinishedDay()
    {
        _date.Resume();
        _date.AdvanceTime(.75);
        _date.Pause();
        _date.AdvanceTime(100);
        Assert.That(_date.CurrentDay, Is.EqualTo(1));

        _date.Resume();
        _date.AdvanceTime(.25);
        Assert.That(_date.CurrentDay, Is.EqualTo(2));
    }

    [Test]
    public void RepeatedResumeDoesNotAccelerateTime()
    {
        int stateChanges = 0;
        _date.OnProgressionChanged += () => stateChanges++;
        for (int i = 0; i < 10; i++) _date.Resume();
        _date.AdvanceTime(1);
        Assert.That(_date.CurrentDay, Is.EqualTo(2));
        Assert.That(stateChanges, Is.EqualTo(1));
    }

    [Test]
    public void DoubleSpeedAdvancesTwoDaysPerSecond()
    {
        var days = new List<int>();
        _date.OnDateChanged += (_, day) => days.Add(day);
        _date.ResumeDoubleSpeed();
        _date.AdvanceTime(1);
        Assert.That(_date.SpeedMultiplier, Is.EqualTo(2));
        Assert.That(days, Is.EqualTo(new[] { 2, 3 }));
    }

    [Test]
    public void ChangingSpeedPreservesTheUnfinishedDay()
    {
        _date.Resume();
        _date.AdvanceTime(.75);
        _date.ResumeDoubleSpeed();
        _date.AdvanceTime(.125);
        Assert.That(_date.CurrentDay, Is.EqualTo(2));

        _date.AdvanceTime(.125);
        _date.Resume();
        _date.AdvanceTime(.75);
        Assert.That(_date.CurrentDay, Is.EqualTo(3));
    }

    [Test]
    public void PausingDoubleSpeedPreservesTheUnfinishedDay()
    {
        _date.ResumeDoubleSpeed();
        _date.AdvanceTime(.375);
        _date.Pause();
        _date.AdvanceTime(100);
        Assert.That(_date.SpeedMultiplier, Is.Zero);
        _date.Resume();
        _date.AdvanceTime(.25);
        Assert.That(_date.CurrentDay, Is.EqualTo(2));
    }

    [Test]
    public void RepeatedDoubleSpeedDoesNotMultiplyTheSpeedAgain()
    {
        int changes = 0;
        _date.OnProgressionChanged += () => changes++;
        for (int i = 0; i < 10; i++) _date.ResumeDoubleSpeed();
        _date.AdvanceTime(.5);
        Assert.That(_date.CurrentDay, Is.EqualTo(2));
        Assert.That(changes, Is.EqualTo(1));
    }

    [TestCase(1, 31, 2)]
    [TestCase(2, 28, 3)]
    [TestCase(4, 30, 5)]
    [TestCase(12, 31, 1)]
    public void DoubleSpeedStopsAtTheMonthBoundaryWhenTheMonthlyHandlerPauses(int month, int day, int nextMonth)
    {
        SetDate(month, day);
        _date.OnMonthChanged += _ => _date.Pause();
        _date.ResumeDoubleSpeed();
        _date.AdvanceTime(100);
        Assert.That((_date.CurrentMonth, _date.CurrentDay), Is.EqualTo((nextMonth, 1)));
        Assert.That(_date.ElapsedMonths, Is.EqualTo(1));
        Assert.That(_date.SpeedMultiplier, Is.Zero);
        _date.Resume();
        _date.AdvanceTime(.1);
        Assert.That(_date.CurrentDay, Is.EqualTo(1));
    }

    [Test]
    public void DailyEventCanInterruptDoubleSpeedWithoutLeavingCatchUpTime()
    {
        _date.OnDateChanged += (_, day) => { if (day == 3) _date.Pause(); };
        _date.ResumeDoubleSpeed();
        _date.AdvanceTime(100);
        Assert.That(_date.CurrentDay, Is.EqualTo(3));
        _date.ResumeDoubleSpeed();
        _date.AdvanceTime(.1);
        Assert.That(_date.CurrentDay, Is.EqualTo(3));
    }

    [TestCase(1, 31)]
    [TestCase(2, 28)]
    [TestCase(4, 30)]
    [TestCase(12, 31)]
    public void MonthEndNotifiesEveryDayAndStopsBeforeTheNextMonthEvent(int month, int days)
    {
        SetDate(month, 1);
        var observed = new List<(int, int)>();
        int changes = 0, nextMonth = month % 12 + 1;
        _date.OnDateChanged += (m, day) => observed.Add((m, day));
        _date.OnMonthChanged += m => {
            changes++;
            Assert.That(m, Is.EqualTo(nextMonth));
            Assert.That(_date.IsRunning, Is.False);
        };
        _date.AdvanceToMonthEnd();
        _date.AdvanceToMonthEnd();
        _date.AdvanceTime(100);

        Assert.That(observed, Is.EqualTo(Enumerable.Range(2, days - 1).Select(day => (month, day)).Append((nextMonth, 1))));
        Assert.That(_date.CurrentMonth, Is.EqualTo(nextMonth));
        Assert.That(_date.CurrentDay, Is.EqualTo(1));
        Assert.That(changes, Is.EqualTo(1));
        Assert.That(_date.IsRunning, Is.False);
        Assert.That(_date.IsAdvancingToMonthEnd, Is.False);
    }

    [Test]
    public void MonthEndCanStartOnTheFinalDay()
    {
        SetDate(2, 28);
        var events = new List<string>();
        _date.OnProgressionChanged += () => { if (!_date.IsRunning) events.Add("stopped"); };
        _date.OnDateChanged += (_, _) => events.Add("date");
        _date.OnMonthChanged += _ => events.Add("month");
        _date.AdvanceToMonthEnd();
        _date.AdvanceTime(100);
        Assert.That((_date.CurrentMonth, _date.CurrentDay), Is.EqualTo((3, 1)));
        Assert.That(events, Is.EqualTo(new[] { "stopped", "date", "month" }));
    }

    [TestCase(1, 31, 2)]
    [TestCase(2, 28, 3)]
    [TestCase(4, 30, 5)]
    [TestCase(12, 31, 1)]
    public void NormalProgressionCrossesTheMonthBoundary(int month, int day, int nextMonth)
    {
        SetDate(month, day);
        var changes = new List<int>();
        _date.OnMonthChanged += changes.Add;
        _date.Resume();
        _date.AdvanceTime(1);
        Assert.That(_date.CurrentMonth, Is.EqualTo(nextMonth));
        Assert.That(_date.CurrentDay, Is.EqualTo(1));
        Assert.That(changes, Is.EqualTo(new[] { nextMonth }));
        Assert.That(_date.ElapsedMonths, Is.EqualTo(1));
    }

    [Test]
    public void ElapsedMonthsDistinguishesTheSameMonthInTheNextYear()
    {
        _date.Resume();
        _date.AdvanceTime(365);
        Assert.That(_date.CurrentMonth, Is.EqualTo(1));
        Assert.That(_date.CurrentDay, Is.EqualTo(1));
        Assert.That(_date.ElapsedMonths, Is.EqualTo(12));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void DailyEventCanInterruptWithoutLeavingCatchUpTime(bool monthEnd)
    {
        _date.OnDateChanged += (_, day) => { if (day == 3) _date.Pause(); };
        if (monthEnd) _date.AdvanceToMonthEnd(); else _date.Resume();
        _date.AdvanceTime(100);
        Assert.That(_date.CurrentDay, Is.EqualTo(3));
        _date.Resume();
        _date.AdvanceTime(.1);
        Assert.That(_date.CurrentDay, Is.EqualTo(3));
    }

    [Test]
    public void MonthEndCanBeCancelledAndNormalSpeedRestored()
    {
        _date.AdvanceToMonthEnd();
        _date.AdvanceTime(.5);
        int day = _date.CurrentDay;
        Assert.That(day, Is.GreaterThan(1).And.LessThan(31));
        _date.Pause();
        _date.AdvanceTime(100);
        Assert.That(_date.CurrentDay, Is.EqualTo(day));
        _date.Resume();
        _date.AdvanceTime(.5);
        Assert.That(_date.CurrentDay, Is.EqualTo(day));
        _date.AdvanceTime(.5);
        Assert.That(_date.CurrentDay, Is.EqualTo(day + 1));
    }

    private void SetDate(int month, int day)
    {
        var data = new SerializedObject(_date);
        data.FindProperty("_currentMonth").intValue = month;
        data.FindProperty("_currentDay").intValue = day;
        data.ApplyModifiedPropertiesWithoutUndo();
    }
}
