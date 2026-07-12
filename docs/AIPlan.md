## AIの設計

## 方針

AI は共通の判断フローを持ち、武器ごとの差分と性格パラメータを重ねて最終判断を変える。  
武器ごとに AI 全体を完全分離しない。  
目的の種類、行動の種類、対象の種類は共通化し、武器と性格はそれぞれ評価値へ補正を与える。  

## NavMeshのエリアとコスト

* 平地：基準
* 川：めっちゃ高い
* 森：基本的には普通
* 雪：ちょっと高い
* 沼：ちょっと高い
* 丘の周囲：基本的には普通
* 岩の周囲：基本的には普通

## 判断フロー

1. 戦況を収集する
2. 収集した情報を判断用の中間指標へ変換する
3. 中間指標から目的ごとのスコアを計算する
4. 最も高い目的を選ぶ
5. 選ばれた目的に対する移動候補を生成する
6. 移動候補ごとのスコアを計算する
7. 選ばれた目的に対するスキル候補を生成する
8. スキル候補ごとのスコアを計算する
9. 移動レイヤーとスキルレイヤーをそれぞれ `CombatAiPlan` へ詰める

## 判断材料の一覧

### 敵の観測情報

* 敵の現在位置
* 敵の最終既知位置
* 敵を現在視認しているか
* 敵を記憶しているか
* 敵が自分を認識しているか

### 敵について収集した情報

* キーはキャラクター
* 相手の武器種
* 相手の攻撃レンジ
* 相手のデバフ状態
* 相手のHP / MaxHP
* 相手が現在行動可能か
* 相手の現在目的

### 味方の動的情報

* 味方の現在位置
* 味方のHP / MaxHP
* 味方のバフ状態
* 味方の現在目的
* 味方が現在行動可能か

### マップ・環境情報

* 天気
* 風
* 岩の位置
* マップの高所
* 森の候補地
* 自分の魔石の位置
* 敵の魔石の位置
* 川の橋の位置

## 判断データ構造

## 中間層

戦況を収集した直後に、目的決定専用の中間層を 1 段だけ挟む。  
生データをそのまま目的判定へ流さず、判断しやすい意味のある指標へ要約してから使う。

レイヤー構造:

1. `CombatAiContext`
   * 生の観測情報
2. `AiAssessment`
   * 判断用の中間指標
3. 目的決定 / 移動決定 / スキル決定

## 中間層の制約

中間層は増殖しやすいので、以下の制約を置く。

* 中間層は 1 段だけにする
* `中間の中間` は作らない
* 指標は目的そのものではなく、戦況の特徴量にする
* 天気、武器、性格ごとに専用指標を増やさない
* 指標名は 1 文で意味を説明できるものだけにする

武器差分、天気差分、性格差分は  
中間指標そのものを増やすのではなく、既存指標の評価値へ補正として入れる。

`CombatAiContext -> AiAssessment` の変換は AI 用の解釈であり、  
`CombatSystem` の事実データそのものではない。  
そのため、中間層の生成は `CombatSystem` ではなく AI 系ファイルへ置く。

中間層は目的の言い換えにしない。  
たとえば `AttackEnemy` に対応する `KillOpportunity` のような 1 対 1 の指標は避け、  
1 つの中間指標が複数の目的評価へ効く形にする。

## 中間層の生成方法

中間層の生成入口は `CombatAiAssessmentBuilder` 1 つにまとめる。  
ここが `CombatAiContext` を受け取り、各中間指標を共通ルールで計算して `CombatAiAssessment` を返す。

流れ:

1. `CombatAiContext` を受け取る
2. 各中間指標を個別関数で計算する
3. 各指標の値を同じスケールへ正規化する
4. `CombatAiAssessment` に詰める
5. 必要ならデバッグ用の内訳も同時に詰める

形:

* `Build(context)`
  * `EvaluateOwnStoneThreat(context)`
  * `EvaluateSelfThreat(context)`
  * `EvaluateAllyFragility(context)`
  * `EvaluateReachableEnemyValue(context)`
  * `EvaluateEnemyStoneReachability(context)`
  * `EvaluateTerrainAdvantage(context)`
  * `EvaluateEnemyLocationConfidence(context)`
  * `EvaluateRetreatRouteSafety(context)`

生成ロジックの原則:

* 入口は `CombatAiAssessmentBuilder` に 1 本化する
* 指標ごとの計算は小さい関数へ分ける
* `CombatAiContext` から直接目的決定しない
* 中間層の生成では武器差分、性格差分を基本入れない
* 武器差分、天気差分、性格差分は後段の評価補正で扱う
* 各指標はできるだけ同じスケールで返す
* デバッグのため、値だけでなく内訳も取れる形にする

## 中間層が返すもの

`CombatAiAssessment` は最低限以下を返す。

### 中間指標本体

* `OwnStoneThreat`
  * 自軍魔石まわりの脅威の強さ
