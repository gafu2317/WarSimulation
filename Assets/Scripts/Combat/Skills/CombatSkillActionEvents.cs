using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;

public enum CombatSkillActionOutcome
{
    Completed,
    NoEffect,
    Failed,
    Cancelled,
}

public enum CombatActionEffectKind
{
    Damage,
    DamagePrevented,
    Healing,
    MagicStoneDamage,
    StatusApplied,
    StatusRefreshed,
    StatusRemoved,
    StatusExpired,
    StatusTick,
    PersistentEffectStarted,
}

public readonly struct CombatSkillActionInfo
{
    public CombatSkillActionInfo(
        long actionId,
        Character actor,
        SkillBase skill,
        SkillExecutionContext context,
        int decisionTick)
    {
        ActionId = actionId;
        Actor = actor;
        Skill = skill;
        SkillId = skill != null ? skill.Id : SkillId.None;
        SkillName = skill != null ? skill.Name : string.Empty;
        Context = context;
        DecisionTick = decisionTick;
    }

    public long ActionId { get; }
    public Character Actor { get; }
    public SkillBase Skill { get; }
    public SkillId SkillId { get; }
    public string SkillName { get; }
    public SkillExecutionContext Context { get; }
    public int DecisionTick { get; }
}

public readonly struct CombatActionEffect
{
    public CombatActionEffect(
        CombatActionEffectKind kind,
        CombatEffectSource source,
        Character target,
        int magicStoneFeatureIndex,
        int amount,
        CombatStatusEffects.EffectType statusType,
        string statusKey)
    {
        Kind = kind;
        Source = source;
        Target = target;
        MagicStoneFeatureIndex = magicStoneFeatureIndex;
        Amount = amount;
        StatusType = statusType;
        StatusKey = statusKey;
    }

    public CombatActionEffectKind Kind { get; }
    public CombatEffectSource Source { get; }
    public Character Target { get; }
    public int MagicStoneFeatureIndex { get; }
    public int Amount { get; }
    public CombatStatusEffects.EffectType StatusType { get; }
    public string StatusKey { get; }
}

public sealed class CombatSkillActionResult
{
    public CombatSkillActionResult(
        CombatSkillActionInfo action,
        CombatSkillActionOutcome outcome,
        IReadOnlyList<CombatActionEffect> effects)
    {
        Action = action;
        Outcome = outcome;
        Effects = effects ?? Array.Empty<CombatActionEffect>();
    }

    public CombatSkillActionInfo Action { get; }
    public CombatSkillActionOutcome Outcome { get; }
    public IReadOnlyList<CombatActionEffect> Effects { get; }
}

public static class CombatSkillActionEvents
{
    private sealed class Recording
    {
        public Recording(CombatSkillActionInfo action)
        {
            Action = action;
        }

        public CombatSkillActionInfo Action { get; }
        public List<CombatActionEffect> Effects { get; } = new List<CombatActionEffect>();
    }

    private static long _nextActionId = 1;
    private static Recording _activeRecording;

    public static event Action<CombatSkillActionInfo> Started;
    public static event Action<CombatSkillActionResult> Completed;
    public static event Action<CombatSkillActionResult> Cancelled;

    public static void ResetBattle()
    {
        _nextActionId = 1;
        _activeRecording = null;
    }

    public static CombatSkillActionInfo Start(
        Character actor,
        SkillBase skill,
        SkillExecutionContext context)
    {
        var action = new CombatSkillActionInfo(
            _nextActionId++,
            actor,
            skill,
            context,
            CombatBattleRandom.GetDecisionTick(actor));
        Started?.Invoke(action);
        return action;
    }

    public static void Execute(CombatSkillActionInfo action, Action execute)
    {
        var recording = new Recording(action);
        Recording previous = _activeRecording;
        _activeRecording = recording;
        Exception failure = null;
        try
        {
            execute?.Invoke();
        }
        catch (Exception exception)
        {
            failure = exception;
        }
        finally
        {
            _activeRecording = previous;
        }

        if (failure != null)
        {
            Completed?.Invoke(new CombatSkillActionResult(
                action,
                CombatSkillActionOutcome.Failed,
                recording.Effects.ToArray()));
            ExceptionDispatchInfo.Capture(failure).Throw();
            return;
        }

        CombatSkillActionOutcome outcome = recording.Effects.Count > 0
            ? CombatSkillActionOutcome.Completed
            : CombatSkillActionOutcome.NoEffect;
        Completed?.Invoke(new CombatSkillActionResult(action, outcome, recording.Effects.ToArray()));
    }

    public static void Cancel(CombatSkillActionInfo action)
    {
        Cancelled?.Invoke(new CombatSkillActionResult(
            action,
            CombatSkillActionOutcome.Cancelled,
            Array.Empty<CombatActionEffect>()));
    }

    public static CombatEffectSource ResolveSource(Character character)
    {
        if (character == null) return CombatEffectSource.None;
        if (_activeRecording != null && _activeRecording.Action.Actor == character)
        {
            CombatSkillActionInfo action = _activeRecording.Action;
            return new CombatEffectSource(character, action.SkillId, action.SkillName);
        }

        return new CombatEffectSource(character, SkillId.None, null);
    }

    public static void RecordCharacterEffect(
        CombatActionEffectKind kind,
        CombatEffectSource source,
        Character target,
        int amount = 0,
        CombatStatusEffects.EffectType statusType = default,
        string statusKey = null)
    {
        if (!CanRecord(source)) return;

        _activeRecording.Effects.Add(new CombatActionEffect(
            kind,
            source,
            target,
            -1,
            amount,
            statusType,
            statusKey));
    }

    public static void RecordMagicStoneDamage(
        CombatEffectSource source,
        int featureIndex,
        int amount)
    {
        if (amount <= 0 || !CanRecord(source)) return;

        _activeRecording.Effects.Add(new CombatActionEffect(
            CombatActionEffectKind.MagicStoneDamage,
            source,
            null,
            featureIndex,
            amount,
            default,
            null));
    }

    private static bool CanRecord(CombatEffectSource source)
    {
        return _activeRecording != null &&
            source.Character != null &&
            source.Character == _activeRecording.Action.Actor;
    }
}
