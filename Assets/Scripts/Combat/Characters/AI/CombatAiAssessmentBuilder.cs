using System.Collections.Generic;
using UnityEngine;

public static class CombatAiAssessmentBuilder
{
    private const float MaxMetricValue = 100f;
    private const float NearbyDistance = 8f;
    private const float LowHpThreshold = 0.4f;

    public static CombatAiContextSummary BuildSummary(
        CombatAiContext context,
        CombatAiPersonalityProfile personalityProfile,
        CombatAiWeaponWeightsProfile weaponWeightsProfile)
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
            if (context.EnemyIntel[i].IsAlive && context.EnemyIntel[i].HasKnownPosition)
            {
                knownEnemies++;
            }
        }

        return new CombatAiContextSummary
        {
            VisibleEnemyCount = CountLivingCharacters(context.VisibleEnemies),
            RememberedEnemyCount = CountLivingCharacters(context.RememberedEnemies),
            KnownEnemyCount = knownEnemies,
            AllyCount = context.Allies.Count,
            LowHpAllyCount = lowHpAllies,
            WeatherLabel = CombatAiDebugLabels.Format(context.Weather.ToString(), context.Weather.ToString()),
            WeaponLabel = CombatAiDebugLabels.Weapon(context.Owner != null ? context.Owner.EquippedWeapon : null),
            PersonalityLabel = CombatAiDebugLabels.Personality(personalityProfile),
            WeaponWeightsLabel = weaponWeightsProfile != null
                ? "WeaponWeights: " + weaponWeightsProfile.name
                : "WeaponWeights: Code Defaults",
        };
    }

    public static CombatAiAssessment Build(CombatAiContext context, bool captureDebug = true)
    {
        var assessment = new CombatAiAssessment();

        var m0 = EvaluateOwnStoneThreat(context, captureDebug, out float v0);
        assessment.SetValue(CombatAiMetricIndex.OwnStoneThreat, v0);
        if (captureDebug) assessment.Metrics.Add(m0);

        var m1 = EvaluateSelfThreat(context, captureDebug, out float v1);
        assessment.SetValue(CombatAiMetricIndex.SelfThreat, v1);
        if (captureDebug) assessment.Metrics.Add(m1);

        var m2 = EvaluateAllyFragility(context, captureDebug, out float v2);
        assessment.SetValue(CombatAiMetricIndex.AllyFragility, v2);
        if (captureDebug) assessment.Metrics.Add(m2);

        var m3 = EvaluateReachableEnemyValue(context, captureDebug, out float v3);
        assessment.SetValue(CombatAiMetricIndex.ReachableEnemyValue, v3);
        if (captureDebug) assessment.Metrics.Add(m3);

        var m4 = EvaluateEnemyStoneReachability(context, captureDebug, out float v4);
        assessment.SetValue(CombatAiMetricIndex.EnemyStoneReachability, v4);
        if (captureDebug) assessment.Metrics.Add(m4);

        var m5 = EvaluateTerrainAdvantage(context, captureDebug, out float v5);
        assessment.SetValue(CombatAiMetricIndex.TerrainAdvantage, v5);
        if (captureDebug) assessment.Metrics.Add(m5);

        var m6 = EvaluateEnemyLocationConfidence(context, captureDebug, out float v6);
        assessment.SetValue(CombatAiMetricIndex.EnemyLocationConfidence, v6);
        if (captureDebug) assessment.Metrics.Add(m6);

        var m7 = EvaluateRetreatRouteSafety(context, captureDebug, out float v7);
        assessment.SetValue(CombatAiMetricIndex.RetreatRouteSafety, v7);
        if (captureDebug) assessment.Metrics.Add(m7);

        var m8 = EvaluateSelfExposure(context, captureDebug, out float v8);
        assessment.SetValue(CombatAiMetricIndex.SelfExposure, v8);
        if (captureDebug) assessment.Metrics.Add(m8);

        var m9 = EvaluateEnemyThreatLevel(context, captureDebug, out float v9);
        assessment.SetValue(CombatAiMetricIndex.EnemyThreatLevel, v9);
        if (captureDebug) assessment.Metrics.Add(m9);

        var m10 = EvaluateKillableTargetValue(context, captureDebug, out float v10);
        assessment.SetValue(CombatAiMetricIndex.KillableTargetValue, v10);
        if (captureDebug) assessment.Metrics.Add(m10);

        var m11 = EvaluateWinProximity(context, captureDebug, out float v11);
        assessment.SetValue(CombatAiMetricIndex.WinProximity, v11);
        if (captureDebug) assessment.Metrics.Add(m11);

        return assessment;
    }

    private static CombatAiMetric EvaluateWinProximity(CombatAiContext context, bool captureDebug, out float value)
    {
        CombatAiMetric metric = captureDebug ? CreateMetric("WinProximity") : null;
        value = context.HasEnemyStoneHealth && context.EnemyStoneMaxHP > 0
            ? ClampMetric((1f - context.EnemyStoneHP / (float)context.EnemyStoneMaxHP) * MaxMetricValue)
            : 0f;
        if (value >= 60f)
        {
            AddReason(metric, CombatAiReasonCode.WinProximityHigh);
        }

        if (metric != null) metric.Value = value;
        return metric;
    }

    private static CombatAiMetric EvaluateOwnStoneThreat(CombatAiContext context, bool captureDebug, out float value)
    {
        CombatAiMetric metric = captureDebug ? CreateMetric("OwnStoneThreat") : null;
        if (!context.HasOwnStonePosition)
        {
            AddReason(metric, CombatAiReasonCode.EnemyLocationUncertain);
            value = 0f;
            return metric;
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
        value = ClampMetric(score);
        if (metric != null) metric.Value = value;
        return metric;
    }

    private static CombatAiMetric EvaluateSelfThreat(CombatAiContext context, bool captureDebug, out float value)
    {
        CombatAiMetric metric = captureDebug ? CreateMetric("SelfThreat") : null;
        Character owner = context.Owner;
        if (owner == null || owner.Health == null || owner.Health.MaxHP <= 0)
        {
            value = 0f;
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
            if (!intel.IsAlive || !intel.HasKnownPosition) continue;

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

        int incomingDamage = GetPendingDamage(context.EnemyPendingDamage, owner);
        if (incomingDamage > 0)
        {
            score += Mathf.Clamp(incomingDamage / (float)owner.Health.MaxHP * 60f, 0f, 60f);
            AddReason(metric, CombatAiReasonCode.IncomingEnemyCast);
        }

        int nearbyEnemies = CountActiveCharactersNear(context.EnemyIntel, owner.transform.position, 10f, true);
        int nearbyAllies = CountActiveCharactersNear(context.AllyIntel, owner.transform.position, 10f, false) + 1;
        if (nearbyEnemies > nearbyAllies)
        {
            score += Mathf.Min(36f, (nearbyEnemies - nearbyAllies) * 12f);
        }

        if (score > 45f)
        {
            AddReason(metric, CombatAiReasonCode.SelfThreatHigh);
        }

        value = ClampMetric(score);
        if (metric != null) metric.Value = value;
        return metric;
    }

    private static CombatAiMetric EvaluateAllyFragility(CombatAiContext context, bool captureDebug, out float value)
    {
        CombatAiMetric metric = captureDebug ? CreateMetric("AllyFragility") : null;
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

            int nearbyEnemyCount = CountKnownEnemiesNear(context.EnemyIntel, ally.CurrentPosition, NearbyDistance);
            if (nearbyEnemyCount > 0)
            {
                score += Mathf.Min(30f, nearbyEnemyCount * 10f);
                AddReason(metric, CombatAiReasonCode.AllyFrontline);
            }

            int incomingDamage = GetPendingDamage(context.EnemyPendingDamage, ally.Character);

            score += Mathf.Clamp(incomingDamage / (float)ally.MaxHP * 40f, 0f, 40f);
        }

        if (score > 35f)
        {
            AddReason(metric, CombatAiReasonCode.AllyFragilityHigh);
        }

        value = ClampMetric(score);
        if (metric != null) metric.Value = value;
        return metric;
    }

    private static CombatAiMetric EvaluateReachableEnemyValue(CombatAiContext context, bool captureDebug, out float value)
    {
        CombatAiMetric metric = captureDebug ? CreateMetric("ReachableEnemyValue") : null;
        Character owner = context.Owner;
        if (owner == null)
        {
            value = 0f;
            return metric;
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

        value = ClampMetric(best);
        if (metric != null) metric.Value = value;
        return metric;
    }

    private static CombatAiMetric EvaluateEnemyStoneReachability(CombatAiContext context, bool captureDebug, out float value)
    {
        CombatAiMetric metric = captureDebug ? CreateMetric("EnemyStoneReachability") : null;
        if (!context.HasEnemyStonePosition || context.Owner == null)
        {
            value = 0f;
            return metric;
        }

        float distance = HorizontalDistance(context.Owner.transform.position, context.EnemyStonePosition);
        float score = Mathf.Lerp(70f, 0f, Mathf.Clamp01(distance / 60f));
        AddReason(metric, CombatAiReasonCode.EnemyStoneKnown);
        if (score > 35f)
        {
            AddReason(metric, CombatAiReasonCode.EnemyStoneReachable);
        }

        value = ClampMetric(score);
        if (metric != null) metric.Value = value;
        return metric;
    }

    private static CombatAiMetric EvaluateTerrainAdvantage(CombatAiContext context, bool captureDebug, out float value)
    {
        CombatAiMetric metric = captureDebug ? CreateMetric("TerrainAdvantage") : null;
        Character owner = context.Owner;
        if (owner == null)
        {
            value = 0f;
            return metric;
        }

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

        value = ClampMetric(score);
        if (metric != null) metric.Value = value;
        return metric;
    }

    private static CombatAiMetric EvaluateEnemyLocationConfidence(CombatAiContext context, bool captureDebug, out float value)
    {
        CombatAiMetric metric = captureDebug ? CreateMetric("EnemyLocationConfidence") : null;
        int visibleEnemies = CountLivingCharacters(context.VisibleEnemies);
        int rememberedEnemies = CountLivingCharacters(context.RememberedEnemies);
        float score = visibleEnemies * 30f + rememberedEnemies * 10f;
        if (visibleEnemies > 0)
        {
            AddReason(metric, CombatAiReasonCode.VisibleEnemy);
        }
        else if (rememberedEnemies > 0)
        {
            AddReason(metric, CombatAiReasonCode.RememberedEnemy);
        }
        else
        {
            AddReason(metric, CombatAiReasonCode.EnemyLocationUncertain);
        }

        value = ClampMetric(score);
        if (metric != null) metric.Value = value;
        return metric;
    }

    private static CombatAiMetric EvaluateRetreatRouteSafety(CombatAiContext context, bool captureDebug, out float value)
    {
        CombatAiMetric metric = captureDebug ? CreateMetric("RetreatRouteSafety") : null;
        Character owner = context.Owner;
        if (owner == null)
        {
            value = 0f;
            return metric;
        }

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
        value = ClampMetric(score);
        if (metric != null) metric.Value = value;
        return metric;
    }

    private static CombatAiMetric EvaluateSelfExposure(CombatAiContext context, bool captureDebug, out float value)
    {
        CombatAiMetric metric = captureDebug ? CreateMetric("SelfExposure") : null;
        Character owner = context.Owner;
        if (owner == null)
        {
            value = 0f;
            return metric;
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
                AddReason(metric, CombatAiReasonCode.EnemyLineOfSight);
            }
        }

        if (score > 20f)
        {
            AddReason(metric, CombatAiReasonCode.SelfExposedByEnemy);
        }

        value = ClampMetric(score);
        if (metric != null) metric.Value = value;
        return metric;
    }

    private static CombatAiMetric EvaluateEnemyThreatLevel(CombatAiContext context, bool captureDebug, out float value)
    {
        CombatAiMetric metric = captureDebug ? CreateMetric("EnemyThreatLevel") : null;
        Character owner = context.Owner;
        if (owner == null)
        {
            value = 0f;
            return metric;
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

            if (enemy.HasDirectSight)
            {
                AddReason(metric, CombatAiReasonCode.VisibleEnemy);
            }
            else if (enemy.HasMemory)
            {
                AddReason(metric, CombatAiReasonCode.RememberedEnemy);
            }

            if (threat >= 0.5f)
            {
                AddReason(metric, CombatAiReasonCode.EnemyThreatHigh);
            }
        }

        float combinedThreat = Mathf.Clamp01(highestThreat + otherThreatTotal * 0.3f);
        value = combinedThreat * MaxMetricValue;
        if (metric != null) metric.Value = value;
        return metric;
    }

    private static CombatAiMetric EvaluateKillableTargetValue(CombatAiContext context, bool captureDebug, out float value)
    {
        CombatAiMetric metric = captureDebug ? CreateMetric("KillableTargetValue") : null;
        Character owner = context.Owner;
        if (owner == null)
        {
            value = 0f;
            return metric;
        }

        float best = 0f;
        for (int i = 0; i < context.EnemyIntel.Count; i++)
        {
            CombatCharacterIntel enemy = context.EnemyIntel[i];
            if (!enemy.HasKnownPosition || enemy.HP <= 0) continue;

            int predictedHp = enemy.HP - GetAllyPendingDamage(context, enemy.Character);
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

            if (finishEase >= 0.7f)
            {
                AddReason(metric, CombatAiReasonCode.EnemyLowHp);
            }

            if (!enemy.CanAct)
            {
                AddReason(metric, CombatAiReasonCode.EnemyUnableToAct);
            }
        }

        value = best * MaxMetricValue;
        if (value > 35f)
        {
            AddReason(metric, CombatAiReasonCode.KillableTargetHigh);
        }

        if (metric != null) metric.Value = value;
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
        if (metric == null) return;
        if (!metric.ReasonCodes.Contains(reason))
        {
            metric.ReasonCodes.Add(reason);
        }
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

    private static float GetFinishEase(CombatCharacterIntel enemy)
    {
        return GetFinishEase(enemy, enemy.HP);
    }

    private static float GetFinishEase(CombatCharacterIntel enemy, int hp)
    {
        float hpRatio = enemy.MaxHP > 0 ? hp / (float)enemy.MaxHP : 1f;
        if (hpRatio <= 0.3f) return 1f;
        if (hpRatio <= 0.5f) return 0.7f;
        if (hpRatio <= 0.7f) return 0.35f;
        return 0.1f;
    }

    private static int GetAllyPendingDamage(CombatAiContext context, Character target)
    {
        int damage = 0;
        for (int i = 0; i < context.AllyPendingDamage.Count; i++)
        {
            if (context.AllyPendingDamage[i].Target == target)
            {
                damage += context.AllyPendingDamage[i].Damage;
            }
        }

        return damage;
    }

    private static int GetPendingDamage(IReadOnlyList<CombatAiPendingDamage> pendingDamage, Character target)
    {
        int damage = 0;
        for (int i = 0; i < pendingDamage.Count; i++)
        {
            if (pendingDamage[i].Target == target)
            {
                damage += pendingDamage[i].Damage;
            }
        }

        return damage;
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

    private static bool HasEnemyNearby(IReadOnlyList<CombatCharacterIntel> enemies, Vector3 position, float radius)
    {
        for (int i = 0; i < enemies.Count; i++)
        {
            if (!enemies[i].IsAlive || !enemies[i].HasKnownPosition) continue;
            if (HorizontalDistance(position, enemies[i].KnownPosition) <= radius)
            {
                return true;
            }
        }

        return false;
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

    private static int CountLivingCharacters(IReadOnlyList<Character> characters)
    {
        int count = 0;
        for (int i = 0; i < characters.Count; i++)
        {
            Character character = characters[i];
            if (character != null && character.Health != null && character.Health.IsAlive) count++;
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
