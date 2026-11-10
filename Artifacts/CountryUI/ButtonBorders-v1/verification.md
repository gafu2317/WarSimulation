# ボタンの暗い輪郭を青灰色へ調整

2026-11-05。GameTab-v1とRosterParts-v1のボタン面・枠に描かれた暗い輪郭を、共通のUIマテリアルで明るくした。暗い画素だけを既存のBorder色へ0.65の強さで寄せる。明るい面、金色の選択枠、文字、アイコン、画像の透過形状は維持する。影は青灰色・透明度0.3へ弱めた。現行仕様は [政務画面の共通部品](../../../docs/Country/UI.md#政務画面の共通部品)。

共通材質は `Assets/Prefabs/Country/UI/Materials/CountryButtonBorder.mat`、描画処理は同じUIフォルダの`Shaders/CountryButtonBorder.shader`。UIのStencil・RectMask2Dの切り抜き・Alpha Clipの処理を持ち、元画像を再生成せずSpriteと9-sliceを使い続ける。`CountryButtonBorderBuilder`はPrefabの依存先から設定し、変更のあったPrefabだけを保存する。通常のタブ・兵士UIの生成元も同じ材質と影を使う。

## 確認

- 既存EditorでC#とシェーダーをコンパイル。最終Consoleエラー0件、シェーダーのメッセージ0件・対応状態true。[Editor状態](editor-after.json)。
- 844×390のみで実Prefabを分離プレビューへ描画。[変更前](before/Training-844x390.png)・[変更後](after/Training-844x390.png)。通常の行・学校タブ・金色の選択枠・詳細操作を目視確認した。
- 同じ行の輪郭画素はRGB(20,24,30)から(116,132,148)へ明るくなり、面の画素RGB(83,95,112)は不変。[画素の確認](pixel-samples.json)。兵士08・26の緑文字と並び順、解除後の色と順、選択行全体の可視範囲を確認し、文字のはみ出し0件。[確認値](after/validation.json)。
- [押下状態](states/Pressed-844x390.png)・[無効状態](states/Disabled-844x390.png)・[一覧末尾](states/ScrollBottom-844x390.png)も確認。押下は実際の入力コールバックを呼び、進捗を終端に置いて描画した。時間を進めたアニメーションの検証とは分ける。押下で面が沈み、解除で位置・大きさが戻った。[状態の確認](states/state-checks.json)。
- 分離プレビュー中の対象112面すべてが1つの共通材質を使用。元の画像2枚とメタデータ、保存済みシーン18ファイルは変更なし。[ファイル確認](file-check.json)。

Editorの起動・再起動、開いているシーンの保存・再読込、Playモード開始は行っていない。WebGLの再ビルド・公開ページと実機タッチでの検証は今回行っていない。見た目のユーザー確認は別に残す。
