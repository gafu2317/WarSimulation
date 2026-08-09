using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "CombatAiPersonalityProfile", menuName = "WarSimulation/Combat/AI Personality Profile")]
public sealed class CombatAiPersonalityProfile : ScriptableObject
{
    public static readonly CombatAiPersonalityKind[] BuiltInKinds =
    {
        CombatAiPersonalityKind.Neutral,
        CombatAiPersonalityKind.AttentionSeeker,
        CombatAiPersonalityKind.BattleJunkie,
        CombatAiPersonalityKind.Cunning,
        CombatAiPersonalityKind.Devoted,
        CombatAiPersonalityKind.Lonely,
        CombatAiPersonalityKind.Reckless,
    };

    [SerializeField] private string _displayNameJapanese = "性格";
    [SerializeField] private CombatAiPersonalityKind _kind;
    [SerializeField] private float _aggression;
    [SerializeField] private float _caution;
    [SerializeField] private float _supportBias;
    [SerializeField] private float _objectiveFocus;
    [SerializeField] private float _explorationBias;
    [SerializeField] private float _riskTolerance;

    public string DisplayNameJapanese => _displayNameJapanese;
    public CombatAiPersonalityKind Kind => _kind;
    public float Aggression => _aggression;
    public float Caution => _caution;
    public float SupportBias => _supportBias;
    public float ObjectiveFocus => _objectiveFocus;
    public float ExplorationBias => _explorationBias;
    public float RiskTolerance => _riskTolerance;

    public static List<CombatAiPersonalityProfile> CreateBuiltInProfiles()
    {
        var profiles = new List<CombatAiPersonalityProfile>(BuiltInKinds.Length);
        for (int i = 0; i < BuiltInKinds.Length; i++)
        {
            profiles.Add(CreateBuiltInProfile(BuiltInKinds[i]));
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
                profile._explorationBias = 0.4f;
                profile._riskTolerance = 0.6f;
                break;
            case CombatAiPersonalityKind.BattleJunkie:
                profile._aggression = 1f;
                profile._riskTolerance = 0.7f;
                break;
            case CombatAiPersonalityKind.Cunning:
                profile._caution = 0.8f;
                profile._objectiveFocus = 0.9f;
                profile._riskTolerance = -0.4f;
                break;
            case CombatAiPersonalityKind.Devoted:
                profile._supportBias = 1f;
                profile._riskTolerance = 0.5f;
                break;
            case CombatAiPersonalityKind.Lonely:
                profile._supportBias = 0.8f;
                profile._explorationBias = 0.5f;
                break;
            case CombatAiPersonalityKind.Reckless:
                profile._objectiveFocus = 1f;
                profile._riskTolerance = 1f;
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
            CombatAiPersonalityKind.Cunning => "狡猾",
            CombatAiPersonalityKind.Devoted => "献身的",
            CombatAiPersonalityKind.Lonely => "寂しがり",
            CombatAiPersonalityKind.Reckless => "猪突猛進",
            _ => "標準",
        };
    }

    public static string GetBehaviorDescriptionJapanese(CombatAiPersonalityKind kind)
    {
        return kind switch
        {
            CombatAiPersonalityKind.AttentionSeeker =>
                "キャラの多い場所へ寄って目立ちます。",
            CombatAiPersonalityKind.BattleJunkie =>
                "何があっても敵への攻撃をやめません。",
            CombatAiPersonalityKind.Cunning =>
                "敵の少ないルートを使って魔石へ向かいます。",
            CombatAiPersonalityKind.Devoted =>
                "一定範囲内のHPが低い味方のもとへ駆けつけます。",
            CombatAiPersonalityKind.Lonely =>
                "他のキャラと一緒に行動し、一人になると探索だけします。",
            CombatAiPersonalityKind.Reckless =>
                "途中の敵を無視して、敵魔石へ一直線に向かいます。",
            _ => "特別な偏りはなく、武器の役割どおりに行動します。",
        };
    }

    public string BehaviorDescriptionJapanese => GetBehaviorDescriptionJapanese(_kind);
}
