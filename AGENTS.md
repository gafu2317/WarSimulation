# WarSimulation

- ゲーム仕様は `./docs`、戦闘AIの入口は `./docs/AI/README.md`。
- コードは短く読みやすく保ち、不要な抽象化・分岐・コメントを追加しない。コードには How、テストには What、コミットログには Why、コメントには Why not を書く。
- 新規TextMeshPro UIには `Assets/Fonts/Noto_Sans_JP/static/NotoSansJP-Regular SDF.asset` と対応マテリアルを使う。

## AIによるUnity操作の安全規則

- Unityの起動は許可する。同じプロジェクトの起動処理が未完了または状態不明の間は、状態確認なしに再起動せず、起動済みなら既存のEditorを再利用する。

## Structure

- `Assets/Scripts/Systems/Combat/` はシーン orchestration、`Assets/Scripts/Combat/` は戦闘ドメイン。`Map` は地形生成・描画データ、`Combat` はAI・スキル等の戦闘設定を指す。
- `MapData` は現在のマップ状態を保持する。高さ・コライダー・NavMeshは戦闘中に変えず、地面状態は `GroundStates` を通じて更新する。
- マップ問い合わせ・更新は `CombatMapSystem` 経由で行う。`TryGetTerrainInfo` は `MapData` のスナップショットを返し、表示系は現在状態を再読込して更新する。
