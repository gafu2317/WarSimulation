using System;
using System.Collections.Generic;
using UnityEngine;

public static class CombatAiDecisionEvents
{
    public static event Action<Character, CombatObjective, CombatObjective, IReadOnlyList<CombatAiReasonCode>> ObjectiveChanged;
    public static event Action<Character, CombatAiPlan, CombatAiPlan> PlanSelected;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForPlay()
    {
        ObjectiveChanged = null;
        PlanSelected = null;
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
}
