using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using WarSimulation.Combat.Map;

public sealed class PlaytestSceneNavigationTests
{
    private CountryTestCheckpoint _checkpoint;

    [SetUp]
    public void SetUp() => _checkpoint = new CountryTestCheckpoint();

    [UnityTest]
    public IEnumerator CountrySceneKeepsTheClockButtonsBelowTheRightHeaderAndTouchable()
    {
        yield return SceneManager.LoadSceneAsync(PlaytestSceneNavigation.CountryScene);
        yield return null;
        Canvas.ForceUpdateCanvases();

        var hud = Object.FindAnyObjectByType<CountryTimeControls>().transform;
        var header = (RectTransform)hud.Find("TopBar");
        var clock = hud.Find("TopBar/Content/DateControls");
        var date = (RectTransform)clock.Find("CurrentDate");
        var dateCenter = date.TransformPoint(date.rect.center);
        var bottomOfHeader = header.TransformPoint(header.rect.min).y;
        Assert.That(dateCenter.x, Is.GreaterThan(header.position.x));
        Assert.That(dateCenter.y, Is.LessThan(bottomOfHeader));
        foreach (var button in clock.GetComponentsInChildren<Button>())
        {
            var rect = (RectTransform)button.transform;
            var center = rect.TransformPoint(rect.rect.center);
            Assert.That(center.x, Is.GreaterThan(header.position.x), button.name);
            Assert.That(center.y, Is.LessThan(dateCenter.y), button.name);
            var hits = new List<RaycastResult>();
            var events = EventSystem.current;
            events.RaycastAll(new PointerEventData(events) { position = RectTransformUtility.WorldToScreenPoint(null, center) }, hits);
            Assert.That(hits.FirstOrDefault().gameObject?.GetComponentInParent<Button>(), Is.SameAs(button), button.name);
        }
    }

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

    [UnityTest, Timeout(180000)]
    public IEnumerator ExpeditionLoadsAllTwentySixSoldiersAndAppliesVictoryExactlyOnce()
    {
        yield return SceneManager.LoadSceneAsync(PlaytestSceneNavigation.CountryScene);
        yield return null;
        var game = PlayDataManager.Instance;
        var date = DateManager.Instance;
        game.SetRaidsEnabled(false);
        var soldiers = Enumerable.Range(0, 26).ToArray();
        var weapons = Enumerable.Range(0, 26).ToArray();
        int forge = game.Roster.Data.Government.FindFacility(CountryGovernmentData.Blacksmith);
        var facility = game.Roster.Data.Government.Facilities[forge];
        for (int month = 0; month < 4; month++)
            game.Roster.Forge(game.Roster.Today.AddMonths(month), 1, facility.Minimum, facility.Minimum);
        for (int i = 0; i < soldiers.Length; i++) Assert.That(game.Roster.TryEquip(soldiers[i], weapons[i]), Is.True);
        int target = Enumerable.Range(0, game.Expeditions.Data.Regions.Length).First(game.Expeditions.CanTarget);
        Assert.That(game.Expeditions.TryDepart(target, soldiers, weapons, out _), Is.True);
        var trip = game.Expeditions.Active.Single();
        date.Resume(); date.AdvanceTime(trip.Days);

        yield return WaitForCountryCombat();
        var flow = Object.FindAnyObjectByType<CombatBattleFlow>();
        var party = Object.FindAnyObjectByType<CombatCharacterSystem>().AllyCharacters;
        Assert.That(party.Count, Is.EqualTo(soldiers.Length));
        Assert.That(party.All(soldier => soldier.GetComponent<UnityEngine.AI.NavMeshAgent>().isOnNavMesh), Is.True);
        Assert.That(party[0].DisplayName, Is.EqualTo(game.Roster.Data.Soldiers[0].Name));
        Assert.That(party[0].EquippedWeapon.Kind, Is.EqualTo(game.Roster.Weapon(0).Kind));
        Assert.That(party[0].MaxHP, Is.EqualTo(game.Roster.Stats(0).HP));
        Assert.That(party[0].GetEffectiveStat(CombatStat.STR), Is.EqualTo(game.Roster.Stats(0).STR));
        Assert.That(party[0].LearnedSkillIds, Is.EqualTo(game.Roster.Soldiers[0].Learned));
        Assert.That(game.Expeditions.Owner(target), Is.Not.Zero);
        Assert.That(game.Roster.Soldiers[0].CP, Is.EqualTo(100));
        DestroyMainStone(FeatureType.EnemyMainStone);
        Assert.That(flow.State, Is.EqualTo(CombatBattleState.Victory));
        flow.EndBattle(CombatBattleState.Victory);
        yield return ReturnFromCountryCombat();
        Assert.That(CountryDate.From(date), Is.EqualTo(trip.Arrival));
        Assert.That(game.Expeditions.Owner(target), Is.Zero);
        Assert.That(game.Roster.Soldiers[0].CP, Is.EqualTo(100 - game.Expeditions.Data.BattleCPCost));
        Assert.That(game.Roster.Durability(0), Is.EqualTo(game.Roster.Weapon(0).MaxDurability - 1));
        Assert.That(trip.Returning, Is.True);
        Assert.That(game.Battle, Is.Null);
    }

