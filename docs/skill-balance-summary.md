# スキル射程・威力 調整メモ

現状のスキル調整値は主に2か所に分かれている。

- 武器の射程・CT・主ステ補正: `Assets/Data/CombatFields/Weapon/*.asset`
- 各スキルの射程・係数・持続時間: `Assets/Scripts/Combat/Skills/Implementations/*.cs` と `Assets/Scripts/Combat/Skills/CombatSkillFactory.cs`

`Assets/Data/Combat/Skills/*.asset` の `SkillDefinition` は表示名と必要武器種の管理が中心で、射程や威力は持っていない。

## 設計基準（バランスアンカー）

数値は以下の基準を前提に決める。個別スキルを調整するときは、まずこの基準へ立ち返る。

### 環境前提

| 項目 | 値 | 補足 |
|---|---:|---|
| マップ一辺 | 60m | 端から端まで約60m |
| 移動速度 | 2 m/s | 60mを横断するのに約30秒 |
| AI判断間隔 | 0.5s | 行動再選択の周期 |
| 最大視界 | 30m | これより遠い敵は認識対象外 |

### キャラ基準

| 項目 | 値 | 補足 |
|---|---:|---|
| 基礎ステータス（標準） | 30 | 想定レンジ 20〜45 |
| 実効ステータス（標準） | 約40 | 基礎30 + 武器主ステ補正の想定合算 |
| 最大HP（標準） | 120 | TTK設計の基準HP |

> 現状のテストシーンは HP50・基礎ステ0 のままで、この基準は反映していない。実挙動で基準通りの数値を確認するには、`CharacterData` 側で基礎ステ約30・HP120 を与える必要がある（別作業）。

### 威力・射程・CTの基準

- 通常攻撃1発の目安ダメージ = `実効ステ40 × 係数`。TTKは通常攻撃 6〜8発（120HP）を標準とする。
  - 主火力（Sword / Wand）: 係数 0.4 → 約16ダメージ
  - 支援兼務（Grimoire / Bible）: 係数 0.35 → 約14ダメージ
  - 補助職（Shield / Rosary）: 係数 0.3 → 約12ダメージ
- バースト攻撃は「通常攻撃の複数発分」を1回で出す代わりに長いCTを持つ。
  - 中バースト（極大魔弾 0.9 ≒ 通常2.3発）: CT 7s
  - 大バースト（神の手 1.3 ≒ 通常3.3発）: CT 9s
- 射程はマップ60m・視界30mに対し、遠隔は 8〜16m を上限帯とする（狙撃でも視界の半分程度に収める）。
  - 近接: 2〜4m
  - 遠隔通常: 8〜10m
  - 遠隔バースト: 12〜16m
  - 支援（回復/バフ/デバフ）: 3〜10m
- 回復は「通常攻撃1〜2発分を返す」を基準にし、単発大回復ほど長いCTにする。
- 状態異常・デバフの持続は 3〜5s、CT は効果の強さに応じて 5〜7s を基準とする。

## 詠唱時間

詠唱時間は各スキル実装の `CastTimeSeconds` にある。魔法系4武器は以下の初期値を使用し、クールダウンは詠唱完了後に開始する。

| 武器 | スキル | 詠唱秒 |
|---|---|---:|
| Wand | `Wand_Bolt` | 0.6 |
| Wand | `Wand_ArcaneBlast` | 1.5 |
| Wand | `Wand_AreaBlast` | 1.5 |
| Wand | `Wand_GodsHand` | 2.5 |
| Grimoire | `Grimoire_Bolt` | 0.7 |
| Grimoire | STR/INT/FAI/AGIデバフ | 1.0 |
| Grimoire | `Grimoire_Bind` | 1.4 |
| Grimoire | `Grimoire_Poison` | 1.1 |
| Grimoire | `Grimoire_Stealth` | 0.8 |
| Bible | `Bible_Smite` | 0.7 |
| Bible | STR/INT/FAI/AGIバフ | 0.9 |
| Bible | `Bible_Invulnerable` | 1.2 |
| Bible | `Bible_Gotsume` | 1.0 |
| Bible | `Bible_CarryRush` | 1.2 |
| Rosary | `Rosary_Strike` | 0.6 |
| Rosary | `Rosary_DistantHeal` | 0.9 |
| Rosary | `Rosary_CloseHeal` | 1.3 |
| Rosary | `Rosary_Regeneration` | 1.0 |
| Rosary | `Rosary_HealingArea` | 1.5 |
| Rosary | `Rosary_SacrificeThunder` | 2.5 |

`Sword` と `Shield` のスキルは0秒で即時実行する。

## 現在の計算式

- 攻撃/回復の基本式: `最終値 = 実効ステータス × スキル係数`
- 実効ステータス: `(キャラ基礎ステータス + 武器の主ステ補正) × バフ/デバフ倍率`
- 武器は直接ダメージを持たず、対応する主ステータスを上げる

## 共通補正

- ステルス奇襲補正: 対象が術者を未認識ならダメージ `x1.5`
- 距離依存ダメージ:
  - `Wand_Bolt`: 近距離 `x0.7` -> 遠距離 `x1.3`
- 距離依存回復:
  - `Rosary_DistantHeal`: 近距離 `x1.4` -> 遠距離 `x0.8`

## 通常攻撃

