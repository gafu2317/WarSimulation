# 国家運営HTMLのゲームUI

## 制約

Webサイトに見える構成を残さない。ゲーム画面として実装する。

- 王国の描画を画面全体に常時配置する。
- 画面上端に高さ96pxの全幅HUDを置き、街へ重ねる。左端に王国名、その右に日付・状態・時間操作、右側に資源、右端に設定を置く。
- HUDは一枚の連続した暗色面と細い金属色の境界でまとめ、情報ごとのカードを並べない。日付と資源残高を主表示にする。計画数と工事件数は常時帯に出さず、計画中の「命令を決める」ボタンも置かない。資源は名称と月収を左、残高を大きく右へ分ける。主要ラベルは14px以上を基準にし、資源アイコンは小さく保つ。画面幅が狭いときは二段に組み替える。
- 建物に付いた旗から施設を直接選択する。
- 施設整備、国家運営、国家記録は背景の上にゲームウィンドウとして開く。
- 城下町を全画面に表示する。画面切替は左下のゲームコマンド、日送り・自動進行はHUD左側の日付と同じ時間操作群に置く。
- 固定サイドバー、縦の手順一覧、城下町の進行案内パネルは使わない。施設は建物の旗から選び、下部コマンドから管理画面へ移る。管理窓の内側で命令を決定し、時間操作から日送り・工事・月次報告を進める。
- 主操作に金、選択した施設・命令に青緑、完成結果に緑、不足に赤を使う。通常のボタン・境界・情報表示は彩度を抑えた灰色にする。
- 管理窓は不透明にし、文字の背後へ街の細部を透かさない。施設の名称・効果・費用・選択操作は列を揃える。
- 画面全体はスクロールしない。施設・記録等のウィンドウ内だけをスクロールする。
- 紹介用の見出し、スローガン、英語の装飾文、サイトのサイドバー、カード型の施設紹介、ページフッターを使わない。
- プレイヤーに見せる文言はゲーム内の状態・命令・結果に限る。

## 実ゲームの確認

2026-09-28に以下の一次資料を確認した。参考ゲームの画像やUI素材は流用しない。

### Crusader Kings III

[公式UI/UX開発記事](https://www.paradoxinteractive.com/games/crusader-kings-iii/news/ck3-console-dev-diary-3-uiux-and-controls)

記事内の `Map Overview1920` と `EarlAlfred1920` をブラウザで画像として表示し、実画面を目視した。地図がゲーム画面の主領域、資源と命令は端、時間操作は下端。人物詳細を開いても地図が隣に残る。記事にも地図とメニューの切り替え、ゲームの没入感を損なう全画面メニューを避ける方針が説明されている。

本作では、常時描画する城下町、コンパクトなHUD、ゲームウィンドウ、下端の時間操作に反映する。

### Civilization VI

[公式マニュアル](https://downloads.2kgames.com/civilization/vi/manuals/eu/CIV_VI_25TH_ONLINE_MANUAL_ENG.pdf)

28ページのメイン画面と94ページの都市画面をPDFから画像へ描画して目視した。ゲーム世界が大きく表示され、都市のバナーを選ぶとその都市の管理に入る。都市の情報と命令は地図の上に重なり、都市画面の内側に操作が集まっている。

本作では、建物の旗からの選択、施設に対応した命令、費用・工期・強化先の同時確認に反映する。

### Total War

[公式キャンペーンUI解説](https://academy.totalwar.com/campaign-what-the-ui-is-and-does/)、[ROME II公式キャンペーン操作解説](https://academy.totalwar.com/total-war-rome-2-campaign-controls/)

公式の文章を確認した。資金と予想収入、勢力・施設操作、ターン終了をプレイ中のキャンペーン地図と結び付ける構成。都市の選択から建設や強化へ進む操作を説明している。

本作では、国庫の支出・残高と強化命令を同じ予算確認ウィンドウに置き、予算決定から日付進行までつなげる。

## 検証対象

### 1ループの案内

施設の旗から選択し、国家運営窓で費用と残高を確認して決定する。決定後は停止中になり、時間操作から工事完了まで進める。完成後は同じ操作群の月次報告ボタンから収入と完成結果を受け取り、次月の施設選択へ戻る。進行案内パネルを常時表示せず、ボタンの用途と状態表示で次の操作を伝える。

城下町、施設整備、国家運営、国家記録、および施設詳細、決定確認、月次報告、設定・保存、初期化確認を対象にする。クリック・戻る・日付進行・保存と復元が以前と同じように動くことを確認する。

施設効果は育成枠の仮表示。実際の兵士育成、戦闘、変動する月収、領土拡張は今回の試作に含まれない。

## 背景画像

内蔵image_genで既存の城下町画像を編集し、`Artifacts/CountryUI/assets/kingdom-clean.png` へ保存した。元画像は残す。Unityの現在のCountryシーンには同じ城下町がなく、同じ景色を再撮影できなかったため、画像から編集用の表示を除去した。

最終プロンプト：

Use case: precise-object-edit. Input image 1 is the edit target. Produce a clean game background from this exact 16:9 low-poly medieval town render. Remove ONLY the Unity editor overlays: red green blue transform axis arrows at the center castle, blue selection outlines around castle, and tiny editor toolbar bottom left. Reconstruct underlying castle masonry and roofs seamlessly. Preserve ALL building geometry, building positions, road layout, perspective, framing, muted original lighting, colors and style exactly. No added objects, no redesign, no text, no UI, no borders. Output full wide image with same composition.
