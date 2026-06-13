# スキル射程・威力 調整メモ

現状のスキル調整値は主に2か所に分かれている。

- 武器の射程・CT・主ステ補正: `Assets/Data/CombatFields/Weapon/*.asset`
- 各スキルの射程・係数・持続時間: `Assets/Scripts/Combat/Skills/Implementations/*.cs` と `Assets/Scripts/Combat/Skills/CombatSkillFactory.cs`

`Assets/Data/Combat/Skills/*.asset` の `SkillDefinition` は表示名と必要武器種の管理が中心で、射程や威力は持っていない。

## 現在の計算式

- 攻撃/回復の基本式: `最終値 = 実効ステータス × スキル係数`
- 実効ステータス: `(キャラ基礎ステータス + 武器の主ステ補正) × バフ/デバフ倍率`
- 武器は直接ダメージを持たず、対応する主ステータスを上げる

## 共通補正

- ステルス奇襲補正: 対象が術者を未認識ならダメージ `x1.5`
- 距離依存ダメージ:
  - `Wand_Bolt`: 近距離 `x0.8` -> 遠距離 `x1.5`
- 距離依存回復:
  - `Rosary_DistantHeal`: 近距離 `x1.5` -> 遠距離 `x0.8`

## 通常攻撃

| 武器 | 実装スキル | 日本語名 | 射程 | 主ステ | スキル係数 | CT |
|---|---|---|---:|---|---:|---:|
| Sword | `Sword_Slash` | 斬撃 | 2.0 | `STR` | 1.0 | 1.0 |
| Shield | `Shield_Slash` | 盾撃 | 2.0 | `STR` | 0.8 | 1.1 |
| Wand | `Wand_Bolt` | 魔弾 | 8.0 | `INT` | 0.8 | 1.4 |
| Grimoire | `Grimoire_Bolt` | 呪弾 | 6.0 | `INT` | 0.7 | 1.3 |
| Bible | `Bible_Smite` | 裁制 | 6.0 | `FAI` | 0.7 | 1.5 |
| Rosary | `Rosary_Strike` | 聖撃 | 4.0 | `FAI` | 0.6 | 1.3 |

## 杖

| スキル | 日本語名 | 射程 | 範囲 | 主ステ | 係数 | CT | 備考 |
|---|---|---:|---:|---|---:|---:|---|
| `Wand_Bolt` | 魔弾 | 8.0 | - | `INT` | 0.8 | 1.4 | 遠いほど高威力 |
| `Wand_ArcaneBlast` | 極大魔弾 | 12.0 | - | `INT` | 1.2 | 7.0 | 単体高火力 |
| `Wand_AreaBlast` | 範囲魔法 | 9.0 | 半径3.0 | `INT` | 0.5 | 5.0 | 範囲攻撃 |
| `Wand_GodsHand` | 神の手 | 10.0 | - | `INT` | 1.6 | 9.0 | 単体超高火力 |

## 魔導書

| スキル | 日本語名 | 射程 | 効果 | CT | 備考 |
|---|---|---:|---|---:|---|
| `Grimoire_Bolt` | 呪弾 | 6.0 | `INT x0.7` | 1.3 | 単体攻撃 |
| `Grimoire_StrDebuff` | STRデバフ | 7.0 | `STR x0.7` に低下 | 5.0 | 5秒 |
| `StatDebuff_INT` | INTデバフ | 7.0 | `INT x0.7` に低下 | 5.0 | 5秒 |
| `StatDebuff_FAI` | FAIデバフ | 7.0 | `FAI x0.7` に低下 | 5.0 | 5秒 |
| `StatDebuff_AGI` | AGIデバフ | 7.0 | `AGI x0.7` に低下 | 5.0 | 5秒 |
| `Grimoire_Bind` | 金縛り | 4.0 | 拘束 | 7.0 | 3秒 |
| `Grimoire_Poison` | 毒 | 6.0 | 2 dmg/tick | 6.0 | 5秒、1秒ごと |
| `Grimoire_Stealth` | 不可視 | 自己 | 不可視 | 7.0 | 5秒 |

