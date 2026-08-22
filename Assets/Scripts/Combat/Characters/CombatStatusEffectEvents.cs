using System;

public enum CombatStatusEffectChangeKind
{
    Applied,
    Refreshed,
    Removed,
    Expired,
    Tick,
}

public readonly struct CombatStatusEffectChange
{
    public CombatStatusEffectChange(
        Character target,
        string key,
        CombatStatusEffects.EffectType type,
        CombatStatusEffectChangeKind kind,
        CombatEffectSource source,
        int amount = 0,
        CombatStatusEffects.StatKind stat = default,
        float multiplier = 1f,
        float remainingSeconds = 0f)
    {
        Target = target;
        Key = key;
        Type = type;
        Kind = kind;
        Source = source;
        Amount = amount;
        Stat = stat;
        Multiplier = multiplier;
        RemainingSeconds = remainingSeconds;
    }

    public Character Target { get; }
    public string Key { get; }
    public CombatStatusEffects.EffectType Type { get; }
    public CombatStatusEffectChangeKind Kind { get; }
    public CombatEffectSource Source { get; }
    public int Amount { get; }
    public CombatStatusEffects.StatKind Stat { get; }
    public float Multiplier { get; }
    public float RemainingSeconds { get; }
}

public static class CombatStatusEffectEvents
{
    public static event Action<Character, CombatStatusEffects.EffectType, Character> Applied;
    public static event Action<CombatStatusEffectChange> Changed;

    public static void RaiseApplied(Character target, CombatStatusEffects.EffectType type, Character source)
    {
        RaiseChanged(new CombatStatusEffectChange(
            target,
            type.ToString(),
            type,
            CombatStatusEffectChangeKind.Applied,
            CombatEffectSource.Capture(source)));
    }

    public static void RaiseChanged(CombatStatusEffectChange change)
    {
        if (change.Target == null) return;

        Changed?.Invoke(change);
        if (change.Kind == CombatStatusEffectChangeKind.Applied ||
            change.Kind == CombatStatusEffectChangeKind.Refreshed)
        {
            Applied?.Invoke(change.Target, change.Type, change.Source.Character);
        }

        CombatSkillActionEvents.RecordCharacterEffect(
            ToActionEffectKind(change.Kind),
            change.Source,
            change.Target,
            change.Amount,
            change.Type,
            change.Key);
    }

    private static CombatActionEffectKind ToActionEffectKind(CombatStatusEffectChangeKind kind)
    {
        return kind switch
        {
            CombatStatusEffectChangeKind.Applied => CombatActionEffectKind.StatusApplied,
            CombatStatusEffectChangeKind.Refreshed => CombatActionEffectKind.StatusRefreshed,
            CombatStatusEffectChangeKind.Removed => CombatActionEffectKind.StatusRemoved,
            CombatStatusEffectChangeKind.Expired => CombatActionEffectKind.StatusExpired,
            _ => CombatActionEffectKind.StatusTick,
        };
    }
}
