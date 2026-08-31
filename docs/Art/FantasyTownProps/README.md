# 街づくり用の小物・接続素材

既存の城・民家・公共施設・歓楽街に追加して、ひとつの城下町を配置できるようにするBlender素材セット。既存の建物、樹木、岩は作り直さない。Unity用データの変更は行わない。

## 制作・確認する範囲

| 不足している用途 | 素材 | 確認すること |
|---|---|---|
| 建物間の移動と広場への接続 | 石畳の直線、曲がり、T字、十字、行き止まり、敷地舗装 | 同じ寸法・高さで接続できる |
| 敷地・庭・作業場の区切り | 木柵パネル、終端柱、木門、低い石塀、石塀の角 | 繰り返しと角・出入口の配置ができる |
| 市場と荷物の搬入 | 青果露店、布露店、荷車、閉じた木箱、開いた木箱、麻袋、青果かご、樽 | 別々に配置・組み合わせられる |
| 給水と生活の跡 | 井戸、水槽、薪置き場、干し草、物干し | 建物の外の生活空間を作れる |
| 案内・休憩・小規模な作業 | 道標、掲示板、金床、小型の炉、ベンチ、街灯、植え込み | 街角・広場・職人の庭に配置できる |
| 路面と軒先の細部 | 排水格子、車止め、木桶 | 大きな建物の間を埋められる |

樽・ベンチ・街灯・植え込みは既存モデルの付属物と同じ造形を、独立した配置素材として用意する。それ以外は新規制作。

各素材を名前付きコレクションと親オブジェクトに分ける。マテリアルはファイル内に保持する。全素材の再読込、有限な頂点、面の閉じ方・面積、基準位置を確認する。道路の接続を実データで確認し、既存建物と小物を組み合わせた街の見本を保存・レンダリングする。

## 配置基準

- 道路は8m四方。中央の車道は幅4mでZ=0、歩道はZ=0.12。回転は90度刻みで接続する。道路の厚みは地面の下に入る。
- 通常の小物は親オブジェクトのZ=0を接地基準にする。歩道に置く場合はZ=0.12に上げる。
- 木柵・木門は始点を原点にした4mピッチ。木柵は左側の柱を含み、最後だけ終端柱を追加する。
- 低い石塀は4mピッチ。角用部品でL字に接続する。
- 素材集の本体コレクションをAppendするか、街の見本と同様にコレクションインスタンスで配置する。見本の既存建物は追加素材に含めた新規建築物ではない。

各素材の寸法と由来は `model_manifest.json`、検証結果は `validation.json` に保存する。

## ファイルを開く

`ArtSource/Blender/FantasyTownProps.blend` を開くと `00 Town Example` が表示される。シーンの切り替えで `01 Roads` ～ `06 Details` の素材一覧を見ることができる。各配置オブジェクトは名前付きのコレクションインスタンスで、移動・回転・複製が可能。形状を編集する場合は、インスタンスを選んで「Make Instances Real（インスタンスを実体化）」で編集用の複製を作る。別のBlenderファイルで使う場合は、対象の本体コレクションをAppendする。

街の見本に使っている城・民家・酒場・診療所・衛兵所・図書館・広場・歓楽街の建物は、従来の完成品をファイル内に読み込んだもの。元ファイルは変更していない。道路や小物にゲーム内の挙動・当たり判定・Unity設定は付けていない。

## 素材名

一覧画像では各カテゴリの素材を上の列から左→右の順に並べている。

| カテゴリ | コレクション名 | 用途 |
|---|---|---|
| Roads | `Road_Straight` | 直線道路 |
| Roads | `Road_Corner` | 曲がり道路 |
| Roads | `Road_T` | T字路 |
| Roads | `Road_Cross` | 十字路 |
| Roads | `Road_End` | 行き止まり |
| Roads | `Paved_Plot` | 敷地舗装 |
| Boundaries | `Fence_Panel` | 木柵 |
| Boundaries | `Fence_Post` | 終端柱 |
| Boundaries | `Fence_Gate` | 木門 |
| Boundaries | `Stone_Wall` | 低い石塀 |
| Boundaries | `Stone_Wall_Corner` | 石塀の角 |
| Market | `Produce_Stall` | 青果露店 |
| Market | `Cloth_Stall` | 布露店 |
| Market | `Handcart` | 荷車 |
| Market | `Crate_Closed` | 木箱・蓋付き |
| Market | `Crate_Open` | 木箱・開放 |
| Market | `Grain_Sack` | 麻袋 |
| Market | `Produce_Basket` | 青果かご |
| Market | `Barrel` | 樽 |
| Life | `Well` | 井戸 |
| Life | `Water_Trough` | 水槽 |
| Life | `Firewood_Rack` | 薪置き場 |
| Life | `Hay_Bale` | 干し草 |
| Life | `Clothesline` | 物干し |
| Civic | `Signpost` | 道標 |
| Civic | `Noticeboard` | 掲示板 |
| Civic | `Anvil` | 金床 |
| Civic | `Forge` | 小型の炉 |
| Civic | `Bench` | ベンチ |
| Civic | `Streetlamp` | 街灯 |
| Civic | `Planter` | 植え込み |
| Details | `Drain_Grate` | 排水格子 |
| Details | `Bollard` | 車止め |
| Details | `Bucket` | 木桶 |

## 再生成と検証

Blender 5.0.0で以下のスクリプトを実行する。外部アドオン・画像テクスチャは不要。

- 制作：`Tools/Blender/generate_fantasy_town_props.py`
- 再読込・形状・接続の確認：`Tools/Blender/validate_fantasy_town_props.py`
- 一覧・街の画像：このフォルダの `*_Preview.png`

道路同士は8m間隔で配置する。歩道上の小物はZ=0.12、車道上はZ=0。縁石だけは約Z=0.14まで立ち上がる。排水格子は埋め込み穴を開けずに路面上に置く装飾部品。
