using UnityEngine;
using UnityEngine.EventSystems;

public sealed class CountryNestedScrollInput : MonoBehaviour, IInitializePotentialDragHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IScrollHandler
{
    [SerializeField] private UnityEngine.UI.ScrollRect _inner, _outer;
    private UnityEngine.UI.ScrollRect _dragTarget;
    public void Configure(UnityEngine.UI.ScrollRect inner, UnityEngine.UI.ScrollRect outer)
    { _inner = inner; _outer = outer; }

    public void OnInitializePotentialDrag(PointerEventData data)
    {
        _inner.OnInitializePotentialDrag(data);
        _outer.OnInitializePotentialDrag(data);
    }

    public void OnBeginDrag(PointerEventData data)
    {
        if (data.button != PointerEventData.InputButton.Left) return;
        Vector2 delta = data.position - data.pressPosition;
        _dragTarget = _inner.horizontal && Mathf.Abs(delta.x) > Mathf.Abs(delta.y)
            ? _inner : CanScrollVertically(delta.y) ? _inner : _outer;
        StopOther(_dragTarget);
        _dragTarget.OnBeginDrag(data);
    }

    public void OnDrag(PointerEventData data) => _dragTarget?.OnDrag(data);
    public void OnEndDrag(PointerEventData data)
    {
        _dragTarget?.OnEndDrag(data);
        _dragTarget = null;
    }

    public void OnScroll(PointerEventData data)
    {
        var target = _inner.horizontal && Mathf.Abs(data.scrollDelta.x) > Mathf.Abs(data.scrollDelta.y)
            ? _inner : CanScrollVertically(-data.scrollDelta.y) ? _inner : _outer;
        StopOther(target);
        target.OnScroll(data);
    }

    private bool CanScrollVertically(float delta)
    {
        if (!_inner.isActiveAndEnabled || !_inner.vertical || _inner.content.rect.height <= _inner.viewport.rect.height + .5f) return false;
        float position = _inner.verticalNormalizedPosition;
        return delta > 0 ? position > .001f : position < .999f;
    }

    private void StopOther(UnityEngine.UI.ScrollRect target)
    {
        (target == _inner ? _outer : _inner).StopMovement();
    }

    private void OnDisable() => _dragTarget = null;
}
