using UnityEngine;

[CreateAssetMenu(fileName = "CombatAiPersonalityProfile", menuName = "WarSimulation/Combat/AI Personality Profile")]
public sealed class CombatAiPersonalityProfile : ScriptableObject
{
    [SerializeField] private string _displayNameJapanese = "性格";
    [SerializeField] private float _aggression;
    [SerializeField] private float _caution;
    [SerializeField] private float _supportBias;
    [SerializeField] private float _objectiveFocus;
    [SerializeField] private float _explorationBias;
    [SerializeField] private float _riskTolerance;
    [SerializeField] private float _preferredRangeBias;

    public string DisplayNameJapanese => _displayNameJapanese;
    public float Aggression => _aggression;
    public float Caution => _caution;
    public float SupportBias => _supportBias;
    public float ObjectiveFocus => _objectiveFocus;
    public float ExplorationBias => _explorationBias;
    public float RiskTolerance => _riskTolerance;
    public float PreferredRangeBias => _preferredRangeBias;
}
