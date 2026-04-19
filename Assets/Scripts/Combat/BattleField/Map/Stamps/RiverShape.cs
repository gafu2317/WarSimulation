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

            for (int i = 0; i < path.Count; i++)
            {
                Vector2Int p = path[i];
                float pHeight = origHeights[i];
                float worldX = (p.x + 0.5f) * cs;
                float worldZ = (p.y + 0.5f) * cs;

                CarveHeightMap(h, p, pHeight, cs, cellRadius, radius);
                TagWater(g, worldX, worldZ, tagRadius, gCell);
            }
        }

        private void CarveHeightMap(HeightMap h, Vector2Int p, float pHeight, float cs, int cellRadius, float radius)
        {
            int x0 = Mathf.Max(0, p.x - cellRadius);
            int x1 = Mathf.Min(h.Width - 1, p.x + cellRadius);
            int y0 = Mathf.Max(0, p.y - cellRadius);
            int y1 = Mathf.Min(h.Height - 1, p.y + cellRadius);

            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    float dx = (x - p.x) * cs;
                    float dy = (y - p.y) * cs;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    if (dist > radius) continue;

                    float t = dist / radius;
                    float carved = pHeight - _depthMeters * (1f - t * t); // 放物線プロファイル
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
