using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using WarSimulation.Combat.Map;

public sealed class CombatBattleMapView : MonoBehaviour, IPointerClickHandler, IPointerDownHandler,
    IPointerUpHandler, IInitializePotentialDragHandler, IDragHandler
{
    private RawImage _image;
    private Texture2D _texture;
    private CombatMapSystem _system;
    private readonly List<GameObject> _markers = new();
    private bool _dragging;
    public bool IsOpen => _texture != null;
    public bool InteractionEnabled { get; set; } = true;
    public RectTransform ImageRect => _image.rectTransform;
    public MapData Map { get; private set; }
    public Vector2 Point { get; private set; }
    public float Scale { get; private set; }
    public event Action<Vector2> PointChanged;

    public static CombatBattleMapView Create(RectTransform area)
    {
        var view = area.gameObject.AddComponent<CombatBattleMapView>();
        view._image = Rect("Map", area).gameObject.AddComponent<RawImage>();
        view._image.gameObject.AddComponent<Mask>().showMaskGraphic = true;
        return view;
    }

    public bool Open(CombatMapSystem system, CombatCharacterSystem characters, Camera camera = null)
    {
        Clear();
        if (system == null || system.CurrentMap == null) return false;
        _system = system;
        Map = system.CurrentMap;
        Point = Map.Height.WorldSize * .5f;
        _texture = CombatMapPreviewTextureBuilder.BuildBackground(Map, includeTreesAndRocks: false);
        _image.texture = _texture;
        camera = camera != null ? camera : Camera.main;
        Vector3 forward = camera != null ? system.MapOrigin.InverseTransformDirection(camera.transform.forward) : Vector3.forward;
        if (forward.x * forward.x + forward.z * forward.z < .0001f && camera != null)
            forward = system.MapOrigin.InverseTransformDirection(camera.transform.up);
        ImageRect.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg);
        Canvas.ForceUpdateCanvases();
        Fit();
        AddCharacters(characters != null ? characters.AllyCharacters : null, new Color(.25f, .75f, 1));
        AddCharacters(characters != null ? characters.EnemyCharacters : null, new Color(1, .35f, .35f));
        if (system.TryGetMainStonePosition(CombatTeam.Ally, out Vector3 ally)) AddMarker(ally, new Color(.25f, .75f, 1), true);
        if (system.TryGetMainStonePosition(CombatTeam.Enemy, out Vector3 enemy)) AddMarker(enemy, new Color(1, .35f, .35f), true);
        return true;
    }

    private void Update() { if (IsOpen) Fit(); }
    private void Fit()
    {
        Vector2 size = Map.Height.WorldSize;
        float angle = ImageRect.localEulerAngles.z * Mathf.Deg2Rad;
        float sin = Mathf.Abs(Mathf.Sin(angle)), cos = Mathf.Abs(Mathf.Cos(angle));
        Vector2 bounds = new(cos * size.x + sin * size.y, sin * size.x + cos * size.y);
        var area = (RectTransform)transform;
        Scale = Mathf.Min(area.rect.width / bounds.x, area.rect.height / bounds.y);
        ImageRect.sizeDelta = size * Scale;
    }

    public Vector3 WorldPoint => _system.MapLocalToSurfaceWorldPosition(new Vector3(Point.x, 0, Point.y));
    public void SetPoint(Vector2 mapPoint) => Point = mapPoint;
    public void PositionMarker(RectTransform marker, Vector2 mapPoint)
    {
        Vector2 size = Map.Height.WorldSize;
        marker.anchorMin = marker.anchorMax = new Vector2(mapPoint.x / size.x, mapPoint.y / size.y);
        marker.anchoredPosition = Vector2.zero;
    }

    public void SelectPosition(Vector2 normalized)
    {
        if (!IsOpen || !InteractionEnabled || normalized.x < 0 || normalized.x > 1 || normalized.y < 0 || normalized.y > 1) return;
        Point = Vector2.Scale(normalized, Map.Height.WorldSize);
        PointChanged?.Invoke(Point);
    }

    public bool SelectPointer(PointerEventData data)
    {
        if (!IsOpen || !InteractionEnabled || data.button != PointerEventData.InputButton.Left) return false;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(ImageRect, data.position, data.pressEventCamera, out Vector2 local) ||
            !TryNormalizePoint(ImageRect.rect, local, out Vector2 normalized)) return false;
        SelectPosition(normalized);
        return true;
    }

    public void OnPointerClick(PointerEventData data) => SelectPointer(data);
    public void OnPointerDown(PointerEventData data) => _dragging = SelectPointer(data);
    public void OnPointerUp(PointerEventData data) => _dragging = false;
    public void OnInitializePotentialDrag(PointerEventData data) => data.useDragThreshold = false;
    public void OnDrag(PointerEventData data) { if (_dragging) SelectPointer(data); }

    public static bool TryNormalizePoint(Rect rect, Vector2 local, out Vector2 normalized)
    {
        normalized = default;
        if (rect.width <= 0 || rect.height <= 0 || local.x < rect.xMin || local.x > rect.xMax ||
            local.y < rect.yMin || local.y > rect.yMax) return false;
        normalized = new Vector2((local.x - rect.xMin) / rect.width, (local.y - rect.yMin) / rect.height);
        return true;
    }

    private void AddCharacters(IReadOnlyList<Character> characters, Color color)
    {
        if (characters == null) return;
        foreach (Character character in characters)
            if (character != null) AddMarker(character.transform.position, color, false);
    }

    private void AddMarker(Vector3 world, Color color, bool stone)
    {
        Vector3 local = _system.MapOrigin.InverseTransformPoint(world);
        Vector2 size = Map.Height.WorldSize;
        if (local.x < 0 || local.z < 0 || local.x > size.x || local.z > size.y) return;
        RectTransform rect = Rect(stone ? "StoneMarker" : "CharacterMarker", ImageRect);
        rect.sizeDelta = new Vector2(18, 18);
        if (stone)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
        }
        else
        {
            var graphic = rect.gameObject.AddComponent<CombatRockShapeGraphic>();
            graphic.Shape = CombatRockShape.Circle;
            graphic.FilledCircle = true;
            graphic.color = new Color(.02f, .03f, .06f);
            graphic.raycastTarget = false;
            RectTransform dot = Rect("Dot", rect);
            Stretch(dot, Vector2.zero, Vector2.one);
            dot.offsetMin = new Vector2(3, 3);
            dot.offsetMax = new Vector2(-3, -3);
            var fill = dot.gameObject.AddComponent<CombatRockShapeGraphic>();
            fill.Shape = CombatRockShape.Circle;
            fill.FilledCircle = true;
            fill.color = color;
            fill.raycastTarget = false;
        }
        PositionMarker(rect, new Vector2(local.x, local.z));
        _markers.Add(rect.gameObject);
    }

    public void Clear()
    {
        _dragging = false;
        if (_image != null) _image.texture = null;
        if (_texture != null) DestroyGenerated(_texture);
        _texture = null;
        foreach (GameObject marker in _markers)
            if (marker != null) { marker.SetActive(false); DestroyGenerated(marker); }
        _markers.Clear();
        Map = null;
        _system = null;
    }

    private void OnDestroy() => Clear();
    internal static void DestroyGenerated(UnityEngine.Object item)
    {
        if (Application.isPlaying) Destroy(item);
        else DestroyImmediate(item);
    }

    internal static RectTransform Rect(string name, Transform parent)
    {
        var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
        rect.SetParent(parent, false);
        return rect;
    }
    internal static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = min; rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
    internal static void SetTop(RectTransform rect, float offset, float height)
    {
        Stretch(rect, new Vector2(0, 1), Vector2.one);
        rect.pivot = new Vector2(.5f, 1);
        rect.anchoredPosition = new Vector2(0, -offset);
        rect.sizeDelta = new Vector2(0, height);
    }
    internal static TMP_Text Label(Transform parent, string name, string text, TMP_FontAsset font, float size)
    {
        var label = Rect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
        label.font = font;
        if (font != null) label.fontSharedMaterial = font.material;
        label.text = text; label.fontSize = size;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white; label.raycastTarget = false;
        return label;
    }
    internal static Button Button(Transform parent, string name, string text, TMP_FontAsset font, UnityEngine.Events.UnityAction action)
    {
        RectTransform rect = Rect(name, parent);
        var image = rect.gameObject.AddComponent<Image>();
        image.color = new Color(.18f, .24f, .36f);
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        var label = Label(rect, "Text", text, font, 24);
        Stretch(label.rectTransform, Vector2.zero, Vector2.one);
        button.onClick.AddListener(action);
        return button;
    }
}
