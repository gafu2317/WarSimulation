# 教会・鍛冶屋 Blender原本

都市の主要教会と鍛冶屋。既存の小規模なChapel・Forgeとは別施設。

- Church: 双塔と身廊を持つ都市の中心施設
- Blacksmith: 炉・金床・工具棚を含む工房

原本: `ArtSource/Blender/FantasyChurchAndBlacksmith.blend`
出力先: `Assets/Models/Kingdom/City/`、`Assets/Prefabs/Kingdom/City/Prefabs/`

再生成:

```sh
/Applications/Blender.app/Contents/MacOS/Blender --background --factory-startup --python-exit-code 1 --python Tools/Blender/generate_fantasy_church_blacksmith.py
```

Unity上の配置は [Country 王国配置](../KingdomCity/README.md) を正本にする。
