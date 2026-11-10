# Country 地面の重複面修正

2026-10-23 / Country.unity / Unity 6000.4.3f1 / URP 17.4.0 / Metal

## 変更内容

- 南門・東門の舗装モデルを残し、その下の道路面を切り抜いた。広場や作業場が覆う道路の範囲も切り抜いた。
- 新作の Road_Cross / Road_Straight / Road_T / Road_Corner / Road_End は、地面と同じ高さの Earth 下地サブメッシュを比較シーンで描画しないようにした。
- 建物の不透明な床と同じ高さの土、および国の土の下に隠れる草原を切り抜いた。
- 元のPrefab・FBX・PNG・材質を保持し、比較専用の65 Meshアセットを保存。次の街再構築にも適用される。
- カメラ・ライティング・建物の配置と形状は変更していない。

## 確認

- 切り抜きのEditModeテスト5件が成功（面積、UVと高さ、切り抜きの重なり、再適用、範囲外）。
- 保存シーンを再読込し、再生画面を確認した。
- 129種類ずつのモデルと新旧同数配置、主要建物552棟の参照と接地、城壁内への収まり、川の接続、屋根配色の既存検査がすべてPASS。
- 元アセット3138ファイルのハッシュが一致。
- シーン差分はMeshFilter 60個とPrefabのMesh参照override 5個。GameObject・Transformの変更、追加、削除はいずれも0。
- 修正後の三角形解析で Paved_Plot と道路の投影重複は0件。新作道路5部品の Earth 描画三角形も0件。

## 同じ視点・3mm移動による比較

各色チャンネルの変化量が閾値を超える画素を、同じ投影領域で比較した。通常のテクスチャ移動や輪郭の変化も含むので、画素数全体をちらつきだけの量とはしない。

| 対象 | 閾値 | 修正前 | 修正後 |
|---|---|---:|---:|
| 旧・南門 | >32 | 1,055 | 246 |
| 新・南門 | >32 | 2,059 | 271 |
| 新・北側交差点 | >16 | 19,147 | 1,324 |

![修正前後](Surface_Fix_BeforeAfter.jpg)

## 関連ファイル

- surface_audit.json / ground_overlap_analysis.json: 修正後の面の検査
- source_preservation.json: 元アセットの保持
- scene_diff_validation.json: 保存シーンの差分分類
- pixel_validation.json: 画素変動と比較領域
- Play_Overview.png: 保存シーンの再生画面
- CountrySurfaceProbe.cs: 再現用の調査・撮影コード（Assets外、非コンパイル）
- ../surface_geometry_tests.json: EditModeテスト結果

水車の独自水面や旧王城のPoint/mipmap設定など、前の調査で未確定だった候補には変更を加えていない。今回確認した地面の重複面を修正対象とした。
