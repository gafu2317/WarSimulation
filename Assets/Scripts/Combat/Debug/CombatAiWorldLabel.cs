using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class CombatAiWorldLabel : MonoBehaviour
{
    private const string OverlayCanvasName = "CombatAiDebugOverlayCanvas";

    [SerializeField] private Vector3 _localOffset = new Vector3(0f, 2.8f, 0f);
    [SerializeField, Min(0.4f)] private float _width = 2.4f;
    [SerializeField, Min(0.12f)] private float _height = 0.42f;
    [SerializeField, Min(0.08f)] private float _weaponHeight = 0.26f;
    [SerializeField, Min(0.08f)] private float _personalityHeight = 0.26f;
    [SerializeField, Min(0.08f)] private float _skillHeight = 0.3f;
    [SerializeField, Min(0f)] private float _stackSpacing = 0.08f;
    [SerializeField, Min(0.01f)] private float _defaultSkillDurationSeconds = 1.2f;
    [SerializeField] private Color _backgroundColor = new Color(0.05f, 0.07f, 0.1f, 0.82f);
    [SerializeField] private Color _weaponBackgroundColor = new Color(0.05f, 0.07f, 0.1f, 0.76f);
    [SerializeField] private Color _personalityBackgroundColor = new Color(0.05f, 0.07f, 0.1f, 0.76f);
    [SerializeField] private Color _personalityHighlightBackgroundColor = new Color(0.85f, 0.65f, 0.12f, 0.9f);
    [SerializeField] private Color _skillBackgroundColor = new Color(0.05f, 0.07f, 0.1f, 0.72f);
    [SerializeField] private Color _defaultTextColor = new Color(1f, 0.95f, 0.85f, 1f);
    [SerializeField] private Color _allyTextColor = new Color(0.3f, 0.7f, 1f, 1f);
    [SerializeField] private Color _enemyTextColor = new Color(1f, 0.3f, 0.25f, 1f);
    [SerializeField] private Color _attackColor = new Color(1f, 0.45f, 0.35f, 1f);
    [SerializeField] private Color _supportColor = new Color(0.45f, 1f, 0.55f, 1f);
    [SerializeField] private Color _searchColor = new Color(0.55f, 0.9f, 1f, 1f);
    [SerializeField] private Color _defendColor = new Color(0.65f, 0.8f, 1f, 1f);
    [SerializeField] private Color _retreatColor = new Color(1f, 0.7f, 0.35f, 1f);
    [SerializeField] private Color _stoneColor = new Color(1f, 0.55f, 0.95f, 1f);

    private static Canvas s_overlayCanvas;
    private static RectTransform s_overlayCanvasRect;
    private Transform _labelRoot;
    private Transform _objectiveRoot;
    private Text _labelText;
    private Image _backgroundImage;
    private Transform _weaponRoot;
    private Text _weaponText;
    private Image _weaponBackgroundImage;
    private Transform _personalityRoot;
    private Text _personalityText;
    private Image _personalityBackgroundImage;
    private Transform _skillRoot;
    private Text _skillText;
    private Image _skillBackgroundImage;
    private Transform _cameraTransform;
    private Character _character;
    private float _skillExpiresAt = float.NegativeInfinity;
    private bool _requestedVisible = true;
    private bool _isInFrontOfCamera = true;

    public string CurrentText => _labelText != null ? _labelText.text : string.Empty;
    public string CurrentWeaponText => _weaponText != null ? _weaponText.text : string.Empty;
    public string CurrentPersonalityText => _personalityText != null ? _personalityText.text : string.Empty;
    public string CurrentSkillText => _skillText != null ? _skillText.text : string.Empty;

    public void SetObjective(CombatObjective objective, bool isActive = true)
    {
        EnsureBuilt();
        if (_labelText == null || _backgroundImage == null) return;

        _labelText.text = CombatAiDebugLabels.ObjectiveShort(objective);
        _labelText.color = ResolveTextColor(objective);
        _backgroundImage.color = isActive ? _backgroundColor : new Color(_backgroundColor.r, _backgroundColor.g, _backgroundColor.b, 0.35f);
        SetVisible(isActive);
    }

    public void SetWeapon(WeaponBase weapon)
    {
        EnsureBuilt();
        if (_weaponText == null || _weaponBackgroundImage == null || _weaponRoot == null) return;

        if (weapon == null)
        {
            _character ??= GetComponent<Character>();
            weapon = _character != null ? _character.EquippedWeapon : null;
        }

        _weaponText.text = CombatAiDebugLabels.WeaponShort(weapon);
        _weaponText.color = ResolveLabelTextColor();
        _weaponBackgroundImage.color = _weaponBackgroundColor;
        _weaponRoot.gameObject.SetActive(true);
    }

    public void SetPersonality(CombatAiPersonalityProfile profile, bool highlighted)
    {
        EnsureBuilt();
        if (_personalityText == null || _personalityBackgroundImage == null || _personalityRoot == null) return;

        _personalityText.text = CombatAiDebugLabels.PersonalityShort(profile);
        _personalityText.color = ResolveLabelTextColor();
        _personalityBackgroundImage.color = highlighted
            ? _personalityHighlightBackgroundColor
            : _personalityBackgroundColor;
        _personalityRoot.gameObject.SetActive(true);
    }

    public void ShowSkill(string skillName, float durationSeconds = -1f)
    {
        EnsureBuilt();
        if (_skillText == null || _skillBackgroundImage == null || _skillRoot == null) return;
        if (string.IsNullOrWhiteSpace(skillName))
        {
            HideSkill();
            return;
        }

        _skillText.text = skillName;
        _skillText.color = ResolveLabelTextColor();
        _skillBackgroundImage.color = _skillBackgroundColor;
        _skillRoot.gameObject.SetActive(true);

        float lifetime = durationSeconds < 0f ? _defaultSkillDurationSeconds : durationSeconds;
        _skillExpiresAt = Time.time + Mathf.Max(0f, lifetime);
        RefreshTransientState(Time.time);
    }

    public void HideSkill()
    {
        _skillExpiresAt = float.NegativeInfinity;
        if (_skillText != null)
        {
            _skillText.text = string.Empty;
        }

        if (_skillRoot != null)
        {
            _skillRoot.gameObject.SetActive(false);
        }
    }

    public void SetVisible(bool visible)
    {
        _requestedVisible = visible;
        UpdateRootVisibleState();
    }

    private void LateUpdate()
    {
        EnsureBuilt();
        if (_labelRoot == null) return;

        RefreshTransientState(Time.time);
        Camera mainCamera = ResolveActiveCamera();
        if (_cameraTransform == null && mainCamera != null)
        {
            _cameraTransform = mainCamera.transform;
        }

        if (_cameraTransform == null || mainCamera == null)
        {
            _isInFrontOfCamera = false;
            UpdateRootVisibleState();
            return;
        }

        Vector3 worldPosition = transform.position + _localOffset;
        Vector3 toLabel = worldPosition - mainCamera.transform.position;
        _isInFrontOfCamera = Vector3.Dot(mainCamera.transform.forward, toLabel) > 0f;
        UpdateRootVisibleState();
        if (!_isInFrontOfCamera) return;

        Vector3 screenPoint = mainCamera.WorldToScreenPoint(worldPosition);
        RectTransform rootRect = (RectTransform)_labelRoot;
        if (s_overlayCanvasRect != null &&
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                s_overlayCanvasRect,
                screenPoint,
                null,
                out Vector2 localPoint))
        {
            rootRect.anchoredPosition = localPoint;
        }
    }

    private Camera ResolveActiveCamera()
    {
        Camera[] cameras = FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = cameras.Length - 1; i >= 0; i--)
        {
            Camera camera = cameras[i];
            if (camera != null && camera.isActiveAndEnabled && camera.CompareTag("MainCamera"))
            {
                _cameraTransform = camera.transform;
                return camera;
            }
        }

        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            _cameraTransform = mainCamera.transform;
        }

        return mainCamera;
    }

    public void RefreshTransientState(float currentTime)
    {
        if (_skillRoot == null) return;
        if (currentTime < _skillExpiresAt) return;
        HideSkill();
    }

    private void EnsureBuilt()
    {
        if (_labelRoot != null) return;
        _character ??= GetComponent<Character>();

        EnsureOverlayCanvas();

        var root = new GameObject("AiObjectiveLabel", typeof(RectTransform));
        root.transform.SetParent(s_overlayCanvasRect, worldPositionStays: false);
        _labelRoot = root.transform;

        RectTransform canvasRect = root.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(
            _width * 100f,
            (_height + _weaponHeight + _personalityHeight + _skillHeight + (_stackSpacing * 3f)) * 100f);
        canvasRect.anchorMin = new Vector2(0.5f, 0.5f);
        canvasRect.anchorMax = new Vector2(0.5f, 0.5f);
        canvasRect.pivot = new Vector2(0.5f, 0f);
        canvasRect.anchoredPosition = Vector2.zero;

        _objectiveRoot = CreateRowRoot(root.transform, "ObjectiveRoot", _height, 0f);
        _backgroundImage = CreateBackground(_objectiveRoot, "ObjectiveBackground");
        _labelText = CreateLabel(_objectiveRoot, "ObjectiveLabel", _height, 16, 32);
        _weaponRoot = CreateWeaponRoot(root.transform);
        _weaponBackgroundImage = CreateBackground(_weaponRoot, "WeaponBackground");
        _weaponText = CreateLabel(_weaponRoot, "WeaponLabel", _weaponHeight, 13, 22);
        _personalityRoot = CreatePersonalityRoot(root.transform);
        _personalityBackgroundImage = CreateBackground(_personalityRoot, "PersonalityBackground");
        _personalityText = CreateLabel(_personalityRoot, "PersonalityLabel", _personalityHeight, 13, 22);
        _skillRoot = CreateSkillRoot(root.transform);
        _skillBackgroundImage = CreateBackground(_skillRoot, "SkillBackground");
        _skillText = CreateLabel(_skillRoot, "SkillLabel", _skillHeight, 14, 24);
        SetWeapon(_character != null ? _character.EquippedWeapon : null);
        SetPersonality(_character != null ? _character.PersonalityProfile : null, highlighted: false);
        HideSkill();
        UpdateRootVisibleState();
    }

    private static void EnsureOverlayCanvas()
    {
        if (s_overlayCanvas != null && s_overlayCanvasRect != null) return;

        GameObject existing = GameObject.Find(OverlayCanvasName);
        if (existing != null)
        {
            s_overlayCanvas = existing.GetComponent<Canvas>();
            s_overlayCanvasRect = existing.GetComponent<RectTransform>();
            if (s_overlayCanvas != null && s_overlayCanvasRect != null)
            {
                s_overlayCanvas.sortingOrder = 0;
                return;
            }
        }

        var canvasObject = new GameObject(OverlayCanvasName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        s_overlayCanvas = canvasObject.GetComponent<Canvas>();
        s_overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        s_overlayCanvas.sortingOrder = 0;

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

    private Transform CreateRowRoot(Transform parent, string objectName, float height, float bottomOffset)
    {
        var rowRoot = new GameObject(objectName, typeof(RectTransform));
        rowRoot.transform.SetParent(parent, false);

        RectTransform rect = rowRoot.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(1f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.sizeDelta = new Vector2(0f, height * 100f);
        rect.anchoredPosition = new Vector2(0f, bottomOffset * 100f);
        return rowRoot.transform;
    }

    private Transform CreateSkillRoot(Transform parent)
    {
        return CreateRowRoot(
            parent,
            "SkillRoot",
            _skillHeight,
            _height + _weaponHeight + _personalityHeight + (_stackSpacing * 3f));
    }

    private Transform CreatePersonalityRoot(Transform parent)
    {
        return CreateRowRoot(
            parent,
            "PersonalityRoot",
            _personalityHeight,
            _height + _weaponHeight + (_stackSpacing * 2f));
    }

    private Transform CreateWeaponRoot(Transform parent)
    {
        return CreateRowRoot(parent, "WeaponRoot", _weaponHeight, _height + _stackSpacing);
    }

    private Image CreateBackground(Transform parent, string objectName)
    {
        var background = new GameObject(objectName, typeof(RectTransform), typeof(Image));
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

    private static Text CreateLabel(Transform parent, string objectName, float height, int minSize, int maxSize)
    {
        var label = new GameObject(objectName, typeof(RectTransform), typeof(Text));
        label.transform.SetParent(parent, false);

        RectTransform rect = label.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(10f, 4f);
        rect.offsetMax = new Vector2(-10f, height >= 0.35f ? -4f : -2f);

        Text text = label.GetComponent<Text>();
        text.alignment = TextAnchor.MiddleCenter;
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = minSize;
        text.resizeTextMaxSize = maxSize;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = CombatAiDebugLabels.ObjectiveShort(CombatObjective.Search);
        return text;
    }

    private Color ResolveTextColor(CombatObjective objective)
    {
        Color teamColor = ResolveLabelTextColor();
        if (_character != null)
        {
            return teamColor;
        }

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

    private Color ResolveLabelTextColor()
    {
        if (_character == null)
        {
            _character = GetComponent<Character>();
        }

        if (_character != null)
        {
            return _character.Team == CombatTeam.Ally ? _allyTextColor : _enemyTextColor;
        }

        return _defaultTextColor;
    }

    private void UpdateRootVisibleState()
    {
        if (_labelRoot == null) return;
        _labelRoot.gameObject.SetActive(_requestedVisible && _isInFrontOfCamera);
    }

    private void OnDestroy()
    {
        if (_labelRoot != null)
        {
            if (Application.isPlaying)
            {
                Destroy(_labelRoot.gameObject);
            }
            else
            {
                DestroyImmediate(_labelRoot.gameObject);
            }

            _labelRoot = null;
        }
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
