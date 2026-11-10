using System;
using UnityEngine;

[Serializable]
public struct CountryIncomeReceipt
{
    [SerializeField] private bool _recorded;
    [SerializeField] private int _monthIndex, _tax, _wood, _ore;
    public bool Recorded => _recorded;
    public int MonthIndex => _monthIndex;
    public int Month => _monthIndex % 12 + 1;
    public int Tax => _tax;
    public int Wood => _wood;
    public int Ore => _ore;

    public CountryIncomeReceipt(int monthIndex, int tax, int wood, int ore)
    {
        _recorded = true; _monthIndex = monthIndex; _tax = tax; _wood = wood; _ore = ore;
    }
}
