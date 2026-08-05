using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "CombatAiPersonalityProfile", menuName = "WarSimulation/Combat/AI Personality Profile")]
public sealed class CombatAiPersonalityProfile : ScriptableObject
{
    [SerializeField] private string _displayNameJapanese = "性格";
    [SerializeField] private CombatAiPersonalityKind _kind;
    [SerializeField] private float _aggression;
    [SerializeField] private float _caution;
    [SerializeField] private float _supportBias;
    [SerializeField] private float _objectiveFocus;
    [SerializeField] private float _explorationBias;
    [SerializeField] private float _riskTolerance;
    [SerializeField] private float _preferredRangeBias;

    public string DisplayNameJapanese => _displayNameJapanese;
    public CombatAiPersonalityKind Kind => _kind;
    public float Aggression => _aggression;
    public float Caution => _caution;
    public float SupportBias => _supportBias;
    public float ObjectiveFocus => _objectiveFocus;
    public float ExplorationBias => _explorationBias;
    public float RiskTolerance => _riskTolerance;
    public float PreferredRangeBias => _preferredRangeBias;

    public static List<CombatAiPersonalityProfile> CreateBuiltInProfiles()
    {
        var profiles = new List<CombatAiPersonalityProfile>();
        for (int value = (int)CombatAiPersonalityKind.Neutral;
             value <= (int)CombatAiPersonalityKind.Unstable;
             value++)
        {
            CombatAiPersonalityKind kind = (CombatAiPersonalityKind)value;
            profiles.Add(CreateBuiltInProfile(kind));
        }

        return profiles;
    }

    public static CombatAiPersonalityProfile CreateBuiltInProfile(CombatAiPersonalityKind kind)
    {
        CombatAiPersonalityProfile profile = CreateInstance<CombatAiPersonalityProfile>();
        profile.hideFlags = HideFlags.DontSave;
        profile._kind = kind;
        profile._displayNameJapanese = GetDisplayNameJapanese(kind);

        switch (kind)
        {
            case CombatAiPersonalityKind.AttentionSeeker:
                profile._aggression = 0.6f;
                profile._riskTolerance = 0.8f;
                break;
            case CombatAiPersonalityKind.BattleJunkie:
                profile._aggression = 1f;
                profile._riskTolerance = 0.7f;
                break;
            case CombatAiPersonalityKind.Calm:
                profile._caution = 0.5f;
                break;
            case CombatAiPersonalityKind.Cautious:
                profile._caution = 1f;
                profile._riskTolerance = -0.6f;
                break;
            case CombatAiPersonalityKind.Clumsy:
                profile._explorationBias = 0.3f;
                profile._riskTolerance = 0.4f;
                break;
            case CombatAiPersonalityKind.Coward:
                profile._caution = 1f;
                profile._riskTolerance = -1f;
                break;
            case CombatAiPersonalityKind.Cunning:
                profile._caution = 0.5f;
                profile._preferredRangeBias = 0.5f;
                break;
            case CombatAiPersonalityKind.Despicable:
                profile._caution = 0.8f;
                profile._supportBias = 0.3f;
                break;
            case CombatAiPersonalityKind.Devoted:
                profile._supportBias = 1f;
                profile._riskTolerance = 0.5f;
                break;
            case CombatAiPersonalityKind.Eccentric:
                profile._explorationBias = 0.8f;
                profile._riskTolerance = 0.5f;
                break;
            case CombatAiPersonalityKind.Gossiper:
                profile._supportBias = 0.4f;
                profile._explorationBias = 0.5f;
                break;
            case CombatAiPersonalityKind.HotBlooded:
                profile._aggression = 0.7f;
                profile._supportBias = 0.4f;
                break;
            case CombatAiPersonalityKind.Innocent:
                profile._explorationBias = 0.6f;
                profile._riskTolerance = 0.8f;
                break;
            case CombatAiPersonalityKind.Lazy:
                profile._caution = 0.2f;
                break;
            case CombatAiPersonalityKind.Lecherous:
                profile._aggression = 0.3f;
                profile._supportBias = 0.5f;
                break;
            case CombatAiPersonalityKind.Lonely:
                profile._supportBias = 0.8f;
                break;
            case CombatAiPersonalityKind.LoneWolf:
                profile._aggression = 0.5f;
                profile._explorationBias = 0.4f;
                break;
            case CombatAiPersonalityKind.OverlySerious:
                profile._objectiveFocus = 0.4f;
                break;
            case CombatAiPersonalityKind.Reckless:
                profile._objectiveFocus = 1f;
                profile._riskTolerance = 1f;
                break;
            case CombatAiPersonalityKind.Unstable:
                profile._aggression = 0.5f;
                profile._riskTolerance = 0.6f;
                break;
        }

        return profile;
    }

    public static string GetDisplayNameJapanese(CombatAiPersonalityKind kind)
    {
        return kind switch
        {
            CombatAiPersonalityKind.AttentionSeeker => "目立ちたがり屋",
            CombatAiPersonalityKind.BattleJunkie => "戦闘狂",
            CombatAiPersonalityKind.Calm => "冷静",
            CombatAiPersonalityKind.Cautious => "慎重",
            CombatAiPersonalityKind.Clumsy => "おっちょこちょい",
            CombatAiPersonalityKind.Coward => "臆病者",
            CombatAiPersonalityKind.Cunning => "狡猾",
            CombatAiPersonalityKind.Despicable => "卑怯者",
            CombatAiPersonalityKind.Devoted => "献身的",
            CombatAiPersonalityKind.Eccentric => "不思議ちゃん",
            CombatAiPersonalityKind.Gossiper => "下世話",
            CombatAiPersonalityKind.HotBlooded => "熱血",
            CombatAiPersonalityKind.Innocent => "天真爛漫",
            CombatAiPersonalityKind.Lazy => "怠け者",
            CombatAiPersonalityKind.Lecherous => "スケベ",
            CombatAiPersonalityKind.Lonely => "寂しがり",
            CombatAiPersonalityKind.LoneWolf => "一匹狼",
            CombatAiPersonalityKind.OverlySerious => "クソ真面目",
            CombatAiPersonalityKind.Reckless => "猪突猛進",
            CombatAiPersonalityKind.Unstable => "メンヘラ",
            _ => "標準",
        };
    }
}
