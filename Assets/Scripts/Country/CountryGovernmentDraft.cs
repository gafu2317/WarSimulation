using System;
using System.Collections.Generic;

public sealed class CountryGovernmentDraft
{
    private readonly CountryGovernmentData _data;
    private readonly int[] _funding;
    private readonly Dictionary<int, CountryGovernmentCost> _orders = new();
    private int _treasury, _wood, _ore;

    public int Tax { get; private set; }
    public bool UseTreasury { get; private set; }
    public int FundingBasis => UseTreasury ? _treasury : Tax;
    public int OrderCount => _orders.Count;
    public int FundingTotal { get { int total = 0; foreach (int value in _funding) total += value; return total; } }
    public int RemainingTax => Math.Max(0, Tax - FundingTotal);
    public int TreasuryUsed => Math.Max(0, FundingTotal - Tax);
    public int TreasuryForecast => _treasury - FundingTotal - OrderMoney;
    public int WoodForecast => _wood - OrderWood;
    public int OreForecast => _ore - OrderOre;
    public bool CanConfirmFunding => FundingTotal <= Capacity && TreasuryForecast >= 0;
    private int Capacity => Math.Min(FundingBasis, _treasury - OrderMoney);
    private int OrderMoney { get { int total = 0; foreach (var cost in _orders.Values) total += cost.Money; return total; } }
    private int OrderWood { get { int total = 0; foreach (var cost in _orders.Values) total += cost.Wood; return total; } }
    private int OrderOre { get { int total = 0; foreach (var cost in _orders.Values) total += cost.Ore; return total; } }

    public CountryGovernmentDraft(CountryGovernmentData data)
    {
        _data = data;
        _funding = new int[data.Facilities.Length];
        for (int i = 0; i < _funding.Length; i++)
        {
            var facility = data.Facilities[i];
            _funding[i] = facility.InitialLevel > 0 ? Math.Max(facility.Minimum, facility.Funding) : 0;
        }
    }

    public CountryGovernmentDraft Copy()
    {
        var copy = new CountryGovernmentDraft(_data);
        Array.Copy(_funding, copy._funding, _funding.Length);
        foreach (var order in _orders) copy._orders.Add(order.Key, order.Value);
        copy._treasury = _treasury; copy._wood = _wood; copy._ore = _ore;
        copy.Tax = Tax; copy.UseTreasury = UseTreasury;
        return copy;
    }

    public void RemoveConstruction(int facility) => _orders.Remove(facility);

    public void UpdateResources(int treasury, int tax, int wood, int ore, bool taxIncluded = true)
    {
        _treasury = treasury + (taxIncluded ? 0 : tax); Tax = tax; _wood = wood; _ore = ore;
    }

    public int Funding(int facility) => _funding[facility];
    public int MaximumFunding(int facility) => Math.Max(_data.Facilities[facility].Minimum, Capacity - FundingTotal + _funding[facility]);
    public void SetFunding(int facility, int amount)
    {
        if (_data.Facilities[facility].InitialLevel == 0) return;
        _funding[facility] = Math.Clamp(amount, _data.Facilities[facility].Minimum, MaximumFunding(facility));
    }

    public bool SetUseTreasury(bool value)
    {
        if (!value && TreasuryUsed > 0) return false;
        UseTreasury = value;
        return true;
    }

    public int EffectPercent(int facility)
    {
        int minimum = _data.Facilities[facility].Minimum;
        return minimum > 0 ? 100 + (_funding[facility] - minimum) * 20 / minimum : 100;
    }

    public bool IsCandidate(int facility, bool upgrade) => (_data.Facilities[facility].InitialLevel > 0) == upgrade;
    public bool IsOrdered(int facility) => _orders.ContainsKey(facility);
    public CountryGovernmentCost Cost(int facility, bool upgrade) => upgrade ? _data.Facilities[facility].Upgrade : _data.Facilities[facility].Construction;
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
