using NUnit.Framework;
using UnityEngine;

public sealed class CountryGovernmentDraftTests
{
    private CountryGovernmentData _data;
    private CountryGovernmentDraft _draft;

    [SetUp]
    public void SetUp()
    {
        _data = ScriptableObject.CreateInstance<CountryGovernmentData>();
        _data.Facilities = new[] {
            new CountryGovernmentFacility { Name = "戦士学校", InitialLevel = 1, TrainingSchool = true, Minimum = 100, Funding = 200,
                Upgrade = new CountryGovernmentCost { Money = 550, Wood = 100, Ore = 80, Months = 1 } },
            new CountryGovernmentFacility { Name = "教会", InitialLevel = 1, TrainingSchool = true, Minimum = 60, Funding = 100 },
            new CountryGovernmentFacility { Name = "精霊使役学校", InitialLevel = 1, Minimum = 90, Funding = 150 },
            new CountryGovernmentFacility { Name = "図書館", InitialLevel = 0, Minimum = 80,
                Construction = new CountryGovernmentCost { Money = 420, Wood = 120, Ore = 60, Months = 2 } }
        };
        _draft = new CountryGovernmentDraft(_data);
        _draft.UpdateResources(1700, 850, 1540, 970);
    }

    [TearDown] public void TearDown() => Object.DestroyImmediate(_data);

    [Test]
    public void FundingPercentagesUseTaxOrTaxAndTreasuryWithoutCountingReceivedTaxTwice()
    {
        Assert.That(_draft.FundingBasis, Is.EqualTo(850));
        Assert.That(_draft.SetUseTreasury(true), Is.True);
        Assert.That(_draft.FundingBasis, Is.EqualTo(1700));
        _draft.UpdateResources(850, 850, 1200, 800, taxIncluded: false);
        Assert.That(_draft.FundingBasis, Is.EqualTo(1700));
        _draft.UpdateResources(0, 850, 1200, 800, taxIncluded: false);
        Assert.That(_draft.FundingBasis, Is.EqualTo(850));
    }

    [Test]
    public void FundingAndConstructionPlansDoNotShrinkThePercentageBasis()
    {
        _draft.SetFunding(0, 300);
        Assert.That(_draft.FundingBasis, Is.EqualTo(850));
        _draft.SetUseTreasury(true);
        Assert.That(_draft.PlanConstruction(0, true), Is.True);
        Assert.That(_draft.FundingBasis, Is.EqualTo(1700));
        Assert.That(_draft.MaximumFunding(0), Is.LessThan(_draft.FundingBasis));
    }

    [Test]
    public void AMonthWithoutTaxHasAZeroBasisUnlessTreasuryIsEnabled()
    {
        _draft.UpdateResources(1000, 0, 1200, 800);
        Assert.That(_draft.FundingBasis, Is.Zero);
        _draft.SetUseTreasury(true);
        Assert.That(_draft.FundingBasis, Is.EqualTo(1000));
    }

    [Test]
    public void TheInitialUnreceivedTaxCanFundTheDraft()
    {
        _draft.UpdateResources(0, 850, 1200, 800, taxIncluded: false);
        Assert.That(_draft.CanConfirmFunding, Is.True);
        Assert.That(_draft.TreasuryForecast, Is.EqualTo(400));
        Assert.That(_draft.UseTreasury, Is.False);
        Assert.That(_draft.WoodForecast, Is.EqualTo(1200));
        Assert.That(_draft.OreForecast, Is.EqualTo(800));
    }

    [Test]
    public void FundingCanIncreaseAgainAfterBeingReducedToItsMinimum()
    {
        _draft.UpdateResources(0, 850, 1200, 800, taxIncluded: false);
        int maximum = _draft.MaximumFunding(0);
        _draft.SetFunding(0, 100);
        Assert.That(_draft.MaximumFunding(0), Is.EqualTo(maximum));
        _draft.SetFunding(0, 350);
        Assert.That(_draft.Funding(0), Is.EqualTo(350));
        Assert.That(_draft.MaximumFunding(0), Is.EqualTo(maximum));
        Assert.That(_draft.CanConfirmFunding, Is.True);
    }

