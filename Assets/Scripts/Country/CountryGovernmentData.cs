using System;
using UnityEngine;

[CreateAssetMenu(menuName = "WarSimulation/Country/Government UI")]
public sealed class CountryGovernmentData : ScriptableObject
{
    public const string Blacksmith = "鍛冶場";
    public const string SpiritSchool = "精霊使役学校";
    internal static int SpiritCPBonus(int level) => 10 * (Math.Clamp(level, 1, 5) - 1);
    public int FindFacility(string name) => Array.FindIndex(Facilities, facility => facility.Name == name);
    public CountryGovernmentFacility[] Facilities = Array.Empty<CountryGovernmentFacility>();
}

[Serializable]
public sealed class CountryGovernmentFacility
{
    public string Name, EffectName;
    [Range(0, 5)] public int InitialLevel;
    [Min(0)] public int Minimum, Funding;
    public bool TrainingSchool;
    public CountryGovernmentCost Construction, Upgrade;
    public int FundingFloor => Name == CountryGovernmentData.Blacksmith ? 0 : Minimum;
    public string Role => Name switch {
        "戦士学校" => "剣・盾の技を学ぶ訓練",
        "魔法学校" => "杖・魔導書の技を学ぶ訓練",
        "教会" => "聖書・ロザリオの技を学ぶ訓練",
        CountryGovernmentData.SpiritSchool => "施設Lvに応じて全員の最大CPが上昇",
        "鍛冶場" => "武器を製作。施設Lvで品質が向上",
        _ => ""
    };
}

[Serializable]
public struct CountryGovernmentCost
{
    [Min(0)] public int Money, Wood, Ore;
    [Min(1)] public int Months;
    public string Effect;
}
