using System;
using System.Collections.Generic;
using UnityEngine;

namespace WarSimulation.Combat.Map
{
    public static class InitialSpawnPositionBaker
    {
        public const int PositionsPerTeam = 10;
        public const float FlatCellMaxSlopeDeg = 8f;
        public const float CharacterSpacingDistance = 1.5f;
        public const float FeatureClearanceDistance = 3f;

        public static Vector3[] Build(MapData map, FeatureType anchorType)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            if (!TryFindAnchor(map.Features, anchorType, out Vector3 anchor)) return Array.Empty<Vector3>();

            return Build(map, anchor);
        }

        public static Vector3[] Build(
            MapData map,
            Vector3 anchor,
            bool requireFlatTerrain = true)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));

            GroundStateGrid ground = map.GroundStates;
            HeightMap height = map.Height;
            var candidates = new List<Vector3>();
            for (int z = 0; z < ground.Height; z++)
            {
                for (int x = 0; x < ground.Width; x++)
                {
                    if (ground.GetCell(x, z) == GroundState.Water) continue;
                    if (requireFlatTerrain && height.IsCliffFaceCell(x, z)) continue;

                    Vector3 position = GetCellCenter(ground, height, x, z);
                    if (requireFlatTerrain && height.SampleSlopeDeg(position) > FlatCellMaxSlopeDeg)
                        continue;
                    if (!IsClearOfFeatures(map.Features, position)) continue;
                    candidates.Add(position);
                }
            }

            candidates.Sort((a, b) =>
                HorizontalDistanceSqr(anchor, a).CompareTo(HorizontalDistanceSqr(anchor, b)));
            return requireFlatTerrain
                ? SelectSpacedPositions(candidates)
                : candidates.ToArray();
        }

        private static Vector3[] SelectSpacedPositions(List<Vector3> candidates)
        {
            var selected = new List<Vector3>(PositionsPerTeam);
            float spacingSqr = CharacterSpacingDistance * CharacterSpacingDistance;
            for (int i = 0; i < candidates.Count && selected.Count < PositionsPerTeam; i++)
            {
                Vector3 candidate = candidates[i];
                bool hasSpace = true;
                for (int p = 0; p < selected.Count; p++)
                {
                    if (HorizontalDistanceSqr(candidate, selected[p]) >= spacingSqr) continue;
                    hasSpace = false;
                    break;
                }

                if (hasSpace) selected.Add(candidate);
            }

            return selected.ToArray();
        }

        private static bool TryFindAnchor(
            IReadOnlyList<PlacedFeature> features,
            FeatureType anchorType,
            out Vector3 anchor)
        {
            for (int i = 0; i < features.Count; i++)
            {
                if (features[i].Type != anchorType) continue;
                anchor = features[i].WorldPosition;
                return true;
            }

            anchor = default;
            return false;
        }

        private static bool IsClearOfFeatures(IReadOnlyList<PlacedFeature> features, Vector3 position)
        {
            float clearanceSqr = FeatureClearanceDistance * FeatureClearanceDistance;
            for (int i = 0; i < features.Count; i++)
            {
                PlacedFeature feature = features[i];
                if (feature.Type == FeatureType.Bridge) continue;
                if (HorizontalDistanceSqr(position, feature.WorldPosition) < clearanceSqr) return false;
            }

            return true;
        }

        private static Vector3 GetCellCenter(
            GroundStateGrid ground,
            HeightMap height,
            int x,
            int z)
        {
            float cellSize = ground.CellSize;
            var position = new Vector3((x + 0.5f) * cellSize, 0f, (z + 0.5f) * cellSize);
            position.y = height.SampleAt(position);
            return position;
        }

        private static float HorizontalDistanceSqr(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return dx * dx + dz * dz;
        }
    }
}
