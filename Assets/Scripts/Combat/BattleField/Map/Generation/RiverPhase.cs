using System.Collections.Generic;
using UnityEngine;

namespace WarSimulation.Combat.Map
{
    /// <summary>
    /// 川フェーズ：対向するマップ端 → マップ端を Perlin ノイズで蛇行させる大河を掘る。
    /// 辺は「上⇔下」か「左⇔右」のどちらかをランダムに選び、必ずマップを横断させる。
    /// 隣接辺（角同士）を選ぶと短い川やマップ端沿いの川になりがちなため、ここでは採用しない。
    ///
    /// パイプライン上 RiverPhase は StructurePhase より前に走り、HeightMap は
    /// BaseHeight（通常 0）で一様。高度探索は行わず、ノイズ蛇行で経路を決める。
    /// 山はこの後 StructurePhase が「川セルを避けて」置くことで、
    /// 「川と山が被らない」制約を物理ではなく配置ルールで担保している。
    /// </summary>
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

        /// <summary>
        /// マップ端 → 別のマップ端を「高度非依存のノイズ蛇行」で引く横断大河。
        /// 辺の組み合わせによっては短い経路しか引けないケースがあるため、
        /// RiverMinPathLength を満たすまで最大 N 回リトライ。全試行失敗時は最長候補にフォールバック。
        /// </summary>
        private static void GenerateFlatCrossMapRiver(
            MapData map, IRandom rng, MapGenerationConfig config, FlatRiverPathBuilder builder)
        {
            HeightMap h = map.Height;
            const int maxAttempts = 10;

            List<Vector2Int> bestPath = null;
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                // 縦断（上⇔下）か横断（左⇔右）を毎回ランダムに選ぶ。常にマップを横切るので
                // 角にちょこんと出る極端に短い川が生まれない。
                bool horizontal = rng.NextInt(0, 2) == 0;
                int startEdge = horizontal ? 2 : 0;
                int endEdge = horizontal ? 3 : 1;

                Vector2Int start = PickRandomEdgeCellOn(h, rng, startEdge);
                Vector2Int end = PickRandomEdgeCellOn(h, rng, endEdge);

                float noiseSeed = rng.NextFloat() * 1000f;
                List<Vector2Int> path = builder.Build(
                    h, start, end,
                    config.FlatRiverMeanderAmplitude,
                    config.FlatRiverMeanderFrequency,
                    noiseSeed);

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

        private static Vector2Int PickRandomEdgeCellOn(HeightMap h, IRandom rng, int edge)
        {
            return edge switch
            {
                0 => new Vector2Int(rng.NextInt(0, h.Width), 0),
                1 => new Vector2Int(rng.NextInt(0, h.Width), h.Height - 1),
                2 => new Vector2Int(0, rng.NextInt(0, h.Height)),
                _ => new Vector2Int(h.Width - 1, rng.NextInt(0, h.Height)),
            };
        }
    }
}
