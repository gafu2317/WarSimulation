# Webプレイテスト

戦闘シーンをWebGLで共有するための手順。Auto Battleの連戦とは別用途。

## ビルド

Unityメニュー:

`Tools/War Simulation/Combat Playtest/Build WebGL`

出力: `.unity/CombatPlaytestWebGL/`

Release・Gzip・Decompression Fallbackで出力する。ビルド対象は開いているシーンとBuild Settingsで有効なシーン。

## 配布

- 展開後の最大ファイルが100MB未満: `gh-pages` のGitHub Pagesへ配置
- 100MB以上: Gitへ載せず、itch.io等へ配布

GitHub Pagesへ置く場合は、`.unityweb`を展開し、`index.html`の参照も展開後の拡張子へ合わせる。
ビルド成果物は本線ブランチへ入れない。
