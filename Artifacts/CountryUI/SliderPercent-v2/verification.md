# 配分目盛りの0円基準

2026-10-28。既存のUnity 6000.4.3f1、Countryシーンで確認。実装済み、見た目はユーザー確認待ち。現行仕様は [国家UI](../../../docs/Country/UI.md) と [月初の政務UI](../../../docs/Country/月初の政務UI.md)。SliderPercent-v1の最低額を0％とする基準を修正した記録。

- 0円＝0％、現在の配分上限＝100％。大目盛りの表示は0・25・50・75・100％、小目盛りは5％刻み。％に小数点は付けない。
- Sliderの描画範囲を0円起点にして、つまみと塗りを配分額÷上限の位置に揃えた。配分の制約は既存のCountryGovernmentDraftが保持し、最低必要額未満の操作は最低額へ戻す。
- HD 1280×720と844×390で5施設の整数％ラベルとアンカー位置を確認。ボタン・目盛りの文字あふれなし。既存ミニマップの「N」の判定は変更対象外。
- 両サイズでGraphicRaycasterからPointerDown・Drag・PointerUpを送り、最低額100→180→上限350→200を確認。下限のつまみ位置は100/350、塗り先端との差は0.1px未満。各位置が配分額÷上限と一致した。
- 0円を指定しても配分額は最低必要額100に留まる。魔法学校の150→200の変更で戦士学校の上限350→300になると、配分額200のつまみ位置が2/3へ追従する。検証後は初期配分へ戻した。
- 既存の配分ルール8件成功。コンパイル完了、新規C#エラー・実行時例外なし。既存の非推奨API警告とmemoryless depthのメッセージは除外して確認。
- Editor起動・再起動、シーン保存・再読込なし。Countryシーンのファイルサイズ・更新日時・dirty状態は作業前と同じ。既存EditorをHDの配分画面で残した。

実画面: [HD](Budget-PC.png)、[最低必要額の位置](Budget-Minimum-PC.png)、[上限変更後](Budget-LimitChanged-PC.png)、[844×390](Budget-Phone.png)。

検証データ: [表示](display-audit.json)、[HDドラッグ](slider-pointer-1280x720.json)、[844×390ドラッグ](slider-pointer-844x390.json)、[上限変更後のつまみ](limit-changed-handle.json)、[配分ルール](budget-rules.json)、[Console](console.json)、[シーン](scene.json)、[操作ログ](actions.json)。
