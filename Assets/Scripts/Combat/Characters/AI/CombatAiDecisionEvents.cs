using System;

public static class CombatAiDecisionEvents
{
    public static event Action<Character, CombatObjective, CombatObjective> ObjectiveChanged;

    public static void RaiseObjectiveChanged(Character owner, CombatObjective previous, CombatObjective next)
    {
        if (owner == null || previous == next) return;
        ObjectiveChanged?.Invoke(owner, previous, next);
    }
}