* `SelfThreat`
  * 自分がどれだけ危険な状態か
* `AllyFragility`
  * 味方全体、または重要味方がどれだけ崩れやすいか
* `ReachableEnemyValue`
  * 今の戦況で攻撃価値の高い敵へ届きやすいか
* `EnemyStoneReachability`
  * 敵魔石へ前進して圧をかけやすいか
* `TerrainAdvantage`
  * 現在または近傍地形がどれだけ有利か
* `EnemyLocationConfidence`
  * 敵位置情報がどれだけ確からしいか
* `RetreatRouteSafety`
  * 撤退経路がどれだけ安全か

### 指標ごとのデバッグ内訳

各指標は可能なら「合計値」と「内訳」を持つ。

例:

* `ReachableEnemyValue`
  * 合計値
  * 高価値敵あり
  * 射線あり
  * 射程内
  * 接敵しやすい
  * 遮蔽あり
  * 天気補正

デバッグ用には以下の形を想定する。

* 指標名
* 最終値
* 加点要素
* 減点要素
* 理由文

### 必要なら将来追加するもの

MVP では増やしすぎないが、必要になったら以下も候補にする。

* 指標ごとの信頼度
  * 記憶ベースか、現在視認ベースか
* 主対象候補
  * その指標に最も強く関係した敵 / 味方 / 地点

ただし、追加する場合も中間層を 1 段に留めることを優先する。

## 中間指標

MVP では中間指標を少数に絞る。

* `OwnStoneThreat`
  * 自軍魔石がどれだけ危険か
  * `DefendOwnStone` を上げやすくし、`DestroyEnemyStone` や `Search` を下げやすくする
* `SelfThreat`
  * 自分がどれだけ危険か
  * `Retreat` を上げやすくし、前進系目的を下げやすくする
* `AllyFragility`
  * 味方がどれだけ崩れやすいか
  * `SupportAlly` や `DefendOwnStone` を上げやすくする
* `ReachableEnemyValue`
  * 今の戦況で攻撃価値の高い敵へどれだけ届きやすいか
  * `AttackEnemy` を上げやすくし、攻撃支援スキル評価にも使う
* `EnemyStoneReachability`
  * 敵軍魔石へどれだけ前進しやすいか
  * `DestroyEnemyStone` を上げやすくする
* `TerrainAdvantage`
  * 現在または近傍地形がどれだけ有利か
  * `AttackEnemy`、`Search`、移動候補評価に再利用する
* `EnemyLocationConfidence`
  * 敵位置情報がどれだけ確からしいか
  * `Search`、`AttackEnemy`、`DefendOwnStone` の確信度に効く
* `RetreatRouteSafety`
  * 撤退経路がどれだけ安全か
  * `Retreat` の実行価値や移動候補評価に使う

中間指標は目的名の別名にしない。  
増やす場合は、複数目的で再利用され、武器や天気の補正を受ける土台になるものだけにする。

## 目的と行動表現

## 目的

目的は「何を達成したいか」を表す。  
実装では英語名を enum 等に使い、日本語名を仕様確認に使う。

* `DestroyEnemyStone`（敵の魔石を破壊する）
  * 敵魔石の位置が分かっている
  * 現在の武器や状況で前進価値が高い
* `DefendOwnStone`（自分の魔石を防衛する）
  * 自軍魔石付近に敵がいる
  * 防衛に戻る価値が高い
* `AttackEnemy`（敵を撃破する）
  * 倒し切れそうな敵がいる
  * 武器相性、距離、地形が有利
* `SupportAlly`（味方を援護する）
  * 支援価値の高い味方がいる
  * バフ、回復、前線維持の価値が高い
* `Search`（索敵する）
  * 明確な攻撃対象や拠点目標がない
  * 敵の最終既知位置や有利地形へ向かいたい
* `ShareIntel`（情報共有する）
  * 情報共有システムを入れる場合の目的
  * MVP では未実装候補
* `Hide`（潜伏する）
  * 森や遮蔽を活かして見つかりにくく動きたい
  * MVP では未実装候補
* `Retreat`（撤退する）
  * 自身のHPが低い
  * 現状では不利で、生存優先の価値が高い

## レイヤー構造

現在の AI プランは「移動」と「スキル」を別レイヤーとして持つ。  
1 つの行動候補だけを選ぶのではなく、同じ目的に対して

* どこへ移動するか
* どのスキルを使うか

を別々に決める。

`CombatAiPlan` が持つもの:

* 目的
* 移動先
* 使用スキル
* スキル対象 / スキル文脈

このため、設計書の「行動」は単一の排他的コマンドではなく、  
移動レイヤーとスキルレイヤーの判断材料をまとめて説明するための便宜的な呼び方として扱う。

## 行動の表現

行動は以下の要素で表現する。

* 行動種別
* 主対象
* 副対象
* 目的地
* 使用スキル
* 選定理由

## 行動種別

行動種別は「何をするか」を表す。

