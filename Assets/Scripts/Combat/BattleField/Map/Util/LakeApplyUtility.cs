using UnityEngine;

namespace WarSimulation.Combat.Map
{
    public static class LakeApplyUtility
    {
        public static void Apply(
            MapData map,
            LakeStampShape shape,
            StampPlacement placement,
            bool isFrozen)
        {
            if (map == null || shape == null) return;

            int lakeCountBefore = map.Lakes.Count;
            shape.Apply(map, placement);
            if (!isFrozen || map.Lakes.Count <= lakeCountBefore) return;

            int idx = map.Lakes.Count - 1;
            LakeRegion region = map.Lakes[idx];
            map.Lakes[idx] = new LakeRegion(
                region.Center,
                region.Radius,
                region.WaterY,
                isFrozen: true,
                waterTaggedRadius: region.WaterTaggedRadius,
                noiseAmplitude: region.NoiseAmplitude,
                noiseFrequency: region.NoiseFrequency);
        }
    }
}
