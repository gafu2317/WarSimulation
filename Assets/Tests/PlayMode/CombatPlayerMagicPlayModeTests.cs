using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

[Category("PlayerMagic")]
public sealed class CombatPlayerMagicPlayModeTests
{
    private GameObject _root;
    private bool _battleScene;
    [UnityTearDown]
    public IEnumerator TearDown()
    {
        Time.timeScale = 1;
        if (_root != null) Object.Destroy(_root);
        yield return null;
        if (!_battleScene) yield break;
        var cleanup = SceneManager.CreateScene("PlayerMagicCleanup");
        SceneManager.SetActiveScene(cleanup);
        var scenes = new List<Scene>();
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i) != cleanup) scenes.Add(SceneManager.GetSceneAt(i));
        foreach (Scene scene in scenes) yield return SceneManager.UnloadSceneAsync(scene);
        _battleScene = false;
    }

    [UnityTest]
    public IEnumerator Invulnerability_FollowsRetreatingCharactersRefreshesAndExpiresWithVfx()
    {
        _root = new GameObject("Magic test");
        var characters = _root.AddComponent<CombatCharacterSystem>();
        var a = new GameObject("Ally"); a.transform.SetParent(_root.transform);
        var b = new GameObject("Enemy"); b.transform.SetParent(_root.transform);
        var ally = a.AddComponent<Character>(); var enemy = b.AddComponent<Character>();
        ally.GetComponent<UnityEngine.AI.NavMeshAgent>().enabled = false;
        enemy.GetComponent<UnityEngine.AI.NavMeshAgent>().enabled = false;
        ally.Health.Initialize(100); enemy.Health.Initialize(100); enemy.Health.EnterRetreat();
        characters.AllyCharacters.Add(ally); characters.EnemyCharacters.Add(enemy);
        var player = _root.AddComponent<SkillVfxPlayer>();
        var system = new CombatPlayerMagicSystem(characters);
        var request = new CombatPlayerMagicRequest(CombatPlayerMagicKind.AllInvulnerable, Vector3.zero, 0, 0, 1);
        player.PlayPlayerMagic(request, system.Execute(request));
        Assert.That(player.ActiveCount, Is.EqualTo(2));
        enemy.transform.position = new Vector3(3, 0, 2);
        yield return null;
        Assert.That(player.ActiveCount, Is.EqualTo(2));
        request = new CombatPlayerMagicRequest(CombatPlayerMagicKind.AllInvulnerable, Vector3.zero, 0, 0, .1f);
        player.PlayPlayerMagic(request, system.Execute(request));
        Assert.That(player.ActiveCount, Is.EqualTo(2));
        Assert.That(ally.Health.TakeDamage(40), Is.Zero);
        yield return new WaitForSeconds(.15f);
        Assert.That(ally.StatusEffects.IsInvulnerable, Is.False);
        Assert.That(enemy.StatusEffects.IsInvulnerable, Is.False);
        Assert.That(ally.Health.TakeDamage(40), Is.EqualTo(40));
        yield return new WaitForSeconds(.3f);
        Assert.That(player.ActiveCount, Is.Zero);
        request = new CombatPlayerMagicRequest(CombatPlayerMagicKind.AllInvulnerable, Vector3.zero, 0, 0, 1);
        player.PlayPlayerMagic(request, system.Execute(request));
        Object.Destroy(enemy.gameObject);
        yield return new WaitForSeconds(.3f);
        Assert.That(player.ActiveCount, Is.EqualTo(1));
        system.Clear();
        yield return new WaitForSeconds(.3f);
        Assert.That(player.ActiveCount, Is.Zero);
    }

    [UnityTest]
    public IEnumerator BattleUi_CastsPlayerMagicAndRestoresPauseSpeedAndExcludesRockModal()
    {
        _battleScene = true;
        yield return SceneManager.LoadSceneAsync("GafuTest", LoadSceneMode.Single);
        var flow = Object.FindAnyObjectByType<CombatFlow>(FindObjectsInactive.Include);
        var canvas = flow.GetComponentInParent<Canvas>(true);
        canvas.gameObject.SetActive(true); canvas.enabled = true; flow.gameObject.SetActive(true);
        yield return null;
        var map = Object.FindAnyObjectByType<CombatMapSystem>();
        float deadline = Time.realtimeSinceStartup + 30;
        while (!map.IsMapReady(map.AuthoredMap) && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(map.IsMapReady(map.AuthoredMap), Is.True);
        var selection = Object.FindAnyObjectByType<CombatCharacterSelection>(FindObjectsInactive.Include);
        var start = (Button)selection.GetType().GetField("_startBattleButton", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(selection);
        start.onClick.Invoke(); yield return null;
        var hud = Object.FindAnyObjectByType<CombatBattleHudView>(FindObjectsInactive.Include);
        hud.SetDebugUiVisible(true);
        var controls = hud.transform.Find("DebugBattlePanel/PlayerMagicControls");
        var view = hud.GetComponentInChildren<CombatPlayerMagicView>(true);
        var characters = Object.FindAnyObjectByType<CombatCharacterSystem>();
        foreach (Character character in Object.FindObjectsByType<Character>())
        {
            var brain = character.GetComponent<CombatAiBrain>();
            if (brain != null) brain.enabled = false;
            character.SkillCaster.ClearCast(); character.StopMoving();
        }
        Character enemy = characters.EnemyCharacters.Find(character => character != null && character.Health.IsTargetable);
        Assert.That(enemy, Is.Not.Null);
        foreach (float speed in new[] { 1f, 4f, 0f })
        {
            typeof(CombatFlow).GetField("_isPaused", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(flow, speed == 0);
            typeof(CombatFlow).GetField("_selectedBattleSpeed", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(flow, speed == 0 ? 2f : speed);
            Time.timeScale = speed;
            controls.Find("Area").GetComponent<Button>().onClick.Invoke();
            Assert.That(view.IsOpen, Is.True); Assert.That(Time.timeScale, Is.Zero);
            Assert.That(hud.OpenRockPlacement(CombatRockShape.Straight, map, characters), Is.False);
            view.Cancel(); Assert.That(Time.timeScale, Is.EqualTo(speed));
        }
        controls.Find("Area").GetComponent<Button>().onClick.Invoke();
        Vector3 local = map.MapOrigin.InverseTransformPoint(enemy.transform.position);
        Vector2 size = map.CurrentMap.Height.WorldSize;
        view.SelectPosition(new Vector2(local.x / size.x, local.z / size.y));
        view.transform.Find("Panel/Parameters/DamageRow/Damage").GetComponent<TMP_InputField>().text = "7";
        int hp = enemy.HP;
        view.Cast();
        Assert.That(view.IsOpen, Is.False); Assert.That(Time.timeScale, Is.Zero);
        Assert.That(enemy.HP, Is.EqualTo(hp));
        var cutIn = hud.GetComponentInChildren<CombatPlayerMagicCutInView>(true);
        Assert.That(cutIn.IsVisible, Is.True); Assert.That(cutIn.Caption, Is.EqualTo("広範囲魔法発動"));
        Assert.That(cutIn.GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
        yield return new WaitForSecondsRealtime(.2f);
        System.IO.Directory.CreateDirectory("/tmp/WarSimulation-player-magic");
        ScreenCapture.CaptureScreenshot("/tmp/WarSimulation-player-magic/cut-in.png");
        yield return new WaitForSecondsRealtime(1.1f);
        Assert.That(cutIn.IsVisible, Is.False); Assert.That(enemy.HP, Is.EqualTo(hp));
        typeof(CombatFlow).GetField("_isPaused", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(flow, false);
        typeof(CombatFlow).GetField("_selectedBattleSpeed", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(flow, 4f);
        Time.timeScale = 4;
        yield return new WaitForSeconds(1f);
        Assert.That(enemy.HP, Is.EqualTo(hp));
        yield return new WaitForSeconds(1.7f);
        Assert.That(enemy.HP, Is.EqualTo(hp - 7));
        Assert.That(Object.FindAnyObjectByType<SkillVfxPlayer>().ActiveCount, Is.GreaterThan(0));
        typeof(CombatFlow).GetField("_isPaused", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(flow, true);
        Time.timeScale = 0;
        controls.Find("AllInvulnerable").GetComponent<Button>().onClick.Invoke();
        view.Cast();
        Assert.That(cutIn.IsVisible, Is.True); Assert.That(cutIn.Caption, Is.EqualTo("全員無敵発動"));
        foreach (Character character in characters.AllyCharacters)
            if (CombatPlayerMagicSystem.CanReceiveInvulnerability(character)) Assert.That(character.StatusEffects.IsInvulnerable, Is.True);
        foreach (Character character in characters.EnemyCharacters)
            if (CombatPlayerMagicSystem.CanReceiveInvulnerability(character)) Assert.That(character.StatusEffects.IsInvulnerable, Is.True);
        controls.Find("Area").GetComponent<Button>().onClick.Invoke();
        System.IO.Directory.CreateDirectory("/tmp/WarSimulation-player-magic");
        ScreenCapture.CaptureScreenshot("/tmp/WarSimulation-player-magic/runtime-map.png");
        yield return null;
        yield return null;
        view.transform.Find("Panel/Parameters/RadiusRow/Radius").GetComponent<TMP_InputField>().text = "0";
        view.Cast(); Assert.That(view.IsOpen, Is.True);
        view.transform.Find("Panel/Parameters/RadiusRow/Radius").GetComponent<TMP_InputField>().text = "10";
        view.Cast();
        hp = enemy.HP;
        Assert.That(cutIn.Caption, Is.EqualTo("広範囲魔法発動"));
        map.SetCurrentMap(null);
        Assert.That(view.IsOpen, Is.False); Assert.That(cutIn.IsVisible, Is.False);
        Time.timeScale = 4;
        yield return new WaitForSeconds(2.7f);
        Assert.That(enemy.HP, Is.EqualTo(hp));
        foreach (Character character in characters.AllyCharacters)
            if (character != null) Assert.That(character.StatusEffects.HasActiveEffect(CombatPlayerMagicSystem.InvulnerableKey), Is.False);
    }
}
