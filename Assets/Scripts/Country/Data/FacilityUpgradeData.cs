using UnityEngine;

[System.Serializable]
public struct FacilityUpgradeCost
{
    public int MoneyCost;
    public int WoodCost;
    public int OreCost;
    public int RequiredDays;
}

public static class FacilityUpgradeData
{
    // 仮の固定データ
    public static FacilityUpgradeCost GetCost(FacilityType type, int targetLevel)
    {
        return new FacilityUpgradeCost
        {
            MoneyCost = 100 * targetLevel,
            WoodCost = 50 * targetLevel,
            OreCost = 30 * targetLevel,
            RequiredDays = 5
        };
    }
}
