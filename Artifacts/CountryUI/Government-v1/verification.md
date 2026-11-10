# 月初の政務UI・確認記録

2026-10-25。Unity 6000.4.3f1、既存のWarSimulation Editor、Countryシーンで確認した。

## 実装範囲

収入は実際の月次入金記録を表示する。配分・工事・育成はUIのセッション内の案であり、実支出、完成処理、実キャラクターへの効果、終了後の保存は未接続。施設7種類とメンバー6人の例は `CountryGovernmentExample.asset` で編集できる。

## 自動テスト

`CountryGovernmentDraftTests|CountryIncomeTests|DateManagerTests` をEditModeで実行。31ケース、失敗0。生の結果は `editmode-results.json`。ブリッジのpassed欄はフィクスチャ等も含め39と数えるため、ケース数はtotalTestsの31を採用する。

## 再生中の確認

- 月末まで1日ずつ進行して停止し、進行ボタンで翌月1日へ到達した。新しい収入画面が自動で開き、時間が停止した。
- 税収850、木材170、鉱石85を表示。所持木材1370、鉱石885となった。表示を開き直しても入金は増えない。
- 戦士学校のスライダーを0に設定する操作でも、最低必要額100を下回らなかった。
- 国庫使用をONにし、戦士学校を600へ増額した。税収850を超える配分中にOFFを要求すると、ONを維持した。200へ戻すとOFFにできた。
- 効果詳細の表示中は背面CanvasGroupのinteractableとblocksRaycastsがfalseとなり、閉じると復帰した。
- 3月1日の国庫1700で図書館の建築案を追加すると、計画1件・国庫見込580となり、同じ種類の決定ボタンは無効になった。新築と強化の候補は重複しない。
- 戦士学校でAを選択し、魔法学校でBを選択して戻ると、戦士学校はAのみを保持した。精霊使役学校は訓練先に出ない。
- 閉じるとカメラ入力がtrueに復帰し、HUDの予算ボタンから再表示できた。施設行は5行のままで増殖しなかった。
- スクロールバーのポインターイベントで配分一覧を末尾まで移動した。精霊使役学校と鍛冶場が表示され、上の集計と下の決定操作は固定された。`scroll-budget.json` と `Budget-bottom-1280x720.png` に記録した。

## 表示確認

1280×720と844×390のGameビューで、収入・配分・強化／新築・育成を表示した。スクリーンショットにはScreen Space OverlayのuGUIを含む。最終版のレポートでTMPのoverflowは0。フォントは指定のNotoSansJP-Regular SDFを使用する。

- `Income-1280x720.png`
- `Budget-1280x720.png`
- `Upgrade-1280x720.png`
- `New-1280x720.png`
- `Training-1280x720.png`
- `Income-844x390.png`
- `Budget-844x390.png`
- `Construction-844x390.png`
- `Training-844x390.png`
- `Budget-details-844x390.png`

各画像と同名のJSONに文字、寸法、スクロール領域を記録した。`final-console.json` に既存のMetalのmemoryless depth surfaceの通知2件がある。新UIのC#コンパイルエラーや実行例外は確認されなかった。

小画面の確認はEditor上の解像度変更であり、実機タッチ操作やプレイヤービルドの確認ではない。

## 引き渡し時

Countryシーンは保存済み。Editorは再生中、ゲーム内の日付は2月1日で停止し、配分画面を開いている。保存された初期日付・資源・カメラ設定は変更しない。
