using System.Collections.Generic;
using UnityEngine;

namespace WarSimulation.Combat.Map
{
    /// <summary>
    /// 魔石（メイン/サブ × 自陣営/敵陣営）を MapData.Features に登録するフェーズ。
    /// - 自陣営は Z が小さい側の 1/3（下）、敵陣営は Z が大きい側の 1/3（上）に配置する
    /// - メイン魔石は陣営ゾーン内でさらに奥（マップ端側）に寄せる
    /// - 全魔石は MagicStoneMinDistance 以上離す（棄却サンプリング）
    /// - River / Lake セルには配置しない
    /// </summary>
    public sealed class DecorationPhase : IMapGenerationPhase
    {
        public void Execute(MapData map, IRandom rng, MapGenerationConfig config)
        {
            if (map == null || rng == null || config == null) return;

            PlaceMagicStones(map, rng, config);
        }

        private static void PlaceMagicStones(MapData map, IRandom rng, MapGenerationConfig config)
        {
            int mainPerSide = Mathf.Max(0, config.MainStonesPerSide);
            int subPerSide = Mathf.Max(0, config.SubStonesPerSide);
            if (mainPerSide + subPerSide <= 0) return;

            float margin = config.MagicStonePlacementMargin;
            float world = config.WorldSize;
            float zoneRatio = Mathf.Clamp(config.MagicStoneZoneRatio, 0.05f, 0.5f);
            float zoneDepth = world * zoneRatio;

            float minX = margin;
            float maxX = Mathf.Max(margin, world - margin);

            // 自陣営 = 下（Z 小）。z in [margin, zoneDepth]
            // 敵陣営 = 上（Z 大）。z in [world - zoneDepth, world - margin]
            float ownZMin = margin;
            float ownZMax = Mathf.Max(margin, zoneDepth);
            float enemyZMin = Mathf.Min(world - margin, world - zoneDepth);
            float enemyZMax = world - margin;

            // メイン魔石はゾーン内でさらに奥寄り（マップ端側）に絞る
            float bias = Mathf.Clamp01(config.MainStoneBackBias);
            float ownMainZMax = Mathf.Lerp(ownZMax, ownZMin + (ownZMax - ownZMin) * 0.5f, bias);
            float enemyMainZMin = Mathf.Lerp(enemyZMin, enemyZMin + (enemyZMax - enemyZMin) * 0.5f, bias);

            float minDist = Mathf.Max(0f, config.MagicStoneMinDistance);
            float minDistSq = minDist * minDist;

            var placed = new List<Vector2>(4 * (mainPerSide + subPerSide));

            // メインを先に置く（奥寄りに確保するため）
            PlaceInZone(map, rng, config, placed, minDistSq,
                FeatureType.OwnMainStone, mainPerSide, minX, maxX, ownZMin, ownMainZMax);
            PlaceInZone(map, rng, config, placed, minDistSq,
                FeatureType.EnemyMainStone, mainPerSide, minX, maxX, enemyMainZMin, enemyZMax);

            // サブはゾーン全体で散らす
            PlaceInZone(map, rng, config, placed, minDistSq,
                FeatureType.OwnSubStone, subPerSide, minX, maxX, ownZMin, ownZMax);
            PlaceInZone(map, rng, config, placed, minDistSq,
                FeatureType.EnemySubStone, subPerSide, minX, maxX, enemyZMin, enemyZMax);
        }

        private static void PlaceInZone(
            MapData map, IRandom rng, MapGenerationConfig config,
            List<Vector2> placed, float minDistSq,
            FeatureType type, int count,
            float minX, float maxX, float minZ, float maxZ)
        {
            if (count <= 0) return;
            if (maxX <= minX || maxZ <= minZ) return;

            int maxAttempts = Mathf.Max(count * 40, 120);
            int remaining = count;

            for (int attempt = 0; attempt < maxAttempts && remaining > 0; attempt++)
            {
                float x = Mathf.Lerp(minX, maxX, rng.NextFloat());
                float z = Mathf.Lerp(minZ, maxZ, rng.NextFloat());
                Vector3 worldPos = new(x, 0f, z);

                // 川・湖は不可
                if (map.GroundStates.SampleAt(worldPos) == GroundState.Water) continue;

                var candidate = new Vector2(x, z);
                bool tooClose = false;
                for (int i = 0; i < placed.Count; i++)
                {
                    if ((placed[i] - candidate).sqrMagnitude < minDistSq)
                    {
                        tooClose = true;
                        break;
                    }
                }
                if (tooClose) continue;

                placed.Add(candidate);
                float height = map.Height.SampleAt(worldPos);
                map.AddFeature(new PlacedFeature(
                    type,
                    new Vector3(x, height, z),
                    Quaternion.identity));
                remaining--;
            }
        }
    }
}