* `Wait`（待機する）
* `Move`（移動する）
* `UseSkill`（スキルを使う）

ただし現在の実装前提では、`Move` と `UseSkill` は別レイヤーで評価する。  
`UseSkill` には通常攻撃スキルも含む。  
現在の `CombatAiPlan` では主に

* 移動先の決定
* スキル使用の決定

を分けて扱う。

## 行動対象

行動対象は「誰に対して / どこに対して行うか」を表す。  
`MoveToAlly` のような単一表現にせず、行動種別と対象種別を分けて表す。

### 対キャラクター対象

* `NearestEnemy`（最も近い敵）
* `SpecificEnemy`（特定の敵）
* `EnemyWithLowestHp`（最も倒し切りやすい敵）
* `EnemyThreateningOwnStone`（自軍魔石を脅かしている敵）
* `NearestAlly`（最も近い味方）
* `SpecificAlly`（特定の味方）
* `AllyNeedingSupport`（支援価値が高い味方）
* `AllyHoldingFrontline`（前線維持中の味方）

### 対地点対象

* `OwnStone`（自軍魔石）
* `EnemyStone`（敵軍魔石）
* `LastKnownEnemyPosition`（敵の最終既知位置）
* `HighGround`（高所）
* `Forest`（森）
* `Bridge`（橋）
* `RetreatPoint`（撤退先）
* `SpecificPosition`（指定地点）

## 対象選定ルール

同じ対象種別でも、どう選ぶかで意味が変わる。  
設計上は対象種別と選定ルールを分けて扱う。

* `Nearest`（最も近い）
* `LowestHp`（HPが最も低い）
* `HighestThreat`（脅威が最も高い）
* `BestKillChance`（倒し切りやすさが最も高い）
* `BestSupportValue`（支援価値が最も高い）
* `BestObjectiveFit`（現在目的との整合が最も高い）

例:

* `Move` + `NearestAlly` + `Nearest`
  * とにかく近くの味方へ寄る
* `Move` + `SpecificAlly` + `BestSupportValue`
  * 支援価値が最も高い特定味方へ寄る
* `UseSkill` + `SpecificEnemy` + `BestKillChance`
  * 最も倒し切りやすい敵へ通常攻撃スキルまたは攻撃スキルを使う

## スコアリング方式

最終スコアは以下の合計で決める。

`基本点 + 武器補正 + 性格補正 + 状況補正`

* 基本点
  * 全ユニット共通の判断基準
* 武器補正
  * 武器ごとの戦術傾向
* 性格補正
  * 性格ごとの優先度傾向
* 状況補正
  * HP、距離、地形、敵味方配置、視界、天気など

## 移動レイヤー

移動レイヤーは「どこへ向かうか」を決める。  
移動先は `Move` 行動の対象として表現する。  
従来のメモを落とさないため、移動意図を以下のように整理する。

* `Wait`（その場で待機）
* `Move` + `LastKnownEnemyPosition`（索敵）
* `Move` + `EnemyStone`（敵拠点へ進軍）
* `Move` + `NearestAlly`（近くの味方へ接近）
* `Move` + `SpecificAlly`（指定の味方へ接近）
* `Move` + `SpecificEnemy`（敵へ接近）
* `Move` + `OwnStone`（自陣へ戻る）
* `Move` + `HighGround`（高所へ移動）
* `Move` + `Forest`（森へ移動）
* `Move` + `SpecificPosition`（指定地点へ移動）

## スキルレイヤー

スキルレイヤーは「何のスキルをどの対象へ使うか」を決める。  
スキル行動は `UseSkill` として扱う。  
通常攻撃もここへ含める。

* `Wait`（何もしない）
* `UseSkill`（スキルを使う）

`UseSkill` では以下を追加で持つ。

* 使用スキル
* 主対象
* 副対象
* 使用位置
* そのスキルを選んだ理由

## 通常攻撃の扱い

通常攻撃は独立した行動種別ではなく、`SkillBase` 系の通常攻撃スキルとして扱う。  
これは `docs/combat-skills.md` の「通常攻撃も含め、戦闘中の行動はすべて `SkillBase` 系のスキルとして扱う」と揃える。

この設計書では以下で統一する。

* 目的としての「攻撃」
  * `AttackEnemy`
* 実行レイヤーとしての「攻撃」
  * `UseSkill`
* 通常攻撃
  * 攻撃系スキルの 1 種
  * 例: `Sword_Slash`, `Shield_Slash`, `Wand_Bolt`, `Bible_Smite`

## スキル候補の作り方

スキル候補は「スキル単体」ではなく、  
`どのスキルを誰 / どこへ使うか` の組み合わせで作る。

候補の単位:

* `Skill + PrimaryTarget + SecondaryTarget + Context`

例:

