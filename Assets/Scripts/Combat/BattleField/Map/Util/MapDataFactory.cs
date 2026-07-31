using UnityEngine;

namespace WarSimulation.Combat.Map
{
    /// <summary>
    /// MapData の空マップ／平坦マップ生成。手作りマップ構築で共有する。
    /// </summary>
    public static class MapDataFactory
    {
        /// <summary>
        /// 解像度だけ揃えた空マップ。高度は 0 のまま。
        /// </summary>
        public static MapData CreateEmptyMap(MapConfig config, int seed)
        {
            if (config == null)
                throw new System.ArgumentNullException(nameof(config));

            int resolution = config.HeightMapResolution;
            float cellSize = config.HeightMapCellSize;
            var height = new HeightMap(resolution, resolution, cellSize);
            var grid = new GroundStateGrid(resolution, resolution, cellSize);
            return new MapData(height, grid, seed);
        }

        /// <summary>
        /// 全セルを <see cref="MapConfig.BaseHeight"/> で埋めた平坦マップ。
        /// </summary>
        public static MapData CreateFlatMap(MapConfig config, int seed)
        {
            MapData map = CreateEmptyMap(config, seed);
            float baseHeight = config.BaseHeight;
            HeightMap height = map.Height;
            for (int z = 0; z < height.Height; z++)
            {
                for (int x = 0; x < height.Width; x++)
                    height.SetHeight(x, z, baseHeight);
            }

            return map;
        }
    }
}
