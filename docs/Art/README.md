# 街素材

## 役割

- 原本: `ArtSource/Blender/*.blend`
- 生成・検証: `Tools/Blender/`
- Unityモデル: `Assets/Models/Kingdom/City/Models/`
- Unity用Prefab: `Assets/Prefabs/Kingdom/City/Prefabs/`
- 現行配置: [Country 王国配置](KingdomCity/README.md)

`.blend` は編集用の正本、Pythonは再生成手順、`Assets` はUnity用の出力と分ける。
原本を直接上書きせず、Unity側の出力だけを更新する。

## 制作ルール

- 原点・正面・接地・縮尺を維持する。
- 道路や接続素材は基準寸法を変えない。
- Blenderのプロシージャル質感は必要な範囲だけベイクする。
- Prefabは配置用の親と表示モデルを分ける。
- 代表素材で見た目と接続を確認してから、他の素材へ展開する。

## 追加フロー

1. 原本のコレクションを確認する。
2. FBX・テクスチャを `City` へ出力する。
3. URPマテリアルとPrefabを作る。
4. `Country.unity` または確認シーンで接地・寸法・接続を確認する。

現行の配置を更新するときは [KingdomCity/README.md](KingdomCity/README.md) と `KingdomCityBuilder` を正本にする。
