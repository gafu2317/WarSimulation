using System;
using System.Collections.Generic;
using System.Linq;

public sealed class CountryExpeditionState
{
    private readonly CountryRoster _roster;
    private readonly int[] _owners;
    private readonly bool[,] _adjacent;
    private readonly List<CountryExpeditionTrip> _active = new(), _returned = new();
    public CountryWorldMapData Data { get; }
    public CountryDate Today { get; private set; }
    public IReadOnlyList<CountryExpeditionTrip> Active { get; }
    public IReadOnlyList<CountryExpeditionTrip> Returned { get; }
    public event Action Changed;
    public CountryExpeditionTrip Arrived => _active.Where(trip => !trip.Returning && Today.CompareTo(trip.Arrival) >= 0)
        .OrderBy(trip => trip.Arrival).FirstOrDefault();

    public CountryExpeditionState(CountryWorldMapData data, CountryRoster roster, CountryDate today)
    {
        Data = data ?? throw new ArgumentNullException(nameof(data)); data.Validate();
        _roster = roster ?? throw new ArgumentNullException(nameof(roster)); Today = today;
        _owners = data.Regions.Select(region => region.InitialOwner).ToArray();
        _adjacent = new bool[_owners.Length, _owners.Length];
        for (int i = 0; i < _owners.Length; i++)
            for (int j = i + 1; j < _owners.Length; j++) _adjacent[i, j] = _adjacent[j, i] = data.Adjacent(i, j);
        Active = _active.AsReadOnly(); Returned = _returned.AsReadOnly();
    }

    public int Owner(int region) => _owners[region];

    internal int RegionIndex(string id)
    {
        int index = Array.FindIndex(Data.Regions, region => region.Id == id);
        CountryCheckpoint.Require(index >= 0, "region ID");
        return index;
    }

    internal CountryCheckpoint.WorldState Capture(CountryCheckpoint.DefenseState defense) => new() {
        Conquered = Data.Regions.Where((region, i) => region.InitialOwner != 0 && _owners[i] == 0).Select(region => region.Id).ToArray(),
        Active = _active.Select(CaptureTrip).ToArray(),
        Returned = _returned.Select(CaptureTrip).ToArray(),
        Defense = defense
    };

    private CountryCheckpoint.Trip CaptureTrip(CountryExpeditionTrip trip) => new() {
        Target = Data.Regions[trip.Target].Id,
        Departure = new CountryCheckpoint.Date(trip.Departure),
        Days = trip.Days,
        Members = trip.Members.Select(_roster.SoldierId).ToArray(),
        Weapons = trip.Weapons.ToArray(),
        Outcome = trip.Won switch { true => CountryCheckpoint.Result.Victory, false => CountryCheckpoint.Result.Defeat, _ => CountryCheckpoint.Result.Pending }
    };

    internal void Restore(CountryCheckpoint.WorldState save)
    {
        CountryCheckpoint.Require(save.Conquered != null && save.Active != null && save.Returned != null, "world");
        foreach (string region in save.Conquered) _owners[RegionIndex(region)] = 0;
        var targets = new HashSet<int>();
        foreach (var entry in save.Active)
        {
            var trip = RestoreTrip(entry);
            CountryCheckpoint.Require(targets.Add(trip.Target) && (!trip.Returning || Today.CompareTo(trip.Arrival) >= 0) &&
                Today.CompareTo(trip.Return) < 0 && (trip.Won == true) == (_owners[trip.Target] == 0), "active expedition");
            for (int i = 0; i < trip.Members.Count; i++)
            {
                int soldier = trip.Members[i], weapon = trip.Weapons[i];
                CountryCheckpoint.Require(_roster.Soldiers[soldier].Expedition == null &&
                    _roster.EquippedWeapon(soldier) == weapon && (trip.Returning || _roster.IsUsableWeapon(weapon)),
                    "expedition equipment");
            }
            _active.Add(trip);
            _roster.Depart(trip);
        }
        foreach (var entry in save.Returned)
        {
            var trip = RestoreTrip(entry);
            CountryCheckpoint.Require(trip.Returning && Today.CompareTo(trip.Return) >= 0, "returned expedition");
            _returned.Add(trip);
        }
        for (int i = 0; i < _roster.WeaponCount; i++)
        {
            int owner = _roster.WeaponOwner(i);
            CountryCheckpoint.Require(owner < 0 || _roster.IsUsableWeapon(i) || _roster.Soldiers[owner].Expedition != null,
                "broken weapon");
        }
    }

    private CountryExpeditionTrip RestoreTrip(CountryCheckpoint.Trip entry)
    {
        var departure = entry.Departure.ToDate();
        CountryCheckpoint.Require(departure.CompareTo(Today) <= 0 && entry.Days is >= 10 and <= 30 &&
            entry.Members != null && entry.Members.Length is >= 1 and <= 26 && entry.Weapons != null &&
            entry.Members.Length == entry.Weapons.Length && entry.Members.Distinct().Count() == entry.Members.Length &&
            entry.Weapons.Distinct().Count() == entry.Weapons.Length &&
            entry.Weapons.All(weapon => weapon >= 0 && weapon < _roster.WeaponCount) &&
            Enum.IsDefined(typeof(CountryCheckpoint.Result), entry.Outcome), "expedition");
        return new CountryExpeditionTrip(RegionIndex(entry.Target), departure, entry.Days,
            entry.Members.Select(_roster.SoldierIndex).ToArray(), entry.Weapons) {
            Won = entry.Outcome switch { CountryCheckpoint.Result.Victory => true, CountryCheckpoint.Result.Defeat => false, _ => null }
        };
    }

