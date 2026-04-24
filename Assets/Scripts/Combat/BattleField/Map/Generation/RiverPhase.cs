using System.Collections.Generic;
using UnityEngine;

namespace WarSimulation.Combat.Map
{
    public sealed class RiverPhase : IMapGenerationPhase
    {
        public void Execute(MapData map, IRandom rng, MapGenerationConfig config)
        {
            if (map == null || rng == null || config == null) return;
            if (config.RiverShape == null) return;
            if (config.CrossMapRiverCount <= 0) return;

            var builder = new FlatRiverPathBuilder();
            for (int i = 0; i < config.CrossMapRiverCount; i++)
            {
                GenerateFlatCrossMapRiver(map, rng, config, builder);
            }
        }

        private static void GenerateFlatCrossMapRiver(
            MapData map, IRandom rng, MapGenerationConfig config, FlatRiverPathBuilder builder)
        {
            HeightMap h = map.Height;
            const int maxAttempts = 10;

            List<Vector2Int> bestPath = null;
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                Vector2Int start = PickRandomPerimeterCell(h, rng);
                Vector2Int end = PickEndEdgeCellFarFrom(h, rng, start);

                float noiseSeed = rng.NextFloat() * 1000f;
                List<Vector2Int> path = builder.Build(
                    h, start, end,
                    config.FlatRiverMeanderAmplitude,
                    config.FlatRiverMeanderFrequency,
                    noiseSeed,
                    spineCurveBendMeters: config.FlatRiverSpineCurveBend);

                if (path.Count >= config.RiverMinPathLength)
                {
                    bestPath = path;
                    break;
                }
                if (bestPath == null || path.Count > bestPath.Count) bestPath = path;
            }

            if (bestPath == null || bestPath.Count < 2) return;

            config.RiverShape.Carve(map, bestPath);
            map.AddRiver(new RiverPath(
                bestPath,
                config.RiverShape.WidthMeters,
                config.RiverShape.DepthMeters));
        }

        /// <summary>
        /// startからの距離がWidth*1.2以上の外周セルの中からランダムに1点選ぶ。
        /// 条件を満たすセルがない場合は最遠セルにフォールバック。
        /// </summary>
        private static Vector2Int PickEndEdgeCellFarFrom(HeightMap h, IRandom rng, Vector2Int start)
        {
            float minDistSq = h.Width * 1.2f;
            minDistSq *= minDistSq;

            int perimeter = 4 * (h.Width - 1);

            var validRanges = new List<(int start, int length)>();
            int? rangeStart = null;

            Vector2Int farthestCell = start;
            float farthestDistSq = -1f;

            for (int i = 0; i <= perimeter; i++)
            {
                bool valid = false;
                if (i < perimeter)
                {
                    Vector2Int cell = PerimeterCellFromIndex(h, i);
                    float dSq = (cell - start).sqrMagnitude;

                    if (dSq > farthestDistSq)
                    {
                        farthestDistSq = dSq;
                        farthestCell = cell;
                    }

                    valid = dSq >= minDistSq;
                }

                if (valid && rangeStart == null)
                    rangeStart = i;
                else if (!valid && rangeStart != null)
                {
                    validRanges.Add((rangeStart.Value, i - rangeStart.Value));
                    rangeStart = null;
                }
            }

            if (validRanges.Count == 0)
                return farthestCell;

            int totalValid = 0;
            foreach (var r in validRanges) totalValid += r.length;

            int pick = rng.NextInt(0, totalValid);
            foreach (var (s, length) in validRanges)
            {
                if (pick < length)
                    return PerimeterCellFromIndex(h, s + pick);
                pick -= length;
            }

            return farthestCell; // unreachable
        }

        /// <summary>
        /// 0〜perimeter-1のインデックスを外周座標に変換する（時計回り、角の重複なし）。
        /// 上辺 → 右辺 → 下辺 → 左辺 の順。
        /// </summary>
        private static Vector2Int PerimeterCellFromIndex(HeightMap h, int idx)
        {
            int n = h.Width - 1;

            if (idx < n) return new Vector2Int(idx, 0);
            idx -= n;

            if (idx < n) return new Vector2Int(n, idx);
            idx -= n;

            if (idx < n) return new Vector2Int(n - idx, n);
            idx -= n;

            return new Vector2Int(0, n - idx);
        }

        /// <summary>
        /// マップ外周からランダムに1セル返す。
        /// </summary>
        private static Vector2Int PickRandomPerimeterCell(HeightMap h, IRandom rng)
        {
            int perimeter = 4 * (h.Width - 1);
            return PerimeterCellFromIndex(h, rng.NextInt(0, perimeter));
        }
    }
}