using System.Collections.Generic;
using UnityEngine;

namespace WarSimulation.Combat.Map
{
    /// <summary>
    /// 1 本の川から水面メッシュを生成する純粋ロジック。
    /// <see cref="LakeMeshBuilder"/> の開放水面と同様、グリッドセルを走査し
    /// 角頂点の四角形を積み上げた水平面メッシュにする（リボンではない）。
    ///
    /// 含めるセル：セル中心から折れ線（経路）までの距離が
    /// (幅/2)×<see cref="RiverPath.WaterTagRatio"/>×<see cref="MeshSurfaceWidthScale"/> 以下のもの。
    /// 倍率は湖レンダラの見た目に合わせた固定値（インスペクタでは変えない）。
    /// 水面 Y は経路セルの平均川床高さ + DepthMeters×比率の一定値（湖の一定 surfaceY に相当）。
    /// </summary>
    public static class RiverMeshBuilder
    {
        /// <summary>水面メッシュの幅を RiverShape 基準より広げる固定倍率。</summary>
        private const float MeshSurfaceWidthScale = 2f;

        private const float UvWorldScale = 0.08f;

        /// <summary>
        /// 川の水面メッシュを 1 枚生成する。無効な入力なら null。
        /// </summary>
        /// <param name="smoothingIterations">互換のため残すが無視する（旧リボン用）。</param>
        public static Mesh Build(
            RiverPath river,
            HeightMap height,
            float waterYOffsetRatio = 0.6f,
            int smoothingIterations = 2,
            float surfaceYOffsetMeters = 0f)
        {
            if (river.Cells == null || river.Cells.Count < 2 || height == null) return null;

            float cs = height.CellSize;
            float halfWidth = river.WidthMeters * 0.5f;
            float tagR = halfWidth * Mathf.Max(0.001f, river.WaterTagRatio) * MeshSurfaceWidthScale;
            float expand = (halfWidth + cs) * MeshSurfaceWidthScale;

            float minWx = float.PositiveInfinity;
            float maxWx = float.NegativeInfinity;
            float minWz = float.PositiveInfinity;
            float maxWz = float.NegativeInfinity;

            IReadOnlyList<Vector2Int> cells = river.Cells;
            for (int i = 0; i < cells.Count; i++)
            {
                Vector2Int c = cells[i];
                float wx = (c.x + 0.5f) * cs;
                float wz = (c.y + 0.5f) * cs;
                if (wx < minWx) minWx = wx;
                if (wx > maxWx) maxWx = wx;
                if (wz < minWz) minWz = wz;
                if (wz > maxWz) maxWz = wz;
            }

            minWx -= expand;
            maxWx += expand;
            minWz -= expand;
            maxWz += expand;

            int xMin = Mathf.Clamp(Mathf.FloorToInt(minWx / cs), 0, height.Width - 1);
            int xMax = Mathf.Clamp(Mathf.CeilToInt(maxWx / cs) - 1, 0, height.Width - 1);
            int zMin = Mathf.Clamp(Mathf.FloorToInt(minWz / cs), 0, height.Height - 1);
            int zMax = Mathf.Clamp(Mathf.CeilToInt(maxWz / cs) - 1, 0, height.Height - 1);

            float surfaceY = ComputeFlatWaterY(height, river, waterYOffsetRatio) + surfaceYOffsetMeters;

            int cornerW = xMax - xMin + 2;
            int cornerH = zMax - zMin + 2;
            var cornerIndex = new int[cornerW * cornerH];
            for (int i = 0; i < cornerIndex.Length; i++) cornerIndex[i] = -1;

            var vertices = new List<Vector3>(256);
            var uvs = new List<Vector2>(256);
            var triangles = new List<int>(384);

            int CornerLocal(int cx, int cz) => (cx - xMin) + (cz - zMin) * cornerW;

            int GetOrCreateCorner(int cx, int cz)
            {
                int li = CornerLocal(cx, cz);
                if (cornerIndex[li] >= 0) return cornerIndex[li];

                float wxCorner = cx * cs;
                float wzCorner = cz * cs;
                int id = vertices.Count;
                cornerIndex[li] = id;
                vertices.Add(new Vector3(wxCorner, surfaceY, wzCorner));
                uvs.Add(new Vector2(wxCorner * UvWorldScale, wzCorner * UvWorldScale));
                return id;
            }

            float tagRSq = tagR * tagR;
            for (int z = zMin; z <= zMax; z++)
            {
                for (int x = xMin; x <= xMax; x++)
                {
                    float wx = (x + 0.5f) * cs;
                    float wz = (z + 0.5f) * cs;
                    if (MinDistSqPointPolyline(new Vector2(wx, wz), cells, cs) > tagRSq)
                        continue;

                    int i00 = GetOrCreateCorner(x, z);
                    int i10 = GetOrCreateCorner(x + 1, z);
                    int i11 = GetOrCreateCorner(x + 1, z + 1);
                    int i01 = GetOrCreateCorner(x, z + 1);

                    triangles.Add(i00);
                    triangles.Add(i11);
                    triangles.Add(i10);
                    triangles.Add(i00);
                    triangles.Add(i01);
                    triangles.Add(i11);
                }
            }

            if (vertices.Count == 0) return null;

            var mesh = new Mesh { name = "RiverMesh" };
            if (vertices.Count > 65535) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.triangles = triangles.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static float ComputeFlatWaterY(HeightMap h, RiverPath river, float waterYOffsetRatio)
        {
            float sum = 0f;
            IReadOnlyList<Vector2Int> cells = river.Cells;
            int n = cells.Count;
            for (int i = 0; i < n; i++)
            {
                Vector2Int c = cells[i];
                sum += h.GetHeight(c.x, c.y);
            }

            float avgBed = sum / n;
            return avgBed + river.DepthMeters * Mathf.Clamp01(waterYOffsetRatio);
        }

        private static float MinDistSqPointPolyline(Vector2 p, IReadOnlyList<Vector2Int> cells, float cs)
        {
            float best = float.PositiveInfinity;
            for (int i = 0; i < cells.Count - 1; i++)
            {
                Vector2 a = new Vector2((cells[i].x + 0.5f) * cs, (cells[i].y + 0.5f) * cs);
                Vector2 b = new Vector2((cells[i + 1].x + 0.5f) * cs, (cells[i + 1].y + 0.5f) * cs);
                float d = DistSqPointSegment2D(p, a, b);
                if (d < best) best = d;
            }
            return best;
        }

        private static float DistSqPointSegment2D(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float den = ab.sqrMagnitude;
            if (den < 1e-12f) return (p - a).sqrMagnitude;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / den);
            Vector2 closest = a + t * ab;
            return (p - closest).sqrMagnitude;
        }
    }
}
