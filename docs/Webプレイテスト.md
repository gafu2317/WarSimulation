# Web プレイテスト（GafuTest）

`GafuTest` を Editor で Play したときと同じ人向けフローを、WebGL ビルドしてブラウザで共有する。

Auto Battle（連戦・CLI）とは別。Git LFS は使わない。

## 準備

1. Unity に **WebGL Build Support** が入っていること
2. Build Target を **WebGL** にする
3. `Assets/Scenes/GafuTest.unity` を開く

`CombatAutoBattleRunner` は不要（あっても、CLI 設定が無ければ人向け UI のまま）。

## ビルド

メニュー: `Tools/War Simulation/Combat Playtest/Build WebGL`

出力: `.unity/CombatPlaytestWebGL/`

成功するとダイアログと Console に **最大ファイルサイズ** が出る。

## サイズで配布先を決める

| 最大ファイル | 配布 |
|---|---|
| 100MB 未満 | `gh-pages` → GitHub Pages |
| 100MB 以上 | git に載せない。itch.io にアップロード |

本線ブランチにはビルド成果を置かない（`.gitignore` 済み）。

### 100MB 未満: GitHub Pages

Gzip のまま載せると GitHub Pages では読めないことが多い（`Content-Encoding` が付かないため）。  
配布時は **展開した非圧縮ファイル**を `gh-pages` ルートに置く。

1. ビルド出力 `.unity/CombatPlaytestWebGL/` の `.gz` を展開する  
   （`.data` / `.wasm` / `.framework.js`。いずれも 100MB 未満であること）
2. `index.html` の URL から `.gz` を外す
3. その内容を `gh-pages` ブランチのルートにコミットして push
4. GitHub → Settings → Pages → Source を `gh-pages` / root にする（初回のみ）
5. `https://gafu2317.github.io/WarSimulation/` を開いて確認する

次回以降のメニュービルドは Decompression Fallback も有効化する。  
（Gzip のまま Pages に載せる場合用。現状の公開手順は非圧縮の方が確実）

1ファイルでも 100MB 以上なら push できない。その場合は itch.io へ。

### 100MB 以上: itch.io

1. [itch.io](https://itch.io) でプロジェクトを作る
2. Kind of project: **HTML**
3. `.unity/CombatPlaytestWebGL/` を zip するか、中身をアップロードする
4. Embed options でこのブラウザで遊べるようにする
5. 制限付き公開（draft / password）でも可
6. プロジェクト URL をメンバーに渡す

## 確認

ブラウザで開き、Editor の `GafuTest` Play と同様に:

1. 編成 UI
2. 戦闘開始
3. カメラ操作
4. 勝敗 → 編成に戻る

ができれば OK。

## メモ

- メニューは **Release + Gzip** でビルドする（Development だと wasm が 100MB 前後まで膨らみやすい）
- 編成画面でデバッグ表示を切り替え可能: 移動経路 / 進攻ルート / 頭上UI / 視線（既定 OFF）
- 各項目の「設定」から詳細を変更できる。パネル先頭の「デフォルトに戻す」でその項目だけ初期値に戻せる
- 表示名: 移動の線 / 魔石ルート / 頭上テキスト / 視界表示
- 戦闘中は上部中央に速度 ×1/×2/×4/×8、「一時停止」/「再開」、「編成に戻る」
- 関連メニュー（自動戦闘）: [AI/自動戦闘デバッグ](AI/自動戦闘デバッグ.md)
