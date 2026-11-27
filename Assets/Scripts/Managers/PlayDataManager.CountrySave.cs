using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public partial class PlayDataManager
{
    private CountryCheckpointStore _checkpointStore;
    private DateManager _campaignClock;
    private bool _savePending, _saveError;
    private double _nextSaveRetry;
    private string _saveNotice;

    internal bool InitializeCountry(CountryWorldMapData world, CountryRosterData data, DateManager date)
    {
        if (_campaignClock != null) return false;
        _campaignClock = date;
        _checkpointStore = new CountryCheckpointStore();
        var loaded = _checkpointStore.Load(save => RestoreCountry(save, world, data));
        if (loaded == CountryCheckpointStore.LoadResult.Unreadable)
        {
            _checkpointStore = null;
            _saveNotice = "保存データを読み込めなかった。このプレイの進行は保存されない。";
        }
        else if (loaded == CountryCheckpointStore.LoadResult.Recovered)
            _saveNotice = "最新の進行を読み込めなかったため、一つ前の保存から再開した。";

        GetOrCreateDefense(world, data, CountryDate.From(date));
        OnResourcesChanged += RequestCountrySave;
        OnGovernmentChanged += RequestCountrySave;
        Roster.Changed += RequestCountrySave;
        Expeditions.Changed += RequestCountrySave;
        Defense.Changed += RequestCountrySave;
        if (loaded != CountryCheckpointStore.LoadResult.Loaded) RequestCountrySave();
        return loaded is CountryCheckpointStore.LoadResult.Loaded or CountryCheckpointStore.LoadResult.Recovered;
    }

    internal void RequestCountrySave() => _savePending = true;

    internal bool SaveCountryProgress()
    {
        if (_checkpointStore == null || !_savePending || Time.realtimeSinceStartupAsDouble < _nextSaveRetry) return false;
        if (_checkpointStore.Write(CaptureCountry()))
        {
            _savePending = false;
            _saveError = false;
            return true;
        }

        _nextSaveRetry = Time.realtimeSinceStartupAsDouble + 5d;
        if (!_saveError) _saveNotice = "進行を保存できなかった。保存できる状態になると再試行する。";
        _saveError = true;
        return false;
    }

    internal string TakeCountrySaveNotice()
    {
        string notice = _saveNotice;
        _saveNotice = null;
        return notice;
    }

    private void OnApplicationPause(bool paused) { if (paused) SaveCountryProgress(); }
    private void OnApplicationFocus(bool focused) { if (!focused) SaveCountryProgress(); }
    private void OnApplicationQuit() => SaveCountryProgress();

    private CountryCheckpoint CaptureCountry() => new() {
        Day = new CountryCheckpoint.Date(CountryDate.From(_campaignClock)),
        DayProgress = _campaignClock.DayProgress,
        Population = _population, Treasury = _money, Wood = _wood, Ore = _ore,
        TaxRemainder = _taxRemainder, WoodRemainder = _woodRemainder, OreRemainder = _oreRemainder,
        Income = _lastIncomeReceipt,
        Roster = Roster.Capture(),
        World = Expeditions.Capture(Defense.Capture()),
        Government = CaptureGovernment()
    };

    private CountryCheckpoint.GovernmentState CaptureGovernment() => new() {
        Facilities = _government.Facilities.Select((facility, i) => new CountryCheckpoint.Facility {
            Name = facility.Name, Level = _levels[i], Funding = _funding[i], DraftFunding = GovernmentDraft.Funding(i)
        }).ToArray(),
        ScheduledFunding = _scheduledFunding.Select(order => new CountryCheckpoint.FundingOrder {
            Month = order.Key, Amounts = order.Value.Select((amount, i) => new CountryCheckpoint.Funding {
                Facility = _government.Facilities[i].Name, Amount = amount
            }).ToArray()
        }).ToArray(),
        Construction = _construction.Select(order => new CountryCheckpoint.Construction {
            Facility = _government.Facilities[order.Key].Name, Cost = order.Value.Cost, Started = order.Value.Started,
            Start = new CountryCheckpoint.Date(order.Value.Start), Due = new CountryCheckpoint.Date(order.Value.Due)
        }).ToArray(),
        UseTreasury = GovernmentDraft.UseTreasury,
        ReportMonth = _reportMonth, FundedMonth = _fundedMonth, Report = _governmentReport.ToArray()
    };

    private void RestoreCountry(CountryCheckpoint save, CountryWorldMapData world, CountryRosterData data)
    {
        var today = save.Day.ToDate();
        CountryCheckpoint.Require(save.DayProgress is >= 0 and < 1 &&
            save.Population >= 0 && save.Treasury >= 0 && save.Wood >= 0 && save.Ore >= 0 &&
            save.TaxRemainder is >= 0 and < 100 && save.WoodRemainder is >= 0 and < 100 && save.OreRemainder is >= 0 and < 100 &&
            (!save.Income.Recorded || (save.Income.MonthIndex >= 0 && save.Income.MonthIndex <= today.MonthIndex &&
                save.Income.Tax >= 0 && save.Income.Wood >= 0 && save.Income.Ore >= 0)), "clock or resources");

        var roster = new CountryRoster(data, today, name => FacilityLevel(name, 1));
        const string spiritSchool = CountryGovernmentData.SpiritSchool;
        int spiritLevel = save.Government.Facilities?.Where(facility => facility.Name == spiritSchool)
            .Select(facility => (int?)facility.Level).FirstOrDefault()
            ?? data.Government.Facilities.FirstOrDefault(facility => facility.Name == spiritSchool)?.InitialLevel ?? 0;
        roster.SetSpiritSchoolLevel(spiritLevel);
        roster.Restore(save.Roster, legacyCP: save.Version == CountryCheckpoint.LegacyCPVersion);
        var expeditions = new CountryExpeditionState(world, roster, today);
        expeditions.Restore(save.World);
        var defense = new CountryDefenseState(expeditions, roster, today);
        defense.Restore(save.World.Defense);
        RestoreGovernment(save.Government, data.Government, today);

        Roster = roster;
        Expeditions = expeditions;
        Defense = defense;
        RaidsEnabled = defense.Enabled;
        (_population, _money, _wood, _ore) = (save.Population, save.Treasury, save.Wood, save.Ore);
        (_taxRemainder, _woodRemainder, _oreRemainder) = (save.TaxRemainder, save.WoodRemainder, save.OreRemainder);
        _lastIncomeReceipt = save.Income;
        _lastIncomeMonthIndex = save.Income.Recorded ? save.Income.MonthIndex : -1;
        _campaignClock.Restore(today, save.DayProgress);
        if (save.Version < CountryCheckpoint.CurrentVersion) RequestCountrySave();
    }

    private void RestoreGovernment(CountryCheckpoint.GovernmentState save, CountryGovernmentData data, CountryDate today)
    {
        CountryCheckpoint.Require(save.Facilities != null && save.Facilities.Length > 0 &&
            save.ScheduledFunding != null && save.Construction != null && save.Report != null &&
            save.ReportMonth >= -1 && save.ReportMonth <= today.MonthIndex &&
            save.FundedMonth >= -1 && save.FundedMonth <= today.MonthIndex, "government");
        int FacilityIndex(string name)
        {
            int id = data.FindFacility(name);
            CountryCheckpoint.Require(id >= 0, "facility name");
            return id;
        }

        int[] levels = data.Facilities.Select(facility => Math.Clamp(facility.InitialLevel, 0, 5)).ToArray();
        int[] funding = data.Facilities.Select((facility, i) => levels[i] > 0 ? Math.Max(facility.Minimum, facility.Funding) : 0).ToArray();
        int[] draftFunding = (int[])funding.Clone();
        var seen = new HashSet<int>();
        foreach (var facility in save.Facilities)
        {
            int id = FacilityIndex(facility.Name);
            CountryCheckpoint.Require(seen.Add(id) && facility.Level is >= 0 and <= 5 &&
                facility.Funding >= 0 && facility.DraftFunding >= 0, "facility");
            levels[id] = facility.Level;
            funding[id] = facility.Funding;
            draftFunding[id] = facility.DraftFunding;
        }
        CountryCheckpoint.Require(ValidFunding(funding) && ValidFunding(draftFunding), "funding total");

        var scheduled = new Dictionary<int, int[]>();
        foreach (var order in save.ScheduledFunding)
        {
            CountryCheckpoint.Require(order.Month > save.FundedMonth && order.Amounts != null, "funding order");
            var amounts = (int[])funding.Clone();
            seen.Clear();
            foreach (var amount in order.Amounts)
            {
                int id = FacilityIndex(amount.Facility);
                CountryCheckpoint.Require(seen.Add(id) && amount.Amount >= 0, "funding amount");
                amounts[id] = amount.Amount;
            }
            CountryCheckpoint.Require(ValidFunding(amounts) && scheduled.TryAdd(order.Month, amounts), "scheduled funding");
        }

        var construction = new Dictionary<int, (CountryGovernmentCost Cost, CountryDate Start, CountryDate Due, bool Started)>();
        foreach (var order in save.Construction)
        {
            int id = FacilityIndex(order.Facility);
            var start = order.Start.ToDate();
            var due = order.Due.ToDate();
            CountryCheckpoint.Require(order.Cost.Money >= 0 && order.Cost.Wood >= 0 && order.Cost.Ore >= 0 &&
                due.CompareTo(today) > 0 && due.CompareTo(start) > 0 && levels[id] < 5 &&
                (order.Started == (start.CompareTo(today) <= 0)) &&
                construction.TryAdd(id, (order.Cost, start, due, order.Started)), "construction order");
        }

        _government = data;
        _levels = levels;
        _funding = funding;
        foreach (var order in scheduled) _scheduledFunding.Add(order.Key, order.Value);
        foreach (var order in construction) _construction.Add(order.Key, order.Value);
        GovernmentDraft = new CountryGovernmentDraft(data, id => _levels[id], _construction.ContainsKey);
        GovernmentDraft.RestoreFunding(draftFunding, save.UseTreasury);
        _reportMonth = save.ReportMonth;
        _fundedMonth = save.FundedMonth;
        _governmentReport.AddRange(save.Report);
    }

    private static bool ValidFunding(int[] funding) => funding.Sum(value => (long)value) <= int.MaxValue;
}
