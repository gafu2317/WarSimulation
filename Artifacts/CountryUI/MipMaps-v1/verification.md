# 共用SpriteのMip MapとTrilinear

2026-11-03。共有素材10枚へ反映済み。実画面の確認とユーザーによる見た目の採用は分けて扱い、見た目はユーザー確認待ち。

## 変更

`CountryButtonSpriteImporter` の通常取り込みをMip Map有効・Trilinearへ変更した。`WarSim > Country > Update Shared Texture Sampling` で既存素材にも同じ設定を反映する。この更新メニューは対象の取り込み設定だけを変更し、Spriteの切り出しやPrefabの再生成は行わない。[生成元の差分](source.diff)

| 画像 | 用途 |
| --- | --- |
| `GameTab-v1.png` | 政務・学校・建設種別・遠征などの共通タブ |
| `RosterParts-v1.png` | 一覧の面と選択枠、円形の縁、空き枠、立ち絵枠、共通の詳細操作 |
| `CommandMotifParts-v1.png` | 配分の指令書・封蝋、遠征行の巻物など。同じ画像内の旧教本パーツの採用状態は変更しない |
| `TrainingPennantParts-v1.png` | 訓練開始・予約の旗と支柱 |
| `ConstructionSignParts-v1.png` | 建設・強化の看板と留め具 |
| `ExpeditionParts-v1.png` | 遠征ボタンの地図と旗 |
| `SchoolEmblems-v1.png` | 学校3種のアイコン |
| `FacilityEmblems-v1.png` | 追加12施設のアイコン |
| `AllocationCoin-v1.png` | 配分つまみ・政務タブのコイン |
| `MapSymbols-v1.png` | 他国の本拠地の城と旗 |

10枚は実際にTrilinearとなり、Mipレベル数は1から8〜12へ変わった。全36SpriteについてGUID・fileID・切り出し矩形・ピボット・9-slice・Pixels Per Unitが一致する。画像サイズ、画素形式、Read/Write設定も維持。[取り込み前](before-textures.json)、[取り込み後](after-textures.json)、[照合](verification.json)

メタファイルの差分は `enableMipMap: 0 → 1` と `filterMode: 1 → 2` だけ。[メタファイル差分](meta-diffs.json)。元PNGや表示サイズ・配置・色は変えていない。フォントSDFと背景素材も維持した。

## 見た目と操作の確認

- 36Spriteを3段階の表示サイズで並べ、HD 1280 × 720と844 × 390で確認。分離描画20枚を目視し、確認した大きさでは隣のSpriteの絵が混入するにじみは見られなかった。小さい横画面で施設一覧の末尾が切れた検査画像は、検査用の配置を直して取り直した。
- 実Gameビューでは育成・配分・建設・遠征地図・HUDを2サイズで確認。育成では選択前後、通常・有効な旗、詳細操作の押下と解除も確認した。タブ切り替え、兵士の選択と解除、国へ戻る、遠征画面の開閉は動作した。訓練開始・配分確定・建設・出発は確定していない。
- 黒い角と円形の縁の段差が減り、線の形と選択色を保っている。学校・施設のアイコンや看板・旗は縮小時に少し柔らかくなる。文字のフォントや配置は変更していない。
- 取得した実画面13枚と分離描画20枚のパス・SHA256・目視範囲は [eye-review.json](eye-review.json)。全ての画面サイズや連続した拡大率、あらゆるSpriteの使い方を保証するものではない。

実Gameビューの主な確認画像：

- 育成HD：[変更前](before/Training-1280x720.png)、[変更後](after/Training-1280x720.png)、[選択中](after/Training-AfterTabs-1280x720.png)
- 育成の小さい横画面：[通常](after/Training-844x390.png)、[選択中](after/Training-Selected-844x390.png)、[押下中](after/Training-Pressed-844x390.png)
- 配分：[HD](after/Budget-1280x720.png)、[小さい横画面](after/Budget-844x390.png)
- 建設：[HD](after/Construction-1280x720.png)、[小さい横画面](after/Construction-844x390.png)
- 遠征：[HD](after/Expedition-1280x720.png)、[小さい横画面](after/Expedition-844x390.png)
- HUDの遠征ボタン：[HD](after/HUD-1280x720.png)、[小さい横画面](after/HUD-844x390.png)

## コンパイル・環境・保持

Unity 6000.4.3f1の既存EditorをUnity CLI Bridgeで操作した。画像設定の反映中はPlayを一時停止し、終了時はHDの育成画面でPlayを再開した。Editorの起動・再起動、シーン保存・明示的な再読込は行っていない。Countryは変更前と同じ未保存状態。[Editor](editor-final.json)、[シーン](scene-final.json)

変更前に98ファイルを記録し、そのうち差分は対象10枚のメタファイルだけ。シーン18個、共有Prefab、PNG、フォント、ProjectSettingsには内容の変化なし。[保持確認](preservation.json)。生成元のC#と既存仕様書は今回の変更対象として別に扱う。

新しいスクリプト例外やコンパイルエラーは見られなかった。Consoleには既存のmemoryless depthのload／storeメッセージと、ユーザーの調査前の画像にも出ていたNoto SDFのNativeFormatImporter不整合メッセージが残る。フォントファイルの内容は不変。接続切断時のキュー破棄はBridgeの`LogWarning`であり、旧Console取得APIがErrorと返すため区別した。[Console](console-final.json)、[分類と状態](status.json)

検査用Editorコードは [記録用のコピー](CountryTextureSamplingReview.cs.txt)を残し、Assets内のコードとメタは削除済み。[操作ログ](actions.json)、[実行時の画面確認スクリプト](check_live.py)も記録として保管する。Unity Test Runner、Standalone/WebGLビルド、実機タッチ検証は行っていない。

## 現行仕様と公開状態

現行のルールは [UIの画像の縮小描画](../../../docs/Country/UI.md#画像の縮小描画) に集約する。調査段階のコピーを使った比較は [TextLayout-v1の原因調査](../TextLayout-v1/verification.md#追加指摘黒い輪郭の粗さ2026-11-02の原因調査)であり、この反映後の実画面と混同しない。

取り込み設定の反映作業時点ではUnityプロジェクトだけが対象で、公開版16には含まれていなかった。その後、同日の配色変更と合わせてWebGLを再ビルドし、itch.io版17（`country-color-20261103`）へ収録した。ビルド、配信データの照合、公開版の起動・操作は [ColorPass-v1の配布記録](../ColorPass-v1/verification.md#itchio更新) を参照する。上の画像とEditor状態は取り込み設定の検証時点の記録として保つ。
