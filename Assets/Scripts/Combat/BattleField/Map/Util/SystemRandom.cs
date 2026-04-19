using SysRandom = System.Random;

namespace WarSimulation.Combat.Map
{
    /// <summary>
    /// System.Random を用いた IRandom の既定実装。
    /// シードを渡せば再現性のある乱数列を生成する。
    /// </summary>
    public sealed class SystemRandom : IRandom
    {
        private readonly SysRandom _rng;

        public SystemRandom(int seed) => _rng = new SysRandom(seed);

        public SystemRandom() => _rng = new SysRandom();

        public int NextInt(int minInclusive, int maxExclusive) => _rng.Next(minInclusive, maxExclusive);

        public float NextFloat() => (float)_rng.NextDouble();
    }
}
