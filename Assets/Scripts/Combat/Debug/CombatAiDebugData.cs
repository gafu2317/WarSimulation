using System.Collections.Generic;
using UnityEngine;

public enum CombatAiDebugReasonCode
{
    None = 0,
    VisibleEnemy = 1,
    RememberedEnemy = 2,
    EnemyLowHp = 3,
    EnemyInRange = 4,
    EnemyLineOfSight = 5,
    EnemyNearOwnStone = 6,
    OwnHpLow = 7,
    AllyLowHp = 8,
    AllyFrontline = 9,
    EnemyStoneKnown = 10,
    OwnStoneKnown = 11,
    HighGroundAvailable = 12,
    ForestAvailable = 13,
    RetreatRouteSafe = 14,
    WeatherPenalty = 15,
    WeatherBonus = 16,
    WeaponPreference = 17,
    PersonalityPreference = 18,
    SkillReady = 19,
    SkillMatchesObjective = 20,
    SkillAreaHitsMultiple = 21,
    TargetInSkillRange = 22,
    TargetOutOfRange = 23,
    TargetInvalid = 24,
    OwnStoneThreatHigh = 25,
    SelfThreatHigh = 26,
    AllyFragilityHigh = 27,
    ReachableEnemyHigh = 28,
    EnemyLocationUncertain = 29,
    EnemyStoneReachable = 30,
    TerrainAdvantageHigh = 31,
    RetreatRouteUnsafe = 32,
}

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
            CombatObjective.DefeatEnemy => Format(nameof(CombatObjective.DefeatEnemy), "敵を撃破"),
            CombatObjective.SupportAlly => Format(nameof(CombatObjective.SupportAlly), "味方を援護"),
            CombatObjective.Search => Format(nameof(CombatObjective.Search), "索敵"),
            CombatObjective.Retreat => Format(nameof(CombatObjective.Retreat), "撤退"),
            _ => Format(objective.ToString(), objective.ToString()),
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
            _ => code,
        };
    }

    public static string Reason(CombatAiDebugReasonCode reason)
    {
        return reason switch
        {
            CombatAiDebugReasonCode.VisibleEnemy => Format(nameof(CombatAiDebugReasonCode.VisibleEnemy), "敵を視認中"),
            CombatAiDebugReasonCode.RememberedEnemy => Format(nameof(CombatAiDebugReasonCode.RememberedEnemy), "敵を記憶中"),
            CombatAiDebugReasonCode.EnemyLowHp => Format(nameof(CombatAiDebugReasonCode.EnemyLowHp), "敵HP低い"),
            CombatAiDebugReasonCode.EnemyInRange => Format(nameof(CombatAiDebugReasonCode.EnemyInRange), "敵が射程内"),
            CombatAiDebugReasonCode.EnemyLineOfSight => Format(nameof(CombatAiDebugReasonCode.EnemyLineOfSight), "射線あり"),
            CombatAiDebugReasonCode.EnemyNearOwnStone => Format(nameof(CombatAiDebugReasonCode.EnemyNearOwnStone), "敵が自軍魔石に近い"),
            CombatAiDebugReasonCode.OwnHpLow => Format(nameof(CombatAiDebugReasonCode.OwnHpLow), "自分のHP低い"),
            CombatAiDebugReasonCode.AllyLowHp => Format(nameof(CombatAiDebugReasonCode.AllyLowHp), "味方HP低い"),
            CombatAiDebugReasonCode.AllyFrontline => Format(nameof(CombatAiDebugReasonCode.AllyFrontline), "味方前線維持中"),
            CombatAiDebugReasonCode.EnemyStoneKnown => Format(nameof(CombatAiDebugReasonCode.EnemyStoneKnown), "敵魔石位置既知"),
            CombatAiDebugReasonCode.OwnStoneKnown => Format(nameof(CombatAiDebugReasonCode.OwnStoneKnown), "自軍魔石位置既知"),
            CombatAiDebugReasonCode.HighGroundAvailable => Format(nameof(CombatAiDebugReasonCode.HighGroundAvailable), "高所有効"),
            CombatAiDebugReasonCode.ForestAvailable => Format(nameof(CombatAiDebugReasonCode.ForestAvailable), "森林利用可"),
            CombatAiDebugReasonCode.RetreatRouteSafe => Format(nameof(CombatAiDebugReasonCode.RetreatRouteSafe), "安全な撤退路あり"),
            CombatAiDebugReasonCode.WeatherPenalty => Format(nameof(CombatAiDebugReasonCode.WeatherPenalty), "天候不利"),
            CombatAiDebugReasonCode.WeatherBonus => Format(nameof(CombatAiDebugReasonCode.WeatherBonus), "天候有利"),
            CombatAiDebugReasonCode.WeaponPreference => Format(nameof(CombatAiDebugReasonCode.WeaponPreference), "武器傾向"),
            CombatAiDebugReasonCode.PersonalityPreference => Format(nameof(CombatAiDebugReasonCode.PersonalityPreference), "性格傾向"),
            CombatAiDebugReasonCode.SkillReady => Format(nameof(CombatAiDebugReasonCode.SkillReady), "スキル使用可能"),
            CombatAiDebugReasonCode.SkillMatchesObjective => Format(nameof(CombatAiDebugReasonCode.SkillMatchesObjective), "目的適合"),
            CombatAiDebugReasonCode.SkillAreaHitsMultiple => Format(nameof(CombatAiDebugReasonCode.SkillAreaHitsMultiple), "範囲対象複数"),
            CombatAiDebugReasonCode.TargetInSkillRange => Format(nameof(CombatAiDebugReasonCode.TargetInSkillRange), "スキル射程内"),
            CombatAiDebugReasonCode.TargetOutOfRange => Format(nameof(CombatAiDebugReasonCode.TargetOutOfRange), "スキル射程外"),
            CombatAiDebugReasonCode.TargetInvalid => Format(nameof(CombatAiDebugReasonCode.TargetInvalid), "対象不正"),
            CombatAiDebugReasonCode.OwnStoneThreatHigh => Format(nameof(CombatAiDebugReasonCode.OwnStoneThreatHigh), "自軍魔石脅威高い"),
            CombatAiDebugReasonCode.SelfThreatHigh => Format(nameof(CombatAiDebugReasonCode.SelfThreatHigh), "自己脅威高い"),
            CombatAiDebugReasonCode.AllyFragilityHigh => Format(nameof(CombatAiDebugReasonCode.AllyFragilityHigh), "味方脆弱性高い"),
            CombatAiDebugReasonCode.ReachableEnemyHigh => Format(nameof(CombatAiDebugReasonCode.ReachableEnemyHigh), "攻撃価値高い敵あり"),
            CombatAiDebugReasonCode.EnemyLocationUncertain => Format(nameof(CombatAiDebugReasonCode.EnemyLocationUncertain), "敵位置不確実"),
            CombatAiDebugReasonCode.EnemyStoneReachable => Format(nameof(CombatAiDebugReasonCode.EnemyStoneReachable), "敵魔石へ前進しやすい"),
            CombatAiDebugReasonCode.TerrainAdvantageHigh => Format(nameof(CombatAiDebugReasonCode.TerrainAdvantageHigh), "地形有利高い"),
            CombatAiDebugReasonCode.RetreatRouteUnsafe => Format(nameof(CombatAiDebugReasonCode.RetreatRouteUnsafe), "撤退路不安"),
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

    public static string Personality(PersonalityBase personality)
    {
        if (personality == null) return Format("None", "なし");
        return Format(personality.GetType().Name, personality.GetType().Name);
    }

    public static string Weapon(WeaponBase weapon)
    {
        if (weapon == null) return Format("Unarmed", "素手");
        return Format(weapon.Kind.ToString(), weapon.Kind.ToString());
    }
}

public sealed class CombatAiDebugContextSummary
{
    public int VisibleEnemyCount { get; set; }
    public int RememberedEnemyCount { get; set; }
    public int AllyCount { get; set; }
    public int LowHpAllyCount { get; set; }
    public string WeatherLabel { get; set; }
    public string WeaponLabel { get; set; }
    public string PersonalityLabel { get; set; }
}

public sealed class CombatAiDebugMetric
{
    public string Code { get; set; }
    public string Label { get; set; }
    public float Value { get; set; }
    public List<CombatAiDebugReasonCode> Reasons { get; } = new();
}

public sealed class CombatAiDebugAssessment
{
    public List<CombatAiDebugMetric> Metrics { get; } = new();

    public float GetValue(string code)
    {
        for (int i = 0; i < Metrics.Count; i++)
        {
            if (Metrics[i].Code == code) return Metrics[i].Value;
        }

        return 0f;
    }
}

public sealed class CombatAiDebugScoreBreakdown
{
    public float BaseScore { get; set; }
    public float WeaponScore { get; set; }
    public float PersonalityScore { get; set; }
    public float SituationScore { get; set; }
    public List<CombatAiDebugReasonCode> Reasons { get; } = new();
    public float Total => BaseScore + WeaponScore + PersonalityScore + SituationScore;
}

public sealed class CombatAiDebugObjectiveEntry
{
    public CombatObjective Objective { get; set; }
    public string Label { get; set; }
    public CombatAiDebugScoreBreakdown Breakdown { get; set; }
}

public sealed class CombatAiDebugMoveEntry
{
    public string Code { get; set; }
    public string Label { get; set; }
    public CombatMoveTarget Target { get; set; }
    public CombatAiDebugScoreBreakdown Breakdown { get; set; }
}

public sealed class CombatAiDebugSkillEntry
{
    public string Code { get; set; }
    public string Label { get; set; }
    public SkillBase Skill { get; set; }
    public SkillExecutionContext SkillContext { get; set; }
    public CombatSkillEvaluationResult Evaluation { get; set; }
    public CombatAiDebugScoreBreakdown Breakdown { get; set; }
}

public sealed class CombatAiDebugSnapshot
{
    public Character Owner { get; set; }
    public CombatAiContext Context { get; set; }
    public CombatAiDebugContextSummary Summary { get; set; }
    public CombatAiDebugAssessment Assessment { get; set; }
    public List<CombatAiDebugObjectiveEntry> ObjectiveEntries { get; } = new();
    public CombatAiDebugObjectiveEntry SelectedObjective { get; set; }
    public List<CombatAiDebugMoveEntry> MoveEntries { get; } = new();
    public CombatAiDebugMoveEntry SelectedMove { get; set; }
    public List<CombatAiDebugSkillEntry> SkillEntries { get; } = new();
    public CombatAiDebugSkillEntry SelectedSkill { get; set; }
    public CombatAiPlan FinalPlan { get; set; }
}
