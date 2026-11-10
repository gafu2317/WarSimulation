using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using WarSimulation.Combat.Map;
using Object = UnityEngine.Object;

[Category("RockPlacement")]
public sealed class CombatRockPlacementTests
{
    private readonly List<Object> _created = new();
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [TearDown]
    public void TearDown()
    {
        foreach (Object item in _created)
            if (item is GameObject go && go.TryGetComponent(out Camera camera)) camera.targetTexture = null;
        for (int i = _created.Count - 1; i >= 0; i--)
            if (_created[i] != null) Object.DestroyImmediate(_created[i]);
        _created.Clear();
        Time.timeScale = 1;
    }

    [TestCase(-100, -50, 0, 0)]
    [TestCase(100, -50, 1, 0)]
    [TestCase(-100, 50, 0, 1)]
    [TestCase(100, 50, 1, 1)]
    [TestCase(0, 0, 0.5f, 0.5f)]
    public void MapClick_MapsCornersAndCenterWithoutFlippingNorth(float x, float y, float u, float v)
    {
        Assert.That(CombatRockPlacementView.TryNormalizePoint(new Rect(-100, -50, 200, 100),
            new Vector2(x, y), out Vector2 normalized), Is.True);
        Assert.That(normalized, Is.EqualTo(new Vector2(u, v)));
    }

    [TestCase(-101, 0)]
    [TestCase(0, 51)]
    public void MapClick_RejectsLetterboxMargins(float x, float y)
    {
        Assert.That(CombatRockPlacementView.TryNormalizePoint(new Rect(-100, -50, 200, 100),
            new Vector2(x, y), out _), Is.False);
    }

    [Test]
    public void Selection_ConfirmsOnlyOnePlacementAndCancelPreservesItWithoutChangingMap()
    {
        CombatMapSystem map = CreateMap();
        var view = CreateView();
        int closed = 0;
        view.Closed += () => closed++;
        Assert.That(view.Open(CombatRockShape.Bent, map, null), Is.True);
        Assert.That(view.IsOpen, Is.True);
        Assert.That(view.ConfirmedPlacement, Is.Null);
        var initialCursor = (RectTransform)view.transform.Find("Panel/MapArea/Map/Selection");
        Assert.That(initialCursor.gameObject.activeSelf, Is.True);
        Assert.That(initialCursor.anchorMin, Is.EqualTo(Vector2.one * 0.5f));
        Assert.That(view.transform.Find("Panel/Actions/Confirm").GetComponent<Button>().interactable, Is.True);
        view.SelectPosition(new Vector2(0.5f, 0.25f));
        view.Rotate();
        view.Confirm();
        Assert.That(view.IsOpen, Is.False);
        Assert.That(view.ConfirmedPlacement.Value.Shape, Is.EqualTo(CombatRockShape.Bent));
        Assert.That(view.ConfirmedPlacement.Value.Position, Is.EqualTo(new Vector2(30, 15)));
        Assert.That(view.ConfirmedPlacement.Value.QuarterTurns, Is.EqualTo(1));
        Assert.That(closed, Is.EqualTo(1));
        Assert.That(view.transform.Find("Panel/MapArea/Map").GetComponent<RawImage>().texture, Is.Null);

        view.Open(CombatRockShape.Bent, map, null);
        view.Confirm();
        Assert.That(view.ConfirmedPlacement.Value.QuarterTurns, Is.EqualTo(1));
        view.Open(CombatRockShape.U, map, null);
        view.SelectPosition(Vector2.one);
        view.Rotate();
        view.Cancel();
        Assert.That(view.ConfirmedPlacement.Value.Position, Is.EqualTo(new Vector2(30, 15)));
        view.Open(CombatRockShape.Straight, map, null);
        view.Confirm();
        Assert.That(view.ConfirmedPlacement.Value.Shape, Is.EqualTo(CombatRockShape.Straight));
        Assert.That(view.ConfirmedPlacement.Value.QuarterTurns, Is.Zero);
        view.Open(CombatRockShape.Circle, map, null);
        view.Rotate();
        view.SelectPosition(Vector2.one);
        view.Confirm();
        Assert.That(view.ConfirmedPlacement.Value.Position, Is.EqualTo(new Vector2(60, 60)));
        Assert.That(view.ConfirmedPlacement.Value.QuarterTurns, Is.Zero);
        Assert.That(map.CurrentMap.Features.Count, Is.Zero);
        Assert.That(map.CurrentMap.Height.GetHeight(0, 0), Is.Zero);
        Assert.That(map.GetComponentsInChildren<Collider>(), Is.Empty);
        view.Clear();
        Assert.That(view.ConfirmedPlacement, Is.Null);
    }

