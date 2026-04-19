using UnityEngine;

namespace WarSimulation.Combat.Map
{
    /// <summary>
    /// MapData.Features から「木・岩・魔石」を拾い、シンプルなプリミティブメッシュで 3D 可視化するコンポーネント。
    /// 橋は別レンダラー（<see cref="BridgeRenderer"/>）側が担当するのでここでは扱わない。
    ///
    /// 生成物は全て「GeneratedFeatures」子配下にまとめ、再生成のたびにクリアする。
    /// 見た目は暫定のプログラマーアート：
    ///   - 木  ：円柱（幹）＋ 球（葉冠）の 2 パーツ
    ///   - 岩  ：立方体 1 個を横長・斜め回転で置く
    ///   - 魔石：立方体を Y 軸 45° 回転して縦長にし、結晶っぽく見せる。陣営色 × 役割（Main=大 / Sub=小）で塗り分け。
    /// 本格的なプレハブに差し替えられるよう、マテリアルは Inspector から上書き可能。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FeatureRenderer : MonoBehaviour
    {
        private const string RootName = "GeneratedFeatures";

        [Header("Tree Appearance")]
        [Tooltip("木全体の高さ（メートル）。幹 + 葉冠 の合計の目安。")]
        [SerializeField, Min(0.2f)] private float _treeHeight = 2.4f;

        [Tooltip("幹の半径（メートル）。")]
        [SerializeField, Min(0.02f)] private float _trunkRadius = 0.12f;

        [Tooltip("葉冠（球）の半径（メートル）。幹の上に乗せる。")]
        [SerializeField, Min(0.1f)] private float _foliageRadius = 0.65f;

        [SerializeField] private Material _trunkMaterial;
        [SerializeField] private Material _foliageMaterial;

        [Header("Rock Appearance")]
        [Tooltip("岩 1 個のベースサイズ（メートル、立方体の一辺）。ランダム揺らぎで ±20% 変動する。")]
        [SerializeField, Min(0.1f)] private float _rockSize = 2.6f;

        [Tooltip("岩の縦潰し比の下限。0.8 で高さ 80%、1.0 で立方体。")]
        [SerializeField, Range(0.3f, 1.0f)] private float _rockHeightScaleMin = 0.7f;

        [Tooltip("岩の縦潰し比の上限。0.95 で高さ 95%、1.0 で立方体。")]
        [SerializeField, Range(0.3f, 1.0f)] private float _rockHeightScaleMax = 0.95f;

        [SerializeField] private Material _rockMaterial;

        [Header("Magic Stone Appearance")]
        [Tooltip("メイン魔石 1 個の底面サイズ（メートル、立方体の一辺）。結晶として縦長に伸ばすのは下の Height で。")]
        [SerializeField, Min(0.1f)] private float _mainStoneBaseSize = 1.2f;

        [Tooltip("メイン魔石の高さ（メートル）。拠点扱いなのでかなり目立たせる。")]
        [SerializeField, Min(0.2f)] private float _mainStoneHeight = 3.2f;

        [Tooltip("サブ魔石 1 個の底面サイズ（メートル）。メインより小さめ。")]
        [SerializeField, Min(0.1f)] private float _subStoneBaseSize = 0.8f;

        [Tooltip("サブ魔石の高さ（メートル）。")]
        [SerializeField, Min(0.2f)] private float _subStoneHeight = 1.8f;

        [Tooltip("自陣営魔石の色。未設定なら明るいシアンを自動生成。")]
        [SerializeField] private Material _ownStoneMaterial;

        [Tooltip("敵陣営魔石の色。未設定なら赤を自動生成。")]
        [SerializeField] private Material _enemyStoneMaterial;

        /// <summary>魔石を地面から少し浮かせて「光っている結晶感」を出す量（メートル）。</summary>
        private const float MagicStoneFloatOffset = 0.05f;

        public void Render(MapData map)
        {
            Clear();
            if (map == null) return;
            var features = map.Features;
            if (features.Count == 0) return;

            bool hasAny = false;
            for (int i = 0; i < features.Count; i++)
            {
                if (IsHandledType(features[i].Type)) { hasAny = true; break; }
            }
            if (!hasAny) return;

            var root = new GameObject(RootName);
            root.transform.SetParent(transform, worldPositionStays: false);

            Material trunkMat = _trunkMaterial != null ? _trunkMaterial
                : CreateLitMaterial("AutoTreeTrunk", new Color(0.36f, 0.22f, 0.11f));
            Material foliageMat = _foliageMaterial != null ? _foliageMaterial
                : CreateLitMaterial("AutoTreeFoliage", new Color(0.12f, 0.50f, 0.18f));
            Material rockMat = _rockMaterial != null ? _rockMaterial
                : CreateLitMaterial("AutoRock", new Color(0.45f, 0.45f, 0.47f));
            Material ownStoneMat = _ownStoneMaterial != null ? _ownStoneMaterial
                : CreateLitMaterial("AutoOwnStone", new Color(0.30f, 0.85f, 1.00f), emission: new Color(0.10f, 0.35f, 0.55f));
            Material enemyStoneMat = _enemyStoneMaterial != null ? _enemyStoneMaterial
                : CreateLitMaterial("AutoEnemyStone", new Color(1.00f, 0.35f, 0.35f), emission: new Color(0.55f, 0.12f, 0.12f));

            Mesh cylinderMesh = GetSharedPrimitiveMesh(PrimitiveType.Cylinder, ref _cachedCylinder);
            Mesh sphereMesh = GetSharedPrimitiveMesh(PrimitiveType.Sphere, ref _cachedSphere);
            Mesh cubeMesh = GetSharedPrimitiveMesh(PrimitiveType.Cube, ref _cachedCube);

            int treeIdx = 0;
            int rockIdx = 0;
            int stoneIdx = 0;
            for (int i = 0; i < features.Count; i++)
            {
                PlacedFeature f = features[i];
                switch (f.Type)
                {
                    case FeatureType.Tree:
                        SpawnTree(root.transform, f, trunkMat, foliageMat, cylinderMesh, sphereMesh, treeIdx++);
                        break;
                    case FeatureType.Rock:
                        SpawnRock(root.transform, f, rockMat, cubeMesh, rockIdx++);
                        break;
                    case FeatureType.OwnMainStone:
                        SpawnMagicStone(root.transform, f, ownStoneMat, cubeMesh, "OwnMain", isMain: true, stoneIdx++);
                        break;
                    case FeatureType.OwnSubStone:
                        SpawnMagicStone(root.transform, f, ownStoneMat, cubeMesh, "OwnSub", isMain: false, stoneIdx++);
                        break;
                    case FeatureType.EnemyMainStone:
                        SpawnMagicStone(root.transform, f, enemyStoneMat, cubeMesh, "EnemyMain", isMain: true, stoneIdx++);
                        break;
                    case FeatureType.EnemySubStone:
                        SpawnMagicStone(root.transform, f, enemyStoneMat, cubeMesh, "EnemySub", isMain: false, stoneIdx++);
                        break;
                }
            }
        }

        private static bool IsHandledType(FeatureType t)
        {
            switch (t)
            {
                case FeatureType.Tree:
                case FeatureType.Rock:
                case FeatureType.OwnMainStone:
                case FeatureType.OwnSubStone:
                case FeatureType.EnemyMainStone:
                case FeatureType.EnemySubStone:
                    return true;
                default:
                    return false;
            }
        }

        public void Clear()
        {
            var existing = transform.Find(RootName);
            if (existing == null) return;

            if (Application.isPlaying) Destroy(existing.gameObject);
            else DestroyImmediate(existing.gameObject);
        }

        /// <summary>
        /// 木 1 本分の親 GameObject を生成し、幹（円柱）と葉冠（球）を子として持たせる。
        /// Y は <see cref="PlacedFeature.WorldPosition"/> を地面として扱い、根本をそこに合わせる。
        /// </summary>
        private void SpawnTree(
            Transform parent, PlacedFeature f,
            Material trunkMat, Material foliageMat,
            Mesh cylinder, Mesh sphere, int idx)
        {
            var tree = new GameObject($"Tree_{idx}");
            tree.transform.SetParent(parent, worldPositionStays: false);
            tree.transform.SetPositionAndRotation(f.WorldPosition, f.Rotation);

            // 木全体の高さを「幹 60% + 葉冠 40% だけ中心を押し上げる」で分ける。
            float trunkHeight = _treeHeight * 0.6f;
            float foliageCenterY = trunkHeight + _foliageRadius * 0.6f;

            // Unity のデフォルト Cylinder は Y 軸に沿って高さ 2m、半径 0.5m。
            // localScale.y = targetHeight / 2 で高さを、localScale.x/z = targetDiameter で太さを作る。
            var trunk = new GameObject("Trunk", typeof(MeshFilter), typeof(MeshRenderer));
            trunk.transform.SetParent(tree.transform, worldPositionStays: false);
            trunk.transform.localPosition = new Vector3(0f, trunkHeight * 0.5f, 0f);
            trunk.transform.localScale = new Vector3(_trunkRadius * 2f, trunkHeight * 0.5f, _trunkRadius * 2f);
            trunk.GetComponent<MeshFilter>().sharedMesh = cylinder;
            trunk.GetComponent<MeshRenderer>().sharedMaterial = trunkMat;

            // Unity のデフォルト Sphere は直径 1m。localScale = diameter で好きなサイズに。
            var foliage = new GameObject("Foliage", typeof(MeshFilter), typeof(MeshRenderer));
            foliage.transform.SetParent(tree.transform, worldPositionStays: false);
            foliage.transform.localPosition = new Vector3(0f, foliageCenterY, 0f);
            float foliageDiameter = _foliageRadius * 2f;
            foliage.transform.localScale = new Vector3(foliageDiameter, foliageDiameter, foliageDiameter);
            foliage.GetComponent<MeshFilter>().sharedMesh = sphere;
            foliage.GetComponent<MeshRenderer>().sharedMaterial = foliageMat;
        }

        /// <summary>
        /// 岩 1 個分の立方体を生成する。見た目が揃いすぎないよう、位置から決定的に
        /// スケールと回転をわずかに揺らす。
        /// </summary>
        private void SpawnRock(Transform parent, PlacedFeature f, Material mat, Mesh cube, int idx)
        {
            var rock = new GameObject($"Rock_{idx}", typeof(MeshFilter), typeof(MeshRenderer));
            rock.transform.SetParent(parent, worldPositionStays: false);

            // 位置ベースで決定的に揺らぎを作る（再生成しても同じ岩が同じ見た目になる）
            uint seed = unchecked((uint)Mathf.FloorToInt(f.WorldPosition.x * 73.1f + f.WorldPosition.z * 19.7f + 37.3f));
            if (seed == 0u) seed = 1u;
            float sx = Mathf.Lerp(0.85f, 1.15f, NextFloat01(ref seed));
            float sz = Mathf.Lerp(0.85f, 1.15f, NextFloat01(ref seed));
            float hMin = Mathf.Min(_rockHeightScaleMin, _rockHeightScaleMax);
            float hMax = Mathf.Max(_rockHeightScaleMin, _rockHeightScaleMax);
            float sy = Mathf.Lerp(hMin, hMax, NextFloat01(ref seed));
            float yaw = NextFloat01(ref seed) * 360f;

            // Cube はローカル ±0.5 の立方体。根本を地面に合わせたいので Y 半分だけ上げる。
            Vector3 pos = f.WorldPosition + new Vector3(0f, _rockSize * sy * 0.5f, 0f);
            rock.transform.SetPositionAndRotation(pos, f.Rotation * Quaternion.Euler(0f, yaw, 0f));
            rock.transform.localScale = new Vector3(_rockSize * sx, _rockSize * sy, _rockSize * sz);
            rock.GetComponent<MeshFilter>().sharedMesh = cube;
            rock.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }

        /// <summary>
        /// 魔石 1 個分を生成する。立方体を Y 軸 45° 回転させて上から見たときにひし形になるようにし、
        /// 縦長にスケールして結晶っぽく見せる。Main はサイズ・高さともに大きく、Sub は控えめ。
        /// </summary>
        private void SpawnMagicStone(
            Transform parent, PlacedFeature f, Material mat, Mesh cube,
            string label, bool isMain, int idx)
        {
            float baseSize = isMain ? _mainStoneBaseSize : _subStoneBaseSize;
            float height = isMain ? _mainStoneHeight : _subStoneHeight;

            var stone = new GameObject($"{label}Stone_{idx}", typeof(MeshFilter), typeof(MeshRenderer));
            stone.transform.SetParent(parent, worldPositionStays: false);

            // 地面 (f.WorldPosition.y) の少し上から、高さの半分だけ Y を持ち上げる
            Vector3 pos = f.WorldPosition + new Vector3(0f, MagicStoneFloatOffset + height * 0.5f, 0f);

            // Y 軸 45° 回転で上から見たひし形に。f.Rotation は魔石では Quaternion.identity 前提だが、
            // 外部で明示的に向きを付けたケースを尊重するため、f.Rotation に 45° を後段合成する。
            stone.transform.SetPositionAndRotation(pos, f.Rotation * Quaternion.Euler(0f, 45f, 0f));

            // Cube はローカル ±0.5 なので、localScale = 目的のワールドサイズ。
            stone.transform.localScale = new Vector3(baseSize, height, baseSize);

            stone.GetComponent<MeshFilter>().sharedMesh = cube;
            stone.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }

        private static Mesh _cachedCylinder;
        private static Mesh _cachedSphere;
        private static Mesh _cachedCube;

        /// <summary>
        /// Unity の既定プリミティブメッシュを取得してキャッシュする。
        /// 毎回 CreatePrimitive すると大量の GameObject が生成されるため、一度取り出した Mesh だけ再利用。
        /// </summary>
        private static Mesh GetSharedPrimitiveMesh(PrimitiveType type, ref Mesh cache)
        {
            if (cache != null) return cache;
            var temp = GameObject.CreatePrimitive(type);
            cache = temp.GetComponent<MeshFilter>().sharedMesh;
            if (Application.isPlaying) Destroy(temp);
            else DestroyImmediate(temp);
            return cache;
        }

        private static Material CreateLitMaterial(string name, Color color, Color? emission = null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) return null;

            var mat = new Material(shader) { name = name, color = color };
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.15f);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);

            // 魔石のように「ぼんやり光る」表現を足したい時だけ emission を有効にする。
            if (emission.HasValue)
            {
                Color em = emission.Value;
                if (mat.HasProperty("_EmissionColor"))
                {
                    mat.SetColor("_EmissionColor", em);
                    mat.EnableKeyword("_EMISSION");
                    // URP Lit は globalIlluminationFlags も見るのでオフにしてランタイム反映だけにする
                    mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                }
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.6f);
            }
            return mat;
        }

        /// <summary>xorshift32 ベースの軽量 PRNG。岩の揺らぎを「位置に紐づけて決定的に」作るために使う。</summary>
        private static float NextFloat01(ref uint state)
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return (state & 0x00FFFFFFu) / (float)0x01000000;
        }
    }
}
