# 戦闘AI 状況カタログと判断軸

戦闘AIを賢くするための土台ドキュメント。個別の状況を分岐で作り込むのではなく、
**「状況 → 判断軸 → 指標(metric) → 目的スコアへの反映」** の写像として整理する。

- 状況は「検証シナリオ」であり、実装上のif分岐ではない。
- 各状況の裏にある一般的な判断軸を抽出し、指標として持たせる。
- 1つの一般ルール（指標＋重み）で複数状況を同時に満たすことを目指す。
- 性格（`game.md` の絶対ルール。戦闘狂・猪突猛進など）だけは例外的なオーバーライドとして扱う。

## 現在の判断フロー

```text
CombatAiContext（観測）
  └─ CombatAiAssessmentBuilder（指標を算出）
       └─ CombatAiObjectiveScorer（目的を選択）
            Score = Base + Situation + Weapon + Personality
       └─ CombatAiMoveScorer / SkillContextBuilder（移動・スキル選択）
```

目的選択は `AttackEnemy` / `DestroyEnemyStone` / `DefendOwnStone` / `SupportAlly` / `Search` / `Retreat` の
スコア最大値を選ぶユーティリティ方式。この基盤は維持する。

## 既存の指標（CombatAiMetricIndex）

| 指標 | 意味 | 主に効く目的 |
|---|---|---|
| `OwnStoneThreat` | 自陣魔石がどれだけ攻められているか | DefendOwnStone |
| `SelfThreat` | 自分がどれだけ狙われ/倒されそうか | Retreat, AttackEnemy(減点) |
| `AllyFragility` | 味方の脆さ・瀕死度 | SupportAlly, DefendOwnStone |
| `ReachableEnemyValue` | 攻撃できる敵の「殴る価値」 | AttackEnemy |
| `EnemyStoneReachability` | 敵魔石への到達しやすさ | DestroyEnemyStone |
| `TerrainAdvantage` | 地形的優位（高所など） | AttackEnemy, Search |
| `EnemyLocationConfidence` | 敵位置の把握度 | Search |
| `RetreatRouteSafety` | 撤退経路の安全度 | Retreat |
| `SelfExposure` | 敵にどれだけ認識・露出しているか | 隠密系の移動 |

## 現状の弱点（この設計で直したい点）

### 1. `ReachableEnemyValue` は敵の「役割・脅威」を見ていない

現状はHP残量・視認・射程内・距離だけで算出する。ヒーラーだけの敵陣でも「攻撃価値が高い」と誤認する。
「殴れる価値」であって「倒す価値／倒すべき相手か」ではない。

### 2. `DestroyEnemyStone` の一律ペナルティ

敵が1体でも見えると固定で大きく減点する実装があり、「回り込んで魔石破壊」を物理的に不可能にしている。
本来は「敵の存在」ではなく「敵が魔石破壊を止められる脅威か」で判定すべき。

## 不足している指標（追加候補）

| 追加指標(案) | 意味 | 満たす状況 |
|---|---|---|
| `EnemyThreatLevel` | 敵集団が自分/味方を倒しきれる力（火力・射程・数・HPから） | ヒーラーのみ→魔石、撤退判断 |
| `EnemyRoleMix` | 敵構成（火力/回復/支援）の内訳 | ヒーラーのみ→魔石、主火力の優先狙い |
| `KillableTargetValue` | 「実際に倒しきれる」敵の価値（今の殴る価値と分離） | 瀕死狙い、袋叩き回避 |
| `StoneApproachSafety` | 敵魔石までの経路が敵射線/射程をどれだけ避けられるか | 回り込み魔石破壊 |
| `ApproachRouteOptions` | 敵魔石への複数経路候補（例: 別の橋を経由するルート） | 最短以外のルート選択 |
| `WinProximity` | 勝利への前進度（敵魔石HP残量など） | 詰め切り判断 |
| `AllyEngagementState` | 味方が誰とどれだけ交戦しているか | 一匹狼、寂しがり、献身 |
| `ScoutValue` | 高所偵察して味方へ敵位置を共有する価値（情報不足×高所×通信） | 偵察係 |
| `EnemyScoutThreat` | 敵が高所で偵察・情報共有している脅威（撃墜対象価値） | 撃墜係 |

## 状況カタログ

各行は「状況 → 期待挙動 → 判断軸 → 必要指標（既存/要追加）」。

### 戦略判断（魔石 vs 敵）

| 状況 | 期待挙動 | 判断軸 | 指標 |
|---|---|---|---|
| 相手がヒーラー/支援のみ | 敵を無視して魔石を攻撃 | 敵は自分を倒せない＝脅威が低い | `EnemyThreatLevel`(要追加), `EnemyRoleMix`(要追加) |
| 敵火力を迂回して魔石へ | 回り込んで魔石破壊 | 経路が敵射線を避けられる | `StoneApproachSafety`(要追加), `EnemyStoneReachability` |
| 敵魔石が残りわずか | 多少無理でも詰めに行く | 勝利が近い | `WinProximity`(要追加) |
| 敵が味方1体を袋叩き中 | 横槍か魔石か撤退を選ぶ | 自分が倒しきれる相手がいない | `KillableTargetValue`(要追加), `SelfThreat` |
| 味方前線が安定 | 火力職は魔石破壊へ切替 | 前線を任せられる | `AllyFragility`, `OwnStoneThreat`（一部実装済） |

