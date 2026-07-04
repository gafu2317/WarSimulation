using System.Collections.Generic;
using UnityEngine;

public readonly struct CombatStatusEffectSnapshot
{
    public string Key { get; }
    public CombatStatusEffects.EffectType Type { get; }
    public CombatStatusEffects.StatKind Stat { get; }
    public float Multiplier { get; }
    public float Magnitude { get; }
    public float TickIntervalSeconds { get; }
    public float RemainingSeconds { get; }
    public bool IsBuff => Type == CombatStatusEffects.EffectType.StatModifier && Multiplier > 1f;
    public bool IsDebuff => Type == CombatStatusEffects.EffectType.StatModifier && Multiplier < 1f;

    public CombatStatusEffectSnapshot(
        string key,
        CombatStatusEffects.EffectType type,
        CombatStatusEffects.StatKind stat,
        float multiplier,
        float magnitude,
        float tickIntervalSeconds,
        float remainingSeconds)
    {
        Key = key;
        Type = type;
        Stat = stat;
        Multiplier = multiplier;
        Magnitude = magnitude;
        TickIntervalSeconds = tickIntervalSeconds;
        RemainingSeconds = remainingSeconds;
    }
}

[RequireComponent(typeof(Character))]
public sealed class CombatStatusEffects : MonoBehaviour
{
    public enum StatKind
    {
        STR,
        INT,
        FAI,
        AGI,
    }

    public enum EffectType
    {
        StatModifier,
        Invulnerable,
        Root,
        Bind,
        Poison,
        HealOverTime,
        Stealth,
    }

    private struct ActiveEffect
    {
        public string Key;
        public EffectType Type;
        public StatKind Stat;
        public float Multiplier;
        public float Magnitude;
        public float TickIntervalSeconds;
        public float NextTickAt;
        public float ExpiresAt;
    }

    private const float MinMultiplier = 0.1f;

    private readonly List<ActiveEffect> _effects = new();
    private readonly List<CombatStatusEffectSnapshot> _effectSnapshots = new();
    private bool _isUpdatingEffects;

    public float GetMultiplier(StatKind stat)
    {
        UpdateEffects();

        float totalDelta = 0f;
        for (int i = 0; i < _effects.Count; i++)
        {
            ActiveEffect effect = _effects[i];
            if (effect.Type != EffectType.StatModifier) continue;
            if (effect.Stat != stat) continue;

            totalDelta += effect.Multiplier - 1f;
        }

        return Mathf.Max(MinMultiplier, 1f + totalDelta);
    }

    public bool HasActiveEffect(string key)
    {
        if (string.IsNullOrEmpty(key)) return false;

        UpdateEffects();
        for (int i = 0; i < _effects.Count; i++)
        {
            if (_effects[i].Key == key) return true;
        }

        return false;
    }

    public bool HasActiveEffect(EffectType type)
    {
        UpdateEffects();
        return HasActiveEffectImmediate(type);
    }

    public bool HasActiveEffectImmediate(EffectType type)
    {
        for (int i = 0; i < _effects.Count; i++)
        {
            if (_effects[i].Type == type) return true;
        }

        return false;
    }

    public float GetRemainingSeconds(string key)
    {
        if (string.IsNullOrEmpty(key)) return 0f;

        UpdateEffects();
        float now = Time.time;
        for (int i = 0; i < _effects.Count; i++)
        {
            ActiveEffect effect = _effects[i];
            if (effect.Key != key) continue;

            return Mathf.Max(0f, effect.ExpiresAt - now);
        }

        return 0f;
    }

    public IReadOnlyList<CombatStatusEffectSnapshot> GetActiveEffectSnapshots()
    {
        UpdateEffects();

        _effectSnapshots.Clear();
        float now = Time.time;
        for (int i = 0; i < _effects.Count; i++)
        {
            ActiveEffect effect = _effects[i];
            _effectSnapshots.Add(new CombatStatusEffectSnapshot(
                effect.Key,
                effect.Type,
                effect.Stat,
                effect.Multiplier,
                effect.Magnitude,
                effect.TickIntervalSeconds,
                Mathf.Max(0f, effect.ExpiresAt - now)));
        }

        return _effectSnapshots;
    }

    public void Apply(StatKind stat, float multiplier, float durationSeconds, string key = null)
    {
        string effectKey = ResolveEffectKey(stat, key);
        ApplyOrUpdateEffect(new ActiveEffect
        {
            Key = effectKey,
            Type = EffectType.StatModifier,
            Stat = stat,
            Multiplier = multiplier,
            Magnitude = 0f,
            TickIntervalSeconds = 0f,
            NextTickAt = float.PositiveInfinity,
            ExpiresAt = Time.time + Mathf.Max(0f, durationSeconds),
        });
    }

    public void ApplyInvulnerable(float durationSeconds, string key = null)
    {
        ApplySimpleEffect(EffectType.Invulnerable, durationSeconds, ResolveEffectKey(EffectType.Invulnerable, key));
    }

    public void ApplyRoot(float durationSeconds, string key = null)
    {
        ApplySimpleEffect(EffectType.Root, durationSeconds, ResolveEffectKey(EffectType.Root, key));
    }

    public void ApplyBind(float durationSeconds, string key = null)
    {
        ApplySimpleEffect(EffectType.Bind, durationSeconds, ResolveEffectKey(EffectType.Bind, key));
    }

