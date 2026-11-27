using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class CountryDefenseTests
{
    private CountryWorldMapData _map;
    private CountryRosterData _data;
    private CountryRoster _roster;
    private CountryExpeditionState _world;
    private CountryDefenseState _defense;
    private readonly CountryDate _start = new(0, 1, 1);

    [SetUp]
    public void SetUp()
    {
        _map = ScriptableObject.CreateInstance<CountryWorldMapData>();
        _map.RaidMinDays = _map.RaidMaxDays = 2;
        _map.Nations = new[] { new CountryWorldNation(), new CountryWorldNation() };
        _map.Regions = Enumerable.Range(0, 2).Select(i => new CountryWorldRegion {
            Id = $"r{i}", Name = $"Region{i}", InitialOwner = i, Center = new Vector2(i + .5f, .5f),
            Polygon = new[] { new Vector2(i, 0), new Vector2(i + 1, 0), new Vector2(i + 1, 1), new Vector2(i, 1) }
        }).ToArray();
        _data = ScriptableObject.CreateInstance<CountryRosterData>();
        _data.Soldiers = Enumerable.Range(0, 3).Select(i => new CountrySoldierDefinition { Id = $"s{i}", Name = $"Soldier{i}", MaxCP = 100, InitialWeaponId = $"w{i}" }).ToArray();
        _data.Weapons = Enumerable.Range(0, 3).Select(i => new CountryWeaponDefinition { Id = $"w{i}", Kind = WeaponKind.Sword }).ToArray();
        _data.Schools = new[] { FacilityType.WarriorSchool, FacilityType.MagicSchool, FacilityType.Church }
            .Select(facility => new CountrySchoolDefinition { Name = facility.ToString() }).ToArray();
        _roster = new CountryRoster(_data, _start); _world = new CountryExpeditionState(_map, _roster, _start);
        _defense = new CountryDefenseState(_world, _roster, _start);
    }
    [TearDown] public void TearDown() { Object.DestroyImmediate(_map); Object.DestroyImmediate(_data); }
    private void Advance(CountryDate date) { _world.AdvanceTo(date); _roster.AdvanceTo(date); _defense.AdvanceTo(date); }

    [Test]
    public void RegistrationRequiresDistinctSoldiersAndWeaponsAndRejectsPartialChanges()
    {
        Assert.That(_defense.TryRegister(new[] { 0 }, new[] { 0 }, out _), Is.True);
        foreach (var pair in new[] { (new[]{0},new[]{-1}), (new[]{0,1},new[]{0,0}), (new[]{0,0},new[]{0,1}), (new[]{3},new[]{1}), (new[]{0},Array.Empty<int>()) })
            Assert.That(_defense.TryRegister(pair.Item1, pair.Item2, out _), Is.False);
        Assert.That(_defense.Members, Is.EqualTo(new[] { 0 })); Assert.That(_defense.Weapons, Is.EqualTo(new[] { 0 }));
        Assert.That(_roster.EquippedWeapon(0), Is.Zero); Assert.That(_roster.WeaponOwner(1), Is.EqualTo(1));
    }
    [Test]
    public void RegistrationCopiesInputsAndCanBeCleared()
    {
        int[] members = { 0 }, weapons = { 0 }; _defense.TryRegister(members, weapons, out _);
        members[0] = weapons[0] = 2;
        Assert.That(_defense.Members[0], Is.Zero); Assert.That(_defense.Weapons[0], Is.Zero);
        Assert.That(_roster.TryEquip(0, 1), Is.True);
        Assert.That(_defense.Members[0], Is.Zero); Assert.That(_defense.Weapons[0], Is.EqualTo(1));
        Assert.That(_defense.TryRegister(Array.Empty<int>(), Array.Empty<int>(), out _), Is.True);
        Assert.That(_defense.Members, Is.Empty);
    }
    [TestCase(0)] [TestCase(1)] [TestCase(2)]
    public void ActiveTrainingAtEverySchoolBlocksRegistrationAndDefense(int school)
    {
        Assert.That(_defense.TryRegister(new[] { 0 }, new[] { 0 }, out _), Is.True);
        Assert.That(_roster.TryStartTraining(school, new[] { 0 }, out _), Is.True);
        Assert.That(_roster.IsTraining(0), Is.True);
        Assert.That(_roster.CanDeploy(0), Is.False);
        Assert.That(_defense.TryRegister(new[] { 0 }, new[] { 0 }, out _), Is.False);
        Advance(_start.AddDays(2));
        Assert.That(_defense.ResolveBattle(_defense.Pending, _defense.Members, _defense.Weapons), Is.False);
        Assert.That(_defense.Members, Is.EqualTo(new[] { 0 }));
    }
    [TestCase(0)] [TestCase(1)] [TestCase(2)]
    public void ReservedTrainingAllowsRegistrationUntilTrainingStarts(int school)
    {
        Advance(_start.AddDays(1));
        Assert.That(_roster.TryStartTraining(school, new[] { 0 }, out _), Is.True);
        Assert.That(_roster.IsTraining(0), Is.False);
        Assert.That(_defense.TryRegister(new[] { 0 }, new[] { 0 }, out _), Is.True);
        Advance(_start.AddMonths(1));
        Assert.That(_roster.IsTraining(0), Is.True);
        Assert.That(_defense.TryRegister(new[] { 0 }, new[] { 0 }, out _), Is.False);
    }
    [Test]
    public void AdvancingTimeRaisesOnePendingRaidOnOwnedLand()
    {
        int notices = 0; _defense.RaidStarted += () => notices++;
        Advance(_start.AddDays(1)); Assert.That(_defense.Pending, Is.Null);
        Advance(_start.AddDays(2));
        Assert.That(_defense.Pending.Region, Is.Zero); Assert.That(_defense.Pending.Date.ToString(), Is.EqualTo("1月3日"));
        Advance(_start.AddDays(2)); Advance(_start.AddDays(10)); Assert.That(notices, Is.EqualTo(1));
    }
    [Test]
    public void DisabledRaidsNeverAccumulateAndEnablingStartsANewInterval()
    {
        _defense.SetEnabled(false); Advance(_start.AddDays(20)); Assert.That(_defense.Pending, Is.Null);
        _defense.SetEnabled(true);
        Assert.That(_defense.NextRaid.ToString(), Is.EqualTo("1月23日"));
        Advance(_start.AddDays(21)); Assert.That(_defense.Pending, Is.Null);
        Advance(_start.AddDays(22)); Assert.That(_defense.Pending, Is.Not.Null);
    }
    [Test]
    public void MonthStartRemainsReservedForMonthlyReports()
    {
        _defense = new CountryDefenseState(_world, _roster, new CountryDate(0, 1, 30));
        Assert.That(_defense.NextRaid.ToString(), Is.EqualTo("2月2日"));
        Assert.That(_defense.AdvanceTo(new CountryDate(1, 2, 1)), Is.False);
        Assert.That(_defense.AdvanceTo(new CountryDate(1, 2, 2)), Is.True);
    }
    [Test]
    public void RaidEditsReplaceTheNextDefaultAndBattleConsumesOnlyParticipantsCpOnce()
    {
        _map.BattleCPCost = 23;
        _map.DefenseBattleCPCost = 12;
        _defense.TryRegister(new[] { 0 }, new[] { 0 }, out _); Advance(_start.AddDays(2));
        _roster.TryEquip(1, -1); _roster.TryEquip(1, 2); _roster.TryEquip(2, 1);
        Assert.That(_defense.ResolveBattle(_defense.Pending, new[] { 1, 2 }, new[] { 2, 1 }), Is.True);
        Assert.That(_defense.Members, Is.EqualTo(new[] { 1, 2 })); Assert.That(_defense.Weapons, Is.EqualTo(new[] { 2, 1 }));
        Assert.That(_roster.Soldiers.Select(s => s.CP), Is.EqualTo(new[] { 100, 88, 88 }));
        Assert.That(_roster.EquippedWeapon(1), Is.EqualTo(2)); Assert.That(_roster.EquippedWeapon(2), Is.EqualTo(1));
        Assert.That(_defense.ResolveBattle(_defense.Pending, new[] { 1, 2 }, new[] { 2, 1 }), Is.False);
        Assert.That(_roster.Soldiers[1].CP, Is.EqualTo(88)); Assert.That(_defense.Pending, Is.Null);
        Advance(_start.AddDays(4)); Assert.That(_defense.Pending, Is.Not.Null);
    }
    [Test]
    public void UnavailableRegisteredMembersMustBeEditedBeforeDefense()
    {
        _defense.TryRegister(new[] { 0 }, new[] { 0 }, out _);
        _roster.TryStartTraining(0, new[] { 0 }, out _); Advance(_start.AddDays(2));
        Assert.That(_defense.ResolveBattle(_defense.Pending, _defense.Members, _defense.Weapons), Is.False);
        Assert.That(_defense.Pending, Is.Not.Null); Assert.That(_defense.Members[0], Is.Zero);
        Assert.That(_defense.ResolveBattle(_defense.Pending, new[] { 1 }, new[] { 1 }), Is.True);
    }
    [Test]
    public void TravelingSoldiersAndCarriedWeaponsCannotBeRegistered()
    {
        _world.TryDepart(1, new[] { 0 }, new[] { 0 }, out _);
        Assert.That(_defense.TryRegister(new[] { 0 }, new[] { 1 }, out _), Is.False);
        Assert.That(_defense.TryRegister(new[] { 1 }, new[] { 0 }, out _), Is.False);
        Assert.That(_defense.Members, Is.Empty);
    }
    [Test]
    public void CpIsClampedAtZeroAndRevivalCancelsReservedTraining()
    {
        Advance(_start.AddDays(1)); _roster.TryStartTraining(0, new[] { 0 }, out _); _roster.TrySetCP(0, 10);
        Advance(_start.AddDays(2)); Assert.That(_defense.ResolveBattle(_defense.Pending, new[] { 0 }, new[] { 0 }), Is.True);
        Assert.That(_roster.Soldiers[0].CP, Is.Zero); Assert.That(_roster.Soldiers[0].Training, Is.Null);
        Assert.That(_roster.Soldiers[0].RevivalDue.Value.ToString(), Is.EqualTo("2月3日"));
        _roster.AdvanceTo(new CountryDate(1, 2, 3)); Assert.That(_roster.Soldiers[0].CP, Is.EqualTo(100));
    }
    [Test]
    public void EmptyFormationCannotFightAndRaidRemainsPending()
    {
        Advance(_start.AddDays(2)); Assert.That(_defense.ResolveBattle(_defense.Pending, Array.Empty<int>(), Array.Empty<int>()), Is.False);
        Assert.That(_defense.Pending, Is.Not.Null);
        Assert.That(_defense.Members, Is.Empty);
        Assert.That(_roster.Soldiers.All(s => s.CP == 100), Is.True);
    }

    [Test]
    public void RaidWithNobodyDeployableResolvesAsDefeatAndSchedulesTheNextRaid()
    {
        for (int soldier = 0; soldier < _roster.Soldiers.Count; soldier++)
            Assert.That(_roster.TrySetCP(soldier, 0), Is.True);
        int opened = 0;
        var defeats = new System.Collections.Generic.List<CountryRaid>();
        _defense.RaidStarted += () => opened++;
        _defense.RaidDefeated += defeats.Add;
        var date = _defense.NextRaid;

        Assert.That(_defense.AdvanceTo(date), Is.True);
        Assert.That(_defense.Pending, Is.Null);
        Assert.That(_defense.NextRaid.CompareTo(date), Is.GreaterThan(0));
        Assert.That(defeats.Single().Date, Is.EqualTo(date));
        Assert.That(opened, Is.Zero);
        Assert.That(_roster.Durability(0), Is.EqualTo(_data.Weapons[0].MaxDurability));
        Assert.That(_defense.AdvanceTo(date), Is.False);
        Assert.That(defeats.Count, Is.EqualTo(1));
    }

    [Test]
    public void ADeployableUnarmedSoldierCanDefendIfAWeaponCanBeEquipped()
    {
        Assert.That(_roster.TryEquip(0, -1), Is.True);
        Assert.That(_roster.TrySetCP(1, 0), Is.True);
        Assert.That(_roster.TrySetCP(2, 0), Is.True);
        Advance(_defense.NextRaid);
        Assert.That(_defense.Pending, Is.Not.Null);
        Assert.That(_roster.TryEquip(0, 0), Is.True);
        Assert.That(_defense.ResolveBattle(_defense.Pending, new[] { 0 }, new[] { 0 }), Is.True);
    }

    [Test]
    public void ADeployableSoldierWithoutAnyUsableWeaponCannotDefend()
    {
        foreach (var weapon in _data.Weapons) weapon.MaxDurability = 1;
        _roster = new CountryRoster(_data, _start);
        _world = new CountryExpeditionState(_map, _roster, _start);
        _defense = new CountryDefenseState(_world, _roster, _start);
        _roster.ConsumeBattleCP(new[] { 0, 1, 2 }, 1, _start);
        CountryRaid defeat = null;
        _defense.RaidDefeated += raid => defeat = raid;

        Advance(_defense.NextRaid);

        Assert.That(_roster.CanDeploy(0), Is.True);
        Assert.That(_defense.Pending, Is.Null);
        Assert.That(defeat, Is.Not.Null);
    }
}
