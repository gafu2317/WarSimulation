using System.Collections.Generic;
using UnityEngine;

namespace WarSimulation.Combat.Map
{
    /// <summary>
    /// 手作りマップ資産を決定的に <see cref="MapData"/> へ展開する。
    /// 適用順は自動生成パイプラインと同じ依存関係を守る。
    /// </summary>
    public static class AuthoredMapBuilder
    {
        public static MapData Build(AuthoredMapDefinition definition)
        {
            if (definition == null)
                throw new System.ArgumentNullException(nameof(definition));
            if (definition.SharedConfig == null)
                throw new System.InvalidOperationException(
                    $"{nameof(AuthoredMapDefinition)}.{nameof(AuthoredMapDefinition.SharedConfig)} is not assigned.");

            MapGenerationConfig config = definition.SharedConfig;
            MapData map = MapDataFactory.CreateFlatMap(config, definition.BuildSeed);
            map.BridgeFeatureExclusionMargin = config.BridgeFeatureExclusionMargin;

            ApplyMountains(map, definition.Mountains);
            ApplyRivers(map, definition.Rivers, config, definition.BuildSeed);
            ApplyLakes(map, definition.Lakes);
            ApplyGroundPatches(map, definition.GroundPatches);
            ApplyForests(map, definition.Forests);
            ApplyBridges(map, definition.Bridges, config);

            // 散布木・岩は自動生成と同じルール。魔石は手作り配置。
            IRandom rng = new SystemRandom(definition.BuildSeed);
            new TreeScatterPhase().Execute(map, rng, config);
            new RockPhase().Execute(map, rng, config);
            ApplyMagicStones(map, definition.MagicStones);
            return map;
        }

        private static void ApplyMountains(MapData map, List<AuthoredMountainPlacement> mountains)
        {
            if (mountains == null) return;
            for (int i = 0; i < mountains.Count; i++)
            {
                AuthoredMountainPlacement entry = mountains[i];
                if (entry?.Shape == null) continue;
                MountainApplyUtility.Apply(map, entry.Kind, entry.Shape, entry.ToStampPlacement());
            }
        }

        private static void ApplyRivers(
            MapData map,
            List<AuthoredRiverPlacement> rivers,
            MapGenerationConfig config,
            int buildSeed)
        {
            if (rivers == null) return;

            var builder = new FlatRiverPathBuilder();
            for (int i = 0; i < rivers.Count; i++)
            {
                AuthoredRiverPlacement entry = rivers[i];
                if (entry?.ControlPoints == null || entry.ControlPoints.Count < 2) continue;

                RiverShape shape = entry.Shape != null ? entry.Shape : config.RiverShape;
                if (shape == null) continue;

                if (!entry.TryGetBezier(out Vector2 start, out Vector2 control, out Vector2 end)) continue;

                Vector2Int startCell = RiverPathRasterizer.WorldToCell(map.Height, start);
                Vector2Int endCell = RiverPathRasterizer.WorldToCell(map.Height, end);
                float noiseSeed = HashRiverNoiseSeed(buildSeed, i, startCell, endCell);

                List<Vector2Int> path = builder.BuildWithBezierControl(
                    map.Height,
                    startCell,
                    endCell,
                    control,
                    config.FlatRiverMeanderAmplitude,
                    config.FlatRiverMeanderFrequency,
                    noiseSeed);

                if (path == null || path.Count < 2) continue;

                // 端点はオーサリングしたセルに固定する。
                path[0] = startCell;
                path[path.Count - 1] = endCell;
                RiverBuildUtility.CarveAndRegister(map, shape, path);
            }
        }

        public static float HashRiverNoiseSeed(
            int buildSeed,
            int riverIndex,
            Vector2Int start,
            Vector2Int end)
        {
            unchecked
            {
                uint h = (uint)buildSeed;
                h = h * 747796405u + (uint)riverIndex * 2891336453u;
                h = h * 747796405u + (uint)start.x * 16777619u;
                h = h * 747796405u + (uint)start.y * 2166136261u;
                h = h * 747796405u + (uint)end.x * 16777619u;
                h = h * 747796405u + (uint)end.y * 2166136261u;
                // [0, 1000) に収めて FlatRiverPathBuilder のノイズオフセットにする。
                return (h & 0x7fffffffu) % 100000u / 100f;
            }
        }

        private static void ApplyLakes(MapData map, List<AuthoredLakePlacement> lakes)
        {
            if (lakes == null) return;
            for (int i = 0; i < lakes.Count; i++)
            {
                AuthoredLakePlacement entry = lakes[i];
                if (entry?.Shape == null) continue;
                LakeApplyUtility.Apply(map, entry.Shape, entry.ToStampPlacement(), entry.IsFrozen);
            }
        }

        private static void ApplyGroundPatches(MapData map, List<AuthoredGroundPatchPlacement> patches)
        {
            if (patches == null) return;
            for (int i = 0; i < patches.Count; i++)
            {
                AuthoredGroundPatchPlacement entry = patches[i];
                if (entry?.Shape == null) continue;
                entry.Shape.Apply(map, entry.ToStampPlacement());
            }
        }

        private static void ApplyForests(MapData map, List<AuthoredForestPlacement> forests)
        {
            if (forests == null) return;
            for (int i = 0; i < forests.Count; i++)
            {
                AuthoredForestPlacement entry = forests[i];
                if (entry?.Shape == null) continue;
                entry.Shape.Apply(map, entry.ToStampPlacement());
            }
        }

        private static void ApplyBridges(
            MapData map,
            List<AuthoredBridgePlacement> bridges,
            MapGenerationConfig config)
        {
            if (bridges == null) return;

            float defaultDepth = config.RiverShape != null ? config.RiverShape.DepthMeters : 0.6f;
            Vector3 fallbackScale = BridgeBuildUtility.DefaultBridgeScale(config, riverWidthMeters: 0f);

            for (int i = 0; i < bridges.Count; i++)
            {
                AuthoredBridgePlacement entry = bridges[i];
                if (entry == null) continue;

                Vector3 scale = IsValidScale(entry.Scale)
                    ? entry.Scale
                    : fallbackScale;
                float depth = BridgeBuildUtility.FindNearestRiverDepth(map, entry.Center, defaultDepth);
                float y = BridgeBuildUtility.ResolveBridgeY(config, depth);
                map.AddFeature(new PlacedFeature(
                    FeatureType.Bridge,
                    new Vector3(entry.Center.x, y, entry.Center.y),
                    Quaternion.Euler(0f, entry.RotationDeg, 0f),
                    scale));
            }
        }

        private static void ApplyMagicStones(MapData map, List<AuthoredMagicStonePlacement> stones)
        {
            if (stones == null) return;
            for (int i = 0; i < stones.Count; i++)
            {
                AuthoredMagicStonePlacement entry = stones[i];
                if (entry == null || !IsMagicStoneType(entry.Type)) continue;
                float y = map.Height.SampleAt(new Vector3(entry.Center.x, 0f, entry.Center.y));
                map.AddFeature(new PlacedFeature(
                    entry.Type,
                    new Vector3(entry.Center.x, y, entry.Center.y),
                    Quaternion.identity));
            }
        }

        private static bool IsValidScale(Vector3 scale) =>
            scale.x > 0.0001f && scale.y > 0.0001f && scale.z > 0.0001f;

        public static bool IsMagicStoneType(FeatureType type) =>
            type == FeatureType.OwnMainStone
            || type == FeatureType.OwnSubStone
            || type == FeatureType.EnemyMainStone
            || type == FeatureType.EnemySubStone;
    }
}
