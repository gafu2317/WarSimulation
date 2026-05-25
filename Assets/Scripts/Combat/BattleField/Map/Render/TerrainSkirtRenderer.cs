using UnityEngine;

namespace WarSimulation.Combat.Map
{
    /// <summary>
    /// <see cref="TerrainRenderer"/> が生成した Terrain の外周に、地中を隠す土壁メッシュを付ける。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TerrainSkirtRenderer : MonoBehaviour
    {
        private const string SkirtObjectName = "TerrainSkirt";

        [Tooltip("false のときスカートを生成しない。")]
        [SerializeField] private bool _skirtEnabled = true;

        [Tooltip("Terrain 底面（ローカル Y=0）から下方向へ垂らす深さ（メートル）。")]
        [SerializeField, Min(0.01f)] private float _skirtDepthMeters = 8f;

        [Tooltip("側面マテリアル。未設定時は Cliff 色に近い茶色の Lit マテリアルを自動生成する。")]
        [SerializeField] private Material _skirtMaterial;

        public void Render(MapData map)
        {
            if (!_skirtEnabled)
            {
                Clear();
                return;
            }

            if (map == null)
            {
                Debug.LogWarning($"[{nameof(TerrainSkirtRenderer)}] Render called with null MapData.");
                return;
            }

            var terrainRenderer = GetComponent<TerrainRenderer>();
            Terrain terrain = terrainRenderer != null ? terrainRenderer.Terrain : null;
            if (terrain == null || terrain.terrainData == null)
            {
                Debug.LogWarning(
                    $"[{nameof(TerrainSkirtRenderer)}] TerrainRenderer.Terrain が未生成です。先に TerrainRenderer.Render を呼んでください。");
                return;
            }

            ClearUnder(terrain.transform);

            float skirtBottomLocalY = -_skirtDepthMeters;
            Mesh mesh = TerrainSkirtMeshBuilder.Build(terrain.terrainData, skirtBottomLocalY);
            if (mesh == null) return;

            var skirtObj = new GameObject(SkirtObjectName, typeof(MeshFilter), typeof(MeshRenderer));
            skirtObj.transform.SetParent(terrain.transform, worldPositionStays: false);
            skirtObj.transform.localPosition = Vector3.zero;
            skirtObj.transform.localRotation = Quaternion.identity;
            skirtObj.transform.localScale = Vector3.one;

            skirtObj.GetComponent<MeshFilter>().sharedMesh = mesh;

            Material mat = _skirtMaterial != null ? _skirtMaterial : CreateDefaultSkirtMaterial();
            skirtObj.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }

        public void Clear()
        {
            var terrainRenderer = GetComponent<TerrainRenderer>();
            if (terrainRenderer != null && terrainRenderer.Terrain != null)
            {
                ClearUnder(terrainRenderer.Terrain.transform);
            }
        }

        private static void ClearUnder(Transform terrainTransform)
        {
            if (terrainTransform == null) return;

            Transform existing = terrainTransform.Find(SkirtObjectName);
            if (existing == null) return;

            GameObject go = existing.gameObject;
            if (Application.isPlaying) Destroy(go);
            else DestroyImmediate(go);
        }

        private static Material CreateDefaultSkirtMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) return null;

            return new Material(shader)
            {
                name = "DefaultTerrainSkirtMaterial",
                color = new Color(0.30f, 0.18f, 0.10f, 1f),
            };
        }
    }
}
