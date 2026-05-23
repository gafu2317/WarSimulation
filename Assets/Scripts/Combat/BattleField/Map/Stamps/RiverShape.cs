using System.Collections.Generic;
using UnityEngine;

namespace WarSimulation.Combat.Map
{
    /// <summary>
    /// 川 1 本分の断面パラメータ（見た目の骨格）。
    /// 点スタンプではなく経路に沿って適用されるため StampShape は継承しない。
    ///
    /// 掘削規則：
    ///   - 経路上の各セルについて、そのセルの「元」の高度を基準として円盤状に掘る
    ///     （連鎖的にどんどん深くなるのを防ぐため、掘削開始前の高度を使う）
    ///   - 深さは中央で _depthMeters、端で 0 に向かう放物線プロファイル
    ///   - 合成は最小値選択：既に低いところは触らない（= 川が氾濫して盛り上がることはない）
    /// </summary>
    [CreateAssetMenu(menuName = "WarSim/Map/River Shape", fileName = "RiverShape")]
    public sealed class RiverShape : ScriptableObject
    {
        [Tooltip("川の幅（ワールドメートル）。断面が最大で広がる距離。")]
        [SerializeField, Min(0.1f)] private float _widthMeters = 3f;

        [Tooltip("川床の最大深さ。中央でこの値だけ周囲地形より低くなる。")]
        [SerializeField, Min(0.01f)] private float _depthMeters = 2f;

        [Tooltip("川幅のうち、GroundStateGrid に Water タグを付ける中心部の割合（0〜1）。岸辺は含めない想定で 0.6 前後。")]
        [SerializeField, Range(0f, 1f)] private float _waterTagRatio = 0.6f;

        public float WidthMeters => _widthMeters;
        public float DepthMeters => _depthMeters;
        public float WaterTagRatio => _waterTagRatio;

        /// <summary>
        /// 指定された経路に沿って <see cref="MapData.Height"/> を掘削し、
        /// <see cref="MapData.GroundStates"/> に Water タグを付与する。
        /// </summary>
        public void Carve(MapData map, IReadOnlyList<Vector2Int> path)
        {
            if (map == null || path == null || path.Count < 2) return;

            HeightMap h = map.Height;
            GroundStateGrid g = map.GroundStates;

            // 掘削の基準となる「元の高度」を先にスナップショット。
            // これをしないと、先行セル掘削後の低い値を基準に後続を掘り直してしまい、
            // 連鎖的にどんどん深くなる（cascading dig bug）。
            var origHeights = new float[path.Count];
            for (int i = 0; i < path.Count; i++)
            {
                Vector2Int p = path[i];
                origHeights[i] = h.GetHeight(p.x, p.y);
            }

            float radius = _widthMeters * 0.5f;
            float cs = h.CellSize;
            int cellRadius = Mathf.Max(1, Mathf.CeilToInt(radius / cs));

            float tagRadius = radius * _waterTagRatio;
            float gCell = g.CellSize;

            // 点ごとの円掘削だと経路が細かく折れた場所で「線分の間」に未掘削が残ることがある。
            // そこで各セグメントを一定ピッチでサンプリングし、連続的に掘削/Waterタグ付けする。
            for (int i = 0; i < path.Count - 1; i++)
            {
                Vector2Int p0 = path[i];
                Vector2Int p1 = path[i + 1];
                float h0 = origHeights[i];
                float h1 = origHeights[i + 1];

                float x0 = (p0.x + 0.5f) * cs;
                float z0 = (p0.y + 0.5f) * cs;
                float x1 = (p1.x + 0.5f) * cs;
                float z1 = (p1.y + 0.5f) * cs;

                float segLen = Vector2.Distance(new Vector2(x0, z0), new Vector2(x1, z1));
                int steps = Mathf.Max(1, Mathf.CeilToInt(segLen / Mathf.Max(0.05f, cs * 0.5f)));
                for (int s = 0; s <= steps; s++)
                {
                    float t = (float)s / steps;
                    float wx = Mathf.Lerp(x0, x1, t);
                    float wz = Mathf.Lerp(z0, z1, t);
                    float wh = Mathf.Lerp(h0, h1, t);

                    CarveHeightMapAtWorld(h, wx, wz, wh, cs, cellRadius, radius);
                    TagWater(g, wx, wz, tagRadius, gCell);
                }
            }
        }

        private void CarveHeightMapAtWorld(
            HeightMap h, float worldX, float worldZ, float baseHeight, float cs, int cellRadius, float radius)
        {
            int cx = Mathf.FloorToInt(worldX / cs);
            int cz = Mathf.FloorToInt(worldZ / cs);
            int x0 = Mathf.Max(0, cx - cellRadius);
            int x1 = Mathf.Min(h.Width - 1, cx + cellRadius);
            int y0 = Mathf.Max(0, cz - cellRadius);
            int y1 = Mathf.Min(h.Height - 1, cz + cellRadius);

            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    float sampleX = (x + 0.5f) * cs;
                    float sampleZ = (y + 0.5f) * cs;
                    float dx = sampleX - worldX;
                    float dy = sampleZ - worldZ;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    if (dist > radius) continue;

                    float t = dist / radius;
                    float carved = baseHeight - _depthMeters * (1f - t * t); // 放物線プロファイル
                    float current = h.GetHeight(x, y);
                    if (carved < current) h.SetHeight(x, y, carved);
                }
            }
        }

        private static void TagWater(GroundStateGrid g, float worldX, float worldZ, float tagRadius, float gCell)
        {
            int cx = Mathf.FloorToInt(worldX / gCell);
            int cz = Mathf.FloorToInt(worldZ / gCell);
            int gRadCells = Mathf.Max(0, Mathf.CeilToInt(tagRadius / gCell));

            for (int gy = cz - gRadCells; gy <= cz + gRadCells; gy++)
            {
                for (int gx = cx - gRadCells; gx <= cx + gRadCells; gx++)
                {
                    if (!g.IsInBounds(gx, gy)) continue;
                    float cxw = (gx + 0.5f) * gCell;
                    float cyw = (gy + 0.5f) * gCell;
                    float ddx = cxw - worldX;
                    float ddy = cyw - worldZ;
                    if (ddx * ddx + ddy * ddy <= tagRadius * tagRadius)
                    {
                        g.SetCell(gx, gy, GroundState.Water);
                    }
                }
            }
        }
    }
}
