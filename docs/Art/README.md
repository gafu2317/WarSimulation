# 3D素材ガイド

建物・街の小物・書斎の仕様と制作手順をまとめる。

| 調べたいこと | 文書 |
| --- | --- |
| 建物の種類、施設仕様との対応、原本 | [建物モデル一覧](建物モデル一覧.md) |
| 街素材の書き出し・取り込み、道路・小物・植栽 | [街素材](街素材.md) |
| 学校3種×Lv1〜5の造形・制作 | [学校](学校.md) |
| 大型王城の寸法・構造・制作 | [王城](王城.md) |
| 書斎・応接室の73部品とUnity取り込み | [書斎](書斎.md) |

## 素材の置き場

- 編集用原本: `ArtSource/Blender/Kingdom/`、`ArtSource/Blender/GrandStudy/`、`ArtSource/Blender/Environment/`
- 制作・検証スクリプト: [Tools/Blender](../../Tools/Blender/README.md)
- Unity用モデル: `Assets/Models/Kingdom/City/`、`Assets/Models/GrandStudy/`
- Unity用Prefab: `Assets/Prefabs/Kingdom/City/`、`Assets/Prefabs/GrandStudy/`

原点・正面・接地・縮尺を維持し、形状を変えたときは原本とUnity出力の両方を更新する。共有素材の変更後は利用先も確認する。

ゲーム用の正式な素材と `.meta`、編集用原本、再生成コード、取り込みに必要な生成記録はGit管理する。`Artifacts/` の中間FBX・ベイク画像、`ArtSource/Exports/` の書き出しコピー、HTML試作の `dist/` はローカルの再生成可能な出力として扱う。取得直後に再取り込みするときは、先に対応するBlender生成処理を実行する。

## 生成・検証記録

機械が読み書きするJSONは `Records/` にまとめる。素材ごとの説明は上表の文書へ追記し、生成処理は記録だけを更新する。

AIの確認用画像・UI状態ダンプ・MCP応答・差分・シーン退避は `Artifacts/` に出力し、確認後に削除する。これらの一時出力はGit管理しない。過去の検証記録にある出力パスは、制作時のローカル出力先を示す。確認結果と再現手順を既存の文書へ残し、取り込みに使うJSONは保持する。

- `Records/City/`・`Catalog/`: 街素材の書き出し、素材一覧シーンの検証。
- `Records/Schools/`・`Castle/`・`GrandStudy/`: 素材ごとの生成、FBX書き出し、Unity取り込みの記録。学校と書斎はBlender側・Unity側を同じ場所に置く。
- その他の素材別フォルダ: 教会・鍛冶屋、植栽、道路・小物、各建築セットの生成・検証記録。`AdditionalImports/`は教会・鍛冶屋・植栽の取り込み用一覧。

検証記録は各制作時点の結果。最新の検証状況は、記録の対象モデル・原本と現在の素材を照合して判断する。
