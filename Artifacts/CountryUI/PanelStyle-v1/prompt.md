# 施設情報パネルの美術試案

2026-10-25。内蔵image_genによる編集。Unity未反映の見た目の案。

- 参照兼編集対象：`Artifacts/CountryUI/Government-v5/Construction-1280x720.png`
- 出力：`facility-panel-concept.png`
- 透過背景：false

## 生成に使ったプロンプト

```text
Use case: ui-mockup / precise visual restyling of one existing game UI component.
Edit the provided screenshot of our medieval kingdom strategy game. Produce one polished art-direction preview at the SAME wide 16:9 framing, keeping the whole existing screenshot visible. This is a controlled experiment on the RIGHT-HAND FACILITY DETAIL PANEL only, NOT a redesign of navigation or layout.

EDIT TARGET: the large rectangular panel roughly x349–1253, y229–619 in the 1280x720 reference, including its header and inset effect area. Show a materially stronger, professionally art-directed MODERN GAME INTERFACE treatment of this panel, instead of the existing flat rectangles. The panel must visibly gain crafted silhouette, subtle illustrated surface texture, and selective detail.

DESIGN:
- Restrained grey with a slight navy cast, consistent with the provided screenshot. Muted brushed pewter edges, blue-grey tightly woven cloth or fine matte bookcloth interior, small pale warm-metal highlights. Clearly readable at normal game size. Refined contemporary strategy-game aesthetic that fits stylized medieval town art and the existing painted resource icons.
- Distinctive stepped silhouette: a shallow integrated nameplate projects slightly above and to the left of the main panel, surrounding the existing sword symbol and 戦士学校 heading. Subtle folded/turned-under edge at the end of the nameplate; one small chamfer or inward step at the opposite lower corner. These are deliberately designed contour changes, not just rounded corners.
- A slim carefully lit metal rim with small purposefully placed corner joints, coherent edge light and a shallow contact shadow. The rim is light, NOT a black outline or a chunky multi-layer frame. The panel feels crafted but stays largely flat in screen space.
- Fine, low-contrast material grain and a broad soft change of light across the face make the interior clearly more tactile than a solid fill. Avoid gritty noise, scratches, rust, dirt, distressed edges and photographic heavy realism.
- In the LOWER-RIGHT unused margin of this facility panel, below the effect content, include a very faint architectural site-plan engraving at about 8% contrast: a few simple walls, a school courtyard and compass marks. It must read as an understated background motif, not a new illustration or an extra icon. Keep text areas calm.
- Keep the four existing cost columns aligned and identical in styling to each other. Use fine inset divider details and quiet label areas rather than four new cards.
- Make the effect region feel like an inset attached strip with a tiny folded corner near 詳細 ›, instead of a second generic floating rectangular card. Keep its text and link where they are.

STRICT INVARIANTS:
Preserve every existing Japanese label, number, icon meaning, font size, position and alignment, including 戦士学校, Lv.1 → Lv.2, 費用, 木材, 鉱石, 工期, 550, 100, 80, 1か月, 完成後の効果〈表示例〉, 訓練効果 100% → 120%, 詳細 ›.
The existing sword, money bag, wood, ore and clock symbols remain recognizable and consistent with the source.
Everything OUTSIDE this facility panel must remain unchanged: top tabs and their selected state, date, treasury 150, stop status, page title and instruction, 強化／新築 switch, facility list and scrollbar, bottom back button, insufficiency message and disabled confirmation button.
No added controls, no new copy, no decorative English headings, no watermark, no logos, no invented national emblem, no extra data, no new functionality. No massive gold filigree, no fantasy vines, no gemstones, no ornate baroque frame, no sci-fi hologram, no glossy website card, no rounded rectangle used as the main design move.
The edited panel should be clearly and tastefully different from the original flat panel while staying practical for a real reusable game UI component.
```

これは生成時の指示の記録であり、全ての不変条件が満たされたことを意味しない。画面は画像として再描画されているため、文字や配置の厳密さと実装の操作性は別途確認する。
