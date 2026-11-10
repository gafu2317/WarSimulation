# スライダー修正・遠征文字・確定アイコン

2026-10-27。実装・検証済み、見た目はユーザー確認待ち。現行仕様は [国家UI](../../../docs/Country/UI.md)、[政務UI](../../../docs/Country/月初の政務UI.md)、[資源と収入](../../../docs/Country/資源と収入.md)。

## 修正内容

- 遠征の文字は56→68、領域は幅308・高さ104、下端から48→20へ移動。専用マテリアルの縁取り幅を0.32→0.5にした。他のメニューの共有文字マテリアルは変更していない。文字領域からも同じ遠征Buttonへ入力を渡す。
- 配分つまみは [AllocationCoin-v1.png](../../../Assets/Images/UI/Country/Controls/AllocationCoin-v1.png) の共通Spriteを28×28で使用。全施設で1枚を共用し、矩形のつまみ・グリップは描画しない。画像中心と塗りの先端を一致させ、スライダー全体の高さ112の入力範囲は維持する。
- 確定・詳細内の主操作アイコンは56×56、ボタン内の左20・上28に置き、高さ112の縦中央に揃えた。文字領域は左92から確保する。
- 初月1日の税収見込を配分案の資金へ1回だけ含める。受取済みや計画用残高へすでに含めた税収は重ねて加えない。実際の国庫・木材・鉱石は変更しない。

## 不具合の原因

初月のUIは税収見込850を表示しながら、配分上限を国庫0で制限していた。減額するとスライダーの最大値も下がり、最低額100では最小値・最大値とも100となって再増額できなかった。修正前の状態は [baseline-government.json](baseline-government.json) と [操作ログ](actions.json) に保存。

`CountryGovernmentDraft.UpdateResources` に、税収が計画用残高へ含まれているかを渡す。初月の未受取分だけを案の見積もりへ含め、同じ850を二重に足さない。実際の入金処理は従来の月末のまま。併せてつまみのpivotを中央へ戻し、バーと画像の端のずれを除いた。

矩形つまみと、減額だけで終えていた前版の確認は [ButtonArt-v2](../ButtonArt-v2/verification.md) に旧版として明記した。

## Unityの実画面

| 対象 | 1280×720 | 844×390 |
| --- | --- | --- |
| 遠征文字 | [画像](HUD-PC.png) | [画像](HUD-Phone.png) |
| 予算・画像つまみ・確定アイコン | [画像](Budget-PC.png) | [画像](Budget-Phone.png) |

[配分案の確定](Budget-Confirmed-PC.png)、遠征の [押下](Expedition-Pressed-PC.png)・[復元](Expedition-Released-PC.png) もGameビューから記録。生成した画面見本ではない。

## 検証

- 回帰テストは8/8成功。[budget-rules.json](budget-rules.json)。未受取の初月税収、最低額からの再増額、既存の税収上限・入金済み分の二重計上防止・国庫使用・建設の規則を含む。シーンを開き直すTestRunnerは使っていない。
- 2サイズでGraphicRaycasterのヒットを確認し、PointerDown／Drag／PointerUpを実行。数値を直接代入せず「100 → 200 → 350 → 200」へ移動できた。各位置で画像の中心と塗りの先端が一致。[HD](slider-pointer-1280x720.json)・[横画面](slider-pointer-844x390.json)。
- 実際の国庫は0のまま、配分700・税収見込850・予定国庫150。確定ボタンを押せて案の確認メッセージが出る。実支出は引き続き未接続。
- 2サイズで対象ボタンの文字あふれ0。既存ミニマップの「N」のあふれは別項目で記録。遠征の押下後は位置・拡縮・回転が通常時へ戻る。
- C#のコンパイルエラー・新しい実行例外は0。[Console](console.json)。既存の非推奨API警告と描画系のmemorylessメッセージは残す。
- Countryシーンはdirtyのまま保存・再読込していない。12,018,166 bytes、更新時刻 `2026-10-25T01:03:21.8634571Z` で不変。[状態](scene.json)。Editorの起動・再起動は行っていない。

CLIの仮想マウス入力は安定しなかったため、その試行は [raw-input-attempt-PC.json](raw-input-attempt-PC.json) に分け、成功した検証には数えない。上記の最終確認は実際のSliderのポインターハンドラーを通したもの。実機のタッチ操作は今回未検証で、入力設定は変更していない。

## 素材と保守

コインは内蔵 `image_gen` で生成し、選んだ画像をAssetsへコピー。PNGを外部ツールで加工していない。Sprite EditorのAPIで透明な余白を除いて切り出し、上限128で取り込む。[プロンプト・未使用案](prompts.json) と [切出し情報](coin-source.json)。他のパネルや背景の一括交換はしていない。

`WarSim > Country > Set Up Button Artwork` で既存Prefabへ設定する。`Verify Budget Rules Without Scene Changes` は回帰テスト、`Verify Slider Pointer Drag` はPlayで予算画面を開いて使う位置・増減の検証メニュー。変更範囲は [changed-files.json](changed-files.json)、表示の集計は [display-audit.json](display-audit.json)、全操作は [actions.json](actions.json)。
