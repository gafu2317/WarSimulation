using UnityEngine;

public static class CombatAiPersonalityBehavior
{
    public const float LonelyNearbyAllyRadius = 5f;
    public const float DevotedAssistRadius = 12f;
    public const float AttentionCrowdSampleRadius = 8f;

    private const float ForcedSelectionScore = 1000f;
    private const float ForcedRejectionScore = -200f;

    public static float GetObjectiveScore(
        CombatAiContext context,
        CombatAiPersonalityProfile profile,
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
        if (profile != null)
        {
            score += profile.Kind switch
            {
                CombatAiPersonalityKind.BattleJunkie when objective == CombatObjective.AttackEnemy => ForcedSelectionScore,
                CombatAiPersonalityKind.BattleJunkie when objective == CombatObjective.DestroyEnemyStone && !HasAttackTarget(context) => ForcedSelectionScore,
                CombatAiPersonalityKind.BattleJunkie => ForcedRejectionScore,
                CombatAiPersonalityKind.Cunning when objective == CombatObjective.DestroyEnemyStone => 160f,
                CombatAiPersonalityKind.Cunning when objective == CombatObjective.AttackEnemy => -36f,
                CombatAiPersonalityKind.Devoted when objective == CombatObjective.SupportAlly => 72f,
                CombatAiPersonalityKind.Devoted when objective == CombatObjective.AttackEnemy => -20f,
                CombatAiPersonalityKind.Lonely when objective == CombatObjective.Search && !HasNearbyAlly(context, LonelyNearbyAllyRadius) => ForcedSelectionScore,
                CombatAiPersonalityKind.Lonely when !HasNearbyAlly(context, LonelyNearbyAllyRadius) => ForcedRejectionScore,
                CombatAiPersonalityKind.Lonely when objective == CombatObjective.SupportAlly => 40f,
                CombatAiPersonalityKind.Reckless when objective == CombatObjective.DestroyEnemyStone => ForcedSelectionScore,
                CombatAiPersonalityKind.Reckless => ForcedRejectionScore,
                _ => 0f,
            };
        }

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
            CombatAiMoveCode.MoveForest => profile.Caution * 8f,
            CombatAiMoveCode.SearchLastKnown => profile.ExplorationBias * 8f + (objective == CombatObjective.Search ? 4f : 0f),
            _ => 0f,
        };
        if (code == CombatAiMoveCode.PersonalitySignature)
        {
            score += profile.Kind switch
            {
                CombatAiPersonalityKind.AttentionSeeker => 130f,
                CombatAiPersonalityKind.BattleJunkie => 96f,
                CombatAiPersonalityKind.Cunning => 90f,
                CombatAiPersonalityKind.Devoted => 140f,
                CombatAiPersonalityKind.Lonely => 136f,
                CombatAiPersonalityKind.Reckless => ForcedSelectionScore,
                _ => 0f,
            };
        }

        score += profile.Kind switch
        {
            CombatAiPersonalityKind.BattleJunkie when code == CombatAiMoveCode.PursueEnemy => 72f,
            CombatAiPersonalityKind.BattleJunkie when (code == CombatAiMoveCode.AdvanceEnemyStone || code == CombatAiMoveCode.AdvanceViaBridge) && objective != CombatObjective.DestroyEnemyStone => -80f,
            CombatAiPersonalityKind.Cunning when code == CombatAiMoveCode.AdvanceViaBridge => 72f,
            CombatAiPersonalityKind.Cunning when code == CombatAiMoveCode.AdvanceEnemyStone => 56f,
            CombatAiPersonalityKind.Cunning when code == CombatAiMoveCode.PursueEnemy => -48f,
            CombatAiPersonalityKind.Devoted when code == CombatAiMoveCode.SupportAlly => 56f,
            CombatAiPersonalityKind.Lonely when code == CombatAiMoveCode.SupportAlly => 40f,
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

        score += profile.Kind switch
        {
            CombatAiPersonalityKind.BattleJunkie when damage => 48f,
            CombatAiPersonalityKind.Cunning when damage && objective == CombatObjective.DestroyEnemyStone => 28f,
            CombatAiPersonalityKind.Devoted when support => 48f,
            CombatAiPersonalityKind.Lonely when support => 18f,
            CombatAiPersonalityKind.Reckless when damage => 24f,
            _ => 0f,
        };
        return score;
    }

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

    private static bool HasAttackTarget(CombatAiContext context)
    {
        if (context == null) return false;

        for (int i = 0; i < context.EnemyIntel.Count; i++)
        {
            CombatCharacterIntel enemy = context.EnemyIntel[i];
            if (!enemy.IsAlive || !enemy.HasKnownPosition) continue;

            int pendingDamage = 0;
            for (int j = 0; j < context.AllyPendingDamage.Count; j++)
            {
                CombatAiPendingDamage pending = context.AllyPendingDamage[j];
                if (pending.Target == enemy.Character) pendingDamage += pending.Damage;
            }

            if (enemy.HP - pendingDamage > 0) return true;
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
