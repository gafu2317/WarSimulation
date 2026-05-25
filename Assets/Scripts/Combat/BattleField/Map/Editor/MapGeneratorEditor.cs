#if UNITY_EDITOR
using System.Collections.Generic;
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
        private const float GroundStateOverlayBlend = 0.75f;
        private const float WaterBodyFillBlend = 0.75f;

        private static readonly Color32 WaterFill = new Color32(51, 128, 242, 255);
        private static readonly Color32 IceFill = new Color32(140, 217, 255, 255);

        private Texture2D _previewTex;
        private Texture2D _navMeshPreviewTex;
        private IReadOnlyList<NavMeshPreviewLegendEntry> _navMeshLegend;
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

            DrawPreview("Map Preview", _previewTex);
            DrawNavMeshPreview();
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

            BuildMapPreviewFromMap(data);
            ClearNavMeshPreview();
            _lastInfo = null;
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

            EnsureRenderComponents(gen);
            gen.Render3D(data);

            _lastInfo = null;
            BuildMapPreviewFromMap(data);
            BuildNavMeshPreviewFromMap(data, gen);
        }

        private void BuildMapPreviewFromMap(MapData data)
        {
            ClearMapPreview();
            _previewTex = BuildMapPreviewTexture(data);
            float cellSize = data.Height.CellSize;
            OverlayForestRegions(_previewTex, data, cellSize);
            OverlayLakeRegions(_previewTex, data, cellSize);
            OverlayRiverCorridors(_previewTex, data, cellSize);
            OverlayFeatures(_previewTex, data, cellSize);
        }

        private void BuildNavMeshPreviewFromMap(MapData data, MapGenerator gen)
        {
            ClearNavMeshPreview();

            if (gen.GetComponent<CombatNavMeshBuilder>() == null)
            {
                _lastInfo = "NavMesh のベイクに失敗したため、NavMesh Preview は生成されませんでした。";
                return;
            }

            NavMeshPreviewBuildResult result = NavMeshPreviewTextureBuilder.Build(data);
            if (!result.Success || result.Texture == null)
            {
                _lastInfo = "NavMesh Preview の生成に失敗しました。NavMesh が空か、サンプリングできませんでした。";
                return;
            }

            _navMeshPreviewTex = result.Texture;
            _navMeshLegend = result.Legend;
        }

        private static void EnsureRenderComponents(MapGenerator gen)
        {
            if (gen.GetComponent<TerrainRenderer>() == null) Undo.AddComponent<TerrainRenderer>(gen.gameObject);
            if (gen.GetComponent<TerrainSkirtRenderer>() == null) Undo.AddComponent<TerrainSkirtRenderer>(gen.gameObject);
            if (gen.GetComponent<RiverRenderer>() == null) Undo.AddComponent<RiverRenderer>(gen.gameObject);
            if (gen.GetComponent<LakeRenderer>() == null) Undo.AddComponent<LakeRenderer>(gen.gameObject);
            if (gen.GetComponent<BridgeRenderer>() == null) Undo.AddComponent<BridgeRenderer>(gen.gameObject);
            if (gen.GetComponent<FeatureRenderer>() == null) Undo.AddComponent<FeatureRenderer>(gen.gameObject);
            if (gen.GetComponent<CombatNavMeshBuilder>() == null) Undo.AddComponent<CombatNavMeshBuilder>(gen.gameObject);
        }

        private void Clear3D()
        {
            var gen = (MapGenerator)target;
            var terrainRenderer = gen.GetComponent<TerrainRenderer>();
            if (terrainRenderer != null) terrainRenderer.Clear();
            var terrainSkirtRenderer = gen.GetComponent<TerrainSkirtRenderer>();
            if (terrainSkirtRenderer != null) terrainSkirtRenderer.Clear();
            var riverRenderer = gen.GetComponent<RiverRenderer>();
            if (riverRenderer != null) riverRenderer.Clear();
            var lakeRenderer = gen.GetComponent<LakeRenderer>();
            if (lakeRenderer != null) lakeRenderer.Clear();
            var bridgeRenderer = gen.GetComponent<BridgeRenderer>();
            if (bridgeRenderer != null) bridgeRenderer.Clear();
            var featureRenderer = gen.GetComponent<FeatureRenderer>();
            if (featureRenderer != null) featureRenderer.Clear();
        }

        private void ClearTextures()
        {
            ClearMapPreview();
            ClearNavMeshPreview();
        }

        private void ClearMapPreview()
        {
            if (_previewTex != null) { DestroyImmediate(_previewTex); _previewTex = null; }
        }

        private void ClearNavMeshPreview()
        {
            if (_navMeshPreviewTex != null) { DestroyImmediate(_navMeshPreviewTex); _navMeshPreviewTex = null; }
            _navMeshLegend = null;
        }

        private void DrawNavMeshPreview()
        {
            if (_navMeshPreviewTex == null) return;

            DrawPreview("NavMesh Preview", _navMeshPreviewTex);
            DrawNavMeshLegend();
        }

        private void DrawNavMeshLegend()
        {
            if (_navMeshLegend == null || _navMeshLegend.Count == 0) return;

            EditorGUILayout.LabelField("NavMesh Areas", EditorStyles.boldLabel);
            const float swatchSize = 12f;

            for (int i = 0; i < _navMeshLegend.Count; i++)
            {
                NavMeshPreviewLegendEntry entry = _navMeshLegend[i];
                using (new EditorGUILayout.HorizontalScope())
                {
                    Rect swatchRect = GUILayoutUtility.GetRect(swatchSize, swatchSize, GUILayout.Width(swatchSize));
                    EditorGUI.DrawRect(swatchRect, entry.Color);

                    string costText = entry.AreaIndex < 0
                        ? entry.AreaName
                        : $"{entry.AreaName} (cost {entry.Cost:0.##})";
                    EditorGUILayout.LabelField(costText);
                }
            }
        }

        private static void DrawPreview(string label, Texture2D tex)
        {
            if (tex == null) return;
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            Rect r = GUILayoutUtility.GetRect(PreviewDisplaySize, PreviewDisplaySize, GUILayout.ExpandWidth(false));
            EditorGUI.DrawPreviewTexture(r, tex);
        }

        /// <summary>
        /// HeightMap の高度ランプをベースに、GroundState（水・雪・沼）と崖を合成したプレビュー。
        /// HeightMap と GroundStateGrid は同一解像度を前提とする。
        /// </summary>
        private static Texture2D BuildMapPreviewTexture(MapData map)
        {
            HeightMap h = map.Height;
            GroundStateGrid g = map.GroundStates;
            float cellSize = h.CellSize;

            float min = float.PositiveInfinity;
            float max = float.NegativeInfinity;
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
            Color32 cliffTint = new Color32(115, 62, 32, 255);

            for (int z = 0; z < h.Height; z++)
            {
                for (int x = 0; x < h.Width; x++)
                {
                    GroundState state = g.GetCell(x, z);

                    Color32 color = HeightColorRamp(h.GetHeight(x, z), min, max);
                    if (h.CliffFaces.Get(x, z))
                        color = Color32.Lerp(color, cliffTint, 0.72f);

                    if (state == GroundState.Snow || state == GroundState.Swamp)
                    {
                        color = Color32.Lerp(color, GroundStateColor(state), GroundStateOverlayBlend);
                    }

                    pixels[z * h.Width + x] = color;
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(false);
            return tex;
        }

        /// <summary>
        /// 高度を「紫（低）→ 緑（海抜 0）→ 黄 → 赤（高）」のカラーランプに落とし込む。
        /// 川・湖・凍結湖は <see cref="OverlayLakeRegions"/> / <see cref="OverlayRiverCorridors"/> で別途重ねる。
        /// 緑を常に h = 0 にピン留めすることで、どのマップでも「0 = 基準地面」として読める。
        /// </summary>
        private static Color32 HeightColorRamp(float height, float min, float max)
        {
            var cLow = new Color(0.50f, 0.20f, 0.70f); // purple
            var cMid = new Color(0.40f, 0.75f, 0.35f); // green
            var cHi1 = new Color(0.95f, 0.90f, 0.30f); // yellow
            var cHi2 = new Color(0.85f, 0.20f, 0.20f); // red

            if (height < 0f)
            {
                float depth = -Mathf.Min(0f, min);
                float t = depth > 1e-5f ? Mathf.Clamp01(1f + height / depth) : 1f;
                return Color.Lerp(cLow, cMid, t);
            }

            float peak = Mathf.Max(0f, max);
            float u = peak > 1e-5f ? Mathf.Clamp01(height / peak) : 0f;
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

            Color forestTint = new Color(0.10f, 0.45f, 0.18f);
            float blend = 0.55f;

            for (int i = 0; i < regions.Count; i++)
            {
                ForestRegion region = regions[i];
                Vector2 center = region.Center;

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
                        if (!region.Contains(new Vector2(wx, wy))) continue;

                        Color cur = tex.GetPixel(px, py);
                        Color mixed = Color.Lerp(cur, forestTint, blend);
                        tex.SetPixel(px, py, mixed);
                    }
                }
            }
        }

        /// <summary>
        /// <see cref="LakeRegion.ContainsCarve"/> に基づく掘削範囲を実寸で重ねる。
        /// 通常の湖は青、凍結湖は氷色（LakeRenderer のデフォルト色に合わせる）。
        /// </summary>
        private static void OverlayLakeRegions(Texture2D tex, MapData map, float cellSize)
        {
            if (tex == null || map == null) return;

            var lakes = map.Lakes;
            if (lakes == null || lakes.Count == 0) return;

            // 通常湖を先に、凍結湖を後から描いて重なり時に氷色が優先されるようにする。
            OverlayLakeRegionsPass(tex, lakes, cellSize, frozen: false);
            OverlayLakeRegionsPass(tex, lakes, cellSize, frozen: true);
        }

        private static void OverlayLakeRegionsPass(
            Texture2D tex,
            List<LakeRegion> lakes,
            float cellSize,
            bool frozen)
        {
            Color32 fill = frozen ? IceFill : WaterFill;

            for (int i = 0; i < lakes.Count; i++)
            {
                LakeRegion lake = lakes[i];
                if (lake.IsFrozen != frozen) continue;

                Vector2 center = lake.Center;
                float outer = lake.OuterRadius;
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
                        if (!lake.ContainsCarve(new Vector2(wx, wy))) continue;

                        Color32 cur = tex.GetPixel(px, py);
                        SetPixelSafe(tex, px, py, Color32.Lerp(cur, fill, WaterBodyFillBlend));
                    }
                }
            }
        }

        /// <summary>
        /// <see cref="RiverPath.WidthMeters"/> に基づく掘削 corridor を実寸で重ねる。
        /// セグメント重なりで塗りが積み上がらないよう、マスクを union してから 1 回だけ着色する。
        /// </summary>
        private static void OverlayRiverCorridors(Texture2D tex, MapData map, float cellSize)
        {
            if (tex == null || map == null) return;

            var rivers = map.Rivers;
            if (rivers == null || rivers.Count == 0) return;

            int w = tex.width;
            int h = tex.height;
            var inside = new bool[w * h];

            for (int r = 0; r < rivers.Count; r++)
            {
                RiverPath river = rivers[r];
                IReadOnlyList<Vector2Int> cells = river.Cells;
                if (cells == null || cells.Count < 2) continue;

                float halfW = river.WidthMeters * 0.5f;
                float rSq = halfW * halfW;

                for (int i = 0; i < cells.Count - 1; i++)
                {
                    Vector2Int c0 = cells[i];
                    Vector2Int c1 = cells[i + 1];
                    Vector2 a = new((c0.x + 0.5f) * cellSize, (c0.y + 0.5f) * cellSize);
                    Vector2 b = new((c1.x + 0.5f) * cellSize, (c1.y + 0.5f) * cellSize);

                    float minX = Mathf.Min(a.x, b.x) - halfW;
                    float maxX = Mathf.Max(a.x, b.x) + halfW;
                    float minY = Mathf.Min(a.y, b.y) - halfW;
                    float maxY = Mathf.Max(a.y, b.y) + halfW;

                    int pxMin = Mathf.Max(0, Mathf.FloorToInt(minX / cellSize));
                    int pxMax = Mathf.Min(w - 1, Mathf.CeilToInt(maxX / cellSize));
                    int pyMin = Mathf.Max(0, Mathf.FloorToInt(minY / cellSize));
                    int pyMax = Mathf.Min(h - 1, Mathf.CeilToInt(maxY / cellSize));

                    for (int py = pyMin; py <= pyMax; py++)
                    {
                        float wy = (py + 0.5f) * cellSize;
                        for (int px = pxMin; px <= pxMax; px++)
                        {
                            float wx = (px + 0.5f) * cellSize;
                            if (RiverCorridorUtility.DistanceSqPointToSegment(new Vector2(wx, wy), a, b) > rSq)
                                continue;

                            inside[py * w + px] = true;
                        }
                    }
                }
            }

            for (int py = 0; py < h; py++)
            {
                for (int px = 0; px < w; px++)
                {
                    if (!inside[py * w + px]) continue;

                    Color32 cur = tex.GetPixel(px, py);
                    SetPixelSafe(tex, px, py, Color32.Lerp(cur, WaterFill, WaterBodyFillBlend));
                }
            }
        }

        /// <summary>
        /// 生成された PlacedFeature を、テクスチャの上に小さなマーカーとして重ねる。
        /// </summary>
        private static void OverlayFeatures(Texture2D tex, MapData map, float cellSize)
        {
            if (tex == null || map == null) return;

            var features = map.Features;
            if (features == null || features.Count == 0) return;

            Color32 outline = new Color(0f, 0f, 0f);
            Color32 ownCore = new Color(0.30f, 0.80f, 1.00f);
            Color32 enemyCore = new Color(1.00f, 0.30f, 0.30f);
            Color32 bridgeCore = new Color(0.95f, 0.70f, 0.25f);
            Color32 treeCore = new Color(0.10f, 0.55f, 0.15f);
            Color32 rockCore = new Color(0.55f, 0.55f, 0.55f);

            for (int i = 0; i < features.Count; i++)
            {
                PlacedFeature f = features[i];
                int px = Mathf.FloorToInt(f.WorldPosition.x / cellSize);
                int py = Mathf.FloorToInt(f.WorldPosition.z / cellSize);

                switch (f.Type)
                {
                    case FeatureType.OwnMainStone:
                        DrawMarker(tex, px, py, ownCore, outline, outlineReach: 7, coreReach: 4);
                        break;
                    case FeatureType.OwnSubStone:
                        DrawMarker(tex, px, py, ownCore, outline, outlineReach: 5, coreReach: 3);
                        break;
                    case FeatureType.EnemyMainStone:
                        DrawMarker(tex, px, py, enemyCore, outline, outlineReach: 7, coreReach: 4);
                        break;
                    case FeatureType.EnemySubStone:
                        DrawMarker(tex, px, py, enemyCore, outline, outlineReach: 5, coreReach: 3);
                        break;
                    case FeatureType.Bridge:
                        DrawBridgeFootprint(tex, f, cellSize, bridgeCore, outline);
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

        /// <summary>
        /// 橋の PlacedFeature（位置・回転・Scale）に合わせた実寸フットプリントを上から描画する。
        /// local +X = 幅、local +Z = 川渡り方向の長さ（BridgePhase / BridgeRenderer と同規約）。
        /// </summary>
        private static void DrawBridgeFootprint(
            Texture2D tex,
            PlacedFeature feature,
            float cellSize,
            Color32 fill,
            Color32 outline)
        {
            float halfWidth = Mathf.Max(0f, feature.Scale.x) * 0.5f;
            float halfLength = Mathf.Max(0f, feature.Scale.z) * 0.5f;
            if (halfWidth <= 0f || halfLength <= 0f) return;

            Quaternion invRot = Quaternion.Inverse(feature.Rotation);
            Vector3 center = feature.WorldPosition;
            float maxExtent = Mathf.Sqrt(halfWidth * halfWidth + halfLength * halfLength);

            int pxMin = Mathf.Max(0, Mathf.FloorToInt((center.x - maxExtent) / cellSize));
            int pxMax = Mathf.Min(tex.width - 1, Mathf.CeilToInt((center.x + maxExtent) / cellSize));
            int pyMin = Mathf.Max(0, Mathf.FloorToInt((center.z - maxExtent) / cellSize));
            int pyMax = Mathf.Min(tex.height - 1, Mathf.CeilToInt((center.z + maxExtent) / cellSize));

            const float fillBlend = 0.75f;

            for (int py = pyMin; py <= pyMax; py++)
            {
                for (int px = pxMin; px <= pxMax; px++)
                {
                    if (!IsInsideBridgeFootprint(invRot, center, halfWidth, halfLength, px, py, cellSize))
                        continue;

                    bool isEdge =
                        !IsInsideBridgeFootprint(invRot, center, halfWidth, halfLength, px - 1, py, cellSize) ||
                        !IsInsideBridgeFootprint(invRot, center, halfWidth, halfLength, px + 1, py, cellSize) ||
                        !IsInsideBridgeFootprint(invRot, center, halfWidth, halfLength, px, py - 1, cellSize) ||
                        !IsInsideBridgeFootprint(invRot, center, halfWidth, halfLength, px, py + 1, cellSize);

                    if (isEdge)
                    {
                        SetPixelSafe(tex, px, py, outline);
                    }
                    else
                    {
                        Color32 cur = tex.GetPixel(px, py);
                        SetPixelSafe(tex, px, py, Color32.Lerp(cur, fill, fillBlend));
                    }
                }
            }
        }

        private static bool IsInsideBridgeFootprint(
            Quaternion invRot,
            Vector3 center,
            float halfWidth,
            float halfLength,
            int px,
            int py,
            float cellSize)
        {
            if (px < 0 || py < 0) return false;

            float wx = (px + 0.5f) * cellSize;
            float wz = (py + 0.5f) * cellSize;
            Vector3 local = invRot * (new Vector3(wx, 0f, wz) - center);
            return Mathf.Abs(local.x) <= halfWidth && Mathf.Abs(local.z) <= halfLength;
        }

        private static void DrawDot(Texture2D tex, int cx, int cy, Color32 color)
        {
            SetPixelSafe(tex, cx, cy, color);
        }

        private static void DrawMarker(
            Texture2D tex,
            int cx,
            int cy,
            Color32 core,
            Color32 outline,
            int outlineReach,
            int coreReach)
        {
            for (int dy = -outlineReach; dy <= outlineReach; dy++)
            {
                for (int dx = -outlineReach; dx <= outlineReach; dx++)
                {
                    if (Mathf.Abs(dx) + Mathf.Abs(dy) > outlineReach) continue;
                    SetPixelSafe(tex, cx + dx, cy + dy, outline);
                }
            }

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
            _ => new Color(1f, 0f, 1f),
        };
    }
}
#endif
