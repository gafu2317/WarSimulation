using UnityEngine;

[CreateAssetMenu(
    fileName = "WeaponConfig",
    menuName = "WarSimulation/Combat/Weapon Config")]
public sealed class WeaponConfig : ScriptableObject
{
    [SerializeField] private WeaponKind _kind = WeaponKind.Sword;

    [Header("Combat")]
    [SerializeField, Min(0.1f)] private float _range = 2f;
    [SerializeField, Min(0.01f)] private float _cooldownSeconds = 1f;
    [SerializeField, Min(0f)] private float _basePower = 12f;

    [Header("AI Bias")]
    [SerializeField] private float _chaseEnemyBias;
    [SerializeField] private float _hideInForestBias;
    [SerializeField] private float _seekHighGroundBias;
    [SerializeField] private float _followMeleeAllyBias;

    public WeaponKind Kind => _kind;
    public float Range => _range;
    public float CooldownSeconds => _cooldownSeconds;
    public float BasePower => _basePower;
    public float ChaseEnemyBias => _chaseEnemyBias;
    public float HideInForestBias => _hideInForestBias;
    public float SeekHighGroundBias => _seekHighGroundBias;
    public float FollowMeleeAllyBias => _followMeleeAllyBias;

    public WeaponBase CreateWeapon()
    {
        return _kind switch
        {
            WeaponKind.Sword => new Sword(
                _range,
                _cooldownSeconds,
                _basePower,
                _chaseEnemyBias,
                _hideInForestBias,
                _seekHighGroundBias,
                _followMeleeAllyBias),
            WeaponKind.Shield => new Shield(
                _range,
                _cooldownSeconds,
                _basePower,
                _chaseEnemyBias,
                _hideInForestBias,
                _seekHighGroundBias,
                _followMeleeAllyBias),
            WeaponKind.Wand => new Wand(
                _range,
                _cooldownSeconds,
                _basePower,
                _chaseEnemyBias,
                _hideInForestBias,
                _seekHighGroundBias,
                _followMeleeAllyBias),
            WeaponKind.Grimoire => new Grimoire(
                _range,
                _cooldownSeconds,
                _basePower,
                _chaseEnemyBias,
                _hideInForestBias,
                _seekHighGroundBias,
                _followMeleeAllyBias),
            WeaponKind.Bible => new Bible(
                _range,
                _cooldownSeconds,
                _basePower,
                _chaseEnemyBias,
                _hideInForestBias,
                _seekHighGroundBias,
                _followMeleeAllyBias),
            WeaponKind.Rosary => new Rosary(
                _range,
                _cooldownSeconds,
                _basePower,
                _chaseEnemyBias,
                _hideInForestBias,
                _seekHighGroundBias,
                _followMeleeAllyBias),
            WeaponKind.Unarmed => WeaponBase.Unarmed,
            _ => WeaponBase.Unarmed,
        };
    }

    private void OnValidate()
    {
        if (_range < 0.1f) _range = 0.1f;
        if (_cooldownSeconds < 0.01f) _cooldownSeconds = 0.01f;
        if (_basePower < 0f) _basePower = 0f;
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
                _basePower = 12f;
                _chaseEnemyBias = 20f;
                _hideInForestBias = 0f;
                _seekHighGroundBias = 0f;
                _followMeleeAllyBias = 0f;
                break;
            case WeaponKind.Shield:
                _range = 1.8f;
                _cooldownSeconds = 1.3f;
                _basePower = 6f;
                _chaseEnemyBias = 0f;
                _hideInForestBias = 0f;
                _seekHighGroundBias = 0f;
                _followMeleeAllyBias = 40f;
                break;
            case WeaponKind.Wand:
                _range = 8f;
                _cooldownSeconds = 1.4f;
                _basePower = 10f;
                _chaseEnemyBias = 0f;
                _hideInForestBias = 70f;
                _seekHighGroundBias = 0f;
                _followMeleeAllyBias = 0f;
                break;
            case WeaponKind.Grimoire:
                _range = 7f;
                _cooldownSeconds = 2f;
                _basePower = 14f;
                _chaseEnemyBias = 0f;
                _hideInForestBias = 70f;
                _seekHighGroundBias = 50f;
                _followMeleeAllyBias = 0f;
                break;
            case WeaponKind.Bible:
                _range = 6f;
                _cooldownSeconds = 1.6f;
                _basePower = 10f;
                _chaseEnemyBias = 0f;
                _hideInForestBias = 65f;
                _seekHighGroundBias = 30f;
                _followMeleeAllyBias = 0f;
                break;
            case WeaponKind.Rosary:
                _range = 5f;
                _cooldownSeconds = 1.2f;
                _basePower = 8f;
                _chaseEnemyBias = 0f;
                _hideInForestBias = 60f;
                _seekHighGroundBias = 50f;
                _followMeleeAllyBias = 0f;
                break;
            default:
                _range = 1.5f;
                _cooldownSeconds = 1.2f;
                _basePower = 4f;
                _chaseEnemyBias = 0f;
                _hideInForestBias = 0f;
                _seekHighGroundBias = 0f;
                _followMeleeAllyBias = 0f;
                break;
        }
    }
}
