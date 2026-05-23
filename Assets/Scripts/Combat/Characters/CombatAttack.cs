using UnityEngine;

[RequireComponent(typeof(Character))]
public sealed class CombatAttack : MonoBehaviour
{
    private Character _owner;
    private float _nextAttackTime;

    public bool IsCooldownReady => Time.time >= _nextAttackTime;
    public WeaponBase CurrentWeapon
    {
        get
        {
            _owner ??= GetComponent<Character>();
            return _owner != null && _owner.EquippedWeapon != null
                ? _owner.EquippedWeapon
                : WeaponBase.Unarmed;
        }
    }

    private void Awake()
    {
        _owner = GetComponent<Character>();
    }

    public bool CanAttack(Character target)
    {
        _owner ??= GetComponent<Character>();
        if (_owner == null || target == null || target == _owner) return false;
        if (!IsEnemyTarget(target)) return false;
        if (_owner.Health == null || !_owner.Health.CanAct) return false;
        if (target.Health == null || !target.Health.IsTargetable) return false;
        if (!IsCooldownReady) return false;

        return IsInRange(target);
    }

    public bool IsEnemyTarget(Character target)
    {
        _owner ??= GetComponent<Character>();
        return _owner != null && target != null && target != _owner && _owner.Team != target.Team;
    }

    public bool IsInRange(Character target)
    {
        if (target == null) return false;

        return Vector3.Distance(transform.position, target.transform.position) <= CurrentWeapon.Range;
    }

    public bool TryAttack(Character target)
    {
        if (!CanAttack(target)) return false;

        WeaponBase weapon = CurrentWeapon;
        int damage = CalculateDamage(weapon);
        target.Health.TakeDamage(damage, _owner);
        _nextAttackTime = Time.time + weapon.CooldownSeconds;
        return true;
    }

    public int CalculateDamage(WeaponBase weapon)
    {
        _owner ??= GetComponent<Character>();
        weapon ??= WeaponBase.Unarmed;
        int stat = GetStatValue(weapon.ScalingStat);
        return Mathf.Max(1, weapon.BasePower + Mathf.RoundToInt(stat * 0.5f));
    }

    private int GetStatValue(CombatStat stat)
    {
        return stat switch
        {
            CombatStat.INT => Mathf.RoundToInt(_owner.INT * _owner.INTBuff),
            CombatStat.FAI => Mathf.RoundToInt(_owner.FAI * _owner.FAIBuff),
            CombatStat.AGI => Mathf.RoundToInt(_owner.AGI * _owner.AGIBuff),
            _ => Mathf.RoundToInt(_owner.STR * _owner.STRBuff),
        };
    }
}
