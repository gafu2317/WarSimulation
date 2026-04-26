using UnityEngine;

namespace WarSimulation.Combat.Map
{
    /// <summary>
    /// MapData.Rivers を読み取り、各川に対応する水面メッシュを Scene に生成するコンポーネント。
    /// 生成オブジェクトは全て「GeneratedRivers」子配下にまとめ、再生成のたびにクリアする。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RiverRenderer : MonoBehaviour
    {
        private const string RiversRootName = "GeneratedRivers";
        private const float SurfaceYOffsetMeters = -0.1f;
        private const int SmoothingIterations = 0;

        [Tooltip("水面に使うマテリアル。未設定の場合は URP Lit か Standard の青色マテリアルを自動生成する。")]
        [SerializeField] private Material _waterMaterial;

        [Tooltip("川床からの水面高さを DepthMeters に対する比率で指定（0.85 で 85% の高さ）。大きいほど水位が高くなり岸に近づく。")]
        [SerializeField, Range(0f, 1f)] private float _waterYOffsetRatio = 0.85f;

        public void Render(MapData map)
        {
            if (map == null)
            {
                Debug.LogWarning($"[{nameof(RiverRenderer)}] Render called with null MapData.");
                return;
            }

            Clear();

            if (map.Rivers.Count == 0) return;

            var root = new GameObject(RiversRootName);
            root.transform.SetParent(transform, worldPositionStays: false);

            Material waterMat = _waterMaterial != null ? _waterMaterial : CreateDefaultWaterMaterial();
            if (waterMat == null)
            {
                Debug.LogWarning(
                    $"[{nameof(RiverRenderer)}] 水面マテリアルを生成できませんでした。川メッシュはマテリアルなしで出力されます。");
            }

            for (int i = 0; i < map.Rivers.Count; i++)
            {
                RiverPath river = map.Rivers[i];
                Mesh mesh = RiverMeshBuilder.Build(
                    river, map.Height, _waterYOffsetRatio, SmoothingIterations, 0f);
                if (mesh == null) continue;

                var go = new GameObject($"River_{i}",
                    typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(root.transform, worldPositionStays: false);
                go.transform.localPosition = new Vector3(0f, SurfaceYOffsetMeters, 0f);
                go.GetComponent<MeshFilter>().sharedMesh = mesh;
                go.GetComponent<MeshRenderer>().sharedMaterial = waterMat;
            }
        }

        public void Clear()
        {
            var existing = transform.Find(RiversRootName);
            if (existing == null) return;

            if (Application.isPlaying) Destroy(existing.gameObject);
            else DestroyImmediate(existing.gameObject);
        }

        /// <summary>
        /// URP or Built-in いずれでも青色で表示できるデフォルトマテリアルを生成する。
        /// ユーザーが _waterMaterial を指定した場合はそちらが優先されるため、
        /// ここはフォールバック用途。
        /// </summary>
        private static Material CreateDefaultWaterMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) return null;

            var mat = new Material(shader)
            {
                name = "DefaultWaterMaterial",
                color = new Color(0.20f, 0.50f, 0.95f, 1f),
            };

            // URP Lit の場合はスムーズネスを少し上げて水っぽく見せる。存在しないプロパティは無視される。
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.8f);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.1f);

            return mat;
        }
    }
}
