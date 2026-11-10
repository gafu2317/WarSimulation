# 武器カテゴリと個別画像の分離

2026-10-27。実装済み、見た目はユーザー確認待ち。仕様は [国家画面のUI](../../../docs/Country/UI.md#一覧と詳細の共通構造) を正本とする。

- 武器のサブタイトルは武器種だけとし、「一品もの」を除去した。装備品の識別子や管理ルールは変更していない。
- 一覧の小アイコンは `CountryRosterView._weaponCategoryIcons` の6種を武器種から選ぶ。戦闘UIと同じSpriteを参照し、習得済みスキルの分類アイコンとも共用する。
- `CountryWeaponDefinition.Image` はその品の個別画像専用に分離。現在の26品に入っていたカテゴリ画像を外し、全て空欄にした。既存データは画像欄以外を保持。作成処理もカテゴリ画像を個別画像へ設定しない。後から別の個別画像を登録しても、再設定で消去しない。
- 右の個別画像がないときは、画像・代替シルエット・注記を表示しない。兵士のシルエットと1：2の立ち絵枠は維持した。
- 武器一覧を1280×720と844×390で確認し、兵士一覧と育成も1280×720で確認。4枚の記録で文字あふれ・開発用注釈・「一品もの」のラベルは0。実行時のカテゴリ6種を照合し、武器画像と代替シルエットが無効、兵士シルエットが有効であることを確認。[表示](display-audit.json)・[実行時参照](runtime-audit.json)・[保存済みデータ](asset-audit.json)。
- Gameビューは確認用の1280×720固定表示。実測の `EditorGUIUtility.pixelsPerPoint` は2で、ユーザー画像のScale 1xでは約640×360ポイント相当になる。ScaleはEditorのプレビュー倍率。Gameビューを大きく確認する場合は2x程度へ上げ、Canvasやカメラの設定で補正しない。[UnityのGameビュー](https://docs.unity3d.com/6000.0/Documentation/Manual/GameView.html)・[pixelsPerPoint](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/EditorGUIUtility-pixelsPerPoint.html)。
- 新しいコンパイルエラー・実行時例外なし。既知のMetalのmemoryless depth surface load/storeメッセージは継続。開いていたEditorだけを利用し、シーンを保存・再読込せず、最後は1280×720の武器一覧をプレイ中で表示した。

[武器一覧](Weapons-1280x720.png)・[小さい横画面](Weapons-844x390.png)・[兵士一覧](Soldiers-1280x720.png)・[育成](Training-1280x720.png)

変更前の対象ファイルは `before/`、変更対象は [changed-files.json](changed-files.json)、操作ログは `actions.json`、結果の要約は [status.json](status.json) に残す。
