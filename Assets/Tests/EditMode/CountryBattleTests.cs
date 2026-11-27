using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class CountryBattleTests
{
    private CountryWorldMapData _world;
    private CountryRosterData _data;
    private CountryRoster _roster;

    [SetUp]
    public void SetUp()
    {
        _world = ScriptableObject.CreateInstance<CountryWorldMapData>();
        _world.EnemyMonthlyGrowthPercent = 7;
        _world.EnemyGrowthMonths = 18;
        _world.EnemySkillIntervalMonths = 3;
        _data = ScriptableObject.CreateInstance<CountryRosterData>();
        _data.Soldiers = new[] { new CountrySoldierDefinition { Id = "soldier", Name = "兵士" } };
        _data.Weapons = new[] { new CountryWeaponDefinition { Id = "sword", Kind = WeaponKind.Sword } };
        _data.Schools = new[] { new CountrySchoolDefinition {
            FirstSkills = new[] { SkillId.Sword_QuickSlash, SkillId.None, SkillId.Sword_StrongSlash },
            SecondSkills = new[] { SkillId.Shield_ShoulderGuard, SkillId.Sword_QuickSlash, SkillId.Shield_Taunt }
        } };
        _roster = new CountryRoster(_data, new CountryDate(0, 1, 1));
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_data);
        Object.DestroyImmediate(_world);
    }

    [Test]
    public void EnemyGrowthContinuesAcrossNewYearAndStopsAtTheConfiguredLimit()
    {
        int previous = Battle(0).EnemyStatPercent;
        for (int month = 1; month <= _world.EnemyGrowthMonths; month++)
        {
            int current = Battle(month).EnemyStatPercent;
            Assert.That(current, Is.GreaterThan(previous), $"Month {month}");
            previous = current;
        }
        Assert.That(Battle(int.MaxValue).EnemyStatPercent, Is.EqualTo(previous));
        Assert.That(Battle(int.MaxValue).EnemySkills, Is.EquivalentTo(Battle(_world.EnemyGrowthMonths).EnemySkills));
    }

    [Test]
    public void EnemySkillsUnlockFromSchoolTablesWithoutEmptyOrRepeatedSkills()
    {
        Assert.That(Battle(0).EnemySkills, Is.EquivalentTo(CountryRoster.BasicSkills));
        Assert.That(Battle(_world.EnemySkillIntervalMonths - 1).EnemySkills, Is.EquivalentTo(CountryRoster.BasicSkills));
        var first = Battle(_world.EnemySkillIntervalMonths).EnemySkills;
        Assert.That(first, Has.Member(SkillId.Sword_QuickSlash));
        Assert.That(first, Has.Member(SkillId.Shield_ShoulderGuard));
        Assert.That(first, Has.No.Member(SkillId.Sword_StrongSlash));
        var later = Battle(_world.EnemyGrowthMonths).EnemySkills;
        Assert.That(later, Is.SupersetOf(first));
        Assert.That(later, Has.Member(SkillId.Sword_StrongSlash));
        Assert.That(later, Has.No.Member(SkillId.None));
        Assert.That(later, Is.Unique);
    }

    [Test]
    public void BattleKeepsItsEnemyStrengthWhenSettingsOrSchoolSkillsChange()
    {
        var battle = Battle(_world.EnemySkillIntervalMonths, true);
        int statPercent = battle.EnemyStatPercent;
        var skills = battle.EnemySkills.ToArray();
        _world.DefenseEnemyStartPercent = 101;
        _world.EnemyMonthlyGrowthPercent = 0;
        _world.EnemySkillIntervalMonths = int.MaxValue;
        _data.Schools[0].FirstSkills[0] = SkillId.None;
        Assert.That(battle.EnemyStatPercent, Is.EqualTo(statPercent));
        Assert.That(battle.EnemySkills, Is.EqualTo(skills));
    }

    [Test]
    public void DefenseHasItsOwnStartingStatsAndSharesMonthlyGrowthAndSkillsWithExpedition()
    {
        _world.DefenseEnemyStartPercent = 82;
        var startDefense = Battle(0, true);
        Assert.That(startDefense.EnemyStatPercent, Is.EqualTo(82));
        Assert.That(startDefense.ScaleEnemyStat(7), Is.EqualTo(5));
        Assert.That(Battle(0).EnemyStatPercent, Is.EqualTo(100));

        var expedition = Battle(3);
        var defense = Battle(3, true);
        Assert.That(defense.EnemyStatPercent, Is.EqualTo(103));
        Assert.That(expedition.EnemyStatPercent, Is.EqualTo(121));
        Assert.That(defense.EnemySkills, Is.EqualTo(expedition.EnemySkills));
    }

    private CountryBattle Battle(int month, bool defense = false) => new(_roster, _world,
        new CountryDate(month, month % 12 + 1, 1), "領地", new[] { 0 }, new[] { 0 }, _ => true, defense);
}
