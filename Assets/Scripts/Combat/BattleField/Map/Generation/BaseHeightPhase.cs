namespace WarSimulation.Combat.Map
{
    /// <summary>
    /// すべてのセルを <see cref="MapGenerationConfig.BaseHeight"/> で初期化する最初のフェーズ。
    /// 他のフェーズは「すでに地面の基準高度がセット済み」であることを前提にできる。
    /// HeightMap の配列は C# 既定値 0 で初期化されるため、BaseHeight=0 なら実質 no-op。
    /// </summary>
    public sealed class BaseHeightPhase : IMapGenerationPhase
    {
        public void Execute(MapData map, IRandom rng, MapGenerationConfig config)
        {
            if (map == null || config == null) return;

            float baseHeight = config.BaseHeight;
            if (baseHeight == 0f) return;

            HeightMap h = map.Height;
            for (int z = 0; z < h.Height; z++)
            {
                for (int x = 0; x < h.Width; x++)
                {
                    h.SetHeight(x, z, baseHeight);
                }
            }
        }
    }
}
