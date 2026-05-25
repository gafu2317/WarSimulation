using System.Collections.Generic;
using UnityEngine;

namespace WarSimulation.Combat.Map
{
    /// <summary>
    /// Unity Terrain の外周四辺に垂らす側面メッシュ（スカート）を組み立てる。
    /// 頂点は Terrain ローカル座標（<see cref="TerrainData.GetHeight"/> と <see cref="TerrainData.size"/> 基準）。
    /// </summary>
    public static class TerrainSkirtMeshBuilder
    {
        public static Mesh Build(TerrainData data, float skirtBottomLocalY)
        {
            if (data == null) return null;

            int res = data.heightmapResolution;
            if (res < 2) return null;

            Vector3 size = data.size;

            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();

            // --- 手前側の面 (Z = 0) ---
            int startIndex = vertices.Count;
            for (int x = 0; x < res; x++)
            {
                float normX = (float)x / (res - 1);
                float localX = normX * size.x;
                float height = data.GetHeight(x, 0);

                vertices.Add(new Vector3(localX, height, 0));
                vertices.Add(new Vector3(localX, skirtBottomLocalY, 0));
                uvs.Add(new Vector2(localX, height));
                uvs.Add(new Vector2(localX, skirtBottomLocalY));

                if (x < res - 1)
                {
                    int i = startIndex + x * 2;
                    triangles.Add(i); triangles.Add(i + 2); triangles.Add(i + 1);
                    triangles.Add(i + 1); triangles.Add(i + 2); triangles.Add(i + 3);
                }
            }

            // --- 奥側の面 (Z = res - 1) ---
            startIndex = vertices.Count;
            for (int x = 0; x < res; x++)
            {
                float normX = (float)x / (res - 1);
                float localX = normX * size.x;
                float height = data.GetHeight(x, res - 1);

                vertices.Add(new Vector3(localX, height, size.z));
                vertices.Add(new Vector3(localX, skirtBottomLocalY, size.z));
                uvs.Add(new Vector2(localX, height));
                uvs.Add(new Vector2(localX, skirtBottomLocalY));

                if (x < res - 1)
                {
                    int i = startIndex + x * 2;
                    triangles.Add(i); triangles.Add(i + 1); triangles.Add(i + 2);
                    triangles.Add(i + 1); triangles.Add(i + 3); triangles.Add(i + 2);
                }
            }

            // --- 左側の面 (X = 0) ---
            startIndex = vertices.Count;
            for (int z = 0; z < res; z++)
            {
                float normZ = (float)z / (res - 1);
                float localZ = normZ * size.z;
                float height = data.GetHeight(0, z);

                vertices.Add(new Vector3(0, height, localZ));
                vertices.Add(new Vector3(0, skirtBottomLocalY, localZ));
                uvs.Add(new Vector2(localZ, height));
                uvs.Add(new Vector2(localZ, skirtBottomLocalY));

                if (z < res - 1)
                {
                    int i = startIndex + z * 2;
                    triangles.Add(i); triangles.Add(i + 1); triangles.Add(i + 2);
                    triangles.Add(i + 1); triangles.Add(i + 3); triangles.Add(i + 2);
                }
            }

            // --- 右側の面 (X = res - 1) ---
            startIndex = vertices.Count;
            for (int z = 0; z < res; z++)
            {
                float normZ = (float)z / (res - 1);
                float localZ = normZ * size.z;
                float height = data.GetHeight(res - 1, z);

                vertices.Add(new Vector3(size.x, height, localZ));
                vertices.Add(new Vector3(size.x, skirtBottomLocalY, localZ));
                uvs.Add(new Vector2(localZ, height));
                uvs.Add(new Vector2(localZ, skirtBottomLocalY));

                if (z < res - 1)
                {
                    int i = startIndex + z * 2;
                    triangles.Add(i); triangles.Add(i + 2); triangles.Add(i + 1);
                    triangles.Add(i + 1); triangles.Add(i + 2); triangles.Add(i + 3);
                }
            }

            var mesh = new Mesh { name = "TerrainSkirtMesh" };
            if (vertices.Count > 65000)
            {
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            }

            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
