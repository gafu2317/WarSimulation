# 襲撃の緊急指令

2026-10-29。Unity 6000.4.3f1の既存Editor、CountryシーンのPlay Modeで確認。襲撃先・状況・防衛状況・対応を無記入の伝令用紙に重ねた。ボタンの位置と縁色はユーザーの指摘を受け [v2](../RaidDispatch-v2/verification.md) で調整した。

- [1280 × 720](Raid-1280x720.png)・[844 × 390](Raid-844x390.png)：実際に襲撃を発生させ、2人の防衛編成とCP消費を表示。両サイズで文字あふれはなく、3つのボタンにRaycastが届くことを確認した。
- 編成変更を押して編成画面へ移り、襲撃に戻って防衛する操作から確認画面を開き、確認を押せることを確認した。
- Unityのコンパイルエラーなし。Editorの起動・再起動、開いているシーンの保存・再読込は行っていない。
- 防衛の登録や処理の確認は [Defense-v1](../Defense-v1/verification.md) に残す。盾と警報旗の [Defense-v2](../Defense-v2/verification.md) は却下・撤回済み。

## 素材

[伝令用紙](../../../Assets/Images/UI/Country/RaidDispatchPaper-v1.png) は組み込みのimagegenで生成した。画像は用紙のみで、文字や操作はUnity上で重ねる。

生成指示：

> Use case: stylized-concept. Asset type: a single reusable background sprite for a medieval strategy game's urgent military dispatch, NOT a full UI mockup. Create one broad horizontal rectangular sheet of aged warm ivory paper seen perfectly straight on, occupying nearly the entire transparent canvas, with realistic subtle paper fiber, gently darkened natural paper edges, restrained tactile hand-painted game-art finish. Leave the entire central sheet uniformly empty and unobstructed so game text can be placed there at runtime. The edges are real paper edges, not a decorative picture frame. Calm, serious, credible, no motifs. No handwriting, text, numbers, seals, icons, ribbon, weapons, ornaments, borders, folds, objects, hands, UI elements, shadows outside the paper, or scenery. Fully transparent background outside the paper. Landscape 3:2 proportions.
