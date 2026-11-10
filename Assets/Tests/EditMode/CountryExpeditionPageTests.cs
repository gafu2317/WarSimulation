using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class CountryExpeditionPageTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private Scene _scene;
    private GameObject _hud, _stateRoot;
    private Camera _camera;
    private RenderTexture _target;
    private CountryExpeditionPanel _panel;
    private CountryRosterView _view;
    private CountryRoster _roster;
    private CountryExpeditionState _state;
    private ScrollRect _page, _list, _members;
    private int _destination;

    [SetUp]
    public void SetUp()
    {
        Assert.That(EditorApplication.isPlaying, Is.False);
        _scene = EditorSceneManager.NewPreviewScene();
        _target = RenderTexture.GetTemporary(2532, 1170, 24);
        var cameraRoot = new GameObject("Expedition page test camera", typeof(Camera));
        SceneManager.MoveGameObjectToScene(cameraRoot, _scene);
        _camera = cameraRoot.GetComponent<Camera>(); _camera.scene = _scene; _camera.targetTexture = _target;
        _camera.aspect = 2532f / 1170; _camera.transform.position = new Vector3(0, 0, -10);
        _camera.clearFlags = CameraClearFlags.SolidColor; _camera.backgroundColor = CountryGovernmentTheme.Background;
        _stateRoot = new GameObject("Expedition page test state"); SceneManager.MoveGameObjectToScene(_stateRoot, _scene);
        var date = _stateRoot.AddComponent<DateManager>();
        var resources = _stateRoot.AddComponent<PlayDataManager>();
        _hud = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Country/UI/CountryHud.prefab"), _scene);
        var canvas = _hud.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = _camera; canvas.planeDistance = 5;
        _panel = _hud.GetComponentInChildren<CountryExpeditionPanel>(true);
        Call(_panel, "Awake");
        var previousDate = DateManager.Instance; var previousResources = PlayDataManager.Instance;
        try
        {
            typeof(ManagerBase<DateManager>).GetProperty("Instance").SetValue(null, date);
            typeof(ManagerBase<PlayDataManager>).GetProperty("Instance").SetValue(null, resources);
            Call(_panel, "Start");
        }
        finally
        {
            typeof(ManagerBase<DateManager>).GetProperty("Instance").SetValue(null, previousDate);
            typeof(ManagerBase<PlayDataManager>).GetProperty("Instance").SetValue(null, previousResources);
        }
        _roster = resources.Roster; _state = resources.Expeditions;
        _view = Field<CountryRosterView>(_panel, "_view");
        _page = Field<ScrollRect>(_panel, "_page"); _list = Field<ScrollRect>(_view, "_list");
        _members = Field<RectTransform>(_panel, "_members").parent.parent.GetComponent<ScrollRect>();
        Call(Field<CountryRosterConfirm>(_panel, "_confirmation"), "Awake");
        Call(Field<CountryRosterConfirm>(_panel, "_logDetails"), "Awake");
        _panel.Open(); Layout();
        _destination = Enumerable.Range(0, _state.Data.Regions.Length).First(_state.CanTarget);
    }

    [TearDown]
    public void TearDown()
    {
        if (_panel != null) _panel.Close();
        EditorSceneManager.ClosePreviewScene(_scene);
        RenderTexture.ReleaseTemporary(_target);
    }

    [Test]
    public void MapAndFormationRemainInTheSameScrollablePage()
    {
        var map = (RectTransform)_page.content.Find("Map");
        var formation = (RectTransform)_page.content.Find("Formation");
        Assert.That(map.gameObject.activeInHierarchy && formation.gameObject.activeInHierarchy, Is.True);
        Assert.That(_page.content.rect.height, Is.GreaterThan(_page.viewport.rect.height));
        var header = _panel.transform.Find("Window/Shell/Header");
        Assert.That(header.Find("Map"), Is.Null); Assert.That(header.Find("Formation"), Is.Null);
        Assert.That(_page.verticalNormalizedPosition, Is.EqualTo(1).Within(.001f));
        foreach (var relay in formation.GetComponentsInChildren<CountryNestedScrollInput>(true))
            Assert.That(Field<ScrollRect>(relay, "_outer"), Is.EqualTo(_page));
    }

    [Test]
    public void ScrollingAndEquipmentChangesPreserveTheDraftAndActualEquipment()
    {
        _panel.SelectRegion(_destination); AddFirstMember();
        int original = _roster.EquippedWeapon(0);
        _page.verticalNormalizedPosition = 0; Layout();
        Field<Button>(_panel, "_equipment").onClick.Invoke();
        _list.content.Find("Row02").GetComponent<Button>().onClick.Invoke(); Field<Button>(_view, "_action").onClick.Invoke();
        Assert.That(_panel.Draft.Target, Is.EqualTo(_destination));
        Assert.That(_panel.Draft.Members, Is.EqualTo(new[] { 0 }));
        Assert.That(_panel.Draft.Weapon(0), Is.EqualTo(1));
        Assert.That(_roster.EquippedWeapon(0), Is.EqualTo(original));
        Assert.That(_page.verticalNormalizedPosition, Is.EqualTo(0).Within(.001f));
        var context = Field<TextMeshProUGUI>(_panel, "_context").text;
        Assert.That(context, Does.Contain(_state.Data.Regions[_destination].Name).And.Contain("到着").And.Contain("帰還").And.Contain("1人"));
        _page.verticalNormalizedPosition = 1; Layout();
        Assert.That(_panel.Draft.Contains(0), Is.True);
    }

    [Test]
    public void ClosingAndReopeningDiscardsTheDraftAndReturnsToTheMap()
    {
        _panel.SelectRegion(_destination); AddFirstMember();
        _page.verticalNormalizedPosition = 0;
        _panel.Close(); _panel.Open(); Layout();
        Assert.That(_panel.Draft.Target, Is.EqualTo(-1)); Assert.That(_panel.Draft.Members, Is.Empty);
        Assert.That(_page.verticalNormalizedPosition, Is.EqualTo(1).Within(.001f));
        Assert.That(Field<ScrollRect>(_view, "_details").verticalNormalizedPosition, Is.EqualTo(1).Within(.001f));
    }

    [Test]
    public void DepartureConfirmationKeepsThePageBlockedUntilCancelled()
    {
        _panel.SelectRegion(_destination); AddFirstMember(); _page.verticalNormalizedPosition = 0;
        Field<Button>(_panel, "_depart").onClick.Invoke();
        var confirmation = Field<CountryRosterConfirm>(_panel, "_confirmation");
        Assert.That(confirmation.IsOpen, Is.True);
        Assert.That(Field<CanvasGroup>(_panel, "_shell").blocksRaycasts, Is.False);
        Field<Button>(confirmation, "_cancel").onClick.Invoke();
        Assert.That(Field<CanvasGroup>(_panel, "_shell").blocksRaycasts, Is.True);
        Assert.That(_panel.Draft.Contains(0), Is.True); Assert.That(_state.Active, Is.Empty);
    }

    [Test]
    public void ListWheelUsesTheListUntilItsEdgeThenMovesThePage()
    {
        _page.verticalNormalizedPosition = .5f; _list.verticalNormalizedPosition = .5f; Layout();
        var relay = _list.viewport.GetComponent<CountryNestedScrollInput>();
        Vector2 pageBefore = _page.content.anchoredPosition, listBefore = _list.content.anchoredPosition;
        relay.OnScroll(Pointer(new Vector2(0, -1)));
        Assert.That(_list.content.anchoredPosition.y, Is.GreaterThan(listBefore.y));
        Assert.That(_page.content.anchoredPosition.y, Is.EqualTo(pageBefore.y).Within(.01f));
        _list.verticalNormalizedPosition = 0; Layout();
        relay.OnScroll(Pointer(new Vector2(0, -1)));
        Assert.That(_page.content.anchoredPosition.y, Is.GreaterThan(pageBefore.y));
        _list.verticalNormalizedPosition = 1; Layout();
        pageBefore = _page.content.anchoredPosition;
        relay.OnScroll(Pointer(new Vector2(0, 1)));
        Assert.That(_page.content.anchoredPosition.y, Is.LessThan(pageBefore.y));
    }

    [Test]
    public void MemberStripSeparatesVerticalPageScrollingFromHorizontalMembers()
    {
        _page.verticalNormalizedPosition = .5f; _members.horizontalNormalizedPosition = 0; Layout();
        var relay = _members.viewport.GetComponent<CountryNestedScrollInput>();
        Vector2 membersBefore = _members.content.anchoredPosition;
        relay.OnScroll(Pointer(new Vector2(0, -1)));
        Assert.That(_members.content.anchoredPosition, Is.EqualTo(membersBefore));
        Vector2 pageBefore = _page.content.anchoredPosition;
        relay.OnScroll(Pointer(new Vector2(-1, 0)));
        Assert.That(_members.content.anchoredPosition.x, Is.LessThan(membersBefore.x));
        Assert.That(_page.content.anchoredPosition.y, Is.EqualTo(pageBefore.y).Within(.01f));
    }

    [Test]
    public void VerticalMemberDragMovesOnlyThePage()
    {
        _page.verticalNormalizedPosition = .5f; Layout();
        var relay = _members.viewport.GetComponent<CountryNestedScrollInput>();
        Vector2 pageBefore = _page.content.anchoredPosition, membersBefore = _members.content.anchoredPosition;
        var pointer = Pointer(Vector2.zero); pointer.pressPosition = new Vector2(300, 250); pointer.position = new Vector2(300, 280);
        relay.OnInitializePotentialDrag(pointer); relay.OnBeginDrag(pointer);
        pointer.position = new Vector2(300, 320); relay.OnDrag(pointer); relay.OnEndDrag(pointer);
        Assert.That(_page.content.anchoredPosition.y, Is.GreaterThan(pageBefore.y));
        Assert.That(_members.content.anchoredPosition, Is.EqualTo(membersBefore));
    }

    [Test]
    public void HorizontalMemberDragMovesOnlyTheMemberStrip()
    {
        _page.verticalNormalizedPosition = .5f; _members.horizontalNormalizedPosition = 0; Layout();
        var relay = _members.viewport.GetComponent<CountryNestedScrollInput>();
        Vector2 pageBefore = _page.content.anchoredPosition, membersBefore = _members.content.anchoredPosition;
        var pointer = Pointer(Vector2.zero); pointer.pressPosition = new Vector2(300, 250); pointer.position = new Vector2(270, 250);
        relay.OnInitializePotentialDrag(pointer); relay.OnBeginDrag(pointer);
        pointer.position = new Vector2(220, 250); relay.OnDrag(pointer); relay.OnEndDrag(pointer);
        Assert.That(_members.content.anchoredPosition.x, Is.LessThan(membersBefore.x));
        Assert.That(_page.content.anchoredPosition, Is.EqualTo(pageBefore));
    }

    [Test]
    public void MapDefersRouteChangesUntilTheLayoutRebuildFinishes()
    {
        _panel.SelectRegion(_destination);
        var map = Field<CountryWorldMapView>(_panel, "_map");
        var route = Field<CountryWorldRouteGraphic>(map, "_route");
        Assert.That(route.gameObject.activeSelf, Is.True);
        var change = new MapLayoutChange(map);
        CanvasUpdateRegistry.RegisterCanvasElementForLayoutRebuild(change);
        Canvas.ForceUpdateCanvases();
        Assert.That(change.InvokedDuringLayout, Is.True);
        Assert.That(route.gameObject.activeSelf, Is.True);
        Call(map, "LateUpdate");
        Assert.That(route.gameObject.activeSelf, Is.False);
    }

    private sealed class MapLayoutChange : ICanvasElement
    {
        private readonly CountryWorldMapView _map;
        public bool InvokedDuringLayout { get; private set; }
        public Transform transform => _map.transform;
        public MapLayoutChange(CountryWorldMapView map) => _map = map;
        public void Rebuild(CanvasUpdate phase)
        {
            if (phase != CanvasUpdate.Layout) return;
            InvokedDuringLayout = CanvasUpdateRegistry.IsRebuildingLayout();
            _map.Refresh(-1);
        }
        public void LayoutComplete() { }
        public void GraphicUpdateComplete() { }
        public bool IsDestroyed() => _map == null;
    }

    private void AddFirstMember() { _view.SelectSoldier(0); Field<Button>(_view, "_action").onClick.Invoke(); Layout(); }
    private void Layout()
    {
        Canvas.ForceUpdateCanvases(); _page.GetComponent<CountryExpeditionPageLayout>().Refresh();
        Canvas.ForceUpdateCanvases(); _camera.Render();
    }
    private PointerEventData Pointer(Vector2 delta) => new(null) { scrollDelta = delta, pointerPressRaycast = new RaycastResult { module = _hud.GetComponent<GraphicRaycaster>() } };
    private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, Private).GetValue(target);
    private static void Call(object target, string method) => target.GetType().GetMethod(method, Private).Invoke(target, null);
}
