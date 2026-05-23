using UnityEngine;

namespace WarSimulation.Combat.Map
{
    /// <summary>
    /// スタンプ 1 回分の配置情報（形状データから独立した値オブジェクト）。
    /// 位置はワールド XZ 座標系で、Vector2 の x 成分を X、y 成分を Z として扱う。
    /// </summary>
    public readonly struct StampPlacement
    {
        public Vector2 Center { get; }
        public float RotationRad { get; }
        public Vector2 Scale { get; }

        public StampPlacement(Vector2 center, float rotationRad = 0f, Vector2 scale = default)
        {
            Center = center;
            RotationRad = rotationRad;
            Scale = scale == default ? Vector2.one : scale;
        }
    }
}
