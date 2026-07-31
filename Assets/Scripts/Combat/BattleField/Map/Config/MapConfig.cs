using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace WarSimulation.Combat.Map
{
    /// <summary>
    /// マップ 1 枚分の共有設定。グリッドサイズ・橋寸法・川の既定・木/岩散布をまとめる。
    /// </summary>
    [MovedFrom(true, "WarSimulation.Combat.Map", "Assembly-CSharp", "MapGenerationConfig")]
    [CreateAssetMenu(menuName = "WarSim/Map/Map Config", fileName = "MapConfig")]
    public sealed class MapConfig : ScriptableObject
    {
        [Header("グリッド")]
        [Tooltip("マップ全体の一辺の長さ（メートル）。Cells Per Side が 2 以上のときはこの幅に対してセルを詰める。")]
        [SerializeField, Min(2f)] private float _worldSize = 60f;

        [Tooltip("一辺あたりのセル数。0 = 従来（セル 1m・セル数 ≒ World Size の四捨五入、配置範囲もそのセル数メートル）。2 以上 = セルサイズ = World Size ÷ この値、配置範囲は World Size メートル。")]
        [SerializeField, Min(0)] private int _cellsPerSide = 0;

        [Tooltip("ベースとなる初期高度。すべてのセルがこの値で初期化される。")]
        [SerializeField] private float _baseHeight = 0f;

        [Header("川の既定")]
        [Tooltip("川の断面形状を定義する SO。未設定の場合は川の幅・深さフォールバックを使う。")]
        [SerializeField] private RiverShape _riverShape;

        [Tooltip("中央から端へ引ける高さ0直線の端点同士を結ぶ川の蛇行幅（メートル）。")]
        [SerializeField, Min(0f)] private float _flatRiverMeanderAmplitude = 10f;

        [Tooltip("川の蛇行周波数。1m 進むごとのノイズ位相。大きいほど細かくうねる。")]
        [SerializeField, Min(0.001f)] private float _flatRiverMeanderFrequency = 0.08f;

        [Header("橋")]
        [Tooltip("川幅から自動計算した橋の長さに足す余白（メートル）。")]
        [SerializeField, Min(0f)] private float _bridgeLengthExtraMargin = 1f;

        [Tooltip("橋の幅（メートル、歩行面の幅）。")]
        [SerializeField, Min(0.1f)] private float _bridgeWidth = 2f;

        [Tooltip("橋の厚み（メートル）。")]
        [SerializeField, Min(0.01f)] private float _bridgeThickness = 0.25f;

        [Tooltip("橋の Y オフセット（川の水面からの高さ）。")]
        [SerializeField, Min(0f)] private float _bridgeHeightAboveWater = 0.3f;

        [Tooltip("橋フットプリント外側の除外余白（メートル）。木・岩・魔石をこの距離内に置かない。")]
        [SerializeField, Min(0f)] private float _bridgeFeatureExclusionMargin = 2f;

        [Header("木の散布")]
        [Tooltip("森クラスター以外の平地〜山中にばら撒く『単独の木』の本数。0 でスキップ。")]
        [SerializeField, Min(0)] private int _scatterTreeCount = 0;

        [Tooltip("散布する木同士の最小間隔（メートル）。クラスター内に既にある木との距離にも使う。")]
        [SerializeField, Min(0f)] private float _scatterTreeMinDistance = 1.5f;

        [Tooltip("散布する木の配置マージン。マップ端からこの距離より内側だけに置く。")]
        [SerializeField, Min(0f)] private float _scatterTreePlacementMargin = 1f;

        [Header("岩の散布")]
        [Tooltip("1 マップあたりに配置する岩の個数。")]
        [SerializeField, Min(0)] private int _rockCount = 30;

        [Tooltip("岩同士の最小間隔（メートル）。")]
        [SerializeField, Min(0f)] private float _rockMinDistance = 1.5f;

        [Tooltip("岩の配置マージン。マップ端からこの距離は中心を置かない。")]
        [SerializeField, Min(0f)] private float _rockPlacementMargin = 1f;

        [Tooltip("マップ高度レンジ（HeightMap min〜max）の上位を岩配置から除外する比率。0.3 = 上位 30% は置かない。")]
        [SerializeField, Range(0f, 1f)] private float _rockTopHeightExclusionRatio = 0.3f;

        [Header("検証")]
        [Tooltip("1 陣営あたりのメイン魔石の個数（Validator の警告閾値）。")]
        [SerializeField, Min(0)] private int _mainStonesPerSide = 1;

        /// <summary>
        /// 一辺セル数。<see cref="_cellsPerSide"/> が 0 のときは 1m セル互換（四捨五入）。2 以上ならその値。
        /// </summary>
        private int ResolvedCellsPerSide =>
            _cellsPerSide >= 2 ? _cellsPerSide : Mathf.Max(2, Mathf.RoundToInt(_worldSize));

        /// <summary>
        /// 配置用のマップ一辺（メートル）。従来モードではセル数＝メートル（1m セル）、サブメートルモードでは <see cref="_worldSize"/>。
        /// </summary>
        public float WorldSize => _cellsPerSide >= 2 ? _worldSize : ResolvedCellsPerSide;

        public int HeightMapResolution => ResolvedCellsPerSide;

        /// <summary>
        /// GroundStateGrid の解像度は HeightMap と同じ。
        /// </summary>
        public int GroundStateGridResolution => ResolvedCellsPerSide;
        public float BaseHeight => _baseHeight;

        public RiverShape RiverShape => _riverShape;
        public float FlatRiverMeanderAmplitude => _flatRiverMeanderAmplitude;
        public float FlatRiverMeanderFrequency => _flatRiverMeanderFrequency;

        public float BridgeLengthExtraMargin => _bridgeLengthExtraMargin;
        public float BridgeWidth => _bridgeWidth;
        public float BridgeThickness => _bridgeThickness;
        public float BridgeHeightAboveWater => _bridgeHeightAboveWater;
        public float BridgeFeatureExclusionMargin => _bridgeFeatureExclusionMargin;

        public int ScatterTreeCount => _scatterTreeCount;
        public float ScatterTreeMinDistance => _scatterTreeMinDistance;
        public float ScatterTreePlacementMargin => _scatterTreePlacementMargin;

        public int RockCount => _rockCount;
        public float RockMinDistance => _rockMinDistance;
        public float RockPlacementMargin => _rockPlacementMargin;
        public float RockTopHeightExclusionRatio => _rockTopHeightExclusionRatio;

        public int MainStonesPerSide => _mainStonesPerSide;

        public float HeightMapCellSize =>
            _cellsPerSide >= 2 ? _worldSize / ResolvedCellsPerSide : 1f;

        public float GroundStateGridCellSize => HeightMapCellSize;
    }
}
