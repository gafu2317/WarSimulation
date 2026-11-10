# 15施設の固有アイコン

2026-10-28。ユーザーが開いたUnity 6000.4.3f1のCountryシーンで検証。設定済み、見た目はユーザー確認待ち。現行仕様と施設ごとのモチーフは [国家UI](../../../docs/Country/UI.md#政務画面の共通部品)、政務画面の操作範囲は [月初の政務UI](../../../docs/Country/月初の政務UI.md) を参照する。

## 素材と共用

既存の学校3種のPNGとSprite参照を維持し、12種類を [FacilityEmblems-v1.png](../../../Assets/Images/UI/Country/Controls/FacilityEmblems-v1.png) の1枚へ追加。全15種類で2枚の透過画像を共有する。Spriteと文字・背景は別オブジェクト。配分・建設・育成と、街のバナー・施設ポップアップで同じ画像を使う。精霊施設を訓練先へ加えていない。施設の費用・効果・建築済み状態と、戦闘の武器カテゴリ画像は変更していない。

組み込みのimage_genで生成。参照は学校3種の画風のみ。生成画像は1448×1086のRGBAで、透過範囲を含めてプロジェクトへバイト単位でコピーした。画像編集による背景消去や描き足しはしていない。輪郭の連結範囲から12種類を特定し、Unity Sprite Editor APIで切り出した。再設定時もSprite IDを保持する。新しい画像は読取不可、Bilinear、MipMapなし。既存の学校PNGは生成元と同一バイトであることを確認。

- [生成条件](prompts.json)：組み込みツール、参照画像と最終プロンプト
- [原画像の情報](source.json)：コピー元、出力先、寸法、SHA-256
- [Spriteの範囲](sprite-regions.json)、[輪郭の範囲](connected-bounds.json)

## Unityでの確認

- カタログはHD 1280×720と844×390で、それぞれ15種類・15個の固有Spriteを確認。実際のCountryFacilitySymbolを描くPlay中だけの確認用表示で、ゲームの画面として保存していない。
- 3つの街の45バナーすべてに正しいSpriteを設定。表示中の街の15施設を開閉し、ポップアップが同じ画像を使うことを確認。広場は実際のバナーButtonからも開閉した。
- バナーは56×56の絵＋名前、施設ポップアップは96×96の絵＋施設名・分類。バナーの幅と文字の余白を調整し、名前の折り返しを解消。画像はRaycastを受けず、既存Buttonの入力を使う。
- 配分の学校と下側の精霊施設・鍛冶場、建設の図書館・天文台・鍛冶場・精霊施設、育成の学校、街と施設ポップアップを確認。HDと844×390の16枚を記録し、変更した文字のあふれなし。既存ミニマップ「N」の判定は変更対象外。
- 配分スライダーは基準額850を維持し、0への操作は最低額100に留まる。170・200への配分も確認し、200へ戻した。％の表示・入金・国庫・施設の計算は変更していない。Prefab更新時に目盛りの保存値を変えないため、目盛りの更新はPlay中だけに限定した。
- Spriteの取込とC#のコンパイルを完了。新規のC#エラー・実行時例外なし。既存の非推奨API警告とmemoryless depthのメッセージは除外して確認。
- Editor起動・再起動、シーン保存・再読込は行っていない。Countryのファイルサイズ・更新日時・dirty状態は作業前と同一。既存EditorをHDの配分画面の下側に残し、追加した精霊施設・鍛冶場が見える状態にした。

画像： [15種類・HD](Catalog-PC.png)、[15種類・844×390](Catalog-Phone.png)、[配分の下側](Budget-Lower-PC.png)、[図書館の建設](Construction-Library-PC.png)、[天文台の建設](Construction-Observatory-PC.png)、[精霊施設の建設](Construction-Spirit-PC.png)、[鍛冶場の建設](Construction-Blacksmith-PC.png)、[育成](Training-PC.png)、[街のバナー](HUD-PC.png)、[施設ポップアップ](Plaza-Phone.png)。

データ： [45バナー・15ポップアップ](bindings.json)、[画面と文字の確認](display-audit.json)、[更新したPrefab](changed-prefabs.json)、[変更したファイル](changed-files.json)、[Console](console.json)、[シーン](scene.json)、[操作ログ](actions.json)。
