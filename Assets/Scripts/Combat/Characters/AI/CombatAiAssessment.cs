using System.Collections.Generic;
using UnityEngine;

public sealed class CombatAiContextSummary
{
    public int VisibleEnemyCount { get; set; }
    public int RememberedEnemyCount { get; set; }
    public int KnownEnemyCount { get; set; }
    public int AllyCount { get; set; }
    public int LowHpAllyCount { get; set; }
    public string WeatherLabel { get; set; }
    public string WeaponLabel { get; set; }
    public string PersonalityLabel { get; set; }
    public string WeaponWeightsLabel { get; set; }
}

public sealed class CombatAiMetric
{
    public string Code { get; set; }
    public string Label { get; set; }
    public float Value { get; set; }
    public List<CombatAiReasonCode> ReasonCodes { get; } = new();
}

public sealed class CombatAiAssessment
{
    public List<CombatAiMetric> Metrics { get; } = new();

    public float GetValue(string code)
    {
        for (int i = 0; i < Metrics.Count; i++)
        {
            if (Metrics[i].Code == code) return Metrics[i].Value;
        }

        return 0f;
    }
}

public sealed class CombatAiScoreBreakdown
{
    public float BaseScore { get; set; }
    public float WeaponScore { get; set; }
    public float PersonalityScore { get; set; }
    public float SituationScore { get; set; }
    public List<CombatAiReasonCode> ReasonCodes { get; } = new();
    public float Total => BaseScore + WeaponScore + PersonalityScore + SituationScore;
}

public sealed class CombatAiObjectiveScoreEntry
{
    public CombatObjective Objective { get; set; }
    public string Label { get; set; }
    public CombatAiScoreBreakdown Breakdown { get; set; }
}

public sealed class CombatAiMoveCandidateEntry
{
    public string Code { get; set; }
    public string Label { get; set; }
    public CombatMoveTarget Target { get; set; }
    public CombatAiScoreBreakdown Breakdown { get; set; }
}

public sealed class CombatAiSkillCandidateEntry
{
    public string Code { get; set; }
    public string Label { get; set; }
    public SkillBase Skill { get; set; }
    public SkillExecutionContext SkillContext { get; set; }
    public CombatSkillEvaluationResult Evaluation { get; set; }
    public CombatAiScoreBreakdown Breakdown { get; set; }
}

public sealed class CombatAiDebugSnapshot
{
    public Character Owner { get; set; }
    public CombatAiContext Context { get; set; }
    public CombatAiContextSummary ContextSummary { get; set; }
    public CombatAiAssessment Assessment { get; set; }
    public List<CombatAiObjectiveScoreEntry> ObjectiveEntries { get; } = new();
    public CombatAiObjectiveScoreEntry SelectedObjective { get; set; }
    public List<CombatAiMoveCandidateEntry> MoveEntries { get; } = new();
    public CombatAiMoveCandidateEntry SelectedMove { get; set; }
    public List<CombatAiSkillCandidateEntry> SkillEntries { get; } = new();
    public CombatAiSkillCandidateEntry SelectedSkill { get; set; }
    public CombatAiPlan FinalPlan { get; set; }
}
