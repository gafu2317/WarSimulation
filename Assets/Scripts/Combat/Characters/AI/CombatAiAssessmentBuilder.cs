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
        assessment.SetValue(CombatAiMetricIndex.ReachableEnemyValue, EvaluateReachableEnemyValue(context));
        assessment.SetValue(CombatAiMetricIndex.EnemyStoneReachability, EvaluateEnemyStoneReachability(context));
        assessment.SetValue(CombatAiMetricIndex.TerrainAdvantage, EvaluateTerrainAdvantage(context));
        assessment.SetValue(CombatAiMetricIndex.EnemyLocationConfidence, EvaluateEnemyLocationConfidence(context));
        assessment.SetValue(CombatAiMetricIndex.RetreatRouteSafety, EvaluateRetreatRouteSafety(context));
        assessment.SetValue(CombatAiMetricIndex.SelfExposure, EvaluateSelfExposure(context));
        assessment.SetValue(CombatAiMetricIndex.EnemyThreatLevel, EvaluateEnemyThreatLevel(context));
        assessment.SetValue(CombatAiMetricIndex.KillableTargetValue, EvaluateKillableTargetValue(context));
        assessment.SetValue(CombatAiMetricIndex.WinProximity, EvaluateWinProximity(context));

        return assessment;
    }

    private static float EvaluateWinProximity(CombatAiContext context)
    {
        return context.HasEnemyStoneHealth && context.EnemyStoneMaxHP > 0
            ? ClampMetric((1f - context.EnemyStoneHP / (float)context.EnemyStoneMaxHP) * MaxMetricValue)
            : 0f;
    }

    private static float EvaluateOwnStoneThreat(CombatAiContext context)
    {
        if (!context.HasOwnStonePosition)
        {
            return 0f;
        }

        float score = 0f;
        for (int i = 0; i < context.EnemyIntel.Count; i++)
        {
            CombatCharacterIntel intel = context.EnemyIntel[i];
            if (!intel.IsAlive || !intel.HasKnownPosition) continue;

            float distance = HorizontalDistance(intel.KnownPosition, context.OwnStonePosition);
            if (distance > 18f) continue;

            score += Mathf.Lerp(28f, 4f, Mathf.Clamp01(distance / 18f));
            if (intel.HasDirectSight)
            {
                score += 8f;
            }

        }
        return ClampMetric(score);
    }

    private static float EvaluateSelfThreat(CombatAiContext context)
    {
        Character owner = context.Owner;
        if (owner == null || owner.Health == null || owner.Health.MaxHP <= 0)
        {
            return 0f;
        }

        float hpRatio = (float)owner.Health.HP / owner.Health.MaxHP;
        float score = (1f - hpRatio) * 60f;
        for (int i = 0; i < context.EnemyIntel.Count; i++)
        {
            CombatCharacterIntel intel = context.EnemyIntel[i];
            if (!intel.IsAlive || !intel.HasKnownPosition) continue;

            float distance = HorizontalDistance(owner.transform.position, intel.KnownPosition);
            if (distance <= NearbyDistance)
            {
                score += 12f;
            }

            if (intel.HasDirectSight && distance <= intel.WeaponRange + 1f)
            {
                score += 10f;
            }

        }

        int incomingDamage = context.GetEnemyPendingDamage(owner);
        if (incomingDamage > 0)
        {
            score += Mathf.Clamp(incomingDamage / (float)owner.Health.MaxHP * 60f, 0f, 60f);
        }

        int nearbyEnemies = CountActiveCharactersNear(context.EnemyIntel, owner.transform.position, 10f, true);
        int nearbyAllies = CountActiveCharactersNear(context.AllyIntel, owner.transform.position, 10f, false) + 1;
        if (nearbyEnemies > nearbyAllies)
        {
            score += Mathf.Min(36f, (nearbyEnemies - nearbyAllies) * 12f);
        }

        return ClampMetric(score);
    }

    private static float EvaluateAllyFragility(CombatAiContext context)
    {
        float score = 0f;
        for (int i = 0; i < context.AllyIntel.Count; i++)
        {
            CombatCharacterIntel ally = context.AllyIntel[i];
            if (!ally.IsAlive || ally.MaxHP <= 0) continue;

            int projectedHP = Mathf.Min(
                ally.MaxHP,
                ally.HP + context.GetAllyPendingHealing(ally.Character));
            float hpRatio = projectedHP / (float)ally.MaxHP;
            if (hpRatio <= LowHpThreshold)
            {
                score += 22f;
            }

            int nearbyEnemyCount = CountKnownEnemiesNear(context.EnemyIntel, ally.CurrentPosition, NearbyDistance);
            if (nearbyEnemyCount > 0)
            {
                score += Mathf.Min(30f, nearbyEnemyCount * 10f);
            }

            int incomingDamage = context.GetEnemyPendingDamage(ally.Character);

            score += Mathf.Clamp(incomingDamage / (float)ally.MaxHP * 40f, 0f, 40f);
        }

        return ClampMetric(score);
    }

    private static float EvaluateReachableEnemyValue(CombatAiContext context)
    {
        Character owner = context.Owner;
        if (owner == null)
        {
            return 0f;
        }

        float best = 0f;
        for (int i = 0; i < context.EnemyIntel.Count; i++)
        {
            CombatCharacterIntel enemy = context.EnemyIntel[i];
            if (!enemy.IsAlive || !enemy.HasKnownPosition) continue;

            float score = 0f;
            float distance = HorizontalDistance(owner.transform.position, enemy.KnownPosition);
            float hpRatio = enemy.MaxHP > 0 ? (float)enemy.HP / enemy.MaxHP : 1f;

            score += (1f - hpRatio) * 38f;
            if (enemy.HasDirectSight)
            {
                score += 16f;
            }
            else if (enemy.HasMemory)
            {
                score += 6f;
            }

            if (distance <= enemy.WeaponRange + 1f || distance <= (owner.EquippedWeapon != null ? owner.EquippedWeapon.Range + 1f : 2f))
            {
                score += 18f;
            }

            score += Mathf.Lerp(18f, 0f, Mathf.Clamp01(distance / 20f));
            best = Mathf.Max(best, score);
        }

        return ClampMetric(best);
    }

    private static float EvaluateEnemyStoneReachability(CombatAiContext context)
    {
        if (!context.HasEnemyStonePosition || context.Owner == null)
        {
            return 0f;
        }

        float distance = HorizontalDistance(context.Owner.transform.position, context.EnemyStonePosition);
        float score = Mathf.Lerp(70f, 0f, Mathf.Clamp01(distance / 60f));
        return ClampMetric(score);
    }

    private static float EvaluateTerrainAdvantage(CombatAiContext context)
    {
        Character owner = context.Owner;
        if (owner == null)
        {
            return 0f;
        }

        float score = 0f;
        if (NearestDistance(owner.transform.position, context.HighGroundCandidates) <= 12f)
        {
            score += 28f;
        }

        if (NearestDistance(owner.transform.position, context.ForestCandidates) <= 10f)
        {
            score += 14f;
        }

        if (context.Weather == CombatMapSystem.Weather.Sunny)
        {
            score += 8f;
        }
        else if (context.Weather == CombatMapSystem.Weather.Rainy || context.Weather == CombatMapSystem.Weather.Cold)
        {
            score -= 6f;
        }

        return ClampMetric(score);
    }

    private static float EvaluateEnemyLocationConfidence(CombatAiContext context)
    {
        int visibleEnemies = CountLivingIntel(context.EnemyIntel, requireDirectSight: true);
        int rememberedEnemies = CountLivingIntel(context.EnemyIntel, requireMemory: true);
        float score = visibleEnemies * 30f + rememberedEnemies * 10f;
        return ClampMetric(score);
    }

    private static float EvaluateRetreatRouteSafety(CombatAiContext context)
    {
        Character owner = context.Owner;
        if (owner == null)
        {
            return 0f;
        }

        float score = 0f;
        if (context.HasOwnStonePosition)
        {
            score += 30f;
        }

        if (NearestDistance(owner.transform.position, context.ForestCandidates) <= 10f)
        {
            score += 20f;
        }

        int nearbyEnemies = CountEnemiesNear(context.EnemyIntel, owner.transform.position, 12f);
        score -= nearbyEnemies * 8f;
        return ClampMetric(score);
    }

    private static float EvaluateSelfExposure(CombatAiContext context)
    {
        Character owner = context.Owner;
        if (owner == null)
        {
            return 0f;
        }

        float score = 0f;
        for (int i = 0; i < context.EnemyIntel.Count; i++)
        {
            CombatCharacterIntel intel = context.EnemyIntel[i];
            if (!intel.IsAlive || !intel.RecognizesOwner || !intel.HasKnownPosition) continue;

            float distance = HorizontalDistance(owner.transform.position, intel.KnownPosition);
            score += Mathf.Lerp(30f, 8f, Mathf.Clamp01(distance / 20f));
            if (intel.HasDirectSight)
            {
                score += 8f;
            }
        }

        return ClampMetric(score);
    }

    private static float EvaluateEnemyThreatLevel(CombatAiContext context)
    {
        Character owner = context.Owner;
        if (owner == null)
        {
            return 0f;
        }

        float highestThreat = 0f;
        float otherThreatTotal = 0f;
        for (int i = 0; i < context.EnemyIntel.Count; i++)
        {
            CombatCharacterIntel enemy = context.EnemyIntel[i];
            if (!enemy.HasKnownPosition || enemy.HP <= 0) continue;

            float threat = GetRoleThreat(enemy.WeaponKind)
                * GetEngagementFactor(owner, enemy)
                * GetActionFactor(enemy);

            if (threat <= 0f) continue;

            if (threat > highestThreat)
            {
                otherThreatTotal += highestThreat;
                highestThreat = threat;
            }
            else
            {
                otherThreatTotal += threat;
            }

        }

        float combinedThreat = Mathf.Clamp01(highestThreat + otherThreatTotal * 0.3f);
        return combinedThreat * MaxMetricValue;
    }

    private static float EvaluateKillableTargetValue(CombatAiContext context)
    {
        Character owner = context.Owner;
        if (owner == null)
        {
            return 0f;
        }

        float best = 0f;
        for (int i = 0; i < context.EnemyIntel.Count; i++)
        {
            CombatCharacterIntel enemy = context.EnemyIntel[i];
            if (!enemy.HasKnownPosition || enemy.HP <= 0) continue;

            int predictedHp = enemy.HP - context.GetAllyPendingDamage(enemy.Character);
            if (predictedHp <= 0) continue;

            float targetValue = Mathf.Lerp(0.45f, 1f, GetRoleThreat(enemy.WeaponKind));
            float finishEase = GetFinishEase(enemy, predictedHp);
            float reachFactor = GetOwnerReachFactor(owner, enemy);
            float recognitionFactor = enemy.HasDirectSight ? 1f : enemy.HasMemory ? 0.7f : 0f;
            float actionOpportunity = enemy.CanAct ? 1f : 1.15f;
            float score = Mathf.Clamp01(targetValue * finishEase * reachFactor * recognitionFactor * actionOpportunity);

            if (score > best)
            {
                best = score;
            }

        }

        return best * MaxMetricValue;
    }

    private static float ClampMetric(float value)
    {
        return Mathf.Clamp(value, 0f, MaxMetricValue);
    }

    private static float GetRoleThreat(WeaponKind kind)
    {
        return kind switch
        {
            WeaponKind.Sword => 0.9f,
            WeaponKind.Wand => 0.9f,
            WeaponKind.Grimoire => 0.7f,
            WeaponKind.Shield => 0.55f,
            WeaponKind.Bible => 0.35f,
            WeaponKind.Rosary => 0.25f,
            _ => 0.2f,
        };
    }

    private static float GetEngagementFactor(Character owner, CombatCharacterIntel enemy)
    {
        float distance = HorizontalDistance(owner.transform.position, enemy.KnownPosition);
        float enemyRange = Mathf.Max(enemy.WeaponRange, 2f);
        if (distance <= enemyRange + 1f) return 1f;
        if (distance <= enemyRange + 6f) return 0.7f;
        if (distance <= 24f) return 0.25f;
        return 0f;
    }

    private static float GetActionFactor(CombatCharacterIntel enemy)
    {
        if (enemy.HP <= 0) return 0f;

        float hpRatio = enemy.MaxHP > 0 ? enemy.HP / (float)enemy.MaxHP : 1f;
        if (!enemy.CanAct && hpRatio <= 0.3f) return 0.25f;
        if (!enemy.CanAct) return 0.35f;
        if (hpRatio <= 0.3f) return 0.6f;
        return 1f;
    }

    private static float GetFinishEase(CombatCharacterIntel enemy, int hp)
    {
        float hpRatio = enemy.MaxHP > 0 ? hp / (float)enemy.MaxHP : 1f;
        if (hpRatio <= 0.3f) return 1f;
        if (hpRatio <= 0.5f) return 0.7f;
        if (hpRatio <= 0.7f) return 0.35f;
        return 0.1f;
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
            Vector3 characterPosition = character.HasKnownPosition ? character.KnownPosition : character.CurrentPosition;
            if (character.CanAct && HorizontalDistance(position, characterPosition) <= radius)
            {
                count++;
            }
        }

        return count;
    }

    private static float GetOwnerReachFactor(Character owner, CombatCharacterIntel enemy)
    {
        float ownerRange = owner.EquippedWeapon != null ? owner.EquippedWeapon.Range : 2f;
        float distance = HorizontalDistance(owner.transform.position, enemy.KnownPosition);
        if (distance <= ownerRange + 1f) return 1f;
        if (distance <= ownerRange + 6f) return 0.6f;
        if (distance <= 16f) return 0.2f;
        return 0f;
    }

    private static int CountKnownEnemiesNear(
        IReadOnlyList<CombatCharacterIntel> enemies,
        Vector3 position,
        float radius)
    {
        int count = 0;
        for (int i = 0; i < enemies.Count; i++)
        {
            if (!enemies[i].IsAlive || !enemies[i].HasKnownPosition || !enemies[i].CanAct) continue;
            if (HorizontalDistance(position, enemies[i].KnownPosition) <= radius)
            {
                count++;
            }
        }

        return count;
    }

    private static int CountEnemiesNear(IReadOnlyList<CombatCharacterIntel> enemies, Vector3 position, float radius)
    {
        int count = 0;
        for (int i = 0; i < enemies.Count; i++)
        {
            if (!enemies[i].IsAlive || !enemies[i].HasKnownPosition) continue;
            if (HorizontalDistance(position, enemies[i].KnownPosition) <= radius)
            {
                count++;
            }
        }

        return count;
    }

    private static int CountLivingIntel(
        IReadOnlyList<CombatCharacterIntel> characters,
        bool requireDirectSight = false,
        bool requireMemory = false)
    {
        int count = 0;
        for (int i = 0; i < characters.Count; i++)
        {
            CombatCharacterIntel character = characters[i];
            if (!character.IsAlive) continue;
            if (requireDirectSight && !character.HasDirectSight) continue;
            if (requireMemory && !character.HasMemory) continue;
            count++;
        }

        return count;
    }

    private static float NearestDistance(Vector3 origin, IReadOnlyList<Vector3> positions)
    {
        float best = float.PositiveInfinity;
        for (int i = 0; i < positions.Count; i++)
        {
            best = Mathf.Min(best, HorizontalDistance(origin, positions[i]));
        }

        return best;
    }

    private static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }
}
