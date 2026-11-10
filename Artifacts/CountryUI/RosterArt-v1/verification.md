# 兵士選択UIの共用画像

2026-10-28。既存のUnity 6000.4.3f1、Countryシーンで確認。実装と操作確認は完了、見た目はユーザー確認待ち。現行仕様は [兵士選択の共用画像](../../../docs/Country/UI.md#兵士選択の共用画像)、訓練の操作要件は [政務UI](../../../docs/Country/月初の政務UI.md#育成) に集約する。

この記録の上段の長方形メンバーカードは [MemberIcons-v1](../MemberIcons-v1/verification.md) で変更した。現行は育成が丸アイコンだけ、遠征は丸アイコンと下の固有武器名。ここに残した画像・押下計測は導入時の状態であり、一覧行・共用画像・立ち絵枠は継続して使う。

## 使用する部品

組み込みimage_genで [RosterParts-v1.png](../../../Assets/Images/UI/Country/Roster/RosterParts-v1.png) を生成し、1枚を5つのSpriteへ切り出した。[生成プロンプト](image-prompt.md)、[原本と用途](image-assets.json)、[実際の切り出し](slicing.json)。画像の画素編集は行っていない。

| Sprite | 用途 |
| --- | --- |
| Roster-RowFace | 一覧行、上段のメンバー枠、共通詳細の主操作、立ち絵の下地。9-sliceで伸縮 |
| Roster-RowFrame | 選択中の金色の外枠、別のホバー／フォーカス枠、主操作の縁。透明な中央を持つ9-slice |
| Roster-IconRing | 顔アイコンと武器分類アイコンの共通の円形の外縁 |
| Roster-EmptySlot | 訓練・遠征で選ばれていない枠の点線の丸。操作ボタンにしない |
| Roster-PortraitFrame | 右の立ち絵の外枠。中央は透明、9-sliceで角の形を保つ |

画像は無彩色を基にし、Unityで紺灰色・金色へ着色する。名前、状態、数値、人物、武器カテゴリ画像は別のまま。完成した人物カードを1人ずつ画像にしていない。外側の作業背景や画面構成は変更しない。

共通の `CountryRosterRow`、`CountrySoldierToken`、`CountryTrainingEmptySlot`、`CountryRosterView` の4Prefabへ適用し、兵士・武器一覧、訓練、遠征で共用する。通常の兵士UI生成にも同じ画像設定を接続した。

## 確認

- 保存Prefabの面・選択枠・円形の縁・マスク・空き枠・立ち絵枠・押下演出の参照を確認。切り出し時に枠4種の中央の透明度を検査した。
- HD 1280×720と844×390で、兵士・武器一覧、訓練、遠征の4画面を撮影。新規の文字あふれは0。既存ミニマップの `North: N` 判定を分けて [表示監査](display-audit.json) に記録する。
- 一覧で兵士03を選ぶと、右の人物名と金色の選択枠が移る。訓練では空き枠が3→2→3、遠征では26→24、別の選択では26→25へ変わることを確認。メンバー枠も一覧と同じ画像面・円形の縁を使う。
- 兵士の立ち絵枠は両サイズで1：2。武器の個別画像は空欄を維持し、行とスキル分類の武器画像は戦闘と共用する。
- [押下と復帰](press-audit.json)を実測。面がY=0→−3、拡縮が1→0.975、人物の親と文字は別の組として1.5沈む。解放後は元の位置・拡縮へ戻る。停止した国の日付に影響せず動作する。
- 縦一覧のスクロールは両サイズとも1回で18参照座標。[HD](scroll-1280x720.json)・[小さい横画面](scroll-844x390.json)。実機タッチ・物理キーボードのEscは未検証。
- 選択枠・装飾画像は入力を遮らず、円形の切り抜き用マスクと既存のButtonを維持する。押下はuGUIのポインターイベントで確認した。
- コンパイル完了、新しい実行時例外なし。[Console](console-final.json)のmemoryless depthの2件は以前からのもの。設定時にCLIがExceptionと記録する既存コードのCS0618警告は実行時例外と分ける。
- Editorの起動・再起動、シーン保存・再読込なし。[前](get_scene_info.json)と[後](scene-final.json)でシーンの更新日時・サイズ・dirty状態は同一。地図データも変更していない。最後は1月1日、遠征や訓練の新規確定なし、選択0人の遠征編成をHDで表示。

## 実画面と状態

[兵士一覧](Soldiers-PC.png)、[武器一覧](Weapons-PC.png)、[訓練](Training-Selected-PC.png)、[遠征の2人選択](Expedition-Selected-PC.png)、[小さい横画面の遠征](Expedition-Phone.png)、[押下中](Soldiers-Pressed-PC.png)。Unityの実画面であり、生成した完成見本ではない。選択人数がある画像は操作確認中の状態。

[変更ファイル](changed-files.json)、[差分](diffs/)、[操作ログ](actions.json)、[実装・確認状態](status.json)。
