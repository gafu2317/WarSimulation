#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using WarSimulation.Combat.Map;

namespace WarSimulation.Combat.Map.EditorOnly
{
    /// <summary>
    /// 手作りマップの真上 2D プレビュー。AuthoredMapBuilder の結果を色分けして表示する。
    /// </summary>
    internal static class MapAuthoringPreview2D
    {
        private const float GroundStateOverlayBlend = 0.75f;
        private const float WaterBodyFillBlend = 0.75f;
        private static readonly Color32 WaterFill = new Color32(51, 128, 242, 255);
        private static readonly Color32 IceFill = new Color32(140, 217, 255, 255);

        public static Texture2D Build(MapData map)
        {
            return BuildBackground(map);
        }

        public static Texture2D BuildBackground(MapData map)
        {
            if (map == null) return null;

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

            var tex = new Texture2D(h.Width, h.Height, TextureFormat.RGBA32, false)
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
                        color = Color32.Lerp(color, GroundStateColor(state), GroundStateOverlayBlend);
                    pixels[z * h.Width + x] = color;
                }
            }

            tex.SetPixels32(pixels);
            OverlayRivers(tex, map, cellSize);
            OverlayLakes(tex, map, cellSize);
            OverlayForests(tex, map, cellSize);
            OverlayTrees(tex, map, cellSize);
            OverlayFeatureDots(tex, map, cellSize);
            tex.Apply(false);
            return tex;
        }

        public static bool TryMapPoint(Rect drawRect, Vector2 guiMouse, float worldSize, out Vector2 mapXZ)
        {
            mapXZ = default;
            if (drawRect.width < 1f || drawRect.height < 1f) return false;
            if (!drawRect.Contains(guiMouse)) return false;

            float u = (guiMouse.x - drawRect.x) / drawRect.width;
            float vFromTop = (guiMouse.y - drawRect.y) / drawRect.height;
            // Texture2D の y=0 はマップ z=0（南）。GUI は上が北なので反転する。
            float v = 1f - vFromTop;
            mapXZ = new Vector2(
                Mathf.Clamp(u * worldSize, 0f, worldSize),
                Mathf.Clamp(v * worldSize, 0f, worldSize));
            return true;
        }

        /// <summary>
        /// マップ外周付近のクリックを拾いやすくする。取得点はワールド座標（端スナップ前）。
        /// </summary>
        public static bool TryMapPointNearEdge(
            Rect drawRect,
            Vector2 guiMouse,
            float worldSize,
            float edgeSlopGui,
            out Vector2 mapXZ)
        {
            mapXZ = default;
            if (drawRect.width < 1f || drawRect.height < 1f) return false;

            var hit = Rect.MinMaxRect(
                drawRect.xMin - edgeSlopGui,
                drawRect.yMin - edgeSlopGui,
                drawRect.xMax + edgeSlopGui,
                drawRect.yMax + edgeSlopGui);
            if (!hit.Contains(guiMouse)) return false;

            float u = (guiMouse.x - drawRect.x) / drawRect.width;
            float vFromTop = (guiMouse.y - drawRect.y) / drawRect.height;
            float v = 1f - vFromTop;
            mapXZ = new Vector2(u * worldSize, v * worldSize);
            return true;
        }

        /// <summary>
        /// マップ矩形 [0, world]×[0, world] の最も近い辺へスナップする。
        /// </summary>
        public static Vector2 SnapToNearestEdge(Vector2 mapXZ, float worldSize)
        {
            float x = Mathf.Clamp(mapXZ.x, 0f, worldSize);
            float z = Mathf.Clamp(mapXZ.y, 0f, worldSize);

            float dLeft = x;
            float dRight = worldSize - x;
            float dBottom = z;
            float dTop = worldSize - z;
            float nearest = Mathf.Min(Mathf.Min(dLeft, dRight), Mathf.Min(dBottom, dTop));

            if (nearest == dLeft) return new Vector2(0f, z);
            if (nearest == dRight) return new Vector2(worldSize, z);
            if (nearest == dBottom) return new Vector2(x, 0f);
            return new Vector2(x, worldSize);
        }

        public static Vector2 MapToGui(Rect drawRect, Vector2 mapXZ, float worldSize)
        {
            float u = worldSize > 0.0001f ? mapXZ.x / worldSize : 0f;
            float v = worldSize > 0.0001f ? mapXZ.y / worldSize : 0f;
            return new Vector2(
                drawRect.x + u * drawRect.width,
                drawRect.y + (1f - v) * drawRect.height);
        }

        private static void OverlayRivers(Texture2D tex, MapData map, float cellSize)
        {
            if (map.Rivers == null || map.Rivers.Count == 0) return;

            int w = tex.width;
            int h = tex.height;
            var inside = new bool[w * h];

            for (int r = 0; r < map.Rivers.Count; r++)
            {
                RiverPath river = map.Rivers[r];
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
                    tex.SetPixel(px, py, Color32.Lerp(cur, WaterFill, WaterBodyFillBlend));
                }
            }
        }

        private static void OverlayLakes(Texture2D tex, MapData map, float cellSize)
        {
            for (int i = 0; i < map.Lakes.Count; i++)
            {
                LakeRegion lake = map.Lakes[i];
                Color32 fill = lake.IsFrozen ? IceFill : WaterFill;
                float outer = lake.OuterRadius;
                int cx = Mathf.FloorToInt(lake.Center.x / cellSize);
                int cy = Mathf.FloorToInt(lake.Center.y / cellSize);
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
                        tex.SetPixel(px, py, Color32.Lerp(cur, fill, WaterBodyFillBlend));
                    }
                }
            }
        }

        private static void OverlayForests(Texture2D tex, MapData map, float cellSize)
        {
            Color forestTint = new Color(0.10f, 0.45f, 0.18f);
            for (int i = 0; i < map.ForestRegions.Count; i++)
            {
                ForestRegion region = map.ForestRegions[i];
                float outer = region.OuterRadius;
                int cx = Mathf.FloorToInt(region.Center.x / cellSize);
                int cy = Mathf.FloorToInt(region.Center.y / cellSize);
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
                        tex.SetPixel(px, py, Color.Lerp(cur, forestTint, 0.55f));
                    }
                }
            }
        }

        private static void OverlayTrees(Texture2D tex, MapData map, float cellSize)
        {
            Color32 tree = new Color32(20, 120, 30, 255);
            for (int i = 0; i < map.Features.Count; i++)
            {
                PlacedFeature f = map.Features[i];
                if (f.Type != FeatureType.Tree) continue;
                int px = Mathf.Clamp(Mathf.FloorToInt(f.WorldPosition.x / cellSize), 0, tex.width - 1);
                int py = Mathf.Clamp(Mathf.FloorToInt(f.WorldPosition.z / cellSize), 0, tex.height - 1);
                tex.SetPixel(px, py, tree);
            }
        }

        private static void OverlayFeatureDots(Texture2D tex, MapData map, float cellSize)
        {
            for (int i = 0; i < map.Features.Count; i++)
            {
                PlacedFeature f = map.Features[i];
                Color32? color = f.Type switch
                {
                    FeatureType.Bridge => new Color32(140, 90, 40, 255),
                    FeatureType.Rock => new Color32(110, 110, 110, 255),
                    _ => null,
                };
                if (!color.HasValue) continue;
                int px = Mathf.Clamp(Mathf.FloorToInt(f.WorldPosition.x / cellSize), 0, tex.width - 1);
                int py = Mathf.Clamp(Mathf.FloorToInt(f.WorldPosition.z / cellSize), 0, tex.height - 1);
                tex.SetPixel(px, py, color.Value);
            }
        }

        private static Color32 HeightColorRamp(float height, float min, float max)
        {
            var cLow = new Color(0.50f, 0.20f, 0.70f);
            var cMid = new Color(0.40f, 0.75f, 0.35f);
            var cHi1 = new Color(0.95f, 0.90f, 0.30f);
            var cHi2 = new Color(0.85f, 0.20f, 0.20f);

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

        private static Color32 GroundStateColor(GroundState state) => state switch
        {
            GroundState.Snow => new Color32(240, 240, 250, 255),
            GroundState.Swamp => new Color32(70, 90, 40, 255),
            _ => new Color32(120, 120, 120, 255),
        };
    }
}
#endif
