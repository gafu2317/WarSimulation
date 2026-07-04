using UnityEngine;
using WarSimulation.Combat.Map;

[RequireComponent(typeof(Terrain))]
public class TerrainSkirtGenerator : MonoBehaviour
{
    [Header("スカートの設定")]
    [Tooltip("側面の底の高さ（TerrainからのローカルY座標）\n例: -10 にするとTerrainの底よりさらに10m下まで壁が作られます")]
    public float skirtBottomY = -10f;

    [Tooltip("側面に割り当てるマテリアル（地層のテクスチャなど）")]
    public Material skirtMaterial;

    [ContextMenu("スカートを生成する (Generate Skirt)")]
    public void GenerateSkirt()
    {
        Terrain terrain = GetComponent<Terrain>();
        TerrainData tData = terrain.terrainData;
        if (tData == null) return;

        Transform oldSkirt = transform.Find("TerrainSkirt");
        if (oldSkirt != null)
        {
            DestroyImmediate(oldSkirt.gameObject);
        }

        Mesh mesh = TerrainSkirtMeshBuilder.Build(tData, skirtBottomY);
        if (mesh == null) return;

        GameObject skirtObj = new GameObject("TerrainSkirt", typeof(MeshFilter), typeof(MeshRenderer));
        skirtObj.transform.SetParent(transform);
        skirtObj.transform.localPosition = Vector3.zero;

        skirtObj.GetComponent<MeshFilter>().sharedMesh = mesh;
        skirtObj.GetComponent<MeshRenderer>().sharedMaterial = skirtMaterial;

        Debug.Log("Terrainの側面メッシュ(スカート)を生成しました！");
    }
}
