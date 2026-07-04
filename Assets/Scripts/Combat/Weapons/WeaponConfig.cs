using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(
    fileName = "WeaponConfig",
    menuName = "WarSimulation/Combat/Weapon Config")]
public sealed class WeaponConfig : ScriptableObject
{
    [SerializeField] private WeaponKind _kind = WeaponKind.Sword;

    [Header("Combat")]
    [SerializeField, Min(0.1f)] private float _range = 2f;
    [SerializeField, Min(0.01f)] private float _cooldownSeconds = 1f;
    [FormerlySerializedAs("_basePower")]
    [SerializeField, Min(0)] private int _primaryStatBonus = 12;

    [Header("AI Bias")]
    [SerializeField] private float _chaseEnemyBias;
    [SerializeField] private float _hideInForestBias;
    [SerializeField] private float _seekHighGroundBias;
    [SerializeField] private float _followMeleeAllyBias;

    [Header("Granted Skills")]
    [SerializeField] private SkillId[] _grantedSkillIds = System.Array.Empty<SkillId>();

    public WeaponKind Kind => _kind;
    public float Range => _range;
    public float CooldownSeconds => _cooldownSeconds;
    public int PrimaryStatBonus => _primaryStatBonus;
    public float ChaseEnemyBias => _chaseEnemyBias;
    public float HideInForestBias => _hideInForestBias;
    public float SeekHighGroundBias => _seekHighGroundBias;
    public float FollowMeleeAllyBias => _followMeleeAllyBias;
    public IReadOnlyList<SkillId> GrantedSkillIds => _grantedSkillIds;

    public WeaponBase CreateWeapon()
    {
        IReadOnlyList<SkillId> grantedSkillIds = _grantedSkillIds;
        return _kind switch
        {
            WeaponKind.Sword => new Sword(
                _range,
                _cooldownSeconds,
                _primaryStatBonus,
                _chaseEnemyBias,
                _hideInForestBias,
                _seekHighGroundBias,
                _followMeleeAllyBias,
                grantedSkillIds),
            WeaponKind.Shield => new Shield(
                _range,
                _cooldownSeconds,
                _primaryStatBonus,
                _chaseEnemyBias,
                _hideInForestBias,
                _seekHighGroundBias,
                _followMeleeAllyBias,
                grantedSkillIds),
            WeaponKind.Wand => new Wand(
                _range,
                _cooldownSeconds,
                _primaryStatBonus,
                _chaseEnemyBias,
                _hideInForestBias,
                _seekHighGroundBias,
                _followMeleeAllyBias,
                grantedSkillIds),
            WeaponKind.Grimoire => new Grimoire(
                _range,
                _cooldownSeconds,
                _primaryStatBonus,
                _chaseEnemyBias,
                _hideInForestBias,
                _seekHighGroundBias,
                _followMeleeAllyBias,
                grantedSkillIds),
            WeaponKind.Bible => new Bible(
                _range,
                _cooldownSeconds,
                _primaryStatBonus,
                _chaseEnemyBias,
                _hideInForestBias,
                _seekHighGroundBias,
                _followMeleeAllyBias,
                grantedSkillIds),
            WeaponKind.Rosary => new Rosary(
                _range,
                _cooldownSeconds,
                _primaryStatBonus,
                _chaseEnemyBias,
                _hideInForestBias,
                _seekHighGroundBias,
                _followMeleeAllyBias,
                grantedSkillIds),
            WeaponKind.Unarmed => WeaponBase.Unarmed,
            _ => WeaponBase.Unarmed,
        };
    }

    private void OnValidate()
    {
        if (_range < 0.1f) _range = 0.1f;
        if (_cooldownSeconds < 0.01f) _cooldownSeconds = 0.01f;
        if (_primaryStatBonus < 0) _primaryStatBonus = 0;
    }

    private void Reset()
    {
        ApplyKindDefaults(_kind);
    }

    public void ApplyKindDefaults(WeaponKind kind)
    {
        _kind = kind;
        switch (kind)
        {
            case WeaponKind.Sword:
                _range = 2f;
                _cooldownSeconds = 1f;
                _primaryStatBonus = 12;
                _chaseEnemyBias = 20f;
                _hideInForestBias = 0f;
                _seekHighGroundBias = 0f;
                _followMeleeAllyBias = 0f;
                break;
            case WeaponKind.Shield:
                _range = 1.8f;
                _cooldownSeconds = 1.3f;
                _primaryStatBonus = 6;
                _chaseEnemyBias = 0f;
                _hideInForestBias = 0f;
                _seekHighGroundBias = 0f;
                _followMeleeAllyBias = 40f;
                break;
            case WeaponKind.Wand:
                _range = 8f;
                _cooldownSeconds = 1.4f;
                _primaryStatBonus = 10;
                _chaseEnemyBias = 0f;
                _hideInForestBias = 70f;
                _seekHighGroundBias = 0f;
                _followMeleeAllyBias = 0f;
                break;
            case WeaponKind.Grimoire:
                _range = 7f;
                _cooldownSeconds = 2f;
                _primaryStatBonus = 14;
                _chaseEnemyBias = 0f;
                _hideInForestBias = 70f;
                _seekHighGroundBias = 50f;
                _followMeleeAllyBias = 0f;
                break;
            case WeaponKind.Bible:
                _range = 6f;
                _cooldownSeconds = 1.6f;
                _primaryStatBonus = 10;
                _chaseEnemyBias = 0f;
                _hideInForestBias = 65f;
                _seekHighGroundBias = 30f;
                _followMeleeAllyBias = 0f;
                break;
            case WeaponKind.Rosary:
                _range = 5f;
                _cooldownSeconds = 1.2f;
                _primaryStatBonus = 8;
                _chaseEnemyBias = 0f;
                _hideInForestBias = 60f;
                _seekHighGroundBias = 50f;
                _followMeleeAllyBias = 0f;
                break;
            default:
                _range = 1.5f;
                _cooldownSeconds = 1.2f;
                _primaryStatBonus = 4;
                _chaseEnemyBias = 0f;
                _hideInForestBias = 0f;
                _seekHighGroundBias = 0f;
                _followMeleeAllyBias = 0f;
                break;
        }
    }
}
