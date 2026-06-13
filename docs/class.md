# クラス設計メモ

古い草案を簡略化し、現行仕様だけ残す。

## Character

- 主な所持値: `MaxHP`, `HP`, `CP`, `STR`, `INT`, `FAI`, `AGI`
- バフ/デバフ倍率を持つ
- 装備武器を 1 つ持つ
- 実効ステータスは `(基礎ステータス + 武器の主ステ補正) × バフ/デバフ倍率`

## 視認と認識

- `VisibleEnemies`: 今この瞬間に見えている敵
- `RecognizedEnemies`: 今は見えていなくても、認識中の敵を含む
- 認識は「自分が見た情報」と「味方から共有された情報」で更新する
- 最後に情報を得てから 5 秒は認識を保持する
- 敵対象スキルは `VisibleEnemies` だけでなく `RecognizedEnemies` も使う

## Weapon

- 武器はダメージ値そのものを持たない
- 武器は対応する主ステータスを上げる
- 例:
  - Sword / Shield: `STR`
  - Wand / Grimoire: `INT`
  - Bible / Rosary: `FAI`

## Skill

- 直接攻撃/単体回復の基本式は `damage = stat * coefficient`
- ここでの `stat` は実効ステータス
- 範囲攻撃、継続ダメージ、継続回復、バフ/デバフはスキル個別仕様

## AI

- AI は `CombatAiPlanner` がスキル候補と移動候補を評価して行動を決める
- 敵対象スキルの候補収集でも認識中の敵を対象に含める
