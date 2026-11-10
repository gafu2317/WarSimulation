# Unity Project Context

<!-- unity-onboarding:generated:start -->

- 調査日: 2026-10-28。対象: `/Users/fukutomi/Unity/WarSimulation`、調査時のHEAD: `c48bd3f`。作業ツリーに多数の未コミット変更があり、HEADだけでは現状を表さない。
- Unity 6000.4.3f1、Universal Render Pipeline 17.4.0、Input System 1.19.0、uGUI 2.0.0、TextMeshPro、UniTask、AI Navigation 2.0.12、Unity Test Framework 1.6.0を使用する。ネットワークゲームとしての利用は未確認。
- `Assets/Scripts/WarSimulation.Runtime.asmdef` がゲーム本体、`WarSimulation.Editor.asmdef` がエディター機能。`Assets/Tests/EditMode` と `Assets/Tests/PlayMode` に別のテストアセンブリがある。
- `Assets/Scenes/Country.unity` が国家シーンでWeb版の起動シーン。`Country`、`GafuTest`、有効な戦闘マップの順にビルドし、国家の設定と戦闘の編成画面から往復する。国家データと政務の操作案は起動中保持するが、戦闘結果は国家へ反映しない。
- `Assets/Scripts/Managers` の `ManagerBase<T>` はシーン間で維持するマネージャーを扱う。国家は `CountrySceneContext` と `Systems/Country`、戦闘は `CombatSceneContext` と `Systems/Combat`・`Combat` に分かれる。
- 国の時計は `DateManager`、資源と兵士・遠征・防衛の状態は `PlayDataManager`、日次・月次の進行は `CountryCycleSystem` が所有する。国家画面は保存済みuGUI Prefabと `Assets/Prefabs/Country/UI/Editor` のBuilderで構成する。現行仕様は [国家画面・国家運営](Country/README.md)。
- 戦闘マップの照会・更新は `CombatMapSystem`、状態は `MapData` が所有する。地面状態は `GroundStates` 経由で更新する。詳細と例外は `AGENTS.md` に従う。
- 命名は既存C#に合わせ、シリアライズする参照は主に `[SerializeField] private`。新規TextMeshPro UIは指定Noto Sans JPのアセットと対応マテリアルを使う。コードの短さと不要な抽象化・分岐の抑制を優先する。
- Unity MCPは `com.coplaydev.unity-mcp`、Unity CLI bridgeは埋め込みパッケージとして存在する。今回、既に開いていたEditorの接続・状態・コンソールとEditModeテスト実行を確認した。CIでのテストや製品ビルドは未確認。
- AIはUnity Editorを起動・再起動しない。ユーザーが開いた既存Editorのみを使う。国家UIの変更では `docs/Country/UI.md` と担当仕様・入口の実装状態を同時更新する。
- 主な根拠: `ProjectSettings/ProjectVersion.txt`、`ProjectSettings/GraphicsSettings.asset`、`ProjectSettings/ProjectSettings.asset`、`ProjectSettings/EditorBuildSettings.asset`、`Packages/manifest.json`、各asmdef、`Assets/Scripts/Managers`、`Assets/Scripts/SceneContexts`、`Assets/Scripts/Systems/Country/CountryCycleSystem.cs`、国家仕様書。

<!-- unity-onboarding:generated:end -->
