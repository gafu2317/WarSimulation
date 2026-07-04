using System.Collections.Generic;
using UnityEngine;

namespace WarSimulation.Combat.Map
{
    /// <summary>
    /// 湖の開放水面メッシュを <see cref="HeightMap"/> から生成する。
    /// 川の <see cref="RiverMeshBuilder"/> と同様、掘削後の地形を参照して岸線を決め、
    /// 単色ディスクだけでは出ない「地形に沿った」水面境界を作る。
    /// </summary>
    public static class LakeMeshBuilder
    {
        /// <summary>
        /// 湖の Water タグ範囲セルを繋いだ水平面メッシュを返す。
        /// 条件を満たすセルが無ければ null。
        /// </summary>
        public static Mesh BuildOpenWaterSurface(
            LakeRegion lake, HeightMap height, float waterY, float radiusScale = 1f)
            => BuildSurfaceCore(lake, height, waterY, useCarveRegion: false, "LakeGridWaterMesh", radiusScale);

        /// <summary>
        /// 凍結湖の氷上面を、実際の湖輪郭（掘削領域）に合わせて生成する。
        /// </summary>
        public static Mesh BuildFrozenTopSurface(
            LakeRegion lake, HeightMap height, float topY, float radiusScale = 1f)
            => BuildSurfaceCore(lake, height, topY, useCarveRegion: true, "LakeFrozenTopMesh", radiusScale);

        /// <summary>
        /// 上面メッシュを下方向に押し出して、厚み付きの氷塊メッシュにする。
        /// </summary>
        public static Mesh BuildExtrudedSlab(Mesh topMesh, float thickness)
        {
            if (topMesh == null || thickness <= 0f) return topMesh;

            var topVertices = topMesh.vertices;
            var topUv = topMesh.uv;
            var topTriangles = topMesh.triangles;
            int n = topVertices.Length;
            if (n == 0 || topTriangles == null || topTriangles.Length < 3) return topMesh;

            var vertices = new List<Vector3>(n * 2);
            var uvs = new List<Vector2>(n * 2);
            vertices.AddRange(topVertices);
            for (int i = 0; i < n; i++)
            {
                Vector3 b = topVertices[i];
                b.y -= thickness;
                vertices.Add(b);
            }

            if (topUv != null && topUv.Length == n)
            {
                uvs.AddRange(topUv);
                uvs.AddRange(topUv);
            }
            else
            {
                for (int i = 0; i < n * 2; i++) uvs.Add(Vector2.zero);
            }

            var triangles = new List<int>(topTriangles.Length * 2);
            // 上面
            triangles.AddRange(topTriangles);
            // 底面（反転）
            for (int i = 0; i < topTriangles.Length; i += 3)
            {
                int a = topTriangles[i] + n;
                int b = topTriangles[i + 1] + n;
                int c = topTriangles[i + 2] + n;
                triangles.Add(a);
                triangles.Add(c);
                triangles.Add(b);
            }

            // 境界エッジだけ側面を作る
            var edgeUseCount = new Dictionary<(int, int), int>();
            void AddEdge(int a, int b)
            {
                var key = a < b ? (a, b) : (b, a);
                if (edgeUseCount.TryGetValue(key, out int cnt)) edgeUseCount[key] = cnt + 1;
                else edgeUseCount[key] = 1;
            }

            for (int i = 0; i < topTriangles.Length; i += 3)
            {
                int a = topTriangles[i];
                int b = topTriangles[i + 1];
                int c = topTriangles[i + 2];
                AddEdge(a, b);
                AddEdge(b, c);
                AddEdge(c, a);
            }

            foreach (var kv in edgeUseCount)
            {
                if (kv.Value != 1) continue;
                int a = kv.Key.Item1;
                int b = kv.Key.Item2;
                int a2 = a + n;
                int b2 = b + n;

                triangles.Add(a);
                triangles.Add(b2);
                triangles.Add(b);

                triangles.Add(a);
                triangles.Add(a2);
                triangles.Add(b2);
            }

            var mesh = new Mesh { name = "LakeIceSlabMesh" };
            if (vertices.Count > 65535) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.triangles = triangles.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh BuildSurfaceCore(
            LakeRegion lake,
            HeightMap height,
            float surfaceY,
            bool useCarveRegion,
            string meshName,
            float radiusScale)
        {
            if (height == null) return null;

            float cs = height.CellSize;
            Vector2 c = lake.Center;
            float scale = Mathf.Max(0.1f, radiusScale);
            float bound = lake.OuterRadius * scale + cs;

            int xMin = Mathf.Clamp(Mathf.FloorToInt((c.x - bound) / cs), 0, height.Width - 1);
            int xMax = Mathf.Clamp(Mathf.CeilToInt((c.x + bound) / cs) - 1, 0, height.Width - 1);
            int zMin = Mathf.Clamp(Mathf.FloorToInt((c.y - bound) / cs), 0, height.Height - 1);
            int zMax = Mathf.Clamp(Mathf.CeilToInt((c.y + bound) / cs) - 1, 0, height.Height - 1);

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

                float wx = cx * cs;
                float wz = cz * cs;
                int id = vertices.Count;
                cornerIndex[li] = id;
                vertices.Add(new Vector3(wx, surfaceY, wz));
                // マテリアル側で繰り返し模様を載せやすいようワールド XZ を UV に流す
                uvs.Add(new Vector2(wx * 0.08f, wz * 0.08f));
                return id;
            }

            for (int z = zMin; z <= zMax; z++)
            {
                for (int x = xMin; x <= xMax; x++)
                {
                    float wx = (x + 0.5f) * cs;
                    float wz = (z + 0.5f) * cs;
                    var p = new Vector2(wx, wz);
                    Vector2 d = p - lake.Center;
                    float eff = lake.EffectiveRadius(d.x, d.y) * scale;
                    if (useCarveRegion) { if (d.sqrMagnitude > eff * eff) continue; }
                    else
                    {
                        float tagR = eff * lake.WaterTagRatio;
                        if (d.sqrMagnitude > tagR * tagR) continue;
                    }

                    int i00 = GetOrCreateCorner(x, z);
                    int i10 = GetOrCreateCorner(x + 1, z);
                    int i11 = GetOrCreateCorner(x + 1, z + 1);
                    int i01 = GetOrCreateCorner(x, z + 1);

                    // 上面（+Y）を向くよう反時計回りで構成する
                    triangles.Add(i00);
                    triangles.Add(i11);
                    triangles.Add(i10);
                    triangles.Add(i00);
                    triangles.Add(i01);
                    triangles.Add(i11);
                }
            }

            if (vertices.Count == 0) return null;

            var mesh = new Mesh { name = meshName };
            if (vertices.Count > 65535) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.triangles = triangles.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
