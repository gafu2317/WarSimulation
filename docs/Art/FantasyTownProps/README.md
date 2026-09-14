# 街づくり用の小物・接続素材

道路、敷地、柵、市場、生活小物を組み合わせて城下町を作る。
建物・樹木・岩は作り直さず、Blender原本とUnity出力を分けて管理する。

原本: `ArtSource/Blender/FantasyTownProps.blend`
Unity出力: `Assets/Models/Kingdom/City/`、`Assets/Prefabs/Kingdom/City/Prefabs/`

## 配置基準

- 道路は8m四方、車道はZ=0、歩道はZ=0.12。
- 小物は親のZ=0を接地基準にする。
- 柵・門・石塀は4mピッチで接続する。
- 道路の原点・正面・高さを変えない。
- Blender原本にはゲーム挙動やColliderを持たせない。

寸法と検証の機械記録は同フォルダの `model_manifest.json` と `validation.json` に保存する。
Unity上の採用と配置は [Country 王国配置](../KingdomCity/README.md) を正本にする。

再生成:

```sh
/Applications/Blender.app/Contents/MacOS/Blender --background --factory-startup --python-exit-code 1 --python Tools/Blender/generate_fantasy_town_props.py
```
