using System.Collections.Generic;
using System.Linq;

public sealed class CountryExpeditionDraft
{
    private readonly CountryRoster _roster;
    private readonly List<int> _members = new();
    public int Target = -1;
    public IReadOnlyList<int> Members { get; }
    public CountryExpeditionDraft(CountryRoster roster) { _roster = roster; Members = _members.AsReadOnly(); }
    public void Load(IReadOnlyList<int> members, IReadOnlyList<int> weapons)
    { _members.Clear(); _members.AddRange(members); }
    public bool Contains(int soldier) => _members.Contains(soldier);
    public void Toggle(int soldier) { if (!_members.Remove(soldier)) _members.Add(soldier); }
    public int Weapon(int soldier) => _roster.EquippedWeapon(soldier);
    public int Owner(int weapon) => _roster.WeaponOwner(weapon);
    public int[] Weapons() => _members.Select(_roster.EquippedWeapon).ToArray();
}
