#if UNITY_EDITOR
using UnityEngine;

namespace WarSimulation.Combat.Map.EditorOnly
{
    internal static class MapAuthoringPreview2D
    {
        public static Texture2D Build(MapData map) => BuildBackground(map);

        public static Texture2D BuildBackground(MapData map, bool includeTreesAndRocks = true) =>
            CombatMapPreviewTextureBuilder.BuildBackground(map, includeTreesAndRocks);

        public static bool TryMapPoint(Rect drawRect, Vector2 guiMouse, float worldSize, out Vector2 mapXZ)
        {
            mapXZ = default;
            if (drawRect.width < 1f || drawRect.height < 1f) return false;
            if (!drawRect.Contains(guiMouse)) return false;

            float u = (guiMouse.x - drawRect.x) / drawRect.width;
            float vFromTop = (guiMouse.y - drawRect.y) / drawRect.height;
            // Texture2D の y=0 はマップ z=0（南）。GUI は上が北なので反転する。
            float v = 1f - vFromTop;
            mapXZ = new Vector2(
                Mathf.Clamp(u * worldSize, 0f, worldSize),
                Mathf.Clamp(v * worldSize, 0f, worldSize));
            return true;
        }

        /// <summary>
        /// マップ外周付近のクリックを拾いやすくする。取得点はワールド座標（端スナップ前）。
        /// </summary>
        public static bool TryMapPointNearEdge(
            Rect drawRect,
            Vector2 guiMouse,
            float worldSize,
            float edgeSlopGui,
            out Vector2 mapXZ)
        {
            mapXZ = default;
            if (drawRect.width < 1f || drawRect.height < 1f) return false;

            var hit = Rect.MinMaxRect(
                drawRect.xMin - edgeSlopGui,
                drawRect.yMin - edgeSlopGui,
                drawRect.xMax + edgeSlopGui,
                drawRect.yMax + edgeSlopGui);
            if (!hit.Contains(guiMouse)) return false;

            float u = (guiMouse.x - drawRect.x) / drawRect.width;
            float vFromTop = (guiMouse.y - drawRect.y) / drawRect.height;
            float v = 1f - vFromTop;
            mapXZ = new Vector2(u * worldSize, v * worldSize);
            return true;
        }

        /// <summary>
        /// マップ矩形 [0, world]×[0, world] の最も近い辺へスナップする。
        /// </summary>
        public static Vector2 SnapToNearestEdge(Vector2 mapXZ, float worldSize)
        {
            float x = Mathf.Clamp(mapXZ.x, 0f, worldSize);
            float z = Mathf.Clamp(mapXZ.y, 0f, worldSize);

            float dLeft = x;
            float dRight = worldSize - x;
            float dBottom = z;
            float dTop = worldSize - z;
            float nearest = Mathf.Min(Mathf.Min(dLeft, dRight), Mathf.Min(dBottom, dTop));

            if (nearest == dLeft) return new Vector2(0f, z);
            if (nearest == dRight) return new Vector2(worldSize, z);
            if (nearest == dBottom) return new Vector2(x, 0f);
            return new Vector2(x, worldSize);
        }

        public static Vector2 MapToGui(Rect drawRect, Vector2 mapXZ, float worldSize)
        {
            float u = worldSize > 0.0001f ? mapXZ.x / worldSize : 0f;
            float v = worldSize > 0.0001f ? mapXZ.y / worldSize : 0f;
            return new Vector2(
                drawRect.x + u * drawRect.width,
                drawRect.y + (1f - v) * drawRect.height);
        }

    }
}
#endif
