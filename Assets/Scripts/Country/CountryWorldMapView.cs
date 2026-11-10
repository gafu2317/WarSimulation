using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class CountryWorldMapView : MonoBehaviour
{
    [SerializeField] private CountryWorldRegionGraphic[] _regions;
    [SerializeField] private Button[] _markers;
    [SerializeField] private Image[] _flags;
    [SerializeField] private TextMeshProUGUI[] _nations;
    [SerializeField] private CountryWorldRouteGraphic _route;
    [SerializeField] private CountryWorldRegionGraphic _continent;
    private CountryExpeditionState _state;
    private Vector2[][] _polygons;
    private int[][] _neighbors;
    private int _selected = -1;
    private bool _refreshPending;
    private void OnRectTransformDimensionsChange()
    {
        if (_neighbors != null && ((RectTransform)transform).rect.width > 0) Refresh(_selected);
    }
    private void LateUpdate()
    {
        if (_refreshPending) Refresh(_selected);
    }
    public void Initialize(CountryExpeditionState state, Action<int> select)
    {
        _state = state;
        _polygons = Enumerable.Range(0, _regions.Length).Select(i => state.Data.Regions[i].Polygon).ToArray();
        _neighbors = new int[_regions.Length][];
        _continent.Bind(state.Data.Coastline, null);
        _continent.SetState(new Color32(184, 194, 169, 255), new Color32(36, 52, 57, 255), 3);
        for (int i = 0; i < _regions.Length; i++)
        {
            int id = i;
            _regions[i].Bind(_polygons[i], () => select(id));
            _neighbors[i] = new int[_polygons[i].Length];
            for (int edge = 0; edge < _polygons[i].Length; edge++)
            {
                _neighbors[i][edge] = -1;
                var a = _polygons[i][edge]; var b = _polygons[i][(edge + 1) % _polygons[i].Length];
                for (int other = 0; other < _polygons.Length; other++)
                    if (other != i)
                        for (int e = 0; e < _polygons[other].Length; e++)
                            if ((a - _polygons[other][(e + 1) % _polygons[other].Length]).sqrMagnitude < .00000001f && (b - _polygons[other][e]).sqrMagnitude < .00000001f)
                                _neighbors[i][edge] = other;
            }
        }
        for (int i = 0; i < _markers.Length; i++)
        {
            int capital = state.Data.Capital(i);
            _markers[i].onClick.AddListener(() => select(capital));
        }
    }
    public void Refresh(int selected)
    {
        _selected = selected;
        if (CanvasUpdateRegistry.IsRebuildingLayout() || CanvasUpdateRegistry.IsRebuildingGraphics())
        {
            _refreshPending = true;
            return;
        }
        _refreshPending = false;
        var data = _state.Data;
        for (int i = 0; i < _regions.Length; i++)
        {
            bool available = _state.CanTarget(i), chosen = selected == i;
            var widths = _neighbors[i].Select(neighbor => chosen ? 6 : available ? 3.5f : neighbor < 0 || _state.Owner(neighbor) != _state.Owner(i) ? 4 : 2.2f).ToArray();
            _regions[i].SetState(data.Nations[_state.Owner(i)].Color, chosen ? available ? CountryGovernmentTheme.Accent : CountryGovernmentTheme.DisabledText : available ? new Color32(230, 224, 186, 255) : new Color32(45, 60, 65, 255), 3, widths);
            if (available) _regions[i].transform.SetAsLastSibling();
        }
        if (selected >= 0) _regions[selected].transform.SetAsLastSibling();
        for (int nation = 0; nation < _nations.Length; nation++)
        {
            var owned = Enumerable.Range(0, _regions.Length).Where(i => _state.Owner(i) == nation).ToArray();
            _nations[nation].gameObject.SetActive(owned.Length > 0);
            int capital = data.Capital(nation);
            bool capitalOwned = capital >= 0 && _state.Owner(capital) == nation;
            _markers[nation].gameObject.SetActive(capitalOwned);
            _flags[nation].color = data.Nations[nation].Color;
            if (owned.Length == 0) continue;
            var center = owned.Select(i => data.Regions[i].Center).Aggregate(Vector2.zero, (a, b) => a + b) / owned.Length;
            int labelRegion = capitalOwned ? capital : owned.OrderBy(i => (data.Regions[i].Center - center).sqrMagnitude).First();
            var rect = _nations[nation].rectTransform;
            var origin = data.Regions[labelRegion].Center;
            var planeSize = ((RectTransform)transform).rect.size;
            float scale = planeSize.x / 1320;
            var text = rect.Find("Text").GetComponent<TextMeshProUGUI>();
            text.fontSize = 32 * scale;
            var half = Vector2.Scale(text.GetPreferredValues(), new Vector2(1 / planeSize.x, 1 / planeSize.y)) * .5f;
            var position = origin + new Vector2(0, origin.y < .3f ? .115f : -.055f);
            foreach (var offset in new[] { new Vector2(0,-64), new Vector2(-24,-64), new Vector2(24,-64), new Vector2(-48,-64), new Vector2(48,-64), new Vector2(0,96), new Vector2(-24,96), new Vector2(24,96) })
            {
                var candidate = origin + Vector2.Scale(offset * scale, new Vector2(1 / planeSize.x, 1 / planeSize.y));
                if (!new[] { candidate + half, candidate - half, candidate + new Vector2(half.x,-half.y), candidate + new Vector2(-half.x,half.y) }
                    .All(point => owned.Any(id => CountryWorldMapData.Contains(_polygons[id], point)))) continue;
                position = candidate; break;
            }
            rect.anchorMin = rect.anchorMax = position;
            rect.pivot = Vector2.one * .5f;
            rect.anchoredPosition = Vector2.zero;
        }
        _route.gameObject.SetActive(_state.CanTarget(selected));
        if (selected >= 0) _route.SetRoute(data.Regions[data.HomeRegion].Center, data.Regions[selected].Center);
    }
}
