# 参考画像に基づく大型王城
原本: ArtSource/Blender/GrandRoyalCastle.blend
生成: Tools/Blender/generate_grand_royal_castle.py
検証: Tools/Blender/validate_grand_royal_castle.py
Unity書き出し: Tools/Blender/export_grand_royal_castle_to_unity.py
Unity取り込み: Assets/Prefabs/Kingdom/City/Editor/GrandRoyalCastleUnityImporter.cs

ユーザー提示画像の構成を基準に再設計。
独立した一重の城壁、四隅の円塔、双円塔の城門、中央にまとまった本館と接続する両翼、
本館の円形脇塔、中央上部の主塔、青灰色の急な屋根、石積み、赤い旗で構成。
敷地は60×54m（元の30×27mの面積4倍）。
本館と両翼の幅は約34m。居住用窓は幅0.55〜0.85m・高さ1.5〜2.05m、入口は幅2m・高さ3m。
窓・扉を一括拡大せず、城本体の幅・部屋数・階数を拡張した。
旧いBlender原本は保持し、既存のUnityシーン構成は壊さずに城Prefabだけを差し替えた。

中庭の空きすぎを調整し、本館を後方へ5m、接続する両翼を前方へ9m・後方へ5m延長。
本館は18×19m、両翼は各8×28m。これらの壁体の平面占有面積は476から790平方mへ増加した
（塔を除く外形面積であり、室内の有効床面積ではない）。窓・扉と建物の高さは維持。
一重の外周城壁は維持し、独立した別棟や塔は追加していない。
正門から玄関までの前庭と中央通路、両側・背面の城壁沿いの通路を残した。
両側に低い植栽帯、後方の脇に小さな木箱を追加。

正面斜め、背面斜め、真上の画像で確認。
円塔と上部主塔の周囲は屋根の面自体を切り欠いて接合している。
保存ファイルで窓ガラスの近傍の構造壁、屋根の頂点・面中心が塔内へ侵入しないこと、
正門から本館前への高さ2mの通路を検査。結果はvalidation.json。
今回の検査では213個の窓の壁支持、406枚の屋根面の塔との切り欠き、門からの通路が合格。

Unityでは `Assets/Models/Kingdom/City/Models/Royal_Castle.fbx` をこの原本から更新し、
同名PrefabをURP Lit・配置用MeshCollider付きで再生成した。`Assets/Scenes/KingdomAssetCatalog.unity`
も再構築し、91種類のPrefabを配置した状態で検証済み。Unity側の結果は `unity_validation.json`、
カタログ側の結果は `docs/Art/KingdomAssetCatalog/validation.json` に保存している。
旧い `Kingdom_Castle` というUnityアセットは残っていない。Blenderの旧原本は再生成用に保持している。
壁・塔の接続に意図した構造体の重なりや内部面はある。
すべての装飾メッシュの交差ゼロや、室内まで完成した構造を保証する検査ではない。
室内・NavMeshは未制作。画像の細かな彫刻や瓦模様は簡略化している。
