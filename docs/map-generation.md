# マップ生成設計

戦闘マップを「テンプレート（スタンプ）合成」で生成するための設計メモ。
実装の細部（各クラスのフィールド・メソッドシグネチャ）は実装中に固めるため、ここでは **責務と関係性** のレベルに留める。

---

## 1. ゴール

- 高低差・水辺・地面状態・オブジェクト散布を組み合わせてバトル用マップを生成する
- 川が氾濫しない（常に周囲より低い谷として存在する）
- デザイナー不在を前提に、パラメトリックに形状を定義する

## 2. モデル：3 レイヤー構成

マップは次の 3 レイヤーで表現する。「旧地形」という単一グリッドに全てを詰める方式から、責務ごとにレイヤーを分けた形に再整理した。

| レイヤー | 保持データ | 種類 |
|---|---|---|
| **地形**（高低差） | `HeightMap`（float の 2D 配列） | 連続値 |
| **地面の状態** | `GroundStateGrid`（`GroundState` の 2D 配列、排他：Water > Snow > Swamp > Normal） | 沼地 / 積雪 / 水 / Normal |
| **オブジェクト** | `PlacedFeature` リスト | 木 / 岩 / 魔石 / 橋 |

原則：
- 山 / 丘 / 崖 は **地形** レイヤーのみを変更する（状態タグは付けない）
- 川 / 湖 は **地形を掘る** + **地面状態を Water に塗る** の両方を行う
- 森 は **木オブジェクトのクラスター**。地面状態は塗らず、`ForestRegion` を登録して後続フェーズ（RockPhase 等）が避けるのに使う

## 3. 採用した方針

### 3.1 表現形式
- **非タイル**（連続 3D 地形）
- Unity Terrain をベースに高さを作る
- 見た目は連続だが、ゲームロジック用に **裏側で低解像度の `GroundStateGrid` を保持** する

### 3.2 生成方式
- **ハイトマップ・スタンプ合成**
- 「スタンプ（= テンプレート）」を順に適用して地形を作る
- スタンプは合成規則（加算 / 上書き / 最小値 / 最大値）を持つ

### 3.3 適用順
**方針：水が先、山は水を避けて後置、オブジェクトは最後に載せる**。物理的な因果（高度が川を決める）を逆転させ、ゲーム的に扱いやすい順で作る。

1. **ベース高度**：全セルを BaseHeight で初期化（`BaseHeightPhase`）
2. **川**：平地状態でマップ端 → マップ端を Perlin ノイズで蛇行する経路を作り掘削 & `Water` 塗布（`RiverPhase` + `FlatRiverPathBuilder`）
3. **湖**：円形のくぼみを掘って水面ディスクを張り、`Water` 塗布（`LakePhase`）
4. **大構造**：山・丘・盆地（`HeightStamp`）。Water セル（川＋湖）に被らない中心だけを採用する棄却サンプリング
5. **地面状態パッチ**：沼地・積雪（`GroundPatchStampShape`、Water は上書きしない）
6. **森**：木のクラスターを配置（`ForestClusterStampShape`）。`ForestRegion` を登録 + `PlacedFeature.Tree` を散布
7. **岩**：マップ全体に岩をランダム散布（Water セルと `ForestRegion` を避ける）
8. **装飾**：魔石（自陣営=下 1/3、敵陣営=上 1/3、それぞれメイン/サブの 4 種別で配置。Water セルを除外）
9. **橋**：各川に N 個、川の接線に垂直に架ける（`BridgePhase`）

### 3.4 川の氾濫防止
- 川の合成規則は **最小値選択（Min）** 固定
- 川先行パイプラインでは、川を平地（BaseHeight）上に掘ってから山を配置するため、川が谷として成立することが自動的に保証される
- 山は `StructurePhase` が「Water セルに被らない中心」だけを採用する棄却サンプリングで配置する

### 3.5 他の主要決定
- **対称性**：不要（ランダム配置、公平性は保証しない）
- **規模**：中（ワールド 60m × 60m 相当）
- **形状定義**：パラメトリック（数値指定、SO 内で完結）
- **川の見た目**：経路に沿ったリボンメッシュ。セル中心ではなく両端はマップ辺にスナップしてメッシュが端まで届くようにしている
- **MapQuery**：小さく始める（最小 API のみ用意、必要時に拡張）

---

## 4. レイヤー構造

```text
[Config 層]       MapGenerationConfig (SO)
                          │
                          ▼
[Generation 層]   MapGenerator
                    ├─ BaseHeightPhase
                    ├─ RiverPhase
                    ├─ LakePhase
                    ├─ StructurePhase
                    ├─ GroundPatchPhase
                    ├─ ForestPhase
                    ├─ RockPhase
                    ├─ DecorationPhase
                    └─ BridgePhase
                          │
                          ▼
[Data 層]         MapData  (Unity 非依存の純粋データ)
                          │
                  ┌───────┴───────┐
                  ▼               ▼
[Query 層]     MapQuery     [Render 層] TerrainRenderer
```

