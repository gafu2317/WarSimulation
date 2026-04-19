using System;
using System.Collections.Generic;

namespace WarSimulation.Combat.Map
{
    /// <summary>
    /// 1 枚分の戦闘マップを表す純粋データコンテナ。
    /// 生成処理（Generation 層）が書き込み、Query / Render 層は読み取り専用で参照する。
    /// Unity の MonoBehaviour / ScriptableObject には依存しないため、
    /// ユニットテストから直接インスタンス化できる。
    ///
    /// 3 レイヤ構成：
    ///   - 地形   : <see cref="Height"/> （HeightMap、連続値の高低差）
    ///   - 地面   : <see cref="GroundStates"/> （GroundStateGrid、セル毎の状態：沼/雪/水）
    ///   - オブジェクト : <see cref="Features"/> （木・岩・魔石・橋など一点配置）
    /// </summary>
    public class MapData
    {
        public HeightMap Height { get; }
        public GroundStateGrid GroundStates { get; }
        public List<PlacedFeature> Features { get; }
        public List<RiverPath> Rivers { get; }
        public List<LakeRegion> Lakes { get; }
        public List<ForestRegion> ForestRegions { get; }
        public int Seed { get; }

        public MapData(HeightMap height, GroundStateGrid groundStates, int seed)
        {
            Height = height ?? throw new ArgumentNullException(nameof(height));
            GroundStates = groundStates ?? throw new ArgumentNullException(nameof(groundStates));
            Seed = seed;
            Features = new List<PlacedFeature>();
            Rivers = new List<RiverPath>();
            Lakes = new List<LakeRegion>();
            ForestRegions = new List<ForestRegion>();
        }

        public void AddFeature(PlacedFeature feature) => Features.Add(feature);

        public void AddRiver(RiverPath river) => Rivers.Add(river);

        public void AddLake(LakeRegion lake) => Lakes.Add(lake);

        public void AddForestRegion(ForestRegion region) => ForestRegions.Add(region);
    }
}
