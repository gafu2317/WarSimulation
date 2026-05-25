using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace WarSimulation.Combat.Map
{
    /// <summary>
    /// マップ 1 枚分のレシピ。各フェーズのパラメータと使う StampShape をまとめて保持する。
    /// パイプライン順：
    ///   BaseHeight → Mountain → River → Lake → GroundPatch → Bridge → Forest → TreeScatter → Rock → Decoration
    /// </summary>
    [CreateAssetMenu(menuName = "WarSim/Map/Map Generation Config", fileName = "MapGenerationConfig")]
    public sealed class MapGenerationConfig : ScriptableObject
    {
        [Header("Grid")]
        [Tooltip("マップ全体の一辺の長さ（メートル）。Cells Per Side が 2 以上のときはこの幅に対してセルを詰める。")]
        [SerializeField, Min(2f)] private float _worldSize = 60f;

        [Tooltip("一辺あたりのセル数。0 = 従来（セル 1m・セル数 ≒ World Size の四捨五入、配置範囲もそのセル数メートル）。2 以上 = セルサイズ = World Size ÷ この値、配置範囲は World Size メートル。")]
        [SerializeField, Min(0)] private int _cellsPerSide = 0;

        [Tooltip("ベースとなる初期高度。すべてのセルがこの値で初期化される。")]
        [SerializeField] private float _baseHeight = 0f;

        [Tooltip("ユニットが登れる最大の地形勾配（度）。ここを超える斜面は『断崖＝登れない』として後段の移動判定で扱う想定。地形生成自体はこの値を直接は使わないが、山スタンプの Cliff 設定と整合する値を入れておく。")]
        [SerializeField, Range(5f, 85f)] private float _maxClimbableSlopeDeg = 30f;

        [Header("Mountain Phase")]
        [Tooltip("大山として 1 個だけ配置する高度スタンプ。未設定、または候補位置が空の場合は大山をスキップ。")]
        [SerializeField] private HeightStampShape _largeMountainShape;

        [Tooltip("大山候補位置。正規化座標で (0,0)=マップ左下/手前、(1,1)=右上/奥。候補からランダムに 1 個選ぶ。")]
        [SerializeField] private List<Vector2> _largeMountainCandidatePositionsNormalized = new();

        [Tooltip("小山スタンプ。上から順に、各行の Shape を Count 回だけ配置する。Shape が null または Count が 0 の行は無視。")]
        [FormerlySerializedAs("_structureStampEntries")]
        [SerializeField] private List<StructureStampEntry> _smallMountainStampEntries = new();

        [Tooltip("小山の配置マージン。マップ端からこの距離は中心を置かない。大山は候補位置をそのまま使う。")]
        [FormerlySerializedAs("_structurePlacementMargin")]
        [SerializeField, Min(0f)] private float _mountainPlacementMargin = 0f;

        [Tooltip("1 個分の小山配置について、既存山と重ならない中心を乱択する試行回数。この回数で見つからなければその 1 個はスキップ。")]
        [FormerlySerializedAs("_structureMaxPlacementAttempts")]
        [SerializeField, Min(1)] private int _mountainMaxPlacementAttempts = 300;

        [Tooltip("山を目標個数に届かせるための外側ループの最大回数。置けない場合はこの上限で打ち切り、目標未満で終わる。")]
        [FormerlySerializedAs("_structureMaxGlobalSearchIterations")]
        [SerializeField, Min(1)] private int _mountainMaxGlobalSearchIterations = 50000;

        [Tooltip("既存山との中心距離に加算する余白（m）。MountainMinCenterDistanceFactor と併用。0 でも係数 1 なら『印の実効半径の和』は離す。")]
        [FormerlySerializedAs("_structureMinCenterSeparation")]
        [SerializeField, Min(0f)] private float _mountainMinCenterSeparation = 0f;

        [Tooltip("既存山との距離に使う『実効半径の和』の係数。1=印同士が外接するくらい離す、0 に近いほど重なりを許す。")]
        [FormerlySerializedAs("_structureMinCenterDistanceFactor")]
        [SerializeField, Range(0f, 1f)] private float _mountainMinCenterDistanceFactor = 1f;

        [Header("River Phase")]
        [Tooltip("川の断面形状を定義する SO。未設定の場合は川フェーズをスキップ。")]
        [SerializeField] private RiverShape _riverShape;

        [Tooltip("1 マップあたりに引く『マップ横断の大河』の本数（端から別の端まで Perlin ノイズで蛇行）。")]
        [SerializeField, Min(0)] private int _crossMapRiverCount = 2;

        [Tooltip("この長さ（メートル）未満の経路になった川は採用しない（短すぎる川を棄却）。")]
        [FormerlySerializedAs("_riverMinPathLength")]
        [SerializeField, Min(0.1f)] private float _riverMinPathLengthMeters = 30f;

        [Tooltip("川 1 本につき試す入口/出口候補ペア数。見つからなければその川をスキップ。")]
        [SerializeField, Min(1)] private int _riverMaxPathSearchAttempts = 48;

        [Tooltip("既存 Water セルから入口/出口・経路をどれだけ離すか（メートル）。2 本目以降の川の重なり防止に使う。")]
        [SerializeField, Min(0f)] private float _riverExistingWaterClearance = 1f;

        [Tooltip("川が経由する中央候補エリアの広さ。WorldSize に対する比率。0.45 なら中央 45% の矩形から高さ0候補を探す。")]
        [SerializeField, Range(0.1f, 1f)] private float _riverCenterCandidateAreaRatio = 0.45f;

        [Tooltip("中央から端へ引ける高さ0直線の端点同士を結ぶ川の蛇行幅（メートル）。大きいほど川らしくうねるが、高さ0制約で失敗しやすい。")]
        [SerializeField, Min(0f)] private float _flatRiverMeanderAmplitude = 10f;

        [Tooltip("川の蛇行周波数。1m 進むごとのノイズ位相。大きいほど細かくうねる。")]
        [SerializeField, Min(0.001f)] private float _flatRiverMeanderFrequency = 0.08f;

        [Tooltip("中央を通る二次ベジェを法線方向へ曲げる最大量（メートル）。大きいほど大きく弧を描く。")]
        [SerializeField, Min(0f)] private float _flatRiverSpineCurveBend = 0f;

        [Header("Bridge Phase")]
        [Tooltip("1 本の川に対して配置する橋の数。")]
        [SerializeField, Min(0)] private int _bridgesPerRiver = 2;

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

        [Header("Lake Phase")]
        [Tooltip("湖を配置するスタンプのリスト。")]
        [SerializeField] private List<LakeStampShape> _lakeStamps = new();

        [Tooltip("1 マップあたりに配置する湖の個数。")]
        [SerializeField, Min(0)] private int _lakeCount = 1;

        [Tooltip("湖の配置マージン。マップ端からこの距離は中心を置かない。")]
        [SerializeField, Min(0f)] private float _lakePlacementMargin = 6f;

        [Tooltip("配置される 1 個の湖が凍結する確率（0〜1）。0 = 絶対に凍らない、1 = 必ず凍る、0.3 = 約 3 割が氷湖。")]
        [SerializeField, Range(0f, 1f)] private float _lakeFreezeProbability = 0f;

        [Tooltip("湖を既存の Water セル（川）から最低限離す距離（メートル）。候補中心から (湖半径 + これ) 以内に川があれば棄却して別位置で再試行する。")]
        [SerializeField, Min(0f)] private float _lakeRiverClearance = 2f;

        [Tooltip("1 個の湖につき、川と被らない中心を探すリトライ上限。使い切るとその湖はスキップする。")]
        [SerializeField, Min(1)] private int _lakeMaxPlacementAttempts = 30;

        [Header("Ground Patch Phase / Swamp・Snow")]
        [Tooltip("沼・雪などの地面状態パッチを配置するスタンプのリスト。")]
        [SerializeField] private List<GroundPatchStampShape> _groundPatchStamps = new();

        [Tooltip("1 マップあたりに押す地面状態パッチの個数。")]
        [SerializeField, Min(0)] private int _groundPatchStampCount = 6;

        [Tooltip("地面状態パッチの配置マージン。マップ端からこの距離は中心を置かない。")]
        [SerializeField, Min(0f)] private float _groundPatchPlacementMargin = 0f;

        [Header("Forest Phase / Trees")]
        [Tooltip("森クラスター（木の群生ゾーン）を配置するスタンプのリスト。")]
        [SerializeField] private List<ForestClusterStampShape> _forestClusterStamps = new();

        [Tooltip("1 マップあたりに配置する森クラスターの個数。")]
        [SerializeField, Min(0)] private int _forestClusterCount = 3;

        [Tooltip("森クラスターの配置マージン。マップ端からこの距離は中心を置かない。")]
        [SerializeField, Min(0f)] private float _forestPlacementMargin = 2f;

        [Header("Tree Scatter Phase")]
        [Tooltip("森クラスター以外の平地〜山中にばら撒く『単独の木』の本数。0 でこのフェーズをスキップ。岩フェーズと同様に水・森ゾーンは避ける。")]
        [SerializeField, Min(0)] private int _scatterTreeCount = 0;

        [Tooltip("散布する木同士の最小間隔（メートル）。クラスター内に既にある木との距離にも使う。")]
        [SerializeField, Min(0f)] private float _scatterTreeMinDistance = 1.5f;

        [Tooltip("散布する木の配置マージン。マップ端からこの距離より内側だけに置く。")]
        [SerializeField, Min(0f)] private float _scatterTreePlacementMargin = 1f;

        [Header("Rock Phase")]
        [Tooltip("1 マップあたりに配置する岩の個数。")]
        [SerializeField, Min(0)] private int _rockCount = 30;

        [Tooltip("岩同士の最小間隔（メートル）。これ未満の距離に他の岩がある候補は棄却される。")]
        [SerializeField, Min(0f)] private float _rockMinDistance = 1.5f;

        [Tooltip("岩の配置マージン。マップ端からこの距離は中心を置かない。")]
        [SerializeField, Min(0f)] private float _rockPlacementMargin = 1f;

        [Header("Decoration Phase / Magic Stones")]
        [Tooltip("1 陣営あたりのメイン魔石の個数。拠点として扱う想定。")]
        [SerializeField, Min(0)] private int _mainStonesPerSide = 1;

        [Tooltip("1 陣営あたりのサブ魔石の個数。支援拠点として扱う想定。")]
        [SerializeField, Min(0)] private int _subStonesPerSide = 2;

        [Tooltip("魔石を配置できる陣営ゾーンの奥行き比率（0.33 なら自陣営は下 1/3、敵陣営は上 1/3）。")]
        [SerializeField, Range(0.05f, 0.5f)] private float _magicStoneZoneRatio = 1f / 3f;

        [Tooltip("メイン魔石を置く範囲をさらに奥（マップ端側）に絞る比率。0 で『陣営ゾーン全体』、1 で『奥半分』。サブは常にゾーン全体に散る。")]
        [SerializeField, Range(0f, 1f)] private float _mainStoneBackBias = 0.5f;

        [Tooltip("魔石同士の最小間隔（メートル）。これ未満の距離に他の魔石がある候補は棄却される。")]
        [SerializeField, Min(0f)] private float _magicStoneMinDistance = 8f;

        [Tooltip("魔石の配置マージン。マップ端からこの距離は中心を置かない。")]
        [SerializeField, Min(0f)] private float _magicStonePlacementMargin = 4f;

        [Tooltip("魔石を置ける最大斜度（度）。これを超える斜面は『山の斜面』とみなして棄却。")]
        [SerializeField, Range(0f, 60f)] private float _magicStoneMaxSlopeDeg = 15f;

        [Tooltip("魔石を置ける BaseHeight からの最大上振れ（メートル）。これを超える高地は『山の上』とみなして棄却。")]
        [SerializeField, Min(0f)] private float _magicStoneMaxRelativeHeight = 0.8f;

        private void OnValidate()
        {
            if (_smallMountainStampEntries == null)
                _smallMountainStampEntries = new List<StructureStampEntry>();
            if (_largeMountainCandidatePositionsNormalized == null)
                _largeMountainCandidatePositionsNormalized = new List<Vector2>();
        }

        /// <summary>
        /// 一辺セル数。<see cref="_cellsPerSide"/> が 0 のときは 1m セル互換（四捨五入）。2 以上ならその値。
        /// </summary>
        private int ResolvedCellsPerSide =>
            _cellsPerSide >= 2 ? _cellsPerSide : Mathf.Max(2, Mathf.RoundToInt(_worldSize));

        /// <summary>
        /// 配置フェーズ用のマップ一辺（メートル）。従来モードではセル数＝メートル（1m セル）、サブメートルモードでは <see cref="_worldSize"/>。
        /// </summary>
        public float WorldSize => _cellsPerSide >= 2 ? _worldSize : ResolvedCellsPerSide;

        public int HeightMapResolution => ResolvedCellsPerSide;

        /// <summary>
        /// GroundStateGrid の解像度は HeightMap と同じに揃える（旧仕様の別解像度は廃止）。
        /// 互換性のため公開プロパティは残すが、実体は <see cref="HeightMapResolution"/> と同じ値を返す。
        /// </summary>
        public int GroundStateGridResolution => ResolvedCellsPerSide;
        public float BaseHeight => _baseHeight;
        public float MaxClimbableSlopeDeg => _maxClimbableSlopeDeg;

        public HeightStampShape LargeMountainShape => _largeMountainShape;
        public IReadOnlyList<Vector2> LargeMountainCandidatePositionsNormalized => _largeMountainCandidatePositionsNormalized;
        public IReadOnlyList<StructureStampEntry> SmallMountainStampEntries => _smallMountainStampEntries;

        public IReadOnlyList<StructureStampEntry> StructureStampEntries => SmallMountainStampEntries;

        /// <summary>
        /// 目標とする山の合計個数（大山候補が有効なら 1 + 小山 Count の和）。
        /// </summary>
        public int MountainStampTargetTotal => SumMountainTargets();

        public int StructureStampTargetTotal => MountainStampTargetTotal;

        /// <summary>
        /// <see cref="StructureStampTargetTotal"/> の別名（既存コード・エディタ用）。
        /// </summary>
        public int StructureStampCount => MountainStampTargetTotal;

        /// <summary>
        /// エディタやデバッグ用：有効なエントリの Shape を列挙（重複可）。
        /// </summary>
        public IReadOnlyList<HeightStampShape> StructureStamps => MountainStampsForInspection;

        private IReadOnlyList<HeightStampShape> MountainStampsForInspection
        {
            get
            {
                var list = new List<HeightStampShape>();
                if (_largeMountainShape != null)
                    list.Add(_largeMountainShape);
                if (_smallMountainStampEntries == null)
                    return list;
                for (int i = 0; i < _smallMountainStampEntries.Count; i++)
                {
                    StructureStampEntry e = _smallMountainStampEntries[i];
                    if (e != null && e.Shape != null && e.Count > 0)
                        list.Add(e.Shape);
                }
                return list;
            }
        }

        private int SumMountainTargets()
        {
            int sum = _largeMountainShape != null
                && _largeMountainCandidatePositionsNormalized != null
                && _largeMountainCandidatePositionsNormalized.Count > 0
                    ? 1
                    : 0;
            if (_smallMountainStampEntries == null)
                return sum;
            for (int i = 0; i < _smallMountainStampEntries.Count; i++)
            {
                StructureStampEntry e = _smallMountainStampEntries[i];
                if (e != null && e.Shape != null && e.Count > 0)
                    sum += e.Count;
            }
            return sum;
        }
        public float MountainPlacementMargin => _mountainPlacementMargin;
        public int MountainMaxPlacementAttempts => _mountainMaxPlacementAttempts;
        public int MountainMaxGlobalSearchIterations => _mountainMaxGlobalSearchIterations;
        public float MountainMinCenterSeparation => _mountainMinCenterSeparation;
        public float MountainMinCenterDistanceFactor => _mountainMinCenterDistanceFactor;

        public float StructurePlacementMargin => MountainPlacementMargin;
        public float StructureRiverClearance => 0f;
        public int StructureMaxPlacementAttempts => MountainMaxPlacementAttempts;
        public int StructureMaxGlobalSearchIterations => MountainMaxGlobalSearchIterations;
        public float StructureMinCenterSeparation => MountainMinCenterSeparation;
        public float StructureMinCenterDistanceFactor => MountainMinCenterDistanceFactor;

        public RiverShape RiverShape => _riverShape;
        public int CrossMapRiverCount => _crossMapRiverCount;
        public float RiverMinPathLengthMeters => _riverMinPathLengthMeters;
        public int RiverMinPathLength => Mathf.CeilToInt(_riverMinPathLengthMeters);
        public int RiverMaxPathSearchAttempts => _riverMaxPathSearchAttempts;
        public float RiverExistingWaterClearance => _riverExistingWaterClearance;
        public float RiverCenterCandidateAreaRatio => _riverCenterCandidateAreaRatio;
        public float FlatRiverMeanderAmplitude => _flatRiverMeanderAmplitude;
        public float FlatRiverMeanderFrequency => _flatRiverMeanderFrequency;
        public float FlatRiverSpineCurveBend => _flatRiverSpineCurveBend;

        public int BridgesPerRiver => _bridgesPerRiver;
        public float BridgeLengthExtraMargin => _bridgeLengthExtraMargin;
        public float BridgeWidth => _bridgeWidth;
        public float BridgeThickness => _bridgeThickness;
        public float BridgeHeightAboveWater => _bridgeHeightAboveWater;
        public float BridgeFeatureExclusionMargin => _bridgeFeatureExclusionMargin;

        public IReadOnlyList<LakeStampShape> LakeStamps => _lakeStamps;
        public int LakeCount => _lakeCount;
        public float LakePlacementMargin => _lakePlacementMargin;
        public float LakeFreezeProbability => _lakeFreezeProbability;
        public float LakeRiverClearance => _lakeRiverClearance;
        public int LakeMaxPlacementAttempts => _lakeMaxPlacementAttempts;

        public IReadOnlyList<GroundPatchStampShape> GroundPatchStamps => _groundPatchStamps;
        public int GroundPatchStampCount => _groundPatchStampCount;
        public float GroundPatchPlacementMargin => _groundPatchPlacementMargin;

        public IReadOnlyList<ForestClusterStampShape> ForestClusterStamps => _forestClusterStamps;
        public int ForestClusterCount => _forestClusterCount;
        public float ForestPlacementMargin => _forestPlacementMargin;

        public int ScatterTreeCount => _scatterTreeCount;
        public float ScatterTreeMinDistance => _scatterTreeMinDistance;
        public float ScatterTreePlacementMargin => _scatterTreePlacementMargin;

        public int RockCount => _rockCount;
        public float RockMinDistance => _rockMinDistance;
        public float RockPlacementMargin => _rockPlacementMargin;

        public int MainStonesPerSide => _mainStonesPerSide;
        public int SubStonesPerSide => _subStonesPerSide;
        public float MagicStoneZoneRatio => _magicStoneZoneRatio;
        public float MainStoneBackBias => _mainStoneBackBias;
        public float MagicStoneMinDistance => _magicStoneMinDistance;
        public float MagicStonePlacementMargin => _magicStonePlacementMargin;
        public float MagicStoneMaxSlopeDeg => _magicStoneMaxSlopeDeg;
        public float MagicStoneMaxRelativeHeight => _magicStoneMaxRelativeHeight;

        public float HeightMapCellSize =>
            _cellsPerSide >= 2 ? _worldSize / ResolvedCellsPerSide : 1f;

        /// <summary>
        /// GroundStateGrid のセルサイズ。解像度は HeightMap と同じに揃えるため、
        /// <see cref="HeightMapCellSize"/> と常に等しい値を返す。
        /// </summary>
        public float GroundStateGridCellSize => HeightMapCellSize;
    }
}
