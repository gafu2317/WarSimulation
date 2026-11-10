using System.Collections;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class PlaytestSceneNavigationTests
{
    [UnityTest, Timeout(300000)]
    public IEnumerator SettingsAndCombatButtonsKeepCountryStateAcrossThreeRoundTrips()
    {
        yield return SceneManager.LoadSceneAsync(PlaytestSceneNavigation.CountryScene);
        yield return null;
        var resources = PlayDataManager.Instance;
        var date = DateManager.Instance;
        resources.SetResources(12000, 20000, 3400, 2200);
        resources.SetRaidsEnabled(false);
        date.Resume(); date.AdvanceTime(4); date.Pause();
        var day = CountryDate.From(date);
        var roster = resources.Roster;
        var expeditions = resources.Expeditions;
        var defense = resources.Defense;
        var government = resources.GovernmentDraft;
        government.SetUseTreasury(true);
        government.SetFunding(0, government.Funding(0) + 10);
        int funding = government.Funding(0);
        Assert.That(government.PlanConstruction(0, true), Is.True);

        for (int round = 0; round < 3; round++)
        {
            var settings = Object.FindAnyObjectByType<CountrySettingsPanel>();
            settings.Open();
            settings.GetComponentsInChildren<TMP_InputField>(true)[0].text = "";
            var combat = settings.GetComponentsInChildren<UnityEngine.UI.Button>(true).Single(button => button.name == "Combat");
            Assert.That(combat.interactable, Is.True);
            combat.onClick.Invoke(); combat.onClick.Invoke();
            yield return WaitForNavigation("GafuTest");
            Assert.That(Object.FindAnyObjectByType<CombatCharacterSelection>(), Is.Not.Null);
            var map = Object.FindAnyObjectByType<CombatMapSystem>();
            float deadline = Time.realtimeSinceStartup + 60;
            while (!map.IsMapReady(map.AuthoredMap) && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(map.IsMapReady(map.AuthoredMap), Is.True,
                $"{map.AuthoredMap?.name}: {map.PreparationState}, {map.PreparationFailure}");
            yield return new WaitForSecondsRealtime(.1f);
            Assert.That(CountryDate.From(date), Is.EqualTo(day));
            Assert.That(date.IsRunning, Is.False);
            var back = Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsSortMode.None)
                .Single(button => button.name == "ReturnToCountryButton");
            var label = back.GetComponentInChildren<TMP_Text>();
            Assert.That(label.font.name, Is.EqualTo("NotoSansJP-Regular SDF"));
            Time.timeScale = 4;
            back.onClick.Invoke();
            yield return WaitForNavigation("Country");

            Assert.That(PlayDataManager.Instance, Is.SameAs(resources));
            Assert.That(DateManager.Instance, Is.SameAs(date));
            Assert.That(new[] { resources.Population, resources.Treasury, resources.Wood, resources.Ore }, Is.EqualTo(new[] { 12000, 20000, 3400, 2200 }));
            Assert.That(CountryDate.From(date), Is.EqualTo(day));
            Assert.That(resources.Roster, Is.SameAs(roster));
            Assert.That(resources.Expeditions, Is.SameAs(expeditions));
            Assert.That(resources.Defense, Is.SameAs(defense));
            Assert.That(resources.RaidsEnabled, Is.False);
            Assert.That(resources.GovernmentDraft.Funding(0), Is.EqualTo(funding));
            Assert.That(resources.GovernmentDraft.IsOrdered(0), Is.True);
            Assert.That(resources.GovernmentDraft.UseTreasury, Is.True);
            Assert.That(Object.FindAnyObjectByType<CountryGovernmentPanel>().Draft, Is.SameAs(government));
            Assert.That(Time.timeScale, Is.EqualTo(1));
            Assert.That(SceneManager.sceneCount, Is.EqualTo(1));
            Assert.That(Object.FindObjectsByType<PlayDataManager>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
        }
    }

    [UnityTest, Timeout(120000)]
    public IEnumerator DirectCombatEntryCanReturnToCountry()
    {
        yield return SceneManager.LoadSceneAsync(PlaytestSceneNavigation.CombatScene);
        yield return null;
        PlaytestSceneNavigation.OpenCountry();
        yield return WaitForNavigation("Country");
        Assert.That(PlayDataManager.Instance, Is.Not.Null);
        Assert.That(DateManager.Instance.IsRunning, Is.False);
        Assert.That(Object.FindAnyObjectByType<CountrySettingsPanel>(), Is.Not.Null);
    }

    private static IEnumerator WaitForNavigation(string scene)
    {
        float deadline = Time.realtimeSinceStartup + 60;
        while (PlaytestSceneNavigation.IsLoading && Time.realtimeSinceStartup < deadline) yield return null;
        yield return null;
        Assert.That(PlaytestSceneNavigation.IsLoading, Is.False);
        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(scene));
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        Time.timeScale = 1;
        var scenes = Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt).ToArray();
        SceneManager.SetActiveScene(SceneManager.CreateScene("PlaytestNavigationCleanup"));
        foreach (var scene in scenes) yield return SceneManager.UnloadSceneAsync(scene);
        if (PlayDataManager.Instance != null) Object.Destroy(PlayDataManager.Instance.gameObject);
        if (DateManager.Instance != null) Object.Destroy(DateManager.Instance.gameObject);
        if (CombatAudioPlayer.Existing != null) Object.Destroy(CombatAudioPlayer.Existing.gameObject);
        yield return null;
    }
}
