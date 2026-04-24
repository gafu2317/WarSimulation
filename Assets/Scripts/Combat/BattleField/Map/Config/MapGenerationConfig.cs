using System.Collections.Generic;
using UnityEngine;

namespace WarSimulation.Combat.Map
{
    /// <summary>
    /// マップ 1 枚分のレシピ。各フェーズのパラメータと使う StampShape をまとめて保持する。
    /// パイプライン順：
    ///   BaseHeight → River → Lake → Structure → GroundPatch → Forest → Rock → Decoration → Bridge
    /// </summary>
    [CreateAssetMenu(menuName = "WarSim/Map/Map Generation Config", fileName = "MapGenerationConfig")]
    public sealed class MapGenerationConfig : ScriptableObject
    {
        [Header("Grid")]
        [Tooltip("マップ全体の一辺の長さ（メートル）。セルは常に 1m 固定なので、この値がそのまま一辺セル数になる。")]
        [SerializeField, Min(2f)] private float _worldSize = 60f;

        [Tooltip("ベースとなる初期高度。すべてのセルがこの値で初期化される。")]
        [SerializeField] private float _baseHeight = 0f;

        [Tooltip("ユニットが登れる最大の地形勾配（度）。ここを超える斜面は『断崖＝登れない』として後段の移動判定で扱う想定。地形生成自体はこの値を直接は使わないが、山スタンプの Cliff 設定と整合する値を入れておく。")]
        [SerializeField, Range(5f, 85f)] private float _maxClimbableSlopeDeg = 30f;

        [Header("Structure Phase")]
        [Tooltip("大構造として使う HeightStampShape アプリセットのリスト（名前の違うアセットを複数並べてよい）。配置時はこの中からランダムに 1 つ選ぶので、同じプリセットを複数要素にすると出やすくなる。形状ファミリー（Dome/Cone/Ridge）は HeightStampShape 側の Kind を参照。")]
        [SerializeField] private List<HeightStampShape> _structureStamps = new();

        [Tooltip("1 マップあたりに配置する大構造スタンプの個数。")]
        [SerializeField, Min(0)] private int _structureStampCount = 5;

        [Tooltip("大構造スタンプの配置マージン。マップ端からこの距離は中心を置かない。")]
        [SerializeField, Min(0f)] private float _structurePlacementMargin = 0f;

        [Tooltip("山の円形フットプリントから水セル（川・湖）までに確保したい追加クリアランス（メートル）。大きいほど水辺から山が遠ざかる。")]
        [SerializeField, Min(0f)] private float _structureRiverClearance = 2f;

        [Tooltip("ランダムに選んだ 1 個のスタンプについて、水に被らない中心を乱択する試行回数。この回数で見つからなければ別のスタンプを選び直す（目標個数に達するまで繰り返す）。")]
        [SerializeField, Min(1)] private int _structureMaxPlacementAttempts = 300;

        [Tooltip("大構造を目標個数に届かせるための外側ループの最大回数（スタンプ選択＋中心探索を 1 回と数える）。水が多くて置けない場合はこの上限で打ち切り、目標未満で終わる。")]
        [SerializeField, Min(1)] private int _structureMaxGlobalSearchIterations = 50000;

        [Tooltip("既存スタンプとの中心距離に加算する余白（m）。StructureMinCenterDistanceFactor と併用。0 でも係数 1 なら『印の実効半径の和』は離す。")]
        [SerializeField, Min(0f)] private float _structureMinCenterSeparation = 0f;

        [Tooltip("既存スタンプとの距離に使う『実効半径の和』の係数。1=印同士が外接するくらい離す（従来の 1.3×ベース半径に相当）、0 に近いほど重なりを許す（係数 0 かつ余白 0 なら中心距離は不問）。")]
        [SerializeField, Range(0f, 1f)] private float _structureMinCenterDistanceFactor = 1f;

        [Header("River Phase")]
        [Tooltip("川の断面形状を定義する SO。未設定の場合は川フェーズをスキップ。")]
        [SerializeField] private RiverShape _riverShape;

        [Tooltip("1 マップあたりに引く『マップ横断の大河』の本数（端から別の端まで Perlin ノイズで蛇行）。")]
        [SerializeField, Min(0)] private int _crossMapRiverCount = 2;

