using UnityEngine;

namespace WarSimulation.Combat.Map
{
    /// <summary>
    /// MapData を受け取り、Unity 標準 Terrain に反映する可視化コンポーネント。
    /// HeightMap を高さに、GroundStateGrid をスプラットマップ（地面状態別の色）に変換する。
    ///
    /// URP プロジェクトでは Terrain のデフォルトマテリアルが URP シェーダーでないと
    /// マゼンタ表示になるため、"Universal Render Pipeline/Terrain/Lit" があれば自動割当する。
    ///
    /// 所有モデル：
    ///   このコンポーネントが付いた GameObject の下に「GeneratedTerrain」子を 1 つ持ち、
    ///   Render() 呼び出しのたびに TerrainData とレイヤーを更新する。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TerrainRenderer : MonoBehaviour
    {
        private const string GeneratedTerrainName = "GeneratedTerrain";

        [Tooltip("MapData の高度が全てこの値を下回ると、安全のため最小範囲をこの値に拡張する。" +
                 "平坦マップで Terrain.size.y が 0 になるのを防ぐ。")]
        [SerializeField, Min(0.01f)] private float _minHeightRange = 0.5f;

        [Tooltip("スプラットマップの解像度。未指定（0）なら GroundStateGrid の解像度に合わせる。")]
        [SerializeField, Min(0)] private int _alphamapResolutionOverride = 0;

        [Tooltip("バイオーム未設定の通常地面に貼る草テクスチャ。未設定なら単色の通常地面を使う。")]
        [SerializeField] private Texture2D _grassTexture;

        [Tooltip("草テクスチャ1枚を貼るワールド寸法（メートル）。")]
        [SerializeField, Min(0.01f)] private float _grassTileSize = 4f;

        [Tooltip("雪地面に貼るテクスチャ。未設定なら白色の雪地面を使う。")]
        [SerializeField] private Texture2D _snowTexture;

        [Tooltip("雪テクスチャ1枚を貼るワールド寸法（メートル）。")]
        [SerializeField, Min(0.01f)] private float _snowTileSize = 4f;

        [Tooltip("沼地に貼るテクスチャ。未設定なら単色の沼地を使う。")]
        [SerializeField] private Texture2D _swampTexture;

        [Tooltip("沼地テクスチャ1枚を貼るワールド寸法（メートル）。")]
        [SerializeField, Min(0.01f)] private float _swampTileSize = 4f;

        [Tooltip("川・湖・凍結湖の地面下地に貼るテクスチャ。川は掘削全幅に塗り、Water タグ幅とは独立。水面メッシュ自体の見た目は変更しない。")]
        [SerializeField] private Texture2D _waterGroundTexture;

        [Tooltip("水地面テクスチャ1枚を貼るワールド寸法（メートル）。")]
        [SerializeField, Min(0.01f)] private float _waterGroundTileSize = 4f;

        [Tooltip("森床に貼るテクスチャ。未設定なら単色の森床を使う。")]
        [SerializeField] private Texture2D _forestFloorTexture;

        [Tooltip("森床テクスチャ1枚を貼るワールド寸法（メートル）。")]
        [SerializeField, Min(0.01f)] private float _forestFloorTileSize = 4f;

        [Tooltip("崖面に貼るテクスチャ。未設定なら茶色の崖面を使う。")]
        [SerializeField] private Texture2D _cliffTexture;

        [Tooltip("崖面テクスチャ1枚を貼るワールド寸法（メートル）。")]
        [SerializeField, Min(0.01f)] private float _cliffTileSize = 4f;

        [SerializeField, HideInInspector] private Terrain _terrain;

        /// <summary>
        /// スプラットマップで使うレイヤー順。index はそのまま alphamap のチャンネルになる。
        /// Water は水面メッシュの下地として Terrain にも塗る。
        /// </summary>
        private static readonly GroundState[] s_layerOrder =
        {
            GroundState.Normal,
            GroundState.Swamp,
            GroundState.Snow,
            GroundState.Water,
        };

        /// <summary>
        /// 森ゾーン（<see cref="MapData.ForestRegions"/>）用の追加レイヤ。
        /// GroundState には含めない（木はオブジェクトであって地面状態ではない）が、
        /// 可視化ではプレイヤーに「ここは森」と分かる独自色で塗り分けたいため Terrain 側だけで追加する。
        /// </summary>
        private const int ForestFloorLayerIndex = 4;

        /// <summary>
        /// <see cref="HeightMap.CliffFaces"/>（スタンプの崖スカートと一致。勾配推定は使わない）。
        /// </summary>
        private const int CliffLayerIndex = 5;

        /// <summary>バイオーム未設定の通常地面用の草レイヤ。</summary>
        private const int GrassLayerIndex = 6;
        private const int TotalLayerCount = 7;

        public Terrain Terrain => _terrain;

        public void Render(MapData map)
        {
            if (map == null)
            {
                Debug.LogWarning($"[{nameof(TerrainRenderer)}] Render called with null MapData.");
                return;
            }

            EnsureTerrain();
            ApplyHeightMap(map);
            ApplySplatMap(map);
        }

        public void Clear()
        {
            if (_terrain == null) return;
            if (Application.isPlaying) Destroy(_terrain.gameObject);
            else DestroyImmediate(_terrain.gameObject);
            _terrain = null;
        }

        private void EnsureTerrain()
        {
            if (_terrain != null) return;

            var existing = transform.Find(GeneratedTerrainName);
            if (existing != null)
            {
                _terrain = existing.GetComponent<Terrain>();
                if (_terrain != null) return;
            }

            var go = new GameObject(GeneratedTerrainName, typeof(Terrain), typeof(TerrainCollider));
            go.transform.SetParent(transform, worldPositionStays: false);

            var terrain = go.GetComponent<Terrain>();
            var td = new TerrainData { name = "GeneratedTerrainData" };
            terrain.terrainData = td;

            var collider = go.GetComponent<TerrainCollider>();
            collider.terrainData = td;

            // URP を使っている場合は URP の Terrain シェーダーをバインドしないとマゼンタになる。
            AssignTerrainMaterial(terrain);

            _terrain = terrain;
        }

        private static void AssignTerrainMaterial(Terrain terrain)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Terrain/Lit");
            if (shader == null) shader = Shader.Find("Nature/Terrain/Standard");
            if (shader == null) return;

            var mat = new Material(shader) { name = "GeneratedTerrainMaterial" };
            terrain.materialTemplate = mat;
        }

