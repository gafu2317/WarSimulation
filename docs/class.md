キャラクタークラス！！
int MaxHP
int HP
int CP
int STR
int INT
int FAI
int AGI

float バフ・デバフ割合...

Dictionary<キャラクタークラス, Vector3>...
/*
全キャラの辞書
Vector3 == nullなら見えてないから参照しない
*/

[武器基底クラス ]

[性格基底クラス]

public void ステータス設定メソッド(国家繁栄度, 設備強化段階,  契約精霊）
{
	パラメータ = 参照データで算出
	[性格基底クラス] = 契約精霊.[性格基底クラス]
}

public void 武器装備メソッド(装備武器)
{
	[武器基底クラス] = 装備武器
}

public void 武器解除メソッド()
{
	[武器基底クラス] = null
}

private void 見えているか判定メソッド()
{
	敵全キャラに見えているかの判定を行い、結果をDictionary<キャラクタークラス, Vector3>...に格納する
自分方向から対象にRay　障害物にぶつかったらnull
反対向きにRay
行きのRayの衝突を「入り」反対のRayの衝突を「出」として視界不良領域の総距離を計算
視界的距離が足りているか判定
}

private void 指定された座標に移動するメソッド() { }

キャラクタープロフィールクラス：SO
float 性格割合...

// 基礎値
int HP
int CP
int STR
int INT
int FAI
int AGI


武器基底クラス
int 使用可能回数
int 性能


武器クラス...：武器基底クラス
public 技のメソッド(自分の必要パラメータ, 技の対象のキャラクラス, 技の対象の相対座標)...
{
	パラメータと武器性能と技の計算式から効果量算出
	対象のキャラクラスに作用させる
}

精霊クラス：SO
[性格基底クラス]

// 向上パラメータ量
int HP
int CP
int STR
int INT
int FAI
int AGI


性格基底クラス
public abstract 行動内容 行動内容選択メソッド(バトルの観測情報)

