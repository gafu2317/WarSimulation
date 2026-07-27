using System.Collections.Generic;
using UnityEngine;

namespace WarSimulation.Combat.Map
{
    public static class RiverBuildUtility
    {
        public static void CarveAndRegister(MapData map, RiverShape shape, IReadOnlyList<Vector2Int> path)
        {
            if (map == null || shape == null || path == null || path.Count < 2) return;

            shape.Carve(map, path);
            map.AddRiver(new RiverPath(
                path,
                shape.WidthMeters,
                shape.DepthMeters,
                shape.WaterTagRatio));
        }
    }
}
