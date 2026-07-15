using System.Collections.Generic;
using UnityEngine;

public static class CombatAiMetricIndex
{
    public const int OwnStoneThreat = 0;
    public const int SelfThreat = 1;
    public const int AllyFragility = 2;
    public const int ReachableEnemyValue = 3;
    public const int EnemyStoneReachability = 4;
    public const int TerrainAdvantage = 5;
    public const int EnemyLocationConfidence = 6;
    public const int RetreatRouteSafety = 7;
    public const int SelfExposure = 8;
    public const int EnemyThreatLevel = 9;
    public const int KillableTargetValue = 10;
    public const int WinProximity = 11;
    public const int Count = 12;
}

public sealed class CombatAiAssessment
{
    private readonly float[] _values = new float[CombatAiMetricIndex.Count];

    public float GetValue(int index) => _values[index];

    internal void SetValue(int index, float value) => _values[index] = value;
}

public sealed class CombatAiScoreBreakdown
{
    public float BaseScore { get; set; }
    public float WeaponScore { get; set; }
    public float PersonalityScore { get; set; }
    public float SituationScore { get; set; }
    private List<CombatAiReasonCode> _reasonCodes;
    public List<CombatAiReasonCode> ReasonCodes => _reasonCodes ??= new List<CombatAiReasonCode>();
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
    public CombatAiAssessment Assessment { get; set; }
    public List<CombatAiObjectiveScoreEntry> ObjectiveEntries { get; } = new();
    public CombatAiObjectiveScoreEntry SelectedObjective { get; set; }
    public List<CombatAiMoveCandidateEntry> MoveEntries { get; } = new();
    public CombatAiMoveCandidateEntry SelectedMove { get; set; }
    public List<CombatAiSkillCandidateEntry> SkillEntries { get; } = new();
    public CombatAiSkillCandidateEntry SelectedSkill { get; set; }
}
