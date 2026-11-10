using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using WarSimulation.Combat.Map;
using Object = UnityEngine.Object;

[Category("PlayerMagic")]
public sealed class CombatPlayerMagicTests
{
    private readonly List<Object> _objects = new();
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private T Track<T>(T item) where T : Object { _objects.Add(item); return item; }
    [TearDown]
    public void TearDown()
    {
        foreach (Object item in _objects)
            if (item is GameObject go && go.TryGetComponent(out Camera camera)) camera.targetTexture = null;
        for (int i = _objects.Count - 1; i >= 0; i--) if (_objects[i] != null) Object.DestroyImmediate(_objects[i]);
        _objects.Clear(); Time.timeScale = 1;
    }
    private Character Character(CombatCharacterSystem system, CombatTeam team, Vector3 position)
    {
        var character = Track(new GameObject("Character")).AddComponent<Character>();
        character.GetComponent<UnityEngine.AI.NavMeshAgent>().enabled = false;
        character.SetTeam(team); character.Health.Initialize(100); character.transform.position = position;
        (team == CombatTeam.Ally ? system.AllyCharacters : system.EnemyCharacters).Add(character);
        return character;
    }
    private CombatCharacterSystem Characters() => Track(new GameObject("Participants")).AddComponent<CombatCharacterSystem>();
    private CombatMapSystem Map()
    {
        var map = Track(new GameObject("Map")).AddComponent<CombatMapSystem>();
        map.SetCurrentMap(new MapData(new HeightMap(60, 60, 1), new GroundStateGrid(60, 60, 1), 1));
        return map;
    }
    private TMP_FontAsset Font() => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
        "Assets/Fonts/Noto_Sans_JP/static/NotoSansJP-Regular SDF.asset");

    [Test]
    public void Area_UsesXZBoundaryAndEnemyOnlyWithPlayerDamageSource()
    {
        var characters = Characters();
        Character ally = Character(characters, CombatTeam.Ally, Vector3.zero);
        Character boundary = Character(characters, CombatTeam.Enemy, new Vector3(10, 50, 0));
        Character outside = Character(characters, CombatTeam.Enemy, new Vector3(10.01f, 0, 0));
        Character immune = Character(characters, CombatTeam.Enemy, Vector3.zero);
        Character reduced = Character(characters, CombatTeam.Enemy, Vector3.zero);
        Character retreating = Character(characters, CombatTeam.Enemy, Vector3.zero);
        immune.StatusEffects.ApplyInvulnerable(10);
        reduced.StatusEffects.ApplyDamageReduction(.5f, 10);
        retreating.Health.EnterRetreat();
        CombatEffectSource source = default;
        void Capture(CombatDamageEvent damage) { if (damage.Target == boundary) source = damage.AttackSource; }
        CombatDamageEvents.Resolved += Capture;
        try
        {
            var system = new CombatPlayerMagicSystem(characters);
            var targets = system.Execute(new CombatPlayerMagicRequest(CombatPlayerMagicKind.Area, Vector3.zero, 10, 40, 0));
            Assert.That(boundary.HP, Is.EqualTo(60));
            Assert.That(outside.HP, Is.EqualTo(100));
            Assert.That(ally.HP, Is.EqualTo(100));
            Assert.That(immune.HP, Is.EqualTo(100));
            Assert.That(reduced.HP, Is.EqualTo(80));
            Assert.That(targets.Contains(retreating), Is.False);
            Assert.That(source.SkillId, Is.EqualTo(SkillId.Player_AreaMagic));
            Assert.That(source.Character, Is.Null);
        }
        finally { CombatDamageEvents.Resolved -= Capture; }
    }

    [Test]
    public void Invulnerability_IncludesBothTeamsAndRetreatingAndRefreshesOnlyItsOwnKey()
    {
        var characters = Characters();
        Character ally = Character(characters, CombatTeam.Ally, Vector3.zero);
        Character enemy = Character(characters, CombatTeam.Enemy, Vector3.one);
        Character retreating = Character(characters, CombatTeam.Enemy, Vector3.one);
        Character withdrawn = Character(characters, CombatTeam.Ally, Vector3.one);
        retreating.Health.EnterRetreat(); withdrawn.Health.WithdrawFromBattle();
        ally.StatusEffects.ApplyInvulnerable(30, "BibleInvulnerable", ally);
        var system = new CombatPlayerMagicSystem(characters);
        var request = new CombatPlayerMagicRequest(CombatPlayerMagicKind.AllInvulnerable, Vector3.zero, 0, 0, 10);
        var targets = system.Execute(request);
        CollectionAssert.AreEquivalent(new[] { ally, enemy, retreating }, targets);
        Assert.That(CombatPlayerMagicSystem.CountInvulnerabilityTargets(characters), Is.EqualTo(3));
        Assert.That(enemy.Health.TakeDamage(40), Is.Zero);
        system.Execute(new CombatPlayerMagicRequest(CombatPlayerMagicKind.AllInvulnerable, Vector3.zero, 0, 0, 2));
        Assert.That(ally.StatusEffects.GetRemainingSeconds(CombatPlayerMagicSystem.InvulnerableKey), Is.EqualTo(2).Within(.01));
        system.Clear();
        Assert.That(enemy.StatusEffects.IsInvulnerable, Is.False);
        Assert.That(ally.StatusEffects.HasActiveEffect("BibleInvulnerable"), Is.True);
    }

    [TestCase(0)] [TestCase(-1)] [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)]
    public void InvalidValues_DoNotApplyAnyEffects(float value)
    {
        var characters = Characters();
        Character enemy = Character(characters, CombatTeam.Enemy, Vector3.zero);
        var system = new CombatPlayerMagicSystem(characters);
        Assert.That(system.Execute(new CombatPlayerMagicRequest(CombatPlayerMagicKind.Area, Vector3.zero, value, 40, 0)), Is.Empty);
        Assert.That(system.Execute(new CombatPlayerMagicRequest(CombatPlayerMagicKind.AllInvulnerable, Vector3.zero, 0, 0, value)), Is.Empty);
        Assert.That(enemy.HP, Is.EqualTo(100)); Assert.That(enemy.StatusEffects.IsInvulnerable, Is.False);
    }

    [Test]
    public void Modal_CommitsLastCastOnlyAndValidatesEditableParameters()
    {
        var canvas = Track(new GameObject("Canvas", typeof(RectTransform), typeof(Canvas)));
        var view = CombatPlayerMagicView.Create(canvas.transform, Font());
        var map = Map();
        int casts = 0; view.CastRequested += _ => casts++;
        view.Open(CombatPlayerMagicKind.Area, map, null);
        Assert.That(view.TryGetRequest(out var initial), Is.True);
        Assert.That(initial.Radius, Is.EqualTo(10)); Assert.That(initial.Damage, Is.EqualTo(40));
        view.SelectPosition(new Vector2(.2f, .7f));
        Field(view, "Radius").text = "12.5"; Field(view, "Damage").text = "55";
        view.Cast();
        Assert.That(casts, Is.EqualTo(1)); Assert.That(view.IsOpen, Is.False);
        view.Open(CombatPlayerMagicKind.Area, map, null);
        Assert.That(view.TryGetRequest(out var previous), Is.True);
        Assert.That(previous.Center, Is.EqualTo(new Vector3(12, 0, 42)));
        Assert.That(previous.Radius, Is.EqualTo(12.5)); Assert.That(previous.Damage, Is.EqualTo(55));
        Field(view, "Damage").text = "0"; view.Cast();
        Assert.That(casts, Is.EqualTo(1)); Assert.That(view.IsOpen, Is.True);
        Field(view, "Radius").text = "3"; view.Cancel();
        view.Open(CombatPlayerMagicKind.Area, map, null);
        Assert.That(view.TryGetRequest(out var preserved), Is.True); Assert.That(preserved.Radius, Is.EqualTo(12.5));
        view.Cancel();
        view.Open(CombatPlayerMagicKind.AllInvulnerable, map, null);
        Assert.That(view.TryGetRequest(out var invulnerable), Is.True); Assert.That(invulnerable.Seconds, Is.EqualTo(10));
        Field(view, "Seconds").text = "2.5"; view.Cast();
        view.Clear(); view.Open(CombatPlayerMagicKind.AllInvulnerable, map, null);
        Assert.That(view.TryGetRequest(out invulnerable), Is.True); Assert.That(invulnerable.Seconds, Is.EqualTo(10));
    }
    private static TMP_InputField Field(CombatPlayerMagicView view, string name) =>
        view.transform.Find($"Panel/Parameters/{name}Row/{name}").GetComponent<TMP_InputField>();

    [TestCase(1920, 1080)] [TestCase(1280, 720)]
    public void Modal_RendersRotatedRangeAndDragsInWorldCoordinates(int width, int height)
    {
        var camera = Track(new GameObject("UI camera")).AddComponent<Camera>();
        camera.targetTexture = Track(new RenderTexture(width, height, 24));
        camera.cullingMask = 1 << 31;
        var battleCamera = Track(new GameObject("Battle camera")).AddComponent<Camera>();
        battleCamera.transform.rotation = Quaternion.Euler(50, 45, 0);
        var canvas = Track(new GameObject("Canvas", typeof(RectTransform))).AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
        var raycaster = canvas.gameObject.AddComponent<GraphicRaycaster>();
        var view = CombatPlayerMagicView.Create(canvas.transform, Font());
        var map = Map();
        view.Open(CombatPlayerMagicKind.Area, map, null, battleCamera);
        foreach (Transform child in canvas.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 31;
        Canvas.ForceUpdateCanvases(); camera.Render();
        var preview = view.GetComponentInChildren<CombatBattleMapView>();
        var range = preview.ImageRect.Find("Range").GetComponent<CombatMagicRangeGraphic>();
        Assert.That(range.canvasRenderer.GetMesh().vertexCount, Is.GreaterThan(0));
        Assert.That(range.rectTransform.rect.width / preview.Scale, Is.EqualTo(20).Within(.01));
        var events = Track(new GameObject("Events")).AddComponent<EventSystem>();
        var pointer = new PointerEventData(events) { pointerPressRaycast = new RaycastResult { module = raycaster } };
        pointer.position = camera.WorldToScreenPoint(preview.ImageRect.TransformPoint(Vector3.zero));
        preview.OnPointerDown(pointer);
        Vector2 chosen = new(.2f, .7f);
        pointer.position = camera.WorldToScreenPoint(preview.ImageRect.TransformPoint(
            preview.ImageRect.rect.min + Vector2.Scale(chosen, preview.ImageRect.rect.size)));
        preview.OnDrag(pointer);
        Assert.That(view.TryGetRequest(out var request), Is.True);
        Assert.That(Vector3.Distance(request.Center, new Vector3(12, 0, 42)), Is.LessThan(.01));
        pointer.position = camera.WorldToScreenPoint(preview.ImageRect.TransformPoint(new Vector3(preview.ImageRect.rect.xMax + 50, 0)));
        preview.OnDrag(pointer);
        Assert.That(view.TryGetRequest(out var unchanged), Is.True); Assert.That(unchanged.Center, Is.EqualTo(request.Center));
        preview.OnPointerUp(pointer);
        camera.Render();
        var screenshot = Track(new Texture2D(width, height, TextureFormat.RGB24, false));
        RenderTexture previous = RenderTexture.active; RenderTexture.active = camera.targetTexture;
        screenshot.ReadPixels(new Rect(0, 0, width, height), 0, 0); screenshot.Apply(); RenderTexture.active = previous;
        System.IO.Directory.CreateDirectory("/tmp/WarSimulation-player-magic");
        System.IO.File.WriteAllBytes($"/tmp/WarSimulation-player-magic/modal-{width}.png", screenshot.EncodeToPNG());
    }

    [Test]
    public void AreaCast_UsesGodHandDurationAndCancellationDoesNotRemoveReusedEffects()
    {
        Assert.That(CombatPlayerMagicSettings.CastSeconds(SkillId.Player_AreaMagic), Is.EqualTo(new WandGodsHandSkill().CastTimeSeconds));
        Assert.That(CombatPlayerMagicSettings.CastSeconds(SkillId.Player_AllInvulnerable), Is.Zero);
        var player = Track(new GameObject("VFX player")).AddComponent<SkillVfxPlayer>();
        var request = new CombatPlayerMagicRequest(CombatPlayerMagicKind.Area, Vector3.zero, 10, 40, 0);
        long oldCast = player.BeginPlayerMagicCast(request);
        Assert.That(oldCast, Is.LessThan(0));
        player.ClearAll();
        player.TryPlay(SkillId.Wand_Bolt, Vector3.zero, Vector3.one, null, out _);
        player.CancelPlayerMagicCast(oldCast);
        Assert.That(player.ActiveCount, Is.EqualTo(1));
        long currentCast = player.BeginPlayerMagicCast(request);
        Assert.That(player.ActiveCount, Is.EqualTo(2));
        player.CancelPlayerMagicCast(currentCast);
        Assert.That(player.ActiveCount, Is.EqualTo(1));
    }

    [Test]
    public void AudioDefaults_CopyOnceAndRemainIndependentAndOutsideCharacterCatalog()
    {
        var settings = Track(ScriptableObject.CreateInstance<CombatAudioSettings>());
        var clip = Track(AudioClip.Create("Test", 100, 1, 44100, false));
        settings.Skills.Add(new CombatAudioSettings.SkillSound { Skill = SkillId.Wand_GodsHand, Clip = clip, Volume = .4f });
        settings.Skills.Add(new CombatAudioSettings.SkillSound { Skill = SkillId.Bible_Invulnerable, Clip = clip, Volume = .7f });
        Assert.That(settings.InitializePlayerMagicSounds(), Is.True);
        Assert.That(settings.GetClip(SkillId.Player_AreaMagic), Is.SameAs(clip));
        Assert.That(settings.GetVolume(SkillId.Player_AllInvulnerable), Is.EqualTo(.7f));
        settings.Skills.Find(x => x.Skill == SkillId.Player_AreaMagic).Volume = .2f;
        Assert.That(settings.InitializePlayerMagicSounds(), Is.False);
        Assert.That(settings.GetVolume(SkillId.Player_AreaMagic), Is.EqualTo(.2f));
        Assert.That(settings.GetVolume(SkillId.Wand_GodsHand), Is.EqualTo(.4f));
        var catalog = Track(CombatSkillCatalog.CreateDefaultRuntimeCatalog());
        Assert.That(catalog.TryGetDefinition(SkillId.Player_AreaMagic, out _), Is.False);
        Assert.That(CombatSkillFactory.Create(SkillId.Player_AllInvulnerable), Is.Null);
    }
}
