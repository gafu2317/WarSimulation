namespace WarSimulation.Combat.Map
{
    /// <summary>
    /// 高度スタンプを既存の HeightMap に合成する際の規則。
    /// </summary>
    public enum HeightBlendMode
    {
        /// <summary>通常の加算（山・丘の積み増しなど）。</summary>
        Add = 0,

        /// <summary>既存値と合成後値の最小値を採る（川の掘削など、常に低く保ちたい場合）。</summary>
        Min,

        /// <summary>既存値と合成後値の最大値を採る（既存地形より高くしたい場合）。</summary>
        Max,
    }
}
