# WarSimulation

- ゲーム仕様は `./docs`、戦闘AIの入口は `./docs/AI/README.md`。
- コードは短く読みやすく保ち、不要な抽象化・分岐・コメントを追加しない。コードには How、テストには What、コミットログには Why、コメントには Why not を書く。
- 新規TextMeshPro UIには `Assets/Fonts/Noto_Sans_JP/static/NotoSansJP-Regular SDF.asset` と対応マテリアルを使う。

## Unity

- 既存のシーン・Prefab・UI階層・参照は、依頼された場合を除き変更しない。追加物は専用の機能ルート配下に置く。
- Editor setup/repair は、既存対象とアセットを全て検証してから変更し、専用ルートだけを冪等に作り直す。想定対象が無ければ代替Prefabを作らず停止する。
- シーン変更前に未コミット変更を確認し、変更後は意図した対象だけが変わったことを確認する。修復成功は事後条件を確認できた場合だけ報告する。
- 既に開いているUnity Editorを使い、別のUnity起動やbatchmode検証を行わない。シーン変更は明示的な操作から開始する。

## Structure

- `Assets/Scripts/Systems/Combat/` はシーン orchestration、`Assets/Scripts/Combat/` は戦闘ドメイン。`Map` は地形生成・描画データ、`Combat` はAI・スキル等の戦闘設定を指す。
- `MapData` は現在のマップ状態を保持する。高さ・コライダー・NavMeshは戦闘中に変えず、地面状態は `GroundStates` を通じて更新する。
- マップ問い合わせ・更新は `CombatMapSystem` 経由で行う。`TryGetTerrainInfo` は `MapData` のスナップショットを返し、表示系は現在状態を再読込して更新する。
