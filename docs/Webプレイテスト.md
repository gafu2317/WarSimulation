# Webプレイテスト

戦闘シーンをWebGLで共有するための手順。Auto Battleの連戦とは別用途。

## ビルド

Unityメニュー:

`Tools/War Simulation/Combat Playtest/Build WebGL`

出力: `.unity/CombatPlaytestWebGL/`

WebGL に切り替えた状態で実行する。Release・Gzip・Decompression Fallbackで出力する。ビルド対象は開いているシーンとBuild Settingsで有効なシーン。

ビルド完了ダイアログの「最大ファイル(Pages判定)」は、`.unityweb` の展開後サイズである。

## 配布先

- 展開後の最大ファイルが100MB未満: `gh-pages`
- 100MB以上: itch.io。Git には載せない

ビルド成果物は本線ブランチへ入れない。

## gh-pages

`.unityweb` を展開し、ファイル名から `.unityweb` を外す。`index.html` の参照も展開後の名前に合わせる。

更新するのは `gh-pages` ブランチだけ。GitHub は1ファイル100MBまでなので、展開後に超えるファイルはプッシュできない。

## itch.io

ページは `gafu2317/webtest`（HTML）。展開はしない。`index.html` が出力フォルダの直下にある状態で上げる。

初回だけ人が行う。ページを HTML で作り、`butler login` でこの Mac に資格情報を保存し、Uploads の `html5` で「This file will be played in the browser」を有効にする。

更新はエージェントが行う。プロジェクトルートで次を実行する。

```bash
butler push --assume-yes .unity/CombatPlaytestWebGL gafu2317/webtest:html5
```

`butler` が無い、または未ログインなら、人が `~/.local/bin` へ入れたうえで `butler login` する。ログインはブラウザの許可が要るので、エージェントは代わりに完了できない。
