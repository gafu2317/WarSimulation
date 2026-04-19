using UnityEngine;

namespace WarSimulation.Combat.Map
{
    /// <summary>
    /// <see cref="GroundStateGrid"/> に指定の地面状態（沼・雪）を、輪郭を Perlin ノイズで
    /// 歪ませて書き込むスタンプ。HeightMap は変更しない（状態ラベルのみ）。
    ///
    /// 塗り分けルール：
    ///   - Water セルには決して書き込まない（川・湖は常に尊重される）
    ///   - MaxHeight > 0 の場合、HeightMap 値がそれを超えるセルは塗らない（例：沼を平地限定に）
    ///   - OverrideExistingState が false の場合、Normal セルのみを塗る（先勝ち）
    ///   - OverrideExistingState が true の場合、Water 以外すべてを塗る（後勝ち）
    ///
    /// NoiseAmplitude を上げると輪郭が不整形になり、山（HeightStampShape）と同じ要領で
    /// 境界の周波数を NoiseFrequency で制御できる。
    /// </summary>
    [CreateAssetMenu(menuName = "WarSim/Map/Ground Patch Stamp", fileName = "GroundPatchStamp")]
    public sealed class GroundPatchStampShape : StampShape
    {
        [Tooltip("このスタンプが塗り付ける地面状態。Water は指定しても無視される（川・湖フェーズで付与される）。")]
        [SerializeField] private GroundState _state = GroundState.Swamp;

        [Tooltip("影響半径（ワールドメートル）。")]
        [SerializeField, Min(0.01f)] private float _radius = 5f;

        [Tooltip("true なら他の状態（Swamp など）の上も上書きする。false なら Normal のみ塗る。")]
        [SerializeField] private bool _overrideExistingState = false;

        [Tooltip("この状態を塗る最大高度（メートル）。HeightMap 値がこれを超えるセルは塗らない。0 以下で無効（全高度に塗る）。沼を平地限定にするときなどに使う。")]
        [SerializeField] private float _maxHeight = 0f;

        [Tooltip("輪郭を Perlin ノイズで歪ませる強さ。0 = 真円、0.3 = 半径が ±30% 揺れて自然な不整形に。")]
        [SerializeField, Range(0f, 0.6f)] private float _noiseAmplitude = 0.25f;

        [Tooltip("ノイズの空間周波数（1/メートル）。大きいほど細かい凹凸、小さいほどゆったりしたうねり。")]
        [SerializeField, Min(0.001f)] private float _noiseFrequency = 0.18f;

        public GroundState State => _state;
        public float Radius => _radius;
        public bool OverrideExistingState => _overrideExistingState;
        public float MaxHeight => _maxHeight;
        public float NoiseAmplitude => _noiseAmplitude;
        public float NoiseFrequency => _noiseFrequency;

        public override void Apply(MapData map, StampPlacement placement)
        {
            if (map == null) return;
            if (_state == GroundState.Water) return;

            GroundStateGrid g = map.GroundStates;
            HeightMap h = map.Height;
            float cs = g.CellSize;

            float noiseExpand = 1f + _noiseAmplitude;
            int cellRadius = Mathf.Max(1, Mathf.CeilToInt(_radius * noiseExpand / cs));

            Vector2Int center = g.WorldToCell(new Vector3(placement.Center.x, 0f, placement.Center.y));
            bool hasHeightLimit = _maxHeight > 0f;
            bool useNoise = _noiseAmplitude > 0f;

            float noiseSaltX = placement.Center.x * 0.41f + 17.3f;
            float noiseSaltY = placement.Center.y * 0.59f + 5.9f;

            for (int dy = -cellRadius; dy <= cellRadius; dy++)
            {
                for (int dx = -cellRadius; dx <= cellRadius; dx++)
                {
                    int x = center.x + dx;
                    int y = center.y + dy;
                    if (!g.IsInBounds(x, y)) continue;

                    float cellX = (x + 0.5f) * cs;
                    float cellY = (y + 0.5f) * cs;
                    float ddx = cellX - placement.Center.x;
                    float ddy = cellY - placement.Center.y;

                    float effectiveRadius = useNoise
                        ? ComputeEffectiveRadius(ddx, ddy, noiseSaltX, noiseSaltY)
                        : _radius;
                    if (ddx * ddx + ddy * ddy > effectiveRadius * effectiveRadius) continue;

                    GroundState current = g.GetCell(x, y);
                    if (current == GroundState.Water) continue;
                    if (current != GroundState.Normal && !_overrideExistingState) continue;

                    if (hasHeightLimit && h.SampleAt(new Vector3(cellX, 0f, cellY)) > _maxHeight) continue;

                    g.SetCell(x, y, _state);
                }
            }
        }

        /// <summary>
        /// 中心からのオフセット (dx, dy) における「そのスタンプの実効半径」を返す。
        /// Perlin ノイズで ±<c>_noiseAmplitude</c> 倍まで揺らすことで輪郭を不整形にする。
        /// </summary>
        private float ComputeEffectiveRadius(float dx, float dy, float saltX, float saltY)
        {
            float nx = dx * _noiseFrequency + saltX;
            float ny = dy * _noiseFrequency + saltY;
            float n = Mathf.PerlinNoise(nx, ny); // [0, 1]
            float perturb = (n - 0.5f) * 2f;     // [-1, 1]
            float radius = _radius * (1f + _noiseAmplitude * perturb);
            return radius < 0.01f ? 0.01f : radius;
        }
    }
}