    [Test]
    public void FundingUsesTheMinimumAsItsLowerBoundAndTaxAsItsDefaultCeiling()
    {
        _draft.SetFunding(0, -100);
        Assert.That(_draft.Funding(0), Is.EqualTo(100));
        _draft.SetFunding(0, int.MaxValue);
        Assert.That(_draft.FundingTotal, Is.EqualTo(850));
        Assert.That(_draft.UseTreasury, Is.False);
        Assert.That(_draft.RemainingTax, Is.Zero);
    }

    [Test]
    public void TreasuryIsNotCreditedAgainWhenPreviewingAlreadyReceivedTax()
    {
        Assert.That(_draft.FundingTotal, Is.EqualTo(450));
        Assert.That(_draft.TreasuryForecast, Is.EqualTo(1250));
        Assert.That(_draft.RemainingTax, Is.EqualTo(400));
    }

    [Test]
    public void TreasuryCannotBeSwitchedOffWhileTheDraftUsesIt()
    {
        Assert.That(_draft.SetUseTreasury(true), Is.True);
        _draft.SetFunding(0, 800);
        Assert.That(_draft.TreasuryUsed, Is.EqualTo(200));
        Assert.That(_draft.SetUseTreasury(false), Is.False);
        Assert.That(_draft.UseTreasury, Is.True);
        _draft.SetFunding(0, 200);
        Assert.That(_draft.SetUseTreasury(false), Is.True);
    }

    [Test]
    public void BuiltAndUnbuiltFacilitiesHaveDisjointConstructionCandidates()
    {
        for (int i = 0; i < _data.Facilities.Length; i++)
            Assert.That(_draft.IsCandidate(i, true), Is.Not.EqualTo(_draft.IsCandidate(i, false)));
        Assert.That(_draft.PlanConstruction(0, false), Is.False);
        Assert.That(_draft.PlanConstruction(3, true), Is.False);
    }

    [Test]
    public void TheSameTypeCannotBePlannedTwiceAndCannotBeUpgradedDuringConstruction()
    {
        Assert.That(_draft.PlanConstruction(3, false), Is.True);
        Assert.That(_draft.PlanConstruction(3, false), Is.False);
        Assert.That(_draft.PlanConstruction(3, true), Is.False);
        Assert.That(_draft.OrderCount, Is.EqualTo(1));
        Assert.That(_draft.TreasuryForecast, Is.EqualTo(830));
        Assert.That(_draft.WoodForecast, Is.EqualTo(1420));
        Assert.That(_draft.OreForecast, Is.EqualTo(910));
    }

    [Test]
    public void InsufficientResourcesCannotAddAnOrder()
    {
        _draft.UpdateResources(450, 850, 0, 970);
        Assert.That(_draft.PlanConstruction(0, true), Is.False);
        Assert.That(_draft.OrderCount, Is.Zero);
    }


    [Test]
    public void GovernmentSpendsAndCompletesEachConfirmedOrderExactlyOnce()
    {
        var go = new GameObject();
        try
        {
            var resources = go.AddComponent<PlayDataManager>();
            resources.SetResources(8500, 1000, 300, 150);
            var draft = resources.GetOrCreateGovernmentDraft(_data).Copy();
            draft.UpdateResources(1000, 850, 300, 150);
            Assert.That(draft.PlanConstruction(3, false), Is.True);
            var today = new CountryDate(0, 1, 2);
            var start = today.PlanningStart;
            resources.ConfirmGovernment(draft, today, start, true, true);
            Assert.That(resources.Treasury, Is.EqualTo(1000));
            Assert.That(resources.GetOrCreateGovernmentDraft(_data).IsCandidate(3, false), Is.False);

            resources.ProcessConstruction(start);
            resources.ProcessConstruction(start);
            resources.ApplyFunding(start);
            resources.ApplyFunding(start);
            Assert.That(resources.Treasury, Is.EqualTo(130));
            Assert.That(resources.Wood, Is.EqualTo(180));
            Assert.That(resources.Ore, Is.EqualTo(90));
            Assert.That(resources.FacilityLevel("図書館"), Is.Zero);
            resources.ProcessConstruction(start.AddMonths(1));
            Assert.That(resources.FacilityLevel("図書館"), Is.Zero);
            resources.ProcessConstruction(start.AddMonths(2));
            resources.ProcessConstruction(start.AddMonths(2));
            Assert.That(resources.FacilityLevel("図書館"), Is.EqualTo(1));
            Assert.That(resources.GovernmentReport.Count, Is.EqualTo(1));
        }
        finally { Object.DestroyImmediate(go); }
    }

