using UnityEngine;

namespace WarSimulation.Combat.Map
{
    /// <summary>
    /// MapData.Features から FeatureType.Bridge を拾い、1 つずつ直方体メッシュを生成して可視化する。
    /// 生成オブジェクトは「GeneratedBridges」子配下にまとめ、再生成のたびにクリアする。
    ///
    /// スケール規約：local +X = 幅（川沿い）、+Y = 厚み、+Z = 長さ（川を跨ぐ方向）。
    /// BridgePhase の回転もこの規約で算出されている。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BridgeRenderer : MonoBehaviour
    {
        private const string BridgesRootName = "GeneratedBridges";

        [Tooltip("橋に使うマテリアル。未設定なら URP Lit / Standard の茶色マテリアルを自動生成する。")]
        [SerializeField] private Material _bridgeMaterial;

        public void Render(MapData map, MapGenerationConfig config)
        {
            if (map == null || config == null)
            {
                Debug.LogWarning($"[{nameof(BridgeRenderer)}] Render called with null arg.");
                return;
            }

            Clear();
            if (map.Features.Count == 0) return;

            var root = new GameObject(BridgesRootName);
            root.transform.SetParent(transform, worldPositionStays: false);

            Material mat = _bridgeMaterial != null ? _bridgeMaterial : CreateDefaultBridgeMaterial();
            Mesh cubeMesh = GetSharedCubeMesh();

            Vector3 fallbackScale = new Vector3(
                config.BridgeWidth,
                config.BridgeThickness,
                config.BridgeWidth + config.BridgeLengthExtraMargin);

            int idx = 0;
            for (int i = 0; i < map.Features.Count; i++)
            {
                PlacedFeature f = map.Features[i];
                if (f.Type != FeatureType.Bridge) continue;

                var go = new GameObject($"Bridge_{idx++}",
                    typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(root.transform, worldPositionStays: false);
                go.transform.localPosition = f.WorldPosition;
                go.transform.localRotation = f.Rotation;
                go.transform.localScale = IsValidScale(f.Scale) ? f.Scale : fallbackScale;
                go.GetComponent<MeshFilter>().sharedMesh = cubeMesh;
                go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            }
        }

        public void Clear()
        {
            var existing = transform.Find(BridgesRootName);
            if (existing == null) return;

            if (Application.isPlaying) Destroy(existing.gameObject);
            else DestroyImmediate(existing.gameObject);
        }

        private static bool IsValidScale(Vector3 scale) =>
            scale.x > 0f && scale.y > 0f && scale.z > 0f;

        /// <summary>
        /// Unity の既定 Cube メッシュを取得する。エディタでもランタイムでも利用可。
        /// 毎回 PrimitiveMesh を新規生成しないよう、GameObject を一度作って Mesh だけ借りて破棄する。
        /// </summary>
        private static Mesh _cachedCubeMesh;
        private static Mesh GetSharedCubeMesh()
        {
            if (_cachedCubeMesh != null) return _cachedCubeMesh;

            var temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _cachedCubeMesh = temp.GetComponent<MeshFilter>().sharedMesh;
            if (Application.isPlaying) Destroy(temp);
            else DestroyImmediate(temp);
            return _cachedCubeMesh;
        }

        private static Material CreateDefaultBridgeMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) return null;

            var mat = new Material(shader)
            {
                name = "DefaultBridgeMaterial",
                color = new Color(0.45f, 0.30f, 0.18f, 1f), // 木の茶色
            };
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.2f);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
            return mat;
        }
    }
}
