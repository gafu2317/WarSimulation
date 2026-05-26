# 戦闘スキル実装メモ

`docs/game.md` の「技」設計に対する、現時点のコード実装状況をまとめる。

## 設計上の前提

- **通常攻撃という独立概念はない**。設計書の「通常の斬撃」「通常の攻撃」も含め、すべて `SkillBase` として実装する。
- **戦闘時は武器種（`WeaponKind`）に合う技だけ使える**（双剣・盾・杖・魔導書・聖書・ロザリオ）。
- **習得**（職業施設レベル）と **武器個体**（性能・専用技）は分離する。国家シミュ側の習得管理は未接続。

## 3層アーキテクチャ

```
使える SkillId
  = (キャラ習得済み ∩ RequiredWeaponKind == 装備Kind)
  ∪ (武器付与 ∩ RequiredWeaponKind == 装備Kind)

↓ CombatSkillFactory

Character.AvailableCombatSkills  ← AI / 実行が参照する唯一のリスト
```

| 層 | 役割 | 主な型 |
|----|------|--------|
| マスタ | 技ID・必要武器種・将来用施設Lv | `SkillId`, `SkillDefinition`, `CombatSkillCatalog` |
| キャラ習得 | 国家で解放される技（後日） | `Character._learnedSkillIds` |
| 武器個体 | 性能 + レア専用技 | `WeaponConfig._grantedSkillIds` → `WeaponBase.GrantedSkillIds` |

### 開発用スタブ

`_learnedSkillIds` が空で、`_unlockAllCatalogSkillsForKindWhenLearnedEmpty == true`（デフォルト）のとき、
装備中 `WeaponKind` に一致するカタログ内スキルをすべて習得済み扱いにする。
国家シミュ接続前の開発・確認用。

## 実行フロー

```
PersonalityBase.Update()
  └─ Tick()
       ├─ DecidePlan()     … PlainPersonality が目的・移動を選択（スキルは null）
       ├─ ExecuteMove()
       └─ ExecuteSkill()    … plan.Skill が null なら no-op。非 null なら Execute + StartCooldown
```

現状の `PlainPersonality` は **目的に基づく移動のみ**（敵魔石が分かれば向かう、否则索敵移動）。スキル選択は Personality 側の将来実装。`PersonalityBase.ExecuteSkill` とスキル実行基盤（`Execute` / CD / loadout）は温存している。

## ファイル配置

すべて `Assets/Scripts/Combat/Skills/` 配下に集約する。

```
Combat/Skills/
├── SkillBase.cs
├── SkillTargetKind.cs
├── SkillId.cs
├── SkillDefinition.cs
├── CombatSkillCatalog.cs
├── CombatSkillFactory.cs
├── CombatSkillLoadoutBuilder.cs
├── IdentifiedSkill.cs
└── Implementations/          … 各スキルの Execute 実装
    ├── SwordSlashSkill.cs
    └── ...
```

### 基盤

| パス | 内容 |
|------|------|
| `Assets/Scripts/Combat/Skills/SkillBase.cs` | スキル抽象クラス |
| `Assets/Scripts/Combat/Skills/SkillTargetKind.cs` | ターゲット種別 |
| `Assets/Scripts/Combat/Skills/SkillId.cs` | 技ID列挙 |
| `Assets/Scripts/Combat/Skills/SkillDefinition.cs` | マスタ SO |
| `Assets/Scripts/Combat/Skills/CombatSkillCatalog.cs` | カタログ SO + `CreateDefaultRuntimeCatalog()` |
| `Assets/Scripts/Combat/Skills/CombatSkillFactory.cs` | ID → 実装クラス |
| `Assets/Scripts/Combat/Skills/CombatSkillLoadoutBuilder.cs` | 3層合成 |
| `Assets/Scripts/Combat/Skills/IdentifiedSkill.cs` | `CooldownKey = SkillId.ToString()` のラッパー |

### スキル実装

`Assets/Scripts/Combat/Skills/Implementations/` に `SkillBase` 継承クラスを置く。

### 接続

| パス | 内容 |
|------|------|
| `Assets/Scripts/Combat/Characters/Chracter.cs` | `AvailableCombatSkills`, `RebuildCombatSkills()` |
| `Assets/Scripts/Combat/Weapons/WeaponConfig.cs` | `GrantedSkillIds` |
| `Assets/Scripts/Combat/Characters/personality/PersonalityBase.cs` | スキル実行 |
| `Assets/Scripts/Combat/Characters/personality/PlainPersonality.cs` | スキル選択 |
| `Assets/Scripts/SceneContexts/CombatSceneContext.cs` | `SkillCatalog` 参照 |

### データアセット

```
Assets/Data/Combat/Skills/
├── CombatSkillCatalog.asset
└── *SkillDefinition.asset（6件）
```

カタログ解決の優先順位:

1. `Character._skillCatalogOverride`
2. `CombatSceneContext.SkillCatalog`
3. `CombatSkillCatalog.CreateDefaultRuntimeCatalog()`（フォールバック）

## 実装済みスキル

