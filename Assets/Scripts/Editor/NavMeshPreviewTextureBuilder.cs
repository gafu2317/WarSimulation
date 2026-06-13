#if UNITY_EDITOR
using System.Collections.Generic;
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
    /// MapData ベースの NavMesh エリアプレビューを 2D テクスチャに落とす。
    /// 木・岩・魔石などの局所障害物は含めず、地形・水域・橋によるエリアだけを表示する。
    /// </summary>
    public static class NavMeshPreviewTextureBuilder
    {
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
            if (map == null) return new NavMeshPreviewBuildResult(null, null, false);

            HeightMap heightMap = map.Height;
            int width = heightMap.Width;
            int height = heightMap.Height;

            var tex = new Texture2D(width, height, TextureFormat.RGB24, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };

            var pixels = new Color32[width * height];
            CombatNavAreaKind[,] areaGrid = CombatNavMeshAreaGridBuilder.Build(map);
            var usedAreas = new HashSet<int>();

            for (int z = 0; z < height; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    string areaName = CombatNavMeshAreaGridBuilder.GetAreaName(areaGrid[x, z]);
                    int areaIndex = ResolveAreaIndex(areaName);
                    usedAreas.Add(areaIndex);
                    pixels[z * width + x] = ResolveAreaColor(areaIndex);
                }
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

        private static int ResolveAreaIndex(string areaName)
        {
            int areaIndex = NavMesh.GetAreaFromName(areaName);
            return areaIndex >= 0 ? areaIndex : 0;
        }
    }
}
#endif
