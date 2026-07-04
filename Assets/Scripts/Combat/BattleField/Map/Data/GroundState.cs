namespace WarSimulation.Combat.Map
{
    /// <summary>
    /// セルごとの「地面の状態」。排他（1 セル = 1 状態）で、優先度は Water > Snow > Swamp > Normal。
    /// - 地形の高低差は <see cref="HeightMap"/> が担う（こちらには含まれない）
    /// - 木・岩・魔石・橋などの単体オブジェクトは <see cref="PlacedFeature"/> が担う
    /// </summary>
    public enum GroundState
    {
        Normal = 0,
        Swamp,
        Snow,
        Water,
    }
}
