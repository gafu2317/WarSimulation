using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using static CombatBattleMapView;

public sealed class CombatPlayerMagicView : MonoBehaviour
{
    private CombatBattleMapView _preview;
    private TMP_Text _title, _summary;
    private TMP_InputField _radius, _damage, _seconds;
    private Button _cast;
    private CombatMagicRangeGraphic _range;
    private RectTransform _center;
    private CombatPlayerMagicKind _kind;
    private CombatCharacterSystem _characters;
    private CombatPlayerMagicRequest? _lastArea, _lastInvulnerable;
    private Vector2 _lastPoint;
    public bool IsOpen => gameObject.activeSelf && _preview != null && _preview.IsOpen;
    public event Action Closed;
    public event Action<CombatPlayerMagicRequest> CastRequested;

    public static CombatPlayerMagicView Create(Transform parent, TMP_FontAsset font)
    {
        var root = Rect("PlayerMagicModal", parent);
        Stretch(root, Vector2.zero, Vector2.one);
        root.gameObject.AddComponent<Image>().color = new Color(.015f, .025f, .05f, .96f);
        var view = root.gameObject.AddComponent<CombatPlayerMagicView>();
        view.Build(font);
        root.gameObject.SetActive(false);
        return view;
    }

    private void Build(TMP_FontAsset font)
    {
        var panel = Rect("Panel", transform);
        Stretch(panel, new Vector2(.04f, .04f), new Vector2(.96f, .96f));
        _title = Label(panel, "Title", "", font, 28);
        SetTop(_title.rectTransform, 0, 44);
        var legend = Label(panel, "Legend", "カメラ前方 ↑　青● 味方　赤● 敵　青■ 自軍魔石　赤■ 敵軍魔石", font, 20);
        SetTop(legend.rectTransform, 48, 32);
        var area = Rect("MapArea", panel);
        Stretch(area, Vector2.zero, Vector2.one);
        area.offsetMin = new Vector2(0, 154);
        area.offsetMax = new Vector2(0, -86);
        _preview = CombatBattleMapView.Create(area);
        _preview.PointChanged += _ => Refresh();
        _range = Rect("Range", _preview.ImageRect).gameObject.AddComponent<CombatMagicRangeGraphic>();
        _range.raycastTarget = false;
        _center = Rect("Center", _preview.ImageRect);
        _center.sizeDelta = new Vector2(8, 8);
        var dot = _center.gameObject.AddComponent<CombatRockShapeGraphic>();
        dot.Shape = CombatRockShape.Circle; dot.FilledCircle = true; dot.color = Color.white; dot.raycastTarget = false;

        var parameters = Rect("Parameters", panel);
        Stretch(parameters, Vector2.zero, new Vector2(1, 0));
        parameters.pivot = new Vector2(.5f, 0);
        parameters.anchoredPosition = new Vector2(0, 104);
        parameters.sizeDelta = new Vector2(0, 44);
        var inputs = parameters.gameObject.AddComponent<HorizontalLayoutGroup>();
        inputs.spacing = 12; inputs.childControlWidth = inputs.childControlHeight = true;
        inputs.childForceExpandWidth = true;
        _radius = Input(parameters, "Radius", "半径（m）", font, false);
        _damage = Input(parameters, "Damage", "ダメージ", font, true);
        _seconds = Input(parameters, "Seconds", "無敵時間（秒）", font, false);
        _summary = Label(panel, "Summary", "", font, 22);
        Stretch(_summary.rectTransform, Vector2.zero, new Vector2(1, 0));
        _summary.rectTransform.pivot = new Vector2(.5f, 0);
        _summary.rectTransform.anchoredPosition = new Vector2(0, 62);
        _summary.rectTransform.sizeDelta = new Vector2(0, 36);
        var actions = Rect("Actions", panel);
        Stretch(actions, Vector2.zero, new Vector2(1, 0));
        actions.pivot = new Vector2(.5f, 0); actions.sizeDelta = new Vector2(0, 54);
        var layout = actions.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 12; layout.childControlWidth = layout.childControlHeight = true; layout.childForceExpandWidth = true;
        _cast = Button(actions, "Cast", "発動", font, Cast);
        Button(actions, "Cancel", "キャンセル", font, Cancel);
    }

    private TMP_InputField Input(Transform parent, string name, string title, TMP_FontAsset font, bool integer)
    {
        var row = Rect(name + "Row", parent);
        var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 8; layout.childControlWidth = layout.childControlHeight = true; layout.childForceExpandWidth = true;
        Label(row, "Label", title, font, 22);
        var rect = Rect(name, row);
        rect.gameObject.AddComponent<Image>().color = new Color(.12f, .16f, .26f);
        var field = rect.gameObject.AddComponent<TMP_InputField>();
        var viewport = Rect("TextArea", rect);
        Stretch(viewport, Vector2.zero, Vector2.one);
        viewport.offsetMin = new Vector2(8, 2); viewport.offsetMax = new Vector2(-8, -2);
        viewport.gameObject.AddComponent<RectMask2D>();
        var text = Label(viewport, "Text", "", font, 24);
        Stretch(text.rectTransform, Vector2.zero, Vector2.one);
        field.textViewport = viewport;
        field.textComponent = (TMP_Text)text;
        field.fontAsset = font;
        field.contentType = integer ? TMP_InputField.ContentType.IntegerNumber : TMP_InputField.ContentType.DecimalNumber;
        field.onValueChanged.AddListener(_ => Refresh());
        return field;
    }

