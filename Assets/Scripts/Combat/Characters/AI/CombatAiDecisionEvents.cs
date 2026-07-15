using System;

public static class CombatAiDecisionEvents
{
    public static event Action<Character, CombatObjective, CombatObjective> ObjectiveChanged;
    public static event Action<Character, CombatAiPlan, CombatAiPlan> PlanSelected;

    public static void RaiseObjectiveChanged(Character owner, CombatObjective previous, CombatObjective next)
    {
        if (owner == null || previous == next) return;
        ObjectiveChanged?.Invoke(owner, previous, next);
    }

    public static void RaisePlanSelected(Character owner, CombatAiPlan previous, CombatAiPlan next)
    {
        if (owner == null) return;
        PlanSelected?.Invoke(owner, previous, next);
    }
}
