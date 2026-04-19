namespace WarSimulation.Combat.Map
{
    /// <summary>
    /// HeightStampShape が生成する形状の種類。
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
