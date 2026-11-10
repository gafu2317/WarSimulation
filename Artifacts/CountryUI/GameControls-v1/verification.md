# 金額の目盛り・学校画像・ゲーム用タブ

2026-10-27。Unityへ設定済みの試作、見た目はユーザー確認待ち。現行仕様は [国家UI](../../../docs/Country/UI.md) と [月初の政務UI](../../../docs/Country/月初の政務UI.md) に集約する。

## 設計と設定

- 配分スライダーに小目盛り・大目盛り・数値を追加。最小・最大と金額の区切りを示し、上限が変わると間隔と数値も追従する。近い数値を重ねず、配分済みの範囲は淡い金、残りは灰青にする。目盛りは入力を受けず、コインとバーの操作を維持。
- 学校3種は、剣と盾、魔導書と杖、小さな礼拝堂のデフォルメ画像へ変更。太い紺色の輪郭・少ない大きな色面で揃え、配分・建設・育成に同じSpriteを使う。画像の元の色と縦横比を保つ。精霊使役学校は訓練先には含めない。
- 収入・配分・建設・育成を、丸い肩と縁を持つ画像パネルへ変更。通常は紺灰と生成り文字、選択中は金色と濃紺文字。浅い影を別レイヤーにして押下時に沈め、フォーカスの外縁を選択状態とは別に示す。
- 学校・強化／新築・兵士／武器も同じ切替部品を共有する。タブの形と色は役割を揃え、決定ボタン・一覧行・情報メモの形は区別した。

画像は [GameTab-v1.png](../../../Assets/Images/UI/Country/Controls/GameTab-v1.png) 1枚を全タブと影・フォーカスへ共有し、[SchoolEmblems-v1.png](../../../Assets/Images/UI/Country/Controls/SchoolEmblems-v1.png) 1枚から学校3種を切り出した。完成ボタンごとの画像を増やしていない。文字・アイコン・面・影は別オブジェクトのまま。背景全体や他の操作部品の一括置換は行っていない。

## Unity実画面

| 画面 | 1280×720 | 844×390 |
| --- | --- | --- |
| 配分・目盛り | [画像](Budget-PC.png) | [画像](Budget-Phone.png) |
| 収入 | [画像](Income-PC.png) | [画像](Income-Phone.png) |
| 建設 | [画像](Construction-PC.png) | [画像](Construction-Phone.png) |
| 育成・学校選択 | [画像](Training-PC.png) | [画像](Training-Phone.png) |

学校の個別選択：[戦士学校](School-Warrior-PC.png)・[魔法学校](School-Magic-PC.png)・[教会](School-Church-PC.png)。[兵士](Personnel-Phone.png)・[武器](Weapons-Phone.png) の切替にも同じパネルを使う。画像はすべて実際のGameビューから記録した。

## 検証結果

- 主画面4つ、3校、兵士／武器を切り替え、選択中の面と文字色が維持されることを確認。表示中のタブに同じ `Game-Tab` Spriteを使用。[navigation-audit.json](navigation-audit.json)。
- 最初の目盛りは100・200・300・350。別施設への配分を変えて上限が300になると、100・150・200・250・300へ更新。[比較画像](Budget-RangeChanged-PC.png)。全施設の数値とアンカー位置が、最小・最大から求めた割合に一致した。
- HDと横画面の両方でポインター処理による100→200→350→200のドラッグが通り、コインの中心と塗りの先端が一致。[HD](slider-pointer-1280x720.json)・[横画面](slider-pointer-844x390.json)。
- 画像タブを長押しして沈み込みと拡縮を実測。遷移途中のY=-2.9145、Scale=0.9757を取得し、解放後はY=0・Scale=1へ完全復元。[値](tab-animation.json)・[押下画像](Tab-Pressed-PC.png)。
- 予算の回帰テスト8/8成功。[budget-rules.json](budget-rules.json)。資金・適用月・訓練・装備のルールは変更していない。
- 2サイズで対象ボタンの文字あふれ0。目盛りの数値もあふれなし。既存ミニマップの「N」のあふれは別項目として残す。
- コンパイルエラー・新しい実行例外は0。[Console](console.json)。既存の非推奨API警告とmemoryless描画メッセージは別扱い。
- Editorは起動・再起動せず、Countryシーンの保存・再読込もしなかった。dirtyのまま12,018,166 bytes、更新時刻 `2026-10-25T01:03:21.8634571Z` で不変。[状態](scene.json)。最後は既存EditorのPlay・HD・予算画面を表示。

## 保守

素材は内蔵 `image_gen` で生成し、選んだPNGをAssetsへコピーした。外部ツールで画像を加工せず、Sprite EditorのAPIで余白の切り出しと9-slice境界を設定。[生成条件](prompts.json)・[切り出し情報](sprite-source.json)。学校の戦闘武器カテゴリ画像や個別武器画像欄は変更していない。

`WarSim > Country > Set Up Button Artwork` で共通Prefabを再設定する。通常の政務・兵士Builderも同じ部品設定を使う。`CountrySliderScale` が金額から目盛りを生成し、`CountryGovernmentTheme.SelectTab` がタブの色を揃える。変更範囲は [changed-files.json](changed-files.json)、表示集計は [display-audit.json](display-audit.json)、操作は [actions.json](actions.json)。
