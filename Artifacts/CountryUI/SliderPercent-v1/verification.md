# 配分スライダーの％目盛り確認

2026-10-28。既存のUnity 6000.4.3f1、Countryシーンで実施。実装済み、見た目はユーザー確認待ち。現行仕様は [国家UI](../../../docs/Country/UI.md) と [月初の政務UI](../../../docs/Country/月初の政務UI.md)。GameControls-v1の金額目盛りを置き換えた記録で、学校画像・タブ等は今回変更していない。

- 大目盛りとラベルは0・25・50・75・100％、小目盛りは5％刻み。0％は最低必要額、100％は現在の配分上限。
- HD 1280×720と844×390の配分画面で、5施設のラベルとアンカー位置を確認。目盛りの文字あふれなし。既存ミニマップの「N」の判定は変更対象外。
- 魔法学校の配分150→200により戦士学校の上限350→300となっても、％ラベルと位置は維持。変更後は元の配分へ戻した。
- 両サイズでGraphicRaycasterからSliderへPointerDown・Drag・PointerUpを送り、下限100→200→上限350→200を確認。コイン中心と塗りの先端の差は0.1px未満。金額、最低必要額、配分上限の計算は変更していない。
- コンパイル完了。新しいC#エラー・実行時例外なし。既存の非推奨API警告とmemoryless depthのメッセージは除外して確認。
- Editorの起動・再起動、シーンの保存・再読込は行っていない。Countryシーンのファイルサイズ・更新日時・dirty状態は作業前と同じ。既存EditorをHDの配分画面で残した。

実画面: [HD](Budget-PC.png)、[844×390](Budget-Phone.png)、[上限変更後](Budget-LimitChanged.png)。

検証データ: [表示と上限変更](display-audit.json)、[HDドラッグ](slider-pointer-1280x720.json)、[844×390ドラッグ](slider-pointer-844x390.json)、[Console](console.json)、[シーン](scene.json)、[操作ログ](actions.json)。
