using UnityEngine;

public sealed class CountryExpeditionPageLayout : MonoBehaviour
{
    [SerializeField] private RectTransform _viewport;
    [SerializeField] private UnityEngine.UI.LayoutElement _map, _formation;
    [SerializeField] private UnityEngine.UI.ScrollRect _list;
    [SerializeField] private UnityEngine.UI.ScrollRect _details;
    [SerializeField] private RectTransform _right;
    private float _height = -1;
    private bool _refreshing;

    private void OnEnable() { if (Application.IsPlaying(gameObject)) Refresh(); }
    private void OnRectTransformDimensionsChange() { if (isActiveAndEnabled && Application.IsPlaying(gameObject)) Refresh(); }

    private void LateUpdate()
    {
        if (_list == null || _details == null || _right == null || _height <= 0) return;
        float detailsExtra = _right.rect.height - _details.viewport.rect.height;
        float rightHeight = Mathf.Max(808, _height - 328, UnityEngine.UI.LayoutUtility.GetPreferredHeight(_details.content) + detailsExtra);
        if (Mathf.Abs(_right.rect.height - rightHeight) >= 1)
            _right.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, rightHeight);
        float otherHeight = _formation.preferredHeight - _list.viewport.rect.height;
        float needed = UnityEngine.UI.LayoutUtility.GetPreferredHeight(_list.content) + otherHeight;
        float height = Mathf.Max(_height, needed);
        if (Mathf.Abs(height - _formation.preferredHeight) < 1) return;
        _formation.minHeight = _formation.preferredHeight = height;
    }

    public void Refresh()
    {
        if (_refreshing || !isActiveAndEnabled) return;
        var canvas = GetComponentInParent<Canvas>(true);
        if (canvas == null) return;
        _refreshing = true;
        canvas = canvas.rootCanvas;
        var rect = (RectTransform)transform;
        var offset = rect.offsetMin;
        offset.y = -UnityEngine.Device.Screen.safeArea.yMin / canvas.scaleFactor;
        if (!Mathf.Approximately(rect.offsetMin.y, offset.y)) rect.offsetMin = offset;
        var layout = _viewport.GetComponentInChildren<UnityEngine.UI.VerticalLayoutGroup>();
        float height = _viewport.rect.height - layout.padding.vertical;
        if (height <= 0 || Mathf.Approximately(height, _height)) { _refreshing = false; return; }
        _height = height;
        var body = (RectTransform)_right.Find("Body");
        body.offsetMax = new Vector2(body.offsetMax.x, 0);
        var portrait = (RectTransform)_right.Find("Body/Portrait");
        portrait.anchorMin = new Vector2(0, portrait.anchorMin.y);
        portrait.anchorMax = new Vector2(0, portrait.anchorMax.y);
        portrait.pivot = new Vector2(0, portrait.pivot.y);
        portrait.anchoredPosition = new Vector2(0, portrait.anchoredPosition.y);
        portrait.sizeDelta = new Vector2(340, 680);
        var detailsRect = (RectTransform)_details.transform;
        detailsRect.anchorMin = new Vector2(0, detailsRect.anchorMin.y);
        detailsRect.offsetMin = new Vector2(356, detailsRect.offsetMin.y);
        detailsRect.offsetMax = new Vector2(detailsRect.offsetMax.x, -128);
        var nameRect = (RectTransform)_right.Find("Name");
        nameRect.offsetMin = new Vector2(356, nameRect.offsetMin.y);
        nameRect.anchoredPosition = new Vector2(nameRect.anchoredPosition.x, -28);
        var subtitleRect = (RectTransform)_right.Find("Subtitle");
        subtitleRect.offsetMin = new Vector2(356, subtitleRect.offsetMin.y);
        _right.Find("Name").GetComponent<TMPro.TextMeshProUGUI>().alignment = TMPro.TextAlignmentOptions.MidlineLeft;
        _right.Find("Subtitle").GetComponent<TMPro.TextMeshProUGUI>().alignment = TMPro.TextAlignmentOptions.MidlineLeft;
        _map.minHeight = _map.preferredHeight = Mathf.Max(1, _viewport.rect.height - layout.padding.top - 24);
        _formation.minHeight = _formation.preferredHeight = height;
        _refreshing = false;
    }
}
