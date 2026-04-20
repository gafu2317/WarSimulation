namespace WarSimulation.Combat.Map
{
    /// <summary>
    /// <see cref="HeightStampShape"/> の「形状ファミリー」（実装が分岐する区分）はこの 3 つだけ。
    /// Inspector に並ぶ Hill_Small / Cliff_Mountain などは別々のアセット（プリセット）であり、
    /// 同じ <see cref="HeightShapeKind"/> に半径・高さ・Cliff などを変えた複数プリセットを置ける。
    /// <see cref="MapGenerationConfig.StructureStamps"/> のリストはそのプリセット集合＋出現重みであり、
    /// 「山の種類が 3 つしかない」こととは一致しない（混同しないこと）。
    /// Dome は正値でヒル、負値で盆地として使える。
    /// </summary>
    public enum HeightShapeKind
    {
        /// <summary>円形・滑らかなドーム（smoothstep で減衰）。</summary>
        Dome = 0,

        /// <summary>円錐形・線形減衰。</summary>
        Cone,

        /// <summary>尾根状。ローカル X 軸方向に伸び、両端と垂直方向に smoothstep で減衰。</summary>
        Ridge,
    }
}
