using System;
using System.Text.RegularExpressions;
using UnityEngine;

[CreateAssetMenu(menuName = "WarSimulation/Country/Soldiers and Weapons")]
public sealed class CountryRosterData : ScriptableObject
{
    public bool Provisional = true;
    public CombatSkillCatalog Skills;
    public CountryGovernmentData Government;
    public CountrySoldierDefinition[] Soldiers = Array.Empty<CountrySoldierDefinition>();
    public UnityEngine.Vector4[] ForgeWeights = { new(100, 0, 0, 0), new(40, 60, 0, 0), new(20, 30, 50, 0), new(10, 20, 30, 40) };
    public CountryWeaponDefinition[] Weapons = Array.Empty<CountryWeaponDefinition>();
    public CountrySchoolDefinition[] Schools = Array.Empty<CountrySchoolDefinition>();

    public string SkillName(SkillId skill)
    {
        if (Skills == null || !Skills.TryGetDefinition(skill, out var definition)) return skill.ToString();
        return Regex.Replace(definition.DisplayName, @"\\+u([0-9a-fA-F]{4})", match => ((char)Convert.ToInt32(match.Groups[1].Value, 16)).ToString()).Replace("弹", "弾");
    }
}

[Serializable]
public sealed class CountrySoldierDefinition
{
    public string Id, Name;
    public Sprite Icon, StandingArt;
    public Rect StandingArtBounds;
    public CountrySoldierStats InitialStats;
    [Min(1)] public int MaxCP = 100;
    public string InitialWeaponId;
}

[Serializable]
public sealed class CountryWeaponDefinition
{
    public string Id, Name;
    public WeaponKind Kind;
    [Range(0, 4)] public int Tier;
    [Min(1)] public int MaxDurability = 5;
    public Sprite Image;
    public CountrySoldierStats Bonus;
}

[Serializable]
public sealed class CountrySchoolDefinition
{
    public string Name;
    public WeaponKind FirstWeapon, SecondWeapon;
    public CountrySoldierStats BonusAt(int level) => level > 0 && level <= LevelBonusPercent.Length ? LevelBonusPercent[level - 1] : default;
    public CountrySoldierStats[] LevelBonusPercent = new CountrySoldierStats[5];
    public SkillId[] FirstSkills = new SkillId[5], SecondSkills = new SkillId[5];
}

[Serializable]
public struct CountrySoldierStats
{
    public int HP, STR, INT, FAI, AGI;
    public static CountrySoldierStats operator +(CountrySoldierStats a, CountrySoldierStats b) => new() {
        HP = a.HP + b.HP, STR = a.STR + b.STR, INT = a.INT + b.INT,
        FAI = a.FAI + b.FAI, AGI = a.AGI + b.AGI
    };
    public CountrySoldierStats Grown(CountrySoldierStats percent) => new() {
        HP = HP + HP * percent.HP / 100, STR = STR + STR * percent.STR / 100, INT = INT + INT * percent.INT / 100,
        FAI = FAI + FAI * percent.FAI / 100, AGI = AGI + AGI * percent.AGI / 100
    };
}
