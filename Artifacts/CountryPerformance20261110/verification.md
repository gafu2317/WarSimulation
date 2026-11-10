# 国シーンの描画負荷改善（2026-11-10）

対象は `Assets/Scenes/Country.unity`。起動済みのUnity 6000.4.3f1を使用し、Editorの起動・再起動は行っていない。

## 計測方法

- Apple M3 Pro / Metal / Unity Editor。Playerやスマホ実機の測定ではない。
- Device Simulatorは `iOS Notch Device (1170×2532)` を横向き、描画2532×1170、Scale 50、Fit to Screenオフ。端末枠を含む画面で確認する。
- 再生開始時の新国・王城視点、日付停止、無操作。カメラ位置 `(152.5, 91.374, 31.9628677)`、角度 `(41.0504341, 0, 0)`。
- Editorを前面にし、3秒ウォームアップ後の15秒を採取。`Time.unscaledDeltaTime` の中央値/p95、UnityStatsの描画命令数と影、ProfilerRecorderのCPU時間を記録。
- 一時的なEditor update callbackは各測定終了時に解除し、Recorderも破棄する。影を無効にした診断測定は終了時に元へ戻す。
- 計測用コードのコンパイル失敗、JSONシリアライズエラーによる停止中の採取は無効とし、以下のJSONには正常に再生が進んだ採取だけを保存する。

| 条件 | 有効フレーム | フレーム中央値 | p95 | 描画命令数の中央値 |
| --- | ---: | ---: | ---: | ---: |
| 変更前、影600m・4カスケード・4096 | 200 | 73.57ms | 86.71ms | 105,196 |
| 変更前の形状で影だけ無効 | 646 | 22.33ms | 27.30ms | 20,936 |
| 統合後、影は変更前と同じ | 948 | 15.68ms | 18.24ms | 16,959 |
| 統合後、影300m・2カスケード・2048 | 1,675 | 8.72ms | 10.31ms | 7,405 |

生データは `baseline.json`、`shadows-off.json`、`combined-original-shadows.json`、`combined-tuned-shadows.json`。変更前から最終状態へのフレーム時間は約88％減、描画命令数は約93％減。中央値の逆数に換算したfpsは約13.6→114.7。街全体や実機で同じfpsになることは保証しない。

## 保存シーン

| 対象 | 有効Renderer・変更前 | 統合後 |
| --- | ---: | ---: |
| 旧国 | 16,883 | 2,985 |
| 新国 | 46,111 | 6,078 |

全1,154組で59,539の元Rendererを5,608の統合Rendererへ置き換えた。材質ごとの三角形数（計3,581,284）と、元Rendererが無効であることを全組で検査しPASS。施設30件のTransform参照も保持した。`bake.json`、`geometry-validation.json` を参照。

保存済み統合メッシュは1,609 assets、約161MiB（Unityのテキスト形式）。元Rendererも編集・再加工用に残すため、シーンのテキスト容量は約16MB→57MBになる。Playerのビルドサイズとメモリへの影響は未測定。

初回ベイクは272.7秒。その後、同じBake内の同一幾何を再生成・保存しないcacheを追加した。cacheはBake終了時に破棄し、次回は同じGUIDの元メッシュの変更も反映する。cache追加後の全域ベイク時間は未測定。

## 変更

- 元Prefab・FBX・材質を保ち、配置Prefabごとに同じ材質／描画設定のメッシュをベイクする。元Rendererは統合したものだけ無効化する。
- 統合メッシュは `Assets/Prefabs/Kingdom/Comparison/Meshes/Combined/` に保存し、同じ構成の幾何は共有する。
- 舗装・壁UV・屋根配色・城門・橋・闘技場の加工後に統合する。地面修復では元Rendererを復元して加工し、統合し直す。
- 影は再生中だけ国シーン専用URPコピーで距離300m・2カスケード・2048に調整。停止時に元パイプラインへ戻す。編集時は共有パイプラインを使う。編集時に非永続コピーをQualitySettingsへ設定すると保存時に参照がnullになるため、編集時の上書きは行わない。
- 距離別LODと小物の追加省略は今回未実装。

## 自動検証

`CountryMeshBatchTests`、`CountryComparisonViewTests`、`CountryPavingCutoutTests`、`CountryStructureRepairsTests` のEditMode 27件と、`CountryInitialViewTests` のPlayMode 2件が通過。形状・UV・法線・接線・bounds、材質と影設定、非表示部品、再適用、元アセットの保持、再生時のパイプライン適用・復元と初期表示後の視点保持を検証する。

## 画面と保全の確認

- 起動済みEditorを外部から画面取得し、端末枠・ノッチ・角・ホームインジケーターを含め、再生中の王城周辺、新旧同時表示、停止後の王城周辺を目視した。描画2532×1170、Scale 50、Fit to Screenオフを維持した。
- 新旧切り替え、同時表示、カメラ移動と拡縮を実際のシーンで実行した。モデル・屋根配色・UIが表示され、Consoleエラーは0件。
- CityV2の元モデル・Prefab・材質等1,605ファイルのSHA-256が作業前と一致。`PC_RPAsset.asset` と `QualitySettings.asset` も作業前と一致。`source-preservation.json` を参照。
- 終了時はCountryシーン、再生停止、新国初期視点、scene dirtyなし、元の`PC_RPAsset`へ復帰。検証用のupdate callbackやRecorderは残さない。

Playerビルドとスマホ実機の性能は未確認。
