# Webプレイテスト

国家画面を入口に、独立した戦闘画面へ往復できるWebGL版を共有する手順。Auto Battleの連戦とは別用途。

国家UIの確認は既存EditorのDevice Simulatorで、ノッチ付き端末の横画面2532×1170、Scale 50、Fit to Screenオフを使う。手順の管理元は [国家UIの入力と表示確認](Country/UI.md#入力と表示確認)。itch.ioの依頼は既存配布物の更新だけを行い、更新後のブラウザー起動・画面／動作検証・公開状態の追加確認は行わない。

## 画面の切り替え

起動シーンは `Country`。歯車の設定画面にある「戦闘へ移動」で `GafuTest` の編成画面を開く。戦闘側の編成画面の「国家へ戻る」で戻る。戦闘中・結果画面では既存の「編成へ戻る」を経由する。

確定した国家の進行はブラウザーの `PlayerPrefs` に保存し、同じブラウザーの保存領域から再開する。戦闘中と帰還後は国家の時計を停止する。設定から開く自由戦闘は国家へ結果を反映しない。遠征・防衛は登録した編成で直接戦闘を開始し、勝敗・撤退時にCPと耐久を一度だけ消費する。遠征は勝利時だけ領地を獲得し、結果画面の「国へ戻る」で帰還する。[進行の保存と再開](Country/時間進行.md#進行の保存と再開) · [Unity PlayerPrefs](https://docs.unity3d.com/jp/current/ScriptReference/PlayerPrefs.html)

## ビルド

Unityメニュー:

`Tools/War Simulation/Web Playtest/Build WebGL`

出力: `.unity/CombatPlaytestWebGL/`

ユーザーが開いた既存EditorをWebGLに切り替えて実行する。AIはEditorを起動・再起動しない。Release・Gzip・Decompression Fallbackで出力する。開いているシーンに関係なく、`Country`、`GafuTest`、Build Settingsで有効な残りのシーンの順で収録する。両シーンと有効な戦闘マップが存在しない場合はビルドを開始しない。

自動化では同じシーン順と設定を使う `CombatPlaytestWebGLPlayer.BuildPlayer()` を呼ぶ。これは完了ダイアログを開かず `BuildReport` を返す。

ビルド前に、開いているシーンの未保存状態と保存ファイルのハッシュを記録する。2026-11-03の更新では、保存確認で「Don't Save」を選んでもビルド後にCountryの保存内容と未保存状態が変わり、ファイル復元後の再インポートでも読み込み中の状態が変わった。未保存状態があるまま自動ビルドを始めず、作業内容の扱いを先に決める。保存しない選択だけで状態が保たれるとは扱わない。Editorの起動・再起動やシーンの保存・再読込をAIが行ってよいという許可ではない。

ビルド成功後、itch.ioのHTML用の1ファイル200MB上限に合わせ、Gzipを展開したUnityデータを128MiBずつの通常の `.bin` ファイルへ分割する。`Build/PlaytestData.json` が分割ファイルの順序を持ち、生成する `index.html` が結合してUnityへ渡す。結合結果は元の展開済みデータと一致する。圧縮状態のまま分割すると、配信側が先頭ファイルだけへ `Content-Encoding: gzip` を付けて壊すため使わない。フォルダ全体を配布し、分割ファイルやJSONを省略しない。既存の成果物だけを梱包し直す場合は `Tools/War Simulation/Web Playtest/Package itch.io Files` を使う。同じ成果物に繰り返し実行できる。

`PackageForItch` は出力HTMLへ表示領域に追従する共通CSSを追加する。デスクトップ用の960×600固定指定を上書きし、Canvasを埋め込み領域へ収める。縦に余裕のあるウィンドウではゲームの比率が16：9より狭くならないよう高さを制限して中央へ置き、固定幅のHUDが横へ切れないようにする。高さ480px以下または幅600px以下ではUnityのフッターを隠してゲーム領域を確保する。1280×720ではフッター38pxを除く1280×682、844×390ではCanvas全体を844×390で表示する。国家UIのHD基準やCanvasScalerをこの都合で変更しない。

2026-11-03のHTML修正は[UXFix-v1](../Artifacts/CountryUI/UXFix-v1/verification.md)で寸法検証後、公開版19のWebGL本体でも確認した。1280×720と844×390の確認は公開配信先を直接開いて実施し、itch.io内の埋め込み起動・育成操作も別に確認した。

Releaseでは、Editor／Development専用の `CombatBattleEventLogger` を `CombatReleaseSceneProcessor` でビルド用シーンから除去する。公開版で存在しないクラスを読み込むことによる保存形式の不一致を防ぐ。保存済みシーンやDevelopmentビルドの診断ログは変更しない。

ビルド完了ダイアログは、分割後のファイル構成についてサイズを確認する。

2026-11-26に、保存と戦闘HUDの変更を含むWebGL Releaseを検証用の一時フォルダーにビルドした。国家を起動シーンとする11シーンのビルドに成功し、エラー・警告は各0件。対象シーンと `ProjectSettings.asset`、`EditorBuildSettings.asset` のハッシュはビルド前後で一致した。一時ページでCanvasの実描画・表示を2532×1170に揃え、ローカルのブラウザーで1月9日まで進めて停止した。ページを再読込すると、同じ日付と停止表示が復元された。この確認では公開先へのアップロードは行っていない。

## 配布先

- 展開後の最大ファイルが100MB未満: `gh-pages`
- 100MB以上: itch.io。Git には載せない

ビルド成果物は本線ブランチへ入れない。

## gh-pages（従来の単一データ版）

以下は分割前の単一データを扱う従来の手順。今回のCountry版は既存のitch.ioを更新する。

`.unityweb` を展開し、ファイル名から `.unityweb` を外す。`index.html` の参照も展開後の名前に合わせる。

更新するのは `gh-pages` ブランチだけ。GitHub は1ファイル100MBまでなので、展開後に超えるファイルはプッシュできない。

## itch.io

更新のみを行い、更新後のブラウザー起動・画面／動作検証・公開状態の追加確認は行わない。以後の更新にも適用する。配布形式は既存のローダーと128MiB分割を維持する。

ページは `gafu2317/webtest`（HTML）。展開はしない。`index.html` が出力フォルダの直下にある状態で上げる。

初回だけ人が行う。ページを HTML で作り、`butler login` でこの Mac に資格情報を保存し、Uploads の `html5` で「This file will be played in the browser」を有効にする。

更新はエージェントが行う。プロジェクトルートで次を実行する。

```bash
butler push --assume-yes .unity/CombatPlaytestWebGL gafu2317/webtest:html5
```

`butler` が無い、または未ログインなら、人が `~/.local/bin` へ入れたうえで `butler login` する。ログインはブラウザの許可が要るので、エージェントは代わりに完了できない。

## 最新の配布記録

2026-11-25、`bb445969` のWebGL Releaseを `webtest-20261125-174646` として `gafu2317/webtest:html5` にアップロードした。butlerはアップロード成功と配信処理の開始を返した。既存ローダーを使い、配布は19ファイル、ゲームデータは128MiB単位の2分割。ビルドはエラー0・警告6で、収録した11シーンの保存内容はビルド前後でSHA-256が一致した。

2026-11-18、`webtest-20261118-175720` 向けのWebGL Releaseビルドを作成し、既存のローダー・128MiB分割でアップロードを実施。ビルドはエラー0・警告1（Noto Sans JP SDFインポーター不整合）、対象11シーンの保存内容はビルド前後で一致。更新後の公開状態・起動・動作確認は行わない。

2026-11-18、役割施設の階高調整を含む保存済みのCountryシーン（183棟）をWebGL Releaseでビルドし、`country-layout-20261118` として `gafu2317/webtest:html5` にアップロードした。butlerはアップロード成功と配信処理の開始を返した。既存ローダーを使い、配布は19ファイル、データは128MiB単位の2分割。結合データはUnityの元データとSHA-256で一致した。ビルド前後で対象11シーンと街のレイアウトアセットのハッシュが一致した。ビルドはエラー0・警告1で、Noto Sans JP SDFのインポーター警告と同アセットの再書き込みを確認した。

2026-11-10、国家シーンの軽量化を含むWebGL Release版を、元のローダー・128MiB分割で `country-performance-20261110-restored`（build `2343551`）として公開した。butlerの処理完了を確認。ゲームデータの結合結果はビルド直後とSHA-256で一致し、配布は21ファイル・データ4分割。ビルドはエラー0・警告2（Noto SDFインポーター不整合）、ビルド対象11シーンの保存内容を保持した。記録は `Artifacts/ItchUpdate20261110/`。復元版への追加ブラウザー検証は行っていない。

2026-11-03、[検査結果への修正](../Artifacts/CountryUI/UXFix-v1/verification.md#itchio更新)を `country-ux-20261103-r2`、`html5` の版19（build `2286556`）として公開した。[公開ページ](https://gafu2317.itch.io/webtest)の更新表示と、butlerのcompletedを確認。WebGL Releaseビルドは成功、エラー0・警告2。警告はNoto SDFインポーター不整合で、今回はフォントファイルの内容もビルド前から変わっている。分割データ3個の結合結果が元の展開済みデータと一致し、最大ファイル128MiB・全20ファイルを確認した。

版18へゲーム本体を公開した後、縦に余裕のあるブラウザーでHUDが横へ切れる問題を確認し、梱包用HTMLの表示比率を修正した。版19は同じゲーム本体へこのHTML修正を加えたもの。公開ページ内の起動と育成、公開配信先でのHD・844×390の育成・技説明・装備比較・取消・配分・報告・遠征先の可否を確認した。ブラウザーのエラーは0件、FSRアップスケール用シェーダー非対応の警告は残る。実機タッチや既存戦闘までの通し確認、見た目のユーザー採用は別に残す。

このビルドではCountryの保存内容と読み込み中の状態が変わった。元の保存ファイルを検査前のハッシュへ戻し、作業状態の控えを残した。ユーザーは自身の未保存作業がないと回答し、復元用シーンは開いていない。シーンを無変更・未保存状態維持で完了したとは扱わない。経緯と最終状態はUXFix-v1へ記録した。

以前の版17（`country-color-20261103`、build `2284657`）の配色・Mip Mapと公開確認は [ColorPass-v1](../Artifacts/CountryUI/ColorPass-v1/verification.md#itchio更新)、版16（`country-text-layout-20261102`、build `2278066`）の文字配置・余白と読み込み失敗の記録は [TextLayout-v1](../Artifacts/CountryUI/TextLayout-v1/verification.md#itchio更新) に残す。過去の公開画像と現行版の検証を混同しない。
