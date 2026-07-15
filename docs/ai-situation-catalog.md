# 戦闘AI 状況カタログ

## この文書の役割

戦闘AIに求める状況別の判断と、それを確認する自動テストの対応を管理する。
現在の計算方法と武器・性格ごとの振る舞いは [AI挙動.md](AI挙動.md)を正とする。
未実装の候補は [AIPlan.md](AIPlan.md)で管理する。

`CombatAiContext → CombatAiAssessment → 目的・移動・スキルの採点 → CombatAiPlan` という共通経路で要求を表現し、状況名ごとの専用分岐は作らない。
対応テストの「未作成」は専用の自動テストがないことを表し、実装がないことは表さない。

## 戦略判断

| 番号 | 状況 | 求める判断 | 対応テスト |
|---|---|---|---|
| 戦略-01 | 敵が回復・支援役だけ | 脅威の低い敵より敵魔石の破壊を優先する | 未作成 |
| 戦略-02 | 敵火力を迂回して敵魔石へ行ける | 危険の少ない経路で敵魔石を攻撃する | `CombatAiMoveTests.Planner_BridgeDetourScoresHigherWhenDirectStoneRouteCrossesEnemyRange` |
| 戦略-03 | 敵魔石の体力が残りわずか | 危険を受け入れて破壊を狙う | `CombatAiObjectiveTests.Planner_LowEnemyMainStoneHealthRaisesDestroyObjectiveScore` |
| 戦略-04 | 敵が味方一体へ集中している | 横槍、魔石攻撃、支援、撤退を比較する | 未作成 |
| 戦略-05 | 味方前線が安定している | 攻撃役は敵魔石の破壊へ回る | `CombatAiObjectiveTests.Planner_StableAllyFrontlineRaisesDamageDealerStoneObjectiveScore` |
| 戦略-06 | 生存している味方が敵より多い | 人数差に応じて攻撃的になり、撤退しにくくなる | `CombatAiObjectiveTests.Planner_NumericalAdvantageRaisesOffenseAndLowersRetreat` |
| 戦略-07 | 生存している敵がいない | 対象のない目的を候補から外し、敵魔石の破壊を検討できる | `CombatAiObjectiveTests.Planner_ExcludesObjectivesAndTargetsThatHaveNoLivingEnemy` |

## 偵察・経路

| 番号 | 状況 | 求める判断 | 対応テスト |
|---|---|---|---|
| 経路-01 | 最短経路が敵射程を長く通る | 到着距離と被攻撃危険を比較する | `CombatAiMoveTests.MoveScorer_RouteThroughEnemyRangeIsRiskierThanClearRoute` |
| 経路-02 | 最短の橋に敵が待ち構えている | より安全な橋を選ぶ | `CombatAiMoveTests.Planner_BridgeDetourScoresHigherWhenDirectStoneRouteCrossesEnemyRange` |
| 経路-03 | 高所を経由・占拠できる | 攻撃または偵察に有利な場合だけ高所へ移動する | `CombatAiMoveTests.MoveScorer_HighGroundScoresHigherWhenItAddsActionableTargets` |
| 偵察-01 | 敵位置が不明で高所がある | 遠距離役は視認範囲を広げられる高所を検討する | `CombatAiMoveTests.Planner_RangedSearchPrefersHighGroundWhenEnemyInfoIsMissing` |
| 偵察-02 | 味方が高所へ偵察に向かっている | 同じ高所への重複を避ける | `CombatAiMoveTests.Planner_HighGroundScoreDropsWhenAllyIsAlreadySearchingThere` |
| 偵察-03 | 未認識の敵がいる | 敵の実位置を使わず、探索情報だけで移動先を決める | `CombatAiAssessmentTests.Assessment_IgnoresEnemyWithoutKnownPosition` |

## 交戦・スキル選択

| 番号 | 状況 | 求める判断 | 対応テスト |
|---|---|---|---|
| 交戦-01 | 瀕死の敵がいる | 倒し切れる中で消費の軽い攻撃を選ぶ | 未作成 |
| 交戦-02 | 敵の主火力が生きている | 高脅威の敵へ攻撃、弱体化、拘束を使う | `CombatAiSkillTests.Planner_GrimoireDebuffsEnemyWhoseRoleMatchesStat` |
| 交戦-03 | 自分が集中攻撃されている | 安全な位置へ退避または撤退する | `CombatAiObjectiveTests.Planner_RosaryPrefersRetreatWhenSelfThreatIsHigh` |
| 技能-01 | 長い詠唱中に敵が接近できる | 危険な詠唱を避ける | `CombatAiSkillTests.Planner_LongCastScoreDropsWhenEnemyCanEnterRangeBeforeCompletion` |
| 技能-02 | 高価な攻撃技でしか倒せない | 倒す価値と消費を比較する | 未作成 |
| 技能-03 | 敵が密集している | 複数の有効対象を含む範囲攻撃を選ぶ | `CombatAiSkillTests.Planner_AreaSkillPrefersPointThatHitsMultipleEnemies` |
| 技能-04 | 味方攻撃役が交戦する | 武器役割に合う強化を使う | `CombatAiSkillTests.Planner_BibleBuffsAllyWhoseRoleMatchesStat` |
| 技能-05 | 対象に同種の状態効果が残っている | 効果時間が十分残っていれば重複を避ける | `CombatAiSkillTests.Planner_EquivalentStatusWithLongRemainingTimeGetsLargerPenalty` |
| 技能-06 | 自己体力消費技で自分が危険になる | 使用後の生存見込みが低い技を避ける | 未作成 |

## 連携・防衛

| 番号 | 状況 | 求める判断 | 対応テスト |
|---|---|---|---|
| 連携-01 | 自分が加われば敵を倒し切れる | 味方の開始済み攻撃を考慮し、必要な火力を加える | 未作成 |
| 連携-02 | 味方の開始済み攻撃だけで敵を倒し切れる | 追加攻撃を避け、別対象または別目的を選ぶ | `CombatAiSkillTests.Planner_SelectsAnotherEnemyWhenAllyCastingWillDefeatTarget` |
| 防衛-01 | 敵の射線が後衛や魔石へ通る | 盾役が間に入る | `CombatAiMoveTests.Planner_ShieldCreatesInterceptionPointBetweenEnemyAndFragileAlly` |
| 防衛-02 | 敵が後衛へ接近している | 先回りできる場合は迎撃地点へ移動する | `CombatAiMoveTests.Planner_ShieldCreatesInterceptionPointBetweenEnemyAndFragileAlly` |
| 防衛-03 | 自軍魔石が攻められている | 魔石防衛を目的候補にする | `CombatAiObjectiveTests.Planner_ShieldDefendsThreatenedOwnStone` |
| 支援-01 | 味方が瀕死または被攻撃中 | 回復後の生存見込みが高い対象を救う | `CombatAiSkillTests.Planner_ChoosesLowHpAllyForHealSkill` |
| 撤退-01 | 数的不利または体力不足で押されている | 攻撃の継続と撤退を比較する | `CombatAiObjectiveTests.Planner_RosaryPrefersRetreatWhenSelfThreatIsHigh` |

## 更新方法

1. 新しい要求を対応する分類の表へ一行追加する。
2. 現在仕様の説明が必要な場合は `AI挙動.md` を更新し、この文書へ重複記載しない。
3. 自動テストがなければ、対応テストを「未作成」とする。
4. 実装とテストの追加後に対応テストを更新する。
