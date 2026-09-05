using System.Collections.Generic;
using UnityEngine;

public static class CombatAiAssessmentBuilder
{
    internal const float OwnStoneAreaRadius = 18f;
    private const float MaxMetricValue = 100f;
    private const float NearbyDistance = 8f;
    private const float LowHpThreshold = 0.4f;

    public static CombatAiAssessment Build(CombatAiContext context)
    {
        var assessment = new CombatAiAssessment();
        assessment.SetValue(CombatAiMetricIndex.OwnStoneThreat, EvaluateOwnStoneThreat(context));
        assessment.SetValue(CombatAiMetricIndex.AllyFragility, EvaluateAllyFragility(context));
        return assessment;
    }

    private static float EvaluateOwnStoneThreat(CombatAiContext context)
    {
        if (!context.HasOwnStonePosition)
        {
            return 0f;
        }

        float value = 0f;
        for (int i = 0; i < context.EnemyIntel.Count; i++)
        {
            CombatCharacterIntel intel = context.EnemyIntel[i];
            if (!intel.IsAlive || !intel.HasKnownPosition) continue;

            float distance = HorizontalDistance(intel.KnownPosition, context.OwnStonePosition);
            if (distance > OwnStoneAreaRadius) continue;

            value += Mathf.Lerp(28f, 4f, Mathf.Clamp01(distance / OwnStoneAreaRadius));
            if (intel.HasDirectSight)
            {
                value += 8f;
            }
        }

        return ClampMetric(value);
    }

    private static float EvaluateAllyFragility(CombatAiContext context)
    {
        float value = 0f;
        for (int i = 0; i < context.AllyIntel.Count; i++)
        {
            CombatCharacterIntel ally = context.AllyIntel[i];
            if (!ally.IsAlive || ally.MaxHP <= 0 ||
                ally.HasObjective && ally.Objective == CombatObjective.SupportAlly) continue;

            float allyValue = 0f;
            int projectedHP = Mathf.Min(ally.MaxHP, ally.HP + context.GetAllyPendingHealing(ally.Character));
            if (projectedHP / (float)ally.MaxHP <= LowHpThreshold)
            {
                allyValue += 22f;
            }

            int nearbyEnemyCount = CountVisibleEnemiesNear(context.EnemyIntel, ally.CurrentPosition, NearbyDistance);
            if (nearbyEnemyCount > 0)
            {
                allyValue += Mathf.Min(30f, nearbyEnemyCount * 10f);
            }

            int incomingDamage = context.GetEnemyPendingDamage(ally.Character);
            allyValue += Mathf.Clamp(incomingDamage / (float)ally.MaxHP * 40f, 0f, 40f);
            value = Mathf.Max(value, allyValue);
        }

        return ClampMetric(value);
    }

    private static int CountVisibleEnemiesNear(
        IReadOnlyList<CombatCharacterIntel> enemies,
        Vector3 position,
        float radius)
    {
        int count = 0;
        for (int i = 0; i < enemies.Count; i++)
        {
            CombatCharacterIntel enemy = enemies[i];
            if (!enemy.IsAlive || !enemy.HasDirectSight || !enemy.HasKnownPosition || !enemy.CanAct) continue;
            if (HorizontalDistance(position, enemy.KnownPosition) <= radius)
            {
                count++;
            }
        }

        return count;
    }

    private static float ClampMetric(float value)
    {
        return Mathf.Clamp(value, 0f, MaxMetricValue);
    }

    private static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }
}
