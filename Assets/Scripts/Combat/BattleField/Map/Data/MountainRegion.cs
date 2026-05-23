using UnityEngine;

namespace WarSimulation.Combat.Map
{
    /// <summary>
    /// 生成済みの山スタンプ配置メタデータ。
    /// 川生成などの後続フェーズは HeightMap に加えてこの情報を参照できる。
    /// </summary>
    public readonly struct MountainRegion
    {
        public MountainKind Kind { get; }
        public Vector2 Center { get; }
        public float Extent { get; }
        public Vector2 Scale { get; }
        public float RotationRad { get; }
        public HeightStampShape Shape { get; }

        public MountainRegion(
            MountainKind kind,
            Vector2 center,
            float extent,
            Vector2 scale,
            float rotationRad,
            HeightStampShape shape)
        {
            Kind = kind;
            Center = center;
            Extent = Mathf.Max(0f, extent);
            Scale = scale;
            RotationRad = rotationRad;
            Shape = shape;
        }
    }
}
