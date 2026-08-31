using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace WarSimulation.Combat.Map
{
    /// <summary>
    /// MapData.Features から「木・岩・魔石」を拾い、3D 可視化するコンポーネント。
    /// 橋は別レンダラー（<see cref="BridgeRenderer"/>）側が担当するのでここでは扱わない。
    ///
    /// 生成物は全て「GeneratedFeatures」子配下にまとめ、再生成のたびにクリアする。
    /// 見た目は次の構成で生成する：
    ///   - 木  ：設定済みPrefab。未設定時は円柱（幹）＋球（葉冠）の旧方式にフォールバック
    ///   - 岩  ：立方体 1 個を横長・斜め回転で置く
    ///   - 魔石：Resources の認定済みモデルPrefabを使い、Coreだけ陣営色で塗り分ける。
    /// 各パーツのテクスチャは Inspector から指定し、描画用 Lit マテリアルを自動生成する。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FeatureRenderer : MonoBehaviour
    {
        private const string RootName = "GeneratedFeatures";
        private const string VisionObstacleLayerName = "VisionObstacle";
        private const string NotWalkableAreaName = "Not Walkable";
        private const float TreeSizeMultiplier = 1.5f;
        private const float RockSizeMultiplier = 2f;

        [Header("Tree Appearance")]
        [Tooltip("木全体の高さ（メートル）。幹 + 葉冠 の合計の目安。")]
        [SerializeField, Min(0.2f)] private float _treeHeight = 2.4f;

        [Tooltip("幹の半径（メートル）。")]
        [SerializeField, Min(0.02f)] private float _trunkRadius = 0.12f;

        [Tooltip("葉冠（球）の半径（メートル）。幹の上に乗せる。")]
        [SerializeField, Min(0.1f)] private float _foliageRadius = 0.65f;

        [Tooltip("木全体の高さ倍率の下限。位置から決定的に揺らす。")]
        [SerializeField, Range(0.3f, 1.5f)] private float _treeHeightScaleMin = 0.75f;

        [Tooltip("木全体の高さ倍率の上限。幹半径・葉冠半径も同率で連動する。")]
        [SerializeField, Range(0.3f, 1.5f)] private float _treeHeightScaleMax = 1.25f;

        [Tooltip("幹に貼るテクスチャ。未設定なら茶色。")]
        [SerializeField] private Texture2D _trunkTexture;

        [SerializeField, Min(0.01f)] private float _trunkTextureTiling = 1f;

        [Tooltip("葉冠に貼るテクスチャ。未設定なら緑色。")]
        [SerializeField] private Texture2D _foliageTexture;

        [SerializeField, Min(0.01f)] private float _foliageTextureTiling = 1f;

        [Header("Tree Prefabs")]
        [Tooltip("木Prefabを10種類、安定した順番で割り当てる。未設定時は旧プロシージャル生成へフォールバックする。")]
        [SerializeField] private GameObject[] _treePrefabs;

        [Header("Rock Appearance")]
        [Tooltip("岩 1 個のベースサイズ（メートル、立方体の一辺）。ランダム揺らぎで ±20% 変動する。")]
        [SerializeField, Min(0.1f)] private float _rockSize = 2.6f;

        [Tooltip("岩の縦潰し比の下限。0.8 で高さ 80%、1.0 で立方体。")]
        [SerializeField, Range(0.3f, 1.0f)] private float _rockHeightScaleMin = 0.7f;

        [Tooltip("岩の縦潰し比の上限。0.95 で高さ 95%、1.0 で立方体。")]
        [SerializeField, Range(0.3f, 1.0f)] private float _rockHeightScaleMax = 0.95f;

        [Tooltip("岩キューブの全面に貼るテクスチャ。未設定なら灰色。")]
        [SerializeField] private Texture2D _rockTexture;

        [Tooltip("岩テクスチャのタイリング回数。1 なら各面に画像を1回表示する。")]
        [SerializeField, Min(0.01f)] private float _rockTextureTiling = 1f;

        [Header("Rock Prefabs")]
        [Tooltip("使用する岩Prefab 5種類（01・02・04・08・07）を割り当てる。未設定時は旧キューブ生成へフォールバックする。")]
        [SerializeField] private GameObject[] _rockPrefabs;

        [Tooltip("岩の底面をTerrainに埋める試作補正。XZ位置・回転・大きさは変更しない。")]
        [SerializeField] private bool _enableRockGrounding;

        [Header("Magic Stone Appearance")]
        [Tooltip("メイン魔石の高さ（メートル）。拠点扱いなのでかなり目立たせる。")]
        [SerializeField, Min(0.2f)] private float _mainStoneHeight = 3.2f;

        [Tooltip("魔石Prefab。未設定時はResourcesからRefinedMagicStoneを読み込む。")]
        [SerializeField] private GameObject _magicStonePrefab;

        /// <summary>魔石を地面から少し浮かせて「光っている結晶感」を出す量（メートル）。</summary>
        private const float MagicStoneFloatOffset = 0.05f;
        private const float RefinedMagicStoneModelHeight = 2.43f;
        private const string MagicStonePrefabResourcePath = "Combat/Map/RefinedMagicStone";
        private const string OwnMagicStoneMaterialResourcePath = "Combat/Map/MagicStoneCoreBlue";
        private const string EnemyMagicStoneMaterialResourcePath = "Combat/Map/MagicStoneCoreRed";
        private Transform _generatedRoot;

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
            _generatedRoot = root.transform;

            Material trunkMat = CreateLitTexturedMaterial(
                "AutoTreeTrunk", _trunkTexture, _trunkTextureTiling, new Color(0.36f, 0.22f, 0.11f));
            Material foliageMat = CreateLitTexturedMaterial(
                "AutoTreeFoliage", _foliageTexture, _foliageTextureTiling, new Color(0.12f, 0.50f, 0.18f));
            Material rockMat = _rockTexture != null
                ? CreateLitTexturedMaterial("AutoRock", _rockTexture, _rockTextureTiling, Color.white)
                : CreateLitMaterial("AutoRock", new Color(0.45f, 0.45f, 0.47f));
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
                        SpawnTree(
                            root.transform,
                            map,
                            f,
                            trunkMat,
                            foliageMat,
                            cylinderMesh,
                            sphereMesh,
                            treeIdx++,
                            i);
                        break;
                    case FeatureType.Rock:
                        SpawnRock(root.transform, map, f, rockMat, cubeMesh, rockIdx++, i);
                        break;
                    case FeatureType.OwnMainStone:
                        SpawnMagicStone(root.transform, f, "OwnMain", stoneIdx++, featureIndex: i);
                        break;
                    case FeatureType.EnemyMainStone:
                        SpawnMagicStone(root.transform, f, "EnemyMain", stoneIdx++, featureIndex: i);
                        break;
                }
            }

            if (_enableRockGrounding && rockIdx > 0)
                GroundRocks(root.transform, map.Height.CellSize, rockIdx);
        }

        private void GroundRocks(Transform root, float cellSize, int count)
        {
            Terrain terrain = GetComponent<TerrainRenderer>()?.Terrain;
            TerrainCollider ground = terrain != null ? terrain.GetComponent<TerrainCollider>() : null;
            if (ground == null || !ground.enabled || !ground.gameObject.activeInHierarchy)
            {
                Debug.LogWarning("[RockGrounding] TerrainColliderが取得できないため、岩の位置を保持します。", this);
                return;
            }

            Physics.SyncTransforms();
            for (int i = 0; i < count; i++)
            {
                Transform rock = root.Find($"Rock_{i}");
                if (!RockGrounding.TryGround(rock, transform, ground, cellSize, out string error))
                    Debug.LogWarning($"[RockGrounding] {rock.name}: {error}。位置を保持します。", rock);
            }
            Physics.SyncTransforms();
        }

        public void RefreshMagicStonePositions(MapData map)
        {
            TryRefreshMagicStonePositions(map);
        }

        public bool TryRefreshMagicStonePositions(MapData map)
        {
            if (map == null) return false;

            Transform root = _generatedRoot != null ? _generatedRoot : transform.Find(RootName);
            bool hasMagicStones = false;
            for (int i = 0; i < map.Features.Count; i++)
            {
                if (IsMagicStoneType(map.Features[i].Type))
                {
                    hasMagicStones = true;
                    break;
                }
            }

            if (!hasMagicStones) return true;
            if (root == null || !root.gameObject.activeInHierarchy) return false;

            MagicStone[] views = root.GetComponentsInChildren<MagicStone>(includeInactive: true);
            var viewsByFeatureIndex = new Dictionary<int, MagicStone>(views.Length);
            for (int i = 0; i < views.Length; i++)
            {
                MagicStone view = views[i];
                if (view != null && view.FeatureIndex >= 0)
                {
                    viewsByFeatureIndex[view.FeatureIndex] = view;
                }
            }

            for (int i = 0; i < map.Features.Count; i++)
            {
                PlacedFeature feature = map.Features[i];
                if (IsMagicStoneType(feature.Type) && !viewsByFeatureIndex.ContainsKey(i))
                {
                    return false;
                }
            }

            for (int i = 0; i < map.Features.Count; i++)
            {
                PlacedFeature feature = map.Features[i];
                if (!IsMagicStoneType(feature.Type) ||
                    !viewsByFeatureIndex.TryGetValue(i, out MagicStone view))
                {
                    continue;
                }
                float height = _mainStoneHeight;
                view.transform.localPosition = feature.WorldPosition +
                    new Vector3(0f, MagicStoneFloatOffset + height * 0.5f, 0f);
                view.transform.localRotation = feature.Rotation * Quaternion.Euler(0f, 45f, 0f);
            }

            return true;
        }

        private static bool IsHandledType(FeatureType t)
        {
            switch (t)
            {
                case FeatureType.Tree:
                case FeatureType.Rock:
                case FeatureType.OwnMainStone:
                case FeatureType.EnemyMainStone:
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsMagicStoneType(FeatureType type)
        {
            return type == FeatureType.OwnMainStone ||
                   type == FeatureType.EnemyMainStone;
        }

        public void Clear()
        {
            Transform existing = _generatedRoot != null ? _generatedRoot : transform.Find(RootName);
            _generatedRoot = null;
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

        /// <summary>
        /// 木Prefabを1本生成し、未設定時は旧プロシージャル木へフォールバックする。
        /// Y は <see cref="PlacedFeature.WorldPosition"/> を地面として扱い、根本をそこに合わせる。
        /// </summary>
        private void SpawnTree(
            Transform parent,
            MapData map,
            PlacedFeature f,
            Material trunkMat, Material foliageMat,
            Mesh cylinder, Mesh sphere, int idx, int featureIndex)
        {
            Quaternion rotation = GetTreeRotation(map.Seed, featureIndex, f);
            if (TrySpawnTreePrefab(parent, map, f, rotation, idx, featureIndex)) return;
            SpawnProceduralTree(parent, f, rotation, trunkMat, foliageMat, cylinder, sphere, idx);
        }

        private bool TrySpawnTreePrefab(
            Transform parent,
            MapData map,
            PlacedFeature f,
            Quaternion rotation,
            int idx,
            int featureIndex)
        {
            if (!HasValidTreePrefabSet())
            {
                if (!_treePrefabWarningLogged)
                {
                    string message =
                        $"[{nameof(FeatureRenderer)}] Exactly {TreePrefabCount} tree prefabs are required; " +
                        "falling back to procedural trees.";
                    if (Application.isBatchMode) Debug.LogWarning(message, this);
                    else Debug.LogError(message, this);
                    _treePrefabWarningLogged = true;
                }

                return false;
            }

            int variant = SelectTreeVariant(map.Seed, featureIndex, f.WorldPosition, _treePrefabs.Length);
            GameObject tree = Instantiate(_treePrefabs[variant], parent, worldPositionStays: false);
            tree.name = $"Tree_{idx}";
            tree.transform.localPosition = f.WorldPosition;
            tree.transform.localRotation = rotation;
            tree.transform.localScale = Vector3.one * (TreeSizeMultiplier * GetTreeHeightScale(f.WorldPosition));
            return true;
        }

        private void SpawnProceduralTree(
            Transform parent,
            PlacedFeature f,
            Quaternion rotation,
            Material trunkMat,
            Material foliageMat,
            Mesh cylinder,
            Mesh sphere,
            int idx)
        {
            var tree = new GameObject($"Tree_{idx}");
            tree.transform.SetParent(parent, worldPositionStays: false);
            SetVisionObstacleLayer(tree);
            // PlacedFeature の座標はマップローカル（親 MapSceneHost 基準）。ワールド直指定だと親が動いているときだけ地形とズレる。
            tree.transform.localPosition = f.WorldPosition;
            tree.transform.localRotation = rotation;

            // 位置ベースで決定的に高さを揺らす（再生成しても同じ木が同じ見た目になる）
            float scale = TreeSizeMultiplier * GetTreeHeightScale(f.WorldPosition);
            float treeHeight = _treeHeight * scale;
            float trunkRadius = _trunkRadius * scale;
            float foliageRadius = _foliageRadius * scale;

            // 木全体の高さを「幹 60% + 葉冠 40% だけ中心を押し上げる」で分ける。
            float trunkHeight = treeHeight * 0.6f;
            float foliageCenterY = trunkHeight + foliageRadius * 0.6f;

            // Unity のデフォルト Cylinder は Y 軸に沿って高さ 2m、半径 0.5m。
            // localScale.y = targetHeight / 2 で高さを、localScale.x/z = targetDiameter で太さを作る。
            var trunk = new GameObject("Trunk", typeof(MeshFilter), typeof(MeshRenderer), typeof(CapsuleCollider));
            trunk.transform.SetParent(tree.transform, worldPositionStays: false);
            SetVisionObstacleLayer(trunk);
            trunk.transform.localPosition = new Vector3(0f, trunkHeight * 0.5f, 0f);
            trunk.transform.localScale = new Vector3(trunkRadius * 2f, trunkHeight * 0.5f, trunkRadius * 2f);
            trunk.GetComponent<MeshFilter>().sharedMesh = cylinder;
            trunk.GetComponent<MeshRenderer>().sharedMaterial = trunkMat;
            var trunkCollider = trunk.GetComponent<CapsuleCollider>();
            trunkCollider.direction = 1; // Y axis
            trunkCollider.isTrigger = false;
            // Unity のデフォルト Sphere は直径 1m。localScale = diameter で好きなサイズに。
            var foliage = new GameObject("Foliage", typeof(MeshFilter), typeof(MeshRenderer), typeof(SphereCollider));
            foliage.transform.SetParent(tree.transform, worldPositionStays: false);
            SetVisionObstacleLayer(foliage);
            foliage.transform.localPosition = new Vector3(0f, foliageCenterY, 0f);
            float foliageDiameter = foliageRadius * 2f;
            foliage.transform.localScale = new Vector3(foliageDiameter, foliageDiameter, foliageDiameter);
            foliage.GetComponent<MeshFilter>().sharedMesh = sphere;
            foliage.GetComponent<MeshRenderer>().sharedMaterial = foliageMat;
            foliage.GetComponent<SphereCollider>().isTrigger = false;
            IgnoreFromNavMeshBuild(foliage);
        }

        private const int TreePrefabCount = 10;
        private bool _treePrefabWarningLogged;

        private bool HasValidTreePrefabSet()
        {
            if (_treePrefabs == null || _treePrefabs.Length != TreePrefabCount) return false;
            for (int i = 0; i < _treePrefabs.Length; i++)
            {
                if (_treePrefabs[i] == null) return false;
            }

            return true;
        }

        private float GetTreeHeightScale(Vector3 position)
        {
            uint seed = unchecked((uint)Mathf.FloorToInt(position.x * 41.3f + position.z * 97.1f + 11.7f));
            if (seed == 0u) seed = 1u;
            float hMin = Mathf.Min(_treeHeightScaleMin, _treeHeightScaleMax);
            float hMax = Mathf.Max(_treeHeightScaleMin, _treeHeightScaleMax);
            return Mathf.Lerp(hMin, hMax, NextFloat01(ref seed));
        }

        private static int SelectTreeVariant(int mapSeed, int featureIndex, Vector3 position, int count)
        {
            unchecked
            {
                uint hash = (uint)mapSeed;
                hash = hash * 16777619u ^ (uint)featureIndex;
                hash = hash * 16777619u ^ (uint)Mathf.RoundToInt(position.x * 100f);
                hash = hash * 16777619u ^ (uint)Mathf.RoundToInt(position.y * 100f);
                hash = hash * 16777619u ^ (uint)Mathf.RoundToInt(position.z * 100f);
                return (int)(hash % (uint)count);
            }
        }

        private static Quaternion GetTreeRotation(int mapSeed, int featureIndex, PlacedFeature feature)
        {
            unchecked
            {
                uint state = (uint)mapSeed ^ 0x9E3779B9u;
                state = state * 16777619u ^ (uint)featureIndex;
                state = state * 16777619u ^ (uint)Mathf.RoundToInt(feature.WorldPosition.x * 100f);
                state = state * 16777619u ^ (uint)Mathf.RoundToInt(feature.WorldPosition.y * 100f);
                state = state * 16777619u ^ (uint)Mathf.RoundToInt(feature.WorldPosition.z * 100f);
                if (state == 0u) state = 1u;
                float yaw = NextFloat01(ref state) * 360f;
                return feature.Rotation * Quaternion.Euler(0f, yaw, 0f);
            }
        }

        private static void SetVisionObstacleLayer(GameObject target)
        {
            int layer = LayerMask.NameToLayer(VisionObstacleLayerName);
            if (layer >= 0) target.layer = layer;
        }

        /// <summary>
        /// 岩Prefabを1個生成し、未設定時は旧キューブ生成へフォールバックする。
        /// </summary>
        private void SpawnRock(
            Transform parent,
            MapData map,
            PlacedFeature f,
            Material mat,
            Mesh cube,
            int idx,
            int featureIndex)
        {
            if (TrySpawnRockPrefab(parent, map, f, idx, featureIndex)) return;
            SpawnProceduralRock(parent, f, mat, cube, idx);
        }

        private bool TrySpawnRockPrefab(
            Transform parent,
            MapData map,
            PlacedFeature f,
            int idx,
            int featureIndex)
        {
            if (!HasValidRockPrefabSet())
            {
                if (!_rockPrefabWarningLogged)
                {
                    string message =
                        $"[{nameof(FeatureRenderer)}] Exactly {RockPrefabCount} rock prefabs are required; " +
                        "falling back to procedural rocks.";
                    if (Application.isBatchMode) Debug.LogWarning(message, this);
                    else Debug.LogError(message, this);
                    _rockPrefabWarningLogged = true;
                }

                return false;
            }

            uint state = GetRockRandomState(map.Seed, featureIndex, f.WorldPosition);
            int variant = (int)(state % (uint)_rockPrefabs.Length);
            float scale = _rockSize * RockSizeMultiplier * Mathf.Lerp(0.85f, 1.15f, NextFloat01(ref state));
            float yaw = NextFloat01(ref state) * 360f;

            GameObject rock = Instantiate(_rockPrefabs[variant], parent, worldPositionStays: false);
            rock.name = $"Rock_{idx}";
            rock.transform.localPosition = f.WorldPosition;
            rock.transform.localRotation = f.Rotation * Quaternion.Euler(0f, yaw, 0f);
            rock.transform.localScale = Vector3.one * scale;
            return true;
        }

        private void SpawnProceduralRock(Transform parent, PlacedFeature f, Material mat, Mesh cube, int idx)
        {
            var rock = new GameObject($"Rock_{idx}", typeof(MeshFilter), typeof(MeshRenderer), typeof(BoxCollider));
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
            float rockSize = _rockSize * RockSizeMultiplier;
            Vector3 pos = f.WorldPosition + new Vector3(0f, rockSize * sy * 0.5f, 0f);
            rock.transform.localPosition = pos;
            rock.transform.localRotation = f.Rotation * Quaternion.Euler(0f, yaw, 0f);
            rock.transform.localScale = new Vector3(rockSize * sx, rockSize * sy, rockSize * sz);
            rock.GetComponent<MeshFilter>().sharedMesh = cube;
            rock.GetComponent<MeshRenderer>().sharedMaterial = mat;
            rock.GetComponent<BoxCollider>().isTrigger = false;
            MarkNotWalkable(rock);
        }

        private const int RockPrefabCount = 5;
        private bool _rockPrefabWarningLogged;

        private bool HasValidRockPrefabSet()
        {
            if (_rockPrefabs == null || _rockPrefabs.Length != RockPrefabCount) return false;
            for (int i = 0; i < _rockPrefabs.Length; i++)
            {
                if (_rockPrefabs[i] == null) return false;
            }

            return true;
        }

        private static uint GetRockRandomState(int mapSeed, int featureIndex, Vector3 position)
        {
            unchecked
            {
                uint state = (uint)mapSeed ^ 0x85EBCA6Bu;
                state = state * 16777619u ^ (uint)featureIndex;
                state = state * 16777619u ^ (uint)Mathf.RoundToInt(position.x * 100f);
                state = state * 16777619u ^ (uint)Mathf.RoundToInt(position.y * 100f);
                state = state * 16777619u ^ (uint)Mathf.RoundToInt(position.z * 100f);
                return state == 0u ? 1u : state;
            }
        }

        /// <summary>
        /// 魔石Prefabを生成し、既存の位置・高さ契約と陣営色を適用する。
        /// </summary>
        private void SpawnMagicStone(
            Transform parent, PlacedFeature f, string label, int idx, int featureIndex)
        {
            float height = _mainStoneHeight;
            GameObject prefab = _magicStonePrefab != null
                ? _magicStonePrefab
                : Resources.Load<GameObject>(MagicStonePrefabResourcePath);
            if (prefab == null)
            {
                Debug.LogError($"Magic stone prefab was not found at Resources/{MagicStonePrefabResourcePath}.", this);
                return;
            }

            GameObject stone = Instantiate(prefab, parent, worldPositionStays: false);
            stone.name = $"{label}Stone_{idx}";
            Vector3 pos = f.WorldPosition + new Vector3(0f, MagicStoneFloatOffset + height * 0.5f, 0f);
            stone.transform.localPosition = pos;
            stone.transform.localRotation = f.Rotation * Quaternion.Euler(0f, 45f, 0f);
            stone.transform.localScale = Vector3.one * (height / RefinedMagicStoneModelHeight);

            BoxCollider collider = stone.GetComponent<BoxCollider>();
            if (collider == null) collider = stone.AddComponent<BoxCollider>();
            collider.isTrigger = false;
            MagicStone instance = stone.AddComponent<MagicStone>();
            instance.Setup(featureIndex, f.Type, height);
            ApplyMagicStoneTeamColor(stone, f.Type);
        }

        private static void ApplyMagicStoneTeamColor(GameObject stone, FeatureType type)
        {
            Transform core = FindChildByName(stone.transform, "Core");
            if (core == null) return;

            string resourcePath = type == FeatureType.OwnMainStone
                ? OwnMagicStoneMaterialResourcePath
                : EnemyMagicStoneMaterialResourcePath;
            Material material = Resources.Load<Material>(resourcePath);
            if (material == null)
            {
                Debug.LogError($"Magic stone core material was not found at Resources/{resourcePath}.");
                return;
            }

            Renderer[] renderers = core.GetComponentsInChildren<Renderer>(includeInactive: true);
            for (int i = 0; i < renderers.Length; i++)
                renderers[i].sharedMaterial = material;
        }

        private static Transform FindChildByName(Transform root, string name)
        {
            if (root.name == name || root.name.StartsWith(name + ".")) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform match = FindChildByName(root.GetChild(i), name);
                if (match != null) return match;
            }

            return null;
        }

        private static void IgnoreFromNavMeshBuild(GameObject go)
        {
            var navModifier = go.AddComponent<NavMeshModifier>();
            navModifier.ignoreFromBuild = true;
        }

        private static void MarkNotWalkable(GameObject go)
        {
            var navModifier = go.AddComponent<NavMeshModifier>();
            navModifier.overrideArea = true;
            navModifier.area = NavMesh.GetAreaFromName(NotWalkableAreaName);
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

        private static Material CreateLitTexturedMaterial(
            string name, Texture2D texture, float tiling, Color fallbackColor, Color? emission = null)
        {
            Material mat = CreateLitMaterial(name, texture != null ? Color.white : fallbackColor, emission);
            if (mat == null) return null;
            if (texture == null) return mat;

            Vector2 scale = Vector2.one * Mathf.Max(0.01f, tiling);
            if (mat.HasProperty("_BaseMap"))
            {
                mat.SetTexture("_BaseMap", texture);
                mat.SetTextureScale("_BaseMap", scale);
            }
            if (mat.HasProperty("_MainTex"))
            {
                mat.SetTexture("_MainTex", texture);
                mat.SetTextureScale("_MainTex", scale);
            }
            return mat;
        }

        /// <summary>xorshift32 ベースの軽量 PRNG。木と岩の揺らぎを決定的に作るために使う。</summary>
        private static float NextFloat01(ref uint state)
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return (state & 0x00FFFFFFu) / (float)0x01000000;
        }
    }
}