原則：
- **MapData は純粋データ**（Unity 型・MonoBehaviour に依存しない）
- **Query と Render は MapData を読むだけ**（書き込まない）
- 生成フェーズは Strategy パターンで分離（差し替え可能）

---

## 5. クラス一覧と責務

### Data 層

| クラス | 責務 |
|---|---|
| `MapData` | `HeightMap` / `GroundStateGrid` / `PlacedFeature` / `RiverPath` / `LakeRegion` / `ForestRegion` 群を保持するだけのデータコンテナ |
| `HeightMap` | 高度の 2D 配列 + セルサイズ。`SampleAt(worldPos)` などのサンプリングを提供 |
| `GroundStateGrid` | `GroundState` の 2D 配列。HeightMap より粗い解像度で OK |
| `GroundState` | enum（`Normal / Swamp / Snow / Water`）。排他、優先度 Water > Snow > Swamp > Normal |
| `FeatureType` | enum（`OwnMainStone / OwnSubStone / EnemyMainStone / EnemySubStone / Tree / Rock / Bridge`） |
| `PlacedFeature` | 木・岩・魔石・橋の情報（`type`, `worldPosition`, `rotation`） |
| `RiverPath`（struct） | 川 1 本分のセル経路と幅・深さ。`RiverRenderer` がメッシュ化する際に参照 |
| `LakeRegion`（struct） | 湖 1 つ分の中心・半径・水面 Y。`LakeRenderer` が水面ディスクを張る際に参照 |
| `ForestRegion`（struct） | 森クラスター 1 つ分の中心・半径。`RockPhase` が配置を避けるのに参照 |

### Stamp（テンプレート）

**Shape（形状データ、SO）** と **Placement（配置情報、値オブジェクト）** を分離する。
同じ Shape を複数箇所に異なる配置で使い回せるようにするため。

**用語（混同しない）**：`HeightShapeKind` は実装の分岐が Dome / Cone / Ridge の 3 つだけ。`MapGenerationConfig` の `Structure Stamps` に並ぶのは別々の `HeightStampShape` アセット（プリセット）であり、名前の違う山・丘・盆地を何種類でも並べられる。同じプリセットを複数要素にすると抽選で出やすくなる。

| クラス | 責務 |
|---|---|
| `StampShape`（抽象 SO） | 全スタンプの基底。`Apply(MapData, StampPlacement)` を持つ |
| `HeightStampShape` | 高度を変える。パラメトリック定義（形状種・半径・最大高さ・減衰カーブ・**輪郭ノイズ**・合成規則） |
| `GroundPatchStampShape` | 地面状態（沼・雪）を塗る。高度は変えない。`MaxHeight` で「平地限定」に絞れる。輪郭ノイズで非真円も可 |
| `ForestClusterStampShape` | 森クラスター：`ForestRegion` を登録 + `PlacedFeature.Tree` を円内に散布 |
| `LakeStampShape` | 湖を配置。円形のボウル型くぼみを Min 合成で掘り、`GroundStateGrid` を Water 塗布し、`LakeRegion` を登録 |
| `RiverShape` | 川 1 本分の見た目パラメータ（幅・深さ・Water タグ比率）。`RiverPhase` から参照 |
| `StampPlacement`（struct） | 配置情報（中心・回転・スケール） |
| `HeightBlendMode`（enum） | `Add / Overwrite / Min / Max` |

### Generation 層

| クラス | 責務 |
|---|---|
| `MapGenerator` | 司令塔（MonoBehaviour）。`Generate()` で全フェーズを順に回す |
| `IMapGenerationPhase` | フェーズ共通インターフェース |
| `BaseHeightPhase` | 全セルを `BaseHeight` で初期化する最初のフェーズ |
| `RiverPhase` | マップ端 → 別のマップ端を Perlin ノイズで蛇行させる横断大河を生成（`FlatRiverPathBuilder` を使用） |
| `LakePhase` | `LakeStampShape` をランダムに配置して湖を生成する |
| `StructurePhase` | 大構造（山・丘・盆地）を配置。非一様スケール + 回転を付与。Water セルに被る候補は棄却して `StructureMaxPlacementAttempts` 回リトライ |
| `GroundPatchPhase` | 沼・雪などの地面状態パッチを配置 |
| `ForestPhase` | 森クラスターを配置。各スタンプが `ForestRegion` を登録 + `Tree` feature を散布 |
| `RockPhase` | 岩をマップ全体に散布。Water セル / `ForestRegion` / 既存岩の最小距離 を避ける棄却サンプリング |
| `DecorationPhase` | 魔石（4 種別）を陣営ゾーン内に配置 |
| `BridgePhase` | 各川の経路上に橋を配置 |

### River 関連の補助クラス

| クラス | 責務 |
|---|---|
| `FlatRiverPathBuilder` | 高度情報に依存せず、マップ端 → マップ端を Perlin ノイズで蛇行させるセル列を作る |
| `RiverShape.Carve` | 経路に沿って高度マップを Min 合成で掘削し、`GroundStateGrid` を Water 塗布する |

