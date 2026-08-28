# Web プレイテスト（GafuTest）

`GafuTest` を Editor で Play したときと同じ人向けフローを、WebGL ビルドしてブラウザで共有する。

Auto Battle（連戦・CLI）とは別。Git LFS は使わない。

## ビルド

メニュー: `Tools/War Simulation/Combat Playtest/Build WebGL`

出力: `.unity/CombatPlaytestWebGL/`

ビルドには開いている戦闘 Scene と、Build Settings で有効な Scene（ランタイム用マップ Scene を含む）が収録される。

成功するとダイアログと Console に **最大ファイルサイズ（Pages 向け展開後）** が出る。

メニューは **Release + Gzip + Decompression Fallback** でビルドする。  
出力の本体は `.data.unityweb` / `.wasm.unityweb` / `.framework.js.unityweb`（中身は Gzip）。

## サイズで配布先を決める

| 最大ファイル（展開後） | 配布 |
|---|---|
| 100MB 未満 | `gh-pages` → GitHub Pages |
| 100MB 以上 | git に載せない。しらせる |


本線ブランチにはビルド成果を置かない（`.gitignore` 済み）。

### 100MB 未満: GitHub Pages

Gzip / `.unityweb` のまま載せると GitHub Pages では読めないことが多い（`Content-Encoding` が付かないため）。  
配布時は **展開した非圧縮ファイル**を `gh-pages` ルートに置く。

1. ビルド出力 `.unity/CombatPlaytestWebGL/` の `.unityweb`（Gzip）を展開する  
   （`.data` / `.wasm` / `.framework.js`。いずれも 100MB 未満であること）
2. `index.html` の URL から `.unityweb` を外す  
   例: `CombatPlaytestWebGL.data.unityweb` → `CombatPlaytestWebGL.data`
3. その内容を `gh-pages` ブランチのルートにコミットして push
4. GitHub → Settings → Pages → Source を `gh-pages` / root にする（初回のみ）
5. `https://gafu2317.github.io/WarSimulation/` を開いて確認する

1ファイルでも 100MB 以上なら push できない。その場合は itch.io へ。

### 100MB 以上: 知らせる


