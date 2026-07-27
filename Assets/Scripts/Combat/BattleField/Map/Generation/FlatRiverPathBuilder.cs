using System.Collections.Generic;
using UnityEngine;

namespace WarSimulation.Combat.Map
{
    /// <summary>
    /// 高度情報に依存せず、マップ端から別のマップ端を結ぶ川経路を作る。
    ///
    /// アルゴリズム：
    ///   1. 骨格：始点 → 終点を直線、または二次ベジェ（明示制御点、または bend 量から自動生成した制御点）で t ∈ [0,1] サンプル
    ///   2. 各 t で進行方向に垂直なオフセットを Perlin ノイズで生成（ベジェ時は接線に直交）
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
        /// <paramref name="spineCurveBendMeters"/> が正なら、弦の中点を法線方向にずらした制御点で二次ベジェ骨格にする。
        /// </summary>
        public List<Vector2Int> Build(
            HeightMap height,
            Vector2Int start,
            Vector2Int end,
            float amplitude,
            float frequency,
            float noiseSeed,
            float spineCurveBendMeters = 0f,
            float stepMeters = 0.5f)
        {
            if (!TryPrepareEndpoints(height, start, end, out Vector2 startW, out Vector2 endW, out Vector2 axis, out float axisLen))
                return CreateTrivialPath(height, start, end);

            Vector2? controlWorld = null;
            if (spineCurveBendMeters > 1e-6f)
            {
                Vector2 axisDir = axis / axisLen;
                Vector2 chordPerp = new Vector2(-axisDir.y, axisDir.x);
                // Perlin が 0.5 付近だと制御点が弦上に乗り二次ベジェが直線に退化する。
                // 符号はシード由来で必ず ±、大きさは下限を付けてプレビューでも弧が残るようにする。
                float mag01 = Mathf.PerlinNoise(noiseSeed * 0.31f, noiseSeed * 0.77f);
                float mag = Mathf.Lerp(0.35f, 1f, mag01);
                Vector2 splineMid = (startW + endW) * 0.5f;
                Vector2 center = new Vector2(
                    height.Width * height.CellSize * 0.5f,
                    height.Height * height.CellSize * 0.5f);
                float bendSign = Vector2.Dot(center - splineMid, chordPerp) >= 0 ? 1f : -1f;
                controlWorld = splineMid + chordPerp * (spineCurveBendMeters * bendSign * mag);
            }

            return BuildCore(
                height, start, end, startW, endW, axis, axisLen,
                amplitude, frequency, noiseSeed, controlWorld, stepMeters);
        }

        /// <summary>
        /// 二次ベジェ制御点を明示指定して川経路を作る。手作りマップ向け。
        /// </summary>
        public List<Vector2Int> BuildWithBezierControl(
            HeightMap height,
            Vector2Int start,
            Vector2Int end,
            Vector2 bezierControlWorld,
            float amplitude,
            float frequency,
            float noiseSeed,
            float stepMeters = 0.5f)
        {
            if (!TryPrepareEndpoints(height, start, end, out Vector2 startW, out Vector2 endW, out Vector2 axis, out float axisLen))
                return CreateTrivialPath(height, start, end);

            return BuildCore(
                height, start, end, startW, endW, axis, axisLen,
                amplitude, frequency, noiseSeed, bezierControlWorld, stepMeters);
        }

        private static List<Vector2Int> BuildCore(
            HeightMap height,
            Vector2Int start,
            Vector2Int end,
            Vector2 startW,
            Vector2 endW,
            Vector2 axis,
            float axisLen,
            float amplitude,
            float frequency,
            float noiseSeed,
            Vector2? controlWorld,
            float stepMeters)
        {
            var path = new List<Vector2Int>();
            float cs = height.CellSize;
            Vector2 axisDir = axis / axisLen;
            Vector2 chordPerp = new Vector2(-axisDir.y, axisDir.x);

            bool useBezier = controlWorld.HasValue;
            Vector2 control = controlWorld.GetValueOrDefault();
            float sampleLen = axisLen;
            if (useBezier)
            {
                sampleLen = 0.5f * (
                    Vector2.Distance(startW, control)
                    + Vector2.Distance(control, endW)
                    + axisLen);
            }

            int steps = Mathf.Max(2, Mathf.CeilToInt(sampleLen / Mathf.Max(0.01f, stepMeters)));
            Vector2Int last = new Vector2Int(int.MinValue, int.MinValue);

            for (int i = 0; i <= steps; i++)
            {
                float t = (float)i / steps;
                Vector2 baseP;
                Vector2 lateralPerp;
                if (useBezier)
                {
                    baseP = QuadraticBezier(startW, control, endW, t);
                    Vector2 tan = QuadraticBezierTangent(startW, control, endW, t);
                    if (tan.sqrMagnitude < 1e-8f)
                        tan = axis;
                    tan.Normalize();
                    lateralPerp = new Vector2(-tan.y, tan.x);
                }
                else
                {
                    baseP = Vector2.Lerp(startW, endW, t);
                    lateralPerp = chordPerp;
                }

                float phase = sampleLen * t * frequency + noiseSeed;
                float n = Mathf.PerlinNoise(phase, noiseSeed * 0.37f) - 0.5f;
                float envelope = Mathf.Sin(Mathf.PI * t);
                float lateral = n * 2f * amplitude * envelope;

                Vector2 p = baseP + lateralPerp * lateral;

                int cx = Mathf.Clamp(Mathf.FloorToInt(p.x / cs), 0, height.Width - 1);
                int cy = Mathf.Clamp(Mathf.FloorToInt(p.y / cs), 0, height.Height - 1);
                var cell = new Vector2Int(cx, cy);
                if (cell == last) continue;

                if (path.Count > 0)
                    FillGap(path, last, cell);

                path.Add(cell);
                last = cell;
            }

            return path;
        }

        private static bool TryPrepareEndpoints(
            HeightMap height,
            Vector2Int start,
            Vector2Int end,
            out Vector2 startW,
            out Vector2 endW,
            out Vector2 axis,
            out float axisLen)
        {
            startW = default;
            endW = default;
            axis = default;
            axisLen = 0f;
            if (height == null) return false;
            if (!height.IsInBounds(start.x, start.y) || !height.IsInBounds(end.x, end.y)) return false;
            if (start == end) return false;

            float cs = height.CellSize;
            startW = new Vector2((start.x + 0.5f) * cs, (start.y + 0.5f) * cs);
            endW = new Vector2((end.x + 0.5f) * cs, (end.y + 0.5f) * cs);
            axis = endW - startW;
            axisLen = axis.magnitude;
            return axisLen >= 1e-3f;
        }

        private static List<Vector2Int> CreateTrivialPath(HeightMap height, Vector2Int start, Vector2Int end)
        {
            var path = new List<Vector2Int>();
            if (height == null) return path;
            if (!height.IsInBounds(start.x, start.y)) return path;
            path.Add(start);
            if (end != start && height.IsInBounds(end.x, end.y))
                path.Add(end);
            return path;
        }

        private static Vector2 QuadraticBezier(Vector2 p0, Vector2 p1, Vector2 p2, float t)
        {
            float u = 1f - t;
            return u * u * p0 + 2f * u * t * p1 + t * t * p2;
        }

        private static Vector2 QuadraticBezierTangent(Vector2 p0, Vector2 p1, Vector2 p2, float t)
        {
            return 2f * (1f - t) * (p1 - p0) + 2f * t * (p2 - p1);
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
