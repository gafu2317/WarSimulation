using UnityEngine;
using System.Collections.Generic;
using System;

public class PlayDataManager : ManagerBase<PlayDataManager>
{
    private const int TaxRate = 10, WoodRate = 2, OreRate = 1;
    [SerializeField, Min(0)] private int _population = 8500;
    [SerializeField, Min(0)] private int _money;
    public int Money => _money;
    public int Treasury => _money;
    public int Population => _population;
    public decimal MonthlyTaxIncome => _population * (TaxRate / 100m);
    public decimal MonthlyWoodIncome => _population * (WoodRate / 100m);
    public decimal MonthlyOreIncome => _population * (OreRate / 100m);
    public event Action OnResourcesChanged;
    public CountryRoster Roster { get; private set; }
    public CountryExpeditionState Expeditions { get; private set; }
    public CountryDefenseState Defense { get; private set; }
    public CountryGovernmentDraft GovernmentDraft { get; private set; }
    public bool RaidsEnabled { get; private set; } = true;

    public CountryGovernmentDraft GetOrCreateGovernmentDraft(CountryGovernmentData data)
        => GovernmentDraft ??= new CountryGovernmentDraft(data);

    public void SetGovernmentDraft(CountryGovernmentDraft draft) => GovernmentDraft = draft;

    public CountryDefenseState GetOrCreateDefense(CountryWorldMapData data, CountryRosterData roster, CountryDate today)
    {
        if (Defense == null)
        {
            Defense = new CountryDefenseState(GetOrCreateExpeditions(data, roster, today), Roster, today);
            Defense.SetEnabled(RaidsEnabled);
        }
        return Defense;
    }

    public void SetRaidsEnabled(bool enabled)
    {
        RaidsEnabled = enabled;
        Defense?.SetEnabled(enabled);
    }

    public CountryExpeditionState GetOrCreateExpeditions(CountryWorldMapData data, CountryRosterData roster, CountryDate today)
        => Expeditions ??= new CountryExpeditionState(data, GetOrCreateRoster(roster, today), today);

    public CountryRoster GetOrCreateRoster(CountryRosterData data, CountryDate today)
    {
        if (Roster == null)
        {
            foreach (var school in data.Schools)
                if (!_facilityLevels.ContainsKey(school.Facility)) _facilityLevels.Add(school.Facility, school.InitialLevel);
            Roster = new CountryRoster(data, today);
            foreach (var level in _facilityLevels) Roster.SetSchoolLevel(level.Key, level.Value);
        }
        return Roster;
    }

    [SerializeField, Min(0)] private int _wood = 300;
    public int Wood => _wood;

    [SerializeField, Min(0)] private int _ore = 150;
    public int Ore => _ore;

    [SerializeField, HideInInspector] private int _taxRemainder, _woodRemainder, _oreRemainder;
    [SerializeField, HideInInspector] private int _lastIncomeMonthIndex = -1;
    [SerializeField, HideInInspector] private CountryIncomeReceipt _lastIncomeReceipt;
    public CountryIncomeReceipt LastIncomeReceipt => _lastIncomeReceipt;

    private int _territory;
    public int Territory => _territory;

    private Dictionary<FacilityType, int> _facilityLevels = new Dictionary<FacilityType, int>();
    public IReadOnlyDictionary<FacilityType, int> FacilityLevels => _facilityLevels;

    public void SetPopulation(int population)
    {
        if (population < 0) throw new ArgumentOutOfRangeException(nameof(population));
        if (_population == population) return;
        _population = population;
        OnResourcesChanged?.Invoke();
    }

    public void SetResources(int population, int treasury, int wood, int ore)
    {
        if (population < 0) throw new ArgumentOutOfRangeException(nameof(population));
        if (treasury < 0) throw new ArgumentOutOfRangeException(nameof(treasury));
        if (wood < 0) throw new ArgumentOutOfRangeException(nameof(wood));
        if (ore < 0) throw new ArgumentOutOfRangeException(nameof(ore));
        if ((_population, _money, _wood, _ore) == (population, treasury, wood, ore)) return;
        (_population, _money, _wood, _ore) = (population, treasury, wood, ore);
        OnResourcesChanged?.Invoke();
    }

    public bool TryCollectMonthlyIncome(int monthIndex)
    {
        if (monthIndex < 0) throw new ArgumentOutOfRangeException(nameof(monthIndex));
        if (monthIndex <= _lastIncomeMonthIndex) return false;
        var (tax, wood, ore) = CalculateMonthlyIncome(out var remainders);
        (_money, _wood, _ore) = (checked(_money + tax), checked(_wood + wood), checked(_ore + ore));
        (_taxRemainder, _woodRemainder, _oreRemainder) = remainders;
        _lastIncomeMonthIndex = monthIndex;
        _lastIncomeReceipt = new CountryIncomeReceipt(monthIndex, tax, wood, ore);
        OnResourcesChanged?.Invoke();
        return true;
    }

    public (int Tax, int Wood, int Ore) EstimateMonthlyIncome() => CalculateMonthlyIncome(out _);

    private (int Tax, int Wood, int Ore) CalculateMonthlyIncome(out (int Tax, int Wood, int Ore) remainders)
    {
        int tax = Collect(_population, TaxRate, _taxRemainder, out int taxRemainder);
        int wood = Collect(_population, WoodRate, _woodRemainder, out int woodRemainder);
        int ore = Collect(_population, OreRate, _oreRemainder, out int oreRemainder);
        remainders = (taxRemainder, woodRemainder, oreRemainder);
        return (tax, wood, ore);
    }

    private static int Collect(int population, int rate, int remainder, out int nextRemainder)
    {
        long total = (long)population * rate + remainder;
        nextRemainder = (int)(total % 100);
        return (int)(total / 100);
    }

    public void SaveMoneyChange(int amount)
    {
        _money += amount;
        OnResourcesChanged?.Invoke();
        // TODO: データ保存（クラウド）
    }
    
    public void SaveWoodChange(int amount)
    {
        _wood += amount;
        OnResourcesChanged?.Invoke();
        // TODO: データ保存（クラウド）
    }
    
    public void SaveOreChange(int amount)
    {
        _ore += amount;
        OnResourcesChanged?.Invoke();
        // TODO: データ保存（クラウド）
    }

    public void SaveTerritoryChange(int amount)
    {
        _territory += amount;
        // TODO: データ保存（クラウド）
    }

    public void SaveFacilityLevelChange(FacilityType type, int levelDelta)
    {
        if (!_facilityLevels.ContainsKey(type))
        {
            _facilityLevels[type] = 0;
        }
        _facilityLevels[type] += levelDelta;
        Roster?.SetSchoolLevel(type, _facilityLevels[type]);
        // TODO: データ保存（クラウド）
    }

    public bool HasEnoughResources(int money, int wood, int ore)
    {
        return _money >= money && _wood >= wood && _ore >= ore;
    }

    public void SaveCurrentDate(int month, int day)
    {
        // TODO: データ保存（現在の日付）
    }

    public void SaveCountryOperation()
    {
        // TODO: データ保存（国家運営アクションの内容）
    }
}