        private void ApplyHeightMap(MapData map)
        {
            HeightMap h = map.Height;
            TerrainData td = _terrain.terrainData;

            GetHeightRange(h, out float min, out float max);
            float range = Mathf.Max(_minHeightRange, max - min);

            td.heightmapResolution = h.Width;
            int res = td.heightmapResolution;

            float worldSize = h.WorldSize.x;
            td.size = new Vector3(worldSize, range, worldSize);

            // Unity が heightmapResolution を許容値に変えると res ≠ h.Width になりうる。
            // 近傍セル1点だけだと平板が伸びて段差が強く見えるので、ワールド位置で SampleAt（バイリニア）する。
            float[,] heights = new float[res, res];
            float denom = res > 1 ? res - 1 : 1f;
            for (int z = 0; z < res; z++)
            {
                float worldZ = z / denom * worldSize;
                for (int x = 0; x < res; x++)
                {
                    float worldX = x / denom * worldSize;
                    float height = h.SampleAt(new Vector3(worldX, 0f, worldZ));
                    heights[z, x] = (height - min) / range;
                }
            }
            td.SetHeights(0, 0, heights);

            _terrain.transform.localPosition = new Vector3(0f, min, 0f);
            _terrain.transform.localRotation = Quaternion.identity;
            _terrain.transform.localScale = Vector3.one;
        }

