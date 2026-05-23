namespace WarSimulation.Combat.Map
{
    /// <summary>
    /// マップ生成で使う乱数ソースの抽象。
    /// System.Random を直接使わないのは、テスト時にモックを差し込めるようにするため
    /// （および将来 Unity.Mathematics.Random 等に差し替えられるようにするため）。
    /// </summary>
    public interface IRandom
    {
        /// <summary>[minInclusive, maxExclusive) の整数を返す。</summary>
        int NextInt(int minInclusive, int maxExclusive);

        /// <summary>[0, 1) の浮動小数を返す。</summary>
        float NextFloat();
    }
}