    public bool Open(CombatPlayerMagicKind kind, CombatMapSystem map, CombatCharacterSystem characters, Camera camera = null)
    {
        if (map == null || map.CurrentMap == null) return false;
        _kind = kind; _characters = characters;
        var settings = CombatPlayerMagicSettings.Default;
        _radius.SetTextWithoutNotify((_lastArea?.Radius ?? settings.AreaRadius).ToString(CultureInfo.InvariantCulture));
        _damage.SetTextWithoutNotify((_lastArea?.Damage ?? settings.AreaDamage).ToString(CultureInfo.InvariantCulture));
        _seconds.SetTextWithoutNotify((_lastInvulnerable?.Seconds ?? settings.InvulnerableSeconds).ToString(CultureInfo.InvariantCulture));
        bool area = kind == CombatPlayerMagicKind.Area;
        _radius.transform.parent.gameObject.SetActive(area);
        _damage.transform.parent.gameObject.SetActive(area);
        _seconds.transform.parent.gameObject.SetActive(!area);
        gameObject.SetActive(true); transform.SetAsLastSibling();
        _preview.Open(map, characters, camera);
        _preview.InteractionEnabled = area;
        if (area && _lastArea.HasValue) _preview.SetPoint(_lastPoint);
        _center.gameObject.SetActive(area);
        _center.SetAsLastSibling();
        Refresh();
        return true;
    }

    public bool TryGetRequest(out CombatPlayerMagicRequest request)
    {
        request = default;
        if (!IsOpen) return false;
        float.TryParse(_radius.text, NumberStyles.Float, CultureInfo.InvariantCulture, out float radius);
        int.TryParse(_damage.text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int damage);
        float.TryParse(_seconds.text, NumberStyles.Float, CultureInfo.InvariantCulture, out float seconds);
        request = new CombatPlayerMagicRequest(_kind, _preview.WorldPoint, radius, damage, seconds);
        return request.IsValid;
    }

    public void SelectPosition(Vector2 normalized) { if (IsOpen) _preview.SelectPosition(normalized); }
    public void Cast()
    {
        if (!TryGetRequest(out var request)) return;
        if (_kind == CombatPlayerMagicKind.Area) { _lastArea = request; _lastPoint = _preview.Point; }
        else _lastInvulnerable = request;
        Cancel();
        CastRequested?.Invoke(request);
    }
    public void Cancel()
    {
        if (!IsOpen) return;
        _preview.Clear(); gameObject.SetActive(false); Closed?.Invoke();
    }
    public void Clear()
    {
        _lastArea = _lastInvulnerable = null;
        Cancel();
    }
    private void OnDisable()
    {
        if (_preview == null || !_preview.IsOpen) return;
        _preview.Clear(); Closed?.Invoke();
    }
    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) Cancel();
        if (IsOpen) RefreshRange();
    }
    private void Refresh()
    {
        if (!IsOpen) return;
        bool valid = TryGetRequest(out var request);
        _cast.interactable = valid;
        _title.text = _kind == CombatPlayerMagicKind.Area ? "広範囲魔法 — ドラッグで中心を移動" : "全員無敵 — キャラ配置を確認";
        _summary.text = !valid ? "正しい数値を入力してください" : _kind == CombatPlayerMagicKind.Area
            ? $"X: {request.Center.x:0.0}　Z: {request.Center.z:0.0}　敵のみ・詠唱 {CombatPlayerMagicSettings.CastSeconds(request.SkillId):0.##}秒・半径 {request.Radius:0.##}m・{request.Damage}ダメージ"
            : $"両軍の対象キャラ {CombatPlayerMagicSystem.CountInvulnerabilityTargets(_characters)}人に {request.Seconds:0.##}秒間の無敵";
        RefreshRange();
    }
    private void RefreshRange()
    {
        bool show = _kind == CombatPlayerMagicKind.Area && TryGetRequest(out var request);
        _range.gameObject.SetActive(show);
        if (!show) return;
        TryGetRequest(out request);
        _range.color = CombatPlayerMagicSettings.Default.AreaColor;
        _range.rectTransform.sizeDelta = Vector2.one * (request.Radius * 2 * _preview.Scale);
        _preview.PositionMarker(_range.rectTransform, _preview.Point);
        _preview.PositionMarker(_center, _preview.Point);
    }
}

[RequireComponent(typeof(CanvasRenderer))]
public sealed class CombatMagicRangeGraphic : MaskableGraphic
{
    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        Vector2 center = rectTransform.rect.center;
        float radius = rectTransform.rect.width * .5f;
        Color fill = color; fill.a = .18f;
        Color edge = color; edge.a = .9f;
        for (int i = 0; i < 96; i++)
        {
            float a = i * Mathf.PI * 2 / 96, b = (i + 1) * Mathf.PI * 2 / 96;
            Vector2 p = new(Mathf.Cos(a), Mathf.Sin(a)), q = new(Mathf.Cos(b), Mathf.Sin(b));
            int start = mesh.currentVertCount;
            mesh.AddVert(center, fill, Vector2.zero);
            mesh.AddVert(center + p * radius, fill, Vector2.zero);
            mesh.AddVert(center + q * radius, fill, Vector2.zero);
            mesh.AddTriangle(start, start + 1, start + 2);
            start = mesh.currentVertCount;
            mesh.AddVert(center + p * Mathf.Max(0, radius - 2), edge, Vector2.zero);
            mesh.AddVert(center + p * radius, edge, Vector2.zero);
            mesh.AddVert(center + q * radius, edge, Vector2.zero);
            mesh.AddVert(center + q * Mathf.Max(0, radius - 2), edge, Vector2.zero);
            mesh.AddTriangle(start, start + 1, start + 2); mesh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
