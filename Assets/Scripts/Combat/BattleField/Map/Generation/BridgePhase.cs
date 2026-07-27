using UnityEngine;

namespace WarSimulation.Combat.Map
{
    /// <summary>
    /// 橋フェーズ：RiverPhase が生成した各川の上に、等間隔で橋を N 個配置する。
    /// Bridge は PlacedFeature（FeatureType.Bridge）として MapData.Features に追加され、
    /// BridgeRenderer が Cube メッシュで可視化する。
    ///
    /// 配置規則：
    /// - セル列を N+1 等分した内部区切り（両端は除外）に橋を置く
    /// - 橋の向きは該当点の前後セルから算出した接線に垂直
    /// - 高さ（Y）は川の水面 + BridgeHeightAboveWater
    /// </summary>
    public sealed class BridgePhase : IMapGenerationPhase
    {
        public void Execute(MapData map, IRandom rng, MapGenerationConfig config)
        {
            BridgeBuildUtility.PlaceAutoBridges(map, config);
        }
    }
}
