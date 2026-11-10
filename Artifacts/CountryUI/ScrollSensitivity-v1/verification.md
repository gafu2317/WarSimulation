# スクロール感度の調整

2026-10-27。設定済み、実際の入力機器での操作感はユーザー確認待ち。仕様は [国家画面のUI](../../../docs/Country/UI.md#画面と基本の部品) に集約する。

- 原因を既存実装から確認。Input System UIの `scrollDeltaPerTick` は6で、uGUIの `ScrollRect.OnScroll` が入力へ感度を乗算していた。縦感度90なら1目盛りで540 UI座標分となり、高さ128の一覧行を約4行分移動する。解像度やGameビューの表示倍率だけで発生する問題ではない。
- 縦90・横100の感度を共通の18へ変更。1目盛り相当は108 UI座標分となる。ドラッグ処理や慣性の設定、レイアウト、解像度は変更していない。
- 共通値は `CountryGovernmentBuilder.ScrollSensitivity`。通常のUI再生成と既存の保存済みPrefabの両方へ反映し、再生成で90や100へ戻らないようにした。
- 保存済みPrefabの5箇所と、実行中の兵士一覧・詳細・配分・建設・育成上段の5箇所で感度18を確認。[保存済み設定](prefab-audit.json)・[実行時設定](runtime-settings.json)。
- 仮想マウスからのホイール入力も試したが、位置の変化を取得できなかった。[入力試行](wheel-probe.json)。この試行を実入力の操作感の確認として扱わない。検証用入力デバイスはPlay終了で片付けた。
- 新しいコンパイルエラー・実行時例外なし。既知のMetalのmemoryless depth surfaceメッセージは継続。開いていたEditorだけを利用し、シーンを保存・再読込せず、最後は武器一覧をプレイ中で表示。

変更前の対象ファイルは `before/`、変更一覧は [changed-files.json](changed-files.json)、操作ログは `actions.json`、要約は [status.json](status.json) に残す。画面の見た目を変える作業ではないため、過去の画像を新しい版の画像として転用していない。
