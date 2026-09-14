# ファンタジー学校3種 Blender原本

外形と前庭設備で役割を見分けられる、非発光マテリアルの学校セット。

- `WarriorAcademy`: 訓練塔・軍旗・武器設備
- `ArcaneAcademy`: 魔術塔・観測台・空中回廊
- `SpiritAcademy`: 大聖堂・聖樹・精霊意匠

原本: `ArtSource/Blender/FantasySchools.blend`
出力先: `Assets/Models/Kingdom/City/`、`Assets/Prefabs/Kingdom/City/Prefabs/`

再生成:

```sh
/Applications/Blender.app/Contents/MacOS/Blender --background --factory-startup --python-exit-code 1 --python Tools/Blender/generate_fantasy_schools.py
```

Unity上の配置は [Country 王国配置](../KingdomCity/README.md) を正本にする。