## 聖書

| スキル | 日本語名 | 射程 | 効果 | CT | 備考 |
|---|---|---:|---|---:|---|
| `Bible_Smite` | 裁制 | 6.0 | `FAI x0.7` | 1.5 | 単体攻撃 |
| `Bible_StrBuff` | 守護 | 味方/自己 | `STR x1.25` | 5.0 | 5秒 |
| `Bible_IntBuff` | INTバフ | 味方/自己 | `INT x1.25` | 5.0 | 5秒 |
| `Bible_FaiBuff` | 信仰バフ | 味方/自己 | `FAI x1.2` | 6.0 | 6秒 |
| `Bible_AgiBuff` | AGIバフ | 味方/自己 | `AGI x1.25` | 5.0 | 5秒 |
| `Bible_Invulnerable` | 無敵 | 自己 | 無敵 | 8.0 | 3秒 |
| `Bible_Gotsume` | ゴツメ | 6.0 | 反射4 dmg | 7.0 | 5秒 |
| `Bible_CarryRush` | 高速移動 | 4.0 | 移動速度 `x1.8` | 8.0 | 4秒 |

## ロザリオ

| スキル | 日本語名 | 射程 | 範囲 | 効果 | CT | 備考 |
|---|---|---:|---:|---|---:|---|
| `Rosary_Strike` | 聖撃 | 4.0 | - | `FAI x0.6` | 1.3 | 単体攻撃 |
| `Rosary_DistantHeal` | 遠隔癒し | 9.0 | - | `FAI x0.45` | 3.5 | 近いほど高回復 |
| `Rosary_CloseHeal` | 大回復 | 3.0 | - | `FAI x1.1` | 6.0 | 単体大回復 |
| `Rosary_Regeneration` | 継続回復 | 5.0 | - | 5 heal/tick | 6.0 | 5秒、1秒ごと |
| `Rosary_HealingArea` | 回復エリア | 7.0 | 半径3.0 | 3 heal/tick | 7.0 | 5秒、1秒ごと |
| `Rosary_SacrificeThunder` | 神の雷 | 認識敵全員 | 全体 | `FAI x0.9` | 9.0 | 自傷8 |

## 直接いじる場所

- 通常攻撃の武器アセット調整:
  - `Assets/Data/CombatFields/Weapon/BibleWeaponConfig.asset`
  - `Assets/Data/CombatFields/Weapon/GrimoireWeaponConfig.asset`
  - `Assets/Data/CombatFields/Weapon/RosaryWeaponConfig.asset`
  - `Assets/Data/CombatFields/Weapon/ShieldWeaponConfig.asset`
  - `Assets/Data/CombatFields/Weapon/SwordWeaponConfig.asset`
  - `Assets/Data/CombatFields/Weapon/WandWeaponConfig.asset`
- スキル個別の数値調整:
  - `Assets/Scripts/Combat/Skills/Implementations/`
- バフ/デバフ共通値調整:
  - `Assets/Scripts/Combat/Skills/CombatSkillFactory.cs`

## 調整時の注意

- `SkillDefinition` アセットでは射程・威力は変わらない。
- 武器はダメージそのものではなく、主ステ補正を持つ。
- 武器ごとの主ステ補正値は固定表ではなく、各 `WeaponConfig.asset` を見る。
- ダメージ/回復の基礎値は、直接攻撃系と単体回復系では使っていない。
- 敵対象スキルは、見えている敵だけでなく認識中の敵も対象に含める。
- 認識は、直接視認した情報と味方共有の情報を最後に得てから 5 秒保持する。
- `Rosary_SacrificeThunder` の自傷は通常ダメージ扱いなので、無敵や肩代わりの影響を受ける。
- `Wand_Bolt` と `Rosary_DistantHeal` は距離補正込みで見ないと見た目より数値差が大きい。
