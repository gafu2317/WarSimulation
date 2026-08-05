using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class CombatCharacterFocusMarker : MonoBehaviour
{
    private const string OverlayCanvasName = "CombatFocusMarkerOverlayCanvas";
    private static readonly Color MarkerColor = new(1f, 0.85f, 0.1f, 0.55f);
    private const float ScreenSizePixels = 52f;
    private const float HeightOffset = 0.05f;
    private const int OverlaySortingOrder = 100;

    private static Canvas s_overlayCanvas;
    private static RectTransform s_overlayCanvasRect;
    private static Sprite s_whiteSprite;

    private Character _character;
    private RectTransform _markerRect;

    public static void EnsureFor(Character character)
    {
        if (character == null)
        {
            return;
        }

        CombatCharacterFocusMarker marker = character.GetComponent<CombatCharacterFocusMarker>();
        if (marker == null)
        {
            marker = character.gameObject.AddComponent<CombatCharacterFocusMarker>();
        }

        marker.Refresh();
    }

    private void Awake()
    {
        _character = GetComponent<Character>();
        CombatPartyFocus.Changed += Refresh;
        EnsureBuilt();
        Refresh();
    }

    private void OnDestroy()
    {
        CombatPartyFocus.Changed -= Refresh;
        if (_markerRect != null)
        {
            Destroy(_markerRect.gameObject);
            _markerRect = null;
        }
    }

    private void LateUpdate()
    {
        if (_markerRect == null)
        {
            return;
        }

        bool selected = _character != null && _character == CombatPartyFocus.Selected;
        if (!selected)
        {
            if (_markerRect.gameObject.activeSelf)
            {
                _markerRect.gameObject.SetActive(false);
            }

            return;
        }

        Camera camera = Camera.main;
        if (camera == null || s_overlayCanvasRect == null)
        {
            return;
        }

        Vector3 worldPosition = transform.position + Vector3.up * HeightOffset;
        Vector3 screenPoint = camera.WorldToScreenPoint(worldPosition);
        if (screenPoint.z <= 0f)
        {
            if (_markerRect.gameObject.activeSelf)
            {
                _markerRect.gameObject.SetActive(false);
            }

            return;
        }

        if (!_markerRect.gameObject.activeSelf)
        {
            _markerRect.gameObject.SetActive(true);
        }

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                s_overlayCanvasRect,
                screenPoint,
                null,
                out Vector2 localPoint))
        {
            _markerRect.anchoredPosition = localPoint;
        }
    }

    private void Refresh()
    {
        EnsureBuilt();
        bool visible = _character != null && _character == CombatPartyFocus.Selected;
        if (_markerRect != null)
        {
            _markerRect.gameObject.SetActive(visible);
        }
    }

    private void EnsureBuilt()
    {
        if (_markerRect != null)
        {
            return;
        }

        EnsureOverlayCanvas();
        if (s_overlayCanvasRect == null)
        {
            return;
        }

        var root = new GameObject("FocusMarker", typeof(RectTransform), typeof(Image));
        root.transform.SetParent(s_overlayCanvasRect, worldPositionStays: false);
        _markerRect = root.GetComponent<RectTransform>();
        _markerRect.anchorMin = new Vector2(0.5f, 0.5f);
        _markerRect.anchorMax = new Vector2(0.5f, 0.5f);
        _markerRect.pivot = new Vector2(0.5f, 0.5f);
        _markerRect.sizeDelta = new Vector2(ScreenSizePixels, ScreenSizePixels);
        _markerRect.anchoredPosition = Vector2.zero;

        Image image = root.GetComponent<Image>();
        image.sprite = GetWhiteSprite();
        image.color = MarkerColor;
        image.raycastTarget = false;
    }

    private static void EnsureOverlayCanvas()
    {
        if (s_overlayCanvas != null && s_overlayCanvasRect != null)
        {
            return;
        }

        GameObject existing = GameObject.Find(OverlayCanvasName);
        if (existing != null)
        {
            s_overlayCanvas = existing.GetComponent<Canvas>();
            s_overlayCanvasRect = existing.GetComponent<RectTransform>();
            if (s_overlayCanvas != null && s_overlayCanvasRect != null)
            {
                s_overlayCanvas.sortingOrder = OverlaySortingOrder;
                return;
            }
        }

        var canvasObject = new GameObject(OverlayCanvasName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        s_overlayCanvas = canvasObject.GetComponent<Canvas>();
        s_overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        s_overlayCanvas.sortingOrder = OverlaySortingOrder;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        scaler.scaleFactor = 1f;
        scaler.referencePixelsPerUnit = 100f;

        s_overlayCanvasRect = canvasObject.GetComponent<RectTransform>();
        s_overlayCanvasRect.anchorMin = Vector2.zero;
        s_overlayCanvasRect.anchorMax = Vector2.one;
        s_overlayCanvasRect.offsetMin = Vector2.zero;
        s_overlayCanvasRect.offsetMax = Vector2.zero;
    }

    private static Sprite GetWhiteSprite()
    {
        if (s_whiteSprite != null)
        {
            return s_whiteSprite;
        }

        var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        texture.hideFlags = HideFlags.HideAndDontSave;
        s_whiteSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 100f);
        s_whiteSprite.hideFlags = HideFlags.HideAndDontSave;
        return s_whiteSprite;
    }
}
