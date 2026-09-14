# Country 王国配置

`Assets/Scenes/Country.unity` に城壁都市を配置する。

## 意図

- 南門から中央広場・公共地区を通って王城へ向かう主街道を作る。
- 東西の副門へ生活・物流の動線をつなぐ。
- 軍事、公共文化、住宅、市場、歓楽街を区画で分ける。
- 道路と入口を連続させ、緑地は防火・裏庭・警戒空間として残す。

## 出力と再生成

- モデル・テクスチャ: `Assets/Models/Kingdom/City/`
- Prefab: `Assets/Prefabs/Kingdom/City/Prefabs/`
- 配置処理: `Assets/Prefabs/Kingdom/City/Editor/KingdomCityBuilder.cs`
- Blender出力: `Tools/Blender/KingdomCity/export_city.py`

Blender原本を出力した後、Countryシーンを開いて `WarSim > Kingdom > Build Country City` を実行する。
この処理は `Kingdom` ルートだけを作り直す。

## 現在の限界

外観と街路配置が対象。住民、室内、NavMesh、営業時間、門の開閉、LOD、実機性能は別作業。
