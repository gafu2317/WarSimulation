public static class CombatAiDebugLabels
{
    public static string Format(string code, string japanese)
    {
        return string.IsNullOrEmpty(japanese) ? code : code + "（" + japanese + "）";
    }

    public static string Objective(CombatObjective objective)
    {
        return objective switch
        {
            CombatObjective.DestroyEnemyStone => Format(nameof(CombatObjective.DestroyEnemyStone), "敵魔石を破壊"),
            CombatObjective.DefendOwnStone => Format(nameof(CombatObjective.DefendOwnStone), "自軍魔石を防衛"),
            CombatObjective.AttackEnemy => Format(nameof(CombatObjective.AttackEnemy), "敵を攻撃"),
            CombatObjective.SupportAlly => Format(nameof(CombatObjective.SupportAlly), "味方を援護"),
            CombatObjective.Search => Format(nameof(CombatObjective.Search), "索敵"),
            CombatObjective.Retreat => Format(nameof(CombatObjective.Retreat), "撤退"),
            _ => Format(objective.ToString(), objective.ToString()),
        };
    }

    public static string ObjectiveShort(CombatObjective objective)
    {
        return objective switch
        {
            CombatObjective.DestroyEnemyStone => "敵魔石を破壊",
            CombatObjective.DefendOwnStone => "自軍魔石を防衛",
            CombatObjective.AttackEnemy => "敵を攻撃",
            CombatObjective.SupportAlly => "味方を援護",
            CombatObjective.Search => "索敵",
            CombatObjective.Retreat => "撤退",
            _ => objective.ToString(),
        };
    }

    public static string MoveCode(string code, string japanese)
    {
        return Format(code, japanese);
    }

    public static string Reason(CombatAiReasonCode reason)
    {
        return reason switch
        {
            CombatAiReasonCode.EnemyInRange => Format(nameof(CombatAiReasonCode.EnemyInRange), "敵が射程内"),
            CombatAiReasonCode.EnemyStoneKnown => Format(nameof(CombatAiReasonCode.EnemyStoneKnown), "敵魔石位置既知"),
            CombatAiReasonCode.PersonalityPreference => Format(nameof(CombatAiReasonCode.PersonalityPreference), "性格傾向"),
            CombatAiReasonCode.OwnStoneThreatHigh => Format(nameof(CombatAiReasonCode.OwnStoneThreatHigh), "自軍魔石脅威高い"),
            CombatAiReasonCode.SelfThreatHigh => Format(nameof(CombatAiReasonCode.SelfThreatHigh), "自己脅威高い"),
            CombatAiReasonCode.AllyFragilityHigh => Format(nameof(CombatAiReasonCode.AllyFragilityHigh), "味方脆弱性高い"),
            CombatAiReasonCode.EnemyLocationUncertain => Format(nameof(CombatAiReasonCode.EnemyLocationUncertain), "敵位置不確実"),
            _ => reason.ToString(),
        };
    }

    public static string Skill(SkillBase skill)
    {
        if (skill == null) return Format("None", "なし");
        if (skill is IdentifiedSkill identified)
        {
            return Format(identified.SkillId.ToString(), skill.Name);
        }

        return Format(skill.GetType().Name, skill.Name);
    }

    public static string Personality(CombatAiPersonalityProfile profile)
    {
        if (profile == null) return Format("None", "なし");
        return Format(profile.name, profile.DisplayNameJapanese);
    }

    public static string PersonalityShort(CombatAiPersonalityProfile profile)
    {
        return profile != null ? profile.DisplayNameJapanese : "なし";
    }

    public static string Weapon(WeaponBase weapon)
    {
        if (weapon == null) return Format("Unarmed", "素手");
        return Format(weapon.Kind.ToString(), weapon.Kind.ToString());
    }

    public static string WeaponShort(WeaponBase weapon)
    {
        if (weapon == null) return "素手";
        return weapon.Kind switch
        {
            WeaponKind.Sword => "剣",
            WeaponKind.Shield => "盾",
            WeaponKind.Wand => "杖",
            WeaponKind.Grimoire => "魔導書",
            WeaponKind.Bible => "聖書",
            WeaponKind.Rosary => "ロ",
            WeaponKind.Unarmed => "素手",
            _ => weapon.Kind.ToString(),
        };
    }
}
