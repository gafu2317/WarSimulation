using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public sealed class CombatPartyMemberView : MonoBehaviour
{
    [SerializeField, Min(0.1f)] private float _skillDisplaySeconds = 2.2f;

    private Character _character;
    private CombatHealth _health;
    private RectTransform _rectTransform;
    private CombatCharacterAppearanceView _appearanceView;
    private Text _weaponText;
    private Text _hpText;
    private Text _skillText;
    private Image _hpFillImage;
    private float _skillHideAtTime = float.NegativeInfinity;

    public Character BoundCharacter => _character;
    public string CurrentSkillText => _skillText != null ? _skillText.text : string.Empty;
    public string CurrentWeaponText => _weaponText != null ? _weaponText.text : string.Empty;
    public float CurrentHpRatio => _hpFillImage != null ? _hpFillImage.fillAmount : 0f;

    private void Awake()
    {
        EnsureBuilt();
    }

    private void OnDestroy()
    {
        UnbindHealth();
    }

    public void Bind(Character character, CombatCharacterAppearanceView.Facing facing)
    {
        EnsureBuilt();
        UnbindHealth();

        _character = character;
        _health = character != null ? character.Health : null;
        if (_health != null)
        {
            _health.HealthChanged += RefreshHealth;
        }

        _appearanceView.Bind(character, facing);
        RefreshWeapon();
        RefreshHealth();
        ClearSkill();
    }

    public void ShowSkill(string skillName, float currentTime)
    {
        EnsureBuilt();
        if (_skillText == null || string.IsNullOrWhiteSpace(skillName))
        {
            return;
        }

        _skillText.text = skillName;
        _skillText.gameObject.SetActive(true);
        _skillHideAtTime = currentTime + Mathf.Max(0.1f, _skillDisplaySeconds);
    }

    public void Tick(float currentTime)
    {
        if (_skillText == null || !_skillText.gameObject.activeSelf)
        {
            return;
        }

        if (currentTime >= _skillHideAtTime)
        {
            ClearSkill();
        }
    }

    public void RefreshWeapon()
    {
        if (_weaponText == null)
        {
            return;
        }

        WeaponKind kind = _character != null && _character.EquippedWeapon != null
            ? _character.EquippedWeapon.Kind
            : WeaponKind.Unarmed;
        _weaponText.text = kind.ToString();
    }

    public void RefreshHealth()
    {
        if (_hpText == null || _hpFillImage == null)
        {
            return;
        }

        int hp = _health != null ? _health.HP : 0;
        int maxHp = _health != null ? Mathf.Max(1, _health.MaxHP) : 1;
        _hpText.text = $"HP {hp}/{maxHp}";
        _hpFillImage.fillAmount = Mathf.Clamp01(hp / (float)maxHp);
    }

    private void EnsureBuilt()
    {
        if (_rectTransform != null)
        {
            return;
        }

        _rectTransform = GetComponent<RectTransform>();
        _rectTransform.sizeDelta = new Vector2(280f, 132f);

        GameObject background = CreateImageObject(transform, "Background", new Color(0.06f, 0.08f, 0.12f, 0.86f));
        RectTransform backgroundRect = background.GetComponent<RectTransform>();
        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.one;
        backgroundRect.offsetMin = Vector2.zero;
        backgroundRect.offsetMax = Vector2.zero;

        GameObject appearanceObject = new GameObject("Appearance", typeof(RectTransform), typeof(CombatCharacterAppearanceView));
        appearanceObject.transform.SetParent(transform, false);
        RectTransform appearanceRect = appearanceObject.GetComponent<RectTransform>();
        appearanceRect.anchorMin = new Vector2(0f, 0.5f);
        appearanceRect.anchorMax = new Vector2(0f, 0.5f);
        appearanceRect.pivot = new Vector2(0f, 0.5f);
        appearanceRect.sizeDelta = new Vector2(108f, 108f);
        appearanceRect.anchoredPosition = new Vector2(12f, 0f);
        _appearanceView = appearanceObject.GetComponent<CombatCharacterAppearanceView>();

        _weaponText = CreateText(transform, "WeaponText", new Vector2(132f, -24f), new Vector2(136f, 28f), 16, TextAnchor.MiddleLeft);
        _hpText = CreateText(transform, "HpText", new Vector2(132f, -58f), new Vector2(136f, 24f), 14, TextAnchor.MiddleLeft);
        _skillText = CreateText(transform, "SkillText", new Vector2(132f, 28f), new Vector2(136f, 44f), 14, TextAnchor.MiddleLeft);
        _skillText.color = new Color(1f, 0.93f, 0.52f, 1f);
        _skillText.gameObject.SetActive(false);

        GameObject hpBackground = CreateImageObject(transform, "HpBarBackground", new Color(0.1f, 0.1f, 0.1f, 0.9f));
        RectTransform hpBackgroundRect = hpBackground.GetComponent<RectTransform>();
        hpBackgroundRect.anchorMin = new Vector2(0f, 0.5f);
        hpBackgroundRect.anchorMax = new Vector2(0f, 0.5f);
        hpBackgroundRect.pivot = new Vector2(0f, 0.5f);
        hpBackgroundRect.sizeDelta = new Vector2(132f, 14f);
        hpBackgroundRect.anchoredPosition = new Vector2(136f, -84f);

        GameObject hpFill = CreateImageObject(hpBackground.transform, "HpBarFill", new Color(0.28f, 0.84f, 0.34f, 1f));
        RectTransform hpFillRect = hpFill.GetComponent<RectTransform>();
        hpFillRect.anchorMin = Vector2.zero;
        hpFillRect.anchorMax = Vector2.one;
        hpFillRect.offsetMin = Vector2.zero;
        hpFillRect.offsetMax = Vector2.zero;

        _hpFillImage = hpFill.GetComponent<Image>();
        _hpFillImage.type = Image.Type.Filled;
        _hpFillImage.fillMethod = Image.FillMethod.Horizontal;
        _hpFillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
    }

    private void UnbindHealth()
    {
        if (_health != null)
        {
            _health.HealthChanged -= RefreshHealth;
        }

        _health = null;
    }

    private void ClearSkill()
    {
        if (_skillText == null)
        {
            return;
        }

        _skillText.text = string.Empty;
        _skillText.gameObject.SetActive(false);
        _skillHideAtTime = float.NegativeInfinity;
    }

    private static GameObject CreateImageObject(Transform parent, string objectName, Color color)
    {
        var imageObject = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(parent, false);
        Image image = imageObject.GetComponent<Image>();
        image.sprite = GetWhiteSprite();
        image.color = color;
        image.raycastTarget = false;
        return imageObject;
    }

    private static Text CreateText(
        Transform parent,
        string objectName,
        Vector2 anchoredPosition,
        Vector2 size,
        int fontSize,
        TextAnchor alignment)
    {
        var textObject = new GameObject(objectName, typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(parent, false);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        Text text = textObject.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = Color.white;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        return text;
    }

    private static Sprite s_whiteSprite;

    private static Sprite GetWhiteSprite()
    {
        if (s_whiteSprite != null)
        {
            return s_whiteSprite;
        }

        var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        s_whiteSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 100f);
        return s_whiteSprite;
    }
}
