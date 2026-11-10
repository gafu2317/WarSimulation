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
            if (weapon < 0 || weapon >= _roster.Data.Weapons.Length) { reason = "全員の武器を選ぶ"; return false; }
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
            if (!trip.Returning && today.CompareTo(trip.Arrival) >= 0)
            {
                _owners[trip.Target] = 0; trip.Returning = true; changed = true;
                _roster.ConsumeBattleCP(trip.Members, Data.BattleCPCost, trip.Arrival);
            }
            if (today.CompareTo(trip.Return) < 0) continue;
            _active.Remove(trip); _returned.Add(trip); _roster.Return(trip); changed = true;
        }
        if (changed) Changed?.Invoke();
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
    public bool Returning { get; internal set; }
    public int ReportMonth => Return.MonthIndex + (Return.Day == 1 ? 0 : 1);
    public CountryExpeditionTrip(int target, CountryDate departure, int days, IReadOnlyList<int> members, IReadOnlyList<int> weapons)
    {
        Target = target; Days = days; Departure = departure; Arrival = departure.AddDays(days); Return = Arrival.AddDays(days);
        Members = Array.AsReadOnly(members.ToArray()); Weapons = Array.AsReadOnly(weapons.ToArray());
    }
}