    [Test]
    public void Markers_IncludeRetreatingParticipantsAndRespectMapOriginAndBounds()
    {
        CombatMapSystem map = CreateMap();
        map.transform.position = new Vector3(100, 0, 200);
        var characters = Track(new GameObject("Participants")).AddComponent<CombatCharacterSystem>();
        Character ally = Track(new GameObject("Ally")).AddComponent<Character>();
        ally.transform.position = new Vector3(115, 0, 230);
        ally.Health.Initialize(30, 30);
        ally.Health.EnterRetreat();
        Character outside = Track(new GameObject("Outside")).AddComponent<Character>();
        outside.transform.position = new Vector3(99, 0, 200);
        characters.AllyCharacters.Add(ally);
        characters.EnemyCharacters.Add(outside);
        var view = CreateView();
        view.Open(CombatRockShape.Straight, map, characters);
        Transform image = view.transform.Find("Panel/MapArea/Map");
        Transform marker = image.Find("CharacterMarker");
        Assert.That(marker, Is.Not.Null);
        Assert.That(image.childCount, Is.EqualTo(2));
        Assert.That(((RectTransform)marker).anchorMin, Is.EqualTo(new Vector2(0.25f, 0.5f)));
        view.SelectPosition(new Vector2(0.5f, 0.5f));
        view.Confirm();
        Assert.That(view.ConfirmedPlacement.Value.Position, Is.EqualTo(new Vector2(130, 230)));
        Assert.That(image.childCount, Is.EqualTo(1));
    }

    [TestCase(1, false)]
    [TestCase(4, false)]
    [TestCase(2, true)]
    public void Modal_RestoresOriginalPauseAndSpeed(float speed, bool paused)
    {
        CombatMapSystem map = CreateMap();
        var battle = Track(new GameObject("Battle")).AddComponent<CombatBattleFlow>();
        Set(battle, "_state", CombatBattleState.Running);
        typeof(CombatBattleFlow).GetField("s_instance", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, battle);
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Combat/BattleUI.prefab");
        var hudObject = Track(Object.Instantiate(prefab));
        var hud = hudObject.GetComponent<CombatBattleHudView>();
        hud.EnsureBuilt();
        hud.SetDebugUiVisible(true);
        hud.SetControlsVisible(true);
        var flow = Track(new GameObject("Flow")).AddComponent<CombatFlow>();
        Set(flow, "_mapSystem", map);
        Set(flow, "_battleFlow", battle);
        Set(flow, "_battleHudView", hud);
        Set(flow, "_selectedBattleSpeed", speed);
        Set(flow, "_isPaused", paused);
        Invoke(flow, "EnsureBattleControls");
        hud.SetControlsVisible(true);
        Time.timeScale = paused ? 0 : speed;
        Transform controls = hud.transform.Find("DebugBattlePanel/RockPlacementControls");
        Assert.That(controls.childCount, Is.EqualTo(4));
        hud.EnsureBuilt();
        Assert.That(controls.childCount, Is.EqualTo(4));
        controls.Find("RockU").GetComponent<Button>().onClick.Invoke();
        var modal = hud.GetComponentInChildren<CombatRockPlacementView>(true);
        Assert.That(modal.IsOpen, Is.True);
        Assert.That(Time.timeScale, Is.Zero);
        modal.Cancel();
        Assert.That(Time.timeScale, Is.EqualTo(paused ? 0 : speed));
        controls.Find("RockCircle").GetComponent<Button>().onClick.Invoke();
        modal.SelectPosition(Vector2.one * 0.5f);
        modal.Confirm();
        Assert.That(flow.ConfirmedRockPlacement, Is.Null);
        Assert.That(modal.IsOpen, Is.True);
        Assert.That(Time.timeScale, Is.Zero);
        map.SetCurrentMap(null);
        Assert.That(modal.IsOpen, Is.False);
        Assert.That(flow.ConfirmedRockPlacement, Is.Null);
        Assert.That(Time.timeScale, Is.EqualTo(paused ? 0 : speed));
        hud.SetControlsVisible(false);
        Assert.That(flow.ConfirmedRockPlacement, Is.Null);
        Assert.That(controls.gameObject.activeSelf, Is.False);
    }

