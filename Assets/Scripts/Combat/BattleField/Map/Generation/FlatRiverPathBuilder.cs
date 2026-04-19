using System.Collections.Generic;
using UnityEngine;

namespace WarSimulation.Combat.Map
{
    /// <summary>
    /// 高度情報に依存せず、マップ端から別のマップ端を「蛇行する直線」で結ぶ川経路を作る。
    ///
    /// アルゴリズム：
    ///   1. 始点 → 終点の直線をパラメータ t ∈ [0,1] で描く
    ///   2. 各 t で進行方向に垂直なオフセットを Perlin ノイズで生成
    ///   3. 両端を固定したいので sin(πt) エンベロープを掛ける（端で 0、中央で最大）
    ///   4. 得られた連続座標を HeightMap のセル座標に丸めて重複を除く
    ///
    /// 高度探索や窪地埋めを経由しないので「最初に川を引いて、あとで山を避けて立てる」
    /// パイプラインと相性が良い。HeightMap を書き換えない純粋な読み取り専用ソルバ。
    /// </summary>
    public sealed class FlatRiverPathBuilder
    {
        /// <summary>
        /// 始点から終点まで蛇行するセル列を返す。
        /// </summary>
        /// <param name="amplitude">中央付近で許す最大の横ずれ（メートル）。</param>
        /// <param name="frequency">1m あたりに進むノイズの位相（大きいほど細かくうねる）。</param>
        /// <param name="noiseSeed">配置ごとに違うパターンを出すためのオフセット。</param>
        /// <param name="stepMeters">サンプリングの刻み幅（メートル）。小さくすると滑らかになる。</param>
        public List<Vector2Int> Build(
            HeightMap height,
            Vector2Int start,
            Vector2Int end,
            float amplitude,
            float frequency,
            float noiseSeed,
            float stepMeters = 0.5f)
        {
            var path = new List<Vector2Int>();
            if (height == null) return path;
            if (!height.IsInBounds(start.x, start.y) || !height.IsInBounds(end.x, end.y)) return path;
            if (start == end)
            {
                path.Add(start);
                return path;
            }

            float cs = height.CellSize;

            Vector2 startW = new Vector2((start.x + 0.5f) * cs, (start.y + 0.5f) * cs);
            Vector2 endW = new Vector2((end.x + 0.5f) * cs, (end.y + 0.5f) * cs);

            Vector2 axis = endW - startW;
            float axisLen = axis.magnitude;
            if (axisLen < 1e-3f)
            {
                path.Add(start);
                return path;
            }
            Vector2 axisDir = axis / axisLen;
            Vector2 perp = new Vector2(-axisDir.y, axisDir.x); // axis を 90° 左回転

            int steps = Mathf.Max(2, Mathf.CeilToInt(axisLen / Mathf.Max(0.01f, stepMeters)));
            Vector2Int last = new Vector2Int(int.MinValue, int.MinValue);

            for (int i = 0; i <= steps; i++)
            {
                float t = (float)i / steps;
                Vector2 baseP = Vector2.Lerp(startW, endW, t);

                // Perlin ノイズで横ずれを取る。freq * axisLen * t を進行距離（m）に使う。
                float phase = axisLen * t * frequency + noiseSeed;
                float n = Mathf.PerlinNoise(phase, noiseSeed * 0.37f) - 0.5f; // [-0.5, 0.5]
                float envelope = Mathf.Sin(Mathf.PI * t);                     // 両端で 0
                float lateral = n * 2f * amplitude * envelope;                // [-amp, amp]

                Vector2 p = baseP + perp * lateral;

                int cx = Mathf.Clamp(Mathf.FloorToInt(p.x / cs), 0, height.Width - 1);
                int cy = Mathf.Clamp(Mathf.FloorToInt(p.y / cs), 0, height.Height - 1);
                var cell = new Vector2Int(cx, cy);
                if (cell == last) continue;

                // 隣接セルが 1 歩で行けない飛びがある場合は直線補間で埋める
                // （stepMeters を小さくしていれば通常不要だが、保険）
                if (path.Count > 0)
                {
                    FillGap(path, last, cell);
                }

                path.Add(cell);
                last = cell;
            }

            return path;
        }

        /// <summary>
        /// 2 セル間が隣接していない場合、Bresenham ライクな直線補間で中間セルを足す。
        /// 川のセル列は RiverShape.Carve で放物線掘削の中心になるため、途切れると掘削にも隙間が出る。
        /// </summary>
        private static void FillGap(List<Vector2Int> path, Vector2Int from, Vector2Int to)
        {
            int dx = Mathf.Abs(to.x - from.x);
            int dy = Mathf.Abs(to.y - from.y);
            if (dx <= 1 && dy <= 1) return;

            int sx = from.x < to.x ? 1 : -1;
            int sy = from.y < to.y ? 1 : -1;
            int err = dx - dy;
            int x = from.x;
            int y = from.y;

            while (x != to.x || y != to.y)
            {
                int e2 = err * 2;
                if (e2 > -dy) { err -= dy; x += sx; }
                if (e2 < dx)  { err += dx; y += sy; }
                if (x == to.x && y == to.y) break;
                path.Add(new Vector2Int(x, y));
            }
        }
    }
}
