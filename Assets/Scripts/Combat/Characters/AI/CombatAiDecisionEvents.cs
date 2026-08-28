using System;
using System.Collections.Generic;
using UnityEngine;

public static class CombatAiDecisionEvents
{
    public static event Action<Character, CombatObjective, CombatObjective, IReadOnlyList<CombatAiReasonCode>> ObjectiveChanged;
    public static event Action<Character, CombatAiPlan, CombatAiPlan> PlanSelected;
    public static event Action<Character, CombatAiPlan, bool, bool, string> PlanExecuted;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForPlay()
    {
        ObjectiveChanged = null;
        PlanSelected = null;
        PlanExecuted = null;
    }

    public static void RaiseObjectiveChanged(
        Character owner,
        CombatObjective previous,
        CombatObjective next,
        IReadOnlyList<CombatAiReasonCode> reasonCodes)
    {
        if (owner == null || previous == next) return;
        ObjectiveChanged?.Invoke(owner, previous, next, reasonCodes);
    }

    public static void RaisePlanSelected(Character owner, CombatAiPlan previous, CombatAiPlan next)
    {
        if (owner == null) return;
        PlanSelected?.Invoke(owner, previous, next);
    }

    public static void RaisePlanExecuted(
        Character owner,
        CombatAiPlan plan,
        bool movementStarted,
        bool skillStarted,
        string failureReason)
    {
        if (owner == null) return;
        PlanExecuted?.Invoke(owner, plan, movementStarted, skillStarted, failureReason ?? string.Empty);
    }
}
