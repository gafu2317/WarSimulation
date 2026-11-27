using UnityEngine;
using System.Collections.Generic;
using System;
using System.Linq;

public partial class PlayDataManager : ManagerBase<PlayDataManager>
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
    public CountryBattle Battle { get; private set; }
    internal bool ExpeditionBattleNeedsRetry { get; private set; }
    internal bool ReturnedFromCountryBattle { get; set; }

    internal bool TryOpenExpeditionBattle()
    {
        var trip = Expeditions?.Arrived;
        if (trip == null || Battle != null) return false;
        var request = new CountryBattle(Roster, Expeditions.Data, trip.Arrival,
            Expeditions.Data.Regions[trip.Target].Name, trip.Members, trip.Weapons,
            won => Expeditions.ResolveBattle(trip, won));
        ExpeditionBattleNeedsRetry = !TryOpenBattle(request);
        return !ExpeditionBattleNeedsRetry;
    }

    public bool TryOpenDefenseBattle(IReadOnlyList<int> members, IReadOnlyList<int> weapons, out string reason)
    {
        if (Battle != null || PlaytestSceneNavigation.IsLoading)
        { reason = "戦闘画面へ移動中"; return false; }
        if (Defense?.Pending == null)
        { reason = "対応中の襲撃がない"; return false; }
        reason = "防衛メンバーを1人以上登録する";
        if (members == null || members.Count == 0) return false;
        if (!Defense.TryRegister(members, weapons, out reason)) return false;
        var raid = Defense.Pending;
        var party = members.ToArray();
        var equipment = weapons.ToArray();
        var request = new CountryBattle(Roster, Expeditions.Data, raid.Date,
            Expeditions.Data.Regions[raid.Region].Name, party, equipment,
            _ => Defense.ResolveBattle(raid, party, equipment), isDefense: true);
        if (TryOpenBattle(request)) return true;
        reason = "戦闘画面を開けなかった";
        return false;
    }

    private bool TryOpenBattle(CountryBattle request)
    {
        if (!PlaytestSceneNavigation.TryOpenCountryBattle()) return false;
        Battle = request;
        RequestCountrySave();
        SaveCountryProgress();
        return true;
    }

    internal bool ResolveCountryBattle(CountryBattle battle, bool won)
    {
        if (battle == null || Battle != battle) return false;
        if (battle.Resolved) return true;
        battle.Resolve(won);
        if (!battle.Resolved) return false;
        RequestCountrySave();
        SaveCountryProgress();
        return true;
    }

    internal void ReturnFromCountryBattle(CountryBattle battle)
    {
        if (Battle != battle) return;
        ExpeditionBattleNeedsRetry = !battle.IsDefense && !battle.Resolved;
        ReturnedFromCountryBattle = battle.Resolved;
        Battle = null;
    }
    public CountryGovernmentDraft GovernmentDraft { get; private set; }
    private CountryGovernmentData _government;
    private int[] _levels, _funding;
    private readonly Dictionary<int, int[]> _scheduledFunding = new();
    private readonly Dictionary<int, (CountryGovernmentCost Cost, CountryDate Start, CountryDate Due, bool Started)> _construction = new();
    private readonly List<string> _governmentReport = new();
    private int _reportMonth = -1, _fundedMonth = -1;
    public IReadOnlyList<string> GovernmentReport => _governmentReport;
    public bool HasAppliedFunding(CountryDate date) => _fundedMonth >= date.MonthIndex;
    public event Action OnGovernmentChanged;
    public event Action<string> OnBattleNotices;

    public bool PublishBattleNotices()
    {
        if (Roster == null || OnBattleNotices == null || Defense?.Pending != null) return false;
        var notices = Roster.TakeBattleNotices();
        if (notices.Count == 0) return false;
        OnBattleNotices.Invoke(string.Join("\n\n", notices));
        RequestCountrySave();
        return true;
    }
    public bool RaidsEnabled { get; private set; } = true;

    public CountryGovernmentDraft GetOrCreateGovernmentDraft(CountryGovernmentData data)
    {
        if (GovernmentDraft != null) return GovernmentDraft;
        _government = data;
        _levels = data.Facilities.Select(facility => Math.Clamp(facility.InitialLevel, 0, 5)).ToArray();
        GovernmentDraft = new CountryGovernmentDraft(data, id => _levels[id], _construction.ContainsKey);
        _funding = GovernmentDraft.FundingSnapshot();
        OnGovernmentChanged?.Invoke();
        return GovernmentDraft;
    }

    public int FacilityLevel(string name, int fallback = 0)
    {
        int id = _government?.FindFacility(name) ?? -1;
        return id >= 0 ? _levels[id] : fallback;
    }

    public void ConfirmGovernment(CountryGovernmentDraft draft, CountryDate today, CountryDate fundingStart, bool applyFunding, bool startConstruction)
    {
        var start = today.PlanningStart;
        if (applyFunding) _scheduledFunding[fundingStart.MonthIndex] = draft.FundingSnapshot();
        if (startConstruction)
            foreach (var order in draft.Orders)
                if (!_construction.ContainsKey(order.Key) && _levels[order.Key] < 5)
                    _construction.Add(order.Key, (order.Value, start, start.AddMonths(Math.Max(1, order.Value.Months)), false));
        var saved = draft.Copy();
        saved.ClearConstruction();
        GovernmentDraft = saved;
        if (today.Day == 1)
        {
            ProcessConstruction(today);
            if (applyFunding && fundingStart.CompareTo(today) == 0) ApplyFunding(today);
        }
        RequestCountrySave();
    }

    private void PrepareReport(CountryDate date)
    {
        if (_reportMonth == date.MonthIndex) return;
        _reportMonth = date.MonthIndex;
        _governmentReport.Clear();
    }

    public void ProcessConstruction(CountryDate date)
    {
        if (_government == null || date.Day != 1) return;
        PrepareReport(date);
        foreach (var order in _construction.ToArray())
        {
            int id = order.Key;
            var work = order.Value;
            string name = _government.Facilities[id].Name;
            if (work.Started && date.CompareTo(work.Due) >= 0)
            {
                _construction.Remove(id);
                int level = Math.Min(5, _levels[id] + 1);
                SetFacilityLevel(id, level);
                _governmentReport.Add($"{name}が完成した（Lv.{level}）");
                OnGovernmentChanged?.Invoke();
            }
            else if (!work.Started && date.CompareTo(work.Start) >= 0)
            {
                var cost = work.Cost;
                if (HasEnoughResources(cost.Money, cost.Wood, cost.Ore))
                {
                    _construction[id] = (cost, work.Start, date.AddMonths(Math.Max(1, cost.Months)), true);
                    SetResources(_population, _money - cost.Money, _wood - cost.Wood, _ore - cost.Ore);
                }
                else
                {
                    _construction.Remove(id);
                    _governmentReport.Add($"資源不足で{name}に着工できなかった");
                }
            }
        }
    }

    public void ApplyFunding(CountryDate date)
    {
        if (_government == null || date.Day != 1 || HasAppliedFunding(date)) return;
        _fundedMonth = date.MonthIndex;
        PrepareReport(date);
        if (_scheduledFunding.Remove(date.MonthIndex, out var funding)) _funding = funding;
        int total = _funding.Sum();
        if (_money < total)
        {
            _governmentReport.Add("国庫が不足し、今月の施設配分を見送った");
            return;
        }
        int forge = _government.FindFacility(CountryGovernmentData.Blacksmith);
        if (forge >= 0 && Roster != null)
        {
            var names = Roster.Forge(date, _levels[forge], _funding[forge], _government.Facilities[forge].Minimum);
            if (names.Count > 0) _governmentReport.Add("完成した武器　" + string.Join("、", names));
        }
        SetResources(_population, _money - total, _wood, _ore);
    }

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
        RequestCountrySave();
    }

    public CountryExpeditionState GetOrCreateExpeditions(CountryWorldMapData data, CountryRosterData roster, CountryDate today)
        => Expeditions ??= new CountryExpeditionState(data, GetOrCreateRoster(roster, today), today);

    public CountryRoster GetOrCreateRoster(CountryRosterData data, CountryDate today)
    {
        if (Roster == null)
        {
            if (data.Government != null) GetOrCreateGovernmentDraft(data.Government);
            Roster = new CountryRoster(data, today, name => FacilityLevel(name, 1));
            Roster.SetSpiritSchoolLevel(FacilityLevel(CountryGovernmentData.SpiritSchool));
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

    private static readonly string[] LegacyFacilityNames = { "戦士学校", "魔法学校", "教会", CountryGovernmentData.SpiritSchool };
    public IReadOnlyDictionary<FacilityType, int> FacilityLevels => LegacyFacilityNames
        .Select((name, id) => (name, id)).ToDictionary(item => (FacilityType)item.id, item => FacilityLevel(item.name, 1));

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
        tax = Math.Min(tax, int.MaxValue - _money);
        wood = Math.Min(wood, int.MaxValue - _wood);
        ore = Math.Min(ore, int.MaxValue - _ore);
        (_money, _wood, _ore) = (_money + tax, _wood + wood, _ore + ore);
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
        int id = _government?.FindFacility(LegacyFacilityNames[(int)type]) ?? -1;
        if (id < 0) return;
        SetFacilityLevel(id, Math.Clamp(_levels[id] + levelDelta, 0, 5));
        OnGovernmentChanged?.Invoke();
    }

    private void SetFacilityLevel(int id, int level)
    {
        _levels[id] = level;
        if (_government.Facilities[id].Name == CountryGovernmentData.SpiritSchool)
            Roster?.SetSpiritSchoolLevel(level);
    }

    public bool HasEnoughResources(int money, int wood, int ore)
    {
        return _money >= money && _wood >= wood && _ore >= ore;
    }

    public void SaveCurrentDate(int month, int day) => RequestCountrySave();

    public void SaveCountryOperation() => RequestCountrySave();
}
