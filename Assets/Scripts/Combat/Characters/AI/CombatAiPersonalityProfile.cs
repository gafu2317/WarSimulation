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
        CombatAiPersonalityKind.Gatekeeper,
        CombatAiPersonalityKind.Tagalong,
        CombatAiPersonalityKind.Avenger,
        CombatAiPersonalityKind.HighGround,
    };

    [SerializeField] private string _displayNameJapanese = "性格";
    [SerializeField] private CombatAiPersonalityKind _kind;

    public string DisplayNameJapanese => _displayNameJapanese;
    public CombatAiPersonalityKind Kind => _kind;

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
        return profile;
    }

    public static string GetDisplayNameJapanese(CombatAiPersonalityKind kind)
    {
        return kind switch
        {
            CombatAiPersonalityKind.AttentionSeeker => "陽キャ",
            CombatAiPersonalityKind.BattleJunkie => "戦闘狂",
            CombatAiPersonalityKind.Cunning => "狡猾",
            CombatAiPersonalityKind.Devoted => "献身的",
            CombatAiPersonalityKind.Lonely => "寂しがり",
            CombatAiPersonalityKind.Reckless => "猪突猛進",
            CombatAiPersonalityKind.Gatekeeper => "門番",
            CombatAiPersonalityKind.Tagalong => "便乗屋",
            CombatAiPersonalityKind.Avenger => "復讐鬼",
            CombatAiPersonalityKind.HighGround => "高所好き",
            _ => "標準",
        };
    }

    public static string GetBehaviorDescriptionJapanese(CombatAiPersonalityKind kind)
    {
        return kind switch
        {
            CombatAiPersonalityKind.AttentionSeeker =>
                "索敵中は味方や敵が集まる場所へ寄り、交戦・支援中は武器本来の役割に戻ります。",
            CombatAiPersonalityKind.BattleJunkie =>
                "攻撃できる敵を優先してしばらく追い、敵がいなければ魔石へ向かいます。",
            CombatAiPersonalityKind.Cunning =>
                "敵魔石へ向かう際は敵の少ない安全な進攻ルートを選び、使える間はその道を維持します。",
            CombatAiPersonalityKind.Devoted =>
                "近くのHPが低い味方を見つけると駆けつけ、危険がなければ通常の支援行動に戻ります。",
            CombatAiPersonalityKind.Lonely =>
                "近くに味方がいない間は技能を使わず味方を探し、合流すると通常行動を再開します。",
            CombatAiPersonalityKind.Reckless =>
                "生存中の敵魔石を最優先し、魔石を狙えるダメージ技能だけで進攻ルートを進みます。",
            CombatAiPersonalityKind.Gatekeeper =>
                "自軍魔石の位置が分かる間は防衛を優先し、魔石前で脅威を迎撃して守備位置を維持します。",
            CombatAiPersonalityKind.Tagalong =>
                "編成画面で便乗屋より上にある選択済みの味方を便乗対象に設定し、その味方の目的・対象・目的地を引き継ぎます。便乗対象が設定されていない、または利用できない場合は通常行動に戻ります。",
            CombatAiPersonalityKind.Avenger =>
                "直近で攻撃した敵が生存し位置を把握できる間は追い続け、見失うと通常の目的へ戻ります。",
            CombatAiPersonalityKind.HighGround =>
                "一定高度以上の高所領域を維持し、技能が使えない場合は高所内から敵・味方・敵魔石を探します。",
            _ => "特別な偏りはなく、装備した武器の役割と戦況に応じて通常の目的を選びます。",
        };
    }

    public string BehaviorDescriptionJapanese => GetBehaviorDescriptionJapanese(_kind);
}
