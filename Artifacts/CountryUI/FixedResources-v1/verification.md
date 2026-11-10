# 上帯の資源欄の固定幅

2026-11-05。人口・国庫・木材・鉱石の4欄を、既存の基準幅236で固定した。各欄の伸縮を無効にし、余った幅は欄の後ろの描画しない余白へ均等に割り当てる。アイコンと数値の開始位置を保ちながら、数値の幅で他の欄を動かさない。

現行仕様は [上の帯](../../../docs/Country/UI.md#上の帯と時間操作)。`CountryTextLayoutBuilder` と `CountryResourceBuilder` の通常生成元にも接続した。`Set Up Fixed Resource Columns` は上帯のPrefabだけを設定し、開いているシーンを保存・再読込しない。

## 確認

- 既存Editorでコンパイル成功、Consoleのエラー0件。
- [validation.json](validation.json)：実HUD Prefabと資源変更通知を分離プレビューへ置き、0・9・10・9999・99999・100000の6パターンを確認。4欄とも幅236、伸縮0で、数値欄の幅と位置、他の資源欄と設定の位置が一致した。数値のはみ出し0件、表示上限の `99999+` も確認した。
- スマホ横画面844×390だけで通常・0・上限表示を描画して目視した。街の3Dモデルを含めないUI確認用の画像。
- [editor-state.json](editor-state.json)：設定の再実行によるファイル差分なし、シーンの未保存変更なし、既存EditorにDevice Simulatorモジュールがあることを記録した。
- [file-check.json](file-check.json)：シーン18ファイルのハッシュ不変。Editorの起動・再起動、シーンの保存・再読込、Playモードの開始・停止は行っていない。

## 画像

- [通常](Default-844x390.png)
- [0](Zero-844x390.png)
- [上限表示](Capped-844x390.png)

## スマホ表示の調査

このEditorはUnity 6000.4.3f1で、組込の `UnityEditor.DeviceSimulatorModule` とDevice Simulatorの型が存在する。公式マニュアルではGameビュー左上の切替、または `Window > General > Device Simulator` から利用できる。機種の画面形状・解像度・向き・セーフエリア、再生中の1本指の入力を確認できる。実機性能・描画能力や複数指の再現は行わない。[Unity 6.4のSimulator view](https://docs.unity3d.com/6000.4/Documentation/Manual/device-simulator-view.html)、[機能と制限](https://docs.unity3d.com/6000.4/Documentation/Manual/device-simulator-introduction.html)

現在の844×390は比率確認用に描画する画素数。機種のネイティブ解像度を指定した検査ではない。iPhone 13の物理画素は2532×1170で、844×390は各辺を1/3にした値であり、比率はいずれも約19.5:9。数値が近い比率でも、同じ解像度や実機の画質を確認したとは扱わない。[AppleのiPhone 13仕様](https://support.apple.com/en-ie/111872)

今回の変更でSimulatorへの切替・機種指定・デバイス定義やパッケージの追加、ビルド対象の変更は行っていない。ノッチと安全領域の検査は未実施で、機能の調査と実施済みの検証を区別する。実機、WebGLビルド、itch.io更新も今回の対象外。

コード・Prefabへの設定と資源6パターンの確認は完了。見た目はユーザー確認待ち。
