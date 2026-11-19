# 役割を建築構造と設備で表す施設

2026-10-24の指定に基づく、CityV2の施設の新規設計。
40資産、街に置く名前付き施設28種類を対象とする。学校は全レベルを作成し、Countryに置くのはLv5だけ。

施設の用途から平面・断面・採光・出入口・屋根・専用設備を組む。住宅モデルや住宅の生成処理は使わず、装飾の付加や配色変更だけでは完成としない。

2026-10-30の追加指定により、少し離れた街の画面で読める大きな目印を採用する。看板の一律禁止は解除し、建物の形も大胆に変えてよい。望遠鏡・魔力集束器・鐘楼・大樹などの設備、図書館の開いた本、酒場のジョッキ、カジノのカードなど、用途に合う異なる立体形状で識別する。全施設に同じ四角い看板を貼る方法には戻さない。支持材、屋根との接合、入口の余白も確保する。

2026-10-31の指定により、天文台は大型化前の観測ドームと望遠鏡を維持する。`design.json`の`preserveIdentity`で追加の大型目印の対象から外す。再生成用の構造・望遠鏡・ドームとBlender原本も変更前へ戻してある。

## 入力と出力

- `design.json`: 施設名、用途の分類、敷地寸法と位置、様式の方針。教会の `meshScale` は、従来の建築レシピから実寸の頂点と入口マーカーを作る倍率。
- `mesh.py`: 新規の小さなメッシュプリミティブ。座標はX右、Y上、正面-Z。
- `architecture.py`: 各施設の平面と構造。用途ごとに棟の配置や屋根・塔の構成を変える。
- `furnishings.py`: 本棚・閲覧机・炉・金床・病床・訓練具・観測機器などの実物。
- `landmarks.py`: 遠目で読める設備、立体看板、彫刻、灯具。各施設の構造に組み込む。
- `build_assets.py`: 空のBlenderから新規作成。既存FBX、Prefab、画像、既存建物の生成レシピは読み込まない。材質名だけをUnityの既存パレットへ対応させる。
- `Artifacts/RoleFacilityRebuild20261024/Generated/`: Unityに入れる前のFBX。Git管理しない中間出力で、再取り込み前に下記のBlender生成処理で作る。取り込み用の `generation_CityV2.json` はGit管理する。
- `Art/RoleFacilities/`: 作成したBlenderソース。

## 再生成

```sh
sh Tools/Blender/run_background.sh --factory-startup --python Tools/Blender/RoleFacilities/build_assets.py -- --style CityV2
```

UnityのCountryをEditモードで開き、`WarSim > Country > Role Facilities`の`Capture Before`、`Install Rebuilt Facilities`、`Validate Rebuilt Facilities`、`Capture After`を使う。インポート対象は生成済みの40モデルだけで、元のGUIDを維持する。比較の配置、住宅、川、城壁、旧モデルの第1国は維持する。

用途の異なる施設へ同じ看板を貼る以前のレシピは、この設計の代替として使わない。CityV2の旧一括生成処理を使った場合も、施設はこの手順で生成し直してから取り込む。

## 識別の根拠

図書館は高い閲覧室・採光屋根・閲覧回廊・蔵書、美術館は展示室・柱廊・実際の彫刻、診療所は低い病棟・回復用ベランダ・病床・薬草園、鍛冶場は開いた作業床・炉・煙突・金床・工具で表す。学校は訓練棟・研究塔・自然を囲む回廊など、教育内容に合う施設構成を持つ。教会は身廊・側廊・後陣・鐘楼を持つ。天文台の観測ドームには本当の開口を設け、望遠鏡は開口を通す。

## 材質・接合の改善（2026-10-30）

天文台と採光屋根には`Architectural_Glass`を使用し、窓ガラスは格子が二重に重ならない専用材へ分離する。窓枠・柱・彫刻は`Carved_Limestone`、金具は`Bronze_Fittings`を使い、石積み画像や屋根用の配色を流用しない。円筒の壁と傾斜屋根は向きに合ったUVを持つ。

美術館・城・戦士学校の屋根と棟の接合、図書館の採光窓と内部梁、教会の高窓、浴場のドームの露出、天文台の観測床と望遠鏡を見直した。荷扱い・鍛冶場・露店の構造には木の支柱と方杖を使用する。歓楽街の上階へ続く階段、床の開口、手すりも生成する。

