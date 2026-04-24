using UnityEngine;

namespace WarSimulation.Combat.Map
{
    /// <summary>
    /// 凍結湖は <see cref="GroundStateGrid"/> 上は <see cref="GroundState.Water"/> のままなので、
    /// 描画・プレビューでは <see cref="MapData.Lakes"/> と組み合わせて判定する。
    /// </summary>
    public static class FrozenLakeQueries
    {
        public static bool IsFrozenLakeWaterAt(MapData map, float worldX, float worldZ)
        {
            if (map == null) return false;
            if (map.GroundStates.SampleAt(new Vector3(worldX, 0f, worldZ)) != GroundState.Water)
                return false;

            var lakes = map.Lakes;
            for (int i = 0; i < lakes.Count; i++)
            {
                LakeRegion lake = lakes[i];
                if (!lake.IsFrozen) continue;

                float dx = worldX - lake.Center.x;
                float dz = worldZ - lake.Center.y;
                float tr = lake.WaterTaggedRadius;
                if (dx * dx + dz * dz <= tr * tr)
                    return true;
            }

            return false;
        }
    }
}
