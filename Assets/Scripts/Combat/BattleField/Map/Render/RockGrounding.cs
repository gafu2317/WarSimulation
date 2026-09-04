using System.Collections.Generic;
using UnityEngine;

namespace WarSimulation.Combat.Map
{
    internal static class RockGrounding
    {
        internal static bool TryGround(
            Transform target,
            Transform contactRoot,
            Transform mapSpace,
            TerrainCollider ground,
            float cellSize,
            float minimumSink,
            out string error)
        {
            error = null;
            Collider[] colliders = contactRoot.GetComponentsInChildren<Collider>();
            var active = new List<Collider>();
            var xs = new SortedSet<float>();
            var zs = new SortedSet<float>();
            Bounds bounds = default;
            foreach (Collider collider in colliders)
            {
                if (!collider.enabled || collider.isTrigger) continue;
                Bounds local = ToLocalBounds(mapSpace, collider.bounds);
                if (active.Count == 0) bounds = local;
                else bounds.Encapsulate(local);
                active.Add(collider);
                Vector3 center = mapSpace.InverseTransformPoint(collider.bounds.center);
                xs.Add(center.x);
                zs.Add(center.z);
            }

            if (active.Count == 0)
            {
                error = "接地判定用Colliderがありません";
                return false;
            }

            AddGrid(xs, bounds.min.x, bounds.max.x, cellSize);
            AddGrid(zs, bounds.min.z, bounds.max.z, cellSize);
            Bounds terrainBounds = ToLocalBounds(mapSpace, ground.bounds);
            float bottom = Mathf.Min(bounds.min.y, terrainBounds.min.y) - cellSize;
            float top = Mathf.Max(bounds.max.y, terrainBounds.max.y) + cellSize;
            Vector3 vertical = mapSpace.TransformVector(Vector3.up);
            float rayLength = (top - bottom) * vertical.magnitude;
            Vector3 up = vertical.normalized;
            Vector3 origin = mapSpace.InverseTransformPoint(target.position);
            var referenceRay = new Ray(mapSpace.TransformPoint(new Vector3(origin.x, top, origin.z)), -up);
            if (!ground.Raycast(referenceRay, out RaycastHit referenceHit, rayLength))
            {
                error = "配置位置にTerrainがありません";
                return false;
            }
            float referenceHeight = mapSpace.InverseTransformPoint(referenceHit.point).y;
            float sink = minimumSink;
            bool hasContact = false;

            foreach (float x in xs)
            foreach (float z in zs)
            {
                var upwardRay = new Ray(mapSpace.TransformPoint(new Vector3(x, bottom, z)), up);
                bool hitsRock = false;
                foreach (Collider collider in active)
                {
                    if (!collider.Raycast(upwardRay, out _, rayLength)) continue;
                    hitsRock = true;
                    break;
                }
                if (!hitsRock) continue;

                var downwardRay = new Ray(mapSpace.TransformPoint(new Vector3(x, top, z)), -up);
                if (!ground.Raycast(downwardRay, out RaycastHit groundHit, rayLength))
                {
                    error = "底面直下にTerrainがありません";
                    return false;
                }

                hasContact = true;
                float height = mapSpace.InverseTransformPoint(groundHit.point).y;
                // 底面自体の高さを基準にすると、形状の丸みまで地中に埋めてしまう。
                sink = Mathf.Max(sink, referenceHeight - height);
            }

            if (!hasContact)
            {
                error = "底面に接地点が見つかりません";
                return false;
            }

            Vector3 position = target.localPosition;
            position.y -= sink;
            target.localPosition = position;
            return true;
        }

        private static void AddGrid(SortedSet<float> values, float min, float max, float step)
        {
            int count = Mathf.CeilToInt((max - min) / step);
            for (int i = 0; i < count; i++) values.Add(min + i * step);
            values.Add(max);
        }

        private static Bounds ToLocalBounds(Transform space, Bounds bounds)
        {
            var result = new Bounds(space.InverseTransformPoint(bounds.min), Vector3.zero);
            for (int x = 0; x < 2; x++)
            for (int y = 0; y < 2; y++)
            for (int z = 0; z < 2; z++)
            {
                Vector3 corner = new Vector3(
                    x == 0 ? bounds.min.x : bounds.max.x,
                    y == 0 ? bounds.min.y : bounds.max.y,
                    z == 0 ? bounds.min.z : bounds.max.z);
                result.Encapsulate(space.InverseTransformPoint(corner));
            }
            return result;
        }
    }
}
