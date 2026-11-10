using UnityEngine;

[CreateAssetMenu(fileName = "NewCharacterData", menuName = "ScriptableObjects/CharacterData")]
public class CharacterData : ScriptableObject
{
    // キャラクター名（識別IDを兼ねる）
    [field: SerializeField] public string CharacterName { private set; get; } = "Name";

    [field: Header("Identity")]
    [field: SerializeField] public CharacterGender Gender { private set; get; }
    [field: SerializeField] public CharacterData Lover { private set; get; }

    public void ConfigureIdentity(CharacterGender gender, CharacterData lover)
    {
        Gender = gender;
        Lover = lover;
    }

    // 基礎パラメータ
    [Header("Base Parameters")]
    [field: SerializeField] public int MaxHP { private set; get; }
    [field: SerializeField] public int HP { private set; get; }
    [field: SerializeField] public int CP { private set; get; }
    [field: SerializeField] public int STR { private set; get; }
    [field: SerializeField] public int INT { private set; get; }
    [field: SerializeField] public int FAI { private set; get; }
    [field: SerializeField] public int AGI { private set; get; }

    // 各性格の発生の基礎確率
    [Header("Personality Probabilities")]

    [Tooltip("陽キャ：キャラの多いところに行きやすい。武器・手動補正込みの最大HP・STR・INT・FAI・AGIが常時10%増加する。")]
    [field: SerializeField] public float AttentionSeeker { private set; get; }

    [Tooltip("戦闘狂：何があっても敵への攻撃をやめない。")]
    [field: SerializeField] public float BattleJunkie { private set; get; }

    [Tooltip("狡猾：敵の少ない進攻ルートを選ぶ。武器・手動補正込みのAGIが常時20%増加する。")]
    [field: SerializeField] public float Cunning { private set; get; }

    [Tooltip("献身的：一定範囲内のHPが低い味方のもとへ駆けつける。")]
    [field: SerializeField] public float Devoted { private set; get; }

    [Tooltip("寂しがり：他のキャラと一緒に行動する。1人になると行動せず他のキャラを探し続ける。")]
    [field: SerializeField] public float Lonely { private set; get; }

    [Tooltip("猪突猛進：魔石に一直線で向かう。武器補正込みのAGIが20%減少する。")]
    [field: SerializeField] public float Reckless { private set; get; }
}
