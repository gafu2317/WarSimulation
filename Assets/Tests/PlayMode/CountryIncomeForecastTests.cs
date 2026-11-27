using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class CountryIncomeForecastTests
{
    private CountryTestCheckpoint _checkpoint;

    [SetUp]
    public void SetUp() => _checkpoint = new CountryTestCheckpoint();

    [UnityTest]
    public IEnumerator DirectPlanningUsesTheSameBalancesBeforeAndAfterMonthEndIncome()
    {
        yield return SceneManager.LoadSceneAsync(PlaytestSceneNavigation.CountryScene);
        yield return null;
        var panel = Object.FindAnyObjectByType<CountryGovernmentPanel>();
        var resources = PlayDataManager.Instance;
        var date = DateManager.Instance;
        resources.SetRaidsEnabled(false);
        resources.SetResources(8555, 0, 300, 150);

        date.Resume();
        date.AdvanceTime(date.DaysInCurrentMonth - 2);
        panel.OpenBudget();
        AssertForecast(panel.Draft, 855, 855, 471, 235);
        Assert.That(resources.Treasury, Is.Zero);
        panel.Close();

        date.Resume();
        date.AdvanceTime(1);
        Assert.That(resources.LastIncomeReceipt.Recorded, Is.True);
        panel.OpenBudget();
        AssertForecast(panel.Draft, 855, 855, 471, 235);
        Assert.That(resources.Treasury, Is.EqualTo(855));
        panel.Close();

        date.Resume();
        date.AdvanceTime(1);
        int funding = resources.GovernmentDraft.FundingTotal;
        Assert.That(resources.Treasury, Is.EqualTo(855 - funding));
        panel.Close();

        panel.OpenBudget();
        AssertForecast(panel.Draft, 856, 1711 - funding, 642, 321);
    }

    private static void AssertForecast(CountryGovernmentDraft draft, int tax, int treasury, int wood, int ore)
    {
        Assert.That(draft.Tax, Is.EqualTo(tax));
        Assert.That(draft.TreasuryForecast + draft.FundingTotal, Is.EqualTo(treasury));
        Assert.That(draft.WoodForecast, Is.EqualTo(wood));
        Assert.That(draft.OreForecast, Is.EqualTo(ore));
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        var country = SceneManager.GetSceneByPath(PlaytestSceneNavigation.CountryScene);
        if (country.isLoaded)
        {
            SceneManager.SetActiveScene(SceneManager.CreateScene("CountryIncomeForecastCleanup"));
            yield return SceneManager.UnloadSceneAsync(country);
        }
        if (PlayDataManager.Instance != null) Object.Destroy(PlayDataManager.Instance.gameObject);
        if (DateManager.Instance != null) Object.Destroy(DateManager.Instance.gameObject);
        yield return null;
        _checkpoint.Dispose();
    }
}
