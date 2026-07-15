# 戦闘AIイベントログ

開発エージェントがバトル後にAIの判断を診断するための、開発専用イベントログの説明。

## 目的

- バトル中の**重要な変化点だけ**をテキストファイルへ記録する
- 後から `Grep` / `Read` で「なぜ魔石ではなく敵を攻撃したか」などを追えるようにする
- ゲーム内AIのランタイム判断には影響しない（開発ツール）

## 出力先

```text
Logs/CombatBattles/battle_yyyyMMdd_HHmmss.log
```

リポジトリ直下。`.gitignore` で Git 管理対象外。

## 有効化

- Unity Editor または Development Build のみ
- シーンに [`CombatBattleEventLogger`](../Assets/Scripts/Combat/Debug/CombatBattleEventLogger.cs) を配置
- Inspector の `Enabled` をオン（デフォルトオン）
- `CombatBattleFlow` が `Running` になったタイミングで自動的に新規ログファイルを作成

## ログ形式

| 行頭 | 意味 |
|---|---|
| `# CombatBattleLog` | ファイルヘッダ |
| `BATTLE_START` | バトル開始 |
| `OBJECTIVE` | AI目的の切替（理由コード付き） |
| `SKILL` | 意味のあるスキル使用（バフ/回復/大技など） |
| `SNAPSHOT` | 魔石HP・生存数（デフォルト10秒間隔） |
| `DEFEATED` | 撃破（戦闘離脱） |
| `STONE_DESTROYED` | 主魔石破壊 |
| `BATTLE_END` | バトル終了サマリ + 通常攻撃集計 |

通常攻撃（斬撃・盾撃・魔弾・通常攻撃）は1行ずつ書かず、`BATTLE_END` の `skillTally` に集計される。

### 出力例

```text
# CombatBattleLog
file=/path/to/Logs/CombatBattles/battle_20260706_154500.log
weather=Sunny
[t=0.0s] BATTLE_START
[t=12.4s] OBJECTIVE Char_Wand01(Wand) 敵を攻撃 -> 敵魔石を破壊 reason=到達可能敵価値が高い
[t=18.6s] SKILL Char_Bible01 used STRバフ target=Char_Sword02
[t=20.0s] SNAPSHOT ownStoneHP=88/120 enemyStoneHP=54/120 alive=4v3
[t=45.2s] DEFEATED Char_Bible01 killer=Enemy_Sword03
[t=182.1s] BATTLE_END outcome=Victory duration=182.1s ownStoneHP=88 enemyStoneHP=0 alive=4v0
  skillTally: 斬撃 x54, 魔弾 x38
```

## 診断の読み方（開発エージェント向け）

特定キャラの目的切替だけ見る:

```bash
rg "OBJECTIVE Char_Wand01" Logs/CombatBattles/battle_*.log
```

魔石破壊方針への切替を全体で見る:

```bash
rg "-> 敵魔石を破壊" Logs/CombatBattles/
```

バトル結果だけ:

```bash
rg "BATTLE_END" Logs/CombatBattles/
```

## 実装の要点

- イベント源: [`CombatAiDecisionEvents`](../Assets/Scripts/Combat/Characters/AI/CombatAiDecisionEvents.cs), [`CombatSkillActionEvents`](../Assets/Scripts/Combat/Skills/CombatSkillActionEvents.cs), [`CombatHealth.Defeated`](../Assets/Scripts/Combat/Characters/CombatHealth.cs), [`CombatMagicStoneSystem.MainStoneDestroyed`](../Assets/Scripts/Combat/Systems/CombatMagicStoneSystem.cs)
- 整形: [`CombatBattleLogFormatter`](../Assets/Scripts/Combat/Debug/CombatBattleLogFormatter.cs)
- 目的変更イベントに含まれる実際の判断理由をそのまま記録し、ログ用に別の判断を実行しない
- 失敗したスキル実行は `SKILL` として記録しない

## 肥大化防止

- 毎tick・全指標ダンプは行わない
- 高頻度の通常攻撃は集計のみ
- 1バトル1ファイル
- 本番ビルド非搭載
