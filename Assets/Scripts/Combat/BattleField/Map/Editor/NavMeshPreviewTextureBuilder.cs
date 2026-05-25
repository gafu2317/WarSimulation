#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace WarSimulation.Combat.Map.EditorOnly
{
    public readonly struct NavMeshPreviewLegendEntry
    {
        public int AreaIndex { get; }
        public string AreaName { get; }
        public float Cost { get; }
        public Color32 Color { get; }

        public NavMeshPreviewLegendEntry(int areaIndex, string areaName, float cost, Color32 color)
        {
            AreaIndex = areaIndex;
            AreaName = areaName;
            Cost = cost;
            Color = color;
        }
    }

    public readonly struct NavMeshPreviewBuildResult
    {
        public Texture2D Texture { get; }
        public IReadOnlyList<NavMeshPreviewLegendEntry> Legend { get; }
        public bool Success { get; }

        public NavMeshPreviewBuildResult(
            Texture2D texture,
            IReadOnlyList<NavMeshPreviewLegendEntry> legend,
            bool success)
        {
            Texture = texture;
            Legend = legend;
            Success = success;
        }
    }

    /// <summary>
    /// ベイク済み NavMesh を MapData と同解像度の 2D テクスチャに落とす。
    /// 同一 XZ で上下に重なる NavMesh がある場合は、マップ上方からサンプルして最も高い歩行面を表示する。
    /// </summary>
    public static class NavMeshPreviewTextureBuilder
    {
        private const float MinVerticalSearchRadius = 4f;
        private const float VerticalSearchPadding = 4f;
        private const int ProgressUpdateInterval = 4096;

        private static readonly (string Name, Color32 Color)[] s_areaPalette =
        {
            ("Walkable", new Color32(210, 195, 160, 255)),
            ("Forest", new Color32(25, 100, 35, 255)),
            ("Snow", new Color32(240, 240, 245, 255)),
            ("Swamp", new Color32(70, 85, 45, 255)),
            ("River", new Color32(40, 90, 200, 255)),
            ("Lake", new Color32(20, 50, 140, 255)),
            ("FrozenLake", new Color32(140, 210, 240, 255)),
            ("Jump", new Color32(180, 120, 220, 255)),
            ("Not Walkable", new Color32(40, 40, 40, 255)),
        };

        private static readonly Color32 s_unwalkableColor = new(0, 0, 0, 255);

        public static NavMeshPreviewBuildResult Build(MapData map)
        {
            if (map == null)
            {
                return new NavMeshPreviewBuildResult(null, null, false);
            }

            NavMeshTriangulation triangulation = NavMesh.CalculateTriangulation();
            if (triangulation.vertices == null || triangulation.vertices.Length == 0)
            {
                return new NavMeshPreviewBuildResult(null, null, false);
            }

            HeightMap heightMap = map.Height;
            int width = heightMap.Width;
            int height = heightMap.Height;
            float cellSize = heightMap.CellSize;

            float probeY = GetTopDownProbeHeight(map, heightMap, out float minHeight);
            float sampleRadius = Mathf.Max(
                MinVerticalSearchRadius,
                probeY - minHeight + VerticalSearchPadding);

            var tex = new Texture2D(width, height, TextureFormat.RGB24, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };

            var pixels = new Color32[width * height];
            var usedAreas = new HashSet<int>();
            int totalCells = width * height;
            int processed = 0;

            try
            {
                for (int z = 0; z < height; z++)
                {
                    float worldZ = (z + 0.5f) * cellSize;
                    for (int x = 0; x < width; x++)
                    {
                        float worldX = (x + 0.5f) * cellSize;
                        var probe = new Vector3(worldX, probeY, worldZ);

                        if (NavMesh.SamplePosition(
                                probe,
                                out NavMeshHit hit,
                                sampleRadius,
                                NavMesh.AllAreas))
                        {
                            int areaIndex = MaskToAreaIndex(hit.mask);
                            usedAreas.Add(areaIndex);
                            pixels[z * width + x] = ResolveAreaColor(areaIndex);
                        }
                        else
                        {
                            pixels[z * width + x] = s_unwalkableColor;
                        }

                        processed++;
                        if (processed % ProgressUpdateInterval == 0)
                        {
                            EditorUtility.DisplayProgressBar(
                                "NavMesh Preview",
                                $"Sampling NavMesh ({processed}/{totalCells})",
                                (float)processed / totalCells);
                        }
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            tex.SetPixels32(pixels);
            tex.Apply(false);

            IReadOnlyList<NavMeshPreviewLegendEntry> legend = BuildLegend(usedAreas);
            return new NavMeshPreviewBuildResult(tex, legend, true);
        }

        private static IReadOnlyList<NavMeshPreviewLegendEntry> BuildLegend(HashSet<int> usedAreas)
        {
            var legend = new List<NavMeshPreviewLegendEntry>();

            for (int i = 0; i < s_areaPalette.Length; i++)
            {
                (string name, Color32 color) = s_areaPalette[i];
                int areaIndex = NavMesh.GetAreaFromName(name);
                if (areaIndex < 0 || !usedAreas.Contains(areaIndex)) continue;

                legend.Add(new NavMeshPreviewLegendEntry(
                    areaIndex,
                    name,
                    NavMesh.GetAreaCost(areaIndex),
                    color));
            }

            foreach (int areaIndex in usedAreas)
            {
                bool alreadyListed = false;
                for (int j = 0; j < legend.Count; j++)
                {
                    if (legend[j].AreaIndex != areaIndex) continue;
                    alreadyListed = true;
                    break;
                }

                if (alreadyListed) continue;

                legend.Add(new NavMeshPreviewLegendEntry(
                    areaIndex,
                    ResolveAreaName(areaIndex),
                    NavMesh.GetAreaCost(areaIndex),
                    ResolveAreaColor(areaIndex)));
            }

            legend.Sort((a, b) => a.AreaIndex.CompareTo(b.AreaIndex));
            legend.Insert(0, new NavMeshPreviewLegendEntry(-1, "Unwalkable", 0f, s_unwalkableColor));
            return legend;
        }

        private static Color32 ResolveAreaColor(int areaIndex)
        {
            for (int i = 0; i < s_areaPalette.Length; i++)
            {
                int paletteIndex = NavMesh.GetAreaFromName(s_areaPalette[i].Name);
                if (paletteIndex == areaIndex) return s_areaPalette[i].Color;
            }

            return new Color32(200, 200, 200, 255);
        }

        private static string ResolveAreaName(int areaIndex)
        {
            for (int i = 0; i < s_areaPalette.Length; i++)
            {
                int paletteIndex = NavMesh.GetAreaFromName(s_areaPalette[i].Name);
                if (paletteIndex == areaIndex) return s_areaPalette[i].Name;
            }

            return $"Area {areaIndex}";
        }

        private static int MaskToAreaIndex(int mask)
        {
            if (mask <= 0) return 0;

            int area = 0;
            int m = mask;
            while ((m & 1) == 0)
            {
                m >>= 1;
                area++;
            }

            return area;
        }

        private static float GetTopDownProbeHeight(MapData map, HeightMap heightMap, out float minHeight)
        {
            GetHeightRange(heightMap, out minHeight, out float maxHeight);

            float maxBridgeDeckY = float.NegativeInfinity;
            List<PlacedFeature> features = map.Features;
            if (features != null)
            {
                for (int i = 0; i < features.Count; i++)
                {
                    PlacedFeature feature = features[i];
                    if (feature.Type != FeatureType.Bridge) continue;

                    float deckY = feature.WorldPosition.y + feature.Scale.y * 0.5f;
                    if (deckY > maxBridgeDeckY) maxBridgeDeckY = deckY;
                }
            }

            float topSurfaceY = maxHeight;
            if (maxBridgeDeckY > topSurfaceY) topSurfaceY = maxBridgeDeckY;
            return topSurfaceY + VerticalSearchPadding;
        }

        private static void GetHeightRange(HeightMap heightMap, out float min, out float max)
        {
            min = float.PositiveInfinity;
            max = float.NegativeInfinity;

            for (int z = 0; z < heightMap.Height; z++)
            {
                for (int x = 0; x < heightMap.Width; x++)
                {
                    float h = heightMap.GetHeight(x, z);
                    if (h < min) min = h;
                    if (h > max) max = h;
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
#endif
