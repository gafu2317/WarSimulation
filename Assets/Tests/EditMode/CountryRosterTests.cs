using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class CountryRosterTests
{
    private CountryRosterData _data;
    private CountryRoster _roster;
    private readonly CountryDate _start = new(0, 1, 1);
    [SetUp]
    public void SetUp()
    {
        _data = ScriptableObject.CreateInstance<CountryRosterData>();
        _data.Skills = CombatSkillCatalog.CreateDefaultRuntimeCatalog();
        _data.Soldiers = Enumerable.Range(0, 8).Select(i => new CountrySoldierDefinition {
            Id = $"s{i}", Name = $"Soldier {i}", MaxCP = 100,
            InitialStats = new CountrySoldierStats { HP = 100 + i, STR = 10 + i, INT = 20 - i },
            InitialWeaponId = i < 6 ? $"w{i}" : null
        }).ToArray();
        _data.Weapons = Enumerable.Range(0, 6).Select(i => new CountryWeaponDefinition {
            Id = $"w{i}", Kind = (WeaponKind)(i + 1), Name = $"Weapon {i}", Bonus = new CountrySoldierStats { STR = i + 2 }
        }).ToArray();
        _data.Schools = new[] {
            new CountrySchoolDefinition { Name = "戦士学校", Facility = FacilityType.WarriorSchool, FirstWeapon = WeaponKind.Sword, SecondWeapon = WeaponKind.Shield,
                Gains = new CountrySoldierStats { HP = 5, STR = 2 },
                FirstSkills = new[] { SkillId.Sword_QuickSlash, SkillId.Sword_StrongSlash, SkillId.None, SkillId.None, SkillId.None },
                SecondSkills = new[] { SkillId.Shield_ShoulderGuard, SkillId.Shield_IronWall, SkillId.Shield_Taunt, SkillId.None, SkillId.None } }
        };
        _roster = new CountryRoster(_data, _start);
    }
    [TearDown]
    public void TearDown()
    {
        foreach (var skill in _data.Skills.Definitions) Object.DestroyImmediate(skill);
        Object.DestroyImmediate(_data.Skills); Object.DestroyImmediate(_data);
    }

    [Test]
    public void InitialSkillsContainOnlyBasicAttacksAndCurrentWeaponControlsUsability()
    {
        Assert.That(_roster.Soldiers[0].Learned, Is.EquivalentTo(CountryRoster.BasicSkills));
        Assert.That(_roster.CanUse(0, SkillId.Sword_Slash), Is.True);
        Assert.That(_roster.CanUse(0, SkillId.Wand_Bolt), Is.False);
        Assert.That(_roster.CanUse(0, SkillId.Sword_QuickSlash), Is.False);
    }

    [Test]
    public void UniqueWeaponTransferRemovesItFromThePreviousOwnerAndReplacesBonuses()
    {
        Assert.That(_roster.TryEquip(0, 1), Is.True);
        Assert.That(_roster.WeaponOwner(1), Is.Zero);
        Assert.That(_roster.EquippedWeapon(1), Is.EqualTo(-1));
        Assert.That(_roster.WeaponOwner(0), Is.EqualTo(-1));
        Assert.That(_roster.Stats(0).STR, Is.EqualTo(13));
        Assert.That(_roster.Stats(1).STR, Is.EqualTo(11));
        Assert.That(_roster.Soldiers[0].Learned, Has.Member(SkillId.Sword_Slash));
        Assert.That(_roster.CanUse(0, SkillId.Shield_Slash), Is.True);
    }

    [TestCase(1, 3)] [TestCase(2, 4)] [TestCase(3, 5)] [TestCase(4, 6)] [TestCase(5, 7)]
    public void SchoolLevelControlsConcurrentCapacity(int level, int capacity)
    {
        _roster.SetSchoolLevel(FacilityType.WarriorSchool, level);
        Assert.That(_roster.Capacity(0), Is.EqualTo(capacity));
        Assert.That(_roster.TryStartTraining(0, Enumerable.Range(0, capacity).ToArray(), out _), Is.True);
        Assert.That(_roster.TryStartTraining(0, new[] { 7 }, out _), Is.False);
    }

    [Test]
    public void InvalidBatchDoesNotPartiallyStartTraining()
    {
        Assert.That(_roster.TryStartTraining(0, new[] { 0, 0 }, out _), Is.False);
        Assert.That(_roster.TryStartTraining(0, new[] { 0, 1, 2, 3 }, out _), Is.False);
        Assert.That(_roster.TryStartTraining(0, new[] { 0, 99 }, out _), Is.False);
        Assert.That(_roster.TrainingCount(0), Is.Zero);
    }

    [Test]
    public void TrainingBlocksDeploymentAndCannotBeRestartedOrOverwritten()
    {
        _roster.TryStartTraining(0, new[] { 2 }, out _);
        var training = _roster.Soldiers[2].Training;
        Assert.That(_roster.CanDeploy(2), Is.False);
        Assert.That(_roster.TryStartTraining(0, new[] { 2 }, out _), Is.False);
        Assert.That(_roster.Soldiers[2].Training, Is.SameAs(training));
        Assert.That(_roster.TrySetCP(2, 0), Is.False);
    }

    [Test]
    public void TwoCalendarMonthsTeachBothProfessionsAndApplyGrowthOnlyAtCompletion()
    {
        _roster.TryStartTraining(0, new[] { 2 }, out _);
        _roster.AdvanceTo(new CountryDate(1, 2, 28));
        Assert.That(_roster.Soldiers[2].Learned, Has.No.Member(SkillId.Sword_QuickSlash));
        Assert.That(_roster.Soldiers[2].Growth.STR, Is.Zero);
        Assert.That(_roster.CanDeploy(2), Is.False);
        _roster.AdvanceTo(new CountryDate(2, 3, 1));
        Assert.That(_roster.Soldiers[2].Learned, Has.Member(SkillId.Sword_QuickSlash).And.Member(SkillId.Shield_ShoulderGuard));
        Assert.That(_roster.Soldiers[2].Growth.STR, Is.EqualTo(2));
        Assert.That(_roster.CanDeploy(2), Is.True);
        Assert.That(_roster.Soldiers[2].Training, Is.Null);
        _roster.AdvanceTo(new CountryDate(3, 4, 30));
        Assert.That(_roster.Soldiers[2].Growth.STR, Is.EqualTo(2));
    }

    [Test]
    public void StartedTrainingKeepsItsLevelAndRewardsAfterSchoolUpgradeAndWeaponChange()
    {
        _roster.TryStartTraining(0, new[] { 0 }, out _);
        _roster.SetSchoolLevel(FacilityType.WarriorSchool, 5);
        _data.Schools[0].Gains = new CountrySoldierStats { STR = 99 };
        _roster.TryEquip(0, 2);
        _roster.AdvanceTo(_start.AddMonths(2));
        Assert.That(_roster.Soldiers[0].Growth.STR, Is.EqualTo(2));
        Assert.That(_roster.Soldiers[0].Learned, Has.No.Member(SkillId.Sword_StrongSlash));
        Assert.That(_roster.Soldiers[0].Learned, Has.Member(SkillId.Shield_ShoulderGuard));
    }

    [Test]
    public void MissingSkillsRemainUnlearnedAndRepeatedTrainingDoesNotDuplicateSkills()
    {
        _roster.SetSchoolLevel(FacilityType.WarriorSchool, 5);
        for (int i = 1; i <= 2; i++)
        { _roster.TryStartTraining(0, new[] { 0 }, out _); _roster.AdvanceTo(_start.AddMonths(i * 2)); }
        Assert.That(_roster.Soldiers[0].Learned.Count, Is.EqualTo(11));
        Assert.That(_roster.Soldiers[0].Learned, Has.No.Member(SkillId.None));
    }

    [Test]
    public void RevivalUsesOneCalendarMonthAndRestoresEachSoldiersOwnInitialState()
    {
        _roster.TryStartTraining(0, new[] { 0, 1 }, out _);
        _roster.AdvanceTo(_start.AddMonths(2));
        _roster.AdvanceTo(new CountryDate(2, 3, 31));
        Assert.That(_roster.TrySetCP(0, 0), Is.True);
        Assert.That(_roster.CanDeploy(0), Is.False);
        Assert.That(_roster.TryStartTraining(0, new[] { 0 }, out _), Is.False);
        Assert.That(_roster.Soldiers[0].RevivalDue.Value.ToString(), Is.EqualTo("4月30日"));
        Assert.That(_roster.TrySetCP(0, 100), Is.False);
        _roster.AdvanceTo(new CountryDate(3, 4, 29));
        Assert.That(_roster.Soldiers[0].CP, Is.Zero);
        _roster.AdvanceTo(new CountryDate(3, 4, 30));
        Assert.That(_roster.Soldiers[0].CP, Is.EqualTo(100));
        Assert.That(_roster.Soldiers[0].Growth.STR, Is.Zero);
        Assert.That(_roster.Soldiers[0].Learned, Is.EquivalentTo(CountryRoster.BasicSkills));
        Assert.That(_roster.Stats(0).HP, Is.EqualTo(100));
        Assert.That(_roster.Stats(1).HP, Is.EqualTo(106));
        Assert.That(_roster.CanDeploy(0), Is.True);
        Assert.That(_roster.EquippedWeapon(0), Is.Zero);
    }

    [TestCase(0, 1, 31, 1, 2, 28)]
    [TestCase(0, 1, 31, 2, 3, 31)]
    [TestCase(11, 12, 31, 2, 2, 28)]
    public void CalendarMonthDeadlineClampsDayAndCrossesYears(int index, int month, int day, int count, int expectedMonth, int expectedDay)
    {
        var due = new CountryDate(index, month, day).AddMonths(count);
        Assert.That((due.MonthIndex, due.Month, due.Day), Is.EqualTo((index + count, expectedMonth, expectedDay)));
    }

    [TestCase(0, 1, 1, 0, 1)]
    [TestCase(0, 1, 2, 1, 2)]
    [TestCase(0, 1, 31, 1, 2)]
    [TestCase(1, 2, 28, 2, 3)]
    [TestCase(11, 12, 1, 11, 12)]
    [TestCase(11, 12, 31, 12, 1)]
    public void GovernmentPlansApplyThisMonthOnlyOnTheFirst(int index, int month, int day, int expectedIndex, int expectedMonth)
    {
        var start = new CountryDate(index, month, day).PlanningStart;
        Assert.That((start.MonthIndex, start.Month, start.Day), Is.EqualTo((expectedIndex, expectedMonth, 1)));
    }

    [Test]
    public void MidMonthTrainingReservesCapacityAndAllowsDeploymentUntilNextMonth()
    {
        _roster.AdvanceTo(new CountryDate(0, 1, 2));
        Assert.That(_roster.TryStartTraining(0, new[] { 0, 1, 2 }, out _), Is.True);
        var training = _roster.Soldiers[0].Training;
        Assert.That(training.Start.ToString(), Is.EqualTo("2月1日"));
        Assert.That(training.Due.ToString(), Is.EqualTo("4月1日"));
        Assert.That(_roster.CanDeploy(0), Is.True);
        Assert.That(_roster.CanTrain(0), Is.False);
        Assert.That(_roster.TryStartTraining(0, new[] { 3 }, out _), Is.False);
        _roster.AdvanceTo(new CountryDate(0, 1, 31));
        Assert.That(training.Progress(_roster.Today), Is.Zero);
        Assert.That(_roster.CanDeploy(0), Is.True);
        _roster.AdvanceTo(new CountryDate(1, 2, 1));
        Assert.That(_roster.IsTraining(0), Is.True);
        Assert.That(_roster.CanDeploy(0), Is.False);
        _roster.AdvanceTo(new CountryDate(2, 3, 31));
        Assert.That(_roster.Soldiers[0].Growth.STR, Is.Zero);
        _roster.AdvanceTo(new CountryDate(3, 4, 1));
        Assert.That(_roster.Soldiers[0].Growth.STR, Is.EqualTo(2));
        Assert.That(_roster.Soldiers[0].Learned, Has.Member(SkillId.Sword_QuickSlash));
        Assert.That(_roster.CanDeploy(0), Is.True);
    }

    [Test]
    public void SpendingCPBeforeTrainingPreservesTheReservationUntilCPReachesZero()
    {
        _roster.AdvanceTo(new CountryDate(0, 1, 2));
        _roster.TryStartTraining(0, new[] { 0 }, out _);
        var training = _roster.Soldiers[0].Training;
        Assert.That(_roster.TrySetCP(0, 50), Is.True);
        Assert.That(_roster.Soldiers[0].Training, Is.SameAs(training));
        Assert.That(_roster.CanDeploy(0), Is.True);
        Assert.That(_roster.TrySetCP(0, 0), Is.True);
        Assert.That(_roster.Soldiers[0].Training, Is.Null);
        Assert.That(_roster.TrainingCount(0), Is.Zero);
        Assert.That(_roster.Soldiers[0].RevivalDue.Value.ToString(), Is.EqualTo("2月2日"));
        Assert.That(_roster.CanDeploy(0), Is.False);
        _roster.AdvanceTo(new CountryDate(3, 4, 1));
        Assert.That(_roster.Soldiers[0].Growth.STR, Is.Zero);
        Assert.That(_roster.Soldiers[0].Learned, Is.EquivalentTo(CountryRoster.BasicSkills));
    }

    [Test]
    public void DuplicateInitialEquipmentIsRejected()
    {
        _data.Soldiers[1].InitialWeaponId = "w0";
        Assert.Throws<ArgumentException>(() => new CountryRoster(_data, _start));
    }

    [Test]
    public void ASecondSchoolCannotTakeASoldierWhoIsAlreadyTraining()
    {
        _data.Schools = new[] { _data.Schools[0], new CountrySchoolDefinition { Name = "魔法学校", Facility = FacilityType.MagicSchool } };
        _roster = new CountryRoster(_data, _start);
        _roster.TryStartTraining(0, new[] { 0 }, out _);
        Assert.That(_roster.TryStartTraining(1, new[] { 1, 0 }, out _), Is.False);
        Assert.That(_roster.Soldiers[1].Training, Is.Null);
        Assert.That(_roster.Soldiers[0].Training.School, Is.Zero);
    }

    [Test]
    public void UnconfiguredGrowthDoesNotInventStatsButStillTeachesConfiguredSkills()
    {
        _data.Schools[0].Gains = default;
        int initial = _roster.Stats(0).STR;
        _roster.TryStartTraining(0, new[] { 0 }, out _);
        _roster.AdvanceTo(_start.AddMonths(2));
        Assert.That(_roster.Stats(0).STR, Is.EqualTo(initial));
        Assert.That(_roster.Soldiers[0].Learned, Has.Member(SkillId.Sword_QuickSlash));
    }

    [Test]
    public void ExistingEscapedSkillNameIsReadableWithoutChangingTheCombatDefinition()
    {
        _data.Skills.TryGetDefinition(SkillId.Grimoire_StrDebuff, out var skill);
        const string legacy = @"STR\u30C7\u30D0\u30D5";
        skill.ConfigureForTests(SkillId.Grimoire_StrDebuff, WeaponKind.Grimoire, legacy);
        Assert.That(_data.SkillName(SkillId.Grimoire_StrDebuff), Is.EqualTo("STRデバフ"));
        Assert.That(skill.DisplayName, Is.EqualTo(legacy));
    }
}
