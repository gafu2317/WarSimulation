using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Terrain))]
public class TerrainSkirtGenerator : MonoBehaviour
{
    [Header("スカートの設定")]
    [Tooltip("側面の底の高さ（TerrainからのローカルY座標）\n例: -10 にするとTerrainの底よりさらに10m下まで壁が作られます")]
    public float skirtBottomY = -10f;

    [Tooltip("側面に割り当てるマテリアル（地層のテクスチャなど）")]
    public Material skirtMaterial;

    // 右クリックメニューから実行できるようにする属性
    [ContextMenu("スカートを生成する (Generate Skirt)")]
    public void GenerateSkirt()
    {
        Terrain terrain = GetComponent<Terrain>();
        TerrainData tData = terrain.terrainData;
        if (tData == null) return;

        int res = tData.heightmapResolution;
        Vector3 size = tData.size;

        // すでにスカート（壁）が存在する場合は古いものを削除
        Transform oldSkirt = transform.Find("TerrainSkirt");
        if (oldSkirt != null)
        {
            DestroyImmediate(oldSkirt.gameObject);
        }

        // 新しいスカート用オブジェクトを作成
        GameObject skirtObj = new GameObject("TerrainSkirt");
        skirtObj.transform.SetParent(transform);
        skirtObj.transform.localPosition = Vector3.zero;

        MeshFilter mf = skirtObj.AddComponent<MeshFilter>();
        MeshRenderer mr = skirtObj.AddComponent<MeshRenderer>();
        mr.material = skirtMaterial;

        List<Vector3> vertices = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> triangles = new List<int>();

        // --- 手前側の面 (Z = 0) ---
        int startIndex = vertices.Count;
        for (int x = 0; x < res; x++)
        {
            float normX = (float)x / (res - 1);
            float localX = normX * size.x;
            float height = tData.GetHeight(x, 0);

            vertices.Add(new Vector3(localX, height, 0));
            vertices.Add(new Vector3(localX, skirtBottomY, 0));
            uvs.Add(new Vector2(localX, height));
            uvs.Add(new Vector2(localX, skirtBottomY));

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
            float height = tData.GetHeight(x, res - 1);

            vertices.Add(new Vector3(localX, height, size.z));
            vertices.Add(new Vector3(localX, skirtBottomY, size.z));
            uvs.Add(new Vector2(localX, height));
            uvs.Add(new Vector2(localX, skirtBottomY));

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
            float height = tData.GetHeight(0, z);

            vertices.Add(new Vector3(0, height, localZ));
            vertices.Add(new Vector3(0, skirtBottomY, localZ));
            uvs.Add(new Vector2(localZ, height));
            uvs.Add(new Vector2(localZ, skirtBottomY));

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
            float height = tData.GetHeight(res - 1, z);

            vertices.Add(new Vector3(size.x, height, localZ));
            vertices.Add(new Vector3(size.x, skirtBottomY, localZ));
            uvs.Add(new Vector2(localZ, height));
            uvs.Add(new Vector2(localZ, skirtBottomY));

            if (z < res - 1)
            {
                int i = startIndex + z * 2;
                triangles.Add(i); triangles.Add(i + 2); triangles.Add(i + 1);
                triangles.Add(i + 1); triangles.Add(i + 2); triangles.Add(i + 3);
            }
        }

        // メッシュの構築
        Mesh mesh = new Mesh();
        mesh.name = "TerrainSkirtMesh";
        // 頂点数が多くなる場合に備えて32bitインデックスを使用
        if (vertices.Count > 65000)
        {
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        }
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        mf.sharedMesh = mesh;

        Debug.Log("Terrainの側面メッシュ(スカート)を生成しました！");
    }
}