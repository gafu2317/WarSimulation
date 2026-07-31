using UnityEngine;

namespace WarSimulation.Combat.Map
{
    /// <summary>
    /// 配置済み橋（<see cref="FeatureType.Bridge"/>）のフットプリント判定。
    /// BridgeRenderer と同規約：local +X = 幅、local +Z = 川渡り方向の長さ。
    /// </summary>
    public static class BridgePlacementUtility
    {
        public static bool IsNearAnyBridge(MapData map, Vector2 worldXZ, float marginMeters)
        {
            if (map == null || marginMeters < 0f) return false;

            var features = map.Features;
            for (int i = 0; i < features.Count; i++)
            {
                PlacedFeature feature = features[i];
                if (feature.Type != FeatureType.Bridge) continue;
                if (IsInsideExpandedFootprint(feature, worldXZ, marginMeters)) return true;
            }

            return false;
        }

        private static bool IsInsideExpandedFootprint(PlacedFeature feature, Vector2 worldXZ, float marginMeters)
        {
            float halfWidth = Mathf.Max(0f, feature.Scale.x) * 0.5f + marginMeters;
            float halfLength = Mathf.Max(0f, feature.Scale.z) * 0.5f + marginMeters;
            if (halfWidth <= 0f || halfLength <= 0f) return false;

            Vector3 center = feature.WorldPosition;
            Vector3 local = Quaternion.Inverse(feature.Rotation) * (new Vector3(worldXZ.x, center.y, worldXZ.y) - center);
            return Mathf.Abs(local.x) <= halfWidth && Mathf.Abs(local.z) <= halfLength;
        }
    }
}
