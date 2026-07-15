using System;
using System.Collections.Generic;

public static class CombatAiDecisionEvents
{
    public static event Action<Character, CombatObjective, CombatObjective, IReadOnlyList<CombatAiReasonCode>> ObjectiveChanged;
    public static event Action<Character, CombatAiPlan, CombatAiPlan> PlanSelected;

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
