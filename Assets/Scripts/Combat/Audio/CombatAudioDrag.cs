using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public sealed class CombatAudioDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler, IPointerEnterHandler, IPointerExitHandler
{
    private static AudioClip _dragged;
    private static CombatAudioDrag _source;
    private RectTransform _floatingCard;
    private Canvas _canvas;
    private CanvasGroup _sourceGroup;
    private float _sourceAlpha;
    private UnityEngine.UI.Outline _highlight;
    public AudioClip Clip;
    public Action<AudioClip> Assign;

    public void OnBeginDrag(PointerEventData data)
    {
        if (Clip == null) return;
        if (_source != null) _source.EndDrag();
        _source = this;
        _dragged = Clip;
        _canvas = GetComponentInParent<Canvas>().rootCanvas;
        _sourceGroup = GetComponent<CanvasGroup>();
        if (_sourceGroup == null) _sourceGroup = gameObject.AddComponent<CanvasGroup>();
        _sourceAlpha = _sourceGroup.alpha;
        _sourceGroup.alpha = .45f;

        var card = new GameObject("DraggedAudio", typeof(RectTransform), typeof(CanvasGroup), typeof(Image), typeof(Shadow));
        _floatingCard = (RectTransform)card.transform;
        _floatingCard.SetParent(_canvas.transform, false);
        _floatingCard.sizeDelta = new Vector2(Mathf.Clamp(((RectTransform)transform).rect.width, 180, 420), 44);
        _floatingCard.localScale = Vector3.one * 1.06f;
        card.GetComponent<CanvasGroup>().blocksRaycasts = false;
        card.GetComponent<Image>().color = new Color(.25f, .39f, .55f);
        var shadow = card.GetComponent<Shadow>();
        shadow.effectColor = new Color(0, 0, 0, .55f);
        shadow.effectDistance = new Vector2(5, -7);
        var label = Instantiate(GetComponentInChildren<TextMeshProUGUI>(), card.transform);
        label.text = Clip.name;
        label.raycastTarget = false;
        var rect = label.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(12, 0);
        rect.offsetMax = new Vector2(-12, 0);
        OnDrag(data);
    }

    public void OnDrag(PointerEventData data)
    {
        if (_floatingCard == null) return;
        Camera camera = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)_canvas.transform, data.position, camera, out var point))
            _floatingCard.localPosition = point + new Vector2(24, 30);
    }

    public void OnEndDrag(PointerEventData data) => EndDrag();

    private void EndDrag()
    {
        if (_floatingCard != null)
        {
            _floatingCard.gameObject.SetActive(false);
            Destroy(_floatingCard.gameObject);
            _floatingCard = null;
        }
        if (_sourceGroup != null) _sourceGroup.alpha = _sourceAlpha;
        if (_source != this) return;
        _source = null;
        _dragged = null;
    }

    public void OnPointerEnter(PointerEventData data)
    {
        if (Assign == null || _dragged == null) return;
        if (_highlight == null)
        {
            _highlight = gameObject.AddComponent<UnityEngine.UI.Outline>();
            _highlight.effectColor = new Color(.3f, 1f, .75f);
            _highlight.effectDistance = new Vector2(3, -3);
        }
        _highlight.enabled = true;
    }

    public void OnPointerExit(PointerEventData data)
    {
        if (_highlight != null) _highlight.enabled = false;
    }

    private void LateUpdate()
    {
        if (_dragged == null && _highlight != null) _highlight.enabled = false;
    }

    private void OnDisable()
    {
        OnPointerExit(null);
        if (_source == this) EndDrag();
    }

    public void OnDrop(PointerEventData data)
    {
        if (_dragged != null) Assign?.Invoke(_dragged);
    }

#if UNITY_EDITOR
    private void OnGUI()
    {
        if (Assign == null) return;
        Event evt = Event.current;
        if (evt.type != EventType.DragUpdated && evt.type != EventType.DragPerform) return;
        var canvas = GetComponentInParent<Canvas>();
        Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        Vector2 point = new(evt.mousePosition.x, Screen.height - evt.mousePosition.y);
        if (!RectTransformUtility.RectangleContainsScreenPoint((RectTransform)transform, point, camera)) return;
        foreach (UnityEngine.Object item in UnityEditor.DragAndDrop.objectReferences)
        {
            if (item is not AudioClip audio) continue;
            UnityEditor.DragAndDrop.visualMode = UnityEditor.DragAndDropVisualMode.Copy;
            if (evt.type == EventType.DragPerform)
            {
                UnityEditor.DragAndDrop.AcceptDrag();
                Assign(audio);
            }
            evt.Use();
            break;
        }
    }
#endif
}
