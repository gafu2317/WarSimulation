# 税収・国庫を基準にした配分％

2026-10-28。ユーザーが開いたUnity 6000.4.3f1のCountryシーンで検証。実装済み、見た目はユーザー確認待ち。現行仕様は [国家UI](../../../docs/Country/UI.md) と [月初の政務UI](../../../docs/Country/月初の政務UI.md)。SliderPercent-v2の施設ごとの配分上限を100％とする基準を変更した記録。

- OFFでは適用月の税収、ONでは税収＋国庫を100％として全施設で共有。入金済みの税収を二重加算しない。配分・建設案の支出で％の基準額は減らさない。
- 配分額170・税収850でつまみが20％、国庫850を加えた1700では同じ配分額が10％の位置にあることを実画面とRectTransformで確認。他施設の配分150→200でも基準額850と、配分額200のつまみ位置200/850は変化しない。
- 0円＝0％、0・25・50・75・100％の整数表示、5％刻みの小目盛りを維持。最低必要額と残り資金による配分上限は独立した制約とし、範囲外へドラッグしても許される金額に留まる。
- 税収850と国庫ONの1700をHD 1280×720、844×390で確認。5施設が同じ基準額を使い、整数％ラベルを維持。ボタン・目盛りの文字あふれなし。既存ミニマップの「N」の判定は変更対象外。
- GraphicRaycasterからPointerDown・Drag・PointerUpを送り、両サイズと両基準で最低必要額・残り資金の上限・つまみの金額比率を確認。コイン中心と塗り先端の差は0.1px未満。
- 配分ルール11件成功。入金前後の税収の二重加算防止、配分・建設案に依存しない基準額、税収0の場合を含む。コンパイル完了、新しいC#エラー・実行時例外なし。既存の非推奨API警告とmemoryless depthのメッセージは除外して確認。
- 国庫ONの検証ではPlay中の国庫を一時的に850に変更し、検証後に0へ復元。配分も初期値に戻した。Editor起動・再起動、シーン保存・再読込は行っていない。Countryのファイルサイズ・更新日時・dirty状態は作業前と同じ。既存EditorはHD・国庫OFFの配分画面で残した。

実画面: [税収の20％](Budget-Tax20-PC.png)、[税収＋国庫の10％](Budget-Treasury10-PC.png)、[国庫ON・HD](Budget-Treasury-PC.png)、[国庫ON・844×390](Budget-Treasury-Phone.png)、[国庫OFF・HD](Budget-Tax-PC.png)、[国庫OFF・844×390](Budget-Tax-Phone.png)。

検証データ: [表示](display-audit.json)、[配分ルール](budget-rules.json)、[OFF・HDのドラッグ](slider-pointer-1280x720.json)、[OFF・844×390のドラッグ](slider-pointer-844x390.json)、[ON・HDのドラッグ](slider-pointer-treasury-1280x720.json)、[ON・844×390のドラッグ](slider-pointer-treasury-844x390.json)、[資源の復元前](resources-before.json)、[復元後](resources-after.json)、[Console](console.json)、[シーン](scene.json)、[操作ログ](actions.json)。
