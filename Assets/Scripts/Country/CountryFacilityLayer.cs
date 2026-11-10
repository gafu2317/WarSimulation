using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class CountryFacilityTarget
{
    public Transform building;
    public Vector3 localAnchor;
    public string name, category, description;
    public Color accent;
}

public sealed class CountryFacilityLayer : MonoBehaviour
{
    [SerializeField] private Camera _viewCamera;
    [SerializeField] private CountryComparisonView _country;
    [SerializeField] private RectTransform _bannerArea;
    [SerializeField] private CountryFacilityBanner _bannerPrefab;
    [SerializeField] private CountryFacilityPopup _popup;
    [SerializeField] private RectTransform[] _obstacles = Array.Empty<RectTransform>();
    [SerializeField] private CountryFacilityTarget[] _targets = Array.Empty<CountryFacilityTarget>();
    private CountryFacilityBanner[] _banners;
    private Rect[] _reserved, _occupied;
    private readonly Vector3[] _corners = new Vector3[4];
    public IReadOnlyList<CountryFacilityTarget> Targets => _targets;
    public IReadOnlyList<CountryFacilityBanner> Banners => _banners;

    public void Bind(Camera viewCamera, CountryComparisonView country, CountryFacilityTarget[] targets)
    {
        _viewCamera = viewCamera;
        _country = country;
        _targets = targets;
    }

    private void Awake()
    {
        _banners = new CountryFacilityBanner[_targets.Length];
        _occupied = new Rect[_targets.Length];
        _reserved = new Rect[_obstacles.Length];
        for (int i = 0; i < _targets.Length; i++)
        {
            _banners[i] = Instantiate(_bannerPrefab, _bannerArea);
            _banners[i].name = _targets[i].name;
            _banners[i].Initialize(_targets[i], _popup);
        }
        if (_viewCamera != null) _popup.BindControls(_viewCamera.GetComponent<CountryCameraController>(), _country);
    }

    private void OnEnable() => Canvas.preWillRenderCanvases += Refresh;
    private void OnDisable()
    {
        Canvas.preWillRenderCanvases -= Refresh;
        _popup?.Close();
    }

    private void Refresh()
    {
        if (_viewCamera == null || _banners == null) return;
        for (int i = 0; i < _obstacles.Length; i++)
        {
            _obstacles[i].GetWorldCorners(_corners);
            Vector2 min = _bannerArea.InverseTransformPoint(_corners[0]);
            Vector2 max = _bannerArea.InverseTransformPoint(_corners[2]);
            _reserved[i] = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
        int occupied = 0;
        for (int i = 0; i < _banners.Length; i++)
        {
            var banner = _banners[i];
            var target = banner.Target;
            bool visible = target.building != null && target.building.gameObject.activeInHierarchy;
            Vector2 point = default;
            if (visible)
            {
                Vector3 screen = _viewCamera.WorldToScreenPoint(target.building.TransformPoint(target.localAnchor));
                visible = screen.z > _viewCamera.nearClipPlane
                    && RectTransformUtility.ScreenPointToLocalPointInRectangle(_bannerArea, screen, null, out point);
            }
            Rect desired = new(point.x - banner.Rect.rect.width * .5f, point.y, banner.Rect.rect.width, banner.Rect.rect.height);
            Rect placed = desired;
            visible &= TryPlace(_bannerArea.rect, desired, _reserved, _occupied, occupied, out placed);
            if (banner.gameObject.activeSelf != visible) banner.gameObject.SetActive(visible);
            if (!visible) continue;
            banner.Position(new Vector2(placed.center.x, placed.yMin), placed.yMin - desired.yMin);
            _occupied[occupied++] = placed;
        }
    }

    public static bool TryPlace(Rect area, Rect desired, Rect[] reserved, Rect[] occupied, int count, out Rect placed)
    {
        placed = desired;
        for (int attempt = 0; attempt <= count; attempt++)
        {
            if (placed.xMin < area.xMin || placed.xMax > area.xMax || placed.yMin < area.yMin || placed.yMax > area.yMax) return false;
            foreach (Rect obstacle in reserved) if (placed.Overlaps(obstacle)) return false;
            bool overlap = false;
            for (int i = 0; i < count; i++)
            {
                if (!placed.Overlaps(occupied[i])) continue;
                placed.y = occupied[i].yMax + 8;
                overlap = true;
                break;
            }
            if (!overlap) return true;
        }
        return false;
    }
}
