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
    private RectTransform _dragTarget, _cardViewport, _pendingPortrait;
    private Action _drop;
    private Func<bool> _canDrag;
    private UnityEngine.UI.ScrollRect _scroll;
    private RectTransform _ghost;
    private UnityEngine.UI.Image _weaponBadge;
    private bool _categoryIcon;
    public void EnableDrag(RectTransform target, Action drop, Func<bool> canDrag = null, RectTransform cardViewport = null)
    {
        _dragTarget = target; _drop = drop; _canDrag = canDrag;
        _cardViewport = cardViewport;
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
        if (e.button != PointerEventData.InputButton.Left) return;
        if (_dragTarget != null && (_canDrag == null || _canDrag()) && transform.Find("Portrait") is RectTransform portrait)
        {
            if (RectTransformUtility.RectangleContainsScreenPoint(portrait, e.pressPosition, e.pressEventCamera))
            {
                ShowDraggedPortrait(portrait);
                MoveGhost(e);
                return;
            }
            if (_cardViewport != null) _pendingPortrait = portrait;
        }
        if (_scroll != null && _scroll.enabled) _scroll.OnBeginDrag(e);
    }

    private void ShowDraggedPortrait(RectTransform portrait)
    {
        _pendingPortrait = null;
        var canvas = GetComponentInParent<Canvas>();
        _ghost = Instantiate(portrait, canvas.transform);
        _ghost.name = "DraggedSoldier";
        foreach (var graphic in _ghost.GetComponentsInChildren<UnityEngine.UI.Graphic>(true)) graphic.raycastTarget = false;
        _ghost.SetAsLastSibling();
        _ghost.anchorMin = _ghost.anchorMax = _ghost.pivot = new Vector2(.5f, .5f);
        _ghost.sizeDelta = portrait.rect.size;
    }

    public void OnDrag(PointerEventData e)
    {
        if (_pendingPortrait != null && !RectTransformUtility.RectangleContainsScreenPoint(_cardViewport, e.position, e.pressEventCamera))
        {
            if (_scroll != null && _scroll.enabled)
            {
                _scroll.OnEndDrag(e);
                _scroll.StopMovement();
            }
            ShowDraggedPortrait(_pendingPortrait);
        }
        if (_ghost != null) MoveGhost(e);
        else if (_scroll != null && _scroll.enabled) _scroll.OnDrag(e);
    }

    public void OnEndDrag(PointerEventData e)
    {
        if (e.button != PointerEventData.InputButton.Left) return;
        bool drop = (_ghost != null || _pendingPortrait != null) && _dragTarget != null
            && RectTransformUtility.RectangleContainsScreenPoint(_dragTarget, e.position, e.pressEventCamera)
            && (_canDrag == null || _canDrag());
        if (_ghost == null && _scroll != null && _scroll.enabled) _scroll.OnEndDrag(e);
        ClearDrag();
        if (drop) _drop?.Invoke();
    }

    private void ClearDrag()
    {
        if (_ghost != null) Destroy(_ghost.gameObject);
        _ghost = null; _pendingPortrait = null;
    }
    private void MoveGhost(PointerEventData e)
    {
        if (_ghost == null) return;
        var parent = (RectTransform)_ghost.parent;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, e.position, e.pressEventCamera, out var point)) _ghost.localPosition = point;
    }
    private void OnDisable() => ClearDrag();
    public void Bind(UnityAction selected) => _button.onClick.AddListener(selected);
    public void SetCaptionVisible(bool visible) => _state.gameObject.SetActive(visible);
    public void SetNameHighlighted(bool highlighted) => _name.color = highlighted ? CountryGovernmentTheme.AccentText : CountryGovernmentTheme.Text;
    public void SetWeaponIcon(Sprite sprite)
    {
        if (_weaponBadge == null)
        {
            if (sprite == null) return;
            var portrait = (RectTransform)transform.Find("Portrait");
            var badge = new GameObject("WeaponBadge", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            var rect = (RectTransform)badge.transform;
            rect.SetParent(portrait, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
            const float weaponInset = 16f;
            rect.anchoredPosition = new Vector2(weaponInset - portrait.anchoredPosition.x, -6);
            rect.sizeDelta = new Vector2(50, 50);
            _weaponBadge = badge.GetComponent<UnityEngine.UI.Image>();
            _weaponBadge.preserveAspect = true;
            _weaponBadge.raycastTarget = false;
        }
        _weaponBadge.sprite = sprite;
        _weaponBadge.gameObject.SetActive(sprite != null);
    }

    private void SetCategoryIcon(bool categoryIcon)
    {
        if (_categoryIcon == categoryIcon) return;
        _categoryIcon = categoryIcon;
        var clip = (RectTransform)_icon.transform.parent;
        var portrait = clip.parent;
        clip.GetComponent<UnityEngine.UI.Mask>().enabled = !categoryIcon;
        clip.GetComponent<CountryCircleGraphic>().enabled = !categoryIcon;
        portrait.GetComponent<CountryCircleGraphic>().enabled = !categoryIcon;
        portrait.Find("Edge").gameObject.SetActive(!categoryIcon);
        _icon.rectTransform.offsetMin = categoryIcon ? -clip.offsetMin : Vector2.zero;
        _icon.rectTransform.offsetMax = categoryIcon ? -clip.offsetMax : Vector2.zero;
    }

    public void SetDimmed(bool dimmed)
    {
        var group = GetComponent<CanvasGroup>();
        if (group != null) group.alpha = 1;
    }
    public void Refresh(string name, Sprite icon, string state, bool selected, bool unavailable = false, bool memberSelected = false, bool categoryIcon = false)
    {
        SetCategoryIcon(categoryIcon);
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
