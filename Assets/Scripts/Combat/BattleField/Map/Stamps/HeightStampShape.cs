using UnityEngine;

namespace WarSimulation.Combat.Map
{
    /// <summary>
    /// パラメトリックに定義される高度スタンプ。
    /// 形状種別・半径・ピーク高さなどの数値のみで決定されるため、
    /// デザイナーが手で float[,] を編集する必要はない。
    /// </summary>
    [CreateAssetMenu(menuName = "WarSim/Map/Height Stamp", fileName = "HeightStamp")]
    public sealed class HeightStampShape : StampShape
    {
        [SerializeField] private HeightShapeKind _kind = HeightShapeKind.Dome;

        [Tooltip("影響半径（ワールド単位）。Ridge の場合は両端の円の半径として解釈。")]
        [SerializeField, Min(0.01f)] private float _radius = 5f;

        [Tooltip("中心部に与える高度デルタ（負値で盆地）。")]
        [SerializeField] private float _peakDelta = 5f;

        [Tooltip("Ridge 形状のときのみ意味を持つ長軸長さ（ローカル X 方向）。")]
        [SerializeField, Min(0f)] private float _ridgeLength = 10f;

        [Tooltip("てっぺんを平らにする範囲を半径比で指定。0 = 中心だけ尖る（従来どおり）、0.5 = 内側 50% が台地状の平面、0.99 = ほぼ板。")]
        [SerializeField, Range(0f, 0.99f)] private float _flatTopRatio = 0f;

        [Tooltip("輪郭を Perlin ノイズで歪ませる強さ。0 = 真円（従来どおり）、0.3 = 半径が ±30% 揺れて自然な不整形に。")]
        [SerializeField, Range(0f, 0.6f)] private float _noiseAmplitude = 0.2f;

        [Tooltip("ノイズの空間周波数（1/メートル）。大きいほど細かい凹凸、小さいほどゆったりしたうねり。")]
        [SerializeField, Min(0.001f)] private float _noiseFrequency = 0.15f;

        [Header("Cliff (急斜面) セクター")]
        [Tooltip("円周のうち『断崖（登れない急斜面）』にする中心角（度）。0 = 断崖なし＝従来どおり全周緩やか。180 = 半円が断崖。")]
        [SerializeField, Range(0f, 360f)] private float _cliffArcDeg = 0f;

        [Tooltip("断崖が向く方向（ローカル +X を 0° とした度数、反時計回り）。配置の Rotation でワールド向きが決まる。")]
        [SerializeField, Range(0f, 360f)] private float _cliffDirectionDeg = 0f;

        [Tooltip("断崖側のスカート幅を、実効半径に対する比率で指定。0.1 = 外縁の 10% 区間で peakDelta を一気に落とす（ほぼ断崖）。緩側は従来どおり FlatTopRatio から外縁までで落とす。\nメモ: 緩側を 30° 以下に収めたいなら  skirt幅 >= peakDelta * 1.73  が目安。例) peakDelta=5m なら緩側 skirt 幅 8.7m 以上。")]
        [SerializeField, Range(0.01f, 1f)] private float _cliffSkirtRatio = 0.12f;

        [Tooltip("断崖セクターと緩セクターの境界をぼかす角度幅（度）。大きいほど境界がなめらかにブレンド。")]
        [SerializeField, Range(0f, 45f)] private float _cliffBlendDeg = 8f;

        [SerializeField] private HeightBlendMode _blend = HeightBlendMode.Add;

        public HeightShapeKind Kind => _kind;
        public float Radius => _radius;
        public float PeakDelta => _peakDelta;
        public float RidgeLength => _ridgeLength;
        public float FlatTopRatio => _flatTopRatio;
        public float NoiseAmplitude => _noiseAmplitude;
        public float NoiseFrequency => _noiseFrequency;
        public float CliffArcDeg => _cliffArcDeg;
        public float CliffDirectionDeg => _cliffDirectionDeg;
        public float CliffSkirtRatio => _cliffSkirtRatio;
        public HeightBlendMode Blend => _blend;

