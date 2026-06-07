using System.Collections.Generic;
using UnityEngine;

public static class CombatAiAssessmentBuilder
{
    private const float MaxMetricValue = 100f;
    private const float NearbyDistance = 8f;
    private const float LowHpThreshold = 0.4f;

    public static CombatAiContextSummary BuildSummary(CombatAiContext context, CombatAiPersonalityProfile personalityProfile)
    {
        int lowHpAllies = 0;
        int knownEnemies = 0;
        for (int i = 0; i < context.AllyIntel.Count; i++)
        {
            CombatCharacterIntel intel = context.AllyIntel[i];
            if (intel.MaxHP <= 0) continue;
            if ((float)intel.HP / intel.MaxHP <= LowHpThreshold)
            {
                lowHpAllies++;
            }
        }

        for (int i = 0; i < context.EnemyIntel.Count; i++)
        {
            if (context.EnemyIntel[i].HasKnownPosition)
            {
                knownEnemies++;
            }
        }

        return new CombatAiContextSummary
        {
            VisibleEnemyCount = context.VisibleEnemies.Count,
            RememberedEnemyCount = context.RememberedEnemies.Count,
            KnownEnemyCount = knownEnemies,
            AllyCount = context.Allies.Count,
            LowHpAllyCount = lowHpAllies,
            WeatherLabel = CombatAiDebugLabels.Format(context.Weather.ToString(), context.Weather.ToString()),
            WeaponLabel = CombatAiDebugLabels.Weapon(context.Owner != null ? context.Owner.EquippedWeapon : null),
            PersonalityLabel = CombatAiDebugLabels.Personality(personalityProfile),
        };
    }

    public static CombatAiAssessment Build(CombatAiContext context)
    {
        var assessment = new CombatAiAssessment();
        assessment.Metrics.Add(EvaluateOwnStoneThreat(context));
        assessment.Metrics.Add(EvaluateSelfThreat(context));
        assessment.Metrics.Add(EvaluateAllyFragility(context));
        assessment.Metrics.Add(EvaluateReachableEnemyValue(context));
        assessment.Metrics.Add(EvaluateEnemyStoneReachability(context));
        assessment.Metrics.Add(EvaluateTerrainAdvantage(context));
        assessment.Metrics.Add(EvaluateEnemyLocationConfidence(context));
        assessment.Metrics.Add(EvaluateRetreatRouteSafety(context));
        return assessment;
    }

    private static CombatAiMetric EvaluateOwnStoneThreat(CombatAiContext context)
    {
        var metric = CreateMetric("OwnStoneThreat");
        if (!context.HasOwnStonePosition)
        {
            metric.ReasonCodes.Add(CombatAiReasonCode.EnemyLocationUncertain);
            return metric;
        }

        float score = 0f;
        for (int i = 0; i < context.EnemyIntel.Count; i++)
        {
            CombatCharacterIntel intel = context.EnemyIntel[i];
            if (!intel.HasKnownPosition) continue;

            float distance = HorizontalDistance(intel.KnownPosition, context.OwnStonePosition);
            if (distance > 18f) continue;

            score += Mathf.Lerp(28f, 4f, Mathf.Clamp01(distance / 18f));
            if (intel.HasDirectSight)
            {
                score += 8f;
                AddReason(metric, CombatAiReasonCode.VisibleEnemy);
            }

            if (distance <= NearbyDistance)
            {
                AddReason(metric, CombatAiReasonCode.EnemyNearOwnStone);
            }
        }

        if (score > 45f)
        {
            AddReason(metric, CombatAiReasonCode.OwnStoneThreatHigh);
        }

        AddReason(metric, CombatAiReasonCode.OwnStoneKnown);
        metric.Value = ClampMetric(score);
        return metric;
    }

