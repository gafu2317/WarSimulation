using UnityEngine;

namespace WarSimulation.Combat.Map
{
    /// <summary>
    /// MapData.Lakes を読み取り、各湖に対応する水面ディスクメッシュを Scene に生成するコンポーネント。
    /// 生成オブジェクトは「GeneratedLakes」子配下にまとめ、再生成のたびにクリアする。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LakeRenderer : MonoBehaviour
    {
        private const string LakesRootName = "GeneratedLakes";

        [Tooltip("水面に使うマテリアル。未設定なら URP Lit / Standard の青色マテリアルを自動生成する。")]
        [SerializeField] private Material _waterMaterial;

        [Tooltip("凍結湖の氷面に使うマテリアル。未設定なら白っぽい氷色マテリアルを自動生成する。")]
        [SerializeField] private Material _iceMaterial;

        [Tooltip("凍結湖の氷面を水面より何メートル上に出すか（氷の厚みに相当）。視認性を確保するため少し厚めの既定値。")]
        [SerializeField, Min(0f)] private float _iceSurfaceOffset = 0.15f;

        [Tooltip("凍結湖の氷塊の厚み（メートル）。フラットなディスクでは視認性に欠けたため、立体のシリンダーで描画する。")]
        [SerializeField, Min(0.01f)] private float _iceSlabThickness = 0.3f;

        [Tooltip("湖ディスクのセグメント数（多いほど円が滑らか）。")]
        [SerializeField, Min(8)] private int _segments = 32;

        public void Render(MapData map)
        {
            if (map == null)
            {
                Debug.LogWarning($"[{nameof(LakeRenderer)}] Render called with null MapData.");
                return;
            }

            Clear();

            if (map.Lakes.Count == 0) return;

            var root = new GameObject(LakesRootName);
            root.transform.SetParent(transform, worldPositionStays: false);

            Material waterMat = _waterMaterial != null ? _waterMaterial : CreateDefaultWaterMaterial();
            Material iceMat = _iceMaterial != null ? _iceMaterial : CreateDefaultIceMaterial();

            for (int i = 0; i < map.Lakes.Count; i++)
            {
                LakeRegion lake = map.Lakes[i];

                GameObject go;
                Material picked;

                if (lake.IsFrozen)
                {
                    // 凍結湖は立体のシリンダーで厚みを持たせて描画する。
                    // フラットなディスクだと他のメッシュや地形と同じ高さ付近で見分けが付かないという報告があったため、
                    // 「一目で氷塊と分かる」ジオメトリを優先する。
                    go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    // 不要な物理当たりはこの段階では不要（他の岩なども Collider を付けていないため整合を取る）。
                    var col = go.GetComponent<Collider>();
                    if (col != null)
                    {
                        if (Application.isPlaying) Destroy(col);
                        else DestroyImmediate(col);
                    }
                    go.name = $"Lake_{i}_Frozen";

                    // Unity の Cylinder はデフォルト半径 0.5m・高さ 2m。
                    // これを湖の半径・指定の氷厚にスケールする。
                    float y = lake.WaterY + _iceSurfaceOffset + _iceSlabThickness * 0.5f;
                    go.transform.SetParent(root.transform, worldPositionStays: false);
                    go.transform.localPosition = new Vector3(lake.Center.x, y, lake.Center.y);
                    go.transform.localScale = new Vector3(
                        lake.Radius * 2f,
                        _iceSlabThickness * 0.5f,
                        lake.Radius * 2f);
                    picked = iceMat;
                }
                else
                {
                    // 通常の水面は従来どおりフラットなディスクで描画（影響を与えない）。
                    Mesh mesh = BuildDisc(lake.Center, lake.Radius, lake.WaterY, _segments);
                    go = new GameObject($"Lake_{i}",
                        typeof(MeshFilter), typeof(MeshRenderer));
                    go.transform.SetParent(root.transform, worldPositionStays: false);
                    go.GetComponent<MeshFilter>().sharedMesh = mesh;
                    picked = waterMat;
                }

                go.GetComponent<MeshRenderer>().sharedMaterial = picked;
            }
        }

        public void Clear()
        {
            var existing = transform.Find(LakesRootName);
            if (existing == null) return;

            if (Application.isPlaying) Destroy(existing.gameObject);
            else DestroyImmediate(existing.gameObject);
        }

        /// <summary>
        /// ワールド XZ の (center, radius) に、Y = waterY の平円ディスクメッシュを生成する。
        /// </summary>
        private static Mesh BuildDisc(Vector2 center, float radius, float waterY, int segments)
        {
            int segs = Mathf.Max(8, segments);
            var vertices = new Vector3[segs + 1];
            var uvs = new Vector2[segs + 1];
            vertices[0] = new Vector3(center.x, waterY, center.y);
            uvs[0] = new Vector2(0.5f, 0.5f);

            float step = 2f * Mathf.PI / segs;
            for (int i = 0; i < segs; i++)
            {
                float a = i * step;
                float cos = Mathf.Cos(a);
                float sin = Mathf.Sin(a);
                vertices[i + 1] = new Vector3(center.x + cos * radius, waterY, center.y + sin * radius);
                uvs[i + 1] = new Vector2(0.5f + 0.5f * cos, 0.5f + 0.5f * sin);
            }

            var triangles = new int[segs * 3];
            for (int i = 0; i < segs; i++)
            {
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = ((i + 1) % segs) + 1;
            }

            var mesh = new Mesh { name = "LakeMesh" };
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Material CreateDefaultWaterMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) return null;

            var mat = new Material(shader)
            {
                name = "DefaultLakeWaterMaterial",
                color = new Color(0.20f, 0.50f, 0.95f, 1f),
            };
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.85f);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.1f);
            return mat;
        }

        /// <summary>
        /// 凍結湖（氷面）用のデフォルトマテリアル。
        /// 色選定の背景：
        ///   TerrainRenderer で Snow = (0.95, 0.95, 0.95) 白、Water = (0.20, 0.50, 0.95) 濃い青を使っている。
        ///   氷を純白にすると雪セルと区別が付かず、青寄りにすると水と区別が付かない。
        ///   → 「淡いシアン」に寄せて、雪（白）と水（濃紺）の中間で独立した色相に置く。
        /// シェーダ選定の背景：
        ///   Lit + 高 Smoothness は skybox reflection が支配的になって色が青く転ぶため Unlit を使う。
        ///   Lit 相当の見た目にしたい場合は Inspector で _iceMaterial を手動アサインすれば置き換えられる。
        /// </summary>
        private static Material CreateDefaultIceMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) return null;

            var mat = new Material(shader)
            {
                name = "DefaultLakeIceMaterial",
                color = new Color(0.55f, 0.85f, 1.00f, 1f),
            };
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", mat.color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", mat.color);
            return mat;
        }
    }
}
