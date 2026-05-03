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
    ///   - 地形   : <see cref="Height"/> （HeightMap：高低差＋<see cref="HeightMap.CliffFaces"/>）
    ///   - 地面   : <see cref="GroundStates"/> （GroundStateGrid、セル毎の状態：沼/雪/水）
    ///   - オブジェクト : <see cref="Features"/> （木・岩・魔石・橋など一点配置）
    /// </summary>
    public class MapData
    {
        public const string UnsetBiomeId = "";

        private readonly string[,] _biomeIds;

        public HeightMap Height { get; }
        public GroundStateGrid GroundStates { get; }

        /// <summary>地形と同じ解像度の崖面マスク。<see cref="Height.CliffFaces"/> と同一。</summary>
        public CliffFaceGrid CliffFaces => Height.CliffFaces;
        public List<PlacedFeature> Features { get; }
        public List<RiverPath> Rivers { get; }
        public List<LakeRegion> Lakes { get; }
        public List<ForestRegion> ForestRegions { get; }
        public int Seed { get; }

        /// <summary>
        /// 直近の <see cref="StructurePhase"/> で実際に高度スタンプが押された回数（目標は <see cref="MapGenerationConfig.StructureStampTargetTotal"/>）。
        /// </summary>
        public int StructureStampPlacedCount { get; set; }

        /// <summary>StructurePhase が試行した候補総数（採用・棄却を含む）。</summary>
        public int StructureTotalAttempts { get; set; }

        /// <summary>候補中心が水チェック円で水セルを含み棄却された回数。</summary>
        public int StructureWaterRejects { get; set; }

        /// <summary>候補中心が既存スタンプとの距離条件で棄却された回数。</summary>
        public int StructureDistanceRejects { get; set; }

        public MapData(HeightMap height, GroundStateGrid groundStates, int seed)
        {
            Height = height ?? throw new ArgumentNullException(nameof(height));
            GroundStates = groundStates ?? throw new ArgumentNullException(nameof(groundStates));
            Seed = seed;
            _biomeIds = new string[groundStates.Width, groundStates.Height];
            Features = new List<PlacedFeature>();
            Rivers = new List<RiverPath>();
            Lakes = new List<LakeRegion>();
            ForestRegions = new List<ForestRegion>();
            StructureStampPlacedCount = 0;
            StructureTotalAttempts = 0;
            StructureWaterRejects = 0;
            StructureDistanceRejects = 0;
        }

        public void AddFeature(PlacedFeature feature) => Features.Add(feature);

        public void AddRiver(RiverPath river) => Rivers.Add(river);

        public void AddLake(LakeRegion lake) => Lakes.Add(lake);

        public void AddForestRegion(ForestRegion region) => ForestRegions.Add(region);

        public string GetBiomeId(int x, int z)
        {
            if (!GroundStates.IsInBounds(x, z)) return UnsetBiomeId;
            return _biomeIds[x, z] ?? UnsetBiomeId;
        }

        public void SetBiomeId(int x, int z, string biomeId)
        {
            if (!GroundStates.IsInBounds(x, z))
                throw new ArgumentOutOfRangeException($"Biome cell ({x}, {z}) is outside the map.");

            _biomeIds[x, z] = string.IsNullOrEmpty(biomeId) ? null : biomeId;
        }

        public void ClearBiomeId(int x, int z) => SetBiomeId(x, z, UnsetBiomeId);
    }
}
