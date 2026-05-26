using System.Collections.Generic;
using UnityEngine;

public readonly struct CombatStatusEffectSnapshot
{
    public string Key { get; }
    public CombatStatusEffects.StatKind Stat { get; }
    public float Multiplier { get; }
    public float RemainingSeconds { get; }
    public bool IsBuff => Multiplier > 1f;
    public bool IsDebuff => Multiplier < 1f;

    public CombatStatusEffectSnapshot(
        string key,
        CombatStatusEffects.StatKind stat,
        float multiplier,
        float remainingSeconds)
    {
        Key = key;
        Stat = stat;
        Multiplier = multiplier;
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

    private struct ActiveEffect
    {
        public string Key;
        public StatKind Stat;
        public float Multiplier;
        public float ExpiresAt;
    }

    private const float MinMultiplier = 0.1f;

    private readonly List<ActiveEffect> _effects = new();
    private readonly List<CombatStatusEffectSnapshot> _effectSnapshots = new();

    public float GetMultiplier(StatKind stat)
    {
        RemoveExpiredEffects();

        float totalDelta = 0f;
        for (int i = 0; i < _effects.Count; i++)
        {
            ActiveEffect effect = _effects[i];
            if (effect.Stat != stat) continue;

            totalDelta += effect.Multiplier - 1f;
        }

        return Mathf.Max(MinMultiplier, 1f + totalDelta);
    }

    public bool HasActiveEffect(string key)
    {
        if (string.IsNullOrEmpty(key)) return false;

        RemoveExpiredEffects();
        for (int i = 0; i < _effects.Count; i++)
        {
            if (_effects[i].Key == key) return true;
        }

        return false;
    }

    public float GetRemainingSeconds(string key)
    {
        if (string.IsNullOrEmpty(key)) return 0f;

        RemoveExpiredEffects();
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
        RemoveExpiredEffects();

        _effectSnapshots.Clear();
        float now = Time.time;
        for (int i = 0; i < _effects.Count; i++)
        {
            ActiveEffect effect = _effects[i];
            _effectSnapshots.Add(new CombatStatusEffectSnapshot(
                effect.Key,
                effect.Stat,
                effect.Multiplier,
                Mathf.Max(0f, effect.ExpiresAt - now)));
        }

        return _effectSnapshots;
    }

    public void Apply(StatKind stat, float multiplier, float durationSeconds, string key = null)
    {
        string effectKey = ResolveEffectKey(stat, key);
        float expiresAt = Time.time + Mathf.Max(0f, durationSeconds);

        for (int i = 0; i < _effects.Count; i++)
        {
            ActiveEffect effect = _effects[i];
            if (effect.Key != effectKey) continue;

            effect.Stat = stat;
            effect.Multiplier = multiplier;
            effect.ExpiresAt = expiresAt;
            _effects[i] = effect;
            return;
        }

        _effects.Add(new ActiveEffect
        {
            Key = effectKey,
            Stat = stat,
            Multiplier = multiplier,
            ExpiresAt = expiresAt,
        });
    }

    private static string ResolveEffectKey(StatKind stat, string key)
    {
        return string.IsNullOrEmpty(key) ? stat.ToString() : key;
    }

    private void Update()
    {
        RemoveExpiredEffects();
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
}
