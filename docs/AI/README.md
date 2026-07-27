# 戦闘AIドキュメント

戦闘AIは、戦況から一つの行動を直接決めるのではなく、目的・移動・スキルを段階的に採点して選ぶ。
武器ごとの役割を基本としつつ、体力、敵味方の位置、魔石の状況、経路の安全性、使用可能なスキル、性格を加味して行動を変える。

## 読む順

| ドキュメント | 内容 |
|---|---|
| [仕様](仕様.md) | 判断ループ、目的・戦況・対象・移動・スキル、武器別AI |
| [性格](性格.md) | 性格固有状態と各性格の補正 |
| [デザイナーズコンボ](デザイナーズコンボ.md) | 武器×性格の連携パターン |
| [デザイナーズコンボテスト方法](デザイナーズコンボテスト方法.md) | コンボ検証の手順と判定基準 |
| [進攻ルート](進攻ルート.md) | 魔石間の進攻ルート候補（薄い実装・AI選択は未接続） |
| [診断ログ](診断ログ.md) | 開発用バトルイベントログの読み方 |

## 判断フロー

```text
戦場の情報を収集する
        ↓
性格固有状態を判定する
        ↓ 通常判断が必要
戦況を数値化する
        ↓
目的候補を比較する
        ↓
移動候補を比較する
        ↓
スキルと対象を比較する
        ↓
移動命令 → スキル → 魔石攻撃の順で実行を試みる
        ↓
次の判断時に最初から再計算する
```

## 関連コード

| 領域 | 主な場所 |
|---|---|
| 判断本体 | `Assets/Scripts/Combat/Characters/AI/`（`CombatAiBrain`, Planner, Scorer 群） |
| 進攻ルート候補 | `Assets/Scripts/Combat/Debug/CombatStoneAssaultRoutes.cs` |
| バトル診断ログ | `Assets/Scripts/Combat/Debug/CombatBattleEventLogger.cs` |
| コンボ自動試験 | `Assets/Tests/DesignerCombos/` |

## 他ドキュメントとの関係

- スキル内容・バランス: [`../combat-skills.md`](../combat-skills.md), [`../skill-balance-summary.md`](../skill-balance-summary.md)
- ゲーム全体: [`../game.md`](../game.md)