### 情報戦・偵察・経路

| 状況 | 期待挙動 | 判断軸 | 指標 |
|---|---|---|---|
| 敵魔石への最短が危険 | 迂回/安全ルートを選ぶ | 最短より生存率の高い経路 | `StoneApproachSafety`(要追加), `ApproachRouteOptions`(要追加) |
| 最短ルートの橋に敵が張っている | 別の橋を経由するルートを選ぶ | チョークポイントの分散 | `BridgePositions`(既存), `ApproachRouteOptions`(要追加) |
| 高所を経由・占拠したい | 目標地点を山（高所）に設定して移動 | 視界と地形優位の確保 | `HighGroundCandidates`(既存), `TerrainAdvantage` |
| 敵位置が不明＆高所がある | 高所へ登り偵察し味方へ位置共有 | 情報共有の価値 | `EnemyLocationConfidence`, `TerrainAdvantage`, `ScoutValue`(要追加) |
| 敵が高所で偵察・共有中 | その偵察役を撃墜し敵の視界を潰す | 敵の情報源を断つ | `EnemyScoutThreat`(要追加) |
| 味方が偵察役を担っている | 火力職は前進、偵察は任せる | 役割の重複回避 | `AllyEngagementState`(要追加), `ScoutValue`(要追加) |

橋（`BridgePositions`）と高所（`HighGroundCandidates`）は既にcontextへ離散点として入っている。
そのため「別の橋ルート」は経由する橋を差し替える離散選択で軽く実装でき、
「目標地点を山にする」も既存の高所候補を移動目標に流用するだけで済む。
任意経路の生成より、これら既知のチョークポイント/高所を経由点にする方式を先に入れる。

「係（役割分担）」はキャラ単体のスコアだけでなく、味方間の重複を避けるチーム調整レイヤーが要る。
偵察係・撃墜係・詰め係などは、個体のユーティリティに「味方が既に担っているか」の情報を足して決める。

### 交戦判断（敵の選び方）

| 状況 | 期待挙動 | 判断軸 | 指標 |
|---|---|---|---|
| 瀕死の敵がいる | 仕留めを優先 | 低コストで確実にキル | `ReachableEnemyValue`（HP項は実装済） |
| 敵の主火力が生きている | デバフ/拘束で無力化 | 脅威源の除去 | `EnemyRoleMix`(要追加)＋スキル対象選択 |
| 自分が集中攻撃されている | 隠れる/撤退 | 被弾を減らす | `SelfThreat`, `SelfExposure`, `RetreatRouteSafety` |
| 敵位置が不明 | 索敵/高所へ | 情報を得る | `EnemyLocationConfidence`, `TerrainAdvantage` |

### 防衛・支援判断

| 状況 | 期待挙動 | 判断軸 | 指標 |
|---|---|---|---|
| 自陣魔石が攻められている | 防衛へ戻る | 拠点を守る | `OwnStoneThreat` |
| 味方が瀕死 | 回復/庇う | 戦力維持 | `AllyFragility` |
| 数的不利で押されている | 撤退して立て直す | 全滅回避 | `SelfThreat`, `RetreatRouteSafety` |

### 武器指針との対応（game.md 基本行動指針）

| 武器 | 指針 | 主に使う指標 |
|---|---|---|
| 双剣 | 敵に近づいて攻撃 | `ReachableEnemyValue` |
| 盾 | 味方にくっつく | `AllyFragility`, `OwnStoneThreat` |
| 杖 | 隠れて攻撃 | `SelfExposure`, `ReachableEnemyValue` |
| 魔導書 | 隠れていやがらせ | `SelfExposure`, `EnemyRoleMix`(要追加) |
| 聖書 | 隠れて回復 | `SelfExposure`, `AllyFragility` |
| ロザリオ | 隠れて支援 | `SelfExposure`, `AllyFragility` |

## 進め方（このカタログの使い方）

1. カタログを随時追記し、状況を増やす。
2. 各状況を判断軸へ還元し、既存指標で足りるか／新指標が要るかを表で管理する。
3. 新指標を `CombatAiAssessmentBuilder` に追加し、`CombatAiObjectiveScorer` の重みへ組み込む。
4. 実装後、カタログの各状況が期待挙動を出すかデバッグ可視化で検証する。
5. 弱点（役割無視の `ReachableEnemyValue`、`DestroyEnemyStone` の一律ペナルティ）を脅威ベースへ置換する。

## 未実装・今後の検討

- 敵の役割推定（装備武器種から火力/回復/支援を分類）
- 経路安全度の算出（射線・射程・遮蔽の考慮）
- 敵魔石への複数経路候補（既存の橋 `BridgePositions` を経由点に別ルート化。高所を目標地点にする案も含む）
- 高所偵察と味方への位置共有を担う偵察係の役割
- 敵の偵察役（高所占拠・広域視界）の検出と撃墜優先
- 役割分担（係）のチーム調整レイヤー（味方間の重複回避）
- 味方交戦状態の共有（一匹狼・寂しがり・献身などの性格に必要）
- 勝利前進度（敵魔石HPの参照）
