# Countryを入口にしたWeb版

2026-10-29実装、2026-10-30公開確認。追加ボタンの見た目はユーザー確認待ち。以下は動作検証の記録で、見た目の採用を意味しない。

## 実装

- CountryをWebGLの先頭に固定し、GafuTestと有効な戦闘マップを収録する。
- 国家の設定の「戦闘へ移動」と、戦闘の編成画面の「国家へ戻る」で往復する。
- 国家の時計を止め、資源・日付・兵士・装備・訓練・遠征・防衛・襲撃設定と政務の操作案を起動中保持する。戦闘結果は国家へ反映しない。

## 自動テスト

- PlayMode: `PlaytestSceneNavigationTests` の2件が成功。3回の往復、未適用の無効な資源入力の破棄、国家データの同一性と値、政務案・建設案、襲撃設定、停止状態、時間倍率、単独シーン、Managerの重複なし、指定フォント、マップ準備、戦闘シーン直接起動からの帰還を確認。
- PlayMode run ID: `1063f7ec-22a7-4c5b-b05c-1ac49f26928f`。所要約32.7秒。最初の実行は準備途中でマップの完了を判定して失敗したため、完了まで待つ条件へ修正した後の結果を使う。
- EditMode: 国家の資源・政務・兵士・遠征・防衛・日付とビルド収録の106件が成功。run ID: `e4b0b0e8-fc1d-4a8e-9d0e-2d7dd21f797d`。所要約3.2秒。
- Release版のログ読込修正後、既存の `CombatBattleLogFormatterTests` 17件が成功。run ID: `baa22a1f-7c5e-4975-a6fb-47b091a5b17b`。
- CLIの集計にはスイートも含まれるため、124というPassed集計ではなく実テスト数106を記録する。既存CLIの複数CollectorによりPlayMode用JSONも後のEditMode結果で上書きされるため、PlayModeの根拠は上記run IDに対する取得済み結果とする。

## Editor上の操作・表示

- 既に開いていたWarSimulationのEditor（PID 94381、Unity 6000.4.3f1）を使用。Editorの起動・再起動は行っていない。
- Countryの設定と戦闘の編成画面を1280×720、844×390で撮影。追加ボタンの文字あふれは0、既存部品と指定Noto Sans JPを使用。
- 国家の設定のボタンから戦闘へ移り、戦闘を開始してRunningを確認。勝利イベントを発生させて結果画面を確認し、既存の編成へ戻るボタンを操作。そこから国家へ帰還して時計停止・時間倍率1・試聴音停止を確認した。勝利は自然決着の検証ではない。
- 画像: `Settings-1280x720.png`、`Settings-844x390.png`、`Combat-1280x720.png`、`Combat-844x390.png`。

## WebGLと公開

- 初回WebGLビルドは成功（エラー・警告0、約382.8秒）。記録は `build-before-release-fix.json`。
- ChromeでCountry起動、設定表示、戦闘への移動を確認。Releaseビルドで `CombatBattleEventLogger` の読込時に32／44バイトのシリアライズ形式不一致を検出した。クラス全体がEditor／Development限定であるため、`CombatReleaseSceneProcessor` でReleaseのビルド用シーンからログ用コンポーネントを除去する。Editor／Developmentのログ処理と、保存済みシーン・Prefabは維持する。フィールドをReleaseに残す実験は同じ読込エラーが続いたため撤回し、元のクラスへ戻した。検証途中の増分ビルドは `build-before-clean.json` に記録する。
- 初回のChromeではURPのFSR Upscalingシェーダーが非対応という警告も出た。起動と画面表示はできており、この変更でレンダリング設定は変更しない。
- Release用シーン処理を含むクリーンビルドが成功。エラー・警告0、約540.3秒、元の成果物314,239,686バイト。`build.json` に記録。CountryとGafuTestの保存済みシーンのSHA-256は前後で一致した。
- クリーンビルドをChromeで起動し、Country→設定→戦闘編成→戦闘開始→一時停止（ボタンが「再開」に変化）→編成→国家帰還を操作。実際の戦闘でHP変化を確認。国の日付・資源は初期値を維持し、全経路でブラウザのerrorログ0件。`Local-Country-return.jpg` に帰還画面を記録。
- itch.ioへ最初に反映したversion 10（build ID `2241769`）は、HTMLの1ファイル200MB制限によりデータ287.23MBが拒否された。直前の動く版をversion 11（`2241784`）として復旧後、128MiB・128MiB・約31.2MiBの3ファイルへ分割した。結合時のSHA-256とバイト数が元データと一致し、全ファイル200MB未満。`packaging.json` に記録。
- 圧縮状態の分割はローカルで起動したが、version 12（`2241808`）の公開環境ではデータサイズ不一致が起きた。HTTPヘッダーを調べると先頭の分割ファイルだけに `Content-Encoding: gzip` が付き、ブラウザがそこだけ自動展開していた。version 13（`2241838`）として直前版へ復旧した。
- 最終方式はGzipを展開したUnityWebDataを通常の `.bin` へ分割する。128MiB・128MiB・124,666,300バイトの3ファイル、合計393,101,756バイト。結合後のSHA-256は元の展開済みデータと一致し、全ファイル200MB未満。通常のJSONと分割バイナリを結合するHTMLを含め20ファイル、配布物は計406,156,640バイト。
- 最終方式もローカルChromeでCountry起動・errorログ0件を確認。分割梱包はEditorの専用メニューから実行でき、ビルド成功後にも自動で適用する。元のゲームデータを変えず、配信の圧縮判定に依存しない。
- 最終方式をversion 14、build ID `2247631`、userVersion `country-20261029.2` として公開。itch.io側の処理完了は2026-10-30 07:09 JST。`publish-status.jsonl` に記録。
- 2026-10-30、Chromeで[公開ページ](https://gafu2317.itch.io/webtest)を開き、Country起動→歯車→設定の「戦闘へ移動」→戦闘編成→「国家へ戻る」を実際に操作した。帰還後も1月1日・人口8,500・税収850・木材1,200・鉱石800、時計停止を確認し、ブラウザーのerrorログは0件。公開配信されたゲームの直接表示でもCountry起動と戦闘への移動を確認した。
- 公開ページの画像は `Published-Settings.jpg`、`Published-Combat.jpg`、帰還後の `Published-Country.jpg`。公開分割データ4ファイルのHTTP応答はすべて200、`Content-Encoding`なしであることを `public-data-headers.json` に記録した。
- 最後に `git diff --check` が成功し、CountryとGafuTestの保存済みシーンのSHA-256も記録と一致。検証用ローカルサーバーを停止し、公開ページのタブを残した。