        public override void Apply(MapData map, StampPlacement placement)
        {
            if (map == null) return;

            HeightMap h = map.Height;
            float cs = h.CellSize;

            float scaleMax = Mathf.Max(placement.Scale.x, placement.Scale.y);
            // ノイズが半径を増やす方向にも触れるので、バウンディングも広げておく
            float noiseExpand = 1f + _noiseAmplitude;
            float extent = _kind == HeightShapeKind.Ridge
                ? (_radius * noiseExpand + _ridgeLength * 0.5f) * scaleMax
                : _radius * noiseExpand * scaleMax;

            int x0 = Mathf.Max(0, Mathf.FloorToInt((placement.Center.x - extent) / cs));
            int x1 = Mathf.Min(h.Width - 1, Mathf.CeilToInt((placement.Center.x + extent) / cs));
            int z0 = Mathf.Max(0, Mathf.FloorToInt((placement.Center.y - extent) / cs));
            int z1 = Mathf.Min(h.Height - 1, Mathf.CeilToInt((placement.Center.y + extent) / cs));

            float cosR = Mathf.Cos(-placement.RotationRad);
            float sinR = Mathf.Sin(-placement.RotationRad);
            float invScaleX = 1f / Mathf.Max(0.0001f, placement.Scale.x);
            float invScaleY = 1f / Mathf.Max(0.0001f, placement.Scale.y);

            // スタンプ毎に異なるノイズパターンを出すためのソルト（配置ワールド座標から導出）
            float noiseSaltX = placement.Center.x * 0.37f + 13.1f;
            float noiseSaltY = placement.Center.y * 0.53f + 7.7f;

            for (int z = z0; z <= z1; z++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    float wx = (x + 0.5f) * cs - placement.Center.x;
                    float wz = (z + 0.5f) * cs - placement.Center.y;

                    // 逆回転 + 逆スケールでローカル座標へ写す
                    float lx = (wx * cosR - wz * sinR) * invScaleX;
                    float lz = (wx * sinR + wz * cosR) * invScaleY;

                    float delta = EvaluateLocal(lx, lz, noiseSaltX, noiseSaltY);
                    if (delta == 0f) continue;

                    float old = h.GetHeight(x, z);
                    float blended = _blend switch
                    {
                        HeightBlendMode.Min => Mathf.Min(old, old + delta),
                        HeightBlendMode.Max => Mathf.Max(old, old + delta),
                        _ => old + delta,
                    };
                    h.SetHeight(x, z, blended);
                }
            }
        }

        /// <summary>
        /// ローカル座標 (lx, lz) における高度デルタを評価する。影響外は 0。
        /// ノイズが有効なとき、実効半径を Perlin ノイズで揺らして輪郭を不整形にする。
        /// Cliff セクターでは角度ごとにスカート幅を圧縮し、局所的に急斜面（断崖）を作る。
        /// </summary>
        private float EvaluateLocal(float lx, float lz, float noiseSaltX, float noiseSaltY)
        {
            float effectiveRadius = ComputeEffectiveRadius(lx, lz, noiseSaltX, noiseSaltY);

            switch (_kind)
            {
                case HeightShapeKind.Cone:
                {
                    float r = Mathf.Sqrt(lx * lx + lz * lz);
                    float cliff = ComputeCliffFactor(lx, lz);
                    return EvaluateWithFlatTop(r, effectiveRadius, linearFalloff: true, cliffFactor: cliff);
                }

                case HeightShapeKind.Ridge:
                {
                    // 両端を結ぶ線分までの距離で円形減衰させる（カプセル形状）。
                    // Ridge は方向性が強いため Cliff セクターは未対応（常に緩側扱い）。
                    float halfLen = _ridgeLength * 0.5f;
                    float alongExcess = Mathf.Max(0f, Mathf.Abs(lx) - halfLen);
                    float perp = Mathf.Abs(lz);
                    float r = Mathf.Sqrt(alongExcess * alongExcess + perp * perp);
                    return EvaluateWithFlatTop(r, effectiveRadius, linearFalloff: false, cliffFactor: 1f);
                }

                case HeightShapeKind.Dome:
                default:
                {
                    float r = Mathf.Sqrt(lx * lx + lz * lz);
                    float cliff = ComputeCliffFactor(lx, lz);
                    return EvaluateWithFlatTop(r, effectiveRadius, linearFalloff: false, cliffFactor: cliff);
                }
            }
        }

