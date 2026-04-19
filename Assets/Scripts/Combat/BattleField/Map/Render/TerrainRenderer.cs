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

        [SerializeField, HideInInspector] private Terrain _terrain;

        /// <summary>
        /// スプラットマップで使うレイヤー順。index はそのまま alphamap のチャンネルになる。
        /// Water レイヤもあるが、実戦時は水面メッシュで覆う想定。
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
        private const int TotalLayerCount = 5;

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

            float[,] heights = new float[res, res];
            float resToSrc = (float)(h.Width - 1) / (res - 1);
            for (int z = 0; z < res; z++)
            {
                int hz = Mathf.Clamp(Mathf.RoundToInt(z * resToSrc), 0, h.Height - 1);
                for (int x = 0; x < res; x++)
                {
                    int hx = Mathf.Clamp(Mathf.RoundToInt(x * resToSrc), 0, h.Width - 1);
                    heights[z, x] = (h.GetHeight(hx, hz) - min) / range;
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
            for (int z = 0; z < res; z++)
            {
                float worldZ = (z + 0.5f) / res * worldSize;
                for (int x = 0; x < res; x++)
                {
                    float worldX = (x + 0.5f) / res * worldSize;
                    GroundState s = g.SampleAt(new Vector3(worldX, 0f, worldZ));

                    // Normal セルが森ゾーンに入っているなら「森の地面」レイヤに塗り替える。
                    // Water / Swamp / Snow はそれぞれ優先してその状態で見せる。
                    if (s == GroundState.Normal && regionCount > 0 &&
                        IsInsideAnyForest(regions, worldX, worldZ))
                    {
                        alphas[z, x, ForestFloorLayerIndex] = 1f;
                    }
                    else
                    {
                        int layerIdx = IndexOfLayer(s);
                        alphas[z, x, layerIdx] = 1f;
                    }
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
        private static TerrainLayer[] BuildOrReuseLayers(TerrainData td)
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
                if (allValid) return existing;
            }

            var layers = new TerrainLayer[TotalLayerCount];
            for (int i = 0; i < s_layerOrder.Length; i++)
            {
                layers[i] = CreateSolidColorLayer(s_layerOrder[i]);
            }
            layers[ForestFloorLayerIndex] = CreateSolidColorLayer("ForestFloor", new Color(0.14f, 0.42f, 0.17f));
            return layers;
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
        /// </summary>
        private static Color GetColorForState(GroundState state) => state switch
        {
            GroundState.Normal => new Color(0.60f, 0.80f, 0.40f),
            GroundState.Swamp => new Color(0.30f, 0.35f, 0.20f),
            GroundState.Snow => new Color(0.95f, 0.95f, 0.95f),
            GroundState.Water => new Color(0.20f, 0.50f, 0.95f),
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
