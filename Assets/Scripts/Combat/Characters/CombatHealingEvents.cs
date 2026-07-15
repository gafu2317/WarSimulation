using System;

public readonly struct CombatHealingEvent
{
    public CombatHealingEvent(Character target, int amount, CombatEffectSource source)
    {
        Target = target;
        Amount = amount;
        Source = source;
    }

    public Character Target { get; }
    public int Amount { get; }
    public CombatEffectSource Source { get; }
}

public static class CombatHealingEvents
{
    public static event Action<Character, int> HealingApplied;
    public static event Action<CombatHealingEvent> Resolved;

    public static void RaiseHealingApplied(Character target, int amount)
    {
        RaiseHealingApplied(target, amount, CombatEffectSource.None);
    }

    public static void RaiseHealingApplied(
        Character target,
        int amount,
        CombatEffectSource source)
    {
        if (target == null || amount <= 0) return;
        HealingApplied?.Invoke(target, amount);
        Resolved?.Invoke(new CombatHealingEvent(target, amount, source));
        CombatSkillActionEvents.RecordCharacterEffect(
            CombatActionEffectKind.Healing,
            source,
            target,
            amount);
    }
}
