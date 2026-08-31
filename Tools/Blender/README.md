# Blender制作スクリプトの扱い

このフォルダは制作用。Unityでモデルを配置・表示するためにPythonを実行する必要はない。完成した模型の原本は `ArtSource/Blender/*.blend` に保存されている。

**当面はPythonを削除・移動・一本化しない。** 22本で約0.30MiBと小さく、生成スクリプト同士がファイル名と相対パスで依存している。まずこの一覧から目的のものを選ぶ。

## ファイルの役割

| 名前 | 用途 |
|---|---|
| `generate_*.py` | 模型・質感・プレビューの生成レシピ |
| `validate_*.py` | 保存済み模型や書き出しの検証。確認レポートを書き出す場合がある |
| `rebuild_rock_bases.py` | 岩の底面を再構成する比較用処理 |
| `trim_rock_bottoms.py` | 岩の下部を切り、接地面を作る比較用処理 |
| `__pycache__/*.pyc` | Pythonの再生成可能なキャッシュ。制作レシピではない |

## 建物の生成入口

| 欲しい素材 | 生成スクリプト |
|---|---|
| 初期デフォルメ版 | `generate_kingdom_buildings.py` |
| リアル寄りの城・民家・3色の城壁 | `generate_realistic_fantasy_buildings.py` |
| 酒場・診療所・衛兵所 | `generate_fantasy_town_facilities.py` |
| カジノ・美術館・闘技場 | `generate_fantasy_culture_facilities.py` |
| 図書館・広場・天文台 | `generate_fantasy_civic_facilities.py` |
| 以前の歓楽街の街区案 | `generate_fantasy_entertainment_district.py` |
| 新しく作った歓楽街の4建物 | `generate_fantasy_nightlife_buildings.py` |
| 道・境界・生活小物34種類 | `generate_fantasy_town_props.py` |
| 環境素材の比較セット | `generate_environment_models.py` |
| 自然な樹木・岩のバリエーション | `generate_natural_tree_variants.py` / `generate_natural_rock_variants.py` |

## 削除すると影響する依存関係

矢印は「左のファイルが右を読み込む」。`generate_` 接頭辞と `.py` 拡張子は省略。

```text
fantasy_town_props ───────────→ fantasy_civic_facilities
fantasy_entertainment_district → fantasy_civic_facilities
fantasy_civic_facilities ─────→ fantasy_culture_facilities
fantasy_culture_facilities ───→ fantasy_town_facilities
fantasy_town_facilities ──────→ realistic_fantasy_buildings
fantasy_nightlife_buildings ──→ realistic_fantasy_buildings
realistic_fantasy_buildings ──→ kingdom_buildings

trim_rock_bottoms.py → rebuild_rock_bases.py → generate_natural_rock_variants.py
```

例えば、初期版の見た目を今後使わなくても `generate_kingdom_buildings.py` は現行の生成処理の土台。削除すると城だけでなく、公共施設や街の小物まで再生成できなくなる。共通部品を別モジュールへ整理することは可能だが、Unityへの移行に必須ではないため今回は行わない。

## 実行前に確認すること

- 通常のPythonではなくBlender付属のPython環境で実行する。`bpy` が必要。
- 生成スクリプトはシーンを初期化し、固定パスの原本や出力を上書きする場合がある。編集中のGUI上で不用意に実行しない。
- Blenderでの手修正はレシピには自動反映されない。手修正した原本を残したいときは、再生成前に別名保存する。
- **Unityへ渡すだけなら再生成しない。** 保存済み `.blend` からFBXを取り出す。
- `__pycache__` は削除候補だが、現在の4ファイルはGit追跡中。`.gitignore` への追加だけでは追跡解除にならない。

採用する素材とUnityへの移行手順は [素材管理ガイド](../../docs/Art/README.md) を参照。