        /// <summary>
        /// ローカル (lx, lz) がどれだけ「緩側」寄りかを [0, 1] で返す。
        /// 0 = 完全に断崖セクター内 / 1 = 完全に緩セクター / 中間はブレンド。
        /// CliffArcDeg == 0 のときは常に 1（従来どおり）。
        /// </summary>
        private float ComputeCliffFactor(float lx, float lz)
        {
            if (_cliffArcDeg <= 0f) return 1f;

            // 原点付近は角度が安定しないのでブレンドを効かせる（0 除算防止も兼ねる）。
            float distSq = lx * lx + lz * lz;
            if (distSq < 1e-6f) return 1f;

            float angleDeg = Mathf.Atan2(lz, lx) * Mathf.Rad2Deg;
            float diff = Mathf.Abs(Mathf.DeltaAngle(angleDeg, _cliffDirectionDeg));
            float halfArc = _cliffArcDeg * 0.5f;

            float inner = halfArc - _cliffBlendDeg;
            float outer = halfArc + _cliffBlendDeg;

            if (diff <= inner) return 0f; // 完全断崖
            if (diff >= outer) return 1f; // 完全緩
            float t = (diff - inner) / Mathf.Max(0.0001f, outer - inner);
            return t * t * (3f - 2f * t); // smoothstep
        }

        /// <summary>
        /// ローカル位置にもとづく Perlin ノイズで、半径を ±<c>_noiseAmplitude</c> だけ揺らす。
        /// 位置依存なので、同じスタンプ内でも場所によって境界が出たり引っ込んだりする＝不整形な輪郭になる。
        /// </summary>
        private float ComputeEffectiveRadius(float lx, float lz, float noiseSaltX, float noiseSaltY)
        {
            if (_noiseAmplitude <= 0f) return _radius;

            float nx = lx * _noiseFrequency + noiseSaltX;
            float ny = lz * _noiseFrequency + noiseSaltY;
            float n = Mathf.PerlinNoise(nx, ny); // [0, 1]
            float perturb = (n - 0.5f) * 2f;     // [-1, 1]
            float radius = _radius * (1f + _noiseAmplitude * perturb);
            return radius < 0.01f ? 0.01f : radius;
        }

        /// <summary>
        /// 「中心からの距離 r」を入力に、FlatTopRatio と Cliff 係数を加味した高度デルタを返す。
        /// cliffFactor = 1 のとき従来どおりの挙動。
        /// cliffFactor = 0 のとき、スカートを外縁に押しつけて急斜面（断崖）にする。
        /// </summary>
        /// <param name="cliffFactor">1 = 緩側、0 = 断崖側、中間は補間。</param>
        private float EvaluateWithFlatTop(float r, float effectiveRadius, bool linearFalloff, float cliffFactor)
        {
            if (r >= effectiveRadius) return 0f;

            // 緩側の inner（天面の外縁）：従来どおり _flatTopRatio で決める
            float gentleInner = _flatTopRatio * effectiveRadius;
            // 断崖側の inner：外縁に寄せてスカートを圧縮。_cliffSkirtRatio が小さいほど崖が急。
            float cliffInner = effectiveRadius * (1f - Mathf.Max(0.001f, _cliffSkirtRatio));
            // 断崖側は緩側より外でないと逆転してしまうので下限を保つ
            if (cliffInner < gentleInner) cliffInner = gentleInner;

            float inner = Mathf.Lerp(cliffInner, gentleInner, cliffFactor);

            if (r <= inner) return _peakDelta;

            float skirt = (r - inner) / (effectiveRadius - inner);
            float t = 1f - skirt;
            if (linearFalloff) return _peakDelta * t;
            return _peakDelta * t * t * (3f - 2f * t);
        }
    }
}
