using TMPro;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public sealed class CombatPartyMemberView : MonoBehaviour
{
    private static readonly Color FocusBackgroundColor = new(1f, 0.85f, 0.1f, 1f);

    [SerializeField, Min(0.1f)] private float _skillDisplaySeconds = 2.2f;
    [SerializeField] private CombatCharacterAppearanceView _appearanceView;
    [SerializeField] private TextMeshProUGUI _nameText;
    [SerializeField] private TextMeshProUGUI _objectiveText;
    [SerializeField] private TextMeshProUGUI _buffDebuffText;
    [SerializeField] private TextMeshProUGUI _personalityText;
    [SerializeField] private TextMeshProUGUI _hpText;
    [SerializeField] private TextMeshProUGUI _skillText;
    [SerializeField] private GameObject _skillBackground;
    [SerializeField] private Image _hpFillImage;

    private Transform _weaponIconRoot;
    private Transform _buffDebuffIconRoot;
    private readonly GameObject[] _weaponIcons = new GameObject[7];
    private readonly List<CombatStatusIconKind> _displayedEffects = new();
    private CombatStatusIconRow _statusIconRow;
    private Character _character;
    private CombatHealth _health;
    private CombatAiBrain _aiBrain;
    private float _skillHideAtTime = float.NegativeInfinity;
    private bool _showingCastSkill;
    private Image _backgroundImage;
    private Color _idleBackgroundColor;
    private bool _hasIdleBackgroundColor;
    private Button _focusButton;

    public Character BoundCharacter => _character;
    public string CurrentNameText => _nameText != null ? _nameText.text : string.Empty;
    public string CurrentObjectiveText => _objectiveText != null ? _objectiveText.text : string.Empty;
    public string CurrentBuffDebuffText => _buffDebuffText != null ? _buffDebuffText.text : string.Empty;
    public string CurrentSkillText => _skillText != null ? _skillText.text : string.Empty;
    public string CurrentPersonalityText => _personalityText != null ? _personalityText.text : string.Empty;
    public float CurrentHpRatio => _hpFillImage != null ? _hpFillImage.fillAmount : 0f;
    public int ActiveStatusIconCount => _statusIconRow != null ? _statusIconRow.ActiveCount : 0;

    private void Awake()
    {
        ResolveReferences();
        EnsureFocusClickable();
    }

    private void OnEnable()
    {
        CombatPartyFocus.Changed += ApplyFocusVisual;
        ApplyFocusVisual();
    }

    private void OnDisable()
    {
        CombatPartyFocus.Changed -= ApplyFocusVisual;
    }

    private void OnDestroy()
    {
        CombatPartyFocus.Changed -= ApplyFocusVisual;
        UnbindHealth();
    }

    public void Bind(Character character, CombatCharacterAppearanceView.Facing facing)
    {
        ResolveReferences();
        EnsureFocusClickable();
        UnbindHealth();

        _character = character;
        _health = character != null ? character.Health : null;
        _aiBrain = character != null ? character.GetComponent<CombatAiBrain>() : null;
        if (_health != null)
        {
            _health.HealthChanged += RefreshHealth;
        }

        if (_appearanceView != null)
        {
            _appearanceView.Bind(character, facing);
        }
        RefreshName();
        RefreshObjective();
        RefreshBuffDebuff();
        RefreshPersonality();
        RefreshHealth();
        RefreshWeaponIcon();
        ClearSkill();
        ApplyFocusVisual();
    }

    public void ShowSkill(string skillName, float currentTime)
    {
        ResolveReferences();
        if (_skillText == null || string.IsNullOrWhiteSpace(skillName))
        {
            return;
        }

        _skillText.text = skillName;
        _skillText.gameObject.SetActive(true);
        if (_skillBackground != null)
        {
            _skillBackground.SetActive(true);
        }
        _skillHideAtTime = currentTime + Mathf.Max(0.1f, _skillDisplaySeconds);
        _showingCastSkill = false;
    }

    public void Tick(float currentTime)
    {
        RefreshObjective();
        RefreshBuffDebuff();
        RefreshPersonality();
        RefreshWeaponIcon();

        if (RefreshCastingSkill())
        {
            return;
        }

        if (_skillText == null || !_skillText.gameObject.activeSelf)
        {
            return;
        }

        if (currentTime >= _skillHideAtTime)
        {
            ClearSkill();
        }
    }

    private bool RefreshCastingSkill()
    {
        CombatSkillCaster caster = _character != null ? _character.SkillCaster : null;
        SkillBase skill = caster != null && caster.IsCasting ? caster.CastingSkill : null;
        if (skill != null && !string.IsNullOrWhiteSpace(skill.Name))
        {
            ResolveReferences();
            if (_skillText == null) return true;

            _skillText.text = $"{skill.Name}詠唱中";
            _skillText.gameObject.SetActive(true);
            if (_skillBackground != null)
            {
                _skillBackground.SetActive(true);
            }
            _skillHideAtTime = float.PositiveInfinity;
            _showingCastSkill = true;
            return true;
        }

        if (_showingCastSkill)
        {
            ClearSkill();
        }

        return false;
    }

    public void RefreshPersonality()
    {
        if (_personalityText == null)
        {
            return;
        }

        _personalityText.text = _character != null && _character.PersonalityProfile != null
            ? _character.PersonalityProfile.DisplayNameJapanese
            : string.Empty;
    }

    public void RefreshName()
    {
        if (_nameText == null)
        {
            return;
        }

        _nameText.text = _character != null ? _character.DisplayName : string.Empty;
    }

    public void RefreshObjective()
    {
        if (_objectiveText == null)
        {
            return;
        }

        CombatObjective objective = _aiBrain != null ? _aiBrain.LastPlan.Objective : CombatObjective.Search;
        _objectiveText.text = CombatAiDebugLabels.ObjectiveShort(objective);
    }

    public void RefreshBuffDebuff()
    {
        _buffDebuffIconRoot ??= transform.Find("BuffDebuffRoot");
        if (_buffDebuffIconRoot is RectTransform root)
        {
            _statusIconRow ??= new CombatStatusIconRow(root, root.rect.height, 3f, TextAnchor.MiddleRight);
            _statusIconRow.Refresh(_character);
        }
        if (_buffDebuffText != null)
        {
            CombatStatusIconSource.Collect(_character, _displayedEffects);
            _buffDebuffText.text = string.Join(" ", _displayedEffects.ConvertAll(CombatStatusIconSource.GetLabel));
        }
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

    public void RefreshWeaponIcon()
    {
        ResolveWeaponIcons();
        if (_weaponIconRoot == null)
        {
            return;
        }

        WeaponKind kind = _character != null && _character.EquippedWeapon != null
            ? _character.EquippedWeapon.Kind
            : WeaponKind.Unarmed;
        string iconName = GetWeaponIconName(kind);
        bool hasIcon = !string.IsNullOrEmpty(iconName);
        _weaponIconRoot.gameObject.SetActive(hasIcon);
        for (int i = 1; i < _weaponIcons.Length; i++)
        {
            if (_weaponIcons[i] != null)
            {
                _weaponIcons[i].SetActive(hasIcon && i == (int)kind);
            }
        }
    }

    private void EnsureFocusClickable()
    {
        ResolveBackgroundImage();
        if (_backgroundImage == null)
        {
            return;
        }

        _backgroundImage.raycastTarget = true;
        if (!_hasIdleBackgroundColor)
        {
            _idleBackgroundColor = _backgroundImage.color;
            _hasIdleBackgroundColor = true;
        }

        if (_focusButton == null)
        {
            _focusButton = GetComponent<Button>();
            if (_focusButton == null)
            {
                _focusButton = gameObject.AddComponent<Button>();
            }

            _focusButton.transition = Selectable.Transition.None;
            _focusButton.targetGraphic = _backgroundImage;
            _focusButton.onClick.RemoveListener(OnFocusClicked);
            _focusButton.onClick.AddListener(OnFocusClicked);
        }
    }

    private void OnFocusClicked()
    {
        if (_character == null)
        {
            return;
        }

        CombatPartyFocus.Toggle(_character);
        ApplyFocusVisual();
        CombatCharacterFocusMarker.EnsureFor(_character);
    }

    private void ApplyFocusVisual()
    {
        ResolveBackgroundImage();
        if (_backgroundImage == null || !_hasIdleBackgroundColor)
        {
            return;
        }

        bool focused = _character != null && _character == CombatPartyFocus.Selected;
        _backgroundImage.color = focused
            ? new Color(FocusBackgroundColor.r, FocusBackgroundColor.g, FocusBackgroundColor.b, _idleBackgroundColor.a)
            : _idleBackgroundColor;
    }

    private void ResolveBackgroundImage()
    {
        if (_backgroundImage != null)
        {
            return;
        }

        Transform background = transform.Find("Background");
        if (background != null)
        {
            _backgroundImage = background.GetComponent<Image>();
        }
    }

    private void ResolveReferences()
    {
        ResolveBackgroundImage();

        if (_appearanceView == null)
        {
            Transform appearance = transform.Find("Appearance");
            if (appearance != null)
            {
                _appearanceView = appearance.GetComponent<CombatCharacterAppearanceView>();
            }
        }

        if (_personalityText == null)
        {
            Transform personalityText = transform.Find("PersonalityText");
            if (personalityText != null)
            {
                _personalityText = personalityText.GetComponent<TextMeshProUGUI>();
            }
        }

        if (_objectiveText == null)
        {
            Transform objectiveText = transform.Find("ObjectiveText");
            if (objectiveText != null)
            {
                _objectiveText = objectiveText.GetComponent<TextMeshProUGUI>();
            }
        }

        if (_buffDebuffText == null)
        {
            Transform buffDebuffText = transform.Find("BuffDebuffText");
            if (buffDebuffText != null)
            {
                _buffDebuffText = buffDebuffText.GetComponent<TextMeshProUGUI>();
            }
        }

        if (_nameText == null)
        {
            Transform nameText = transform.Find("NameText");
            if (nameText != null)
            {
                _nameText = nameText.GetComponent<TextMeshProUGUI>();
            }
        }

        if (_hpText == null)
        {
            Transform hpText = transform.Find("HpText");
            if (hpText != null)
            {
                _hpText = hpText.GetComponent<TextMeshProUGUI>();
            }
        }

        if (_skillBackground == null)
        {
            Transform skillBackground = transform.Find("SkillBackground");
            skillBackground ??= transform.Find("Skill");
            if (skillBackground != null)
            {
                _skillBackground = skillBackground.gameObject;
            }
        }

        if (_skillText == null)
        {
            Transform skillText = _skillBackground != null
                ? _skillBackground.transform.Find("SkillText")
                : transform.Find("SkillText");
            skillText ??= _skillBackground != null
                ? _skillBackground.transform.Find("Text")
                : null;
            if (skillText != null)
            {
                _skillText = skillText.GetComponent<TextMeshProUGUI>();
            }
        }

        if (_weaponIconRoot == null)
        {
            _weaponIconRoot = transform.Find("WeaponIconRoot");
        }

        _buffDebuffIconRoot ??= transform.Find("BuffDebuffRoot");

        if (_hpFillImage == null)
        {
            Transform hpFill = transform.Find("HpBarBackground/HpBarFill");
            if (hpFill != null)
            {
                _hpFillImage = hpFill.GetComponent<Image>();
            }
        }
    }

    private void UnbindHealth()
    {
        if (_health != null)
        {
            _health.HealthChanged -= RefreshHealth;
        }

        _health = null;
        _aiBrain = null;
    }

    private void ClearSkill()
    {
        if (_skillText == null)
        {
            return;
        }

        _skillText.text = string.Empty;
        _skillText.gameObject.SetActive(false);
        if (_skillBackground != null)
        {
            _skillBackground.SetActive(false);
        }
        _skillHideAtTime = float.NegativeInfinity;
        _showingCastSkill = false;
    }

    private void ResolveWeaponIcons()
    {
        if (_weaponIconRoot == null)
        {
            _weaponIconRoot = transform.Find("WeaponIconRoot");
        }

        if (_weaponIconRoot == null || _weaponIcons[(int)WeaponKind.Sword] != null)
        {
            return;
        }

        for (int i = (int)WeaponKind.Sword; i <= (int)WeaponKind.Rosary; i++)
        {
            string iconName = GetWeaponIconName((WeaponKind)i);
            Transform icon = FindDescendant(_weaponIconRoot, iconName);
            _weaponIcons[i] = icon != null ? icon.gameObject : null;
        }
    }

    private static Transform FindDescendant(Transform root, string targetName)
    {
        if (root == null || string.IsNullOrEmpty(targetName))
        {
            return null;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child.name == targetName)
            {
                return child;
            }

            Transform nested = FindDescendant(child, targetName);
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }

    private static string GetWeaponIconName(WeaponKind kind)
    {
        return kind switch
        {
            WeaponKind.Sword => "SwordIcon",
            WeaponKind.Shield => "ShieldIcon",
            WeaponKind.Wand => "WandIcon",
            WeaponKind.Grimoire => "GrimoireIcon",
            WeaponKind.Bible => "BibleIcon",
            WeaponKind.Rosary => "RosaryIcon",
            _ => string.Empty,
        };
    }

}
