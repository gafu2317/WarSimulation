using System.Collections.Generic;
using UnityEngine;

namespace WarSimulation.Combat.Map
{
    /// <summary>
    /// 岩フェーズ：マップ全体にランダムで岩を散布する。
    /// 棄却サンプリングで以下を避ける：
    ///   - 水セル（川・湖の中心部タグ）
    ///   - 川・湖の掘削・見た目の範囲（Water タグが岸に付かない設定でも岸に岩が乗らないようにする）
    ///   - 森クラスター領域（<see cref="MapData.ForestRegions"/>）
    ///   - 既存の岩からの最小距離
    /// </summary>
    public sealed class RockPhase : IMapGenerationPhase
    {
        public void Execute(MapData map, IRandom rng, MapGenerationConfig config)
        {
            if (map == null || rng == null || config == null) return;
            int target = config.RockCount;
            if (target <= 0) return;

            float margin = config.RockPlacementMargin;
            float minCenter = margin;
            float maxCenter = config.WorldSize - margin;
            if (maxCenter <= minCenter)
            {
                minCenter = maxCenter = config.WorldSize * 0.5f;
            }

            float minDist = Mathf.Max(0f, config.RockMinDistance);
            float minDistSq = minDist * minDist;
            int maxAttempts = Mathf.Max(target * 20, 100);
            int placed = 0;
            int startIndex = map.Features.Count;

            for (int attempt = 0; attempt < maxAttempts && placed < target; attempt++)
            {
                float x = Mathf.Lerp(minCenter, maxCenter, rng.NextFloat());
                float z = Mathf.Lerp(minCenter, maxCenter, rng.NextFloat());
                Vector3 worldPos = new(x, 0f, z);

                if (map.GroundStates.SampleAt(worldPos) == GroundState.Water) continue;

                var xz = new Vector2(x, z);
                if (IsInsideLakeCarve(map, xz)) continue;
                if (IsInsideRiverCorridor(map, xz)) continue;

                if (IsInsideForest(map, xz)) continue;

                if (minDistSq > 0f && IsTooCloseToExistingRock(map, startIndex, x, z, minDistSq)) continue;

                float y = map.Height.SampleAt(worldPos);
                map.AddFeature(new PlacedFeature(
                    FeatureType.Rock,
                    new Vector3(x, y, z),
                    Quaternion.identity));
                placed++;
            }
        }

        private static bool IsInsideLakeCarve(MapData map, Vector2 xz)
        {
            var lakes = map.Lakes;
            for (int i = 0; i < lakes.Count; i++)
            {
                if (lakes[i].ContainsCarve(xz)) return true;
            }
            return false;
        }

        /// <summary>
        /// 川は <see cref="RiverShape"/> で幅いっぱい掘るが Water タグは内側だけのことがある。
        /// <see cref="RiverPath"/> のセル中心を結んだ折れ線からの距離でフル幅を判定する。
        /// </summary>
        private static bool IsInsideRiverCorridor(MapData map, Vector2 xz)
        {
            var rivers = map.Rivers;
            if (rivers.Count == 0) return false;
            float cs = map.Height.CellSize;

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
                    Vector2 a = new((c0.x + 0.5f) * cs, (c0.y + 0.5f) * cs);
                    Vector2 b = new((c1.x + 0.5f) * cs, (c1.y + 0.5f) * cs);
                    if (DistanceSqPointToSegment(xz, a, b) <= rSq) return true;
                }
            }
            return false;
        }

        private static float DistanceSqPointToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float abSqr = ab.sqrMagnitude;
            if (abSqr < 1e-8f) return (p - a).sqrMagnitude;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / abSqr);
            Vector2 closest = a + ab * t;
            return (p - closest).sqrMagnitude;
        }

        private static bool IsInsideForest(MapData map, Vector2 pos)
        {
            var regions = map.ForestRegions;
            for (int i = 0; i < regions.Count; i++)
            {
                if (regions[i].Contains(pos)) return true;
            }
            return false;
        }

        private static bool IsTooCloseToExistingRock(MapData map, int startIndex, float x, float z, float minDistSq)
        {
            var features = map.Features;
            for (int i = startIndex; i < features.Count; i++)
            {
                if (features[i].Type != FeatureType.Rock) continue;
                Vector3 wp = features[i].WorldPosition;
                float ddx = wp.x - x;
                float ddz = wp.z - z;
                if (ddx * ddx + ddz * ddz < minDistSq) return true;
            }
            return false;
        }
    }
}
