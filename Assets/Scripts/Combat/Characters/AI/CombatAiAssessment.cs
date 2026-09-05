public static class CombatAiMetricIndex
{
    public const int OwnStoneThreat = 0;
    public const int AllyFragility = 1;
    public const int Count = 2;
}

public sealed class CombatAiAssessment
{
    private readonly float[] _values = new float[CombatAiMetricIndex.Count];

    public float GetValue(int index) => _values[index];

    internal void SetValue(int index, float value) => _values[index] = value;
}

public sealed class CombatAiDebugSnapshot
{
    public Character Owner { get; set; }
    public CombatAiContext Context { get; set; }
    public CombatAiAssessment Assessment { get; set; }
    public CombatObjective PreviousState { get; set; }
    public CombatAiPlan Plan { get; set; }
    public CombatObjective SelectedState => Plan.Objective;
    public CombatAiReasonCode TransitionReason => Plan.TransitionReason;
    public string ActionCode => Plan.ActionCode;
}
