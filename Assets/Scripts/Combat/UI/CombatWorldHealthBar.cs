using System;
using UnityEngine;
using UnityEngine.UI;

public interface ICombatHealthSource
{
    int HP { get; }
    int MaxHP { get; }
    bool IsAlive { get; }
    event Action HealthChanged;
}

[DisallowMultipleComponent]
public sealed class CombatWorldHealthBar : MonoBehaviour
{
    [SerializeField] private Vector3 _localOffset = new Vector3(0f, 2.2f, 0f);
    [SerializeField, Min(0.1f)] private float _barWidth = 1f;
    [SerializeField, Min(0.02f)] private float _barHeight = 0.12f;
    [SerializeField] private Color _backgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.85f);
    [SerializeField] private Color _fillColor = new Color(0.2f, 0.85f, 0.25f, 1f);

    private ICombatHealthSource _source;
    private Transform _barRoot;
    private Image _fillImage;
    private Transform _cameraTransform;

    public void Configure(ICombatHealthSource source, Vector3? localOffset = null, float? barWidth = null)
    {
        if (_source != null)
        {
            _source.HealthChanged -= Refresh;
        }

        _source = source;
        if (localOffset.HasValue) _localOffset = localOffset.Value;
        if (barWidth.HasValue) _barWidth = barWidth.Value;

        EnsureBuilt();
        Refresh();

        if (_source != null)
        {
            _source.HealthChanged += Refresh;
        }
    }

    private void OnDestroy()
    {
        if (_source != null)
        {
            _source.HealthChanged -= Refresh;
        }
    }

    private void LateUpdate()
    {
        if (_barRoot == null) return;

        _barRoot.position = transform.position + _localOffset;

        if (_cameraTransform == null && Camera.main != null)
        {
            _cameraTransform = Camera.main.transform;
        }

        if (_cameraTransform != null)
        {
            _barRoot.rotation = Quaternion.LookRotation(_barRoot.position - _cameraTransform.position);
        }
    }

    private void EnsureBuilt()
    {
        if (_barRoot != null) return;

        var root = new GameObject("HealthBar", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        root.transform.SetParent(transform, worldPositionStays: true);
        _barRoot = root.transform;

        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        RectTransform canvasRect = root.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(_barWidth * 100f, _barHeight * 100f);
        canvasRect.localScale = Vector3.one * 0.01f;

        _fillImage = CreateBarImage(root.transform, "Fill", _fillColor, Image.Type.Filled, anchorMin: new Vector2(0f, 0f), anchorMax: Vector2.one);
        CreateBarImage(root.transform, "Background", _backgroundColor, Image.Type.Simple, anchorMin: Vector2.zero, anchorMax: Vector2.one, siblingIndex: 0);
    }

    private static Image CreateBarImage(
        Transform parent,
        string name,
        Color color,
        Image.Type imageType,
        Vector2 anchorMin,
        Vector2 anchorMax,
        int siblingIndex = -1)
    {
        var imageObject = new GameObject(name, typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(parent, false);

        if (siblingIndex >= 0)
        {
            imageObject.transform.SetSiblingIndex(siblingIndex);
        }

        RectTransform rect = imageObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image image = imageObject.GetComponent<Image>();
        image.sprite = GetWhiteSprite();
        image.type = imageType;
        if (imageType == Image.Type.Filled)
        {
            image.fillMethod = Image.FillMethod.Horizontal;
            image.fillOrigin = (int)Image.OriginHorizontal.Left;
        }
        image.color = color;
        return image;
    }

    public void SetVisible(bool visible)
    {
        if (_barRoot != null)
        {
            _barRoot.gameObject.SetActive(visible);
        }
    }

    private void Refresh()
    {
        if (_fillImage == null || _source == null) return;

        int maxHp = Mathf.Max(1, _source.MaxHP);
        float ratio = Mathf.Clamp01(_source.HP / (float)maxHp);
        _fillImage.fillAmount = ratio;

        if (_barRoot != null)
        {
            _barRoot.gameObject.SetActive(_source.IsAlive);
        }
    }

    private static Sprite s_whiteSprite;

    internal static Sprite GetWhiteSprite()
    {
        if (s_whiteSprite != null) return s_whiteSprite;

        var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        s_whiteSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 100f);
        return s_whiteSprite;
    }
}
