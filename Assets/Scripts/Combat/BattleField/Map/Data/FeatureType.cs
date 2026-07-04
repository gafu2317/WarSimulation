namespace WarSimulation.Combat.Map
{
    /// <summary>
    /// 単一座標に配置される「点」としての設置物種別。
    /// 面として広がる「地面の状態」は <see cref="GroundState"/> / <see cref="GroundStateGrid"/>
    /// で表現し、こちらは一点配置のもののみ。
    ///
    /// 魔石は陣営（自/敵）× 役割（メイン/サブ）で 4 種類に分ける。
    /// メインは拠点（破壊されると敗北）、サブは支援拠点の想定。
    /// </summary>
    public enum FeatureType
    {
        OwnMainStone = 0,
        OwnSubStone,
        EnemyMainStone,
        EnemySubStone,
        Tree,
        Rock,
        Bridge,
    }
}
