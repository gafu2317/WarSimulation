using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class CombatAiWorldLabel : MonoBehaviour
{
    [SerializeField] private Vector3 _localOffset = new Vector3(0f, 2.8f, 0f);
    [SerializeField, Min(0.4f)] private float _width = 2.4f;
    [SerializeField, Min(0.12f)] private float _height = 0.42f;
    [SerializeField] private Color _backgroundColor = new Color(0.05f, 0.07f, 0.1f, 0.82f);
    [SerializeField] private Color _defaultTextColor = new Color(1f, 0.95f, 0.85f, 1f);
    [SerializeField] private Color _attackColor = new Color(1f, 0.45f, 0.35f, 1f);
    [SerializeField] private Color _supportColor = new Color(0.45f, 1f, 0.55f, 1f);
    [SerializeField] private Color _searchColor = new Color(0.55f, 0.9f, 1f, 1f);
    [SerializeField] private Color _defendColor = new Color(0.65f, 0.8f, 1f, 1f);
    [SerializeField] private Color _retreatColor = new Color(1f, 0.7f, 0.35f, 1f);
    [SerializeField] private Color _stoneColor = new Color(1f, 0.55f, 0.95f, 1f);

    private Transform _labelRoot;
    private Text _labelText;
    private Image _backgroundImage;
    private Transform _cameraTransform;

    public string CurrentText => _labelText != null ? _labelText.text : string.Empty;

    public void SetObjective(CombatObjective objective, bool isActive = true)
    {
        EnsureBuilt();
        if (_labelText == null || _backgroundImage == null) return;

        _labelText.text = CombatAiDebugLabels.ObjectiveShort(objective);
        _labelText.color = ResolveTextColor(objective);
        _backgroundImage.color = isActive ? _backgroundColor : new Color(_backgroundColor.r, _backgroundColor.g, _backgroundColor.b, 0.35f);
        SetVisible(isActive);
    }

    public void SetVisible(bool visible)
    {
        if (_labelRoot != null)
        {
            _labelRoot.gameObject.SetActive(visible);
        }
    }

    private void LateUpdate()
    {
        if (_labelRoot == null) return;

        _labelRoot.position = transform.position + _localOffset;

        if (_cameraTransform == null && Camera.main != null)
        {
            _cameraTransform = Camera.main.transform;
        }

        if (_cameraTransform != null)
        {
            _labelRoot.rotation = Quaternion.LookRotation(_labelRoot.position - _cameraTransform.position);
        }
    }

    private void EnsureBuilt()
    {
        if (_labelRoot != null) return;

        var root = new GameObject("AiObjectiveLabel", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        root.transform.SetParent(transform, worldPositionStays: true);
        _labelRoot = root.transform;

        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        RectTransform canvasRect = root.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(_width * 100f, _height * 100f);
        canvasRect.localScale = Vector3.one * 0.012f;

        _backgroundImage = CreateBackground(root.transform);
        _labelText = CreateLabel(root.transform);
    }

    private Image CreateBackground(Transform parent)
    {
        var background = new GameObject("Background", typeof(RectTransform), typeof(Image));
        background.transform.SetParent(parent, false);

        RectTransform rect = background.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image image = background.GetComponent<Image>();
        image.sprite = GetWhiteSprite();
        image.type = Image.Type.Simple;
        image.color = _backgroundColor;
        return image;
    }

    private static Text CreateLabel(Transform parent)
    {
        var label = new GameObject("Label", typeof(RectTransform), typeof(Text));
        label.transform.SetParent(parent, false);

        RectTransform rect = label.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(10f, 4f);
        rect.offsetMax = new Vector2(-10f, -4f);

        Text text = label.GetComponent<Text>();
        text.alignment = TextAnchor.MiddleCenter;
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = 16;
        text.resizeTextMaxSize = 32;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = CombatAiDebugLabels.ObjectiveShort(CombatObjective.Search);
        return text;
    }

    private Color ResolveTextColor(CombatObjective objective)
    {
        return objective switch
        {
            CombatObjective.DestroyEnemyStone => _stoneColor,
            CombatObjective.DefendOwnStone => _defendColor,
            CombatObjective.AttackEnemy => _attackColor,
            CombatObjective.SupportAlly => _supportColor,
            CombatObjective.Search => _searchColor,
            CombatObjective.Retreat => _retreatColor,
            _ => _defaultTextColor,
        };
    }

    private static Sprite s_whiteSprite;

    private static Sprite GetWhiteSprite()
    {
        if (s_whiteSprite != null) return s_whiteSprite;

        var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        s_whiteSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 100f);
        return s_whiteSprite;
    }
}
