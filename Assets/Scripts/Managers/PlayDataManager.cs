using UnityEngine;
using System.Collections.Generic;

public class PlayDataManager : ManagerBase<PlayDataManager>
{
    private int _money;
    public int Money => _money;

    private int _wood;
    public int Wood => _wood;

    private int _ore;
    public int Ore => _ore;

    private int _territory;
    public int Territory => _territory;

    private Dictionary<FacilityType, int> _facilityLevels = new Dictionary<FacilityType, int>();
    public IReadOnlyDictionary<FacilityType, int> FacilityLevels => _facilityLevels;

    public void SaveMoneyChange(int amount)
    {
        _money += amount;
        // TODO: データ保存（クラウド）
    }
    
    public void SaveWoodChange(int amount)
    {
        _wood += amount;
        // TODO: データ保存（クラウド）
    }
    
    public void SaveOreChange(int amount)
    {
        _ore += amount;
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