    private static CombatAiMetric EvaluateSelfThreat(CombatAiContext context)
    {
        var metric = CreateMetric("SelfThreat");
        Character owner = context.Owner;
        if (owner == null || owner.Health == null || owner.Health.MaxHP <= 0)
        {
            return metric;
        }

        float hpRatio = (float)owner.Health.HP / owner.Health.MaxHP;
        float score = (1f - hpRatio) * 60f;
        if (hpRatio <= LowHpThreshold)
        {
            AddReason(metric, CombatAiReasonCode.OwnHpLow);
        }

        for (int i = 0; i < context.EnemyIntel.Count; i++)
        {
            CombatCharacterIntel intel = context.EnemyIntel[i];
            if (!intel.HasKnownPosition) continue;

            float distance = HorizontalDistance(owner.transform.position, intel.KnownPosition);
            if (distance <= NearbyDistance)
            {
                score += 12f;
            }

            if (intel.HasDirectSight && distance <= intel.WeaponRange + 1f)
            {
                score += 10f;
                AddReason(metric, CombatAiReasonCode.EnemyLineOfSight);
                AddReason(metric, CombatAiReasonCode.EnemyInRange);
            }
        }

        if (score > 45f)
        {
            AddReason(metric, CombatAiReasonCode.SelfThreatHigh);
        }

        metric.Value = ClampMetric(score);
        return metric;
    }

    private static CombatAiMetric EvaluateAllyFragility(CombatAiContext context)
    {
        var metric = CreateMetric("AllyFragility");
        float score = 0f;
        for (int i = 0; i < context.AllyIntel.Count; i++)
        {
            CombatCharacterIntel ally = context.AllyIntel[i];
            if (ally.MaxHP <= 0) continue;

            float hpRatio = (float)ally.HP / ally.MaxHP;
            if (hpRatio <= LowHpThreshold)
            {
                score += 22f;
                AddReason(metric, CombatAiReasonCode.AllyLowHp);
            }

            if (HasEnemyNearby(context.EnemyIntel, ally.CurrentPosition, NearbyDistance))
            {
                score += 10f;
                AddReason(metric, CombatAiReasonCode.AllyFrontline);
            }
        }

        if (score > 35f)
        {
            AddReason(metric, CombatAiReasonCode.AllyFragilityHigh);
        }

        metric.Value = ClampMetric(score);
        return metric;
    }

    private static CombatAiMetric EvaluateReachableEnemyValue(CombatAiContext context)
    {
        var metric = CreateMetric("ReachableEnemyValue");
        Character owner = context.Owner;
        if (owner == null) return metric;

        float best = 0f;
        for (int i = 0; i < context.EnemyIntel.Count; i++)
        {
            CombatCharacterIntel enemy = context.EnemyIntel[i];
            if (!enemy.HasKnownPosition) continue;

            float score = 0f;
            float distance = HorizontalDistance(owner.transform.position, enemy.KnownPosition);
            float hpRatio = enemy.MaxHP > 0 ? (float)enemy.HP / enemy.MaxHP : 1f;

            score += (1f - hpRatio) * 38f;
            if (hpRatio <= LowHpThreshold)
            {
                AddReason(metric, CombatAiReasonCode.EnemyLowHp);
            }

            if (enemy.HasDirectSight)
            {
                score += 16f;
                AddReason(metric, CombatAiReasonCode.VisibleEnemy);
                AddReason(metric, CombatAiReasonCode.EnemyLineOfSight);
            }
            else if (enemy.HasMemory)
            {
                score += 6f;
                AddReason(metric, CombatAiReasonCode.RememberedEnemy);
            }

            if (distance <= enemy.WeaponRange + 1f || distance <= (owner.EquippedWeapon != null ? owner.EquippedWeapon.Range + 1f : 2f))
            {
                score += 18f;
                AddReason(metric, CombatAiReasonCode.EnemyInRange);
            }

            score += Mathf.Lerp(18f, 0f, Mathf.Clamp01(distance / 20f));
            best = Mathf.Max(best, score);
        }

        if (best > 40f)
        {
            AddReason(metric, CombatAiReasonCode.ReachableEnemyHigh);
        }

        metric.Value = ClampMetric(best);
        return metric;
    }

    private static CombatAiMetric EvaluateEnemyStoneReachability(CombatAiContext context)
    {
        var metric = CreateMetric("EnemyStoneReachability");
        if (!context.HasEnemyStonePosition || context.Owner == null)
        {
            return metric;
        }

        float distance = HorizontalDistance(context.Owner.transform.position, context.EnemyStonePosition);
        float score = Mathf.Lerp(70f, 0f, Mathf.Clamp01(distance / 60f));
        AddReason(metric, CombatAiReasonCode.EnemyStoneKnown);
        if (score > 35f)
        {
            AddReason(metric, CombatAiReasonCode.EnemyStoneReachable);
        }

        metric.Value = ClampMetric(score);
        return metric;
    }

