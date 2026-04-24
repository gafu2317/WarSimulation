using UnityEngine;

namespace WarSimulation.Combat.Map
{
    /// <summary>
    /// 湖フェーズ：Config 指定の LakeStampShape リストから、
    /// ランダムな位置に指定個数の湖を配置する。
    ///
    /// 実行順は River の後・Biome の前が望ましい：
    ///   - River の後：川と被った場合でも Min 合成で破綻しない
    ///   - GroundPatch の前：GroundPatchStampShape は Water を上書きしないので、湖の形が残る
    /// </summary>
    public sealed class LakePhase : IMapGenerationPhase
    {
        public void Execute(MapData map, IRandom rng, MapGenerationConfig config)
        {
            if (map == null || rng == null || config == null) return;

            var stamps = config.LakeStamps;
            if (stamps == null || stamps.Count == 0) return;
            if (config.LakeCount <= 0) return;

            float minCenter = config.LakePlacementMargin;
            float maxCenter = config.WorldSize - config.LakePlacementMargin;
            if (maxCenter <= minCenter)
            {
                minCenter = maxCenter = config.WorldSize * 0.5f;
            }

            int maxAttempts = Mathf.Max(1, config.LakeMaxPlacementAttempts);
            float clearance = Mathf.Max(0f, config.LakeRiverClearance);

            for (int i = 0; i < config.LakeCount; i++)
            {
                LakeStampShape shape = stamps[rng.NextInt(0, stamps.Count)];
                if (shape == null) continue;

                // 川（および既に置かれた湖）と重ならない中心を棄却サンプリングで探す。
                // GroundStateGrid の Water セルは RiverPhase／前回までの LakePhase の両方が書き込んでいるので、
                // これ 1 本で「川 vs 湖」「湖 vs 湖」両方の重なりを防げる。
                float checkRadius = shape.Radius + clearance;
                Vector2 center = default;
                bool found = false;
                for (int attempt = 0; attempt < maxAttempts; attempt++)
                {
                    Vector2 candidate = new(
                        Mathf.Lerp(minCenter, maxCenter, rng.NextFloat()),
                        Mathf.Lerp(minCenter, maxCenter, rng.NextFloat()));
                    if (!map.GroundStates.HasAnyCellInCircle(candidate, checkRadius, GroundState.Water))
                    {
                        center = candidate;
                        found = true;
                        break;
                    }
                }
                if (!found) continue; // 置く余地が無い：この湖はスキップ

                // 先に確率を引いておく（スタンプの適用順と無関係に、湖ごとに 1 回引く）。
                bool freeze = config.LakeFreezeProbability > 0f
                    && rng.NextFloat() < config.LakeFreezeProbability;

                int lakeCountBefore = map.Lakes.Count;
                shape.Apply(map, new StampPlacement(center));

                // LakeStampShape 側は凍結を知らないので、ここで直近に追加された LakeRegion を
                // 凍結フラグ付きで差し替える。スタンプが何らかの理由で追加しなかった場合はスキップ。
                if (freeze && map.Lakes.Count > lakeCountBefore)
                {
                    int idx = map.Lakes.Count - 1;
                    LakeRegion r = map.Lakes[idx];
                    map.Lakes[idx] = new LakeRegion(
                        r.Center,
                        r.Radius,
                        r.WaterY,
                        isFrozen: true,
                        waterTaggedRadius: r.WaterTaggedRadius);
                    FlattenFrozenLakeHeights(map, map.Lakes[idx]);
                }
            }
        }

        /// <summary>
        /// 凍結湖の Water セル高さを水面 Y に揃え、HeightMap プレビューと Terrain が「平らな氷面」になるようにする。
        /// </summary>
        private static void FlattenFrozenLakeHeights(MapData map, LakeRegion lake)
        {
            if (!lake.IsFrozen || map == null) return;

            HeightMap h = map.Height;
            GroundStateGrid g = map.GroundStates;
            float cs = h.CellSize;
            float r = lake.WaterTaggedRadius;
            float rSq = r * r;
            int cx = Mathf.FloorToInt(lake.Center.x / cs);
            int cz = Mathf.FloorToInt(lake.Center.y / cs);
            int cellR = Mathf.CeilToInt(r / cs);
            float iceY = lake.WaterY;

            for (int dz = -cellR; dz <= cellR; dz++)
            {
                for (int dx = -cellR; dx <= cellR; dx++)
                {
                    int x = cx + dx;
                    int z = cz + dz;
                    if (!h.IsInBounds(x, z)) continue;

                    float wx = (x + 0.5f) * cs - lake.Center.x;
                    float wz = (z + 0.5f) * cs - lake.Center.y;
                    if (wx * wx + wz * wz > rSq) continue;
                    if (g.GetCell(x, z) != GroundState.Water) continue;

                    h.SetHeight(x, z, iceY);
                }
            }
        }
    }
}
