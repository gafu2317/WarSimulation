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
    private GameObject _formation;
    private ScrollRect _list, _details, _members;
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
        _hud = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Country/UI/CountryHud.prefab"));
        SceneManager.MoveGameObjectToScene(_hud, _scene);
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
        _formation = Field<GameObject>(_panel, "_formationPage");
        _list = Field<ScrollRect>(_view, "_list"); _details = Field<ScrollRect>(_view, "_details");
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
    public void SideButtonsShowOnePageAtATimeAndProgressDoesNotNavigate()
    {
        var map = Field<GameObject>(_panel, "_destinationPage");
        var next = Field<Button>(_panel, "_next");
        var depart = Field<Button>(_panel, "_depart");
        Assert.That(map.activeInHierarchy && next.gameObject.activeInHierarchy, Is.True);
        Assert.That(_formation.activeInHierarchy || depart.gameObject.activeInHierarchy, Is.False);

        next.onClick.Invoke(); Layout();
        Assert.That(map.activeInHierarchy || next.gameObject.activeInHierarchy, Is.False);
        Assert.That(_formation.activeInHierarchy && depart.gameObject.activeInHierarchy, Is.True);

        var back = Field<Button>(_panel, "_back");
        Assert.That(back.GetComponentInChildren<TextMeshProUGUI>().text, Does.Contain("領地を選ぶ"));
        foreach (string field in new[] { "_targetStep", "_memberStep" })
        {
            var step = Field<TextMeshProUGUI>(_panel, field);
            Assert.That(step.GetComponentInParent<Button>(), Is.Null);
            Assert.That(step.raycastTarget, Is.False);
            ExecuteEvents.ExecuteHierarchy(step.gameObject, Pointer(Vector2.zero), ExecuteEvents.pointerClickHandler);
            Assert.That(_formation.activeInHierarchy, Is.True);
        }
        Back();
        Assert.That(map.activeInHierarchy, Is.True);
        next.onClick.Invoke();
        Assert.That(_formation.activeInHierarchy, Is.True);
    }

    [Test]
    public void ReturningToTheMapPreservesTheDraftAndRosterPosition()
    {
        _panel.SelectRegion(_destination); OpenMembers(); AddFirstMember();
        _list.verticalNormalizedPosition = .5f;
        _details.verticalNormalizedPosition = .75f;
        Back();
        Assert.That(Field<GameObject>(_panel, "_destinationPage").activeInHierarchy, Is.True);
        Assert.That(_panel.Draft.Target, Is.EqualTo(_destination));
        OpenMembers();
        Assert.That(_panel.Draft.Members, Is.EqualTo(new[] { 0 }));
        Assert.That(_list.verticalNormalizedPosition, Is.EqualTo(.5f).Within(.001f));
        Assert.That(_details.verticalNormalizedPosition, Is.EqualTo(.75f).Within(.001f));
        var context = Field<TextMeshProUGUI>(_panel, "_context").text;
        Assert.That(context, Does.Contain(_state.Data.Regions[_destination].Name).And.Contain("到着").And.Contain("帰還"));
    }

    [Test]
    public void EquipmentChangesRemainAppliedAfterReturningToTheMap()
    {
        _panel.SelectRegion(_destination); OpenMembers(); AddFirstMember();
        int original = _roster.EquippedWeapon(0);
        int replacement = original == 0 ? 1 : 0;
        _list.verticalNormalizedPosition = .5f;
        _details.verticalNormalizedPosition = .75f;
        var listPosition = _list.content.anchoredPosition;
        var detailPosition = _details.content.anchoredPosition;
        Field<Button>(_panel, "_equipment").onClick.Invoke();
        Assert.That(_list.isActiveAndEnabled, Is.True);
        Assert.That(_details.isActiveAndEnabled, Is.True);
        _list.content.Find($"Row{replacement + 1:00}").GetComponent<Button>().onClick.Invoke();
        Field<Button>(_view, "_action").onClick.Invoke();
        Assert.That(_list.content.anchoredPosition, Is.EqualTo(listPosition));
        Assert.That(_details.content.anchoredPosition, Is.EqualTo(detailPosition));
        Assert.That(_roster.EquippedWeapon(0), Is.EqualTo(replacement));

        Back();
        OpenMembers();
        Assert.That(_panel.Draft.Members, Is.EqualTo(new[] { 0 }));
        Assert.That(_panel.Draft.Weapon(0), Is.EqualTo(replacement));
    }

    [Test]
    public void ReturningToTheCountryDiscardsTheDraftAndResetsTheRosterPosition()
    {
        _panel.SelectRegion(_destination); OpenMembers(); AddFirstMember();
        _list.verticalNormalizedPosition = 0;
        _details.verticalNormalizedPosition = 0;
        Back();
        Back();
        Assert.That(_panel.IsOpen, Is.False);
        _panel.Open(); Layout();
        Assert.That(_panel.Draft.Target, Is.EqualTo(-1));
        Assert.That(_panel.Draft.Members, Is.Empty);
        Assert.That(Field<GameObject>(_panel, "_destinationPage").activeInHierarchy, Is.True);
        OpenMembers();
        Assert.That(_list.verticalNormalizedPosition, Is.EqualTo(1).Within(.001f));
        Assert.That(_details.verticalNormalizedPosition, Is.EqualTo(1).Within(.001f));
    }

    [Test]
    public void DepartureConfirmationKeepsThePageBlockedUntilCancelled()
    {
        _panel.SelectRegion(_destination); OpenMembers(); AddFirstMember();
        Field<Button>(_panel, "_depart").onClick.Invoke();
        var confirmation = Field<CountryRosterConfirm>(_panel, "_confirmation");
        Assert.That(confirmation.IsOpen, Is.True);
        Assert.That(Field<CanvasGroup>(_panel, "_shell").blocksRaycasts, Is.False);
        Back();
        Assert.That(_formation.activeInHierarchy, Is.True);
        Field<Button>(confirmation, "_cancel").onClick.Invoke();
        Assert.That(Field<CanvasGroup>(_panel, "_shell").blocksRaycasts, Is.True);
        Assert.That(_panel.Draft.Contains(0), Is.True); Assert.That(_state.Active, Is.Empty);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ScrollingEitherPaneLeavesTheOtherPaneAndMembersInPlace(bool scrollDetails)
    {
        OpenMembers();
        var target = scrollDetails ? _details : _list;
        var other = scrollDetails ? _list : _details;
        var before = target.content.anchoredPosition;
        var otherBefore = other.content.anchoredPosition;
        var count = Field<TextMeshProUGUI>(_panel, "_count").rectTransform;
        float stripY = _members.content.position.y, countY = count.position.y;
        ExecuteEvents.ExecuteHierarchy(target.viewport.gameObject, Pointer(Vector2.down), ExecuteEvents.scrollHandler);
        Assert.That(target.content.anchoredPosition.y, Is.GreaterThan(before.y));
        Assert.That(other.content.anchoredPosition, Is.EqualTo(otherBefore));
        Assert.That(_members.content.position.y, Is.EqualTo(stripY).Within(.001f));
        Assert.That(count.position.y, Is.EqualTo(countY).Within(.001f));
    }

    [Test]
    public void DraggingFromTheCardToTheMemberStripAddsThePressedSoldier()
    {
        _panel.SelectRegion(_destination); OpenMembers();
        var card = (RectTransform)_list.content.Find("Row02");
        var pointer = BeginCardDrag(card);
        pointer.position = RectTransformUtility.WorldToScreenPoint(_camera, _members.viewport.TransformPoint(_members.viewport.rect.center));
        ExecuteEvents.Execute(card.gameObject, pointer, ExecuteEvents.endDragHandler);
        Assert.That(_panel.Draft.Members, Is.EqualTo(new[] { 1 }));
    }

    [Test]
    public void DraggingTheCardWithinTheListScrollsWithoutAddingAMember()
    {
        _panel.SelectRegion(_destination); OpenMembers();
        var card = (RectTransform)_list.content.Find("Row02");
        var pointer = BeginCardDrag(card);
        var before = _list.content.anchoredPosition;
        pointer.position += Vector2.up * 48;
        ExecuteEvents.Execute(card.gameObject, pointer, ExecuteEvents.dragHandler);
        ExecuteEvents.Execute(card.gameObject, pointer, ExecuteEvents.endDragHandler);
        Assert.That(_list.content.anchoredPosition.y, Is.GreaterThan(before.y));
        Assert.That(_panel.Draft.Members, Is.Empty);
    }

    [Test]
    public void TwoConsecutiveClicksToggleMembershipButAFirstClickOrDragDoesNot()
    {
        OpenMembers();
        var row = (RectTransform)_list.content.Find("Row01");
        var other = _list.content.Find("Row02");
        ClickRow(row);
        Assert.That(_panel.Draft.Members, Is.Empty);
        ClickRow(other);
        ClickRow(row);
        Assert.That(_panel.Draft.Members, Is.Empty);
        var drag = BeginCardDrag(row);
        ExecuteEvents.Execute(row.gameObject, drag, ExecuteEvents.endDragHandler);
        ClickRow(row);
        Assert.That(_panel.Draft.Members, Is.Empty);
        ExecuteEvents.Execute(row.gameObject, Pointer(Vector2.down), ExecuteEvents.scrollHandler);
        ClickRow(row);
        Assert.That(_panel.Draft.Members, Is.Empty);
        ClickRow(row);
        Assert.That(_panel.Draft.Members, Is.EqualTo(new[] { 0 }));
        var member = Field<RectTransform>(_panel, "_members").Find("Member01");
        ClickRow(member);
        Assert.That(_panel.Draft.Members, Is.EqualTo(new[] { 0 }));
        ClickRow(member);
        Assert.That(_panel.Draft.Members, Is.Empty);
        Assert.That(_state.Active, Is.Empty);
    }

    [Test]
    public void DraggingTheMemberStripOnlyMovesItsHorizontalContent()
    {
        OpenMembers();
        var listBefore = _list.content.anchoredPosition;
        var detailsBefore = _details.content.anchoredPosition;
        var membersBefore = _members.content.anchoredPosition;
        var pointer = Pointer(Vector2.zero);
        pointer.position = new Vector2(500, 250);
        var handler = ExecuteEvents.GetEventHandler<IDragHandler>(_members.viewport.gameObject);
        ExecuteEvents.Execute(handler, pointer, ExecuteEvents.initializePotentialDrag);
        ExecuteEvents.Execute(handler, pointer, ExecuteEvents.beginDragHandler);
        pointer.position += Vector2.left * 80;
        ExecuteEvents.Execute(handler, pointer, ExecuteEvents.dragHandler);
        ExecuteEvents.Execute(handler, pointer, ExecuteEvents.endDragHandler);
        Assert.That(_members.content.anchoredPosition.x, Is.LessThan(membersBefore.x));
        Assert.That(_list.content.anchoredPosition, Is.EqualTo(listBefore));
        Assert.That(_details.content.anchoredPosition, Is.EqualTo(detailsBefore));
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

    private void OpenMembers() { Field<Button>(_panel, "_next").onClick.Invoke(); Layout(); }
    private void Back() => Field<Button>(_panel, "_back").onClick.Invoke();
    private void ClickRow(Transform row) => ExecuteEvents.Execute(row.gameObject, Pointer(Vector2.zero), ExecuteEvents.pointerClickHandler);
    private void AddFirstMember() { _view.SelectSoldier(0); Field<Button>(_view, "_action").onClick.Invoke(); Layout(); }
    private void Layout()
    {
        Canvas.ForceUpdateCanvases(); _camera.Render();
    }
    private PointerEventData BeginCardDrag(RectTransform card)
    {
        var point = new Vector2(card.rect.xMax - 24, card.rect.center.y);
        var pointer = Pointer(Vector2.zero);
        pointer.pressPosition = pointer.position = RectTransformUtility.WorldToScreenPoint(_camera, card.TransformPoint(point));
        ExecuteEvents.Execute(card.gameObject, pointer, ExecuteEvents.initializePotentialDrag);
        ExecuteEvents.Execute(card.gameObject, pointer, ExecuteEvents.beginDragHandler);
        return pointer;
    }
    private PointerEventData Pointer(Vector2 delta) => new(null) { scrollDelta = delta, pointerPressRaycast = new RaycastResult { module = _hud.GetComponent<GraphicRaycaster>() } };
    private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, Private).GetValue(target);
    private static void Call(object target, string method) => target.GetType().GetMethod(method, Private).Invoke(target, null);
}