### Config 層

| クラス | 責務 |
|---|---|
| `MapGenerationConfig`（SO） | 解像度・ワールドサイズ・使用 Shape 群・各フェーズのパラメータをまとめて保持 |

### Query 層

| クラス | 責務 |
|---|---|
| `MapQuery` | AI・戦闘側がマップ情報を問い合わせる入口。**最小構成でスタート** |

初期 API（拡張は必要時）：
- `GroundState GetGroundAt(Vector3 worldPos)`
- `float GetHeightAt(Vector3 worldPos)`

### Render 層

| クラス | 責務 |
|---|---|
| `TerrainRenderer` | `MapData.Height` → Unity 標準 Terrain の高さ、`MapData.GroundStates` → スプラットマップ（Normal/Swamp/Snow/Water の 4 レイヤ）。URP 用 Terrain マテリアルと単色 TerrainLayer を自動生成する |
| `RiverMeshBuilder`（static） | `RiverPath` + `HeightMap` からリボンメッシュ（水面）を生成する純粋ロジック |
| `RiverRenderer` | `MapData.Rivers` をもとに `RiverMeshBuilder` を呼び、川 GameObject を `GeneratedRivers` 配下にまとめて生成 |
| `LakeRenderer` | `MapData.Lakes` から水面ディスクメッシュを生成し、湖 GameObject を `GeneratedLakes` 配下にまとめて生成 |
| `BridgeRenderer` | `MapData.Features` の Bridge を拾い、Cube メッシュでインスタンス化 |
| `(未実装) TreeRenderer / RockRenderer` | `PlacedFeature.Tree` / `Rock` のプレハブ Instantiate（今後追加） |

方針：
- **Render 層は読み取り専用**：`MapData` に書き戻さない
- **生成 GameObject は全て子配下**にまとめ、再生成時にまるごと差し替える（差分管理しない）

### Util

| クラス | 責務 |
|---|---|
| `IRandom` | シード対応 RNG の抽象。テスト容易性のため |

---

## 6. 生成時のデータフロー

```text
1. MapData(empty) を作成

2. BaseHeightPhase
   - 全セルを BaseHeight（既定 0）で初期化

3. RiverPhase
   - CrossMapRiverCount 本を生成：
     - 4 辺から 2 辺を選び辺上の始点・終点を決定
     - FlatRiverPathBuilder で Perlin 蛇行パスを生成（短すぎる経路はリトライ→それでも短ければ最長を採用）
     - RiverShape.Carve で HeightMap を掘削 + GroundStateGrid を Water 塗布 + RiverPath を登録

4. LakePhase
   - LakeStampShape をランダム位置に LakeCount 個配置
   - ボウル型 Min 合成掘削 + 半径に応じて Water 塗布 + LakeRegion を登録

5. StructurePhase
   - 中心候補を出し、baseRadius*1.3 + StructureRiverClearance 円内に Water セルがあれば棄却して再抽選
   - 非一様スケール（X/Y 独立）+ ランダム回転でスタンプ適用

6. GroundPatchPhase
   - GroundPatchStampShape（Swamp / Snow）をランダム散布
   - Water は上書き禁止、MaxHeight 指定時は高所にも塗らない

7. ForestPhase
   - ForestClusterStampShape を ForestClusterCount 個配置
   - 各クラスターが ForestRegion を登録し、円内に Tree feature を散布

8. RockPhase
   - マップ全体にランダム散布。Water セル / ForestRegion 内 / 既存岩 MinDistance 未満 は棄却

9. DecorationPhase
   - 魔石を陣営 × 役割で 4 種別として配置
     - Own* = 下 1/3（Z 小）、Enemy* = 上 1/3（Z 大）
     - メインはゾーン内でさらに奥寄り（MainStoneBackBias）
     - Water セル除外 + 全魔石間 MagicStoneMinDistance

10. BridgePhase
    - 各 RiverPath の経路を BridgesPerRiver + 1 に等分し、接線に垂直な橋（PlacedFeature.Bridge）を追加

→ MapData 完成 → 各 Renderer に渡して可視化
```

---

## 7. 設計上の原則（将来判断に迷ったときの指針）

- **生成 / 保持 / 描画 / 問い合わせ を分離する**：1 つのクラスに混ぜない
- **MapData は Unity 非依存**：ユニットテスト可能にする
- **3 レイヤの責務を混ぜない**：地形（高度）/ 地面（状態）/ オブジェクト（点配置）
- **Shape はパラメトリック**：`float[,]` のような重たいデータを持たない
- **拡張は後から**：MapQuery など最小構成で始め、必要になったら足す
- **川の物理的整合性は合成規則で守る**：`Min` 固定という仕様をクラスに閉じ込める

---

## 8. 未決定・将来検討

- 天候レイヤーの接続方法（マップ生成とは独立レイヤーを想定）
- 木・岩のプレハブ化（現状はエディタプレビューの点描画のみ、3D 描画は未実装）
- AI 用 API の追加タイミングと粒度
- セーブ／リプレイ用のシード再現
