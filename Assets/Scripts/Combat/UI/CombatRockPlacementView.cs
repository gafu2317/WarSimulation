using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using WarSimulation.Combat.Map;
using static CombatBattleMapView;

public sealed class CombatRockPlacementView : MonoBehaviour, IPointerClickHandler, IPointerDownHandler,
    IPointerUpHandler, IInitializePotentialDragHandler, IDragHandler
{
    private CombatBattleMapView _preview;
    private TMP_Text _title;
    private TMP_Text _coordinates;
    private Button _confirm;
    private Button _rotate;
    private Button _cancel;
    private Button _remove;
    private bool _busy;
    private bool _draggingMap;
    private CombatRockShapeGraphic _cursor;
    private CombatMapSystem _mapSystem;
    private MapData _map;
    private CombatRockShape _shape;
    private Vector2? _point;
    private Vector2 _confirmedPoint;
    private int _quarterTurns;
    private TMP_FontAsset _font;

    public CombatRockPlacement? ConfirmedPlacement { get; private set; }
    public bool IsOpen => gameObject.activeSelf && _preview != null && _preview.IsOpen;
    public event Action Closed;
    public event Action<CombatRockPlacement, Action<bool, string>> PlacementRequested;
    public event Action RemovalRequested;

    public static CombatRockPlacementView Create(Transform parent, TMP_FontAsset font)
    {
        var root = new GameObject("RockPlacementModal", typeof(RectTransform), typeof(Image), typeof(CombatRockPlacementView));
        RectTransform rect = (RectTransform)root.transform;
        rect.SetParent(parent, false);
        Stretch(rect, Vector2.zero, Vector2.one);
        root.GetComponent<Image>().color = new Color(0.015f, 0.025f, 0.05f, 0.96f);
        var view = root.GetComponent<CombatRockPlacementView>();
        view._font = font;
        view.Build();
        root.SetActive(false);
        return view;
    }

    private void Build()
    {
        RectTransform panel = Rect("Panel", transform);
        Stretch(panel, new Vector2(0.04f, 0.04f), new Vector2(0.96f, 0.96f));
        _title = Label(panel, "Title", "", _font, 28);
        SetTop(_title.rectTransform, 0, 44);
        TMP_Text legend = Label(panel, "Legend", "カメラ前方 ↑　青● 味方　赤● 敵　青■ 自軍魔石　赤■ 敵軍魔石", _font, 20);
        SetTop(legend.rectTransform, 48, 32);
        RectTransform mapArea = Rect("MapArea", panel);
        Stretch(mapArea, Vector2.zero, Vector2.one);
        mapArea.offsetMin = new Vector2(0, 104);
        mapArea.offsetMax = new Vector2(0, -86);
        _preview = CombatBattleMapView.Create(mapArea);
        _preview.PointChanged += point => { _point = point; RefreshSelection(); };
        RectTransform imageRect = _preview.ImageRect;
        RectTransform cursorRect = Rect("Selection", imageRect);
        cursorRect.sizeDelta = new Vector2(48, 48);
        _cursor = cursorRect.gameObject.AddComponent<CombatRockShapeGraphic>();
        _cursor.color = new Color(1f, 0.9f, 0.15f);
        _cursor.raycastTarget = false;
        _coordinates = Label(panel, "Coordinates", "", _font, 22);
        RectTransform coordinatesRect = _coordinates.rectTransform;
        Stretch(coordinatesRect, Vector2.zero, new Vector2(1, 0));
        coordinatesRect.pivot = new Vector2(0.5f, 0);
        coordinatesRect.anchoredPosition = new Vector2(0, 62);
        coordinatesRect.sizeDelta = new Vector2(0, 36);
        RectTransform actions = Rect("Actions", panel);
        Stretch(actions, Vector2.zero, new Vector2(1, 0));
        actions.pivot = new Vector2(0.5f, 0);
        actions.sizeDelta = new Vector2(0, 54);
        var layout = actions.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 12;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        _rotate = Button(actions, "Rotate", "90°回転", _font, Rotate);
        _confirm = Button(actions, "Confirm", "確定", _font, Confirm);
        _remove = Button(actions, "Remove", "岩を撤去", _font, Remove);
        _cancel = Button(actions, "Cancel", "キャンセル", _font, Cancel);
    }

    public bool Open(CombatRockShape shape, CombatMapSystem mapSystem, CombatCharacterSystem characters, Camera camera = null)
    {
        if (mapSystem == null || mapSystem.CurrentMap == null) return false;
        ReleasePreview();
        _mapSystem = mapSystem;
        _map = mapSystem.CurrentMap;
        _shape = shape;
        _busy = false;
        _point = ConfirmedPlacement.HasValue ? _confirmedPoint : _map.Height.WorldSize * 0.5f;
        _quarterTurns = ConfirmedPlacement.HasValue && ConfirmedPlacement.Value.Shape == shape
            ? ConfirmedPlacement.Value.QuarterTurns : 0;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        _preview.InteractionEnabled = true;
        _preview.Open(mapSystem, characters, camera);
        _preview.SetPoint(_point.Value);
        FitCursor();
        _cursor.transform.SetAsLastSibling();
        RefreshSelection();
        return true;
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) Cancel();
        if (_map != null) FitCursor();
    }

    private void FitCursor() => _cursor.rectTransform.sizeDelta = Vector2.one * _preview.Scale * (6f / .96f);

    public void OnPointerClick(PointerEventData eventData) => SelectPointerPosition(eventData);

    public void OnPointerDown(PointerEventData eventData) => _draggingMap = SelectPointerPosition(eventData);

    public void OnPointerUp(PointerEventData eventData) => _draggingMap = false;

    public void OnInitializePotentialDrag(PointerEventData eventData) => eventData.useDragThreshold = false;

    public void OnDrag(PointerEventData eventData)
    {
        if (_draggingMap) SelectPointerPosition(eventData);
    }

    private bool SelectPointerPosition(PointerEventData eventData)
    {
        if (!IsOpen || _busy || eventData.button != PointerEventData.InputButton.Left) return false;
        return _preview.SelectPointer(eventData);
    }

    public static bool TryNormalizePoint(Rect rect, Vector2 local, out Vector2 normalized) =>
        CombatBattleMapView.TryNormalizePoint(rect, local, out normalized);

    public void SelectPosition(Vector2 normalized)
    {
        if (IsOpen && !_busy) _preview.SelectPosition(normalized);
    }

    public void Rotate()
    {
        if (!IsOpen || _busy || _shape == CombatRockShape.Circle) return;
        _quarterTurns = (_quarterTurns + 1) % 4;
        RefreshSelection();
    }

    public void Confirm()
    {
        if (!IsOpen || _busy || !_point.HasValue) return;
        Vector2 point = _point.Value;
        Vector3 world = _mapSystem.MapLocalToSurfaceWorldPosition(new Vector3(point.x, 0, point.y));
        var placement = new CombatRockPlacement(_shape, new Vector2(world.x, world.z), _quarterTurns);
        SetBusy(true);
        if (PlacementRequested == null) CompleteConfirmation(placement, point, true, null);
        else PlacementRequested.Invoke(placement, (success, error) => CompleteConfirmation(placement, point, success, error));
    }

    private void CompleteConfirmation(CombatRockPlacement placement, Vector2 point, bool success, string error)
    {
        if (this == null || !IsOpen) return;
        SetBusy(false);
        if (!success) { _coordinates.text = error; return; }
        _confirmedPoint = point;
        ConfirmedPlacement = placement;
        Cancel();
    }

    public void Remove()
    {
        if (!IsOpen || _busy || !ConfirmedPlacement.HasValue) return;
        RemovalRequested?.Invoke();
        Clear();
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        _preview.InteractionEnabled = !busy;
        RefreshSelection();
        if (busy) _coordinates.text = "岩を設置中…";
    }

    public void Cancel()
    {
        if (!IsOpen || _busy) return;
        ReleasePreview();
        gameObject.SetActive(false);
        Closed?.Invoke();
    }

    public void Clear()
    {
        ConfirmedPlacement = null;
        _busy = false;
        Cancel();
    }

    private void OnDisable()
    {
        bool wasOpen = _preview != null && _preview.IsOpen;
        ReleasePreview();
        if (wasOpen) Closed?.Invoke();
    }

    private void OnDestroy() => ReleasePreview();

    private void RefreshSelection()
    {
        _title.text = $"{CombatRockShapeGraphic.Label(_shape)} — ドラッグで設置位置を選択（高さ3m）";
        _rotate.interactable = !_busy && _shape != CombatRockShape.Circle;
        _confirm.interactable = !_busy && _point.HasValue;
        _cancel.interactable = !_busy;
        _remove.interactable = !_busy && ConfirmedPlacement.HasValue;
        _cursor.gameObject.SetActive(_point.HasValue);
        _cursor.Shape = _shape;
        _cursor.rectTransform.localRotation = Quaternion.Euler(0, 0, -90 * _quarterTurns);
        if (_point.HasValue)
        {
            _preview.PositionMarker(_cursor.rectTransform, _point.Value);
            Vector3 world = _mapSystem.MapLocalToSurfaceWorldPosition(new Vector3(_point.Value.x, 0, _point.Value.y));
            _coordinates.text = $"X: {world.x:0.0}　Z: {world.z:0.0}　向き: {_quarterTurns * 90}°";
        }
        else _coordinates.text = "マップをクリックして位置を選んでください";
    }

    private void ReleasePreview()
    {
        _draggingMap = false;
        if (_preview != null) _preview.Clear();
        _map = null;
        _mapSystem = null;
        _point = null;
    }
}
