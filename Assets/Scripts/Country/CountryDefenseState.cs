using System;
using System.Collections.Generic;
using System.Linq;

public sealed class CountryDefenseState
{
    private readonly CountryExpeditionState _world;
    private readonly CountryRoster _roster;
    private readonly Random _random = new();
    private int[] _members = Array.Empty<int>();
    public IReadOnlyList<int> Members => Array.AsReadOnly(_members);
    public IReadOnlyList<int> Weapons => Array.AsReadOnly(_members.Select(_roster.EquippedWeapon).ToArray());
    public CountryDate Today { get; private set; }
    public CountryDate NextRaid { get; private set; }
    public CountryRaid Pending { get; private set; }
    public bool Enabled { get; private set; } = true;
    public int CPCost => _world.Data.BattleCPCost;
    public event Action RaidStarted;

    public CountryDefenseState(CountryExpeditionState world, CountryRoster roster, CountryDate today)
    {
        _world = world; _roster = roster; Today = today;
        Schedule();
    }

    public void SetEnabled(bool enabled)
    {
        if (Enabled == enabled) return;
        Enabled = enabled;
        if (enabled) Schedule();
    }

    private void Schedule()
    {
        NextRaid = Today.AddDays(_random.Next(_world.Data.RaidMinDays, _world.Data.RaidMaxDays + 1));
        if (NextRaid.Day == 1) NextRaid = NextRaid.AddDays(1);
    }

    public bool AdvanceTo(CountryDate today)
    {
        if (today.CompareTo(Today) <= 0) return false;
        Today = today;
        if (!Enabled || Pending != null || today.Day == 1 || today.CompareTo(NextRaid) < 0) return false;
        var territories = Enumerable.Range(0, _world.Data.Regions.Length).Where(i => _world.Owner(i) == 0).ToArray();
        if (territories.Length == 0) { Schedule(); return false; }
        Pending = new CountryRaid(territories[_random.Next(territories.Length)], today);
        RaidStarted?.Invoke();
        return true;
    }

    public bool Validate(IReadOnlyList<int> members, IReadOnlyList<int> weapons, out string reason)
    {
        reason = "";
        if (members == null || weapons == null || members.Count > 26 || members.Count != weapons.Count)
        { reason = "兵士と武器を組み合わせて登録する"; return false; }
        var soldiers = new HashSet<int>(); var items = new HashSet<int>();
        for (int i = 0; i < members.Count; i++)
        {
            int soldier = members[i], weapon = weapons[i];
            if (soldier < 0 || soldier >= _roster.Soldiers.Count || !soldiers.Add(soldier))
            { reason = "同じ兵士は重ねて登録できない"; return false; }
            if (!_roster.CanDeploy(soldier))
            { reason = $"{_roster.Data.Soldiers[soldier].Name}は遠征中・訓練中・復活待ちのため参加できない"; return false; }
            if (_roster.EquippedWeapon(soldier) != weapon) { reason = "装備が変更されたため確認する"; return false; }
            if (weapon < 0 || weapon >= _roster.Data.Weapons.Length)
            { reason = "全員の武器を選ぶ"; return false; }
            if (!items.Add(weapon)) { reason = "同じ武器は複数人に登録できない"; return false; }
            int owner = _roster.WeaponOwner(weapon);
            if (owner >= 0 && _roster.Soldiers[owner].Expedition != null)
            { reason = "遠征中の武器は帰還まで使えない"; return false; }
        }
        return true;
    }

    public bool TryRegister(IReadOnlyList<int> members, IReadOnlyList<int> weapons, out string reason)
    {
        if (!Validate(members, weapons, out reason)) return false;
        _members = members.ToArray();
        return true;
    }

    public bool TryDefend(IReadOnlyList<int> members, IReadOnlyList<int> weapons, out string reason)
    {
        reason = "防衛メンバーを1人以上登録する";
        if (Pending == null || members == null || members.Count == 0) return false;
        if (!TryRegister(members, weapons, out reason)) return false;
        var date = Pending.Date;
        Pending = null;
        _roster.ConsumeBattleCP(_members, CPCost, date);
        Schedule();
        return true;
    }

}

public sealed class CountryRaid
{
    public int Region { get; }
    public CountryDate Date { get; }
    public CountryRaid(int region, CountryDate date) { Region = region; Date = date; }
}
