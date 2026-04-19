namespace WarSimulation.Combat.Map
{
    /// <summary>
    /// マップ生成における 1 フェーズの抽象。
    /// 各フェーズは渡された MapData を直接書き換えて責務を完了させる。
    /// </summary>
    public interface IMapGenerationPhase
    {
        /// <summary>
        /// フェーズの処理を実行する。
        /// </summary>
        /// <param name="map">書き換え対象のマップデータ。</param>
        /// <param name="rng">フェーズ内で使用する乱数ソース。</param>
        /// <param name="config">全フェーズ共通のマップ生成設定。</param>
        void Execute(MapData map, IRandom rng, MapGenerationConfig config);
    }
}
