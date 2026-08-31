using System;
using UnityEngine;

namespace WarSimulation.Combat.Map
{
    internal sealed class PlacementCandidates
    {
        private readonly Rect _area;
        private readonly Vector2 _cellSize;
        private readonly int _width;
        private readonly int _depth;
        private readonly Vector2[] _positions;
        private readonly int[] _active;
        private readonly int[] _slots;
        private readonly float _spacing;
        private int _count;

        public PlacementCandidates(MapData map, FeatureType type, Rect area, float minimumDistance, IRandom rng)
        {
            _area = area;
            // 地形より細かい候補や、新しい密度設定を増やさず既存の配置間隔を使う。
            float step = Mathf.Max(map.Height.CellSize, minimumDistance);
            _width = Mathf.Max(1, Mathf.CeilToInt(area.width / step));
            _depth = Mathf.Max(1, Mathf.CeilToInt(area.height / step));
            // 端だけ狭い区画を残すと、単位面積あたりの候補数が端で増えてしまう。
            _cellSize = new Vector2(area.width > 0f ? area.width / _width : step,
                area.height > 0f ? area.height / _depth : step);
            _count = _width * _depth;
            _positions = new Vector2[_count];
            _active = new int[_count];
            _slots = new int[_count];
            var radii = map.PlacementRadii;
            float selfRadius = radii.Radius(type, type);
            _spacing = Mathf.Max(minimumDistance, selfRadius > 0f ? selfRadius * 2f + radii.Clearance : 0f);

            for (int z = 0; z < _depth; z++)
            for (int x = 0; x < _width; x++)
            {
                int id = z * _width + x;
                float left = area.xMin + x * _cellSize.x;
                float bottom = area.yMin + z * _cellSize.y;
                _positions[id] = new Vector2(
                    Mathf.Lerp(left, Mathf.Min(left + _cellSize.x, area.xMax), rng.NextFloat()),
                    Mathf.Lerp(bottom, Mathf.Min(bottom + _cellSize.y, area.yMax), rng.NextFloat()));
                _active[id] = id;
                _slots[id] = id;
            }

            foreach (PlacedFeature feature in map.Features)
            {
                var center = new Vector2(feature.WorldPosition.x, feature.WorldPosition.z);
                if (feature.Type == FeatureType.Bridge)
                {
                    float margin = map.BridgeFeatureExclusionMargin + radii.Radius(type, feature.Type) + radii.Clearance;
                    float reach = new Vector2(feature.Scale.x * 0.5f + margin, feature.Scale.z * 0.5f + margin).magnitude;
                    ExcludeNear(center, reach, p => BridgePlacementUtility.IsInsideExpandedFootprint(feature, p, margin));
                    continue;
                }
                float own = radii.Radius(type, feature.Type);
                float other = radii.Radius(feature.Type, type);
                float distance = own > 0f && other > 0f ? own + other + radii.Clearance : 0f;
                if (feature.Type == type) distance = Mathf.Max(distance, minimumDistance);
                ExcludeCircle(center, distance);
            }
        }

        public void KeepWhere(Func<Vector2, bool> isValid)
        {
            for (int slot = _count - 1; slot >= 0; slot--)
                if (!isValid(_positions[_active[slot]])) Remove(_active[slot]);
        }

        public bool TryTake(IRandom rng, out Vector2 position)
        {
            position = default;
            if (_count == 0) return false;
            int id = _active[rng.NextInt(0, _count)];
            position = _positions[id];
            Remove(id);
            ExcludeCircle(position, _spacing);
            return true;
        }

        public void ExcludeCircle(Vector2 center, float radius)
        {
            if (radius <= 0f) return;
            float squared = radius * radius;
            ExcludeNear(center, radius, p => (p - center).sqrMagnitude < squared);
        }

        private void ExcludeNear(Vector2 center, float radius, Func<Vector2, bool> overlaps)
        {
            int minX = Mathf.Max(0, Mathf.FloorToInt((center.x - radius - _area.xMin) / _cellSize.x));
            int maxX = Mathf.Min(_width - 1, Mathf.FloorToInt((center.x + radius - _area.xMin) / _cellSize.x));
            int minZ = Mathf.Max(0, Mathf.FloorToInt((center.y - radius - _area.yMin) / _cellSize.y));
            int maxZ = Mathf.Min(_depth - 1, Mathf.FloorToInt((center.y + radius - _area.yMin) / _cellSize.y));
            for (int z = minZ; z <= maxZ; z++)
            for (int x = minX; x <= maxX; x++)
            {
                int id = z * _width + x;
                if (_slots[id] >= 0 && overlaps(_positions[id])) Remove(id);
            }
        }

        private void Remove(int id)
        {
            int slot = _slots[id];
            int last = _active[--_count];
            _active[slot] = last;
            _slots[last] = slot;
            _slots[id] = -1;
        }
    }
}
