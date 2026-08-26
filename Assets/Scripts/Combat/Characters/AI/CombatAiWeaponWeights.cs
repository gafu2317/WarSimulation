public static class CombatAiWeaponWeights
{
    public static float GetObjectiveWeight(WeaponKind kind, CombatObjective objective)
    {
        return kind switch
        {
            WeaponKind.Sword => objective switch
            {
                CombatObjective.AttackEnemy => 24f,
                CombatObjective.DestroyEnemyStone => 36f,
                CombatObjective.DefendOwnStone => 4f,
                CombatObjective.Search => -14f,
                CombatObjective.SupportAlly => -14f,
                _ => 0f,
            },
            WeaponKind.Shield => objective switch
            {
                CombatObjective.DefendOwnStone => 28f,
                CombatObjective.SupportAlly => 24f,
                CombatObjective.AttackEnemy => 8f,
                CombatObjective.DestroyEnemyStone => 4f,
                CombatObjective.Search => -12f,
                _ => 0f,
            },
            WeaponKind.Wand => objective switch
            {
                CombatObjective.AttackEnemy => 24f,
                CombatObjective.DestroyEnemyStone => 36f,
                CombatObjective.Search => -8f,
                CombatObjective.Retreat => 8f,
                CombatObjective.SupportAlly => -12f,
                CombatObjective.DefendOwnStone => -6f,
                _ => 0f,
            },
            WeaponKind.Grimoire => objective switch
            {
                CombatObjective.AttackEnemy => 22f,
                CombatObjective.DestroyEnemyStone => 8f,
                CombatObjective.Search => 10f,
                CombatObjective.Retreat => 8f,
                CombatObjective.SupportAlly => -6f,
                CombatObjective.DefendOwnStone => -6f,
                _ => 0f,
            },
            WeaponKind.Bible => objective switch
            {
                CombatObjective.SupportAlly => 28f,
                CombatObjective.DefendOwnStone => 20f,
                CombatObjective.AttackEnemy => 2f,
                CombatObjective.DestroyEnemyStone => -4f,
                CombatObjective.Retreat => 6f,
                _ => 0f,
            },
            WeaponKind.Rosary => objective switch
            {
                CombatObjective.SupportAlly => 32f,
                CombatObjective.Retreat => 16f,
                CombatObjective.DefendOwnStone => 12f,
                CombatObjective.AttackEnemy => -4f,
                CombatObjective.DestroyEnemyStone => -16f,
                CombatObjective.Search => 2f,
                _ => 0f,
            },
            _ => 0f,
        };
    }

    public static float GetMoveWeight(WeaponKind kind, string moveCode)
    {
        if (moveCode == CombatAiMoveCode.InterceptThreat)
        {
            return kind == WeaponKind.Shield ? 28f : -40f;
        }

        return kind switch
        {
            WeaponKind.Sword => moveCode switch
            {
                CombatAiMoveCode.PursueEnemy => 22f,
                CombatAiMoveCode.AdvanceEnemyStone => 16f,
                _ => 0f,
            },
            WeaponKind.Shield => moveCode switch
            {
                CombatAiMoveCode.ReturnOwnStone => 22f,
                CombatAiMoveCode.SupportAlly => 24f,
                CombatAiMoveCode.PursueEnemy => 4f,
                _ => 0f,
            },
            WeaponKind.Wand => moveCode switch
            {
                CombatAiMoveCode.TakeHighGround => 20f,
                CombatAiMoveCode.MoveForest => 12f,
                CombatAiMoveCode.PursueEnemy => -10f,
                CombatAiMoveCode.AdvanceEnemyStone => 16f,
                _ => 0f,
            },
            WeaponKind.Grimoire => moveCode switch
            {
                CombatAiMoveCode.TakeHighGround => 42f,
                CombatAiMoveCode.MoveForest => 12f,
                CombatAiMoveCode.PursueEnemy => -4f,
                CombatAiMoveCode.AdvanceEnemyStone => 4f,
                CombatAiMoveCode.SearchLastKnown => 8f,
                _ => 0f,
            },
            WeaponKind.Bible => moveCode switch
            {
                CombatAiMoveCode.SupportAlly => 24f,
                CombatAiMoveCode.ReturnOwnStone => 12f,
                CombatAiMoveCode.MoveForest => 8f,
                CombatAiMoveCode.TakeHighGround => 40f,
                _ => 0f,
            },
            WeaponKind.Rosary => moveCode switch
            {
                CombatAiMoveCode.SupportAlly => 26f,
                CombatAiMoveCode.ReturnOwnStone => 8f,
                CombatAiMoveCode.MoveForest => 10f,
                CombatAiMoveCode.PursueEnemy => -12f,
                CombatAiMoveCode.TakeHighGround => 20f,
                _ => 0f,
            },
            _ => 0f,
        };
    }

    public static float GetSkillWeight(WeaponKind kind, SkillBase skill)
    {
        if (CombatAiSkillClassifier.IsDamage(skill))
        {
            return kind switch
            {
                WeaponKind.Sword => 18f,
                WeaponKind.Shield => 4f,
                WeaponKind.Wand => 20f,
                WeaponKind.Grimoire => 8f,
                WeaponKind.Rosary => -6f,
                _ => 0f,
            };
        }

        if (CombatAiSkillClassifier.IsProtect(skill))
        {
            return kind switch
            {
                WeaponKind.Sword => -8f,
                WeaponKind.Shield => 24f,
                WeaponKind.Wand => -10f,
                WeaponKind.Bible => 24f,
                WeaponKind.Rosary => 10f,
                _ => 0f,
            };
        }

        if (CombatAiSkillClassifier.IsHeal(skill))
        {
            return kind switch
            {
                WeaponKind.Sword => -8f,
                WeaponKind.Wand => -10f,
                WeaponKind.Rosary => 28f,
                _ => 0f,
            };
        }

        if (CombatAiSkillClassifier.IsBuff(skill))
        {
            return kind switch
            {
                WeaponKind.Sword => -8f,
                WeaponKind.Bible => 24f,
                _ => 0f,
            };
        }

        if (CombatAiSkillClassifier.IsDebuff(skill))
        {
            return kind switch
            {
                WeaponKind.Sword => -8f,
                WeaponKind.Grimoire => 24f,
                _ => 0f,
            };
        }

        if (CombatAiSkillClassifier.IsStealth(skill))
        {
            return kind switch
            {
                WeaponKind.Sword => -8f,
                WeaponKind.Grimoire => 12f,
                _ => 0f,
            };
        }

        return 0f;
    }
}