* `StrBuff` + `SpecificAlly(前衛)` 
* `DefBuff` + `SpecificAlly(瀕死味方)`
* `Debuff_FaiDown` + `SpecificEnemy(聖職者)`
* `Sword_Slash` + `SpecificEnemy(最も倒し切りやすい敵)`
* `AreaDamage` + `SpecificPosition(複数敵を巻き込める地点)`

スキル候補生成の流れ:

1. 現在使用可能なスキルを列挙する
2. 各スキルごとに取りうる対象候補を列挙する
3. `スキル + 対象` ごとに評価する
4. 最も高い候補を `CombatAiPlan.Skill` と `CombatAiPlan.SkillContext` に入れる

## スキル評価の基本式

スキルの最終評価値は以下の合計で決める。

`目的一致 + 対象価値 + 効果相性 + 射程到達性 + 位置相性 - 無駄撃ちペナルティ`

### 目的一致

現在の目的とスキル役割がどれだけ噛み合っているか。

* `SupportAlly` 中ならバフ、回復、保護系へ加点
* `AttackEnemy` 中なら攻撃、デバフ系へ加点
* `DefendOwnStone` 中なら防衛、足止め、耐久支援へ加点
* `Retreat` 中なら自己防衛、離脱補助、回復へ加点

### 対象価値

その対象へ使う価値がどれだけ高いか。

* 味方なら
  * その味方が重要か
  * その味方が近く行動機会を得るか
  * その味方を生かす価値が高いか
* 敵なら
  * その敵が脅威か
  * その敵を弱体化する価値が高いか
  * その敵を今止める意味が大きいか

### 効果相性

スキル効果と対象の役割・性能がどれだけ噛み合っているか。

* `STR` バフは `Sword` や `Shield` に加点しやすい
* `INT` バフは `Wand` や `Grimoire` に加点しやすい
* `FAI` バフは `Bible` や `Rosary` に加点しやすい
* 防御バフは前線維持中の味方に加点しやすい
* 移動補助や撤退補助は危険な位置にいる味方へ加点しやすい

### 射程到達性

今すぐ届くか、少し移動すれば届くかを評価する。

* 既に有効射程内なら高く評価する
* 移動後に自然に届くならやや高く評価する
* 大きく位置を崩さないと届かないなら下げる

### 位置相性

現在位置や移動後位置とスキルの使いやすさが合っているか。

* 範囲攻撃なら複数敵を巻き込みやすい位置を加点
* 支援スキルなら複数味方へ届きやすい位置を加点
* 射線が必要なスキルなら遮蔽や高低差も評価する

### 無駄撃ちペナルティ

撃っても価値が低い場合は減点する。

* 既に同系統バフが十分に入っている
* 対象がすぐ倒れそうで効果を活かしにくい
* 効果対象としての相性が低い
* クールダウンに対して見返りが小さい

## スキル役割

スキル評価のため、各スキルに役割タグを持たせる前提で考える。

* `Damage`（ダメージ）
* `AreaDamage`（範囲ダメージ）
* `Buff`（バフ）
* `Debuff`（デバフ）
* `Heal`（回復）
* `Protect`（防御・保護）
* `Mobility`（移動補助）
* `SelfOnly`（自己対象）
* `AllyOnly`（味方対象）
* `EnemyOnly`（敵対象）

1 つのスキルが複数タグを持ってよい。

## バフ対象選定

バッファーが適したバフを選ぶには、  
「バフを使うか」だけでなく「どのバフを誰に使うか」を評価する必要がある。

### バフ対象を選ぶときに見るもの

* 対象の武器種
* 対象の主能力値
* 対象の現在目的
* 対象の現在HP
* 対象が前線か後衛か
* 対象が近く行動機会を持つか
* 対象に既に何のバフがかかっているか

### バフ相性の基本例

* `STR` バフ
  * `Sword`
  * `Shield`
* `INT` バフ
  * `Wand`
  * `Grimoire`
* `FAI` バフ
  * `Bible`
  * `Rosary`
* 防御バフ
  * 前線維持中の味方
  * 狙われやすい味方
* 回避や移動補助
  * 撤退中の味方
  * 危険地帯から抜けたい味方

### バッファーの判断例

例:

* 味方に `Sword` と `Wand` がいる
* 使用可能スキルが `StrBuff`, `DefBuff`, `FaiBuff`

このとき:

* `StrBuff -> Sword`
  * 高評価になりやすい
* `DefBuff -> Sword`
  * 前線維持中なら高評価になりやすい
* `FaiBuff -> Sword`
  * 低評価になりやすい
* `StrBuff -> Wand`
  * 低評価になりやすい
* `DefBuff -> Wand`
  * 狙われているなら中評価になりうる
* `FaiBuff -> Wand`
  * 基本は低評価になりやすい

つまり、バッファーは「支援スキルなら何でも撃つ」のではなく、  
対象との相性と現在状況から最も価値の高い組み合わせを選ぶ。

## 補正要素

## 武器差分

武器ごとに AI 全体を別実装するのではなく、  
共通の目的・行動評価に対して補正を与える。

