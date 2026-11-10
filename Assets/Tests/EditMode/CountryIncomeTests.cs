using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public sealed class CountryIncomeTests
{
    private GameObject _root;
    private PlayDataManager _resources;

    [SetUp]
    public void SetUp()
    {
        _root = new GameObject("Income test");
        _resources = _root.AddComponent<PlayDataManager>();
    }

    [TearDown]
    public void TearDown() => UnityEngine.Object.DestroyImmediate(_root);

    private int[] Stocks() => new[] { _resources.Treasury, _resources.Wood, _resources.Ore };
    private decimal[] Incomes() => new[] { _resources.MonthlyTaxIncome, _resources.MonthlyWoodIncome, _resources.MonthlyOreIncome };
    private int[] Receipt() => new[] { _resources.LastIncomeReceipt.Tax, _resources.LastIncomeReceipt.Wood, _resources.LastIncomeReceipt.Ore };
    private static int Whole(decimal value) => (int)decimal.Floor(value);

    [Test]
    public void EditingResourcesPublishesOneCompleteStateAndRecalculatesIncome()
    {
        _resources.SetPopulation(0);
        int changes = 0;
        int[] observed = null;
        _resources.OnResourcesChanged += () => {
            changes++;
            observed = new[] { _resources.Population, _resources.Treasury, _resources.Wood, _resources.Ore };
        };
        _resources.SetResources(12000, 5000, 3400, 2200);
        Assert.That(observed, Is.EqualTo(new[] { 12000, 5000, 3400, 2200 }));
        Assert.That(changes, Is.EqualTo(1));
        Assert.That(Incomes().All(amount => amount > 0), Is.True);
    }

    [Test]
    public void InvalidResourceEditsLeaveEveryResourceUnchangedWithoutNotification()
    {
        var before = new[] { _resources.Population, _resources.Treasury, _resources.Wood, _resources.Ore };
        int changes = 0;
        _resources.OnResourcesChanged += () => changes++;
        Assert.Throws<ArgumentOutOfRangeException>(() => _resources.SetResources(-1, 5000, 3400, 2200));
        Assert.Throws<ArgumentOutOfRangeException>(() => _resources.SetResources(12000, -1, 3400, 2200));
        Assert.Throws<ArgumentOutOfRangeException>(() => _resources.SetResources(12000, 5000, -1, 2200));
        Assert.Throws<ArgumentOutOfRangeException>(() => _resources.SetResources(12000, 5000, 3400, -1));
        Assert.That(new[] { _resources.Population, _resources.Treasury, _resources.Wood, _resources.Ore }, Is.EqualTo(before));
        Assert.That(changes, Is.Zero);
    }

    [Test]
    public void EditingStocksPreservesIncomeHistoryAndUnpaidFractions()
    {
        _resources.SetPopulation(8555);
        var income = Incomes();
        _resources.TryCollectMonthlyIncome(0);
        var receipt = Receipt();
        _resources.SetResources(8555, 1000, 2000, 3000);
        var edited = Stocks();
        Assert.That(_resources.TryCollectMonthlyIncome(0), Is.False);
        Assert.That(Receipt(), Is.EqualTo(receipt));
        _resources.TryCollectMonthlyIncome(1);
        Assert.That(Stocks(), Is.EqualTo(edited.Select((stock, i) => stock + Whole(income[i] * 2) - Whole(income[i]))));
    }

    [Test]
    public void MonthlyIncomeCreditsTheTreasuryAndStocksAndMatchesTheReceipt()
    {
        var before = Stocks();
        var income = Incomes();
        Assert.That(_resources.TryCollectMonthlyIncome(0), Is.True);
        var received = Stocks().Select((stock, i) => stock - before[i]).ToArray();
        Assert.That(received, Is.EqualTo(income.Select(Whole)));
        Assert.That(Receipt(), Is.EqualTo(received));
    }

    [Test]
    public void AMonthCannotBePaidTwiceOrBackdated()
    {
        int changes = 0;
        _resources.OnResourcesChanged += () => changes++;
        _resources.TryCollectMonthlyIncome(1);
        var after = Stocks();
        Assert.That(_resources.TryCollectMonthlyIncome(1), Is.False);
        Assert.That(_resources.TryCollectMonthlyIncome(0), Is.False);
        Assert.That(Stocks(), Is.EqualTo(after));
        Assert.That(changes, Is.EqualTo(1));
    }

    [Test]
    public void FractionalIncomeIsCarriedIntoTheNextMonth()
    {
        _resources.SetPopulation(8555);
        var before = Stocks();
        var income = Incomes();
        _resources.TryCollectMonthlyIncome(0);
        _resources.TryCollectMonthlyIncome(1);
        Assert.That(Stocks(), Is.EqualTo(before.Select((stock, i) => stock + Whole(income[i] * 2))));
    }

    [Test]
    public void SmallPopulationsAccumulateFractionalIncomeAcrossMonths()
    {
        _resources.SetPopulation(1);
        var before = Stocks();
        var income = Incomes();
        for (int month = 0; month < 100; month++) _resources.TryCollectMonthlyIncome(month);
        Assert.That(Stocks(), Is.EqualTo(before.Select((stock, i) => stock + Whole(income[i] * 100))));
    }

    [Test]
    public void PopulationChangesPreserveUnpaidFractions()
    {
        _resources.SetPopulation(8555);
        var before = Stocks();
        var first = Incomes();
        _resources.TryCollectMonthlyIncome(0);
        _resources.SetPopulation(8445);
        var second = Incomes();
        _resources.TryCollectMonthlyIncome(1);
        Assert.That(Stocks(), Is.EqualTo(before.Select((stock, i) => stock + Whole(first[i] + second[i]))));
    }

    [Test]
    public void SpendingTreasuryDoesNotChangeTheTaxIncome()
    {
        _resources.TryCollectMonthlyIncome(0);
        int before = _resources.Treasury;
        decimal income = _resources.MonthlyTaxIncome;
        int spent = Math.Min(300, before);
        _resources.SaveMoneyChange(-spent);
        Assert.That(_resources.Money, Is.EqualTo(before - spent));
        Assert.That(_resources.Treasury, Is.EqualTo(before - spent));
        Assert.That(_resources.MonthlyTaxIncome, Is.EqualTo(income));
    }

    [Test]
    public void ZeroPopulationDoesNotGenerateIncome()
    {
        _resources.SetPopulation(0);
        var before = Stocks();
        _resources.TryCollectMonthlyIncome(0);
        Assert.That(Stocks(), Is.EqualTo(before));
    }

    [Test]
    public void ReceiptKeepsTheActualIncomeAfterPopulationChangesAndDuplicateCollection()
    {
        _resources.SetPopulation(8555);
        _resources.TryCollectMonthlyIncome(0);
        var receipt = Receipt();
        _resources.SetPopulation(1);
        _resources.TryCollectMonthlyIncome(0);
        Assert.That(_resources.LastIncomeReceipt.Recorded, Is.True);
        Assert.That(_resources.LastIncomeReceipt.MonthIndex, Is.Zero);
        Assert.That(Receipt(), Is.EqualTo(receipt));
    }

    [Test]
    public void NegativePopulationIsRejected()
    {
        int before = _resources.Population;
        Assert.Throws<ArgumentOutOfRangeException>(() => _resources.SetPopulation(-1));
        Assert.That(_resources.Population, Is.EqualTo(before));
    }
}
