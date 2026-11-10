using System;
using UnityEngine;

[CreateAssetMenu(menuName = "WarSimulation/Country/Government UI")]
public sealed class CountryGovernmentData : ScriptableObject
{
    [Tooltip("施設の費用・効果・建築状況はUI調整用。ゲームへの確定処理は後で接続する。")]
    public CountryGovernmentFacility[] Facilities = Array.Empty<CountryGovernmentFacility>();
}

[Serializable]
public sealed class CountryGovernmentFacility
{
    public string Name, EffectName;
    [Min(0)] public int InitialLevel, Minimum, Funding;
    public bool TrainingSchool;
    public CountryGovernmentCost Construction, Upgrade;
    public string Role => Name switch {
        "戦士学校" => "剣・盾の技を学ぶ訓練",
        "魔法学校" => "杖・魔導書の技を学ぶ訓練",
        "教会" => "聖書・ロザリオの技を学ぶ訓練",
        "図書館" => "CP0復帰時の性格を蔵書で調整",
        "広場" => "CP0復帰時の性格を飾り付けで調整",
        "精霊使役学校" => "ステータスを上昇させる精霊施設",
        "鍛冶場" => "武器を製作。施設Lvで品質が向上",
        "酒場" or "天文台" or "闘技場" or "美術館" or "診療所" or "カジノ" or "衛兵所" or "歓楽街" => "キャラクターの性格に関わる施設",
        _ => ""
    };
}

[Serializable]
public struct CountryGovernmentCost
{
    [Min(0)] public int Money, Wood, Ore;
    [Min(1)] public int Months;
    public string Effect;
}
