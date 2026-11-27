using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class CountryExpeditionTests
{
    private CountryWorldMapData _map;
    private CountryRosterData _data;
    private CountryRoster _roster;
    private CountryExpeditionState _state;
    private readonly CountryDate _start = new(0, 1, 1);

    [SetUp]
    public void SetUp()
    {
        _map = ScriptableObject.CreateInstance<CountryWorldMapData>();
        _map.BattleCPCost = 20;
        _map.Nations = Enumerable.Range(0, 4).Select(i => new CountryWorldNation { Name = $"Nation{i}", Color = Color.white }).ToArray();
        _map.Regions = Enumerable.Range(0, 4).Select(i => {
            float x = i % 2 * .5f, y = i / 2 * .5f;
            return new CountryWorldRegion { Id = $"r{i}", Name = $"Region{i}", InitialOwner = i == 0 ? 0 : 1, Center = new Vector2(x + .25f, y + .25f),
                Polygon = new[] { new Vector2(x,y), new Vector2(x+.5f,y), new Vector2(x+.5f,y+.5f), new Vector2(x,y+.5f) } };
        }).ToArray();
        _data = ScriptableObject.CreateInstance<CountryRosterData>();
        _data.Soldiers = Enumerable.Range(0, 26).Select(i => new CountrySoldierDefinition { Id = $"s{i}", Name = $"Soldier{i}", MaxCP = 100, InitialWeaponId = $"w{i}" }).ToArray();
        _data.Weapons = Enumerable.Range(0, 26).Select(i => new CountryWeaponDefinition { Id = $"w{i}", Name = $"Weapon{i}", Kind = (WeaponKind)(i % 6 + 1), Bonus = new CountrySoldierStats { STR = i + 1 } }).ToArray();
        _data.Schools = new[] { new CountrySchoolDefinition { Name = "School" } };
        _roster = new CountryRoster(_data, _start); _state = new CountryExpeditionState(_map, _roster, _start);
    }
    [TearDown] public void TearDown() { Object.DestroyImmediate(_map); Object.DestroyImmediate(_data); }
    private void Advance(CountryDate date)
    {
        _state.AdvanceTo(date);
        while (_state.Arrived is { } trip) Assert.That(_state.ResolveBattle(trip, true), Is.True);
        _roster.AdvanceTo(date);
    }

    [Test]
    public void ExpeditionDetailsShowThePartyWithoutTemporaryBattleCost()
    {
        var trip = new CountryExpeditionTrip(1, _start, _map.TravelDays(1), new[] { 0 }, new[] { 0 });
        string details = CountryExpeditionRow.Details(_state, trip, _roster, false);
        Assert.That(details, Does.Contain("Region1").And.Contain("Soldier0").And.Contain("Weapon0"));
        Assert.That(details, Does.Not.Contain("CP"));
    }
    [Test]
    public void BordersAllowNeighborsButACornerDoesNot()
    {
        Assert.That(_map.Adjacent(0, 1), Is.True); Assert.That(_map.Adjacent(1, 0), Is.True);
        Assert.That(_map.Adjacent(0, 2), Is.True); Assert.That(_map.Adjacent(0, 3), Is.False);
        Assert.That(_state.CanTarget(0), Is.False); Assert.That(_state.CanTarget(1), Is.True);
        Assert.That(_state.CanTarget(2), Is.True); Assert.That(_state.CanTarget(3), Is.False);
    }
    [Test]
    public void TravelTimeDependsOnDistanceAndNotOwnership()
    {
        int near = _map.TravelDays(1), far = _map.TravelDays(3);
        Assert.That(near, Is.GreaterThan(0)); Assert.That(far, Is.GreaterThan(near));
        _state.TryDepart(1, new[] { 0 }, new[] { 0 }, out _); Advance(_state.Active[0].Arrival);
        Assert.That(_map.TravelDays(1), Is.EqualTo(near)); Assert.That(_map.TravelDays(3), Is.EqualTo(far));
    }
    [Test]
    public void EquipmentChangesIndependentlyOfPartyMembership()
    {
        var draft = new CountryExpeditionDraft(_roster);
        draft.Toggle(0);
        Assert.That(_roster.TryEquip(1, 0), Is.True);
        Assert.That(draft.Contains(1), Is.False);
        Assert.That(draft.Weapon(0), Is.EqualTo(-1));
        Assert.That(draft.Weapon(1), Is.Zero);
        draft.Toggle(1); draft.Toggle(1);
        Assert.That(_roster.EquippedWeapon(1), Is.Zero);
        Assert.That(_state.TryDepart(1, new[] { 0 }, new[] { 0 }, out _), Is.False);
    }
    [Test]
    public void InvalidDepartureDoesNotPartiallyChangeEquipmentOrMembership()
    {
        int notices = 0; _roster.Changed += () => notices++;
        foreach (var choice in new[] { (3,new[]{0},new[]{1}), (1,new[]{0,0},new[]{0,1}), (1,new[]{0,1},new[]{2,2}), (1,new[]{0,26},new[]{0,1}), (1,new[]{0},new[]{-1}) })
            Assert.That(_state.TryDepart(choice.Item1, choice.Item2, choice.Item3, out _), Is.False);
        Assert.That(_state.Active, Is.Empty); Assert.That(notices, Is.Zero);
        Assert.That(Enumerable.Range(0, 26).All(i => _roster.EquippedWeapon(i) == i && _roster.Soldiers[i].Expedition == null), Is.True);
    }
    [Test]
    public void DeparturePreservesEquipmentAndPublishesOnlyTheCompleteLockedParty()
    {
        _roster.TryEquip(0, -1); _roster.TryEquip(0, 1); _roster.TryEquip(1, 0);
        int notices = 0;
        _roster.Changed += () => {
            notices++;
            Assert.That(_state.Active.Count, Is.EqualTo(1));
            Assert.That(_roster.EquippedWeapon(0), Is.EqualTo(1)); Assert.That(_roster.EquippedWeapon(1), Is.Zero);
            Assert.That(_roster.CanDeploy(0), Is.False); Assert.That(_roster.CanDeploy(1), Is.False);
        };
        Assert.That(_state.TryDepart(1, new[] { 0, 1 }, new[] { 1, 0 }, out _), Is.True);
        Assert.That(notices, Is.EqualTo(1));
    }
    [Test]
    public void AllTwentySixCanTravelTogetherButEmptyAndOversizedPartiesCannot()
    {
        Assert.That(_state.TryDepart(1, Array.Empty<int>(), Array.Empty<int>(), out _), Is.False);
        Assert.That(_state.TryDepart(1, Enumerable.Range(0, 27).ToArray(), Enumerable.Range(0, 27).ToArray(), out _), Is.False);
        Assert.That(_state.TryDepart(1, Enumerable.Range(0, 26).ToArray(), Enumerable.Range(0, 26).ToArray(), out _), Is.True);
        Assert.That(_state.Active[0].Members.Count, Is.EqualTo(26));
    }
    [Test]
    public void SeparatePartiesTravelTogetherButDestinationsMembersAndWeaponsCannotOverlap()
    {
        Assert.That(_state.TryDepart(1, new[] { 0 }, new[] { 0 }, out _), Is.True);
        Assert.That(_state.TryDepart(1, new[] { 1 }, new[] { 1 }, out _), Is.False);
        Assert.That(_state.TryDepart(2, new[] { 0 }, new[] { 1 }, out _), Is.False);
        Assert.That(_state.TryDepart(2, new[] { 1 }, new[] { 0 }, out _), Is.False);
        Assert.That(_state.TryDepart(2, new[] { 1 }, new[] { 1 }, out _), Is.True);
        Assert.That(_state.Active.Count, Is.EqualTo(2));
    }
    [Test]
    public void TravelingSoldiersAndCarriedWeaponsRemainLockedUntilReturn()
    {
        _roster.TryEquip(0, 1);
        _state.TryDepart(1, new[] { 0 }, new[] { 1 }, out _);
        var returnDate = _state.Active[0].Return;
        Assert.That(_roster.CanDeploy(0), Is.False); Assert.That(_roster.CanTrain(0), Is.False);
        Assert.That(_roster.TryStartTraining(0, new[] { 0 }, out _), Is.False);
        Assert.That(_roster.TryEquip(0, 2), Is.False); Assert.That(_roster.TryEquip(2, 1), Is.False);
        Advance(returnDate);
        Assert.That(_roster.CanDeploy(0), Is.True); Assert.That(_roster.CanTrain(0), Is.True);
        Assert.That(_roster.TryEquip(2, 1), Is.True);
    }
    [Test]
    public void VictoryAcquiresLandAndOpensItsBorderBeforeThePartyReturns()
    {
        _state.TryDepart(1, new[] { 0 }, new[] { 0 }, out _);
        var trip = _state.Active[0];
        Advance(_start.AddDays(_map.TravelDays(1) - 1)); Assert.That(_state.Owner(1), Is.EqualTo(1));
        Advance(trip.Arrival);
        Assert.That(_state.Owner(1), Is.Zero); Assert.That(trip.Returning, Is.True);
        Assert.That(_state.CanTarget(3), Is.True); Assert.That(_roster.CanDeploy(0), Is.False);
        Advance(trip.Return); Assert.That(_state.Active, Is.Empty); Assert.That(_roster.CanDeploy(0), Is.True);
    }
    [Test]
    public void BattleResolutionConsumesCpOnceAndPreservesStatsAndSkills()
    {
        var skills = _roster.Soldiers[0].Learned.ToArray(); var stats = _roster.Stats(0);
        _state.TryDepart(1, new[] { 0 }, new[] { 0 }, out _);
        var trip = _state.Active[0];
        Advance(_start.AddDays(_map.TravelDays(1) - 1));
        Assert.That(_roster.Soldiers[0].CP, Is.EqualTo(100));
        Advance(trip.Arrival); Assert.That(_roster.Soldiers[0].CP, Is.EqualTo(80));
        Advance(trip.Return); Assert.That(_roster.Soldiers[0].CP, Is.EqualTo(80));
        Assert.That(_roster.Stats(0), Is.EqualTo(stats));
        Assert.That(_roster.Soldiers[0].Learned, Is.EqualTo(skills)); Assert.That(_roster.EquippedWeapon(0), Is.Zero);
    }
    [Test]
    public void ZeroCpInExpeditionUsesArrivalDateForRevivalAndKeepsEquipmentLockedUntilReturn()
    {
        _roster.TrySetCP(0, 10);
        _state.TryDepart(1, new[] { 0 }, new[] { 0 }, out _);
        var trip = _state.Active[0];
        Advance(trip.Arrival);
        Assert.That(_roster.Soldiers[0].CP, Is.Zero);
        var revival = trip.Arrival.AddMonths(1);
        Assert.That(_roster.Soldiers[0].RevivalDue.Value, Is.EqualTo(revival));
        Assert.That(_roster.TryEquip(1, 0), Is.False);
        Advance(trip.Return); Assert.That(_roster.CanDeploy(0), Is.False);
        Advance(revival); Assert.That(_roster.Soldiers[0].CP, Is.EqualTo(100));
    }
    [Test]
    public void ZeroCpAndActiveTrainingBlockSelection()
    {
        _roster.TrySetCP(0, 0); _roster.TryStartTraining(0, new[] { 1 }, out _);
        Assert.That(_state.CanSelect(0, 1, out _), Is.False); Assert.That(_state.CanSelect(1, 1, out _), Is.False);
    }
    [TestCase(11, true)] [TestCase(12, true)] [TestCase(13, false)]
    public void ReservedTrainingAllowsOnlyReturnsOnOrBeforeItsStart(int departureDay, bool allowed)
    {
        Advance(new CountryDate(0, 1, departureDay)); _roster.TryStartTraining(0, new[] { 0 }, out _);
        Assert.That(_state.CanSelect(0, 1, out _), Is.EqualTo(allowed));
        Assert.That(_state.TryDepart(1, new[] { 0 }, new[] { 0 }, out _), Is.EqualTo(allowed));
    }
    [Test]
    public void ChangingDestinationCanMakeASelectedMemberUnavailableWithoutPreventingRemoval()
    {
        _map.Regions[2].Center = new Vector2(.49f, .99f);
        Advance(new CountryDate(0, 1, 11));
        Assert.That(_roster.TryStartTraining(0, new[] { 0 }, out _), Is.True);
        var draft = new CountryExpeditionDraft(_roster) { Target = 1 };
        Assert.That(_state.CanSelect(0, draft.Target, out _), Is.True);
        draft.Toggle(0);
        draft.Target = 2;
        Assert.That(_state.CanTarget(draft.Target), Is.True);
        Assert.That(_state.CanSelect(0, draft.Target, out _), Is.False);
        Assert.That(_state.ValidateDeparture(draft.Target, draft.Members, draft.Weapons(), out _), Is.False);
        draft.Toggle(0);
        Assert.That(draft.Members, Is.Empty);
    }
    [Test]
    public void ReturningOnTrainingStartReleasesTheSoldierBeforeTrainingBegins()
    {
        Advance(new CountryDate(0, 1, 12)); _roster.TryStartTraining(0, new[] { 0 }, out _);
        _state.TryDepart(1, new[] { 0 }, new[] { 0 }, out _); Advance(new CountryDate(1, 2, 1));
        Assert.That(_roster.Soldiers[0].Expedition, Is.Null); Assert.That(_roster.IsTraining(0), Is.True);
        Assert.That(_roster.CanDeploy(0), Is.False); Assert.That(_state.Returned[0].ReportMonth, Is.EqualTo(1));
    }
    [TestCase(11,1)] [TestCase(12,1)] [TestCase(13,2)]
    public void ReturnReportsAppearAtNextMonthOrOnTheReturnDaysMonthStart(int departureDay, int reportMonth)
    {
        Advance(new CountryDate(0, 1, departureDay)); _state.TryDepart(1, new[] { 0 }, new[] { 0 }, out _);
        Advance(_state.Active[0].Return);
        Assert.That(_state.Returned[0].ReportMonth, Is.EqualTo(reportMonth));
    }
    [Test]
    public void RepeatedAndLargeDateAdvancesDoNotDuplicateConquestOrReports()
    {
        _state.TryDepart(1, new[] { 0 }, new[] { 0 }, out _);
        var returnDate = _state.Active[0].Return;
        Advance(new CountryDate(3, 4, 1)); Advance(new CountryDate(3, 4, 1)); Advance(new CountryDate(4, 5, 1));
        Assert.That(_state.Owner(1), Is.Zero); Assert.That(_state.Active, Is.Empty); Assert.That(_state.Returned.Count, Is.EqualTo(1));
        Assert.That(_state.Returned[0].Return, Is.EqualTo(returnDate));
        Assert.That(_state.Returned[0].ReportMonth, Is.EqualTo(returnDate.MonthIndex + (returnDate.Day == 1 ? 0 : 1)));
    }
    [Test]
    public void DepartureRecordsDoNotKeepMutableCallerArrays()
    {
        int[] members = { 0 }, weapons = { 0 }; _state.TryDepart(1, members, weapons, out _); members[0] = weapons[0] = 2;
        Assert.That(_state.Active[0].Members[0], Is.Zero); Assert.That(_state.Active[0].Weapons[0], Is.Zero);
    }
    [TestCase(0,1,31,1,1,2,1)] [TestCase(1,2,28,1,2,3,1)]
    [TestCase(11,12,20,20,12,1,9)] [TestCase(1,2,1,60,3,4,2)] [TestCase(0,1,1,0,0,1,1)]
    public void DayDurationsUseTheExistingCalendarAcrossMonthsAndYears(int index, int month, int day, int days, int dueIndex, int dueMonth, int dueDay)
    {
        var due = new CountryDate(index,month,day).AddDays(days);
        Assert.That((due.MonthIndex,due.Month,due.Day), Is.EqualTo((dueIndex,dueMonth,dueDay)));
    }
    [Test] public void NegativeDayDurationsAreRejected() => Assert.Throws<ArgumentOutOfRangeException>(() => _start.AddDays(-1));

    [Test]
    public void BrokenWeaponStaysWithTheExpeditionUntilReturnAndReportsOnlyOnce()
    {
        _data.Weapons[0].MaxDurability = 1;
        _roster = new CountryRoster(_data, _start);
        _state = new CountryExpeditionState(_map, _roster, _start);
        Assert.That(_roster.TrySetCP(0, 1), Is.True);
        Assert.That(_state.TryDepart(1, new[] { 0 }, new[] { 0 }, out _), Is.True);
        var trip = _state.Active[0];

        Advance(trip.Arrival);
        Assert.That(_roster.Durability(0), Is.Zero);
        Assert.That(_roster.Soldiers[0].CP, Is.Zero);
        Assert.That(_roster.WeaponOwner(0), Is.Zero);
        Assert.That(_roster.TryEquip(1, 0), Is.False);
        Assert.That(_roster.TakeBattleNotices().Count, Is.EqualTo(2));
        Advance(trip.Arrival);
        Assert.That(_roster.TakeBattleNotices(), Is.Empty);

        Advance(trip.Return);
        Assert.That(_roster.WeaponOwner(0), Is.EqualTo(-1));
        Assert.That(_roster.TryEquip(1, 0), Is.False);
        Assert.That(_roster.TakeBattleNotices(), Is.Empty);
    }


    [Test]
    public void ArrivalAwaitsTheBattleAndDefeatReturnsWithoutAcquiringLand()
    {
        Assert.That(_state.TryDepart(1, new[] { 0 }, new[] { 0 }, out _), Is.True);
        var trip = _state.Active[0];
        Assert.That(_state.ResolveBattle(trip, true), Is.False);
        _state.AdvanceTo(trip.Arrival);
        Assert.That(_state.Arrived, Is.SameAs(trip));
        Assert.That(_state.Owner(1), Is.EqualTo(1));
        Assert.That(_roster.Soldiers[0].CP, Is.EqualTo(100));

        Assert.That(_state.ResolveBattle(trip, false), Is.True);
        Assert.That(_state.ResolveBattle(trip, true), Is.False);
        Assert.That(trip.Won, Is.False);
        Assert.That(_state.Owner(1), Is.EqualTo(1));
        Assert.That(_roster.Soldiers[0].CP, Is.EqualTo(80));
        Assert.That(_roster.CanDeploy(0), Is.False);
        _state.AdvanceTo(trip.Return);
        Assert.That(_state.Returned.Single(), Is.SameAs(trip));
        Assert.That(_roster.CanDeploy(0), Is.True);
    }
}
