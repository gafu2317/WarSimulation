using System.Collections.Generic;
using UnityEngine;

public static class CombatAiAssessmentBuilder
{
    private const float MaxMetricValue = 100f;
    private const float NearbyDistance = 8f;
    private const float LowHpThreshold = 0.4f;

    public static CombatAiAssessment Build(CombatAiContext context)
    {
        var assessment = new CombatAiAssessment();
        assessment.SetValue(CombatAiMetricIndex.OwnStoneThreat, EvaluateOwnStoneThreat(context));
        assessment.SetValue(CombatAiMetricIndex.SelfThreat, EvaluateSelfThreat(context));
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
            if (distance > 18f) continue;

            value += Mathf.Lerp(28f, 4f, Mathf.Clamp01(distance / 18f));
            if (intel.HasDirectSight)
            {
                value += 8f;
            }
        }

        return ClampMetric(value);
    }

    private static float EvaluateSelfThreat(CombatAiContext context)
    {
        Character owner = context.Owner;
        if (owner == null || owner.Health == null || owner.Health.MaxHP <= 0)
        {
            return 0f;
        }

        float hpRatio = (float)owner.Health.HP / owner.Health.MaxHP;
        float value = (1f - hpRatio) * 60f;
        for (int i = 0; i < context.EnemyIntel.Count; i++)
        {
            CombatCharacterIntel intel = context.EnemyIntel[i];
            if (!intel.IsAlive || !intel.HasKnownPosition) continue;

            float distance = HorizontalDistance(owner.transform.position, intel.KnownPosition);
            if (distance <= NearbyDistance)
            {
                value += 12f;
            }

            if (intel.HasDirectSight && distance <= intel.WeaponRange + 1f)
            {
                value += 10f;
            }
        }

        int incomingDamage = context.GetEnemyPendingDamage(owner);
        if (incomingDamage > 0)
        {
            value += Mathf.Clamp(incomingDamage / (float)owner.Health.MaxHP * 60f, 0f, 60f);
        }

        int nearbyEnemies = CountActiveCharactersNear(context.EnemyIntel, owner.transform.position, 10f, true);
        int nearbyAllies = CountActiveCharactersNear(context.AllyIntel, owner.transform.position, 10f, false) + 1;
        if (nearbyEnemies > nearbyAllies)
        {
            value += Mathf.Min(36f, (nearbyEnemies - nearbyAllies) * 12f);
        }

        return ClampMetric(value);
    }

    private static float EvaluateAllyFragility(CombatAiContext context)
    {
        float value = 0f;
        for (int i = 0; i < context.AllyIntel.Count; i++)
        {
            CombatCharacterIntel ally = context.AllyIntel[i];
            if (!ally.IsAlive || ally.MaxHP <= 0) continue;

            int projectedHP = Mathf.Min(ally.MaxHP, ally.HP + context.GetAllyPendingHealing(ally.Character));
            if (projectedHP / (float)ally.MaxHP <= LowHpThreshold)
            {
                value += 22f;
            }

            int nearbyEnemyCount = CountKnownEnemiesNear(context.EnemyIntel, ally.CurrentPosition, NearbyDistance);
            if (nearbyEnemyCount > 0)
            {
                value += Mathf.Min(30f, nearbyEnemyCount * 10f);
            }

            int incomingDamage = context.GetEnemyPendingDamage(ally.Character);
            value += Mathf.Clamp(incomingDamage / (float)ally.MaxHP * 40f, 0f, 40f);
        }

        return ClampMetric(value);
    }

    private static int CountActiveCharactersNear(
        IReadOnlyList<CombatCharacterIntel> characters,
        Vector3 position,
        float radius,
        bool requireKnownPosition)
    {
        int count = 0;
        for (int i = 0; i < characters.Count; i++)
        {
            CombatCharacterIntel character = characters[i];
            if (!character.IsAlive || (requireKnownPosition && !character.HasKnownPosition)) continue;

            Vector3 characterPosition = character.HasKnownPosition
                ? character.KnownPosition
                : character.CurrentPosition;
            if (character.CanAct && HorizontalDistance(position, characterPosition) <= radius)
            {
                count++;
            }
        }

        return count;
    }

    private static int CountKnownEnemiesNear(
        IReadOnlyList<CombatCharacterIntel> enemies,
        Vector3 position,
        float radius)
    {
        int count = 0;
        for (int i = 0; i < enemies.Count; i++)
        {
            CombatCharacterIntel enemy = enemies[i];
            if (!enemy.IsAlive || !enemy.HasKnownPosition || !enemy.CanAct) continue;
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
