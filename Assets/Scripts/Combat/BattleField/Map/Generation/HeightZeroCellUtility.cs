using System.Collections.Generic;
using UnityEngine;

namespace WarSimulation.Combat.Map
{
    internal static class HeightZeroCellUtility
    {
        private const float HeightZeroTolerance = 0.001f;

        public static bool IsHeightZero(HeightMap height, int x, int z)
        {
            return height != null && Mathf.Abs(height.GetHeight(x, z)) <= HeightZeroTolerance;
        }

        public static bool IsRiverPassable(MapData map, int x, int z, float existingWaterClearance)
        {
            if (map == null) return false;
            HeightMap height = map.Height;
            if (!height.IsInBounds(x, z)) return false;
            if (!IsHeightZero(height, x, z)) return false;

            Vector2 world = CellCenter(height, x, z);
            if (IsInsideMountain(map.Mountains, world)) return false;
            if (IsNearWater(map.GroundStates, world, existingWaterClearance)) return false;
            return true;
        }

        public static Vector2 CellCenter(HeightMap height, int x, int z)
        {
            float cs = height.CellSize;
            return new Vector2((x + 0.5f) * cs, (z + 0.5f) * cs);
        }

        private static bool IsInsideMountain(IReadOnlyList<MountainRegion> mountains, Vector2 world)
        {
            if (mountains == null) return false;
            for (int i = 0; i < mountains.Count; i++)
            {
                MountainRegion mountain = mountains[i];
                float limit = mountain.Extent;
                if ((world - mountain.Center).sqrMagnitude <= limit * limit)
                    return true;
            }
            return false;
        }

        private static bool IsNearWater(GroundStateGrid ground, Vector2 world, float clearance)
        {
            if (ground == null) return false;
            if (clearance <= 0f)
                return ground.SampleAt(new Vector3(world.x, 0f, world.y)) == GroundState.Water;

            float cell = ground.CellSize;
            int radius = Mathf.CeilToInt(clearance / cell);
            int cx = Mathf.FloorToInt(world.x / cell);
            int cz = Mathf.FloorToInt(world.y / cell);
            for (int z = cz - radius; z <= cz + radius; z++)
            {
                for (int x = cx - radius; x <= cx + radius; x++)
                {
                    if (!ground.IsInBounds(x, z)) continue;
                    if (ground.GetCell(x, z) != GroundState.Water) continue;

                    float wx = (x + 0.5f) * cell;
                    float wz = (z + 0.5f) * cell;
                    float dx = wx - world.x;
                    float dz = wz - world.y;
                    if (dx * dx + dz * dz <= clearance * clearance)
                        return true;
                }
            }
            return false;
        }
    }
}
