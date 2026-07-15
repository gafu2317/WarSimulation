using UnityEngine;

public static class CombatAiPersonalityBehavior
{
    public static float GetObjectiveScore(
        Character owner,
        CombatAiPersonalityProfile profile,
        CombatAiAssessment assessment,
        CombatObjective objective)
    {
        if (profile == null) return 0f;

        float selfThreat = assessment.GetValue(CombatAiMetricIndex.SelfThreat);
        float exposure = assessment.GetValue(CombatAiMetricIndex.SelfExposure);
        if (profile.Kind == CombatAiPersonalityKind.Eccentric)
        {
            int choice = CombatBattleRandom.Choose(owner, "EccentricObjective", CombatBattleRandom.GetInterval(3f), 6);
            return objective == (CombatObjective)choice ? 160f : 0f;
        }

        return profile.Kind switch
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
            CombatAiPersonalityKind.Reckless when objective == CombatObjective.DestroyEnemyStone => 1000f,
            CombatAiPersonalityKind.Reckless => -200f,
            _ => 0f,
        };
    }

    public static float GetMoveScore(CombatAiPersonalityProfile profile, string code)
    {
        if (profile == null) return 0f;

        if (code == CombatAiMoveCode.PersonalitySignature)
        {
            return profile.Kind switch
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
                CombatAiPersonalityKind.Reckless => 1000f,
                _ => 0f,
            };
        }

        return profile.Kind switch
        {
            CombatAiPersonalityKind.BattleJunkie when code == CombatAiMoveCode.PursueEnemy => 38f,
            CombatAiPersonalityKind.Cautious when code == CombatAiMoveCode.AdvanceViaBridge || code == CombatAiMoveCode.MoveForest => 28f,
            CombatAiPersonalityKind.Cunning when code == CombatAiMoveCode.MoveForest => 42f,
            CombatAiPersonalityKind.Lazy when code == CombatAiMoveCode.HoldPosition => 72f,
            CombatAiPersonalityKind.OverlySerious when code == CombatAiMoveCode.MoveForest => -120f,
            CombatAiPersonalityKind.Reckless when code == CombatAiMoveCode.AdvanceEnemyStone => 1000f,
            _ => 0f,
        };
    }

    public static float GetSkillScore(Character owner, CombatAiPersonalityProfile profile, SkillBase skill)
    {
        if (profile == null || skill == null) return 0f;

        bool damage = CombatAiSkillClassifier.IsDamage(skill);
        bool support = CombatAiSkillClassifier.IsSupport(skill);
        if (profile.Kind == CombatAiPersonalityKind.Eccentric)
        {
            int interval = CombatBattleRandom.GetInterval(3f);
            int choice = CombatBattleRandom.Choose(owner, "EccentricSkill:" + skill.Name, interval, 4);
            return choice == 0 ? 90f : 0f;
        }

        return profile.Kind switch
        {
            CombatAiPersonalityKind.BattleJunkie when damage => 32f,
            CombatAiPersonalityKind.Devoted when support => 28f,
            CombatAiPersonalityKind.HotBlooded when damage => 20f,
            CombatAiPersonalityKind.Innocent when damage => -120f,
            CombatAiPersonalityKind.Lazy => -18f,
            CombatAiPersonalityKind.Reckless when damage => 24f,
            _ => 0f,
        };
    }
}
