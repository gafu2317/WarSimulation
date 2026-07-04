using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public sealed class CombatPartyMemberView : MonoBehaviour
{
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
    private Character _character;
    private CombatHealth _health;
    private CombatAiBrain _aiBrain;
    private float _skillHideAtTime = float.NegativeInfinity;
    private bool _showingCastSkill;

    public Character BoundCharacter => _character;
    public string CurrentNameText => _nameText != null ? _nameText.text : string.Empty;
    public string CurrentObjectiveText => _objectiveText != null ? _objectiveText.text : string.Empty;
    public string CurrentBuffDebuffText => _buffDebuffText != null ? _buffDebuffText.text : string.Empty;
    public string CurrentSkillText => _skillText != null ? _skillText.text : string.Empty;
    public string CurrentPersonalityText => _personalityText != null ? _personalityText.text : string.Empty;
    public float CurrentHpRatio => _hpFillImage != null ? _hpFillImage.fillAmount : 0f;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnDestroy()
    {
        UnbindHealth();
    }

    public void Bind(Character character, CombatCharacterAppearanceView.Facing facing)
    {
        ResolveReferences();
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
        if (_buffDebuffText == null)
        {
            return;
        }

        if (_character == null || _character.StatusEffects == null)
        {
            _buffDebuffText.text = string.Empty;
            return;
        }

        var effects = _character.StatusEffects.GetActiveEffectSnapshots();
        if (effects == null || effects.Count == 0)
        {
            _buffDebuffText.text = string.Empty;
            return;
        }

        _buffDebuffText.text = FormatEffects(effects);
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
        for (int i = 0; i < _weaponIconRoot.childCount; i++)
        {
            Transform child = _weaponIconRoot.GetChild(i);
            child.gameObject.SetActive(hasIcon && child.name == iconName);
        }
    }

    private void ResolveReferences()
    {
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
            if (skillText != null)
            {
                _skillText = skillText.GetComponent<TextMeshProUGUI>();
            }
        }

        if (_weaponIconRoot == null)
        {
            _weaponIconRoot = transform.Find("WeaponIconRoot");
        }

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

    private static string FormatEffects(System.Collections.Generic.IReadOnlyList<CombatStatusEffectSnapshot> effects)
    {
        System.Text.StringBuilder builder = new();
        for (int i = 0; i < effects.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(' ');
            }

            builder.Append(FormatEffectLabel(effects[i]));
        }

        return builder.ToString();
    }

    private static string FormatEffectLabel(CombatStatusEffectSnapshot effect)
    {
        return effect.Type switch
        {
            CombatStatusEffects.EffectType.StatModifier => FormatStatModifierLabel(effect),
            CombatStatusEffects.EffectType.Invulnerable => "無敵",
            CombatStatusEffects.EffectType.Root => "移動不能",
            CombatStatusEffects.EffectType.Bind => "金縛り",
            CombatStatusEffects.EffectType.Poison => "毒",
            CombatStatusEffects.EffectType.HealOverTime => "継続回復",
            CombatStatusEffects.EffectType.Stealth => "不可視",
            _ => effect.Type.ToString(),
        };
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

    private static string FormatStatModifierLabel(CombatStatusEffectSnapshot effect)
    {
        string statName = effect.Stat switch
        {
            CombatStatusEffects.StatKind.STR => "STR",
            CombatStatusEffects.StatKind.INT => "INT",
            CombatStatusEffects.StatKind.FAI => "FAI",
            CombatStatusEffects.StatKind.AGI => "AGI",
            _ => effect.Stat.ToString(),
        };

        if (effect.IsBuff) return statName + "バフ";
        if (effect.IsDebuff) return statName + "デバフ";
        return statName + "補正";
    }

}
