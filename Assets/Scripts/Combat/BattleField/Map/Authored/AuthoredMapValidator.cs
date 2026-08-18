using System.Collections.Generic;
using UnityEngine;

namespace WarSimulation.Combat.Map
{
    public readonly struct AuthoredMapValidationIssue
    {
        public string Message { get; }
        public bool IsError { get; }

        public AuthoredMapValidationIssue(string message, bool isError)
        {
            Message = message;
            IsError = isError;
        }
    }

    public static class AuthoredMapValidator
    {
        public static List<AuthoredMapValidationIssue> Validate(AuthoredMapDefinition definition)
        {
            var issues = new List<AuthoredMapValidationIssue>();
            if (definition == null)
            {
                issues.Add(Error("Authored map definition is null."));
                return issues;
            }

            MapConfig config = definition.SharedConfig;
            if (config == null)
            {
                issues.Add(Error("SharedConfig is not assigned."));
                return issues;
            }

            float world = config.WorldSize;
            ValidateMountains(definition.Mountains, world, issues);
            ValidateRivers(definition.Rivers, config, world, issues);
            ValidateLakes(definition.Lakes, world, issues);
            ValidateGroundPatches(definition.GroundPatches, world, issues);
            ValidateForests(definition.Forests, world, issues);
            ValidateBridges(definition.Bridges, world, issues);
            ValidateMagicStones(definition.MagicStones, config, world, issues);
            ValidateAssaultRoutes(definition.AssaultRoutes, world, issues);
            return issues;
        }

        private static void ValidateAssaultRoutes(
            List<AuthoredAssaultRoute> routes,
            float world,
            List<AuthoredMapValidationIssue> issues)
        {
            if (routes == null || routes.Count == 0)
            {
                issues.Add(Warning("No assault routes are authored."));
                return;
            }

            var ids = new HashSet<string>();
            for (int i = 0; i < routes.Count; i++)
            {
                AuthoredAssaultRoute route = routes[i];
                if (route == null)
                {
                    issues.Add(Error($"AssaultRoute[{i}] is null."));
                    continue;
                }

                if (string.IsNullOrWhiteSpace(route.RouteId) || !ids.Add(route.RouteId))
                    issues.Add(Error($"AssaultRoute[{i}] RouteId is empty or duplicated."));
                if (route.Waypoints == null) continue;
                for (int p = 0; p < route.Waypoints.Count; p++)
                {
                    if (!IsInsideMap(route.Waypoints[p], world))
                        issues.Add(Error($"AssaultRoute[{i}] waypoint[{p}] is outside the map."));
                }
            }
        }

        public static bool HasErrors(IReadOnlyList<AuthoredMapValidationIssue> issues)
        {
            if (issues == null) return false;
            for (int i = 0; i < issues.Count; i++)
            {
                if (issues[i].IsError) return true;
            }

            return false;
        }

        private static void ValidateMountains(
            List<AuthoredMountainPlacement> mountains,
            float world,
            List<AuthoredMapValidationIssue> issues)
        {
            if (mountains == null) return;
            for (int i = 0; i < mountains.Count; i++)
            {
                AuthoredMountainPlacement entry = mountains[i];
                if (entry == null)
                {
                    issues.Add(Error($"Mountain[{i}] is null."));
                    continue;
                }

                if (entry.Shape == null)
                    issues.Add(Error($"Mountain[{i}] Shape is missing."));
                if (!IsInsideMap(entry.Center, world))
                    issues.Add(Error($"Mountain[{i}] center is outside the map."));
                if (!IsValidScale(entry.Scale))
                    issues.Add(Error($"Mountain[{i}] scale is invalid."));
            }
        }

        private static void ValidateRivers(
            List<AuthoredRiverPlacement> rivers,
            MapConfig config,
            float world,
            List<AuthoredMapValidationIssue> issues)
        {
            if (rivers == null) return;
            for (int i = 0; i < rivers.Count; i++)
            {
                AuthoredRiverPlacement entry = rivers[i];
                if (entry == null)
                {
                    issues.Add(Error($"River[{i}] is null."));
                    continue;
                }

                if (entry.Shape == null && config.RiverShape == null)
                    issues.Add(Error($"River[{i}] Shape is missing and SharedConfig.RiverShape is unset."));
                if (entry.ControlPoints == null || entry.ControlPoints.Count < 2)
                {
                    issues.Add(Warning($"River[{i}] needs at least 2 control points to carve."));
                    continue;
                }

                for (int p = 0; p < entry.ControlPoints.Count; p++)
                {
                    if (!IsInsideMap(entry.ControlPoints[p], world))
                        issues.Add(Error($"River[{i}] control point[{p}] is outside the map."));
                }

                // 端点セルが分かれることだけ確認（経路はベジェ＋meander で Builder 側）
                int resolution = config.HeightMapResolution;
                float cellSize = config.HeightMapCellSize;
                var height = new HeightMap(resolution, resolution, cellSize);
                if (!entry.TryGetEndpoints(out Vector2 start, out Vector2 end))
                    continue;
                Vector2Int startCell = RiverPathRasterizer.WorldToCell(height, start);
                Vector2Int endCell = RiverPathRasterizer.WorldToCell(height, end);
                if (startCell == endCell)
                    issues.Add(Error($"River[{i}] start and end map to the same cell."));
            }
        }