    public bool CanTarget(int region) => region >= 0 && region < _owners.Length && _owners[region] != 0 &&
        !_active.Any(trip => trip.Target == region) && Enumerable.Range(0, _owners.Length).Any(i => _owners[i] == 0 && _adjacent[i, region]);

    public bool CanSelect(int soldier, int target, out string reason)
    {
        reason = "";
        if (soldier < 0 || soldier >= _roster.Soldiers.Count) { reason = "兵士を選ぶ"; return false; }
        if (!_roster.CanDeploy(soldier))
        {
            var state = _roster.Soldiers[soldier];
            reason = state.CP == 0 ? "復活待ち" : state.Expedition != null ? "遠征中" : "訓練中"; return false;
        }
        var training = _roster.Soldiers[soldier].Training;
        if (target >= 0 && target < _owners.Length && training != null && Today.AddDays(Data.TravelDays(target) * 2).CompareTo(training.Start) > 0)
        { reason = $"訓練開始 {training.Start} に帰還が間に合わない"; return false; }
        return true;
    }

    public bool ValidateDeparture(int target, IReadOnlyList<int> members, IReadOnlyList<int> weapons, out string reason)
    {
        reason = "";
        if (!CanTarget(target)) { reason = "自領に隣接する遠征先を選ぶ"; return false; }
        if (members == null || weapons == null || members.Count < 1 || members.Count > 26 || weapons.Count != members.Count)
        { reason = "遠征メンバーを選ぶ"; return false; }
        var soldiers = new HashSet<int>(); var items = new HashSet<int>();
        for (int i = 0; i < members.Count; i++)
        {
            if (!soldiers.Add(members[i])) { reason = "同じ兵士は重ねて選べない"; return false; }
            if (!CanSelect(members[i], target, out reason)) return false;
            int weapon = weapons[i];
            if (_roster.EquippedWeapon(members[i]) != weapon) { reason = "装備が変更されたため確認する"; return false; }
            if (!_roster.IsUsableWeapon(weapon)) { reason = "全員の武器を選ぶ"; return false; }
            if (!items.Add(weapon)) { reason = "同じ武器は複数人に持たせられない"; return false; }
            int owner = _roster.WeaponOwner(weapon);
            if (owner >= 0 && _roster.Soldiers[owner].Expedition != null) { reason = "遠征中の武器は使えない"; return false; }
        }
        return true;
    }

    public bool TryDepart(int target, IReadOnlyList<int> members, IReadOnlyList<int> weapons, out string reason)
    {
        if (!ValidateDeparture(target, members, weapons, out reason)) return false;
        var trip = new CountryExpeditionTrip(target, Today, Data.TravelDays(target), members, weapons);
        _active.Add(trip);
        _roster.Depart(trip);
        Changed?.Invoke();
        return true;
    }

    public void AdvanceTo(CountryDate today)
    {
        if (today.CompareTo(Today) <= 0) return;
        Today = today;
        bool changed = false;
        foreach (var trip in _active.ToArray())
        {
            if (!trip.Returning || today.CompareTo(trip.Return) < 0) continue;
            _active.Remove(trip); _returned.Add(trip); _roster.Return(trip); changed = true;
        }
        if (changed) Changed?.Invoke();
    }

    public bool ResolveBattle(CountryExpeditionTrip trip, bool won)
    {
        if (trip == null || !_active.Contains(trip) || trip.Returning || Today.CompareTo(trip.Arrival) < 0) return false;
        trip.Won = won;
        if (won) _owners[trip.Target] = 0;
        _roster.ConsumeBattleCP(trip.Members, Data.BattleCPCost, trip.Arrival);
        if (Today.CompareTo(trip.Return) >= 0)
        {
            _active.Remove(trip); _returned.Add(trip); _roster.Return(trip);
        }
        Changed?.Invoke();
        return true;
    }
}

public sealed class CountryExpeditionTrip
{
    public int Target { get; }
    public int Days { get; }
    public CountryDate Departure { get; }
    public CountryDate Arrival { get; }
    public CountryDate Return { get; }
    public IReadOnlyList<int> Members { get; }
    public IReadOnlyList<int> Weapons { get; }
    public bool? Won { get; internal set; }
    public bool Returning => Won.HasValue;
    public int ReportMonth => Return.MonthIndex + (Return.Day == 1 ? 0 : 1);
    public CountryExpeditionTrip(int target, CountryDate departure, int days, IReadOnlyList<int> members, IReadOnlyList<int> weapons)
    {
        Target = target; Days = days; Departure = departure; Arrival = departure.AddDays(days); Return = Arrival.AddDays(days);
        Members = Array.AsReadOnly(members.ToArray()); Weapons = Array.AsReadOnly(weapons.ToArray());
    }
}
