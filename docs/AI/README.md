# 戦闘AI

## 意図

AIは「目的を1つ選ぶ → 移動とスキルを1つの計画にする → 実行する」。
武器と性格は目的を増やさず、目的内の優先順位と立ち位置を変える。

```text
戦況・文脈 → 目的 → Plan（移動＋スキル） → Execute
```

地形、追従、索敵、高所、詠唱は追加ステートにしない。

## 読む順番

- [判断の流れ](判断の流れ.md): 目的選択とPrepare/Execute
- [移動](移動.md): 立ち位置と経路
- [スキルと対象](スキルと対象.md): 技能・対象の選び方
- [武器](武器.md): 武器ごとの役割
- [性格](性格.md): 性格ごとの意図
- [進攻ルート](進攻ルート.md): 魔石への経路

デバッグ手順は [自動戦闘デバッグ](自動戦闘デバッグ.md)、ログ形式は [診断ログ](診断ログ.md) を参照する。

## コードの入口

- `Assets/Scripts/Combat/Characters/AI/`
- 判断: `CombatCharacterSystem.TickAiDecisionsNow`
- 計画: `CombatAiBrain` / `CombatAiPlanner`
- ログ: `CombatBattleEventLogger`