        private static void ValidateLakes(
            List<AuthoredLakePlacement> lakes,
            float world,
            List<AuthoredMapValidationIssue> issues)
        {
            if (lakes == null) return;
            for (int i = 0; i < lakes.Count; i++)
            {
                AuthoredLakePlacement entry = lakes[i];
                if (entry == null)
                {
                    issues.Add(Error($"Lake[{i}] is null."));
                    continue;
                }

                if (entry.Shape == null)
                    issues.Add(Error($"Lake[{i}] Shape is missing."));
                if (!IsInsideMap(entry.Center, world))
                    issues.Add(Error($"Lake[{i}] center is outside the map."));
            }
        }

        private static void ValidateGroundPatches(
            List<AuthoredGroundPatchPlacement> patches,
            float world,
            List<AuthoredMapValidationIssue> issues)
        {
            if (patches == null) return;
            for (int i = 0; i < patches.Count; i++)
            {
                AuthoredGroundPatchPlacement entry = patches[i];
                if (entry == null)
                {
                    issues.Add(Error($"GroundPatch[{i}] is null."));
                    continue;
                }

                if (entry.Shape == null)
                    issues.Add(Error($"GroundPatch[{i}] Shape is missing."));
                if (!IsInsideMap(entry.Center, world))
                    issues.Add(Error($"GroundPatch[{i}] center is outside the map."));
            }
        }

        private static void ValidateForests(
            List<AuthoredForestPlacement> forests,
            float world,
            List<AuthoredMapValidationIssue> issues)
        {
            if (forests == null) return;
            for (int i = 0; i < forests.Count; i++)
            {
                AuthoredForestPlacement entry = forests[i];
                if (entry == null)
                {
                    issues.Add(Error($"Forest[{i}] is null."));
                    continue;
                }

                if (entry.Shape == null)
                    issues.Add(Error($"Forest[{i}] Shape is missing."));
                if (!IsInsideMap(entry.Center, world))
                    issues.Add(Error($"Forest[{i}] center is outside the map."));
            }
        }

        private static void ValidateBridges(
            List<AuthoredBridgePlacement> bridges,
            float world,
            List<AuthoredMapValidationIssue> issues)
        {
            if (bridges == null) return;
            for (int i = 0; i < bridges.Count; i++)
            {
                AuthoredBridgePlacement entry = bridges[i];
                if (entry == null)
                {
                    issues.Add(Error($"Bridge[{i}] is null."));
                    continue;
                }

                if (!IsInsideMap(entry.Center, world))
                    issues.Add(Error($"Bridge[{i}] center is outside the map."));
            }
        }

        private static void ValidateMagicStones(
            List<AuthoredMagicStonePlacement> stones,
            MapConfig config,
            float world,
            List<AuthoredMapValidationIssue> issues)
        {
            int ownMain = 0;
            int enemyMain = 0;
            if (stones == null)
            {
                issues.Add(Warning("No magic stones are placed."));
                return;
            }

            for (int i = 0; i < stones.Count; i++)
            {
                AuthoredMagicStonePlacement entry = stones[i];
                if (entry == null)
                {
                    issues.Add(Error($"MagicStone[{i}] is null."));
                    continue;
                }

                if (!AuthoredMapBuilder.IsMagicStoneType(entry.Type))
                    issues.Add(Error($"MagicStone[{i}] has invalid FeatureType {entry.Type}."));
                if (!IsInsideMap(entry.Center, world))
                    issues.Add(Error($"MagicStone[{i}] center is outside the map."));

                if (entry.Type == FeatureType.OwnMainStone) ownMain++;
                if (entry.Type == FeatureType.EnemyMainStone) enemyMain++;
            }

            if (ownMain < Mathf.Max(1, config.MainStonesPerSide))
                issues.Add(Warning("Own main magic stone is missing or fewer than expected."));
            if (enemyMain < Mathf.Max(1, config.MainStonesPerSide))
                issues.Add(Warning("Enemy main magic stone is missing or fewer than expected."));
        }

        private static bool IsInsideMap(Vector2 center, float world) =>
            center.x >= 0f && center.y >= 0f && center.x <= world && center.y <= world;

        private static bool IsValidScale(Vector2 scale) =>
            scale.x > 0.0001f && scale.y > 0.0001f;

        private static AuthoredMapValidationIssue Error(string message) =>
            new AuthoredMapValidationIssue(message, isError: true);

        private static AuthoredMapValidationIssue Warning(string message) =>
            new AuthoredMapValidationIssue(message, isError: false);
    }
}