全141種類の同一視点による変更前後、建物・学校の背面、現在の街の近景は`Artifacts/CityV2Quality20261030/`で確認する。画像はUnityの実物を撮影したもの。再取り込み時にも`CityV2MaterialQuality`が石畳の彩度、切石、金具、ガラスの設定を適用する。

## 2026-11-17のコンセプトからの制作（Unity反映済み）

国家の15種類の役割施設を対象に、歓楽街4棟を含む17棟を新作した。既存の教会を加えた18棟を `Art/RoleFacilities/ConceptModels/` に個別のBlender原本として保存した。街の全景を確認した後、各棟の幅・奥行き・高さを制作時の0.8倍へ調整し、Countryシーンへ反映した。舞踏館と仮面のサロンは、歓楽街の回廊館と灯楼の外観を置き換えた。

| 施設 | 原本 | 2026-11-17反映時の幅 × 奥行 × 高さ（m） |
|---|---|---:|
| 教会 | `Church.blend` | 21.84 × 31.20 × 19.34 |
| 天文台 | `Observatory.blend` | 21.50 × 21.84 × 19.79 |
| 図書館 | `Library.blend` | 25.95 × 23.85 × 19.20 |
| 美術館 | `Museum.blend` | 29.74 × 24.27 × 17.89 |
| 診療所 | `Clinic.blend` | 23.94 × 25.14 × 16.58 |
| 戦士学校 Lv5 | `WarriorAcademy_Lv5.blend` | 28.29 × 27.33 × 19.84 |
| 魔法学校 Lv5 | `ArcaneAcademy_Lv5.blend` | 31.00 × 19.78 × 22.27 |
| 精霊使役学校 Lv5 | `SpiritAcademy_Lv5.blend` | 29.62 × 27.57 × 20.51 |
| 衛兵所 | `Guardhouse.blend` | 15.72 × 28.29 × 20.74 |
| 闘技場 | `Arena.blend` | 35.69 × 36.01 × 11.54 |
| 広場 | `Plaza.blend` | 28.32 × 22.14 × 6.57 |
| 鍛冶場 | `Blacksmith.blend` | 26.56 × 21.65 × 19.32 |
| 酒場 | `Tavern.blend` | 26.77 × 22.62 × 17.74 |
| カジノ | `Casino.blend` | 25.12 × 25.26 × 19.22 |
| 歓楽街・演芸館 | `CrimsonRowhouse.blend` | 26.62 × 21.67 × 19.42 |
| 歓楽街・中庭館 | `VeiledCourtyard.blend` | 27.58 × 25.30 × 17.46 |
| 歓楽街・舞踏館 | `Ballroom.blend` | 27.98 × 23.80 × 19.62 |
| 歓楽街・仮面のサロン | `MasqueradeSalon.blend` | 25.69 × 20.89 × 17.70 |

壁、屋根、床、設備、目印を別メッシュとして編集できる。寸法は頂点に反映し、配置スケールは1とした。石目、瓦、木目、絵、カードの模様、寄木床の装飾はテクスチャで表現し、各原本に埋め込んである。プレビュー用の背景と地面は描画時に追加する。各施設の敷地内の床や中庭の舗装は原本に含む。

```sh
sh Tools/Blender/run_background.sh --factory-startup --disable-autoexec -t 8 --python-exit-code 1 --python Tools/Blender/RoleFacilities/ConceptModels/build.py -- Observatory --views three_quarter front rear
```

施設名を原本のファイル名（拡張子なし）に置き換えると、対応する原本、寸法のJSON、3方向の2532×1170画像を `Artifacts/RoleFacilityConceptChecks/` に再生成できる。縮小版は頂点と入口のマーカーに0.8を掛ける。制作時の寸法は `--scale 1` で再生成できる。保存した18原本を開き直し、縮小前後の全頂点、面の構成、UV、入口マーカーと埋め込みテクスチャを照合した。

戦士学校の質感は `CityV2_Facilities.blend` の既存モデルと比較した。既存モデルにCountryでの配置倍率1.25と `Warrior_Oxblood_Roof_Slate` の屋根の色分けを適用し、`preview.py` の同一照明と正投影カメラで並べた。屋根のUV一周期の長さは既存2.75m、初回確認版2.20m。初回確認版は瓦を `Roof_Clay` に替え、正面の漆喰を灰色の石積みに替えた。瓦の濃淡と粒度、壁面の色むらに差が出た。

