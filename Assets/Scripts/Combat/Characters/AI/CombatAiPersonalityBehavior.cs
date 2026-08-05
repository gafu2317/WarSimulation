using UnityEngine;

public static class CombatAiPersonalityBehavior
{
    private const float ForcedSelectionScore = 1000f;
    private const float ForcedRejectionScore = -200f;

    public static float GetObjectiveScore(
        CombatAiContext context,
        CombatAiPersonalityProfile profile,
        CombatAiAssessment assessment,
        CombatObjective objective)
    {
        float score = profile != null ? objective switch
        {
            CombatObjective.AttackEnemy => profile.Aggression * 20f - profile.Caution * 8f,
            CombatObjective.DefendOwnStone => profile.ObjectiveFocus * 16f + profile.Caution * 6f,
            CombatObjective.SupportAlly => profile.SupportBias * 20f + profile.Caution * 4f,
            CombatObjective.DestroyEnemyStone => profile.ObjectiveFocus * 18f + profile.RiskTolerance * 8f,
            CombatObjective.Search => profile.ExplorationBias * 18f,
            CombatObjective.Retreat => profile.Caution * 18f - profile.RiskTolerance * 6f,
            _ => 0f,
        } : 0f;
        float selfThreat = assessment.GetValue(CombatAiMetricIndex.SelfThreat);
        float exposure = assessment.GetValue(CombatAiMetricIndex.SelfExposure);
        if (profile != null && profile.Kind == CombatAiPersonalityKind.Eccentric)
        {
            int interval = CombatBattleRandom.GetDecisionInterval(context.Owner, 2.5f);
            int choice = CombatBattleRandom.Choose(context.Owner, "EccentricObjective", interval, 6);
            score += objective == (CombatObjective)choice ? 220f : -40f;
        }
        else if (profile != null)
        {
            score += profile.Kind switch
            {
                CombatAiPersonalityKind.AttentionSeeker when objective == CombatObjective.AttackEnemy => 48f,
                CombatAiPersonalityKind.BattleJunkie when objective == CombatObjective.AttackEnemy => 72f,
                CombatAiPersonalityKind.BattleJunkie when objective == CombatObjective.Retreat || objective == CombatObjective.DestroyEnemyStone => -48f,
                CombatAiPersonalityKind.Calm when objective == CombatObjective.Retreat && selfThreat >= 36f => 96f,
                CombatAiPersonalityKind.Calm when objective == CombatObjective.AttackEnemy && selfThreat >= 36f => -40f,
                CombatAiPersonalityKind.Cautious when objective == CombatObjective.Retreat && selfThreat >= 28f => 40f,
                CombatAiPersonalityKind.Cautious when objective == CombatObjective.AttackEnemy && exposure >= 30f => -24f,
                CombatAiPersonalityKind.Coward when objective == CombatObjective.Retreat && (selfThreat >= 14f || exposure >= 14f) => 88f,
                CombatAiPersonalityKind.Coward when objective == CombatObjective.AttackEnemy && selfThreat >= 14f => -36f,
                CombatAiPersonalityKind.Cunning when objective == CombatObjective.AttackEnemy => 18f,
                CombatAiPersonalityKind.Despicable when objective == CombatObjective.SupportAlly => 24f,
                CombatAiPersonalityKind.Despicable when objective == CombatObjective.Retreat && selfThreat >= 22f => 36f,
                CombatAiPersonalityKind.Devoted when objective == CombatObjective.SupportAlly => 64f,
                CombatAiPersonalityKind.Devoted when objective == CombatObjective.AttackEnemy => -12f,
                CombatAiPersonalityKind.HotBlooded when objective == CombatObjective.AttackEnemy => 40f,
                CombatAiPersonalityKind.Innocent when objective == CombatObjective.AttackEnemy => -120f,
                CombatAiPersonalityKind.Innocent when objective == CombatObjective.Search => 48f,
                CombatAiPersonalityKind.Lazy when objective == CombatObjective.Search => 28f,
                CombatAiPersonalityKind.Lonely when objective == CombatObjective.SupportAlly => 56f,
                CombatAiPersonalityKind.Lonely when objective == CombatObjective.AttackEnemy && !HasNearbyAlly(context, 5f) => -40f,
                CombatAiPersonalityKind.LoneWolf when objective == CombatObjective.AttackEnemy => 36f,
                CombatAiPersonalityKind.LoneWolf when objective == CombatObjective.SupportAlly => -28f,
                CombatAiPersonalityKind.OverlySerious when objective == CombatObjective.AttackEnemy => 28f,
                CombatAiPersonalityKind.OverlySerious when objective == CombatObjective.Search => -20f,
                CombatAiPersonalityKind.Reckless when objective == CombatObjective.DestroyEnemyStone => ForcedSelectionScore,
                CombatAiPersonalityKind.Reckless => ForcedRejectionScore,
                CombatAiPersonalityKind.Unstable when objective == CombatObjective.AttackEnemy => 16f,
                _ => 0f,
            };
        }

        if (objective == CombatObjective.AttackEnemy && HasNearbyHotBloodedAlly(context)) score += 36f;
        return score;
    }

