using UnityEngine;

namespace WarSimulation.Combat.Map
{
    /// <summary>
    /// 木が密集するゾーン。ForestClusterStampShape が登録し、
    /// 以降のフェーズ（例：RockPhase）がこの範囲を避けるのに使う。
    /// 実際の木本体は <see cref="FeatureType.Tree"/> として個別に PlacedFeature 化される。
    ///
    /// 輪郭は真円ではなく、Perlin ノイズで ±<c>NoiseAmplitude</c> 倍だけ半径が揺れる不整形。
    /// GroundPatchStampShape（沼・雪）と同じノイズ歪ませを採用しており、
    /// 「森だけ真円で浮く」見た目にならないようにする。
    /// </summary>
    public readonly struct ForestRegion
    {
        public Vector2 Center { get; }

        /// <summary>基本半径（ノイズ歪みの中心値、メートル）。</summary>
        public float Radius { get; }

        /// <summary>半径を揺らす強さ（0 = 真円、0.3 で ±30%）。</summary>
        public float NoiseAmplitude { get; }

        /// <summary>ノイズの空間周波数（1/メートル）。</summary>
        public float NoiseFrequency { get; }

        /// <summary>
        /// ノイズ歪みを含めた最大外径。衝突判定や描画のバウンディングボックス計算に使う。
        /// </summary>
        public float OuterRadius => Radius * (1f + Mathf.Max(0f, NoiseAmplitude));

        public ForestRegion(Vector2 center, float radius, float noiseAmplitude, float noiseFrequency)
        {
            Center = center;
            Radius = radius;
            NoiseAmplitude = Mathf.Max(0f, noiseAmplitude);
            NoiseFrequency = Mathf.Max(0.001f, noiseFrequency);
        }

        public bool Contains(Vector2 worldPos)
        {
            Vector2 d = worldPos - Center;
            float distSq = d.sqrMagnitude;
            float effectiveR = EffectiveRadius(d.x, d.y);
            return distSq <= effectiveR * effectiveR;
        }

        /// <summary>
        /// 中心からのオフセット (dx, dy) 方向における「実効半径」。
        /// Perlin ノイズで ±<see cref="NoiseAmplitude"/> 倍まで揺らす。
        /// </summary>
        public float EffectiveRadius(float dx, float dy)
        {
            if (NoiseAmplitude <= 0f) return Radius;

            // 中心座標を塩として加えることで、森ごとに違う歪み方になる
            float saltX = Center.x * 0.41f + 17.3f;
            float saltY = Center.y * 0.59f + 5.9f;
            float nx = dx * NoiseFrequency + saltX;
            float ny = dy * NoiseFrequency + saltY;
            float n = Mathf.PerlinNoise(nx, ny); // [0, 1]
            float perturb = (n - 0.5f) * 2f;     // [-1, 1]
            float r = Radius * (1f + NoiseAmplitude * perturb);
            return r < 0.01f ? 0.01f : r;
        }
    }
}