### 武器ごとの目的傾向

各武器は全ての目的を選べるが、評価値への補正によって取りやすい目的が変わる。

* `Sword`（剣）
  * 取りやすい目的
    * `AttackEnemy`
    * `DestroyEnemyStone`
  * 状況次第で取る目的
    * `DefendOwnStone`
    * `Retreat`
  * 取りにくい目的
    * `SupportAlly`
    * `Hide`
    * `ShareIntel`
* `Shield`（盾）
  * 取りやすい目的
    * `DefendOwnStone`
    * `AttackEnemy`
  * 状況次第で取る目的
    * `SupportAlly`
    * `Retreat`
    * `DestroyEnemyStone`
  * 取りにくい目的
    * `Hide`
    * `ShareIntel`
* `Wand`（杖）
  * 取りやすい目的
    * `AttackEnemy`
    * `Search`
  * 状況次第で取る目的
    * `DestroyEnemyStone`
    * `Retreat`
    * `Hide`
  * 取りにくい目的
    * `SupportAlly`
    * `DefendOwnStone`
    * `ShareIntel`
* `Grimoire`（魔導書）
  * 取りやすい目的
    * `AttackEnemy`
    * `DestroyEnemyStone`
  * 状況次第で取る目的
    * `Search`
    * `Retreat`
    * `Hide`
  * 取りにくい目的
    * `SupportAlly`
    * `DefendOwnStone`
    * `ShareIntel`
* `Bible`（聖書）
  * 取りやすい目的
    * `SupportAlly`
    * `DefendOwnStone`
  * 状況次第で取る目的
    * `AttackEnemy`
    * `Retreat`
    * `DestroyEnemyStone`
  * 取りにくい目的
    * `Hide`
    * `ShareIntel`
* `Rosary`（ロザリオ）
  * 取りやすい目的
    * `SupportAlly`
    * `Retreat`
  * 状況次第で取る目的
    * `DefendOwnStone`
    * `Search`
    * `AttackEnemy`
  * 取りにくい目的
    * `DestroyEnemyStone`
    * `Hide`
    * `ShareIntel`

### 武器ごとの目的切り替え

目的の切り替えは「共通ルール + 武器補正」ではなく、  
武器ごとに何を勝ち筋へつなぐ手段として扱うかまで含めて決める。  
特に `AttackEnemy`、`DestroyEnemyStone`、`DefendOwnStone`、`Search` の切り替えは武器依存が大きい。

* `Sword`（剣）
  * 主眼は敵を落として進路を開けること
  * 倒し切りやすい敵がいる、敵後衛へ触れられる、前進しても孤立しにくいなら `AttackEnemy` を他より多めに意識する
  * 一度殴りに行く敵を決めたら、短時間はその敵を追い続ける
  * 少し有利そうな別敵が見えただけでは乗り換えず、対象死亡、見失い、明確な優位差がある場合だけ切り替える
  * 近接武器は射程に入るまで仕事が始まらないため、追撃継続を優先して無意味な目標変更を減らす
  * 敵前衛が薄い、敵魔石前までの進路が通る、敵を追うより魔石へ圧をかける価値が高いなら `DestroyEnemyStone` へ寄る
  * 自軍魔石付近に敵が入り、追撃より帰還の価値が高いなら `DefendOwnStone` へ戻る
* `Shield`（盾）
  * 主眼は守るべき場所と前線を維持すること
  * 自軍魔石周辺が危険、狭所や橋を維持したい、味方前衛の壁役が必要なら `DefendOwnStone` を他より多めに意識する
  * 前線が安定し、守るより押し返した方が有利を広げられるなら `AttackEnemy` へ寄る
  * 敵防衛が崩れ、自分が前へ出ても味方戦列が割れにくいときだけ `DestroyEnemyStone` を取る
* `Wand`（杖）
  * 主眼は安全な攻撃位置を確保して継続的に削ること
  * 視界が弱い、まだ安全な射線位置がない、先に有利地形を取りたいなら `Search` を他より多めに意識する
  * 射線が通る、高所や安全位置から敵へ触れる、削り価値の高い敵が見えているなら `AttackEnemy` へ寄る
  * 既に攻撃射程を満たしているなら、不要に前進して敵へ近づきすぎない
  * 近接敵が自分へ寄ってきたら、攻撃射程を保ったまま距離を取り直す
  * 敵魔石を直接殴るより、魔石周辺を守る敵を崩す方が価値が高い間は `DestroyEnemyStone` へ寄りにくい
* `Grimoire`（魔導書）
  * 主眼は敵陣形を崩して押し込みやすい状況を作ること
  * 複数敵を巻き込みやすい、敵陣形を崩せる、前に出ず火力を通せるなら `AttackEnemy` を他より多めに意識する
  * 敵前衛や防衛位置を崩した結果、魔石周辺へ圧を継続できるなら `DestroyEnemyStone` へ寄る
  * 射線や位置が悪く火力を通せないなら、無理に攻めず `Search` へ戻る
