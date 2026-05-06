using UnityEngine;

namespace WarSimulation.Combat.Map
{
    /// <summary>
    /// 魔石や装飾オブジェクトなど、単一座標に配置される設置物の情報。
    /// 不変な値オブジェクトとして扱う。
    /// </summary>
    public readonly struct PlacedFeature
    {
        public FeatureType Type { get; }
        public Vector3 WorldPosition { get; }
        public Quaternion Rotation { get; }
        public Vector3 Scale { get; }

        public PlacedFeature(FeatureType type, Vector3 worldPosition, Quaternion rotation)
            : this(type, worldPosition, rotation, Vector3.one) { }

        public PlacedFeature(FeatureType type, Vector3 worldPosition, Quaternion rotation, Vector3 scale)
        {
            Type = type;
            WorldPosition = worldPosition;
            Rotation = rotation;
            Scale = scale;
        }

        public PlacedFeature(FeatureType type, Vector3 worldPosition)
            : this(type, worldPosition, Quaternion.identity) { }
    }
}
