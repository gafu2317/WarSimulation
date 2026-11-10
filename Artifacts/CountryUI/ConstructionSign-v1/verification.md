# 建設・強化の木の看板

2026-11-02。建設画面の決定ボタンへ設定した試作。見た目はユーザー確認待ち。現行の仕様は [建設の決定ボタン](../../../docs/Country/UI.md#建設の決定ボタン)、操作の仕様は [月初の政務UI](../../../docs/Country/月初の政務UI.md#建設) にまとめる。

配分・育成で従来の決定ボタンを使っていた状態は [CommandMotifs-v1](../CommandMotifs-v1/verification.md) で更新した。建設・強化の看板は継続し、配分は指令書、育成は教本になっている。この記録の予算・育成画像は看板導入時の状態。

## 素材と適用

組み込みimage_genで [ConstructionSignParts-v1.png](../../../Assets/Images/UI/Country/Buttons/ConstructionSignParts-v1.png) を生成した。[生成プロンプト](image-prompt.md)、[原本・切り出し・SHA256](image-assets.json)。生成後の画素編集は行っていない。

1枚から二枚板と鉄の留め具の2つのSpriteを切り出す。板は9-slice、留め具は左右で共用し、右だけ中心を支点に反転する。板・留め具・槌・文字・影・フォーカスを分けた `CountryConstructionCommand.prefab` を、建設・強化・翌月の決定・計画済みで共用する。槌は既存の建設アイコン、文字は指定のNoto Sans JP SDFを使う。

建設でだけ看板を表示し、予算と育成には従来のボタンを表示する。元の確定処理、条件判定、工事費用・工期・適用月をそのまま使う。通常の政務UI生成にも接続を追加し、再生成時に参照を失わないようにした。

## 確認

- 既存Editor、CountryシーンのPlayで確認。新規のUIコンパイルエラーなし。
- [保存Prefabの参照](setup.json)を検査し、板と2つの共通の留め具、通常ボタンと分けた建設の決定ボタンを確認した。
- HD 1280×720と844×390で「強化する」「建設する」「来月から建設」「計画済み」を確認。建設と強化は同じ看板を使用し、政務画面内の文字あふれは0。
- 押下で板のYが0→−3、拡縮が1→0.975、槌が−24→−25.5、文字が0→−1.5。解放後はいずれも復帰した。国の時間が停止中でも演出が動く。[押下](Pressed-PC.json)・[復帰](Released-PC.json)。
- 看板を押すと従来の強化計画が追加され、「計画済み」となって無効になることを確認。予算・育成へ切り替えると看板は非表示、従来の決定ボタンが表示される。
- 新築・強化、当月・翌月の確認にはPlay内の資源変更を使用した。確認後はPlayを停止し、資源変更と建設案は保存していない。
- 右の留め具の反転支点を中心に調整し、最終版の両画面サイズを再撮影した。`*-Final-*` が最終配置。その他の画像は調整中の右側の留め具位置を含む。
- 開いているシーンの保存・再読込、Editorの起動・再起動なし。シーンのサイズ・更新日時・dirty状態は [作業前](get_scene_info-before.json)と[作業後](get_scene_info-final.json)で同一。[最終Console](read_console-final.json)は既知のmemoryless depthの2件のみ。
- Playerビルド・実機タッチ・物理キーボード操作の確認は行っていない。

## 既存のコンパイル阻害の解消

作業前から `CountryPavingCutoutTests` に `CountryPavingScale` を参照できないエラーが4件あった。処理は既定のEditorアセンブリにあり、テストは名前付きの `WarSimulation.Editor` を参照していたため、UIの新しいEditorメニューもロードされなかった。

Unityの `AssetDatabase.MoveAsset` で `CountryPavingScale.cs` を `Assets/Prefabs/Kingdom/Comparison/Editor/` から、既存の `CountryPavingCutout.cs` と同じ `Assets/Scripts/Editor/Country/` へ移した。処理内容と.metaのGUIDを保持し、[移動前後のハッシュ](compile-blocker.json)を照合した。テスト本体と舗装の処理は変更していない。移動後のコンパイルとPlay開始を確認した。舗装の描画検証とテスト全体の再実行は行っていない。

## 最終の実画面

- [強化・HD](Upgrade-Final-PC.png)／[強化・小さい横画面](Upgrade-Final-Phone.png)
- [翌月の建設・HD](NextMonth-Final-PC.png)／[翌月の建設・小さい横画面](NextMonth-Final-Phone.png)
- [予算の従来ボタン](Budget-Phone.png)／[育成の従来ボタン](Training-Phone.png)

[操作ログ](actions.json)、[変更ファイル](changed-files.json)、[差分](diffs/)、[表示監査](display-audit.json)、[実装と見た目の状態](status.json)。画像はUnityの実画面であり、別の完成見本ではない。