* `Bible`（聖書）
  * 主眼は味方前線を保って攻勢か防衛を成立させること
  * 前線維持役や重要味方へ支援を通す価値が高いなら `SupportAlly` を他より多めに意識する
  * 自軍魔石周辺の維持、前線崩壊防止、守りながら支援を回したいなら `DefendOwnStone` へ寄る
  * 味方攻勢が十分に成立し、支援を前進継続へ変換できるときだけ `AttackEnemy` や `DestroyEnemyStone` の価値が上がる
* `Rosary`（ロザリオ）
  * 主眼は崩壊防止と立て直しで負け筋を消すこと
  * 回復や支援で味方の崩壊を止められるなら `SupportAlly` を他より多めに意識する
  * 前に出るより立て直しの価値が高い、危険地帯を避けながら継戦を保ちたいなら `Retreat` を多めに意識する
  * 基本は味方へ回復が届く後方支援距離を保ち、前線へ重なりすぎない
  * 今いる位置から使える回復で足りるなら無理に前へ出ない
  * 今使える回復量では足りず、より強い近距離回復が必要な時だけ前へ詰める
  * 敵が自分へ寄ってきたら、味方への支援射程を保てる範囲で距離を取り直す
  * 味方前線が安定し、後方支援位置を保ったまま押し上げられるなら `DefendOwnStone` や `Search` へ寄る
  * 自分から `DestroyEnemyStone` を主軸にすることは少なく、他武器の攻勢成立を支える形で間接的に寄与する

### 武器ごとの差分内容

* `Sword`（剣）
  * `AttackEnemy` を高く評価しやすい
  * `DestroyEnemyStone` をやや高く評価しやすい
  * 移動では倒し切りやすい敵への接近を高く評価しやすい
  * 移動では敵との交戦距離に素早く入れる位置を高く評価しやすい
  * 移動では現在追っている敵への接近継続を高く評価しやすい
  * 移動では少差の評価変動で対象を切り替えず、追撃中の敵を維持しやすい
  * 移動では高所が攻撃成立に寄与するなら優先しやすい
* `Shield`（盾）
  * `DefendOwnStone` を高く評価しやすい
  * 移動では自軍魔石付近への復帰を高く評価しやすい
  * 移動では味方前方を維持する位置取りを高く評価しやすい
  * 移動では橋や狭所の封鎖に向いた位置を高く評価しやすい
* `Wand`（杖）
  * 移動では高所を高く評価しやすい
  * 移動では射線が通る位置を高く評価しやすい
  * 移動では敵との距離維持を高く評価しやすい
  * 移動では既に有効射程に入っている相手へ過剰接近せず、届く距離を保つ位置を高く評価しやすい
  * 移動では近接敵へ詰められにくい退避経路つきの位置を高く評価しやすい
* `Grimoire`（魔導書）
  * 移動では複数敵を巻き込みやすい位置を高く評価しやすい
  * 移動では射線と範囲攻撃の両立がしやすい位置を高く評価しやすい
  * 移動では前に出すぎず火力を通せる位置を高く評価しやすい
* `Bible`（聖書）
  * `SupportAlly` を高く評価しやすい
  * `DefendOwnStone` をやや高く評価しやすい
  * 移動では支援価値の高い味方へ寄る位置を高く評価しやすい
  * 移動では前線全体へ効果を届かせやすい中間位置を高く評価しやすい
  * 移動では自軍魔石防衛へ戻りやすい位置をやや高く評価しやすい
* `Rosary`（ロザリオ）
  * 移動では回復・支援対象への接近を高く評価しやすい
  * 移動では危険地帯へ深く入りすぎない後方位置を高く評価しやすい
  * 移動では複数味方へ届きやすい回復拠点的な位置を高く評価しやすい
  * 移動では味方の真上まで寄るより、回復が届く支援距離を保つ位置を高く評価しやすい
  * 移動では現在位置の回復で十分なら距離維持を優先し、近距離大回復が必要な時だけ前進を高く評価しやすい

武器差分は少なくとも以下 2 系統へ反映する。

* 目的評価
  * どの目的を取りやすいか
* 移動評価
  * どの位置、どの対象、どの距離感を取りやすいか

必要なら将来的にさらに以下へ広げる。

* スキル評価
  * どのスキル、どの対象へ撃ちやすいか

## 性格の中身

性格は専用 AI クラスの追加ではなく、  
目的や行動の評価値にかかる補正パラメータとして表現する。

* `Aggression`（攻撃性）
* `Caution`（慎重さ）
* `SupportBias`（支援志向）
* `ObjectiveFocus`（拠点や任務への集中度）
* `ExplorationBias`（索敵志向）
* `RiskTolerance`（危険許容度）
* `PreferredRangeBias`（好みの距離感）

性格の役割:

