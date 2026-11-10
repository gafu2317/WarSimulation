# 街路脇の舗装・生活用具・菜園

2026-11-01。`Assets/Scenes/Country.unity` に反映し、既存のUnity Editorで保存・検証した。見た目はユーザー確認待ち。

![同じ視点の変更前後](street_life_comparison.png)

## 変更

- 住宅・施設の寸法と位置、道路40本、入口経路249本、広場・緑地・監視塔の配置は変更前と完全一致。建物敷地占有率47.91％も維持。
- 細い入口だけが裸地に突き出す状態を整え、玄関前219か所と道路に接する側面320か所を石敷きでつないだ。内部道路32本には両縁1.2mの連続舗装を追加。芝と菜園の土を新しい側面・道路縁舗装から除き、既存の入口経路は維持した。
- 壁際181棟・247か所へ、薪、物干し、樽、木箱、荷車、植木鉢、ベンチなど468点を用途ごとに配置した。小さすぎた既存の家裏装飾38点を置き換え、配置表の小物は143点から573点へ更新。
- 既存16区画の庭に、背面柵と植栽を持つ小さな菜園24区画を追加。道路・入口・公共広場・施設の緑地を避けた。
- ミニマップも同じ追加舗装を描画する。

## 確認画像

`Before/Streets/` と `After/Streets/` に、新旧それぞれ13視点のEditor内カメラ画像を保存した。ユーザーが示した街路は [記録したSceneビューの位置・角度](user_view.json) を変更前後で再使用している。画像の編集は一覧用の縮小・整列・ラベルのみ。

- 街路：[変更前](Before/Streets/New_UserStreet.png) / [変更後](After/Streets/New_UserStreet.png)
- 裏庭：[変更前](Before/Streets/New_BackCourts.png) / [変更後](After/Streets/New_BackCourts.png)
- [住宅地](After/Streets/New_Residential.png)、[商店街](After/Streets/New_ShopLane.png)、[市場](After/Streets/New_Market.png)、[全景](After/Streets/New_Overview.png)

街路の石敷きの途切れが解消し、壁際の物干し・樽と、裏庭の菜園・荷車・箱を画像で確認した。庭の作業スペースと道路・玄関前の通行空間は残している。

## 検証

| 検査 | 結果・証拠 |
|---|---|
| 元の配置の保持 | [PASS](layout_preservation.json)。buildings、roads、squares、greens、accessPaths、watchtowersの全配列が完全一致 |
| 計画の生成検査 | [PASS](After/plan_validation.json)。建物敷地の重複0、道路との重複0、他敷地を横切る入口0、入口249本が舗装へ接続 |
| 追加小物の独立確認 | [PASS](placement_review.json)。新旧モデルの最大幅・奥行を用いた平面SATで、追加468点と建物・道路・入口・追加小物同士の重複0 |
| 生成した配置 | [PASS](After/scene_validation.json)。各国249棟・小物573点、失敗0 |
| 保存後の建物 | [PASS](After/saved_scene_validation.txt)。新旧498棟、入口各249本 |
| 城壁内への収まり | [PASS](After/inside_walls_validation.json)。58,873 Renderer、243,814頂点を検査、外側0点 |
| モデルの種類 | [PASS](After/model_usage_validation.json)。新旧それぞれ129種類を維持 |
| 川の接続 | [PASS](After/river_validation.json)。水門4、川の接続8、水車の端4 |
| 施設とカメラの参照 | [PASS](After/scene_links.json)。施設30対象 |
| 舗装の既存EditModeテスト | [5件PASS](After/paving_test_status.json)。穴の面積、重複切り抜き、反復、高さ・UV、無関係な切り抜き |
| コンパイル | [エラー0](After/compilation_state.json)、更新・コンパイル待ちなし。既存の非推奨API等の警告は残る |
| 保存状態 | [Countryシーン保存済み](After/scene_info.json)、`isDirty=false`。新モデルの国を初期表示 |

Unity Editorの起動・再起動はしていない。ビルドや公開は今回の対象外。実機内の生成・保存検査とEditor画像を確認しており、この記録をゲームプレイの通行検査や性能計測として扱わない。

## 主な変更元

- `Tools/World/build_country_plan.py`、`Assets/Data/CountryComparison/town_plan.json`
- `Assets/Prefabs/Kingdom/Comparison/Editor/CountryComparisonBuilder.cs`
- `Assets/Prefabs/Kingdom/Comparison/Editor/CountryGroundSurfaces.cs`
- `Assets/Prefabs/Kingdom/Comparison/Editor/CountryTownLayoutReview.cs`
- `Assets/Prefabs/Country/UI/Editor/CountryMinimapBuilder.cs`
- 保存シーン、舗装メッシュ、ミニマップ配置アセット
- `docs/Country/README.md`、`docs/Country/UI.md`、`docs/Country/城壁都市比較.md`

施設拡大・監視塔・市場整理は前段の [TownScale20261101](../TownScale20261101/verification.md)、道路の直線化は [TownDensity20261101](../TownDensity20261101/verification.md) に記録した。前段の画像や小物143点という件数は、今回更新前の状態を表す。
