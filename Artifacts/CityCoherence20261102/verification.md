# 全街区の用途・構造・石材尺度の見直し

2026-11-02。`Assets/Scenes/Country.unity` に反映・保存した。全域のEditor画像と検証結果を記録する。見た目はユーザー確認待ち。

![水車・河岸・桟橋の変更前後](comparison.png)

## 今回の基準と変更

前段のStreetLifeでは追加小物の交差だけを検査し、既存小物、配置図と別に作る設備、空間の使い方、モデル拡大後の石材尺度を十分に確認できていなかった。今回は指摘画像の地点に限定せず、共通する生成規則と保存シーン全域を対象にした。

- 通常の都市地面を共通の石敷きへつなぎ、歯状の裸土帯をなくした。菜園・花壇・樹木の根元は明示した土床にする。花壇の囲いを閉じ、休憩所は樹木と片側の腰掛けをまとめて中央の通行空間を残す。倉庫周辺は荷車・荷物、軍の庭は訓練用具を使う。
- 配置表の小物は573点から682点、庭は40から42区画へ更新。25か所の共同庭・作業区画を整えた。既存26点を交差検査に基づいて再配置し、便所・物干しの場所も整理した。全682点を建物・道路・入口・小物同士・別生成設備の予約範囲と検査する。立木は幹の占有範囲を使い、樹冠の張り出しとは区別する。
- 街路、敷地、入口、広場、石橋の歩行面は都市座標の2m/反復へ統一。新モデルの石壁4材質も、元のUV方向と円周の展開を保ちながら拡縮後の実寸へ補正する。小面の倍率を丸めて共有した際のUV崩れも修正した。
- 展示目的で道路へ重ねていた `Road_*` 5種類を撤去。不要な壁角・石階段も除き、モデル全種類の使用を完成条件にしない。新旧それぞれ122種類を採用した。
- 石橋の歩行面と内側面の向きを修正し、橋ごとの実際の床高・通行幅に岸側の接続を合わせた。桟橋は中央の木階段と支持材でつなぎ、階段上端を床高へ合わせる。両岸の手すりは橋・水車・荷揚げ場で開口を持つ。
- 水車の本流へ突き出す長い流路壁を新モデルの配置では非表示にし、車輪と軸支持を残す。作業囲いは実寸の柵と開く入口を持ち、便所・洗濯・備品を収める。既存荷車を柵の占有範囲から移した。
- 新しい闘技場は入口奥で全周につながっていた段席・外周輪を切り開き、左右の切断面も閉じた。元のFBX・Prefabは保持し、派生メッシュとシーンのオーバーライドを使う。

住宅を含む249棟、道路40本、入口249本、元の広場・緑地・監視塔の配置表は変更前と完全一致。[保持検査](After/layout_preservation.json) に記録した。

## 画像の確認範囲

変更前後それぞれ92枚。新モデルの20街区を俯瞰し、全40道路を高さ2.5mの視点で確認。さらに新旧各16か所の代表視点を撮影した。一覧で全域を確認し、河岸、桟橋、石畳、作業囲い、噴水周辺、闘技場は原寸でも確認した。

- [20街区の最終一覧](After/Grid_contact.png) / [40道路の最終一覧](After/Walk_contact.png)
- [施設前の石畳・作業囲いの変更前後](paving_comparison.png)
- [河岸](After/Streets/New_River.png)、[桟橋](After/Streets/New_QuayLanding.png)、[作業囲い](After/Streets/New_DeliveryYard.png)
- [噴水周辺の歩行視点](After/Streets/Walk_03.png)、[橋の接続](After/Streets/New_BridgeApproach.png)、[闘技場の入口](After/Streets/New_ArenaEntrance.png)
- 原寸画像は `Before/Streets/` と `After/Streets/`。一覧・比較画像は縮小、整列、ラベル追加だけを行った。

`BridgeApproach` の変更前はカメラが建物に入っていたため、変更後に位置を修正した。この1視点は同一視点の前後比較に使用しない。

## 検証

| 対象 | 最終結果 |
| --- | --- |
| 全配置表の小物 | [682点PASS](After/coherence_plan_validation.json)。建物・街路・入口、相互、別生成設備との予約範囲の交差0 |
| 保存後の建物 | [498棟PASS](After/saved_scene_validation.txt)。入口各249本 |
| 舗装の尺度 | [656面・10,268頂点PASS](paving_scale_validation.json)。不一致0、最大UV誤差0.00000763 |
| 石壁の尺度 | [4,776面・351,244三角形PASS](paving_scale_validation.json)。尺度不一致0、最大誤差約0.00013m/反復 |
| 橋・桟橋・闘技場・囲い | [PASS](After/structure_validation.json)。橋12岸の接続、桟橋2組の上端、闘技場2棟の入口レイ、囲いの柵接続を検査 |
| 城壁内の収まり | [PASS](After/inside_walls_validation.json)。62,286 Renderer、1,609,513頂点、外側0点 |
| モデルの使用 | [新旧各122種類PASS](After/model_usage_validation.json)。学校Lv1〜4と不使用の展示部品は不在 |
| 川と施設参照 | [水門・河川・水車PASS](After/river_validation.json)、[施設30対象PASS](After/scene_links.json) |
| 舗装・壁のEditModeテスト | [10件PASS](paving_test_status.json)。切り抜き、拡縮・回転、atlas保持、橋、石壁、小面の高UV座標を検査 |
| 闘技場のEditModeテスト | [1件PASS](arena_test_status.json)。前から中央へ通り抜け、背面段席と切断面が残ることを検査 |
| コンパイル・保存 | [エラー0](After/compilation_state.json)、[Country保存済み・isDirty=false](After/scene_info.json) |

## 面積監査の扱い

[変更前](Before/SpaceAudit/audit.json)と[変更後](After/SpaceAudit/audit.json)を、同じ1mグリッドで独立に集計した。裸土の未計画部分は約11,714㎡から0㎡となったが、全面舗装を用途の確認済みとは数えない。自動分類に属さない空間は約15,688㎡から8,912㎡、そのうち小物の5m圏外は6,670㎡から2,617㎡。この分類は敷地周辺・城壁際の通行余地も含むため、数値だけを見た目の合否に使わない。

Builderが付ける樹木・草花の小さな土床は、配置表だけを読む面積監査には含まれない。面積はサンプルによる概算であり、元の建物敷地面積29,667.58㎡（47.91％）とは区別する。外観は全域画像と原寸画像で併せて確認した。

Editorの起動・再起動はしていない。検証は既存Editor内の生成・保存・メッシュ検査・撮影とEditModeテスト。実際のキャラクター移動、NavMesh、Webビルドや性能の検証として扱わない。

## 変更元

- `Tools/World/build_country_plan.py`、`Tools/World/audit_city_coherence.py`
- `Assets/Data/CountryComparison/town_plan.json`
- `Assets/Prefabs/Kingdom/Comparison/Editor/CountryComparisonBuilder.cs`、`CountryComparisonAudit.cs`、`CountryGroundSurfaces.cs`、`CountryTownLayoutReview.cs`、`CountryTownCoherenceReview.cs`
- `Assets/Scripts/Editor/Country/CountryPavingScale.cs`、`CountryStructureRepairs.cs`
- 舗装・構造のテスト、保存シーン、派生メッシュ、ミニマップ配置データ
- `docs/Country/README.md`、`UI.md`、`城壁都市比較.md`

前段の [StreetLife20261101](../StreetLife20261101/verification.md) は当時の状態を記録したもので、今回の保存シーンの完成根拠として流用していない。
