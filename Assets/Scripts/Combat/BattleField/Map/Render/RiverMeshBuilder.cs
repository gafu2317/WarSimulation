using UnityEngine;

namespace WarSimulation.Combat.Map
{
    /// <summary>
    /// 1 本の川経路から水面リボンメッシュを生成する純粋ロジック。
    /// Unity の MonoBehaviour には依存しないため、単体テストやエディタ上でも呼べる。
    ///
    /// 水面 Y の決め方：
    ///   掘削済み HeightMap をその位置でサンプリングし、そこに
    ///   DepthMeters * waterYOffsetRatio を加える。
    ///   これで水面は「掘削された川床」の少し上、かつ周囲地形（岸）より下に収まる。
    /// </summary>
    public static class RiverMeshBuilder
    {
        /// <summary>
        /// 川経路からリボンメッシュを 1 枚生成する。無効な入力なら null。
        /// </summary>
        /// <param name="river">対象の川経路</param>
        /// <param name="height">掘削済み HeightMap（Y を決めるためサンプリングする）</param>
        /// <param name="waterYOffsetRatio">川床からの水面高さを DepthMeters に対する比率で指定（0.6 で 60% の高さ）</param>
        /// <param name="smoothingIterations">中央線の移動平均スムージング回数（0 でスムージングなし）</param>
        public static Mesh Build(
            RiverPath river,
            HeightMap height,
            float waterYOffsetRatio = 0.6f,
            int smoothingIterations = 2,
            float surfaceYOffsetMeters = 0f)
        {
            if (river.Cells == null || river.Cells.Count < 2 || height == null) return null;

            Vector3[] points = ConvertToWorldPoints(river, height, waterYOffsetRatio, surfaceYOffsetMeters);
            for (int i = 0; i < smoothingIterations; i++)
            {
                points = SmoothMovingAverage(points);
            }

            return BuildRibbon(points, river.WidthMeters);
        }

        /// <summary>
        /// セル経路をワールド座標に変換し、Y は HeightMap の値 + オフセットとする。
        /// 両端がマップ端セル上にある場合は、実際のマップ境界まで外挿して水面メッシュが
        /// 地図端まで届くようにする（セル中心のままだと 0.5 セル分＝数十cm 内側で途切れて見える）。
        /// </summary>
        private static Vector3[] ConvertToWorldPoints(
            RiverPath river, HeightMap height, float waterYOffsetRatio, float surfaceYOffsetMeters)
        {
            var cells = river.Cells;
            var result = new Vector3[cells.Count];
            float cs = height.CellSize;
            float waterOffset = river.DepthMeters * Mathf.Clamp01(waterYOffsetRatio);
            float worldW = height.Width * cs;
            float worldH = height.Height * cs;

            for (int i = 0; i < cells.Count; i++)
            {
                Vector2Int c = cells[i];
                float worldX = (c.x + 0.5f) * cs;
                float worldZ = (c.y + 0.5f) * cs;

                // 先頭・末尾のみマップ辺まで外挿する
                bool isEndpoint = i == 0 || i == cells.Count - 1;
                if (isEndpoint)
                {
                    if (c.x == 0) worldX = 0f;
                    else if (c.x == height.Width - 1) worldX = worldW;
                    if (c.y == 0) worldZ = 0f;
                    else if (c.y == height.Height - 1) worldZ = worldH;
                }

                float worldY = height.GetHeight(c.x, c.y) + waterOffset + surfaceYOffsetMeters;
                result[i] = new Vector3(worldX, worldY, worldZ);
            }
            return result;
        }

        /// <summary>3 点移動平均による軽量スムージング。両端は動かさない。</summary>
        private static Vector3[] SmoothMovingAverage(Vector3[] points)
        {
            if (points.Length < 3) return points;

            var result = new Vector3[points.Length];
            result[0] = points[0];
            result[points.Length - 1] = points[points.Length - 1];
            for (int i = 1; i < points.Length - 1; i++)
            {
                // XZ だけ平滑化し、Y は元の掘削済み高さ基準を維持する。
                // Y まで平均すると局所的に水面が沈み、地形に埋まって「切れ目」が見えることがある。
                Vector3 avg = (points[i - 1] + points[i] + points[i + 1]) / 3f;
                avg.y = points[i].y;
                result[i] = avg;
            }
            return result;
        }

        /// <summary>
        /// 中央線を 1 段階サブディビジョン（中点挿入）して点数を倍化する。
        /// スムージング後のリボンを更に柔らかくしたい場合に使う。現状は未使用。
        /// </summary>
        private static Mesh BuildRibbon(Vector3[] points, float width)
        {
            int n = points.Length;
            var vertices = new Vector3[n * 2];
            var uvs = new Vector2[n * 2];
            var triangles = new int[(n - 1) * 6];

            // UV V 方向に川の全長を巻くため、累積距離を先に計算する。
            float totalLength = 0f;
            var cumLen = new float[n];
            for (int i = 1; i < n; i++)
            {
                totalLength += (points[i] - points[i - 1]).magnitude;
                cumLen[i] = totalLength;
            }

            float halfWidth = width * 0.5f;
            Vector3 prevSide = Vector3.zero;
            for (int i = 0; i < n; i++)
            {
                Vector3 tangent;
                if (i == 0) tangent = (points[1] - points[0]);
                else if (i == n - 1) tangent = (points[n - 1] - points[n - 2]);
                else tangent = (points[i + 1] - points[i - 1]);

                // 水面は基本フラット（XZ 平面内で幅を取る）方向に広がる。
                Vector3 flatTangent = new Vector3(tangent.x, 0f, tangent.z);
                float flatMag = flatTangent.magnitude;
                if (flatMag < 1e-5f)
                {
                    flatTangent = Vector3.forward;
                }
                else
                {
                    flatTangent /= flatMag;
                }
                Vector3 side = new Vector3(-flatTangent.z, 0f, flatTangent.x);
                if (i > 0 && Vector3.Dot(side, prevSide) < 0f)
                {
                    // ねじれ防止：急カーブで side が反転したら前フレーム向きに合わせる
                    side = -side;
                }
                prevSide = side;

                vertices[i * 2] = points[i] + side * halfWidth;
                vertices[i * 2 + 1] = points[i] - side * halfWidth;

                float v = totalLength > 1e-3f ? cumLen[i] / totalLength : 0f;
                uvs[i * 2] = new Vector2(0f, v);
                uvs[i * 2 + 1] = new Vector2(1f, v);
            }

            for (int i = 0; i < n - 1; i++)
            {
                int baseV = i * 2;
                int t = i * 6;
                triangles[t] = baseV;
                triangles[t + 1] = baseV + 2;
                triangles[t + 2] = baseV + 1;
                triangles[t + 3] = baseV + 1;
                triangles[t + 4] = baseV + 2;
                triangles[t + 5] = baseV + 3;
            }

            var mesh = new Mesh { name = "RiverMesh" };
            if (vertices.Length > 65535)
            {
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            }
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
