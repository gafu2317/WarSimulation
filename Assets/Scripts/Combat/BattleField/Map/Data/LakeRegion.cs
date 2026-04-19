using UnityEngine;

namespace WarSimulation.Combat.Map
{
    /// <summary>
    /// 湖 1 つ分のデータ（ワールド上の中心・半径・水面 Y）。
    /// Render 層（LakeRenderer）が水面ディスクメッシュを張る際に参照する。
    /// </summary>
    public readonly struct LakeRegion
    {
        /// <summary>ワールド XZ 上の中心（X = .x, Z = .y）。</summary>
        public Vector2 Center { get; }

        /// <summary>半径（ワールドメートル）。</summary>
        public float Radius { get; }

        /// <summary>水面のワールド Y。湖は水面が平らなので 1 値で保持する。</summary>
        public float WaterY { get; }

        /// <summary>
        /// 凍結しているか。true の場合、水面は氷として描画され、
        /// 将来の移動判定で「歩行可能」として扱われる想定。
        /// GroundStateGrid 側は従来どおり Water タグのままなので、
        /// 他の配置フェーズ（木・岩など）は湖を避け続ける。
        /// </summary>
        public bool IsFrozen { get; }

        public LakeRegion(Vector2 center, float radius, float waterY, bool isFrozen = false)
        {
            Center = center;
            Radius = radius;
            WaterY = waterY;
            IsFrozen = isFrozen;
        }
    }
}