* 判断材料から目的の選択に補正をかける
* 判断材料と目的から行動選択に補正をかける

## MVP

最初は以下のみ実装対象とする。

* 判断フローの 2D 可視化
  * 実キャラ挙動の確認より先に、判断フローを図として見える状態を作る
* 目的
  * `AttackEnemy`
  * `DefendOwnStone`
  * `SupportAlly`
  * `DestroyEnemyStone`
  * `Retreat`
  * `Search`
* 行動
  * `Move`
  * `UseSkill`
  * `Wait`
* 行動対象
  * `SpecificEnemy`
  * `SpecificAlly`
  * `EnemyStone`
  * `OwnStone`
  * `HighGround`
  * `RetreatPoint`
* 武器対応
  * `Sword`
  * `Shield`
  * `Wand`
  * `Grimoire`
  * `Bible`
  * `Rosary`
* 性格対応
  * 性格補正を入れる
  * ただし性格ごとの完全別 AI は作らない

MVP の目的は以下。

* 目的 enum 作成
* 行動種別 / 対象種別 / 選定ルールの分離
* 情報 -> 中間層生成 -> 目的決定 -> 移動候補生成 -> スキル候補生成
* 通常攻撃を `UseSkill` 内の通常攻撃スキルとして扱う
* 全武器種の差分による目的 / 行動補正
* 性格補正による目的 / 行動補正
* 各段階の候補と内訳を `CombatAiDebugSnapshot` として保持する
* 判断フロー全体を 2D で見える `EditorWindow` を用意する
* 自由文の理由説明は持たず、英語名と日本語訳の組を使って理由を表現する

MVP では、実キャラを NavMesh 上で動かして検証することを主目的にしない。  
まずは以下を確認できる状態を優先する。

* 何を見たか
* 中間層がどうなったか
* 目的候補にどう点が入ったか
* 移動候補とスキル候補がどう選ばれたか
* 武器補正と性格補正がどこに効いたか
* 選ばれた理由が `ReasonCode（日本語訳）` で追えるか

## 今はやらないこと

* 武器ごとの完全別 AI
* 性格ごとの完全別 AI
* 情報共有の詳細ロジック
* 潜伏専用ロジック
* 高度な連携戦術

## デバッグ機能

AI はブラックボックスにしない。  
デバッグ時は「何を見たか」「どう評価したか」「何を選んだか」を追えるようにする。

### EditorWindow

AI デバッグの主表示は専用 `EditorWindow` とする。  
`Inspector` 依存にはせず、全キャラを一覧で見られることを優先する。

MVP では `EditorWindow` を単なる一覧ではなく、  
判断フローそのものを左から右へ追える 2D 表示として実装する。

表示したい流れ:

1. `CombatAiContext`
2. `AiAssessment`
3. 目的候補スコア
4. 選ばれた目的
5. 移動候補スコア
6. 選ばれた移動候補
7. スキル候補スコア
8. 選ばれたスキル候補
9. `CombatAiPlan`

各段階は箱や列として並べ、フロー図として読めるようにする。

`EditorWindow` で見えるようにしたいもの:

* 各キャラの現在目的
* 各キャラの移動先
* 各キャラの現在スキル選択
* 各キャラの中間層 (`AiAssessment`)
* 中間指標の主要値
  * `OwnStoneThreat`
  * `SelfThreat`
  * `AllyFragility`
  * `ReachableEnemyValue`
  * `EnemyStoneReachability`
  * `TerrainAdvantage`
  * `EnemyLocationConfidence`
  * `RetreatRouteSafety`
* 必要なら目的候補、移動候補、スキル候補の上位

表示は `英語名（日本語訳）` を基本形とする。  
自由文の日本語説明は持たない。  
一覧性を保つため、常時表示は要約中心にし、詳細は同ウィンドウ内で展開できる形にする。

MVP では特に以下を見えるようにする。

* 中間層の各指標
* 目的スコアの内訳
  * 基本点
  * 武器補正
  * 性格補正
  * 状況補正
* 移動候補スコアの内訳
* スキル候補スコアの内訳
* 候補ごとの `ReasonCode（日本語訳）`
* 最終的に選ばれた `CombatAiPlan`

理由表示の例:

* `VisibleEnemy`（敵を視認中）
* `LineOfSightAvailable`（射線あり）
* `InSkillRange`（スキル射程内）
* `HighTerrainAdvantage`（地形有利高い）

### Scene デバッグ表示

空間情報の把握は `Scene` 上のデバッグ表示で行う。  
文字を大量に置かず、線・範囲・マーカー中心で表現する。

`Scene` 上で見えるようにしたいもの:

* 移動ルート
* 視線
* 射線
* 対象位置
* 候補地点
* 範囲スキルの効果範囲

例:

* ルート: 現在選ばれている移動経路
* 視線: 対象を視認できているか
* 射線: 攻撃やスキルの線が通るか
* 範囲スキル: 円、扇形、矩形などで効果範囲を表示する

