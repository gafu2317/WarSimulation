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
            int choice = CombatBattleRandom.Choose(context.Owner, "EccentricObjective", CombatBattleRandom.GetInterval(3f), 6);
            score += objective == (CombatObjective)choice ? 160f : 0f;
        }
        else if (profile != null)
        {
            score += profile.Kind switch
            {
                CombatAiPersonalityKind.AttentionSeeker when objective == CombatObjective.AttackEnemy => 24f,
                CombatAiPersonalityKind.BattleJunkie when objective == CombatObjective.AttackEnemy => 48f,
                CombatAiPersonalityKind.BattleJunkie when objective == CombatObjective.Retreat || objective == CombatObjective.DestroyEnemyStone => -28f,
                CombatAiPersonalityKind.Calm when objective == CombatObjective.Retreat && selfThreat >= 42f => 64f,
                CombatAiPersonalityKind.Coward when objective == CombatObjective.Retreat && (selfThreat >= 18f || exposure >= 18f) => 58f,
                CombatAiPersonalityKind.Devoted when objective == CombatObjective.SupportAlly => 42f,
                CombatAiPersonalityKind.HotBlooded when objective == CombatObjective.AttackEnemy => 28f,
                CombatAiPersonalityKind.Innocent when objective == CombatObjective.AttackEnemy => -120f,
                CombatAiPersonalityKind.Lonely when objective == CombatObjective.SupportAlly => 36f,
                CombatAiPersonalityKind.LoneWolf when objective == CombatObjective.AttackEnemy => 22f,
                CombatAiPersonalityKind.Reckless when objective == CombatObjective.DestroyEnemyStone => ForcedSelectionScore,
                CombatAiPersonalityKind.Reckless => ForcedRejectionScore,
                _ => 0f,
            };
        }

        if (objective == CombatObjective.AttackEnemy && HasNearbyHotBloodedAlly(context)) score += 24f;
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
                CombatAiPersonalityKind.AttentionSeeker => 72f,
                CombatAiPersonalityKind.Clumsy => 110f,
                CombatAiPersonalityKind.Coward => 88f,
                CombatAiPersonalityKind.Cunning => 64f,
                CombatAiPersonalityKind.Despicable => 82f,
                CombatAiPersonalityKind.Devoted => 88f,
                CombatAiPersonalityKind.Eccentric => 70f,
                CombatAiPersonalityKind.Gossiper => 88f,
                CombatAiPersonalityKind.HotBlooded => 68f,
                CombatAiPersonalityKind.Innocent => 100f,
                CombatAiPersonalityKind.Lonely => 84f,
                CombatAiPersonalityKind.LoneWolf => 72f,
                CombatAiPersonalityKind.Lecherous => 84f,
                CombatAiPersonalityKind.OverlySerious => 66f,
                CombatAiPersonalityKind.Reckless => ForcedSelectionScore,
                _ => 0f,
            };
        }

        score += profile.Kind switch
        {
            CombatAiPersonalityKind.BattleJunkie when code == CombatAiMoveCode.PursueEnemy => 38f,
            CombatAiPersonalityKind.Cautious when code == CombatAiMoveCode.AdvanceViaBridge || code == CombatAiMoveCode.MoveForest => 28f,
            CombatAiPersonalityKind.Cunning when code == CombatAiMoveCode.MoveForest => 42f,
            CombatAiPersonalityKind.Lazy when code == CombatAiMoveCode.HoldPosition => 72f,
            CombatAiPersonalityKind.OverlySerious when code == CombatAiMoveCode.MoveForest => -120f,
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
            int interval = CombatBattleRandom.GetInterval(3f);
            int choice = CombatBattleRandom.Choose(owner, "EccentricSkill:" + skill.Name, interval, 4);
            score += choice == 0 ? 90f : 0f;
        }

        score += profile.Kind switch
        {
            CombatAiPersonalityKind.BattleJunkie when damage => 32f,
            CombatAiPersonalityKind.Devoted when support => 28f,
            CombatAiPersonalityKind.HotBlooded when damage => 20f,
            CombatAiPersonalityKind.Innocent when damage => -120f,
            CombatAiPersonalityKind.Lazy => -18f,
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
                Vector3.Distance(context.Owner.transform.position, ally.CurrentPosition) <= 6f)
            {
                return true;
            }
        }

        return false;
    }
}
