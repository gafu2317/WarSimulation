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
            if (ally.Character == null || !ally.IsAlive || ally.MaxHP <= 0) continue;

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
}