### デバッグ表示の役割分担

* `EditorWindow`
  * 中間層
  * 目的
  * 移動
  * スキル
  * 候補と評価値
* `Scene`
  * ルート
  * 視線 / 射線
  * 範囲スキル範囲
  * 空間的な対象関係

## ファイル構成案

AI の判断ロジックは AI 系ディレクトリへまとめる。  
`CombatSystem` は生データ取得 API に留め、AI 用の解釈は持たせない。

### 新規作成

* `Assets/Scripts/Combat/Characters/AI/CombatAiAssessment.cs`
  * 中間指標の入れ物
  * 例:
    * `OwnStoneThreat`
    * `SelfThreat`
    * `AllyFragility`
    * `ReachableEnemyValue`
    * `EnemyStoneReachability`
    * `TerrainAdvantage`
    * `EnemyLocationConfidence`
    * `RetreatRouteSafety`
* `Assets/Scripts/Combat/Characters/AI/CombatAiAssessmentBuilder.cs`
  * `CombatAiContext -> CombatAiAssessment`
  * 共通変換のみを持つ
  * 武器差分、性格差分はここへ直接入れない
* `Assets/Scripts/Combat/Characters/AI/CombatAiAssessment.cs`
  * `CombatAiAssessment`
  * `CombatAiDebugSnapshot`
  * 目的 / 移動 / スキル候補 1 件分のスコアと内訳
* `Assets/Scripts/Combat/Characters/AI/CombatAiPersonalityProfile.cs`
  * 性格ごとの差分定義
  * 目的補正
  * 移動補正
  * スキル補正
* `Assets/Scripts/Combat/Characters/AI/CombatAiPlanner.cs`
  * 全体の組み立て
  * `CombatAiContext` 収集後の判断フローをまとめる
  * 目的 / 移動 / スキルの個別採点式は直接持たない
  * 各 builder / scorer を呼び出して `CombatAiPlan` と `CombatAiDebugSnapshot` を組み立てる
* `Assets/Scripts/Combat/Characters/AI/CombatAiObjectiveScorer.cs`
  * 中間指標から目的候補のスコアを計算する
  * 基本点、武器補正、性格補正、状況補正を合成する
* `Assets/Scripts/Combat/Characters/AI/CombatAiMoveScorer.cs`
  * 移動候補 1 件分のスコアを計算する
  * 移動候補そのものの生成は持たない
* `Assets/Scripts/Combat/Characters/AI/CombatAiSkillContextBuilder.cs`
  * スキルごとの対象 / 地点候補を列挙する
  * スキル候補の採点は持たない
* `Assets/Scripts/Combat/Characters/AI/CombatAiSkillClassifier.cs`
  * スキルが攻撃、回復、支援、妨害などのどれに当たるかを判定する
* `Assets/Scripts/Combat/Characters/AI/CombatAiFocusTargeting.cs`
  * 剣など、短時間の対象継続が必要なフォーカス補正を扱う
* `Assets/Scripts/Combat/Characters/AI/CombatAiMoveCode.cs`
  * 移動候補コードの定数を持つ
* `Assets/Scripts/Combat/Characters/AI/CombatAiReasonCode.cs`
  * 候補が選ばれた理由コード
  * 英語名を内部保持し、日本語訳を対応づける
* `Assets/Scripts/Combat/Debug/CombatAiDecisionDebugView.cs`
  * 判断フローを 2D で可視化する表示専用ビュー
  * `CombatAiContextCollector` と `CombatAiPlanner` を呼んで描画する

### 既存編集

* `Assets/Scripts/Combat/Characters/Character.cs`
  * `PersonalityBase` 依存を削除し、`SpiritData.PersonalityProfile` を保持する
  * `PlainPersonality` 自動追加を行わない
* `Assets/Scripts/Combat/Characters/SpiritData.cs`
  * `CombatAiPersonalityProfile PersonalityProfile` を保持する
* `Assets/Scripts/Combat/Characters/AI/CombatAiContext.cs`
  * 必要な生データが不足していれば追加する
  * AI 用の解釈は入れない
* `Assets/Scripts/Combat/Characters/AI/CombatAiContextCollector.cs`
  * `CombatAiContext` 収集だけを担当する
  * `AiAssessment` 計算は入れない
* `Assets/Scripts/Combat/Characters/AI/CombatAiPlan.cs`
  * 必要なら補助 API を追加する
* `Assets/Scripts/Combat/Characters/AI/CombatAiPlanner.cs`
  * `CombatAiPlan` だけでなく `CombatAiDebugSnapshot` を生成できるようにする

### 置かない場所

以下には `AiAssessment` や目的評価ロジックを置かない。

* `Assets/Scripts/Combat/...CombatSystem...`
* `Assets/Scripts/Combat/BattleField/...`

理由:

* `CombatSystem` は世界の事実データを扱う層
* `AiAssessment` は AI 用の解釈層