    public static float GetMoveScore(
        CombatAiPersonalityProfile profile,
        string code,
        CombatObjective objective)
    {
        if (profile == null) return 0f;

        float score = code switch
        {
            CombatAiMoveCode.PursueEnemy => profile.Aggression * 14f - profile.Caution * 4f,
            CombatAiMoveCode.AdvanceEnemyStone => profile.ObjectiveFocus * 16f + profile.RiskTolerance * 6f,
            CombatAiMoveCode.AdvanceViaBridge => profile.ObjectiveFocus * 14f + profile.Caution * 6f,
            CombatAiMoveCode.ReturnOwnStone => profile.Caution * 10f,
            CombatAiMoveCode.SupportAlly => profile.SupportBias * 14f,
            CombatAiMoveCode.InterceptThreat => profile.SupportBias * 12f + profile.Caution * 4f,
            CombatAiMoveCode.TakeHighGround => profile.PreferredRangeBias * 10f,
            CombatAiMoveCode.MoveForest => profile.Caution * 8f,
            CombatAiMoveCode.SearchLastKnown => profile.ExplorationBias * 8f + (objective == CombatObjective.Search ? 4f : 0f),
            _ => 0f,
        };
        if (code == CombatAiMoveCode.PersonalitySignature)
        {
            score += profile.Kind switch
            {
                CombatAiPersonalityKind.AttentionSeeker => 120f,
                CombatAiPersonalityKind.BattleJunkie => 96f,
                CombatAiPersonalityKind.Calm => 130f,
                CombatAiPersonalityKind.Cautious => 110f,
                CombatAiPersonalityKind.Clumsy => 140f,
                CombatAiPersonalityKind.Coward => 130f,
                CombatAiPersonalityKind.Cunning => 118f,
                CombatAiPersonalityKind.Despicable => 124f,
                CombatAiPersonalityKind.Devoted => 132f,
                CombatAiPersonalityKind.Eccentric => 100f,
                CombatAiPersonalityKind.Gossiper => 140f,
                CombatAiPersonalityKind.HotBlooded => 112f,
                CombatAiPersonalityKind.Innocent => 150f,
                CombatAiPersonalityKind.Lonely => 136f,
                CombatAiPersonalityKind.LoneWolf => 118f,
                CombatAiPersonalityKind.Lecherous => 130f,
                CombatAiPersonalityKind.OverlySerious => 108f,
                CombatAiPersonalityKind.Reckless => ForcedSelectionScore,
                CombatAiPersonalityKind.Unstable => 160f,
                _ => 0f,
            };
        }

        score += profile.Kind switch
        {
            CombatAiPersonalityKind.BattleJunkie when code == CombatAiMoveCode.PursueEnemy => 52f,
            CombatAiPersonalityKind.BattleJunkie when code == CombatAiMoveCode.AdvanceEnemyStone => -36f,
            CombatAiPersonalityKind.Cautious when code == CombatAiMoveCode.AdvanceViaBridge || code == CombatAiMoveCode.MoveForest => 42f,
            CombatAiPersonalityKind.Cautious when code == CombatAiMoveCode.PursueEnemy => -18f,
            CombatAiPersonalityKind.Cunning when code == CombatAiMoveCode.MoveForest => 56f,
            CombatAiPersonalityKind.Devoted when code == CombatAiMoveCode.InterceptThreat || code == CombatAiMoveCode.SupportAlly => 36f,
            CombatAiPersonalityKind.HotBlooded when code == CombatAiMoveCode.PursueEnemy => 24f,
            CombatAiPersonalityKind.Lazy when code == CombatAiMoveCode.HoldPosition => 96f,
            CombatAiPersonalityKind.Lonely when code == CombatAiMoveCode.SupportAlly => 40f,
            CombatAiPersonalityKind.LoneWolf when code == CombatAiMoveCode.PursueEnemy => 28f,
            CombatAiPersonalityKind.OverlySerious when code == CombatAiMoveCode.MoveForest => -160f,
            CombatAiPersonalityKind.OverlySerious when code == CombatAiMoveCode.PursueEnemy => 34f,
            CombatAiPersonalityKind.Reckless when code == CombatAiMoveCode.AdvanceEnemyStone => ForcedSelectionScore,
            _ => 0f,
        };
        return score;
    }

