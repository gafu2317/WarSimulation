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

    public static string Metric(string code)
    {
        return code switch
        {
            "OwnStoneThreat" => Format("OwnStoneThreat", "自軍魔石脅威"),
            "SelfThreat" => Format("SelfThreat", "自己脅威"),
            "AllyFragility" => Format("AllyFragility", "味方脆弱性"),
            "ReachableEnemyValue" => Format("ReachableEnemyValue", "到達可能敵価値"),
            "EnemyStoneReachability" => Format("EnemyStoneReachability", "敵魔石到達性"),
            "TerrainAdvantage" => Format("TerrainAdvantage", "地形有利"),
            "EnemyLocationConfidence" => Format("EnemyLocationConfidence", "敵位置確信度"),
            "RetreatRouteSafety" => Format("RetreatRouteSafety", "撤退路安全性"),
            "SelfExposure" => Format("SelfExposure", "自己露出度"),
            "EnemyThreatLevel" => Format("EnemyThreatLevel", "敵脅威度"),
            "KillableTargetValue" => Format("KillableTargetValue", "倒し切り価値"),
            "WinProximity" => Format("WinProximity", "勝利接近度"),
            _ => code,
        };
    }

    public static string Reason(CombatAiReasonCode reason)
    {
        return reason switch
        {
            CombatAiReasonCode.VisibleEnemy => Format(nameof(CombatAiReasonCode.VisibleEnemy), "敵を視認中"),
            CombatAiReasonCode.RememberedEnemy => Format(nameof(CombatAiReasonCode.RememberedEnemy), "敵を記憶中"),
            CombatAiReasonCode.EnemyLowHp => Format(nameof(CombatAiReasonCode.EnemyLowHp), "敵HP低い"),
            CombatAiReasonCode.EnemyInRange => Format(nameof(CombatAiReasonCode.EnemyInRange), "敵が射程内"),
            CombatAiReasonCode.EnemyLineOfSight => Format(nameof(CombatAiReasonCode.EnemyLineOfSight), "射線あり"),
            CombatAiReasonCode.EnemyNearOwnStone => Format(nameof(CombatAiReasonCode.EnemyNearOwnStone), "敵が自軍魔石に近い"),
            CombatAiReasonCode.OwnHpLow => Format(nameof(CombatAiReasonCode.OwnHpLow), "自分のHP低い"),
            CombatAiReasonCode.AllyLowHp => Format(nameof(CombatAiReasonCode.AllyLowHp), "味方HP低い"),
            CombatAiReasonCode.AllyFrontline => Format(nameof(CombatAiReasonCode.AllyFrontline), "味方前線維持中"),
            CombatAiReasonCode.EnemyStoneKnown => Format(nameof(CombatAiReasonCode.EnemyStoneKnown), "敵魔石位置既知"),
            CombatAiReasonCode.OwnStoneKnown => Format(nameof(CombatAiReasonCode.OwnStoneKnown), "自軍魔石位置既知"),
            CombatAiReasonCode.HighGroundAvailable => Format(nameof(CombatAiReasonCode.HighGroundAvailable), "高所有効"),
            CombatAiReasonCode.ForestAvailable => Format(nameof(CombatAiReasonCode.ForestAvailable), "森林利用可"),
            CombatAiReasonCode.RetreatRouteSafe => Format(nameof(CombatAiReasonCode.RetreatRouteSafe), "安全な撤退路あり"),
            CombatAiReasonCode.WeatherPenalty => Format(nameof(CombatAiReasonCode.WeatherPenalty), "天候不利"),
            CombatAiReasonCode.WeatherBonus => Format(nameof(CombatAiReasonCode.WeatherBonus), "天候有利"),
            CombatAiReasonCode.WeaponPreference => Format(nameof(CombatAiReasonCode.WeaponPreference), "武器傾向"),
            CombatAiReasonCode.PersonalityPreference => Format(nameof(CombatAiReasonCode.PersonalityPreference), "性格傾向"),
            CombatAiReasonCode.SkillReady => Format(nameof(CombatAiReasonCode.SkillReady), "スキル使用可能"),
            CombatAiReasonCode.SkillMatchesObjective => Format(nameof(CombatAiReasonCode.SkillMatchesObjective), "目的適合"),
            CombatAiReasonCode.SkillAreaHitsMultiple => Format(nameof(CombatAiReasonCode.SkillAreaHitsMultiple), "範囲対象複数"),
            CombatAiReasonCode.TargetInSkillRange => Format(nameof(CombatAiReasonCode.TargetInSkillRange), "スキル射程内"),
            CombatAiReasonCode.TargetOutOfRange => Format(nameof(CombatAiReasonCode.TargetOutOfRange), "スキル射程外"),
            CombatAiReasonCode.TargetInvalid => Format(nameof(CombatAiReasonCode.TargetInvalid), "対象不正"),
            CombatAiReasonCode.OwnStoneThreatHigh => Format(nameof(CombatAiReasonCode.OwnStoneThreatHigh), "自軍魔石脅威高い"),
            CombatAiReasonCode.SelfThreatHigh => Format(nameof(CombatAiReasonCode.SelfThreatHigh), "自己脅威高い"),
            CombatAiReasonCode.AllyFragilityHigh => Format(nameof(CombatAiReasonCode.AllyFragilityHigh), "味方脆弱性高い"),
            CombatAiReasonCode.ReachableEnemyHigh => Format(nameof(CombatAiReasonCode.ReachableEnemyHigh), "攻撃価値高い敵あり"),
            CombatAiReasonCode.EnemyLocationUncertain => Format(nameof(CombatAiReasonCode.EnemyLocationUncertain), "敵位置不確実"),
            CombatAiReasonCode.EnemyStoneReachable => Format(nameof(CombatAiReasonCode.EnemyStoneReachable), "敵魔石へ前進しやすい"),
            CombatAiReasonCode.TerrainAdvantageHigh => Format(nameof(CombatAiReasonCode.TerrainAdvantageHigh), "地形有利高い"),
            CombatAiReasonCode.RetreatRouteUnsafe => Format(nameof(CombatAiReasonCode.RetreatRouteUnsafe), "撤退路不安"),
            CombatAiReasonCode.SelfExposedByEnemy => Format(nameof(CombatAiReasonCode.SelfExposedByEnemy), "敵に露出中"),
            CombatAiReasonCode.EnemyThreatHigh => Format(nameof(CombatAiReasonCode.EnemyThreatHigh), "敵脅威高い"),
            CombatAiReasonCode.KillableTargetHigh => Format(nameof(CombatAiReasonCode.KillableTargetHigh), "倒し切れる敵あり"),
            CombatAiReasonCode.EnemyUnableToAct => Format(nameof(CombatAiReasonCode.EnemyUnableToAct), "敵が行動不能"),
            CombatAiReasonCode.WinProximityHigh => Format(nameof(CombatAiReasonCode.WinProximityHigh), "敵魔石の残りHPが少ない"),
            CombatAiReasonCode.RouteRiskHigh => Format(nameof(CombatAiReasonCode.RouteRiskHigh), "移動経路の危険度が高い"),
            CombatAiReasonCode.BodyBlockValuable => Format(nameof(CombatAiReasonCode.BodyBlockValuable), "遮断で味方や魔石を守れる"),
            CombatAiReasonCode.IncomingEnemyCast => Format(nameof(CombatAiReasonCode.IncomingEnemyCast), "敵の詠唱攻撃を受ける見込み"),
            CombatAiReasonCode.NumericalAdvantage => Format(nameof(CombatAiReasonCode.NumericalAdvantage), "数的優勢"),
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
            WeaponKind.Rosary => "ロザリオ",
            WeaponKind.Unarmed => "素手",
            _ => weapon.Kind.ToString(),
        };
    }
}