    [Test]
    public void GovernmentCancelsAnUnaffordableOrderWithoutPartialSpend()
    {
        var go = new GameObject();
        try
        {
            var resources = go.AddComponent<PlayDataManager>();
            var draft = resources.GetOrCreateGovernmentDraft(_data).Copy();
            draft.UpdateResources(1000, 850, 300, 150);
            Assert.That(draft.PlanConstruction(3, false), Is.True);
            var today = new CountryDate(0, 1, 2);
            var start = today.PlanningStart;
            resources.ConfirmGovernment(draft, today, start, false, true);
            resources.SetResources(8500, 1000, 300, 0);
            resources.ProcessConstruction(start);
            Assert.That(resources.Treasury, Is.EqualTo(1000));
            Assert.That(resources.Wood, Is.EqualTo(300));
            Assert.That(resources.FacilityLevel("図書館"), Is.Zero);
            Assert.That(resources.GetOrCreateGovernmentDraft(_data).IsCandidate(3, false), Is.True);
            Assert.That(resources.GovernmentReport.Count, Is.EqualTo(1));
        }
        finally { Object.DestroyImmediate(go); }
    }

    [Test]
    public void SpiritSchoolCompletionIncreasesCPOnceAndKeepsRevivalOnItsScheduledDay()
    {
        var host = new GameObject();
        var data = ScriptableObject.CreateInstance<CountryRosterData>();
        try
        {
            data.Government = _data;
            data.Soldiers = new[] {
                new CountrySoldierDefinition { Id = "ready" },
                new CountrySoldierDefinition { Id = "reviving" }
            };
            var game = host.AddComponent<PlayDataManager>();
            var today = new CountryDate(0, 1, 1);
            var roster = game.GetOrCreateRoster(data, today);
            Assert.That(roster.TrySetCP(0, 60), Is.True);
            Assert.That(roster.TrySetCP(1, 0), Is.True);
            int spirit = _data.FindFacility(CountryGovernmentData.SpiritSchool);
            _data.Facilities[spirit].Upgrade = new CountryGovernmentCost { Money = 10, Months = 1 };
            game.SetResources(8500, 1000, 300, 150);

            var draft = game.GovernmentDraft.Copy();
            draft.UpdateResources(1000, 850, 300, 150);
            Assert.That(draft.PlanConstruction(spirit, true), Is.True);
            game.ConfirmGovernment(draft, today, today, false, true);
            Assert.That(roster.MaxCP(0), Is.EqualTo(100));
            var complete = today.AddMonths(1);
            game.ProcessConstruction(complete);
            game.ProcessConstruction(complete);
            Assert.That(roster.MaxCP(0), Is.EqualTo(110));
            Assert.That(roster.Soldiers[0].CP, Is.EqualTo(70));
            Assert.That(roster.Soldiers[1].CP, Is.Zero);
            Assert.That(roster.Soldiers[1].RevivalDue, Is.EqualTo(complete));

            roster.AdvanceTo(complete);
            Assert.That(roster.Soldiers[1].CP, Is.EqualTo(110));
            game.SaveFacilityLevelChange(FacilityType.SpiritSchool, 3);
            Assert.That(roster.MaxCP(0), Is.EqualTo(140));
            Assert.That(roster.Soldiers[0].CP, Is.EqualTo(100));
            Assert.That(roster.Soldiers[1].CP, Is.EqualTo(140));
        }
        finally
        {
            Object.DestroyImmediate(host);
            Object.DestroyImmediate(data);
        }
    }
}
