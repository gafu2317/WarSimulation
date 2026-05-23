using System.Collections.Generic;
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

        public static bool IsFarEnoughFromPlaced(
            Vector2 center,
            float newExtent,
            List<Vector2> placedCenters,
            List<float> placedExtents,
            float extraSeparation,
            float distanceFactor)
        {
            for (int i = 0; i < placedCenters.Count; i++)
            {
                float need = distanceFactor * (placedExtents[i] + newExtent) + extraSeparation;
                if (need <= 0f) continue;
                if ((placedCenters[i] - center).sqrMagnitude < need * need)
                    return false;
            }
            return true;
        }

        public static Vector2 NormalizedToWorld(Vector2 normalized, float worldSize)
        {
            return new Vector2(
                Mathf.Clamp01(normalized.x) * worldSize,
                Mathf.Clamp01(normalized.y) * worldSize);
        }
    }
}
