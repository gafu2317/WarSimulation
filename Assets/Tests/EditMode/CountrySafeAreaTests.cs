using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public sealed class CountrySafeAreaTests
{
    private readonly Vector2Int _size = new(2532, 1170);
    private readonly Rect _safe = new(141, 63, 2250, 1107);
    private readonly Vector3[] _corners = new Vector3[4];
    private Scene _scene;
    private GameObject _hud;
    private Camera _camera;
    private RenderTexture _texture;
    private CountrySafeArea _layout;
    private EventSystem _events;

    [SetUp]
    public void SetUp()
    {
        Assert.That(EditorApplication.isPlaying, Is.False);
        _scene = EditorSceneManager.NewPreviewScene();
        _texture = RenderTexture.GetTemporary(_size.x, _size.y, 24);
        var cameraRoot = new GameObject("Safe area test camera", typeof(Camera));
        SceneManager.MoveGameObjectToScene(cameraRoot, _scene);
        _camera = cameraRoot.GetComponent<Camera>();
        _camera.scene = _scene; _camera.targetTexture = _texture; _camera.aspect = (float)_size.x / _size.y;
        _camera.transform.position = new Vector3(0, 0, -10);
        var input = new GameObject("Safe area test input", typeof(EventSystem));
        SceneManager.MoveGameObjectToScene(input, _scene); _events = input.GetComponent<EventSystem>();
        _hud = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Country/UI/CountryHud.prefab"), _scene);
        var canvas = _hud.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = _camera; canvas.planeDistance = 5;
        _layout = _hud.GetComponent<CountrySafeArea>();
        Canvas.ForceUpdateCanvases();
    }

    [TearDown]
    public void TearDown()
    {
        EditorSceneManager.ClosePreviewScene(_scene);
        RenderTexture.ReleaseTemporary(_texture);
    }

    [Test]
    public void HeaderAndCornerMenusFitTheNotchedPhoneWithoutShrinkingButtons()
    {
        Assert.That(Bounds("Minimap").xMin, Is.LessThan(_safe.xMin));
        var buttonSize = Bounds("BottomLeftButtons").size;
        _layout.Apply(_safe, _size); Canvas.ForceUpdateCanvases();
        foreach (string name in new[] { "TopBar/Content", "Minimap", "BottomLeftButtons", "BottomRightButton", "Government/Window/Shell", "Personnel/Window/Shell", "Expeditions/Window/Shell", "Defense/Window/Shell/Formation/Main", "Settings/Window/Panel" })
            Inside(Bounds(name), name);
        Assert.That(Bounds("TopBar").xMin, Is.EqualTo(0).Within(.1f));
        Assert.That(Bounds("TopBar").xMax, Is.EqualTo(_size.x).Within(.1f));
        Assert.That(Vector2.Distance(Bounds("BottomLeftButtons").size, buttonSize), Is.LessThan(.1f));
        foreach (var column in new[] { "Population", "TaxIncome", "Wood", "Ore" })
        {
            var rect = (RectTransform)_hud.transform.Find("TopBar/Content/" + column);
            Inside(Bounds(rect), column);
        }
        Assert.That(_hud.transform.Find("TopBar").GetComponentsInChildren<TextMeshProUGUI>().Where(t => t.isTextOverflowing), Is.Empty);
    }

    [Test]
    public void ResourceValuesDoNotShiftTheirColumns()
    {
        _layout.Apply(_safe, _size); Canvas.ForceUpdateCanvases();
        var columns = new[] { "Population", "TaxIncome", "Wood", "Ore" }
            .Select(name => (RectTransform)_hud.transform.Find("TopBar/Content/" + name)).ToArray();
        var before = columns.Select(Bounds).ToArray();
        var resources = new SerializedObject(_hud.GetComponent<CountryResourceHud>());
        foreach (string name in new[] { "_population", "_taxIncome", "_wood", "_ore" })
            ((TextMeshProUGUI)resources.FindProperty(name).objectReferenceValue).text = "99,999+";
        Canvas.ForceUpdateCanvases();
        for (int i = 0; i < columns.Length; i++)
        {
            var after = Bounds(columns[i]);
            Assert.That(Vector2.Distance(after.position, before[i].position), Is.LessThan(.1f), columns[i].name);
            Assert.That(Vector2.Distance(after.size, before[i].size), Is.LessThan(.1f), columns[i].name);
            Inside(after, columns[i].name);
        }
    }

    [Test]
    public void FullScreenBackgroundsCoverTheDisplayAndControlsStaySafe()
    {
        _layout.Apply(_safe, _size); Canvas.ForceUpdateCanvases();
        const string facilityModal = "FacilityLayer/Popup/Modal/";
        foreach (string name in new[] { "Government/Window", "Personnel/Window", "Expeditions/Window", "Defense/Window/Dim", "Defense/Window/Shell/Formation", "Settings/Window/Dim", facilityModal + "Dim" })
        {
            var rect = Bounds(name);
            Assert.That(rect.xMin, Is.EqualTo(0).Within(.1f), name);
            Assert.That(rect.xMax, Is.EqualTo(_size.x).Within(.1f), name);
            Assert.That(rect.yMin, Is.EqualTo(0).Within(.1f), name);
            Assert.That(rect.yMax, Is.EqualTo(_size.y).Within(.1f), name);
        }
        foreach (string name in new[] { "Government/Window/Shell", "Personnel/Window/Shell", "Expeditions/Window/Shell", "Defense/Window/Shell/Formation/Header", "Defense/Window/Shell/Formation/Footer", "Settings/Window/Panel",
            "FacilityLayer/Banners", facilityModal + "Panel", facilityModal + "Panel/CloseButton", facilityModal + "Panel/CloseIcon" })
            Inside(Bounds(name), name);
        Assert.That(Vector2.Distance(Bounds(facilityModal + "Panel").center, _safe.center), Is.LessThan(.1f));
    }

    [Test]
    public void ReapplyingOrRemovingInsetsDoesNotAccumulateOffsets()
    {
        var original = Bounds("BottomRightButton");
        _layout.Apply(_safe, _size); Canvas.ForceUpdateCanvases();
        var inset = Bounds("BottomRightButton");
        _layout.Apply(new Rect(0, 63, 2532, 1107), _size); Canvas.ForceUpdateCanvases();
        _layout.Apply(_safe, _size); Canvas.ForceUpdateCanvases();
        Assert.That(Vector2.Distance(Bounds("BottomRightButton").position, inset.position), Is.LessThan(.1f));
        Assert.That(Vector2.Distance(Bounds("BottomRightButton").size, original.size), Is.LessThan(.1f));
        _layout.Apply(new Rect(Vector2.zero, _size), _size); Canvas.ForceUpdateCanvases();
        Assert.That(Vector2.Distance(Bounds("BottomRightButton").position, original.position), Is.LessThan(.1f));
        _layout.Apply(_safe, _size); Canvas.ForceUpdateCanvases();
        _layout.enabled = false; Canvas.ForceUpdateCanvases();
        Assert.That(Vector2.Distance(Bounds("BottomRightButton").position, original.position), Is.LessThan(.1f));
        _layout.enabled = true;
        _layout.Apply(_safe, _size); Canvas.ForceUpdateCanvases();
        Assert.That(Vector2.Distance(Bounds("BottomRightButton").position, inset.position), Is.LessThan(.1f));
    }

    [Test]
    public void SettingsCardAndItsFooterRemainInsideThePhoneSafeArea()
    {
        _layout.Apply(_safe, _size); Canvas.ForceUpdateCanvases();
        const string panel = "Settings/Window/Panel";
        Inside(Bounds(panel), "Settings panel");
        foreach (string name in new[] { "Close", "Footer/Cancel", "Footer/Combat", "Footer/Apply", "Raids", "Error" })
            Inside(Bounds(panel + "/" + name), name);
        Assert.That(Bounds(panel + "/Error").Overlaps(Bounds(panel + "/Footer")), Is.False);
    }

    [Test]
    public void MovedMenuButtonsStillReceivePointerInput()
    {
        _layout.Apply(_safe, _size); Canvas.ForceUpdateCanvases();
        _camera.Render();
        var buttons = _hud.transform.Find("BottomLeftButtons").GetComponentsInChildren<UnityEngine.UI.Button>().ToList();
        buttons.Add(_hud.transform.Find("BottomRightButton").GetComponent<UnityEngine.UI.Button>());
        var raycaster = _hud.GetComponent<UnityEngine.UI.GraphicRaycaster>();
        foreach (var button in buttons)
        {
            var bounds = Bounds((RectTransform)button.transform); Inside(bounds, button.name);
            var hits = new List<RaycastResult>();
            raycaster.Raycast(new PointerEventData(_events) { position = bounds.center }, hits);
            Assert.That(hits.Any(hit => hit.gameObject.GetComponentInParent<UnityEngine.UI.Button>() == button), Is.True, button.name);
        }
    }

    private Rect Bounds(string name) => Bounds((RectTransform)_hud.transform.Find(name));
    private Rect Bounds(RectTransform rect)
    {
        rect.GetWorldCorners(_corners);
        Vector2 min = RectTransformUtility.WorldToScreenPoint(_camera, _corners[0]);
        Vector2 max = RectTransformUtility.WorldToScreenPoint(_camera, _corners[2]);
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    private void Inside(Rect rect, string name)
    {
        Assert.That(rect.xMin, Is.GreaterThanOrEqualTo(_safe.xMin - .1f), name);
        Assert.That(rect.xMax, Is.LessThanOrEqualTo(_safe.xMax + .1f), name);
        Assert.That(rect.yMin, Is.GreaterThanOrEqualTo(_safe.yMin - .1f), name);
        Assert.That(rect.yMax, Is.LessThanOrEqualTo(_safe.yMax + .1f), name);
    }
}
