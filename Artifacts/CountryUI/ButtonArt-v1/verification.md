# ボタンのモチーフと押下演出

2026-10-27。Countryシーンの既存Editorで設定・確認。実装済みの試作であり、見た目はユーザー確認待ち。現行仕様は [国家画面のUI](../../../docs/Country/UI.md)、操作要件は [月初の政務UI](../../../docs/Country/月初の政務UI.md) に集約する。

## 設定した内容

- 戻る・閉じる・確定・装備変更など、モチーフのない操作は直角の長方形。意味のない切り欠きを除去した。
- 左下4メニューは袋・本・槌・兜の既存アイコンと接続構造を維持し、面の上下の明暗の帯を非表示にした。
- 遠征は折り地図そのものをボタンの形とし、旗・文字・地図・浅い影を別オブジェクトにした。四角いボタン背景や金属の勲章は使わない。
- 素材は [ExpeditionParts-v1.png](../../../Assets/Images/UI/Country/Buttons/ExpeditionParts-v1.png) 1枚。地図と旗をSprite EditorのAPIで切り出し、地図を影にも再利用する。文字はTMPのまま保持する。光沢・発光・光の筋を追加していない。
- 共通の押下演出は0.06秒で沈み、0.14秒で復元する。通常3・遠征5の移動、面97.5%の拡縮、内容の追加移動を使う。旗はフォーカス時に根元から4度傾ける。
- 遠征の入力形状は地図と旗のアルファを組み合わせ、透明な角は入力を通す。通常ボタンは既存の入力範囲を維持する。

背景・パネル全体の交換、ゲームルールの変更、遠征先の接続は行っていない。遠征のクリック先は従来どおり未接続。

## Unity実画面

| 状態 | 1280 × 720 | 844 × 390 |
| --- | --- | --- |
| 通常 | [画像](Normal-PC.png)・[値](Normal-PC.json) | [画像](Normal-Phone.png)・[値](Normal-Phone.json) |
| 押下 | [画像](Pressed-PC.png)・[値](Pressed-PC.json) | [画像](Pressed-Phone.png)・[値](Pressed-Phone.json) |
| 解放 | [画像](Released-PC.png)・[値](Released-PC.json) | [画像](Released-Phone.png)・[値](Released-Phone.json) |

[旗のフォーカス](Focused-PC.png)、[左下の押下](Budget-Pressed-PC.png)、[配分](Budget-PC.png)、[建設](Construction-PC.png)、[育成](Training-PC.png)、[兵士](Personnel-PC.png)、[武器](Weapons-PC.png) も実際のGameビューから記録した。生成した画面見本ではない。

## 確認結果

- 2サイズで地図と旗のヒットを確認し、透明な角のヒットはfalse。
- 遠征は押下時に面のY=-5・Scale=0.975となり、旗と文字も個別に移動。解放後は位置・Scale・回転・影が通常時と一致。
- フォーカス時の旗はZ=-4度。左下予算ボタンもY=-3の押下と、解放時の復元を確認。
- 対象ボタンの文字あふれは0。既存ミニマップの「N」は別項目としてあふれが報告される。この変更の対象外で、ミニマップは未変更。
- 予算・育成・建設・兵士から各画面を開き、戻る操作で閉じることを確認。兵士から武器一覧へ切り替え可能。
- 予算ボタンと遠征ボタンの `CountryButtonFeedback` はそれぞれ1個。遠征に `CountryButtonShape` を1個追加。
- C#のコンパイルエラー・実行例外は発生していない。既存の非推奨API警告と、描画系の `Ignoring depth surface ... as it is memoryless` 2件は記録に残す。
- Countryシーンはdirtyのまま保存・再読込していない。ファイルは12,018,166 bytes、更新時刻は `2026-10-25T01:03:21.8634571Z` で変わらない。Editorの起動・再起動は行っていない。

実測値は [animation-audit.json](animation-audit.json)、部品数は [component-audit.json](component-audit.json)、開閉は [navigation-audit.json](navigation-audit.json)、操作とConsoleは [actions.json](actions.json)・[console.json](console.json) に記録した。ドメインのゲームルールは変更していないため、今回の確認はコンパイルと実画面・入力・演出・導線を対象とした。

## 素材と再設定

画像は内蔵 `image_gen` で生成し、選んだ1枚をAssetsへコピーした。最終プロンプト・生成方法・未使用案は [prompts.json](prompts.json) に保存。最初の光沢のある金属枠、用途に結び付かない幾何形状の案は現行UIに設定していない。`rejected-geometric-and-glossy/` は未使用案の保管場所であり、採用部品ではない。

`WarSim > Country > Set Up Button Artwork` で素材の取込・共通ボタン・遠征・演出参照を再設定できる。Playモードを停止して使う。シーン保存・再読込・Editor起動は行わない。Sprite IDを保ち、Prefabに名前付きSprite参照を保存する。

今回の変更範囲と比較用ハッシュは [changed-files.json](changed-files.json)。`before/` は着手前の対象Prefab・仕様書を保管している。`CountryButtonFeedback.cs` の着手前ソースは、最初の全文読取記録から復元して保管したもの。
