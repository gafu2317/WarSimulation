public static class CombatAiFocusTargeting
{
    private const float SwordFocusObjectiveBonus = 20f;
    private const float SwordFocusMoveBonus = 30f;
    private const float SwordFocusSkillBonus = 25f;
    private const float SwordFocusTargetScoreBonus = 20f;

    public static float GetObjectiveScore(
        CombatAiContext context,
        WeaponBase weapon,
        CombatObjective objective,
        Character focusEnemy,
        float focusCommitmentRemainingSeconds,
        CombatObjective previousObjective)
    {
        if (!HasFocusCommitment(context, weapon, focusEnemy, focusCommitmentRemainingSeconds))
        {
            return 0f;
        }

        return objective switch
        {
            CombatObjective.AttackEnemy => SwordFocusObjectiveBonus,
            CombatObjective.DestroyEnemyStone when previousObjective == CombatObjective.AttackEnemy => -SwordFocusObjectiveBonus,
            CombatObjective.Search when previousObjective == CombatObjective.AttackEnemy => -SwordFocusObjectiveBonus,
            _ => 0f,
        };
    }

    public static float GetMoveScore(
        CombatAiContext context,
        WeaponBase weapon,
        string code,
        CombatMoveTarget target,
        Character focusEnemy,
        float focusCommitmentRemainingSeconds)
    {
        if (!HasFocusCommitment(context, weapon, focusEnemy, focusCommitmentRemainingSeconds))
        {
            return 0f;
        }

        if (code == CombatAiMoveCode.PursueEnemy &&
            target.Kind == CombatMoveTargetKind.Character &&
            target.TargetCharacter == focusEnemy)
        {
            return SwordFocusMoveBonus;
        }

        if (code == CombatAiMoveCode.AdvanceEnemyStone || code == CombatAiMoveCode.SearchLastKnown)
        {
            return -SwordFocusMoveBonus * 0.5f;
        }

        return 0f;
    }

    public static float GetSkillScore(
        CombatAiContext context,
        WeaponBase weapon,
        SkillBase skill,
        CombatSkillEvaluationResult evaluation,
        Character focusEnemy,
        float focusCommitmentRemainingSeconds)
    {
        if (!HasFocusCommitment(context, weapon, focusEnemy, focusCommitmentRemainingSeconds))
        {
            return 0f;
        }

        if (!evaluation.CanUse || !CombatAiSkillClassifier.IsDamage(skill))
        {
            return 0f;
        }

        return evaluation.Context.PrimaryTarget == focusEnemy ? SwordFocusSkillBonus : 0f;
    }

    public static float GetSelectionBonus(
        CombatAiContext context,
        Character candidate,
        float focusCommitmentRemainingSeconds)
    {
        WeaponBase weapon = context != null && context.Owner != null ? context.Owner.EquippedWeapon : null;
        return HasFocusCommitment(context, weapon, candidate, focusCommitmentRemainingSeconds)
            ? SwordFocusTargetScoreBonus
            : 0f;
    }

    public static bool IsValid(CombatAiContext context, Character focusEnemy)
    {
        if (context == null || focusEnemy == null) return false;

        CombatCharacterIntel intel = context.FindEnemyIntel(focusEnemy);
        return intel.Character != null &&
            intel.HasKnownPosition &&
            intel.HP > 0;
    }

    private static bool HasFocusCommitment(
        CombatAiContext context,
        WeaponBase weapon,
        Character focusEnemy,
        float focusCommitmentRemainingSeconds)
    {
        bool sword = weapon != null && weapon.Kind == WeaponKind.Sword;
        bool battleJunkie = context != null &&
            context.Owner != null &&
            context.Owner.PersonalityProfile != null &&
            context.Owner.PersonalityProfile.Kind == CombatAiPersonalityKind.BattleJunkie;
        return (sword || battleJunkie) &&
            focusCommitmentRemainingSeconds > 0f &&
            IsValid(context, focusEnemy);
    }

}
