using UnityEngine;

public static class CombatAiPersonalityBehavior
{
    public const float LonelyNearbyAllyRadius = 5f;
    public const float DevotedAssistRadius = 12f;
    public const float AttentionCrowdSampleRadius = 8f;

    public static bool HasNearbyAlly(CombatAiContext context, float radius)
    {
        if (context == null || context.Owner == null) return false;
        for (int i = 0; i < context.AllyIntel.Count; i++)
        {
            CombatCharacterIntel ally = context.AllyIntel[i];
            if (!ally.IsAlive || ally.Character == null) continue;
            if (Vector3.Distance(context.Owner.transform.position, ally.CurrentPosition) <= radius)
            {
                return true;
            }
        }

        return false;
    }

    public static Character FindLowestHpAllyInRange(CombatAiContext context, float radius)
    {
        if (context == null || context.Owner == null) return null;

        Character best = null;
        float bestHpRatio = float.PositiveInfinity;
        float bestDistance = float.PositiveInfinity;
        for (int i = 0; i < context.AllyIntel.Count; i++)
        {
            CombatCharacterIntel ally = context.AllyIntel[i];
            if (ally.Character == null || !ally.IsAlive || ally.MaxHP <= 0 ||
                ally.HasObjective && ally.Objective == CombatObjective.SupportAlly) continue;

            float distance = Vector3.Distance(context.Owner.transform.position, ally.CurrentPosition);
            if (distance > radius) continue;

            float hpRatio = ally.HP / (float)ally.MaxHP;
            if (hpRatio >= 0.95f) continue;
            if (hpRatio > bestHpRatio || hpRatio == bestHpRatio && distance >= bestDistance) continue;
            bestHpRatio = hpRatio;
            bestDistance = distance;
            best = ally.Character;
        }

        return best;
    }

    public static bool TryFindAssignedAllyWithObjective(
        CombatAiContext context,
        out CombatCharacterIntel leader)
    {
        leader = default;
        if (context == null || context.Owner == null || context.TagalongTarget == null ||
            context.TagalongTarget == context.Owner) return false;

        CombatCharacterIntel assigned = context.FindAllyIntel(context.TagalongTarget);
        if (assigned.Character != context.TagalongTarget || assigned.Team != context.Owner.Team ||
            !assigned.IsAlive || !assigned.CanAct || !assigned.HasObjective ||
            (!assigned.HasIntendedDestination && assigned.IntendedTarget == null) ||
            assigned.IntendedTarget != null &&
            (assigned.IntendedTarget.Health == null || !assigned.IntendedTarget.Health.IsAlive))
        {
            return false;
        }

        leader = assigned;
        return true;
    }

    public static bool TryFindKnownRecentAttacker(
        CombatAiContext context,
        out CombatCharacterIntel attacker)
    {
        attacker = default;
        if (context == null || context.RecentAttacker == null) return false;

        attacker = context.FindEnemyIntel(context.RecentAttacker);
        return attacker.Character != null && attacker.IsAlive && attacker.HasKnownPosition;
    }
}
