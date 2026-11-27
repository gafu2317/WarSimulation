using System;
using System.Collections.Generic;
using System.Linq;

public sealed class CountryBattle
{
    public readonly struct Soldier
    {
        public readonly string Name;
        public readonly string WeaponName;
        public readonly WeaponKind Weapon;
        public readonly CountrySoldierStats Stats;
        public readonly CombatAiPersonalityKind Personality;
        public readonly IReadOnlyList<SkillId> Skills;

        internal Soldier(string name, CountryWeaponDefinition weapon, CountrySoldierStats stats,
            CombatAiPersonalityKind personality, IReadOnlyList<SkillId> skills)
        {
            Name = name; WeaponName = weapon.Name; Weapon = weapon.Kind; Stats = stats; Personality = personality; Skills = skills;
        }
    }

    private readonly Func<bool, bool> _resolve;
    public string Region { get; }
    public bool IsDefense { get; }
    public bool Resolved { get; private set; }
    public IReadOnlyList<Soldier> Soldiers { get; }
    public int EnemyStatPercent { get; }
    internal int TimeLimitSeconds { get; }
    public IReadOnlyList<SkillId> EnemySkills { get; }

    internal CountryBattle(CountryRoster roster, CountryWorldMapData world, CountryDate date,
        string region, IReadOnlyList<int> members, IReadOnlyList<int> weapons,
        Func<bool, bool> resolve, bool isDefense = false)
    {
        Region = region;
        IsDefense = isDefense;
        TimeLimitSeconds = world.BattleTimeLimitSeconds;
        _resolve = resolve;
        int months = Math.Min(date.MonthIndex, world.EnemyGrowthMonths);
        int startPercent = isDefense ? world.DefenseEnemyStartPercent : 100;
        EnemyStatPercent = startPercent + months * world.EnemyMonthlyGrowthPercent;
        int skillLevel = Math.Min(5, months / world.EnemySkillIntervalMonths);
        EnemySkills = Array.AsReadOnly(CountryRoster.BasicSkills.Concat(roster.Data.Schools.SelectMany(school =>
            school.FirstSkills.Take(skillLevel).Concat(school.SecondSkills.Take(skillLevel))))
            .Where(skill => skill != SkillId.None).Distinct().ToArray());
        Soldiers = Array.AsReadOnly(members.Select((id, i) => new Soldier(roster.Data.Soldiers[id].Name,
            roster.Weapon(weapons[i]), roster.Stats(id, weapons[i]), roster.Soldiers[id].Personality,
            Array.AsReadOnly(roster.Soldiers[id].Learned.ToArray()))).ToArray());
    }

    internal int ScaleEnemyStat(int value) => (int)((long)value * EnemyStatPercent / 100);

    internal void Resolve(bool won)
    {
        if (!Resolved) Resolved = _resolve(won);
    }
}
