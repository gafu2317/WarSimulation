# 予算画面内の操作と遠征文字

2026-10-27。実装済みの試作、見た目はユーザー確認待ち。前の [ButtonArt-v1](../ButtonArt-v1/verification.md) はHUDと基本の押下演出が中心だった。この版では画面内の操作も対象にした。現行仕様は [UI](../../../docs/Country/UI.md) と [月初の政務UI](../../../docs/Country/月初の政務UI.md) に集約する。

この版の矩形つまみはユーザーに却下され、[ButtonArt-v3](../ButtonArt-v3/verification.md) で共通のコイン画像へ変更した。初月の配分上限・主操作アイコンの位置・遠征文字もv3で修正。当時のスライダー確認は減額と最低額までであり、再増額の確認が不足していた。以下の画像と結果はv2当時の記録として残す。

## 変更

- 遠征の文字を40から56へ拡大。幅280・高さ88の独立したTMP領域にし、アイコンの幅に合わせて縮小しない。縁取りと影は既存の共有マテリアルを維持。
- 予算の税収・配分合計・国庫への残額または国庫からの補填を、名称と数値の2段へ整理。
- 国庫使用は、つまみの左右・OFF／ON・色で状態を示す横スイッチ。0.12秒で動き、初期値はOFF。
- 配分スライダーは切り欠きのない長方形のつまみと2本のグリップを使う。最低額のルールは変更しない。
- 効果詳細は、右上を折った無地の情報メモとして共有。布地の表示と不要な面取りを外し、文字と詳細リンクの領域を確保。
- 政務の主操作には袋・槌・本を別画像で重ね、予算・建設・育成の切替に応じて変更。兵士・武器・育成の詳細内の操作にも、装備・装備先の兵士・訓練を表す既存画像を使う。
- 装備の選択中は「装備先を選ぶ」と「この武器を装備」の用途を区別し、画面の種類だけでアイコンを決めない。

戻る・閉じるなどはシンプルな四角のまま。背景や作業面全体の置き換えは行っていない。新たな生成画像や、ボタンごとの完成画像は追加していない。既存の画像と形状部品を再利用し、画像・文字・面は分離したまま共通の押下演出を使う。

## Unity実画面

| 対象 | 1280 × 720 | 844 × 390 |
| --- | --- | --- |
| 遠征文字 | [HUD](HUD-PC.png) | [HUD](HUD-Phone.png) |
| 予算・国庫OFF | [画像](Budget-OFF-PC.png) | [画像](Budget-OFF-Phone.png) |
| 予算・国庫ON | [画像](Budget-ON-PC.png) | [画像](Budget-ON-Phone.png) |
| 建設 | [画像](Construction-PC.png) | [画像](Construction-Phone.png) |
| 育成 | [画像](Training-PC.png) | [画像](Training-Phone.png) |
| 武器一覧 | [画像](Weapons-PC.png) | [画像](Weapons-Phone.png) |

[兵士](Soldiers-PC.png)、[装備選択](Equipment-Picker-PC.png)、[効果詳細](Effect-Details-PC.png)、[訓練の選択](Training-Selected-PC.png)・[確認](Training-Confirm-PC.png)、[配分変更後](Budget-Slider-PC.png)、[遠征押下](Expedition-Pressed-PC.png)・[解放](Expedition-Released-PC.png) も実際のGameビューから記録。

## 確認

- 2サイズで対象ボタンの文字あふれ0。HUDには既存ミニマップの「N」のあふれがあり、別項目として記録した。
- 遠征の実際のfontSizeは56、AutoSizingはfalse、文字あふれはfalse。[実値](expedition-text.json)。拡大後も地図・旗を押せ、透明な角は入力を通す。押下後の位置・拡縮・回転が通常時へ戻る。
- 国庫使用のクリックでONとOFFを切り替え、つまみ位置と表示色が変わる。[ONの実値](switch-on-state.json)。再表示とPlayの初期状態も確認。
- 戦士学校のスライダーへ最低額未満を要求すると、100に止まる。配分合計・残額・効果表示も更新。[実値](slider-audit.json)。この操作はPlay中の案だけを変更し、最後にPlayを入り直して初期状態へ戻した。
- 予算の効果詳細を開閉、育成で対象を選んで確認を開きキャンセル、兵士から装備を選んで確認を開きキャンセル、建設・武器一覧の切替と国へ戻る操作を確認。訓練・装備の確定は実行していない。
- コンパイルエラーと新しい実行例外は0。既存の非推奨API警告、描画系の `Ignoring depth surface ... as it is memoryless` は別扱い。[Console](console.json)。
- Editorの起動・再起動、Countryシーンの保存・再読込はしていない。シーンはdirtyのまま、12,018,166 bytes、更新時刻 `2026-10-25T01:03:21.8634571Z`。[シーン状態](scene.json)。最後は既存EditorのPlay・1280×720・国家HUDを表示。

## 保守

`CountryGovernmentControlsBuilder` がスイッチ・情報メモ・スライダー・主操作の構成を共有し、既存の政務・兵士・素材Builderから同じ設定を適用する。`WarSim > Country > Set Up Button Artwork` でもシーン保存なしで共通Prefabを更新できる。金額計算、適用月、育成予約、装備の移動、工事のルールは変更していない。

操作ログは [actions.json](actions.json)、表示確認の集計は [display-audit.json](display-audit.json)、変更範囲は [changed-files.json](changed-files.json)。`before/` はこの版の着手前の対象ファイルを保持する。前の版の画像や検証結果は、この版の結果として書き換えない。
