using System;
using UnityEngine;

public readonly struct CombatDamageEvent
{
    public CombatDamageEvent(
        Character target,
        int amount,
        CombatEffectSource attackSource,
        CombatEffectSource preventionSource,
        bool wasPrevented)
    {
        Target = target;
        Amount = amount;
        AttackSource = attackSource;
        PreventionSource = preventionSource;
        WasPrevented = wasPrevented;
    }

    public Character Target { get; }
    public int Amount { get; }
    public CombatEffectSource AttackSource { get; }
    public CombatEffectSource PreventionSource { get; }
    public CombatEffectSource Source => WasPrevented && PreventionSource.HasCharacter
        ? PreventionSource
        : AttackSource;
    public bool WasPrevented { get; }
}

public static class CombatDamageEvents
{
    public static event Action<Character, int, Character> DamageApplied;
    public static event Action<Character, int, Character> DamagePrevented;
    public static event Action<CombatDamageEvent> Resolved;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForPlay()
    {
        DamageApplied = null;
        DamagePrevented = null;
        Resolved = null;
    }

    public static void RaiseDamageApplied(Character victim, int amount, Character attacker)
    {
        RaiseDamageApplied(victim, amount, CombatEffectSource.Capture(attacker));
    }

    public static void RaiseDamageApplied(Character victim, int amount, CombatEffectSource source)
    {
        if (victim == null || amount <= 0) return;
        DamageApplied?.Invoke(victim, amount, source.Character);
        Resolved?.Invoke(new CombatDamageEvent(
            victim,
            amount,
            source,
            CombatEffectSource.None,
            wasPrevented: false));
        CombatSkillActionEvents.RecordCharacterEffect(
            CombatActionEffectKind.Damage,
            source,
            victim,
            amount);
    }

    public static void RaiseDamagePrevented(Character victim, int amount, Character attacker)
    {
        RaiseDamagePrevented(
            victim,
            amount,
            CombatEffectSource.Capture(attacker),
            CombatEffectSource.None);
    }

    public static void RaiseDamagePrevented(
        Character victim,
        int amount,
        CombatEffectSource attackSource,
        CombatEffectSource preventionSource)
    {
        if (victim == null || amount <= 0) return;
        DamagePrevented?.Invoke(victim, amount, attackSource.Character);
        Resolved?.Invoke(new CombatDamageEvent(
            victim,
            amount,
            attackSource,
            preventionSource,
            wasPrevented: true));
        CombatSkillActionEvents.RecordCharacterEffect(
            CombatActionEffectKind.DamagePrevented,
            attackSource,
            victim,
            amount);
    }
}