    public static float GetSkillScore(
        Character owner,
        CombatAiPersonalityProfile profile,
        SkillBase skill,
        CombatObjective objective)
    {
        if (profile == null || skill == null) return 0f;

        bool damage = CombatAiSkillClassifier.IsDamage(skill);
        bool support = CombatAiSkillClassifier.IsSupport(skill);
        float score = damage
            ? profile.Aggression * 10f - profile.Caution * 2f
            : support
                ? profile.SupportBias * 12f + profile.Caution * 4f
                : CombatAiSkillClassifier.IsMobility(skill) && objective == CombatObjective.Search
                    ? profile.ExplorationBias * 10f
                    : 0f;
        if (profile.Kind == CombatAiPersonalityKind.Eccentric)
        {
            int interval = CombatBattleRandom.GetDecisionInterval(owner, 2.5f);
            int choice = CombatBattleRandom.Choose(owner, "EccentricSkill:" + skill.Name, interval, 3);
            score += choice == 0 ? 120f : -20f;
        }

        score += profile.Kind switch
        {
            CombatAiPersonalityKind.AttentionSeeker when damage => 18f,
            CombatAiPersonalityKind.BattleJunkie when damage => 40f,
            CombatAiPersonalityKind.Coward when damage => -16f,
            CombatAiPersonalityKind.Despicable when damage => -8f,
            CombatAiPersonalityKind.Devoted when support => 40f,
            CombatAiPersonalityKind.HotBlooded when damage => 28f,
            CombatAiPersonalityKind.Innocent when damage => -120f,
            CombatAiPersonalityKind.Lazy => -28f,
            CombatAiPersonalityKind.Lonely when support => 22f,
            CombatAiPersonalityKind.OverlySerious when damage => 18f,
            CombatAiPersonalityKind.Reckless when damage => 24f,
            _ => 0f,
        };
        return score;
    }

    private static bool HasNearbyHotBloodedAlly(CombatAiContext context)
    {
        if (context == null || context.Owner == null) return false;
        for (int i = 0; i < context.AllyIntel.Count; i++)
        {
            CombatCharacterIntel ally = context.AllyIntel[i];
            if (!ally.IsAlive || ally.Character == null || ally.Character.PersonalityProfile == null) continue;
            if (ally.Character.PersonalityProfile.Kind == CombatAiPersonalityKind.HotBlooded &&
                Vector3.Distance(context.Owner.transform.position, ally.CurrentPosition) <= 7f)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasNearbyAlly(CombatAiContext context, float radius)
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
}
