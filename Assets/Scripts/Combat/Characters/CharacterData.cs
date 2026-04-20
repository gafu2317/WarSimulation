using UnityEngine;

[CreateAssetMenu(fileName = "NewCharacterData", menuName = "ScriptableObjects/CharacterData")]
public class CharacterData : ScriptableObject
{
    // キャラクター名（識別IDを兼ねる）
    [field: SerializeField] public string CharacterName { private set; get; } = "Name";

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

    [Tooltip("目立ちたがり屋：敵の注意を引くことを優先する。")]
    [field: SerializeField] public float AttentionSeeker { private set; get; }

    [Tooltip("戦闘狂：何があっても敵への攻撃をやめない。")]
    [field: SerializeField] public float BattleJunkie { private set; get; }

    [Tooltip("冷静：HPが危険状態になった場合に自主的に撤退する。")]
    [field: SerializeField] public float Calm { private set; get; }

    [Tooltip("慎重：被ダメージを減らすための行動を取りやすい。")]
    [field: SerializeField] public float Cautious { private set; get; }

    [Tooltip("おっちょこちょい：稀に味方を攻撃する。")]
    [field: SerializeField] public float Clumsy { private set; get; }

    [Tooltip("臆病者：敵の注意を集めないことを優先する。")]
    [field: SerializeField] public float Coward { private set; get; }

    [Tooltip("狡猾：敵の認識から外れて不意打ちを狙う。")]
    [field: SerializeField] public float Cunning { private set; get; }

    [Tooltip("卑怯者：味方を盾にするように移動する。")]
    [field: SerializeField] public float Despicable { private set; get; }

    [Tooltip("献身的：敵と味方の斜線を遮るように移動する。")]
    [field: SerializeField] public float Devoted { private set; get; }

    [Tooltip("不思議ちゃん：行動が完全ランダム。")]
    [field: SerializeField] public float Eccentric { private set; get; }

    [Tooltip("下世話：戦場に恋人関係の2人がいる場合、その近くに行く。近くにいる間、パラメータが大幅に上昇する。2人のうちどちらかが戦闘を離脱した場合、やる気をなくして自分も戦闘を離脱する。")]
    [field: SerializeField] public float Gossiper { private set; get; }

    [Tooltip("熱血：攻撃を行いやすくなる。周囲のキャラも攻撃系の行動を行いやすくなる。")]
    [field: SerializeField] public float HotBlooded { private set; get; }

    [Tooltip("天真爛漫：攻撃をあまり行わず、敵の周りをぐるぐる回っている。敵に狙われやすい。敵の攻撃を確率で回避する。")]
    [field: SerializeField] public float Innocent { private set; get; }

    [Tooltip("怠け者：あまり行動しない。")]
    [field: SerializeField] public float Lazy { private set; get; }

    [Tooltip("スケベ：異性のキャラの近くに行く。異性キャラの近くならステータスが上がるが、そうでなければステータスが下がる。")]
    [field: SerializeField] public float Lecherous { private set; get; }

    [Tooltip("寂しがり：他のキャラと一緒に行動する。1人になると行動せず他のキャラを探し続ける。")]
    [field: SerializeField] public float Lonely { private set; get; }

    [Tooltip("一匹狼：他のキャラが交戦していない敵に優先して攻撃する。他のキャラが戦闘に参加すると別の敵のところへ向かう。")]
    [field: SerializeField] public float LoneWolf { private set; get; }

    [Tooltip("クソ真面目：不意打ちを行わない。")]
    [field: SerializeField] public float OverlySerious { private set; get; }

    [Tooltip("猪突猛進：敵は完全に無視して城へ攻撃する。")]
    [field: SerializeField] public float Reckless { private set; get; }

    [Tooltip("メンヘラ：自分がダメージを受けたとき、他のキャラがその攻撃を庇える状況にあった場合、庇われなかったことに逆ギレしてそのキャラを攻撃する。")]
    [field: SerializeField] public float Unstable { private set; get; }
}