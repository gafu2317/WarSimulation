using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using System;

public sealed class CountryRosterRow : MonoBehaviour, IInitializePotentialDragHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] private UnityEngine.UI.Button _button;
    [SerializeField] private UnityEngine.UI.Image _icon;
    [SerializeField] private CountrySoldierSilhouette _placeholder;
    [SerializeField] private TextMeshProUGUI _name, _state;
    [SerializeField] private GameObject _selected;
    private RectTransform _dragTarget;
    private Action _drop;
    private Func<bool> _canDrag;
    private UnityEngine.UI.ScrollRect _scroll;
    private RectTransform _ghost;
    private UnityEngine.UI.Image _weaponIcon;
    private GameObject _weaponBadge;
    private bool _draggingIcon;
    public void EnableDrag(RectTransform target, Action drop, Func<bool> canDrag = null)
    {
        _dragTarget = target; _drop = drop; _canDrag = canDrag;
        foreach (var scroll in GetComponentsInParent<UnityEngine.UI.ScrollRect>())
            if (scroll.enabled) { _scroll = scroll; break; }
    }
    public void OnInitializePotentialDrag(PointerEventData e)
    {
        if (_scroll == null)
            foreach (var scroll in GetComponentsInParent<UnityEngine.UI.ScrollRect>())
                if (scroll.enabled) { _scroll = scroll; break; }
        if (_scroll != null && _scroll.enabled) _scroll.OnInitializePotentialDrag(e);
    }
    public void OnBeginDrag(PointerEventData e)
    {
        if (_dragTarget == null) { if (_scroll != null && _scroll.enabled) _scroll.OnBeginDrag(e); return; }
        var portrait = transform.Find("Portrait") as RectTransform;
        _draggingIcon = portrait != null && (_canDrag == null || _canDrag()) && RectTransformUtility.RectangleContainsScreenPoint(portrait, e.pressPosition, e.pressEventCamera);
        if (!_draggingIcon) { if (_scroll != null && _scroll.enabled) _scroll.OnBeginDrag(e); return; }
        var canvas = GetComponentInParent<Canvas>().rootCanvas;
        _ghost = Instantiate(portrait, canvas.transform);
        _ghost.name = "DraggedSoldier";
        foreach (var graphic in _ghost.GetComponentsInChildren<UnityEngine.UI.Graphic>(true)) graphic.raycastTarget = false;
        _ghost.SetAsLastSibling();
        _ghost.anchorMin = _ghost.anchorMax = _ghost.pivot = new Vector2(.5f, .5f);
        _ghost.sizeDelta = portrait.rect.size;
        MoveGhost(e);
    }
    public void OnDrag(PointerEventData e)
    { if (_draggingIcon) MoveGhost(e); else if (_scroll != null && _scroll.enabled) _scroll.OnDrag(e); }
    public void OnEndDrag(PointerEventData e)
    {
        if (!_draggingIcon) { if (_scroll != null && _scroll.enabled) _scroll.OnEndDrag(e); return; }
        if (_ghost != null) Destroy(_ghost.gameObject);
        _ghost = null; _draggingIcon = false;
        if (RectTransformUtility.RectangleContainsScreenPoint(_dragTarget, e.position, e.pressEventCamera)) _drop?.Invoke();
    }
    private void MoveGhost(PointerEventData e)
    {
        if (_ghost == null) return;
        var parent = (RectTransform)_ghost.parent;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, e.position, e.pressEventCamera, out var point)) _ghost.anchoredPosition = point;
    }
    private void OnDisable() { if (_ghost != null) Destroy(_ghost.gameObject); _ghost = null; _draggingIcon = false; }
    public void Bind(UnityAction selected) => _button.onClick.AddListener(selected);
    public void SetCaptionVisible(bool visible) => _state.gameObject.SetActive(visible);
    public void SetNameHighlighted(bool highlighted) => _name.color = highlighted ? CountryGovernmentTheme.AccentText : CountryGovernmentTheme.Text;
    public void SetWeaponIcon(Sprite sprite)
    {
        if (_weaponBadge == null)
        {
            var portrait = (RectTransform)transform.Find("Portrait");
            var rect = Instantiate(portrait, portrait);
            rect.name = "WeaponBadge";
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1, 0);
            rect.anchoredPosition = new Vector2(6, -6); rect.sizeDelta = new Vector2(50, 50);
            _weaponBadge = rect.gameObject;
            _weaponIcon = rect.Find("Clip/Icon").GetComponent<UnityEngine.UI.Image>();
            _weaponIcon.enabled = true;
            var silhouette = rect.GetComponentInChildren<CountrySoldierSilhouette>(true);
            if (silhouette != null) silhouette.gameObject.SetActive(false);
            foreach (var graphic in rect.GetComponentsInChildren<UnityEngine.UI.Graphic>(true)) graphic.raycastTarget = false;
        }
        _weaponIcon.sprite = sprite;
        _weaponBadge.SetActive(sprite != null);
    }

    public void SetDimmed(bool dimmed)
    {
        var group = GetComponent<CanvasGroup>();
        if (group != null) group.alpha = 1;
    }
    public void Refresh(string name, Sprite icon, string state, bool selected, bool unavailable = false, bool memberSelected = false)
    {
        _name.text = name; _state.text = state;
        if (_name.gameObject.activeSelf)
        {
            float height = Mathf.Max(44, Mathf.Ceil(_state.GetPreferredValues(state, Mathf.Max(1, _state.rectTransform.rect.width), 0).y));
            _state.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 108 + height;
        }
        _icon.sprite = icon; _icon.enabled = icon != null; _placeholder.enabled = icon == null;
        _selected.SetActive(selected);
        _name.color = CountryGovernmentTheme.Text;
        _state.color = memberSelected ? CountryGovernmentTheme.Positive : CountryGovernmentTheme.Muted;
        CountryGovernmentTheme.Colors(_button, selected ? CountryGovernmentTheme.Selected : CountryGovernmentTheme.Panel);
    }
}
