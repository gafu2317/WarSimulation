using UnityEngine;

namespace WarSimulation.Combat.Map
{
    internal static class HeightStampPlacementUtility
    {
        public static float ComputeExtent(HeightStampShape shape, Vector2 scale)
        {
            if (shape == null) return 0f;

            float scaleMax = Mathf.Max(scale.x, scale.y);
            float noiseExpand = 1f + shape.NoiseAmplitude;
            if (shape.Kind == HeightShapeKind.Ridge)
                return (shape.Radius * noiseExpand + shape.RidgeLength * 0.5f) * scaleMax;
            return shape.Radius * noiseExpand * scaleMax;
        }
    }
}