    public void ApplyPoison(int damagePerTick, float durationSeconds, float tickIntervalSeconds, string key = null)
    {
        ApplyTickEffect(
            EffectType.Poison,
            Mathf.Max(0, damagePerTick),
            durationSeconds,
            tickIntervalSeconds,
            ResolveEffectKey(EffectType.Poison, key));
    }

    public void ApplyHealOverTime(int healPerTick, float durationSeconds, float tickIntervalSeconds, string key = null)
    {
        ApplyTickEffect(
            EffectType.HealOverTime,
            Mathf.Max(0, healPerTick),
            durationSeconds,
            tickIntervalSeconds,
            ResolveEffectKey(EffectType.HealOverTime, key));
    }

    public void ApplyStealth(float durationSeconds, string key = null)
    {
        ApplySimpleEffect(EffectType.Stealth, durationSeconds, ResolveEffectKey(EffectType.Stealth, key));
    }

    public bool IsInvulnerable => HasActiveEffect(EffectType.Invulnerable);
    public bool IsRooted => HasActiveEffect(EffectType.Root);
    public bool IsBound => HasActiveEffect(EffectType.Bind);
    public bool IsStealthed => HasActiveEffect(EffectType.Stealth);

    public void ClearEffect(EffectType type)
    {
        for (int i = _effects.Count - 1; i >= 0; i--)
        {
            if (_effects[i].Type != type) continue;

            _effects.RemoveAt(i);
        }
    }

    public void ClearAll()
    {
        _effects.Clear();
        _effectSnapshots.Clear();
    }

    private void Update()
    {
        UpdateEffects();
    }

    private void ApplySimpleEffect(EffectType type, float durationSeconds, string key)
    {
        ApplyOrUpdateEffect(new ActiveEffect
        {
            Key = key,
            Type = type,
            Stat = default,
            Multiplier = 1f,
            Magnitude = 0f,
            TickIntervalSeconds = 0f,
            NextTickAt = float.PositiveInfinity,
            ExpiresAt = Time.time + Mathf.Max(0f, durationSeconds),
        });
    }

    private void ApplyTickEffect(
        EffectType type,
        int magnitude,
        float durationSeconds,
        float tickIntervalSeconds,
        string key)
    {
        float safeTickInterval = Mathf.Max(0.01f, tickIntervalSeconds);
        ApplyOrUpdateEffect(new ActiveEffect
        {
            Key = key,
            Type = type,
            Stat = default,
            Multiplier = 1f,
            Magnitude = magnitude,
            TickIntervalSeconds = safeTickInterval,
            NextTickAt = Time.time + safeTickInterval,
            ExpiresAt = Time.time + Mathf.Max(0f, durationSeconds),
        });
    }

    private void ApplyOrUpdateEffect(ActiveEffect newEffect)
    {
        for (int i = 0; i < _effects.Count; i++)
        {
            ActiveEffect effect = _effects[i];
            if (effect.Key != newEffect.Key) continue;

            _effects[i] = newEffect;
            return;
        }

        _effects.Add(newEffect);
    }

    private static string ResolveEffectKey(StatKind stat, string key)
    {
        return string.IsNullOrEmpty(key) ? stat.ToString() : key;
    }

    private static string ResolveEffectKey(EffectType type, string key)
    {
        return string.IsNullOrEmpty(key) ? type.ToString() : key;
    }

    private void UpdateEffects()
    {
        if (_isUpdatingEffects)
        {
            RemoveExpiredEffects();
            return;
        }

        _isUpdatingEffects = true;
        try
        {
            ApplyPeriodicEffects();
            RemoveExpiredEffects();
        }
        finally
        {
            _isUpdatingEffects = false;
        }
    }

    private void ApplyPeriodicEffects()
    {
        CombatHealth ownerHealth = ResolveOwnerHealth();
        if (ownerHealth == null) return;

        float now = Time.time;
        for (int i = 0; i < _effects.Count; i++)
        {
            ActiveEffect effect = _effects[i];
            if (effect.TickIntervalSeconds <= 0f) continue;
            if (effect.Magnitude <= 0f) continue;

            bool changed = false;
            while (now >= effect.NextTickAt && effect.NextTickAt <= effect.ExpiresAt)
            {
                ApplyTickEffect(ownerHealth, effect);
                effect.NextTickAt += effect.TickIntervalSeconds;
                changed = true;
            }

            if (changed)
            {
                _effects[i] = effect;
            }
        }
    }

    private static void ApplyTickEffect(CombatHealth ownerHealth, ActiveEffect effect)
    {
        int amount = Mathf.RoundToInt(effect.Magnitude);
        if (amount <= 0) return;

        switch (effect.Type)
        {
            case EffectType.Poison:
                ownerHealth.TakeDamage(amount);
                break;
            case EffectType.HealOverTime:
                ownerHealth.Heal(amount);
                break;
        }
    }

    private void RemoveExpiredEffects()
    {
        float now = Time.time;
        for (int i = _effects.Count - 1; i >= 0; i--)
        {
            if (now <= _effects[i].ExpiresAt) continue;

            _effects.RemoveAt(i);
        }
    }

    private CombatHealth ResolveOwnerHealth()
    {
        Character owner = GetComponent<Character>();
        return owner != null ? owner.Health : null;
    }
}
