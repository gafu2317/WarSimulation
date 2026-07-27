using UnityEngine;

namespace WarSimulation.Combat.Map
{
    public static class MountainApplyUtility
    {
        public static void Apply(
            MapData map,
            MountainKind kind,
            HeightStampShape shape,
            StampPlacement placement)
        {
            if (map == null || shape == null) return;

            float extent = HeightStampPlacementUtility.ComputeExtent(shape, placement.Scale);
            shape.Apply(map, placement);
            map.AddMountain(new MountainRegion(
                kind,
                placement.Center,
                extent,
                placement.Scale,
                placement.RotationRad,
                shape));
        }
    }
}