        private void ApplySplatMap(MapData map)
        {
            TerrainData td = _terrain.terrainData;
            GroundStateGrid g = map.GroundStates;

            TerrainLayer[] layers = BuildOrReuseLayers(td);
            td.terrainLayers = layers;

            int alphaRes = _alphamapResolutionOverride > 0
                ? _alphamapResolutionOverride
                : Mathf.Max(32, g.Width);
            td.alphamapResolution = alphaRes;
            int res = td.alphamapResolution;

            int layerCount = layers.Length;
            float[,,] alphas = new float[res, res, layerCount];

            // ForestRegions は List なので配列化しておくとタイトループで有利
            var regions = map.ForestRegions;
            int regionCount = regions?.Count ?? 0;

            float worldSize = g.WorldSize.x;
            CliffFaceGrid cliffs = map.Height.CliffFaces;
            for (int z = 0; z < res; z++)
            {
                int cellZ = Mathf.Clamp(Mathf.FloorToInt((z + 0.5f) * g.Height / (float)res), 0, g.Height - 1);
                float worldZ = (cellZ + 0.5f) * g.CellSize;
                for (int x = 0; x < res; x++)
                {
                    int cellX = Mathf.Clamp(Mathf.FloorToInt((x + 0.5f) * g.Width / (float)res), 0, g.Width - 1);
                    float worldX = (cellX + 0.5f) * g.CellSize;
                    Vector3 worldPos = new Vector3(worldX, 0f, worldZ);
                    GroundState sampledState = g.GetCell(cellX, cellZ);
                    GroundState s = sampledState;

                    // 川床の土テクスチャは掘削全幅（WidthMeters）。Water タグ幅（WaterTagRatio）とは独立。
                    if (RiverCorridorUtility.Contains(map, new Vector2(worldX, worldZ)))
                    {
                        alphas[z, x, IndexOfLayer(GroundState.Water)] = 1f;
                        continue;
                    }

                    // Water / Swamp / Snow は地面状態を優先（湖など）。
                    if (s == GroundState.Water || s == GroundState.Swamp || s == GroundState.Snow)
                    {
                        alphas[z, x, IndexOfLayer(s)] = 1f;
                        continue;
                    }

                    bool inForest = s == GroundState.Normal && regionCount > 0 &&
                        IsInsideAnyForest(regions, worldX, worldZ);
                    if (inForest)
                    {
                        alphas[z, x, ForestFloorLayerIndex] = 1f;
                        continue;
                    }

                    if (s == GroundState.Normal && cliffs.SampleAt(worldPos))
                    {
                        alphas[z, x, CliffLayerIndex] = 1f;
                        continue;
                    }

                    bool hasNoBiome = map.GetBiomeId(cellX, cellZ) == MapData.UnsetBiomeId;
                    if (sampledState == GroundState.Normal && hasNoBiome && _grassTexture != null)
                    {
                        alphas[z, x, GrassLayerIndex] = 1f;
                        continue;
                    }

                    alphas[z, x, IndexOfLayer(s)] = 1f;
                }
            }
            td.SetAlphamaps(0, 0, alphas);
        }

        private static bool IsInsideAnyForest(System.Collections.Generic.List<ForestRegion> regions, float x, float z)
        {
            // Contains 側がノイズ歪みを考慮するので、呼び出し側は素直に渡すだけでよい。
            Vector2 p = new Vector2(x, z);
            for (int i = 0; i < regions.Count; i++)
            {
                if (regions[i].Contains(p)) return true;
            }
            return false;
        }

        /// <summary>
        /// TerrainData が既に同じ数のレイヤーを持っていればそのまま使い、
        /// 違う場合は地面状態毎に単色レイヤーを新規生成する。
        /// </summary>
        private TerrainLayer[] BuildOrReuseLayers(TerrainData td)
        {
            TerrainLayer[] existing = td.terrainLayers;
            if (existing != null && existing.Length == TotalLayerCount)
            {
                bool allValid = true;
                for (int i = 0; i < existing.Length; i++)
                {
                    if (existing[i] == null || existing[i].diffuseTexture == null)
                    {
                        allValid = false;
                        break;
                    }
                }
                if (allValid)
                {
                    ConfigureGrassLayer(existing[GrassLayerIndex]);
                    ConfigureWaterGroundLayer(existing[IndexOfLayer(GroundState.Water)]);
                    ConfigureSnowLayer(existing[IndexOfLayer(GroundState.Snow)]);
                    ConfigureSwampLayer(existing[IndexOfLayer(GroundState.Swamp)]);
                    ConfigureForestFloorLayer(existing[ForestFloorLayerIndex]);
                    ConfigureCliffLayer(existing[CliffLayerIndex]);
                    return existing;
                }
            }

            var layers = new TerrainLayer[TotalLayerCount];
            for (int i = 0; i < s_layerOrder.Length; i++)
            {
                layers[i] = CreateSolidColorLayer(s_layerOrder[i]);
            }
            ConfigureWaterGroundLayer(layers[IndexOfLayer(GroundState.Water)]);
            ConfigureSnowLayer(layers[IndexOfLayer(GroundState.Snow)]);
            ConfigureSwampLayer(layers[IndexOfLayer(GroundState.Swamp)]);
            layers[ForestFloorLayerIndex] = CreateSolidColorLayer("ForestFloor", new Color(0.14f, 0.42f, 0.17f));
            ConfigureForestFloorLayer(layers[ForestFloorLayerIndex]);
            layers[CliffLayerIndex] = CreateSolidColorLayer("Cliff", new Color(0.30f, 0.18f, 0.10f));
            ConfigureCliffLayer(layers[CliffLayerIndex]);
            layers[GrassLayerIndex] = CreateGrassLayer();
            return layers;
        }

