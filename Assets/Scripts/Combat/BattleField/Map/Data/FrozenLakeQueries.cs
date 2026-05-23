using UnityEngine;

namespace WarSimulation.Combat.Map
{
    /// <summary>
    /// 凍結湖は <see cref="GroundStateGrid"/> 上は <see cref="GroundState.Water"/> のままなので、
    /// 描画・プレビューでは <see cref="MapData.Lakes"/> と組み合わせて判定する。
    /// </summary>
    public static class FrozenLakeQueries
    {
        /// <summary>
        /// 指定位置が Water セルかつ、いずれかの湖の Water タグ範囲内ならその <see cref="LakeRegion"/> を返す。
        /// （川だけの Water は false）
        /// </summary>
        public static bool TryFindLakeWithTaggedWaterAt(MapData map, float worldX, float worldZ, out LakeRegion lake)
        {
            lake = default;
            if (map == null) return false;
            if (map.GroundStates.SampleAt(new Vector3(worldX, 0f, worldZ)) != GroundState.Water)
                return false;

            var lakes = map.Lakes;
            Vector2 p = new Vector2(worldX, worldZ);
            for (int i = 0; i < lakes.Count; i++)
            {
                LakeRegion L = lakes[i];
                if (L.ContainsWaterTagged(p))
                {
                    lake = L;
                    return true;
                }
            }

            return false;
        }

        public static bool IsFrozenLakeWaterAt(MapData map, float worldX, float worldZ)
        {
            if (!TryFindLakeWithTaggedWaterAt(map, worldX, worldZ, out LakeRegion lake)) return false;
            return lake.IsFrozen;
        }
    }
}