        [Tooltip("このセル数未満の経路になった川は採用しない（短すぎる川を棄却）。")]
        [SerializeField, Min(2)] private int _riverMinPathLength = 30;

        [Tooltip("横断大河の蛇行幅（メートル）：始点-終点の直線に対して中央付近でずらす最大距離。大きいほどうねる。")]
        [SerializeField, Min(0f)] private float _flatRiverMeanderAmplitude = 10f;

        [Tooltip("横断大河の蛇行周波数：1m 進むごとのノイズ位相。大きいほど細かくうねる（≒ 蛇行の周期）。")]
        [SerializeField, Min(0.001f)] private float _flatRiverMeanderFrequency = 0.08f;

        [Tooltip("川の骨格を二次ベジェで曲げる強さ（メートル）。0 = 従来どおり始点〜終点の直線スパイン。大きいほど弦の法線方向に大きく弧を描く（ノイズはその接線に直交して別途乗る）。")]
        [SerializeField, Min(0f)] private float _flatRiverSpineCurveBend = 0f;

        [Header("Bridge Phase")]
        [Tooltip("1 本の川に対して配置する橋の数。")]
        [SerializeField, Min(0)] private int _bridgesPerRiver = 2;

        [Tooltip("橋の長さ（メートル）。川幅より長くする必要がある。")]
        [SerializeField, Min(0.1f)] private float _bridgeLength = 5f;

        [Tooltip("橋の幅（メートル、歩行面の幅）。")]
        [SerializeField, Min(0.1f)] private float _bridgeWidth = 2f;

        [Tooltip("橋の厚み（メートル）。")]
        [SerializeField, Min(0.01f)] private float _bridgeThickness = 0.25f;

        [Tooltip("橋の Y オフセット（川の水面からの高さ）。")]
        [SerializeField, Min(0f)] private float _bridgeHeightAboveWater = 0.3f;

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

        /// <summary>
        /// セルサイズ 1m 固定のため、マップ一辺セル数は「一辺メートル数」を四捨五入して使う。
        /// </summary>
        private int SquareResolution => Mathf.Max(2, Mathf.RoundToInt(_worldSize));

        public float WorldSize => SquareResolution;
        public int HeightMapResolution => SquareResolution;

        /// <summary>
        /// GroundStateGrid の解像度は HeightMap と同じに揃える（旧仕様の別解像度は廃止）。
        /// 互換性のため公開プロパティは残すが、実体は <see cref="HeightMapResolution"/> と同じ値を返す。
        /// </summary>
        public int GroundStateGridResolution => SquareResolution;
        public float BaseHeight => _baseHeight;
        public float MaxClimbableSlopeDeg => _maxClimbableSlopeDeg;

        public IReadOnlyList<HeightStampShape> StructureStamps => _structureStamps;
        public int StructureStampCount => _structureStampCount;
        public float StructurePlacementMargin => _structurePlacementMargin;
        public float StructureRiverClearance => _structureRiverClearance;
        public int StructureMaxPlacementAttempts => _structureMaxPlacementAttempts;
        public int StructureMaxGlobalSearchIterations => _structureMaxGlobalSearchIterations;
        public float StructureMinCenterSeparation => _structureMinCenterSeparation;
        public float StructureMinCenterDistanceFactor => _structureMinCenterDistanceFactor;

        public RiverShape RiverShape => _riverShape;
        public int CrossMapRiverCount => _crossMapRiverCount;
        public int RiverMinPathLength => _riverMinPathLength;
        public float FlatRiverMeanderAmplitude => _flatRiverMeanderAmplitude;
        public float FlatRiverMeanderFrequency => _flatRiverMeanderFrequency;
        public float FlatRiverSpineCurveBend => _flatRiverSpineCurveBend;

        public int BridgesPerRiver => _bridgesPerRiver;
        public float BridgeLength => _bridgeLength;
        public float BridgeWidth => _bridgeWidth;
        public float BridgeThickness => _bridgeThickness;
        public float BridgeHeightAboveWater => _bridgeHeightAboveWater;

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

        public float HeightMapCellSize => 1f;

        /// <summary>
        /// GroundStateGrid のセルサイズ。解像度は HeightMap と同じに揃えるため、
        /// <see cref="HeightMapCellSize"/> と常に等しい値を返す。
        /// </summary>
        public float GroundStateGridCellSize => HeightMapCellSize;
    }
}