    [TestCase(1920, 1080, 0, 0)]
    [TestCase(1280, 720, 0, 0)]
    [TestCase(1920, 1080, 45, 0)]
    [TestCase(1280, 720, 180, 0)]
    [TestCase(1280, 720, 90, 30)]
    public void Modal_RendersAndAcceptsClicksAtSupportedResolutions(int width, int height, float heading, float mapHeading)
    {
        CombatMapSystem map = CreateMap();
        foreach (string guid in AssetDatabase.FindAssets("t:AuthoredMapDefinition"))
        {
            var definition = AssetDatabase.LoadAssetAtPath<AuthoredMapDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (!definition.HasValidBakedMapData) continue;
            map.SetCurrentMap(definition.BakedMapData.CreateRuntimeMap());
            break;
        }
        var camera = Track(new GameObject("PreviewCamera")).AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        camera.cullingMask = 1 << 31;
        var target = Track(new RenderTexture(width, height, 24));
        camera.targetTexture = target;
        var canvas = Track(new GameObject("Canvas", typeof(RectTransform))).AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera;
        canvas.planeDistance = 1;
        var raycaster = canvas.gameObject.AddComponent<GraphicRaycaster>();
        var view = CombatRockPlacementView.Create(canvas.transform, Font());
        var characters = Track(new GameObject("Participants")).AddComponent<CombatCharacterSystem>();
        Vector2 mapSize = map.CurrentMap.Height.WorldSize;
        for (int i = 0; i < 2; i++)
        {
            var character = Track(new GameObject(i == 0 ? "Ally" : "Enemy")).AddComponent<Character>();
            character.transform.position = map.MapLocalToSurfaceWorldPosition(
                new Vector3(mapSize.x * (i == 0 ? 0.25f : 0.75f), 0, mapSize.y * 0.5f));
            (i == 0 ? characters.AllyCharacters : characters.EnemyCharacters).Add(character);
        }
        map.transform.rotation = Quaternion.Euler(0, mapHeading, 0);
        var battleCamera = Track(new GameObject("BattleCamera")).AddComponent<Camera>();
        battleCamera.transform.rotation = Quaternion.Euler(50, heading, 0);
        view.Open(CombatRockShape.U, map, characters, battleCamera);
        view.SelectPosition(new Vector2(0.4f, 0.55f));
        foreach (Transform child in canvas.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 31;
        Canvas.ForceUpdateCanvases();
        RectTransform mapRect = (RectTransform)view.transform.Find("Panel/MapArea/Map");
        Assert.That(mapRect.rect.width, Is.GreaterThan(100));
        Assert.That(mapRect.rect.width / mapRect.rect.height,
            Is.EqualTo(map.CurrentMap.Height.WorldSize.x / map.CurrentMap.Height.WorldSize.y).Within(0.01));
        Assert.That(Mathf.DeltaAngle(mapRect.localEulerAngles.z, heading - mapHeading), Is.EqualTo(0).Within(0.01));
        var corners = new Vector3[4];
        mapRect.GetWorldCorners(corners);
        var area = (RectTransform)mapRect.parent;
        foreach (Vector3 corner in corners)
        {
            Vector3 local = area.InverseTransformPoint(corner);
            Assert.That(local.x, Is.InRange(area.rect.xMin - 0.01f, area.rect.xMax + 0.01f));
            Assert.That(local.y, Is.InRange(area.rect.yMin - 0.01f, area.rect.yMax + 0.01f));
        }
        camera.Render();
        var selection = view.transform.Find("Panel/MapArea/Map/Selection").GetComponent<CombatRockShapeGraphic>();
        Assert.That(selection.canvasRenderer.GetMesh().vertexCount, Is.GreaterThan(0));
        var screenshot = Track(new Texture2D(width, height, TextureFormat.RGB24, false));
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = target;
        screenshot.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        screenshot.Apply();
        RenderTexture.active = previous;
        Directory.CreateDirectory("/tmp/WarSimulation-rock-ui");
        File.WriteAllBytes($"/tmp/WarSimulation-rock-ui/modal-{width}x{height}-{heading}-{mapHeading}.png", screenshot.EncodeToPNG());
        Vector2 chosen = new(0.2f, 0.7f);
        Vector2 mapLocal = mapRect.rect.min + Vector2.Scale(chosen, mapRect.rect.size);
        Vector3 screen = camera.WorldToScreenPoint(mapRect.TransformPoint(mapLocal));
        var eventSystem = Track(new GameObject("EventSystem")).AddComponent<UnityEngine.EventSystems.EventSystem>();
        var click = new UnityEngine.EventSystems.PointerEventData(eventSystem)
        {
            position = screen,
            pointerPressRaycast = new UnityEngine.EventSystems.RaycastResult { module = raycaster },
        };
        view.OnInitializePotentialDrag(click);
        Assert.That(click.useDragThreshold, Is.False);
        click.position = camera.WorldToScreenPoint(mapRect.TransformPoint(mapRect.rect.center));
        view.OnPointerDown(click);
        Assert.That(Vector2.Distance(selection.rectTransform.anchorMin, Vector2.one * 0.5f), Is.LessThan(0.001f));
        click.position = screen;
        view.OnDrag(click);
        Assert.That(Vector2.Distance(selection.rectTransform.anchorMin, chosen), Is.LessThan(0.001f));
        click.position = camera.WorldToScreenPoint(mapRect.TransformPoint(new Vector2(mapRect.rect.xMax + 10, 0)));
        view.OnDrag(click);
        Assert.That(Vector2.Distance(selection.rectTransform.anchorMin, chosen), Is.LessThan(0.001f));
        click.position = screen;
        view.OnDrag(click);
        view.OnPointerUp(click);
        click.position = camera.WorldToScreenPoint(mapRect.TransformPoint(mapRect.rect.center));
        view.OnDrag(click);
        Assert.That(Vector2.Distance(selection.rectTransform.anchorMin, chosen), Is.LessThan(0.001f));
        view.Rotate();
        Assert.That(Mathf.DeltaAngle(selection.rectTransform.localEulerAngles.z, -90), Is.EqualTo(0).Within(0.01));
        view.Confirm();
        Vector3 expected = map.MapLocalToSurfaceWorldPosition(new Vector3(mapSize.x * chosen.x, 0, mapSize.y * chosen.y));
        Assert.That(Vector2.Distance(view.ConfirmedPlacement.Value.Position, new Vector2(expected.x, expected.z)), Is.LessThan(0.1f));
        battleCamera.transform.rotation = Quaternion.Euler(50, heading + 90, 0);
        view.Open(CombatRockShape.U, map, characters, battleCamera);
        Assert.That(Mathf.DeltaAngle(mapRect.localEulerAngles.z, heading + 90 - mapHeading), Is.EqualTo(0).Within(0.01));
        Assert.That(Vector2.Distance(selection.rectTransform.anchorMin, chosen), Is.LessThan(0.001f));
    }

    private CombatMapSystem CreateMap()
    {
        var system = Track(new GameObject("Map")).AddComponent<CombatMapSystem>();
        system.SetCurrentMap(new MapData(new HeightMap(60, 60, 1), new GroundStateGrid(60, 60, 1), 1));
        return system;
    }

    private CombatRockPlacementView CreateView()
    {
        var canvas = Track(new GameObject("Canvas", typeof(RectTransform), typeof(Canvas)));
        return CombatRockPlacementView.Create(canvas.transform, Font());
    }

    private static TMP_FontAsset Font() => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
        "Assets/Fonts/Noto_Sans_JP/static/NotoSansJP-Regular SDF.asset");
    private T Track<T>(T value) where T : Object { _created.Add(value); return value; }
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
    private static void Invoke(object target, string name) => target.GetType().GetMethod(name, Private).Invoke(target, null);
}