    private static CombatAiMetric EvaluateTerrainAdvantage(CombatAiContext context)
    {
        var metric = CreateMetric("TerrainAdvantage");
        Character owner = context.Owner;
        if (owner == null) return metric;

        float score = 0f;
        if (NearestDistance(owner.transform.position, context.HighGroundCandidates) <= 12f)
        {
            score += 28f;
            AddReason(metric, CombatAiReasonCode.HighGroundAvailable);
        }

        if (NearestDistance(owner.transform.position, context.ForestCandidates) <= 10f)
        {
            score += 14f;
            AddReason(metric, CombatAiReasonCode.ForestAvailable);
        }

        if (context.Weather == CombatMapSystem.Weather.Sunny)
        {
            score += 8f;
            AddReason(metric, CombatAiReasonCode.WeatherBonus);
        }
        else if (context.Weather == CombatMapSystem.Weather.Rainy || context.Weather == CombatMapSystem.Weather.Cold)
        {
            score -= 6f;
            AddReason(metric, CombatAiReasonCode.WeatherPenalty);
        }

        if (score > 20f)
        {
            AddReason(metric, CombatAiReasonCode.TerrainAdvantageHigh);
        }

        metric.Value = ClampMetric(score);
        return metric;
    }

    private static CombatAiMetric EvaluateEnemyLocationConfidence(CombatAiContext context)
    {
        var metric = CreateMetric("EnemyLocationConfidence");
        float score = context.VisibleEnemies.Count * 30f + context.RememberedEnemies.Count * 10f;
        if (context.VisibleEnemies.Count > 0)
        {
            AddReason(metric, CombatAiReasonCode.VisibleEnemy);
        }
        else if (context.RememberedEnemies.Count > 0)
        {
            AddReason(metric, CombatAiReasonCode.RememberedEnemy);
        }
        else
        {
            AddReason(metric, CombatAiReasonCode.EnemyLocationUncertain);
        }

        metric.Value = ClampMetric(score);
        return metric;
    }

    private static CombatAiMetric EvaluateRetreatRouteSafety(CombatAiContext context)
    {
        var metric = CreateMetric("RetreatRouteSafety");
        Character owner = context.Owner;
        if (owner == null) return metric;

        float score = 0f;
        if (context.HasOwnStonePosition)
        {
            score += 30f;
            AddReason(metric, CombatAiReasonCode.OwnStoneKnown);
        }

        if (NearestDistance(owner.transform.position, context.ForestCandidates) <= 10f)
        {
            score += 20f;
            AddReason(metric, CombatAiReasonCode.ForestAvailable);
        }

        int nearbyEnemies = CountEnemiesNear(context.EnemyIntel, owner.transform.position, 12f);
        score -= nearbyEnemies * 8f;
        AddReason(metric, nearbyEnemies <= 1 ? CombatAiReasonCode.RetreatRouteSafe : CombatAiReasonCode.RetreatRouteUnsafe);
        metric.Value = ClampMetric(score);
        return metric;
    }

    private static CombatAiMetric CreateMetric(string code)
    {
        return new CombatAiMetric
        {
            Code = code,
            Label = CombatAiDebugLabels.Metric(code),
            Value = 0f,
        };
    }

    private static void AddReason(CombatAiMetric metric, CombatAiReasonCode reason)
    {
        if (!metric.ReasonCodes.Contains(reason))
        {
            metric.ReasonCodes.Add(reason);
        }
    }

    private static float ClampMetric(float value)
    {
        return Mathf.Clamp(value, 0f, MaxMetricValue);
    }

    private static bool HasEnemyNearby(IReadOnlyList<CombatCharacterIntel> enemies, Vector3 position, float radius)
    {
        for (int i = 0; i < enemies.Count; i++)
        {
            if (!enemies[i].HasKnownPosition) continue;
            if (HorizontalDistance(position, enemies[i].KnownPosition) <= radius)
            {
                return true;
            }
        }

        return false;
    }

    private static int CountEnemiesNear(IReadOnlyList<CombatCharacterIntel> enemies, Vector3 position, float radius)
    {
        int count = 0;
        for (int i = 0; i < enemies.Count; i++)
        {
            if (!enemies[i].HasKnownPosition) continue;
            if (HorizontalDistance(position, enemies[i].KnownPosition) <= radius)
            {
                count++;
            }
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