    [UnityTest, Timeout(120000)]
    public IEnumerator ExpeditionUsesTheAuthoredBattlefieldWithoutFreeBattleOptionsOrPreview()
    {
        yield return ReloadCountry();
        var game = PlayDataManager.Instance;
        AuthoredMapDefinition battlefield = null;
        void ConfigureCombatScene(Scene scene, LoadSceneMode mode)
        {
            if (scene.path != PlaytestSceneNavigation.CombatScene) return;
            battlefield = CopyAuthoredBattlefield();
            battlefield.SetBakedPreview(null, 0);
            GetField<List<AuthoredMapDefinition>>(Object.FindAnyObjectByType<CombatMapSelectionView>(
                FindObjectsInactive.Include), "_mapOptions").Clear();
        }
        SceneManager.sceneLoaded += ConfigureCombatScene;
        try
        {
            StartExpeditionBattle(game);
            yield return WaitForCountryCombat();
            Assert.That(Object.FindAnyObjectByType<CombatMapSystem>().AuthoredMap, Is.SameAs(battlefield));
        }
        finally
        {
            SceneManager.sceneLoaded -= ConfigureCombatScene;
            Object.Destroy(battlefield);
        }
    }

    [UnityTest, Timeout(180000)]
    public IEnumerator UnavailableBattlefieldPreservesTheEncounterAndAllowsRetry([Values(false, true)] bool defense)
    {
        yield return ReloadCountry();
        var game = PlayDataManager.Instance;
        var date = DateManager.Instance;
        AuthoredMapDefinition battlefield = null;
        void ConfigureCombatScene(Scene scene, LoadSceneMode mode)
        {
            if (scene.path != PlaytestSceneNavigation.CombatScene) return;
            battlefield = CopyAuthoredBattlefield();
            battlefield.SetBakedRuntimeScene("Assets/Scenes/MissingCountryBattlefield.unity", battlefield.ComputeGeometryFingerprint());
            var map = Object.FindAnyObjectByType<CombatMapSystem>();
            SetField(map, "_mapSceneHost", null);
            SetField(map, "_buildMapOnStart", false);
            var options = GetField<List<AuthoredMapDefinition>>(Object.FindAnyObjectByType<CombatMapSelectionView>(
                FindObjectsInactive.Include), "_mapOptions");
            options.Clear();
            options.Add(battlefield);
        }

        int initialCP = game.Roster.Soldiers[0].CP, durability = game.Roster.Durability(0);
        LogAssert.Expect(LogType.Warning, "Country battle could not start: ランタイム用マップSceneを読み込めません");
        SceneManager.sceneLoaded += ConfigureCombatScene;
        try
        {
            if (defense)
            {
                Assert.That(game.Roster.TryEquip(0, 0), Is.True);
                for (int attempt = 0; attempt < 3 && game.Defense.Pending == null; attempt++) AdvanceToNextStop(date);
                Assert.That(game.Defense.Pending, Is.Not.Null);
                Assert.That(game.TryOpenDefenseBattle(new[] { 0 }, new[] { 0 }, out var reason), Is.True, reason);
            }
            else StartExpeditionBattle(game);

            var raid = game.Defense.Pending;
            var trip = game.Expeditions.Arrived;
            var encounterDay = CountryDate.From(date);
            yield return WaitForNavigation("GafuTest");
            var combat = Object.FindAnyObjectByType<CombatFlow>();
            var back = GetField<Button>(combat, "_backToSelectionButton");
            float deadline = Time.realtimeSinceStartup + 20;
            while (!back.isActiveAndEnabled && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(back.isActiveAndEnabled, Is.True, "A failed battle must offer a way back to the country.");
            var title = GetField<TMP_Text>(combat, "_resultTitle");
            title.ForceMeshUpdate();
            var safe = (RectTransform)title.rectTransform.parent.parent;
            var text = title.textBounds;
            Assert.That(safe.rect.Contains(safe.InverseTransformPoint(title.rectTransform.TransformPoint(text.min))), Is.True);
            Assert.That(safe.rect.Contains(safe.InverseTransformPoint(title.rectTransform.TransformPoint(text.max))), Is.True);
            Assert.That(title.isTextOverflowing, Is.False);
            Assert.That(game.Battle.Resolved, Is.False);
            Assert.That(Object.FindAnyObjectByType<CombatBattleFlow>().State, Is.EqualTo(CombatBattleState.WaitingToStart));
            yield return ReturnFromCountryCombat();

            Assert.That(game.Battle, Is.Null);
            Assert.That(game.Roster.Soldiers[0].CP, Is.EqualTo(initialCP));
            Assert.That(game.Roster.Durability(0), Is.EqualTo(durability));
            Assert.That(CountryDate.From(date), Is.EqualTo(encounterDay));
            Assert.That(date.IsRunning, Is.False);
            if (defense) Assert.That(game.Defense.Pending, Is.SameAs(raid));
            else Assert.That(game.Expeditions.Arrived, Is.SameAs(trip));
        }
        finally
        {
            SceneManager.sceneLoaded -= ConfigureCombatScene;
            Object.Destroy(battlefield);
        }

        if (defense)
            Assert.That(game.TryOpenDefenseBattle(game.Defense.Members, game.Defense.Weapons, out var retryReason), Is.True, retryReason);
        else date.Resume();
        yield return WaitForCountryCombat();
        DestroyMainStone(FeatureType.EnemyMainStone);
        int cpCost = defense ? game.Defense.CPCost : game.Expeditions.Data.BattleCPCost;
        Assert.That(game.Roster.Soldiers[0].CP, Is.EqualTo(initialCP - cpCost));
        Assert.That(game.Roster.Durability(0), Is.EqualTo(durability - 1));
        yield return ReturnFromCountryCombat();
    }

    [UnityTest, Timeout(180000)]
    public IEnumerator DefenseEntersCombatAndReturnsAfterDefeat()
    {
        yield return SceneManager.LoadSceneAsync(PlaytestSceneNavigation.CountryScene);
        yield return null;
        var game = PlayDataManager.Instance;
        var date = DateManager.Instance;
        Assert.That(game.Roster.TryEquip(0, 0), Is.True);
        Assert.That(game.Roster.TrySetCP(0, game.Defense.CPCost), Is.True);
        var today = game.Defense.NextRaid;
        game.Roster.AdvanceTo(today);
        game.Defense.AdvanceTo(today);
        var panel = Object.FindAnyObjectByType<CountryDefensePanel>();
        Assert.That(panel.IsOpen, Is.True);
        Assert.That(game.Defense.TryRegister(new[] { 0 }, new[] { 0 }, out _), Is.True);
        Assert.That(game.TryOpenDefenseBattle(new[] { 0 }, new[] { 0 }, out var reason), Is.True, reason);

        yield return WaitForCountryCombat();
        var flow = Object.FindAnyObjectByType<CombatBattleFlow>();
        Assert.That(game.Battle.IsDefense, Is.True);
        DestroyMainStone(FeatureType.OwnMainStone);
        Assert.That(flow.State, Is.EqualTo(CombatBattleState.Defeat));
        yield return ReturnFromCountryCombat();
        Assert.That(game.Defense.Pending, Is.Null);
        Assert.That(game.Roster.Soldiers[0].CP, Is.Zero);
        Assert.That(game.Roster.Soldiers[0].RevivalDue, Is.EqualTo(today.AddMonths(1)));
        Assert.That(game.Battle, Is.Null);
        Assert.That(date.IsRunning, Is.False);
    }

    [UnityTest, Timeout(180000)]
    public IEnumerator LaterDefenseHasStrongerEnemiesAndAdditionalWeaponSkills()
    {
        yield return SceneManager.LoadSceneAsync(PlaytestSceneNavigation.CountryScene);
        yield return null;
        var game = PlayDataManager.Instance;
        var date = DateManager.Instance;
        Assert.That(game.Roster.TryEquip(0, 0), Is.True);
        var soldierStats = game.Roster.Stats(0);
        date.Resume(); date.AdvanceTime(10000);
        Assert.That(game.Defense.Pending, Is.Not.Null);
        Assert.That(game.TryOpenDefenseBattle(new[] { 0 }, new[] { 0 }, out var reason), Is.True, reason);
        yield return WaitForCountryCombat();
        var first = Object.FindAnyObjectByType<CombatCharacterSystem>().EnemyCharacters[0];
        var stats = EnemyStats(first);
        var skills = first.LearnedSkillIds.ToArray();
        DestroyMainStone(FeatureType.OwnMainStone);
        yield return ReturnFromCountryCombat();

        game.SetRaidsEnabled(false);
        int laterMonth = date.ElapsedMonths + game.Expeditions.Data.EnemySkillIntervalMonths;
        while (date.ElapsedMonths < laterMonth)
        {
            Object.FindAnyObjectByType<CountryGovernmentPanel>().Close();
            date.Resume(); date.AdvanceTime(10000);
            yield return null;
        }
        game.SetRaidsEnabled(true);
        while (game.Defense.Pending == null)
        {
            Object.FindAnyObjectByType<CountryGovernmentPanel>().Close();
            date.Resume(); date.AdvanceTime(10000);
            yield return null;
        }
        Assert.That(game.TryOpenDefenseBattle(new[] { 0 }, new[] { 0 }, out reason), Is.True, reason);
        yield return WaitForCountryCombat();
        var later = Object.FindAnyObjectByType<CombatCharacterSystem>().EnemyCharacters[0];
        var laterStats = EnemyStats(later);
        for (int i = 0; i < stats.Length; i++) Assert.That(laterStats[i], Is.GreaterThan(stats[i]));
        Assert.That(later.LearnedSkillIds, Is.SupersetOf(skills));
        Assert.That(later.LearnedSkillIds.Count, Is.GreaterThan(skills.Length));
        Assert.That(game.Roster.Stats(0), Is.EqualTo(soldierStats));
        DestroyMainStone(FeatureType.OwnMainStone);
        yield return ReturnFromCountryCombat();
    }

    private static float[] EnemyStats(Character character) => new[] { (float)character.MaxHP,
        character.GetEffectiveStat(CombatStat.STR), character.GetEffectiveStat(CombatStat.INT),
        character.GetEffectiveStat(CombatStat.FAI), character.GetEffectiveStat(CombatStat.AGI) };

    [UnityTest, Timeout(180000)]
    public IEnumerator TrainingAndExpeditionVictorySurviveAColdReload()
    {
        yield return ReloadCountry();
        var game = PlayDataManager.Instance;
        var date = DateManager.Instance;
        game.SetRaidsEnabled(false);
        game.SetResources(8500, 10000, 5000, 5000);
        int forge = game.Roster.Data.Government.FindFacility(CountryGovernmentData.Blacksmith);
        int forgeLevel = game.FacilityLevel(CountryGovernmentData.Blacksmith);
        int initialWeapons = game.Roster.WeaponCount;
        var government = game.GovernmentDraft.Copy();
        government.SetUseTreasury(true);
        Assert.That(government.PlanConstruction(forge, true), Is.True);
        var today = CountryDate.From(date);
        game.ConfirmGovernment(government, today, today.AddMonths(1), applyFunding: true, startConstruction: true);
        int treasuryAfterStart = game.Treasury;
        Assert.That(game.Roster.TryEquip(0, 0), Is.True);
        var initial = game.Roster.Stats(0);
        Assert.That(game.Roster.TryStartTraining(0, new[] { 0 }, out var reason), Is.True, reason);
        Assert.That(game.Roster.CanDeploy(0), Is.False);
        var training = game.Roster.Soldiers[0].Training;
        yield return null;

        yield return ReloadCountry();
        game = PlayDataManager.Instance;
        date = DateManager.Instance;
        Assert.That(game.Roster.Soldiers[0].Training?.Due, Is.EqualTo(training.Due));
        Assert.That(game.RaidsEnabled, Is.False);
        Assert.That(game.Treasury, Is.EqualTo(treasuryAfterStart));
        AdvanceToDate(date, training.Due);
        Assert.That(game.FacilityLevel(CountryGovernmentData.Blacksmith), Is.EqualTo(forgeLevel + 1));
        Assert.That(game.Roster.WeaponCount, Is.GreaterThan(initialWeapons));
        var trained = game.Roster.Soldiers[0];
        var stats = game.Roster.Stats(0);
        Assert.That(stats.STR, Is.GreaterThan(initial.STR));
        Assert.That(trained.Learned, Has.Member(SkillId.Sword_QuickSlash));
        trained.Personality = CombatAiPersonalityKind.AttentionSeeker;
        var displayed = game.Roster.BattleStats(0, 0);
        int target = Enumerable.Range(0, game.Expeditions.Data.Regions.Length).First(game.Expeditions.CanTarget);
        Object.FindAnyObjectByType<CountryGovernmentPanel>().Close();
        Assert.That(game.Expeditions.TryDepart(target, new[] { 0 }, new[] { 0 }, out reason), Is.True, reason);
        var arrival = game.Expeditions.Active.Single().Arrival;
        int initialCP = trained.CP, durability = game.Roster.Durability(0);
        yield return null;

        yield return ReloadCountry();
        game = PlayDataManager.Instance;
        date = DateManager.Instance;
        var trip = game.Expeditions.Active.Single();
        Assert.That(game.Roster.Soldiers[0].Expedition, Is.SameAs(trip));
        Assert.That(game.Roster.EquippedWeapon(0), Is.Zero);
        Assert.That(trip.Arrival, Is.EqualTo(arrival));
        date.Resume();
        date.AdvanceTime(CountryDate.From(date).DaysUntil(arrival));
        yield return WaitForCountryCombat();
        var hero = Object.FindAnyObjectByType<CombatCharacterSystem>().AllyCharacters.Single();
        Assert.That(hero.AvailableCombatSkills.Select(skill => skill.Id), Has.Member(SkillId.Sword_QuickSlash));
        Assert.That(hero.AvailableCombatSkills.Select(skill => skill.Id), Has.No.Member(SkillId.Shield_ShoulderGuard));
        Assert.That(hero.PersonalityProfile.Kind, Is.EqualTo(CombatAiPersonalityKind.AttentionSeeker));
        Assert.That(hero.GetEffectiveStat(CombatStat.STR), Is.EqualTo(displayed.STR));
        Assert.That(hero.MaxHP, Is.EqualTo(displayed.HP));
        DestroyMainStone(FeatureType.EnemyMainStone);

        yield return ReloadCountry();
        game = PlayDataManager.Instance;
        date = DateManager.Instance;
        Assert.That(game.Battle, Is.Null);
        Assert.That(game.Expeditions.Active.Single().Won, Is.True);
        Assert.That(game.Expeditions.Owner(target), Is.Zero);
        Assert.That(game.Roster.Soldiers[0].CP, Is.EqualTo(initialCP - game.Expeditions.Data.BattleCPCost));
        Assert.That(game.Roster.Durability(0), Is.EqualTo(durability - 1));
        Assert.That(CountryDate.From(date), Is.EqualTo(arrival));
        Assert.That(date.IsRunning, Is.False);
    }

    [UnityTest, Timeout(180000)]
    public IEnumerator InterruptedDefenseKeepsItsRaidAndSettledDefeatKeepsItsRevival()
    {
        yield return ReloadCountry();
        var game = PlayDataManager.Instance;
        var date = DateManager.Instance;
        Assert.That(game.Roster.TryEquip(0, 0), Is.True);
        int cost = game.Defense.CPCost;
        while (game.Roster.Durability(0) > 1)
            game.Roster.ConsumeBattleCP(new[] { 0 }, 0, CountryDate.From(date));
        Assert.That(game.Roster.TrySetCP(0, cost), Is.True);
        for (int attempt = 0; attempt < 3 && game.Defense.Pending == null; attempt++) AdvanceToNextStop(date);
        Assert.That(game.Defense.Pending, Is.Not.Null);
        int region = game.Defense.Pending.Region;
        var raidDay = game.Defense.Pending.Date;
        Assert.That(game.TryOpenDefenseBattle(new[] { 0 }, new[] { 0 }, out var reason), Is.True, reason);
        yield return WaitForCountryCombat();

        yield return ReloadCountry();
        game = PlayDataManager.Instance;
        date = DateManager.Instance;
        Assert.That(game.Defense.Pending?.Date, Is.EqualTo(raidDay));
        Assert.That(game.Defense.Pending?.Region, Is.EqualTo(region));
        Assert.That(game.Defense.Members, Is.EqualTo(new[] { 0 }));
        Assert.That(game.Roster.Soldiers[0].CP, Is.EqualTo(cost));
        Assert.That(game.Roster.Durability(0), Is.EqualTo(1));
        Assert.That(Object.FindAnyObjectByType<CountryDefensePanel>().IsOpen, Is.True);
        date.Resume();
        Assert.That(date.IsRunning, Is.False);
        Assert.That(game.TryOpenDefenseBattle(game.Defense.Members, game.Defense.Weapons, out reason), Is.True, reason);
        yield return WaitForCountryCombat();
        DestroyMainStone(FeatureType.OwnMainStone);
        yield return ReloadCountry();

        game = PlayDataManager.Instance;
        date = DateManager.Instance;
        Assert.That(game.Defense.Pending, Is.Null);
        Assert.That(game.Defense.Members, Is.Empty);
        Assert.That(game.Roster.Soldiers[0].CP, Is.Zero);
        var due = raidDay.AddMonths(1);
        Assert.That(game.Roster.Soldiers[0].RevivalDue, Is.EqualTo(due));
        Assert.That(game.Roster.Durability(0), Is.Zero);
        Assert.That(game.Roster.EquippedWeapon(0), Is.EqualTo(-1));
        game.SetRaidsEnabled(false);
        AdvanceToDate(date, due);
        date.Pause();
        var personality = game.Roster.Soldiers[0].Personality;
        Assert.That(game.Roster.Soldiers[0].CP, Is.EqualTo(game.Roster.Data.Soldiers[0].MaxCP));
        yield return null;
        yield return ReloadCountry();
        Assert.That(PlayDataManager.Instance.Roster.Soldiers[0].Personality, Is.EqualTo(personality));
        Assert.That(PlayDataManager.Instance.Roster.Soldiers[0].RevivalDue, Is.Null);
    }

    [UnityTest]
    public IEnumerator LegacySaveReceivesTheSpiritSchoolCPBonusExactlyOnce()
    {
        yield return ReloadCountry();
        var game = PlayDataManager.Instance;
        Assert.That(game.Roster.TrySetCP(0, 60), Is.True);
        Assert.That(game.Roster.TrySetCP(1, 0), Is.True);
        var revival = game.Roster.Soldiers[1].RevivalDue;
        yield return null;

        var entry = Enumerable.Range(0, 2).Select(i => CountryCheckpointStore.TestKey + "." + i)
            .Where(PlayerPrefs.HasKey)
            .Select(key => (Key: key, Save: JsonUtility.FromJson<CountryCheckpoint>(PlayerPrefs.GetString(key))))
            .OrderByDescending(item => item.Save.Revision).First();
        var facilities = entry.Save.Government.Facilities;
        int spirit = System.Array.FindIndex(facilities, facility => facility.Name == CountryGovernmentData.SpiritSchool);
        facilities[spirit].Level = 4;
        entry.Save.Version = CountryCheckpoint.LegacyCPVersion;
        PlayerPrefs.SetString(entry.Key, JsonUtility.ToJson(entry.Save));
        PlayerPrefs.Save();

        yield return ReloadCountry();
        CheckMigratedRoster(90);
        Assert.That(PlayDataManager.Instance.Roster.TrySetCP(0, 120), Is.True);
        yield return null;
        yield return ReloadCountry();
        CheckMigratedRoster(120);

        void CheckMigratedRoster(int expectedCP)
        {
            var roster = PlayDataManager.Instance.Roster;
            Assert.That(roster.MaxCP(0), Is.EqualTo(130));
            Assert.That(roster.Soldiers[0].CP, Is.EqualTo(expectedCP));
            Assert.That(roster.Soldiers[1].CP, Is.Zero);
            Assert.That(roster.Soldiers[1].RevivalDue, Is.EqualTo(revival));
        }
    }

    [UnityTest]
    public IEnumerator InvalidNewestCheckpointRecoversTheLastCompleteCampaign()
    {
        yield return ReloadCountry();
        var game = PlayDataManager.Instance;
        game.SetResources(8455, 4100, 700, 900);
        Assert.That(game.Roster.TryEquip(0, 0), Is.True);
        Assert.That(game.SaveCountryProgress(), Is.True);
        game.SetResources(9210, 3900, 650, 790);
        Assert.That(game.Roster.TryEquip(1, 0), Is.True);
        Assert.That(game.SaveCountryProgress(), Is.True);
        var entries = Enumerable.Range(0, 2).Select(i => CountryCheckpointStore.TestKey + "." + i)
            .Select(key => (Key: key, Save: JsonUtility.FromJson<CountryCheckpoint>(PlayerPrefs.GetString(key))))
            .OrderByDescending(entry => entry.Save.Revision).ToArray();
        entries[0].Save.World.Active = new[] { new CountryCheckpoint.Trip { Target = "invalid" } };
        PlayerPrefs.SetString(entries[0].Key, JsonUtility.ToJson(entries[0].Save));
        PlayerPrefs.Save();

        yield return ReloadCountry();
        game = PlayDataManager.Instance;
        Assert.That(new[] { game.Population, game.Treasury, game.Wood, game.Ore }, Is.EqualTo(new[] { 8455, 4100, 700, 900 }));
        Assert.That(game.Roster.WeaponOwner(0), Is.Zero);
        Assert.That(game.Expeditions.Active, Is.Empty);
    }

    [UnityTest, Timeout(180000)]
    public IEnumerator TimeLimitFavorsTheSideDefendingTheTerritory([Values(true, false)] bool defense)
    {
        yield return ReloadCountry();
        var game = PlayDataManager.Instance;
        var date = DateManager.Instance;
        Assert.That(game.Roster.TryEquip(0, 0), Is.True);
        var world = game.Expeditions.Data;
        int originalLimit = world.BattleTimeLimitSeconds;
        world.BattleTimeLimitSeconds = 2;
        try
        {
            if (defense)
            {
                for (int attempt = 0; attempt < 3 && game.Defense.Pending == null; attempt++) AdvanceToNextStop(date);
                Assert.That(game.Defense.Pending, Is.Not.Null);
                Assert.That(game.TryOpenDefenseBattle(new[] { 0 }, new[] { 0 }, out var reason), Is.True, reason);
            }
            else
            {
                game.SetRaidsEnabled(false);
                int target = Enumerable.Range(0, world.Regions.Length).First(game.Expeditions.CanTarget);
                Assert.That(game.Expeditions.TryDepart(target, new[] { 0 }, new[] { 0 }, out var reason), Is.True, reason);
                var trip = game.Expeditions.Active.Single();
                date.Resume();
                date.AdvanceTime(trip.Days);
            }
            yield return WaitForCountryCombat();
            var flow = Object.FindAnyObjectByType<CombatBattleFlow>();
            var pause = Object.FindObjectsByType<UnityEngine.UI.Button>().Single(button =>
                button.isActiveAndEnabled && button.GetComponentInChildren<TMP_Text>()?.text == "一時停止");
            pause.onClick.Invoke();
            yield return new WaitForSecondsRealtime(2.1f);
            Assert.That(flow.State, Is.EqualTo(CombatBattleState.Running));
            pause.onClick.Invoke();
            float deadline = Time.realtimeSinceStartup + 12;
            while (flow.State == CombatBattleState.Running && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(flow.State, Is.EqualTo(defense ? CombatBattleState.Victory : CombatBattleState.Defeat));
            Assert.That(game.Battle.Resolved, Is.True);
            yield return ReturnFromCountryCombat();
            Assert.That(date.IsRunning, Is.False);
        }
        finally { world.BattleTimeLimitSeconds = originalLimit; }
    }

    private static void AdvanceToNextStop(DateManager date)
    {
        Object.FindAnyObjectByType<CountryGovernmentPanel>().Close();
        date.Resume();
        date.AdvanceTime(10000);
    }

    private static void StartExpeditionBattle(PlayDataManager game)
    {
        game.SetRaidsEnabled(false);
        Assert.That(game.Roster.TryEquip(0, 0), Is.True);
        int target = Enumerable.Range(0, game.Expeditions.Data.Regions.Length).First(game.Expeditions.CanTarget);
        Assert.That(game.Expeditions.TryDepart(target, new[] { 0 }, new[] { 0 }, out var reason), Is.True, reason);
        var date = DateManager.Instance;
        date.Resume();
        date.AdvanceTime(game.Expeditions.Active.Single().Days);
    }

    private static AuthoredMapDefinition CopyAuthoredBattlefield()
    {
        var map = Object.FindAnyObjectByType<CombatMapSystem>();
        var copy = Object.Instantiate(map.AuthoredMap);
        SetField(map, "_authoredMap", copy);
        return copy;
    }

    private const System.Reflection.BindingFlags Private = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
    private static T GetField<T>(object target, string name) => (T)target.GetType().GetField(name, Private).GetValue(target);
    private static void SetField(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);

    private static void AdvanceToDate(DateManager date, CountryDate target)
    {
        for (int attempt = 0; attempt < 4 && CountryDate.From(date).CompareTo(target) < 0; attempt++)
        {
            Object.FindAnyObjectByType<CountryGovernmentPanel>().Close();
            date.Resume();
            date.AdvanceTime(CountryDate.From(date).DaysUntil(target));
        }
        Assert.That(CountryDate.From(date), Is.EqualTo(target));
    }

    private static IEnumerator ReloadCountry()
    {
        yield return UnloadCampaign();
        yield return SceneManager.LoadSceneAsync(PlaytestSceneNavigation.CountryScene);
        yield return null;
    }

    private static void DestroyMainStone(FeatureType type)
    {
        var stones = Object.FindAnyObjectByType<CombatMagicStoneSystem>();
        Assert.That(stones.TryGetState(type, out var stone), Is.True);
        Assert.That(stones.TakeDamage(stone.FeatureIndex, stone.HP), Is.GreaterThan(0));
    }

    private static IEnumerator WaitForCountryCombat()
    {
        yield return WaitForNavigation("GafuTest");
        var flow = Object.FindAnyObjectByType<CombatBattleFlow>();
        var back = GetField<Button>(Object.FindAnyObjectByType<CombatFlow>(), "_backToSelectionButton");
        float deadline = Time.realtimeSinceStartup + 60;
        while (flow.State == CombatBattleState.WaitingToStart && !back.isActiveAndEnabled &&
            Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(flow.State, Is.EqualTo(CombatBattleState.Running),
            GetField<TMP_Text>(Object.FindAnyObjectByType<CombatFlow>(), "_resultTitle").text);
    }

    private static IEnumerator ReturnFromCountryCombat()
    {
        var button = Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsSortMode.None)
            .Single(item => item.GetComponentInChildren<TMP_Text>()?.text == "国へ戻る");
        button.onClick.Invoke();
        yield return WaitForNavigation("Country");
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
        yield return UnloadCampaign();
        _checkpoint.Dispose();
    }

    private static IEnumerator UnloadCampaign()
    {
        Time.timeScale = 1;
        var scenes = Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt).ToArray();
        SceneManager.SetActiveScene(SceneManager.CreateScene("PlaytestNavigationCleanup-" + System.Guid.NewGuid().ToString("N")));
        foreach (var scene in scenes) yield return SceneManager.UnloadSceneAsync(scene);
        if (PlayDataManager.Instance != null) Object.Destroy(PlayDataManager.Instance.gameObject);
        if (DateManager.Instance != null) Object.Destroy(DateManager.Instance.gameObject);
        if (CombatAudioPlayer.Existing != null) Object.Destroy(CombatAudioPlayer.Existing.gameObject);
        yield return null;
    }
}

internal sealed class CountryTestCheckpoint : System.IDisposable
{
    private readonly string _previous = CountryCheckpointStore.TestKey;
    private readonly string _key = "WarSimulation.Test.Country." + System.Guid.NewGuid().ToString("N");

    public CountryTestCheckpoint() => CountryCheckpointStore.TestKey = _key;

    public void Dispose()
    {
        PlayerPrefs.DeleteKey(_key + ".0");
        PlayerPrefs.DeleteKey(_key + ".1");
        PlayerPrefs.Save();
        CountryCheckpointStore.TestKey = _previous;
    }
}
