namespace WarSimulation.Combat.Map
{
    /// <summary>
    /// 単一座標に配置される「点」としての設置物種別。
    /// 面として広がる「地面の状態」は <see cref="GroundState"/> / <see cref="GroundStateGrid"/>
    /// で表現し、こちらは一点配置のもののみ。
    ///
    /// 魔石は陣営（自/敵）ごとのメイン拠点として配置される。
    /// </summary>
    public enum FeatureType
    {
        OwnMainStone = 0,
        EnemyMainStone = 2,
        Tree = 4,
        Rock = 5,
        Bridge = 6,
    }
}
