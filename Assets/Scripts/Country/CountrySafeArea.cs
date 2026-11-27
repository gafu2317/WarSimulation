using UnityEngine;
using Screen = UnityEngine.Device.Screen;

[ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(Canvas))]
public sealed class CountrySafeArea : MonoBehaviour
{
    [System.Serializable]
    public struct Target
    {
        public RectTransform Rect;
        public Vector2 Min, Max;
        public bool Horizontal, Vertical;
    }

    [SerializeField] private bool _matchPhoneOnDesktopWeb;
    [SerializeField] private Target[] _targets = System.Array.Empty<Target>();
    private DrivenRectTransformTracker _tracker;
    private Rect _area;
    private Vector2Int _size;

    private void OnEnable()
    {
#if UNITY_EDITOR
        if (UnityEditor.SceneManagement.EditorSceneManager.IsPreviewScene(gameObject.scene)) return;
#endif
        _size = default;
        Canvas.preWillRenderCanvases += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        Canvas.preWillRenderCanvases -= Refresh;
        if (_size != default) foreach (var target in _targets)
        {
            if (target.Rect == null) continue;
            target.Rect.anchorMin = target.Min;
            target.Rect.anchorMax = target.Max;
        }
        _tracker.Clear();
        _size = default;
    }

    private void Update() => Refresh();
    private void OnValidate() => _size = default;

    private void Refresh()
    {
#if UNITY_EDITOR
        if (UnityEditor.SceneManagement.EditorSceneManager.IsPreviewScene(gameObject.scene)) return;
#endif
        var size = new Vector2Int(Screen.width, Screen.height);
        var displaySize = GetComponent<Canvas>().renderingDisplaySize;
        if (_size == default)
        {
            var display = Vector2Int.RoundToInt(displaySize);
            Apply(new Rect(Vector2.zero, displaySize), display);
        }
        if (Mathf.RoundToInt(displaySize.x) != size.x || Mathf.RoundToInt(displaySize.y) != size.y) return;
        Apply(_matchPhoneOnDesktopWeb ? GetCountryArea(size) : Screen.safeArea, size);
    }

    internal static Rect GetCountryArea(Vector2Int size)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        _isDesktopBrowser ??= CountryIsDesktopBrowser() != 0;
        if (_isDesktopBrowser.Value)
        {
            // iOS Notch Device (1170x2532), LandscapeLeft.
            float side = size.x * (141f / 2532f);
            float bottom = size.y * (63f / 1170f);
            return Rect.MinMaxRect(side, bottom, size.x - side, size.y);
        }
#endif
        return Screen.safeArea;
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    private static bool? _isDesktopBrowser;

    [System.Runtime.InteropServices.DllImport("__Internal")]
    private static extern int CountryIsDesktopBrowser();
#endif

    internal static void Track(RectTransform rect)
    {
        if (rect == null) return;
        var canvas = rect.GetComponentInParent<Canvas>(true);
        if (canvas == null) return;
        var safe = canvas.rootCanvas.GetComponent<CountrySafeArea>() ?? canvas.rootCanvas.gameObject.AddComponent<CountrySafeArea>();
        System.Array.Resize(ref safe._targets, safe._targets.Length + 1);
        safe._targets[^1] = new Target { Rect = rect, Min = rect.anchorMin, Max = rect.anchorMax, Horizontal = true, Vertical = true };
        safe._size = default;
    }

    public void Configure(Target[] targets, bool matchPhoneOnDesktopWeb = false)
    {
        _matchPhoneOnDesktopWeb = matchPhoneOnDesktopWeb;
        _targets = targets;
        _size = default;
    }

    internal static void FitBottomBand(RectTransform band, RectTransform safe, float height)
    {
        band.anchorMin = Vector2.zero;
        band.anchorMax = new Vector2(1, safe.anchorMin.y);
        band.offsetMin = Vector2.zero;
        band.offsetMax = new Vector2(0, safe.offsetMin.y + height);
    }

    public void Apply(Rect area, Vector2Int size)
    {
        if (_targets.Length == 0 || size.x <= 0 || size.y <= 0 || area.width <= 0 || area.height <= 0) return;
        if (_area == area && _size == size) return;
        var min = new Vector2(Mathf.Clamp01(area.xMin / size.x), Mathf.Clamp01(area.yMin / size.y));
        var max = new Vector2(Mathf.Clamp01(area.xMax / size.x), Mathf.Clamp01(area.yMax / size.y));
        _tracker.Clear();
        _area = area;
        _size = size;
        foreach (var target in _targets)
        {
            if (target.Rect == null) continue;
            _tracker.Add(this, target.Rect, DrivenTransformProperties.Anchors);
            target.Rect.anchorMin = MapAnchor(target.Min, target, min, max);
            target.Rect.anchorMax = MapAnchor(target.Max, target, min, max);
        }
    }

    private static Vector2 MapAnchor(Vector2 original, Target target, Vector2 min, Vector2 max)
    {
        var anchor = min + Vector2.Scale(original, max - min);
        if (!target.Horizontal) anchor.x = original.x;
        if (!target.Vertical) anchor.y = original.y;
        return anchor;
    }
}