### 通常攻撃（武器ごと1ファイル）

| SkillId | 武器種 | クラス | 係数 | 射程 | CD | 概要 |
|---------|--------|--------|------|------|-----|------|
| `Sword_Slash` | 双剣 | `SwordSlashSkill` | STR | 2m | 1.0s | 斬撃 |
| `Shield_Slash` | 盾 | `ShieldSlashSkill` | STR | 2m | 1.1s | 盾撃（双剣よりやや弱め） |
| `Wand_Bolt` | 杖 | `WandBoltSkill` | INT | 8m | 1.4s | 魔弾 |
| `Wand_ArcaneBlast` | 杖 | `WandArcaneBlastSkill` | INT | 15m | 8.0s | 極大魔弾（長CD・高威力） |
| `Grimoire_Bolt` | 魔導書 | `GrimoireBoltSkill` | INT | 6m | 1.3s | 呪弾 |
| `Bible_Smite` | 聖書 | `BibleSmiteSkill` | FAI | 5m | 1.5s | 制裁 |
| `Rosary_Strike` | ロザリオ | `RosaryStrikeSkill` | FAI | 4m | 1.3s | 聖撃 |

### 回復

| SkillId | 武器種 | クラス | 対象 | 射程 | CD | 効果 |
|---------|--------|--------|------|------|-----|------|
| `Rosary_DistantHeal` | ロザリオ | `RosaryDistantHealSkill` | 味方/自分 | 9m | 3.5s | `3 + FAI×0.3` 微回復 |
| `Rosary_CloseHeal` | ロザリオ | `RosaryCloseHealSkill` | 味方/自分 | 2.5m | 7.0s | `15 + FAI×0.8` 大回復 |

味方対象スキルは `PersonalityBase.IsValidSkillTarget` でも `MaxRange` を参照する（自分自身は距離不問）。

### ステータスバフ（`StatBuffSkill`・聖書専用）

| SkillId | 対象ステ | 武器種（カタログ） | 表示名 |
|---------|----------|-------------------|--------|
| `Bible_StrBuff` | STR | 聖書 | 守護 |
| `Bible_IntBuff` | INT | 聖書 | INTバフ |
| `Bible_FaiBuff` | FAI | 聖書 | 信仰バフ |
| `Bible_AgiBuff` | AGI | 聖書 | AGIバフ |

### ステータスデバフ（`StatDebuffSkill`）

| SkillId | 対象ステ | 武器種（カタログ） | 表示名 |
|---------|----------|-------------------|--------|
| `Grimoire_StrDebuff` | STR | 魔導書 | STRデバフ |
| `StatDebuff_INT` | INT | 魔導書 | INTデバフ |
| `StatDebuff_FAI` | FAI | 魔導書 | FAIデバフ |
| `StatDebuff_AGI` | AGI | 魔導書 | AGIデバフ |

バフ・デバフは `StatBuffSkill` / `StatDebuffSkill` に集約。EffectKey は `StatBuff_{Stat}` / `StatDebuff_{Stat}`。

`docs/game.md` には武器ごと約5技が定義されているが、現時点では上記17件（通常攻撃7 + 回復2 + バフ4 + デバフ4）。

## 非推奨・削除済みパターン

- `WeaponBase.Skills` への直書き → `[Obsolete]`、常に空。参照しない。
- 武器コンストラクタ内の `_skills = new ...` → 削除済み。
- `Combat/Weapons/Skills/` フォルダ → 削除済み（`Combat/Skills/` に統合）

## 新スキルを追加するとき

1. `Assets/Scripts/Combat/Skills/SkillId.cs` に enum を追加
2. `Assets/Scripts/Combat/Skills/Implementations/` に `SkillBase` 実装を追加
3. `Assets/Scripts/Combat/Skills/CombatSkillFactory.cs` に case を追加
4. `Assets/Data/Combat/Skills/` に `SkillDefinition` アセットを作成し `CombatSkillCatalog.asset` に登録
5. （任意）特定武器だけ使わせる → `WeaponConfig.GrantedSkillIds`
6. EditMode テストを追加

## テスト

| パス | 内容 |
|------|------|
| `Assets/Tests/EditMode/CombatSkillLoadoutBuilderTests.cs` | 習得 / 付与 / Kind フィルタ |
| `Assets/Tests/EditMode/CombatSkillExecutionTests.cs` | Execute + CD |
| `Assets/Tests/EditMode/PlainPersonalityTests.cs` | AI スキル選択 |
| `Assets/Tests/EditMode/CombatHealthAttackTests.cs` | キャラ loadout |
| `Assets/Tests/EditMode/CombatEditModeTestUtil.cs` | カタログ・スキルリストのテスト用ヘルパ |

## 未実装（将来）

- 国家シミュ: 職業施設 Lv → `LearnedSkillIds` 追加
- 精霊リロールでの習得リセット
- `SkillDefinition.UnlockFacilityLevel` によるフィルタ
- 設計書の残りの技（詠唱・カウンター・AoE 等）
- プレイヤー援護魔法・煙幕
