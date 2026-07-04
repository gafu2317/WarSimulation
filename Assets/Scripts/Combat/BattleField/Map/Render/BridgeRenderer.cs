using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace WarSimulation.Combat.Map
{
    /// <summary>
    /// MapData.Features から FeatureType.Bridge を拾い、1 つずつ直方体メッシュを生成して可視化する。
    /// 生成オブジェクトは「GeneratedBridges」子配下にまとめ、再生成のたびにクリアする。
    ///
    /// スケール規約：local +X = 幅（川沿い）、+Y = 厚み、+Z = 長さ（川を跨ぐ方向）。
    /// BridgePhase の回転もこの規約で算出されている。
    ///
    /// NavMesh：見た目 Cube は <see cref="NavMeshModifier.ignoreFromBuild"/>、
    /// 上面 Quad のみ Walkable としてベイクする。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BridgeRenderer : MonoBehaviour
    {
        private const string BridgesRootName = "GeneratedBridges";

        [Tooltip("橋の全面に貼るテクスチャ。未設定なら茶色。")]
        [SerializeField] private Texture2D _bridgeTexture;

        [SerializeField, Min(0.01f)] private float _bridgeTextureTiling = 1f;

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

            Material mat = CreateBridgeMaterial(_bridgeTexture, _bridgeTextureTiling);
            Mesh cubeMesh = GetSharedCubeMesh();
            Mesh deckMesh = GetSharedDeckQuadMesh();
            int walkableArea = ResolveWalkableAreaIndex();

            Vector3 fallbackScale = new Vector3(
                config.BridgeWidth,
                config.BridgeThickness,
                config.BridgeWidth + config.BridgeLengthExtraMargin);

            int idx = 0;
            for (int i = 0; i < map.Features.Count; i++)
            {
                PlacedFeature f = map.Features[i];
                if (f.Type != FeatureType.Bridge) continue;

                Vector3 scale = IsValidScale(f.Scale) ? f.Scale : fallbackScale;

                var bridgeRoot = new GameObject($"Bridge_{idx++}");
                bridgeRoot.transform.SetParent(root.transform, worldPositionStays: false);
                bridgeRoot.transform.localPosition = f.WorldPosition;
                bridgeRoot.transform.localRotation = f.Rotation;
                bridgeRoot.transform.localScale = scale;

                var visual = new GameObject("Visual", typeof(MeshFilter), typeof(MeshRenderer));
                visual.transform.SetParent(bridgeRoot.transform, worldPositionStays: false);
                visual.GetComponent<MeshFilter>().sharedMesh = cubeMesh;
                visual.GetComponent<MeshRenderer>().sharedMaterial = mat;
                var visualNav = visual.AddComponent<NavMeshModifier>();
                visualNav.ignoreFromBuild = true;

                var walkSurface = new GameObject("WalkSurface", typeof(MeshFilter), typeof(MeshRenderer));
                walkSurface.transform.SetParent(bridgeRoot.transform, worldPositionStays: false);
                walkSurface.GetComponent<MeshFilter>().sharedMesh = deckMesh;
                walkSurface.GetComponent<MeshRenderer>().sharedMaterial = mat;
                var walkNav = walkSurface.AddComponent<NavMeshModifier>();
                walkNav.overrideArea = true;
                walkNav.area = walkableArea;
            }
        }

        public void Clear()
        {
            var existing = transform.Find(BridgesRootName);
            if (existing == null) return;

            GameObject existingGameObject = existing.gameObject;
            if (Application.isPlaying)
            {
                existingGameObject.SetActive(false);
                Destroy(existingGameObject);
            }
            else
            {
                DestroyImmediate(existingGameObject);
            }
        }

        private static bool IsValidScale(Vector3 scale) =>
            scale.x > 0f && scale.y > 0f && scale.z > 0f;

        private static int ResolveWalkableAreaIndex()
        {
            int walkable = NavMesh.GetAreaFromName("Walkable");
            return walkable >= 0 ? walkable : 0;
        }

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

        /// <summary>
        /// 単位 Cube（±0.5）の上面。親 Transform の scale で幅×長さの歩行面になる。
        /// </summary>
        private static Mesh _cachedDeckQuadMesh;
        private static Mesh GetSharedDeckQuadMesh()
        {
            if (_cachedDeckQuadMesh != null) return _cachedDeckQuadMesh;

            const float y = 0.5f;
            var mesh = new Mesh { name = "BridgeDeckQuad" };
            mesh.SetVertices(new[]
            {
                new Vector3(-0.5f, y, -0.5f),
                new Vector3(0.5f, y, -0.5f),
                new Vector3(0.5f, y, 0.5f),
                new Vector3(-0.5f, y, 0.5f),
            });
            mesh.SetTriangles(new[] { 0, 2, 1, 0, 3, 2 }, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            _cachedDeckQuadMesh = mesh;
            return _cachedDeckQuadMesh;
        }

        private static Material CreateBridgeMaterial(Texture2D texture, float tiling)
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
            if (texture != null)
            {
                Vector2 scale = Vector2.one * Mathf.Max(0.01f, tiling);
                if (mat.HasProperty("_BaseMap"))
                {
                    mat.SetTexture("_BaseMap", texture);
                    mat.SetTextureScale("_BaseMap", scale);
                    mat.SetColor("_BaseColor", Color.white);
                }
                if (mat.HasProperty("_MainTex"))
                {
                    mat.SetTexture("_MainTex", texture);
                    mat.SetTextureScale("_MainTex", scale);
                    mat.SetColor("_Color", Color.white);
                }
            }
            return mat;
        }
    }
}
