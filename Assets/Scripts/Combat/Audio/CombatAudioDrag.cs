using System;
using UnityEngine;
using UnityEngine.EventSystems;

public sealed class CombatAudioDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
{
    private static AudioClip _dragged;
    public AudioClip Clip;
    public Action<AudioClip> Assign;
    public void OnBeginDrag(PointerEventData data) => _dragged = Clip;
    public void OnDrag(PointerEventData data) { }
    public void OnEndDrag(PointerEventData data) => _dragged = null;
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
