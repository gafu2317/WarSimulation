using System;
using System.Text.RegularExpressions;
using UnityEngine;

[CreateAssetMenu(menuName = "WarSimulation/Country/Soldiers and Weapons")]
public sealed class CountryRosterData : ScriptableObject
{
    public bool Provisional = true;
    public CombatSkillCatalog Skills;
    public CountrySoldierDefinition[] Soldiers = Array.Empty<CountrySoldierDefinition>();
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
    public CountrySoldierStats InitialStats;
    [Min(1)] public int MaxCP = 100;
    public string InitialWeaponId;
}

[Serializable]
public sealed class CountryWeaponDefinition
{
    public string Id, Name;
    public WeaponKind Kind;
    public Sprite Image;
    public CountrySoldierStats Bonus;
}

[Serializable]
public sealed class CountrySchoolDefinition
{
    public string Name;
    public FacilityType Facility;
    [Range(1, 5)] public int InitialLevel = 1;
    public WeaponKind FirstWeapon, SecondWeapon;
    public CountrySoldierStats Gains;
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
}
