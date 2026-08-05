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

    public static string GetBehaviorDescriptionJapanese(CombatAiPersonalityKind kind)
    {
        return kind switch
        {
            CombatAiPersonalityKind.AttentionSeeker =>
                "敵集団の正面へ出て、注意を自分へ集めます。",
            CombatAiPersonalityKind.BattleJunkie =>
                "一度掴んだ敵を長く追い、魔石や別敵へ切り替えにくくします。",
            CombatAiPersonalityKind.Calm =>
                "危険が高まったときだけ、はっきり後退します。",
            CombatAiPersonalityKind.Cautious =>
                "直線突撃を避け、森や味方側から迂回して前進します。",
            CombatAiPersonalityKind.Clumsy =>
                "普段は通常どおりですが、まれに変な方向や対象へ寄ります。",
            CombatAiPersonalityKind.Coward =>
                "敵が近いと攻撃を中断し、味方の後ろへ下がります。",
            CombatAiPersonalityKind.Cunning =>
                "行動したあと、森や遮蔽へ引っ込んでから再び戦います。",
            CombatAiPersonalityKind.Despicable =>
                "常に味方を盾にし、その影に隠れます。",
            CombatAiPersonalityKind.Devoted =>
                "狙われた味方と敵の間に割って入り、身を挺して守ります。",
            CombatAiPersonalityKind.Eccentric =>
                "ときどき戦況と無関係な目的や移動へ切り替わります。",
            CombatAiPersonalityKind.Gossiper =>
                "恋人二人の間にへばりつき、近くでは能力が上がります。",
            CombatAiPersonalityKind.HotBlooded =>
                "近くの味方が前進すると、一緒に前へ出ます。",
            CombatAiPersonalityKind.Innocent =>
                "攻撃せず、敵の周囲をぐるぐる回ります。被弾を避けることもあります。",
            CombatAiPersonalityKind.Lazy =>
                "動いたあと、しばらくその場でサボります。",
            CombatAiPersonalityKind.Lecherous =>
                "異性のそばへ合流し、そこを拠点に戦います。",
            CombatAiPersonalityKind.Lonely =>
                "味方から離れるとすぐ合流し、単独行動を避けます。",
            CombatAiPersonalityKind.LoneWolf =>
                "味方がいない敵を選び、単独で寄ります。",
            CombatAiPersonalityKind.OverlySerious =>
                "森や回り込みを使わず、正面から最短で接近します。",
            CombatAiPersonalityKind.Reckless =>
                "途中の敵を無視して、敵魔石へ直進します。",
            CombatAiPersonalityKind.Unstable =>
                "庇わなかった味方を覚え、一度だけ報復します。",
            _ => "特別な偏りはなく、武器の役割どおりに行動します。",
        };
    }

    public string BehaviorDescriptionJapanese => GetBehaviorDescriptionJapanese(_kind);
}