### 瓦の調整

瓦屋根の13棟を `Roof_Slate` の色・法線・粗さマップと、既存の街で計測した建物ごとのUV間隔で再生成した。制作時は戦士学校で2.75m、衛兵所で3.52mが一周期。0.8倍版では同じUVの割り付けを使い、一周期は戦士学校で2.20m、衛兵所で2.816mになった。`ConceptModels/roof_tiles.py` は `CityRoofPalette.shader` と同じ区切りで瓦のくぼみ、本体、縁を彩色する。中庭館は赤褐色、仮面のサロンは青みのある炭色を使い、ほか11棟は既存の施設別パレットを使う。

修正前後の `.blend` を開き直し、メッシュの頂点、面、階層と瓦以外のUVが一致することを確認した。修正後の全13棟で瓦のテクスチャ3枚の埋め込みと屋根面のUV間隔を確認した。`preview.py` で修正前後を同じ角度・縮尺・照明で描画し、戦士学校は屋根の同じ位置も拡大して比較した。

## 住宅を基準にした階高の調整（2026-11-18）

二階建て住宅 `Fantasy_House` は、原本で地面から二階の見切りまで2.60m、玄関扉は高さ2.25m。配置倍率0.84〜1.10では、見切りは地面から2.18〜2.86mになる。確認画像では倍率0.98の住宅と身長1.70mの人型を基準にする。

二階建てに見える施設は、実際の床または外壁の見切りの位置を使い、主に3.15〜3.60mの一階に調整する。カジノの集会室は4.50mとする。演芸館の測定箇所は横の玄関棟、中庭館は二階回廊。学校の講堂、舞踏館の吹き抜け、観測ドーム、見張り塔は用途に合う高さを持つ。教会の高さ19.34mを街の目印として使う。

| 施設 | 一階の高さ | 調整後の全高 |
|---|---:|---:|
| 酒場 | 6.02 → 3.25m | 10.39m |
| 診療所 | 4.82 → 3.30m | 10.19m |
| 精霊使役学校 | 5.07 → 3.45m | 15.57m |
| カジノ | 7.81 → 4.50m | 12.12m |
| 仮面のサロン | 6.34 → 3.60m | 10.67m |
| 演芸館の玄関棟 | 4.90 → 3.15m | 14.91m（舞台棟を含む） |
| 中庭館の回廊 | 4.74 → 3.15m | 11.81m（奥棟を含む） |

採用した15棟の調整は `Art/RoleFacilities/ConceptModels/` のメッシュ頂点に反映した。水平寸法を施設別に決め、上の7棟は各階の高さも調整した。敷居と入口マーカーをモデルに追従させている。施設ごとの材質を使用し、瓦の繰り返しUVは水平寸法の変化に合わせる。同じ正投影カメラと縮尺で変更前後を2532×1170に描画した。

道路に面する敷地の縁を維持し、入口への舗装を引き直した。演芸館の玄関棟への通路を空けるため、前庭の街灯1本を `(15.747, -28.765)` から `(14.997, -28.765)` へ移した。正式な街の原本 `Art/CountryLayout/TownLayout.blend` を開き直し、入口への184経路の通行と小物1,233点の配置を検査した。Unityでは教会・広場・闘技場を含む18棟のメッシュ寸法と配置スケール1を照合した。

```sh
out=Artifacts/CountryFacilityScaleReview
sh Tools/Blender/run_background.sh --factory-startup --disable-autoexec -t 8 --python-exit-code 1 --python Tools/World/adjust_country_facility_scale.py -- "$out"
python3 Tools/World/build_country_redesign.py "$out/dimensions.json" "$out/town_plan.json" --from-plan Art/CountryLayout/town_plan.json --keep-frontage
sh Tools/Blender/run_background.sh --factory-startup --disable-autoexec -t 8 --python-exit-code 1 --python Tools/World/render_country_redesign.py -- "$out/town_plan.json" "$out" --concept-dir "$out/models" --save "$out/TownLayout.blend" --views 02_avenue
sh Tools/Blender/run_background.sh --factory-startup "$out/TownLayout.blend" --disable-autoexec -t 4 --python-exit-code 1 --python Tools/World/verify_country_access.py -- "$out/town_plan.json"
```
