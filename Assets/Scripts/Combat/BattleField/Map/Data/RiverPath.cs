using System;
using System.Collections.Generic;
using UnityEngine;

namespace WarSimulation.Combat.Map
{
    /// <summary>
    /// 生成された川 1 本分のパスデータ。Render 層（川メッシュ生成）が参照する。
    /// Unity の MonoBehaviour / ScriptableObject には依存しない純粋データとして保持する。
    /// セル座標は HeightMap のグリッド座標系で、ワールド変換は <see cref="HeightMap.CellSize"/>
    /// を使って行う。
    /// </summary>
    public readonly struct RiverPath
    {
        /// <summary>経路上のセル座標列（HeightMap グリッド座標）。</summary>
        public IReadOnlyList<Vector2Int> Cells { get; }

        /// <summary>川幅（ワールドメートル）。</summary>
        public float WidthMeters { get; }

        /// <summary>川の掘削深さ（ワールドメートル）。水面 Y オフセットの参考値としても使える。</summary>
        public float DepthMeters { get; }

        /// <summary>
        /// 川幅のうち水面メッシュを張る内側の割合（<see cref="RiverShape.WaterTagRatio"/> と同義）。
        /// </summary>
        public float WaterTagRatio { get; }

        public RiverPath(
            IReadOnlyList<Vector2Int> cells,
            float widthMeters,
            float depthMeters,
            float waterTagRatio = 0.6f)
        {
            Cells = cells ?? throw new ArgumentNullException(nameof(cells));
            WidthMeters = widthMeters;
            DepthMeters = depthMeters;
            WaterTagRatio = Mathf.Clamp01(waterTagRatio);
        }
    }
}