        private TerrainLayer CreateGrassLayer()
        {
            TerrainLayer layer = CreateSolidColorLayer(GroundState.Normal);
            layer.name = "Auto_Grass";
            ConfigureGrassLayer(layer);
            return layer;
        }

        private void ConfigureGrassLayer(TerrainLayer layer)
        {
            if (_grassTexture != null)
            {
                layer.diffuseTexture = _grassTexture;
            }
            layer.tileSize = Vector2.one * _grassTileSize;
            layer.tileOffset = Vector2.zero;
        }

        private void ConfigureSnowLayer(TerrainLayer layer)
        {
            if (_snowTexture != null)
            {
                layer.diffuseTexture = _snowTexture;
            }
            layer.tileSize = Vector2.one * _snowTileSize;
            layer.tileOffset = Vector2.zero;
        }

        private void ConfigureSwampLayer(TerrainLayer layer)
        {
            if (_swampTexture != null)
            {
                layer.diffuseTexture = _swampTexture;
            }
            layer.tileSize = Vector2.one * _swampTileSize;
            layer.tileOffset = Vector2.zero;
        }

        private void ConfigureWaterGroundLayer(TerrainLayer layer)
        {
            if (_waterGroundTexture != null)
            {
                layer.diffuseTexture = _waterGroundTexture;
            }
            layer.tileSize = Vector2.one * _waterGroundTileSize;
            layer.tileOffset = Vector2.zero;
        }

        private void ConfigureForestFloorLayer(TerrainLayer layer)
        {
            if (_forestFloorTexture != null)
            {
                layer.diffuseTexture = _forestFloorTexture;
            }
            layer.tileSize = Vector2.one * _forestFloorTileSize;
            layer.tileOffset = Vector2.zero;
        }

        private void ConfigureCliffLayer(TerrainLayer layer)
        {
            if (_cliffTexture != null)
            {
                layer.diffuseTexture = _cliffTexture;
            }
            layer.tileSize = Vector2.one * _cliffTileSize;
            layer.tileOffset = Vector2.zero;
        }

        private static int IndexOfLayer(GroundState state)
        {
            for (int i = 0; i < s_layerOrder.Length; i++)
            {
                if (s_layerOrder[i] == state) return i;
            }
            return 0;
        }

        private static TerrainLayer CreateSolidColorLayer(GroundState state)
        {
            return CreateSolidColorLayer(state.ToString(), GetColorForState(state));
        }

        private static TerrainLayer CreateSolidColorLayer(string label, Color color)
        {
            Texture2D tex = CreateSolidTexture(color);
            var layer = new TerrainLayer
            {
                name = $"Auto_{label}",
                diffuseTexture = tex,
                tileSize = new Vector2(4f, 4f),
                tileOffset = Vector2.zero,
            };
            return layer;
        }

        private static Texture2D CreateSolidTexture(Color color)
        {
            // 最小 4x4：マイクロストライプを避けつつモバイル対応サイズ
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, mipChain: false)
            {
                name = "AutoSolidTex",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
            };
            var pixels = new Color[16];
            for (int i = 0; i < 16; i++) pixels[i] = color;
            tex.SetPixels(pixels);
            tex.Apply(updateMipmaps: false);
            return tex;
        }

        /// <summary>
        /// エディタプレビューと同じカラーパレットを採用して 2D / 3D の見た目をそろえる。
        /// Water タグは水面メッシュ下の地面として別レイヤで塗る。
        /// 川については <see cref="RiverCorridorUtility"/>（掘削全幅）でも同じレイヤを使う。
        /// </summary>
        private static Color GetColorForState(GroundState state) => state switch
        {
            GroundState.Normal => new Color(0.60f, 0.80f, 0.40f),
            GroundState.Swamp => new Color(0.30f, 0.35f, 0.20f),
            GroundState.Snow => new Color(0.95f, 0.95f, 0.95f),
            GroundState.Water => new Color(0.42f, 0.33f, 0.24f),
            _ => new Color(1f, 0f, 1f),
        };

        private static void GetHeightRange(HeightMap h, out float min, out float max)
        {
            min = float.PositiveInfinity;
            max = float.NegativeInfinity;
            for (int z = 0; z < h.Height; z++)
            {
                for (int x = 0; x < h.Width; x++)
                {
                    float v = h.GetHeight(x, z);
                    if (v < min) min = v;
                    if (v > max) max = v;
                }
            }
            if (float.IsInfinity(min) || float.IsInfinity(max))
            {
                min = 0f;
                max = 0f;
            }
        }
    }
}
