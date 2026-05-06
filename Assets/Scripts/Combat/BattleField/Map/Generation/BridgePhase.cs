using UnityEngine;

namespace WarSimulation.Combat.Map
{
    /// <summary>
    /// 橋フェーズ：RiverPhase が生成した各川の上に、等間隔で橋を N 個配置する。
    /// Bridge は PlacedFeature（FeatureType.Bridge）として MapData.Features に追加され、
    /// BridgeRenderer が Cube メッシュで可視化する。
    ///
    /// 配置規則：
    /// - セル列を N+1 等分した内部区切り（両端は除外）に橋を置く
    /// - 橋の向きは該当点の前後セルから算出した接線に垂直
    /// - 高さ（Y）は川の水面 + BridgeHeightAboveWater
    /// </summary>
    public sealed class BridgePhase : IMapGenerationPhase
    {
        public void Execute(MapData map, IRandom rng, MapGenerationConfig config)
        {
            if (map == null || config == null) return;
            if (config.BridgesPerRiver <= 0) return;
            if (map.Rivers.Count == 0) return;

            int bridgesPerRiver = config.BridgesPerRiver;
            float depth = config.RiverShape != null ? config.RiverShape.DepthMeters : 0.6f;
            float waterOffsetRatio = 0.85f; // RiverRenderer の既定値と合わせる
            float waterY = -depth * (1f - waterOffsetRatio);
            float bridgeY = waterY + config.BridgeHeightAboveWater;

            HeightMap h = map.Height;
            float cs = h.CellSize;

            for (int r = 0; r < map.Rivers.Count; r++)
            {
                RiverPath river = map.Rivers[r];
                var cells = river.Cells;
                if (cells.Count < 3) continue;

                float bridgeLength = river.WidthMeters + config.BridgeLengthExtraMargin;
                Vector3 bridgeScale = new Vector3(config.BridgeWidth, config.BridgeThickness, bridgeLength);

                for (int b = 0; b < bridgesPerRiver; b++)
                {
                    // 両端を避けて等分点に打つ：idx = (b+1) * N / (count+1) 相当
                    int idx = (int)((b + 1L) * cells.Count / (bridgesPerRiver + 1L));
                    idx = Mathf.Clamp(idx, 1, cells.Count - 2);

                    Vector2Int prev = cells[idx - 1];
                    Vector2Int next = cells[idx + 1];
                    Vector2Int here = cells[idx];

                    // 川の流れ方向（接線）をワールド座標で算出。単位ベクトル化。
                    Vector2 tangent = new Vector2(next.x - prev.x, next.y - prev.y);
                    if (tangent.sqrMagnitude < 1e-6f) tangent = new Vector2(1f, 0f);
                    tangent.Normalize();

                    // 橋の規約：ローカル +Z（長辺）を「川を跨ぐ方向」に向ける。
                    // BridgeRenderer 側は scale = (width, thickness, length) で Cube を作る。
                    // ゆえに欲しいのは「ローカル +Z が tangent の垂直方向を向く」Y 回転。
                    // Unity（LH）で Y=θ 回転後、local +Z のワールド方向は (sin θ, 0, cos θ)。
                    // 垂直方向 = (-tangent.y, tangent.x) に合わせるので
                    //   sin θ = -tangent.y, cos θ = tangent.x  →  θ = atan2(-ty, tx)
                    float yRot = Mathf.Atan2(-tangent.y, tangent.x) * Mathf.Rad2Deg;

                    Vector3 worldPos = new Vector3(
                        (here.x + 0.5f) * cs,
                        bridgeY,
                        (here.y + 0.5f) * cs);

                    map.AddFeature(new PlacedFeature(
                        FeatureType.Bridge,
                        worldPos,
                        Quaternion.Euler(0f, yRot, 0f),
                        bridgeScale));
                }
            }
        }
    }
}
