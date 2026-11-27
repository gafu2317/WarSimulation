using System;
using System.Collections.Generic;

public sealed class CountryGovernmentDraft
{
    private readonly CountryGovernmentData _data;
    private readonly int[] _funding;
    private readonly Func<int, int> _level;
    private readonly Func<int, bool> _underConstruction;
    public int Level(int facility) => _level(facility);
    internal IReadOnlyDictionary<int, CountryGovernmentCost> Orders => _orders;
    internal int[] FundingSnapshot() => (int[])_funding.Clone();
    internal void ClearConstruction() => _orders.Clear();
    internal void RestoreFunding(int[] funding, bool useTreasury)
    {
        Array.Copy(funding, _funding, _funding.Length);
        UseTreasury = useTreasury;
    }
    private readonly Dictionary<int, CountryGovernmentCost> _orders = new();
    private int _treasury, _wood, _ore;
    private bool _reserveFunding = true, _reserveConstruction = true;

    public int Tax { get; private set; }
    public bool UseTreasury { get; private set; }
    public int FundingBasis => UseTreasury ? _treasury : Tax;
    public int OrderCount => _orders.Count;
    public int FundingTotal { get { int total = 0; foreach (int value in _funding) total += value; return total; } }
    public int RemainingTax => Math.Max(0, Tax - FundingTotal);
    public int TreasuryUsed => Math.Max(0, FundingTotal - Tax);
    public int TreasuryForecast => _treasury - (_reserveFunding ? FundingTotal : 0) - (_reserveConstruction ? OrderMoney : 0);
    public int WoodForecast => _wood - (_reserveConstruction ? OrderWood : 0);
    public int OreForecast => _ore - (_reserveConstruction ? OrderOre : 0);
    public bool CanConfirmFunding => FundingTotal <= Capacity && TreasuryForecast >= 0;
    private int Capacity => Math.Min(FundingBasis, _treasury - (_reserveConstruction ? OrderMoney : 0));
    private int OrderMoney { get { int total = 0; foreach (var cost in _orders.Values) total += cost.Money; return total; } }
    private int OrderWood { get { int total = 0; foreach (var cost in _orders.Values) total += cost.Wood; return total; } }
    private int OrderOre { get { int total = 0; foreach (var cost in _orders.Values) total += cost.Ore; return total; } }

    public CountryGovernmentDraft(CountryGovernmentData data, Func<int, int> level = null, Func<int, bool> underConstruction = null)
    {
        _data = data;
        _level = level ?? (id => data.Facilities[id].InitialLevel);
        _underConstruction = underConstruction ?? (_ => false);
        _funding = new int[data.Facilities.Length];
        for (int i = 0; i < _funding.Length; i++)
        {
            var facility = data.Facilities[i];
            _funding[i] = Level(i) > 0 ? Math.Max(facility.Minimum, facility.Funding) : 0;
        }
    }

    public CountryGovernmentDraft Copy()
    {
        var copy = new CountryGovernmentDraft(_data, _level, _underConstruction);
        Array.Copy(_funding, copy._funding, _funding.Length);
        foreach (var order in _orders) copy._orders.Add(order.Key, order.Value);
        copy._treasury = _treasury; copy._wood = _wood; copy._ore = _ore;
        copy.Tax = Tax; copy.UseTreasury = UseTreasury;
        copy._reserveFunding = _reserveFunding; copy._reserveConstruction = _reserveConstruction;
        return copy;
    }

    public void RemoveConstruction(int facility) => _orders.Remove(facility);

    public void UpdateResources(int treasury, int tax, int wood, int ore, bool taxIncluded = true, bool reserveFunding = true, bool reserveConstruction = true)
    {
        _treasury = treasury + (taxIncluded ? 0 : tax); Tax = tax; _wood = wood; _ore = ore;
        _reserveFunding = reserveFunding; _reserveConstruction = reserveConstruction;
    }

    public int Funding(int facility) => _funding[facility];
    public int MaximumFunding(int facility) => Math.Max(_data.Facilities[facility].FundingFloor, Capacity - FundingTotal + _funding[facility]);
    public void SetFunding(int facility, int amount)
    {
        if (Level(facility) == 0) return;
        _funding[facility] = Math.Clamp(amount, _data.Facilities[facility].FundingFloor, MaximumFunding(facility));
    }

    public bool SetUseTreasury(bool value)
    {
        if (!value && TreasuryUsed > 0) return false;
        UseTreasury = value;
        return true;
    }

    public bool IsCandidate(int facility, bool upgrade)
    {
        int level = Level(facility);
        return !_underConstruction(facility) && (upgrade ? level > 0 && level < 5 : level == 0);
    }
    public bool IsOrdered(int facility) => _orders.ContainsKey(facility);
    public CountryGovernmentCost Cost(int facility, bool upgrade)
    {
        var definition = _data.Facilities[facility];
        if (!upgrade) return definition.Construction;
        var cost = definition.Upgrade;
        int level = Level(facility);
        cost.Money *= level; cost.Wood *= level; cost.Ore *= level;
        return cost;
    }
    public bool CanConstruct(int facility, bool upgrade)
    {
        if (!IsCandidate(facility, upgrade) || IsOrdered(facility)) return false;
        var cost = Cost(facility, upgrade);
        return TreasuryForecast >= cost.Money && WoodForecast >= cost.Wood && OreForecast >= cost.Ore;
    }

    public bool PlanConstruction(int facility, bool upgrade)
    {
        if (!CanConstruct(facility, upgrade)) return false;
        _orders.Add(facility, Cost(facility, upgrade));
        return true;
    }

}
