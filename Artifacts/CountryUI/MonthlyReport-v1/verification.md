# 月初報告の紙面

2026-10-28。Unity 6000.4.3f1の既存Editor、CountryシーンのPlay Modeで確認。以下はUnityのGameビューの実画面。色合いはユーザーに評価されたが、報告書らしさが不足すると指摘され、収入カードを [v2](../MonthlyReport-v2/verification.md) の表形式へ変更した。

- [1280 × 720](Report-1280x720.png) と [844 × 390](Report-844x390.png)：月初に実際に入金された税収・木材・鉱石と、帰還した遠征1件を表示。各サイズで報告領域内のTextMeshProの文字あふれ0。
- [844 × 390・帰還3件のスクロール](Report-scroll-844x390.png)：帰還3件を作り、最後の行までスクロール。行全体へのRaycastとクリックで詳細が開くこと、閉じて配分タブから報告へ戻れることを確認。
- 月末の時間進行で翌月1日に報告が開き、収入の記録と帰還した隊の行が表示された。再表示で国庫残高が再加算されず、帰還記録も維持された。
- 木材と鉱石の所持数をそれぞれ2,147,483,647にした場合にも、報告領域内の文字あふれ0。
- `CountryExpeditionTests`、`CountryIncomeTests`、`DateManagerTests` のEditModeテスト55件が成功。
- CountryHudのPrefab内に欠落スクリプトはなく、報告用の紙画像と帰還行Prefabの参照を確認。既存のCountryシーンは保存・再読込していない。
- 紙面は `Assets/Images/UI/Country/ReportPaper-v1.png` の無記入素材を使用。テキスト・数値・仕切りはUnityのUI部品で表示する。右上の折り返しは紙面の形を示す部品で、確認画像にはゲーム画面以外の注釈を入れていない。
