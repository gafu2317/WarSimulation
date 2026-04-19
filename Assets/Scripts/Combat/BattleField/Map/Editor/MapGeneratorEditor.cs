#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace WarSimulation.Combat.Map.EditorOnly
{
    /// <summary>
    /// MapGenerator の Inspector に「Generate Preview」ボタンと可視化テクスチャを追加する。
    /// Play モードに入らずに生成結果を目視確認するための開発用ツール。
    /// </summary>
    [CustomEditor(typeof(MapGenerator))]
    public sealed class MapGeneratorEditor : Editor
    {
        private const int PreviewDisplaySize = 256;

        private Texture2D _heightTex;
        private Texture2D _terrainTex;
        private string _lastInfo;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Generate Preview", GUILayout.Height(26)))
                {
                    GeneratePreview();
                }
                if (GUILayout.Button("Clear", GUILayout.Height(26), GUILayout.Width(80)))
                {
                    ClearTextures();
                    _lastInfo = null;
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Generate + Render 3D", GUILayout.Height(26)))
                {
                    GenerateAndRender3D();
                }
                if (GUILayout.Button("Clear 3D", GUILayout.Height(26), GUILayout.Width(80)))
                {
                    Clear3D();
                }
            }

            if (!string.IsNullOrEmpty(_lastInfo))
            {
                EditorGUILayout.HelpBox(_lastInfo, MessageType.Info);
            }

            DrawPreview("Height Map (color ramp)", _heightTex);
            DrawPreview("Terrain Grid", _terrainTex);
        }

        private void OnDisable() => ClearTextures();

        private void GeneratePreview()
        {
            var gen = (MapGenerator)target;
            MapData data = gen.Generate();
            if (data == null)
            {
                _lastInfo = "Generate() が null を返しました。Config が未設定の可能性があります。";
                return;
            }

            ClearTextures();
            _heightTex = BuildHeightTexture(data, out float min, out float max);
            _terrainTex = BuildTerrainTexture(data.GroundStates);

            OverlayForestRegions(_heightTex, data, data.Height.CellSize);
            OverlayForestRegions(_terrainTex, data, data.GroundStates.CellSize);
            OverlayFeatures(_heightTex, data, data.Height.CellSize);
            OverlayFeatures(_terrainTex, data, data.GroundStates.CellSize);

            _lastInfo =
                $"Seed: {data.Seed}\n" +
                $"HeightMap: {data.Height.Width}x{data.Height.Height}  (cell={data.Height.CellSize:F3} m)\n" +
                $"GroundStateGrid: {data.GroundStates.Width}x{data.GroundStates.Height}  (cell={data.GroundStates.CellSize:F3} m)\n" +
                $"Height range: {min:F2} .. {max:F2}\n" +
                ConfigSummary(gen.Config) + "\n" +
                MagicStonesSummary(data) + "\n" +
                $"Forests: {data.ForestRegions.Count}, Trees: {CountFeatures(data, FeatureType.Tree)}, Rocks: {CountFeatures(data, FeatureType.Rock)}";
        }

        private void GenerateAndRender3D()
        {
            var gen = (MapGenerator)target;
            MapData data = gen.Generate();
            if (data == null)
            {
                _lastInfo = "Generate() が null を返しました。Config が未設定の可能性があります。";
                return;
            }

            var terrainRenderer = gen.GetComponent<TerrainRenderer>();
            if (terrainRenderer == null)
            {
                terrainRenderer = Undo.AddComponent<TerrainRenderer>(gen.gameObject);
            }
            terrainRenderer.Render(data);

            var riverRenderer = gen.GetComponent<RiverRenderer>();
            if (riverRenderer == null)
            {
                riverRenderer = Undo.AddComponent<RiverRenderer>(gen.gameObject);
            }
            riverRenderer.Render(data);

            var lakeRenderer = gen.GetComponent<LakeRenderer>();
            if (lakeRenderer == null)
            {
                lakeRenderer = Undo.AddComponent<LakeRenderer>(gen.gameObject);
            }
            lakeRenderer.Render(data);

            var bridgeRenderer = gen.GetComponent<BridgeRenderer>();
            if (bridgeRenderer == null)
            {
                bridgeRenderer = Undo.AddComponent<BridgeRenderer>(gen.gameObject);
            }
            bridgeRenderer.Render(data, gen.Config);

            var featureRenderer = gen.GetComponent<FeatureRenderer>();
            if (featureRenderer == null)
            {
                featureRenderer = Undo.AddComponent<FeatureRenderer>(gen.gameObject);
            }
            featureRenderer.Render(data);

            ClearTextures();
            _heightTex = BuildHeightTexture(data, out float min, out float max);
            _terrainTex = BuildTerrainTexture(data.GroundStates);
            OverlayForestRegions(_heightTex, data, data.Height.CellSize);
            OverlayForestRegions(_terrainTex, data, data.GroundStates.CellSize);
            OverlayFeatures(_heightTex, data, data.Height.CellSize);
            OverlayFeatures(_terrainTex, data, data.GroundStates.CellSize);

            _lastInfo =
                $"[3D Rendered] Seed: {data.Seed}\n" +
                $"Height range: {min:F2} .. {max:F2}\n" +
                ConfigSummary(gen.Config) + "\n" +
                MagicStonesSummary(data) + "\n" +
                $"Forests: {data.ForestRegions.Count}, Trees: {CountFeatures(data, FeatureType.Tree)}, Rocks: {CountFeatures(data, FeatureType.Rock)}\n" +
                $"Rivers: {data.Rivers.Count}\n" +
                $"Lakes: {data.Lakes.Count} (Frozen: {CountFrozenLakes(data)})\n" +
                $"Bridges: {CountFeatures(data, FeatureType.Bridge)}";
        }

        private void Clear3D()
        {
            var gen = (MapGenerator)target;
            var terrainRenderer = gen.GetComponent<TerrainRenderer>();
            if (terrainRenderer != null) terrainRenderer.Clear();
            var riverRenderer = gen.GetComponent<RiverRenderer>();
            if (riverRenderer != null) riverRenderer.Clear();
            var lakeRenderer = gen.GetComponent<LakeRenderer>();
            if (lakeRenderer != null) lakeRenderer.Clear();
            var bridgeRenderer = gen.GetComponent<BridgeRenderer>();
            if (bridgeRenderer != null) bridgeRenderer.Clear();
            var featureRenderer = gen.GetComponent<FeatureRenderer>();
            if (featureRenderer != null) featureRenderer.Clear();
        }

        private static int CountFeatures(MapData map, FeatureType type)
        {
            int n = 0;
            var features = map.Features;
            for (int i = 0; i < features.Count; i++)
            {
                if (features[i].Type == type) n++;
            }
            return n;
        }

        /// <summary>
        /// 現在 MapGenerator が参照している Config の主要値をダンプする。
        /// Asset を編集しても Unity が再インポートしていない／Generator が別の Config を見ている、
        /// といったケースをユーザーが一目で切り分けられるように出す。
        /// </summary>
        private static string ConfigSummary(MapGenerationConfig config)
        {
            if (config == null) return "Config: (null)";
            int stampList = config.StructureStamps != null ? config.StructureStamps.Count : 0;
            int forestList = config.ForestClusterStamps != null ? config.ForestClusterStamps.Count : 0;
            int groundPatchList = config.GroundPatchStamps != null ? config.GroundPatchStamps.Count : 0;
            int cliffStamps = 0;
            if (config.StructureStamps != null)
            {
                foreach (var s in config.StructureStamps)
                {
                    if (s != null && s.CliffArcDeg > 0f) cliffStamps++;
                }
            }
            return
                $"Config[Structures]: Count={config.StructureStampCount}, StampListSize={stampList}, CliffStamps={cliffStamps}\n" +
                $"Config[Forest]    : Count={config.ForestClusterCount}, StampListSize={forestList}\n" +
                $"Config[GroundPatch]: Count={config.GroundPatchStampCount}, StampListSize={groundPatchList}\n" +
                $"Config[Rock]      : Count={config.RockCount}\n" +
                $"Config[Bridge]    : PerRiver={config.BridgesPerRiver}, Rivers={config.CrossMapRiverCount}\n" +
                $"Config[Lake]      : Count={config.LakeCount}, FreezeProb={config.LakeFreezeProbability:F2}\n" +
                $"Config[Climb]     : MaxSlopeDeg={config.MaxClimbableSlopeDeg:F1}";
        }

        private static int CountFrozenLakes(MapData map)
        {
            if (map == null) return 0;
            int n = 0;
            for (int i = 0; i < map.Lakes.Count; i++) if (map.Lakes[i].IsFrozen) n++;
            return n;
        }

        private static string MagicStonesSummary(MapData map)
        {
            int ownMain = CountFeatures(map, FeatureType.OwnMainStone);
            int ownSub = CountFeatures(map, FeatureType.OwnSubStone);
            int enemyMain = CountFeatures(map, FeatureType.EnemyMainStone);
            int enemySub = CountFeatures(map, FeatureType.EnemySubStone);
            return
                $"Magic Stones (Own)   main={ownMain}, sub={ownSub}\n" +
                $"Magic Stones (Enemy) main={enemyMain}, sub={enemySub}";
        }

        private void ClearTextures()
        {
            if (_heightTex != null) { DestroyImmediate(_heightTex); _heightTex = null; }
            if (_terrainTex != null) { DestroyImmediate(_terrainTex); _terrainTex = null; }
        }

        private static void DrawPreview(string label, Texture2D tex)
        {
            if (tex == null) return;
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            Rect r = GUILayoutUtility.GetRect(PreviewDisplaySize, PreviewDisplaySize, GUILayout.ExpandWidth(false));
            EditorGUI.DrawPreviewTexture(r, tex);
        }

        private static Texture2D BuildHeightTexture(MapData map, out float min, out float max)
        {
            HeightMap h = map.Height;
            GroundStateGrid g = map.GroundStates;

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
            var tex = new Texture2D(h.Width, h.Height, TextureFormat.RGB24, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };

            var pixels = new Color32[h.Width * h.Height];
            float hmCell = h.CellSize;
            float gCell = g.CellSize;
            Color32 waterColor = new Color(0.20f, 0.50f, 0.95f);

            for (int z = 0; z < h.Height; z++)
            {
                for (int x = 0; x < h.Width; x++)
                {
                    float worldX = (x + 0.5f) * hmCell;
                    float worldZ = (z + 0.5f) * hmCell;
                    int gx = Mathf.Clamp(Mathf.FloorToInt(worldX / gCell), 0, g.Width - 1);
                    int gy = Mathf.Clamp(Mathf.FloorToInt(worldZ / gCell), 0, g.Height - 1);

                    Color32 color = g.GetCell(gx, gy) == GroundState.Water
                        ? waterColor
                        : HeightColorRamp(h.GetHeight(x, z), min, max);

                    pixels[z * h.Width + x] = color;
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false);
            return tex;
        }

        private static Texture2D BuildTerrainTexture(GroundStateGrid g)
        {
            var tex = new Texture2D(g.Width, g.Height, TextureFormat.RGB24, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };

            var pixels = new Color32[g.Width * g.Height];
            for (int z = 0; z < g.Height; z++)
            {
                for (int x = 0; x < g.Width; x++)
                {
                    pixels[z * g.Width + x] = GroundStateColor(g.GetCell(x, z));
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false);
            return tex;
        }

        /// <summary>
        /// 高度を「紫（低）→ 緑（海抜 0）→ 黄 → 赤（高）」のカラーランプに落とし込む。
        /// 将来実装する川の「青」と区別できるよう、低地を紫に割り当てている。
        /// 緑を常に h = 0 にピン留めすることで、どのマップでも「0 = 基準地面」として読める。
        /// </summary>
        private static Color32 HeightColorRamp(float h, float min, float max)
        {
            var cLow = new Color(0.50f, 0.20f, 0.70f); // purple
            var cMid = new Color(0.40f, 0.75f, 0.35f); // green
            var cHi1 = new Color(0.95f, 0.90f, 0.30f); // yellow
            var cHi2 = new Color(0.85f, 0.20f, 0.20f); // red

            if (h < 0f)
            {
                float depth = -Mathf.Min(0f, min);
                float t = depth > 1e-5f ? Mathf.Clamp01(1f + h / depth) : 1f;
                return Color.Lerp(cLow, cMid, t);
            }

            float peak = Mathf.Max(0f, max);
            float u = peak > 1e-5f ? Mathf.Clamp01(h / peak) : 0f;
            return u < 0.5f
                ? Color.Lerp(cMid, cHi1, u / 0.5f)
                : Color.Lerp(cHi1, cHi2, (u - 0.5f) / 0.5f);
        }

        /// <summary>
        /// <see cref="MapData.ForestRegions"/> の各円の内側を、森らしい深緑で半透明に塗る。
        /// 木（<see cref="FeatureType.Tree"/>）のドットより先に呼び出して、ドットが上に乗るようにする。
        /// </summary>
        private static void OverlayForestRegions(Texture2D tex, MapData map, float cellSize)
        {
            if (tex == null || map == null) return;
            var regions = map.ForestRegions;
            if (regions == null || regions.Count == 0) return;

            // 地面色との視認差が出るよう、少しくすんだ濃緑にブレンド
            Color forestTint = new Color(0.10f, 0.45f, 0.18f);
            float blend = 0.55f;

            for (int i = 0; i < regions.Count; i++)
            {
                ForestRegion region = regions[i];
                Vector2 center = region.Center;

                // ノイズで膨らんだ最大外径をバウンディングに使う
                float outer = region.OuterRadius;
                int cx = Mathf.FloorToInt(center.x / cellSize);
                int cy = Mathf.FloorToInt(center.y / cellSize);
                int r = Mathf.CeilToInt(outer / cellSize);

                int yMin = Mathf.Max(0, cy - r);
                int yMax = Mathf.Min(tex.height - 1, cy + r);
                int xMin = Mathf.Max(0, cx - r);
                int xMax = Mathf.Min(tex.width - 1, cx + r);

                for (int py = yMin; py <= yMax; py++)
                {
                    float wy = (py + 0.5f) * cellSize;
                    for (int px = xMin; px <= xMax; px++)
                    {
                        float wx = (px + 0.5f) * cellSize;
                        // Contains 側でノイズ歪みを計算してくれる
                        if (!region.Contains(new Vector2(wx, wy))) continue;

                        Color cur = tex.GetPixel(px, py);
                        Color mixed = Color.Lerp(cur, forestTint, blend);
                        tex.SetPixel(px, py, mixed);
                    }
                }
            }
            // Apply は OverlayFeatures 側で最後に呼ばれるのでここでは省略
        }

        /// <summary>
        /// 生成された PlacedFeature を、テクスチャの上に小さなマーカーとして重ねる。
        /// Height Map / Terrain Grid はピクセル解像度が違うため、セルサイズを渡して
        /// 各プレビューに合わせたスケールで打つ。
        /// </summary>
        private static void OverlayFeatures(Texture2D tex, MapData map, float cellSize)
        {
            if (tex == null || map == null) return;

            var features = map.Features;
            if (features == null || features.Count == 0) return;

            Color32 outline = new Color(0f, 0f, 0f);             // black halo
            Color32 ownCore = new Color(0.30f, 0.80f, 1.00f);    // cyan（自陣営）
            Color32 enemyCore = new Color(1.00f, 0.30f, 0.30f);  // red（敵陣営）
            Color32 bridgeCore = new Color(0.95f, 0.70f, 0.25f); // amber
            Color32 treeCore = new Color(0.10f, 0.55f, 0.15f);   // dark green
            Color32 rockCore = new Color(0.55f, 0.55f, 0.55f);   // gray

            for (int i = 0; i < features.Count; i++)
            {
                PlacedFeature f = features[i];
                int px = Mathf.FloorToInt(f.WorldPosition.x / cellSize);
                int py = Mathf.FloorToInt(f.WorldPosition.z / cellSize);

                switch (f.Type)
                {
                    case FeatureType.OwnMainStone:
                        DrawMarker(tex, px, py, ownCore, outline, isMain: true);
                        break;
                    case FeatureType.OwnSubStone:
                        DrawMarker(tex, px, py, ownCore, outline, isMain: false);
                        break;
                    case FeatureType.EnemyMainStone:
                        DrawMarker(tex, px, py, enemyCore, outline, isMain: true);
                        break;
                    case FeatureType.EnemySubStone:
                        DrawMarker(tex, px, py, enemyCore, outline, isMain: false);
                        break;
                    case FeatureType.Bridge:
                        DrawMarker(tex, px, py, bridgeCore, outline, isMain: false);
                        break;
                    case FeatureType.Tree:
                        DrawDot(tex, px, py, treeCore);
                        break;
                    case FeatureType.Rock:
                        DrawDot(tex, px, py, rockCore);
                        break;
                }
            }

            tex.Apply(false);
        }

        /// <summary>単色の 1 ピクセル点を打つ（木・岩のような「小物」用）。</summary>
        private static void DrawDot(Texture2D tex, int cx, int cy, Color32 color)
        {
            SetPixelSafe(tex, cx, cy, color);
        }

        /// <summary>十字 + 中心点のマーカーを描画する（黒の輪郭 + 明るいコア）。</summary>
        /// <param name="isMain">true なら大きめマーカー（メイン魔石用）。false は従来サイズ。</param>
        private static void DrawMarker(Texture2D tex, int cx, int cy, Color32 core, Color32 outline, bool isMain)
        {
            int outlineReach = isMain ? 3 : 2;
            int coreReach = isMain ? 2 : 1;

            // 菱形の外側輪郭
            for (int dy = -outlineReach; dy <= outlineReach; dy++)
            {
                for (int dx = -outlineReach; dx <= outlineReach; dx++)
                {
                    if (Mathf.Abs(dx) + Mathf.Abs(dy) > outlineReach) continue;
                    SetPixelSafe(tex, cx + dx, cy + dy, outline);
                }
            }
            // 中心のコア（メインは 3x3、サブは 1x1 の十字）
            for (int dy = -coreReach; dy <= coreReach; dy++)
            {
                for (int dx = -coreReach; dx <= coreReach; dx++)
                {
                    if (Mathf.Abs(dx) + Mathf.Abs(dy) > coreReach) continue;
                    SetPixelSafe(tex, cx + dx, cy + dy, core);
                }
            }
        }

        private static void SetPixelSafe(Texture2D tex, int x, int y, Color32 color)
        {
            if (x < 0 || y < 0 || x >= tex.width || y >= tex.height) return;
            tex.SetPixel(x, y, color);
        }

        private static Color32 GroundStateColor(GroundState state) => state switch
        {
            GroundState.Normal => new Color(0.60f, 0.80f, 0.40f),
            GroundState.Swamp => new Color(0.30f, 0.35f, 0.20f),
            GroundState.Snow => new Color(0.95f, 0.95f, 0.95f),
            GroundState.Water => new Color(0.20f, 0.50f, 0.95f),
            _ => new Color(1f, 0f, 1f),
        };
    }
}
#endif