実効ステ40想定のダメージ目安を併記する。TTKは通常攻撃6〜8発（120HP）が標準。

| 武器 | 実装スキル | 日本語名 | 射程 | 主ステ | スキル係数 | CT | 目安ダメージ |
|---|---|---|---:|---|---:|---:|---:|
| Sword | `Sword_Slash` | 斬撃 | 2.0 | `STR` | 0.4 | 1.0 | 16 |
| Shield | `Shield_Slash` | 盾撃 | 2.0 | `STR` | 0.3 | 1.1 | 12 |
| Wand | `Wand_Bolt` | 魔弾 | 10.0 | `INT` | 0.4 | 1.4 | 11〜21 |
| Grimoire | `Grimoire_Bolt` | 呪弾 | 8.0 | `INT` | 0.35 | 1.3 | 14 |
| Bible | `Bible_Smite` | 裁制 | 8.0 | `FAI` | 0.35 | 1.5 | 14 |
| Rosary | `Rosary_Strike` | 聖撃 | 4.0 | `FAI` | 0.3 | 1.3 | 12 |

## 杖

| スキル | 日本語名 | 射程 | 範囲 | 主ステ | 係数 | CT | 備考 |
|---|---|---:|---:|---|---:|---:|---|
| `Wand_Bolt` | 魔弾 | 10.0 | - | `INT` | 0.4 | 1.4 | 遠いほど高威力（約11〜21） |
| `Wand_ArcaneBlast` | 極大魔弾 | 15.0 | - | `INT` | 0.9 | 7.0 | 中バースト（約36、通常2.3発） |
| `Wand_AreaBlast` | 範囲魔法 | 12.0 | 半径3.0 | `INT` | 0.35 | 5.0 | 範囲攻撃（1体約14） |
| `Wand_GodsHand` | 神の手 | 16.0 | - | `INT` | 1.3 | 9.0 | 大バースト（約52、通常3.3発） |

## 魔導書

| スキル | 日本語名 | 射程 | 効果 | CT | 備考 |
|---|---|---:|---|---:|---|
| `Grimoire_Bolt` | 呪弾 | 8.0 | `INT x0.35` | 1.3 | 単体攻撃（約14） |
| `Grimoire_StrDebuff` | STRデバフ | 8.0 | `STR x0.7` に低下 | 5.0 | 5秒 |
| `StatDebuff_INT` | INTデバフ | 8.0 | `INT x0.7` に低下 | 5.0 | 5秒 |
| `StatDebuff_FAI` | FAIデバフ | 8.0 | `FAI x0.7` に低下 | 5.0 | 5秒 |
| `StatDebuff_AGI` | AGIデバフ | 8.0 | `AGI x0.7` に低下 | 5.0 | 5秒 |
| `Grimoire_Bind` | 金縛り | 6.0 | 拘束 | 7.0 | 3秒 |
| `Grimoire_Poison` | 毒 | 6.0 | 4 dmg/tick | 6.0 | 5秒、1秒ごと（計約20） |
| `Grimoire_Stealth` | 不可視 | 自己 | 不可視 | 7.0 | 5秒 |

## 聖書

| スキル | 日本語名 | 射程 | 効果 | CT | 備考 |
|---|---|---:|---|---:|---|
| `Bible_Smite` | 裁制 | 8.0 | `FAI x0.35` | 1.5 | 単体攻撃（約14） |
| `Bible_StrBuff` | 守護 | 味方/自己 | `STR x1.25` | 5.0 | 5秒 |
| `Bible_IntBuff` | INTバフ | 味方/自己 | `INT x1.25` | 5.0 | 5秒 |
| `Bible_FaiBuff` | 信仰バフ | 味方/自己 | `FAI x1.2` | 6.0 | 6秒 |
| `Bible_AgiBuff` | AGIバフ | 味方/自己 | `AGI x1.25` | 5.0 | 5秒 |
| `Bible_Invulnerable` | 無敵 | 自己 | 無敵 | 8.0 | 3秒 |
| `Bible_Gotsume` | ゴツメ | 6.0 | 反射8 dmg | 7.0 | 5秒 |
| `Bible_CarryRush` | 高速移動 | 4.0 | 移動速度 `x1.8` | 8.0 | 4秒 |

## ロザリオ

| スキル | 日本語名 | 射程 | 範囲 | 効果 | CT | 備考 |
|---|---|---:|---:|---|---:|---|
| `Rosary_Strike` | 聖撃 | 4.0 | - | `FAI x0.3` | 1.3 | 単体攻撃（約12） |
| `Rosary_DistantHeal` | 遠隔癒し | 10.0 | - | `FAI x0.4` | 3.5 | 近いほど高回復（約13〜22） |
| `Rosary_CloseHeal` | 大回復 | 3.0 | - | `FAI x0.9` | 6.0 | 単体大回復（約36） |
| `Rosary_Regeneration` | 継続回復 | 5.0 | - | 7 heal/tick | 6.0 | 5秒、1秒ごと（計約35） |
| `Rosary_HealingArea` | 回復エリア | 7.0 | 半径3.0 | 4 heal/tick | 7.0 | 5秒、1秒ごと（計約20） |
| `Rosary_SacrificeThunder` | 神の雷 | 認識敵全員 | 全体 | `FAI x0.7` | 9.0 | 自傷12（約28） |

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
