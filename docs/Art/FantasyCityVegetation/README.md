# ファンタジー都市用植栽 Blender原本

建物際・道路沿い・広場の空地を埋める非発光の植栽セット。

短草、中草、高草、シダ、野花、花壇、街路樹、日陰樹を含む。
すべて地面へ直接置ける接地状態にする。

原本: `ArtSource/Blender/FantasyCityVegetation.blend`
出力先: `Assets/Models/Kingdom/City/`、`Assets/Prefabs/Kingdom/City/Prefabs/`

再生成:

```sh
/Applications/Blender.app/Contents/MacOS/Blender --background --factory-startup --python-exit-code 1 --python Tools/Blender/generate_fantasy_city_vegetation.py
```

Unity上の配置は [Country 王国配置](../KingdomCity/README.md) を正本にする。
