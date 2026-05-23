using UnityEngine;

namespace WarSimulation.Combat.Map
{
    /// <summary>
    /// 湖 1 つ分のデータ（ワールド上の中心・基準半径・水面 Y）。
    /// Render 層（LakeRenderer）が水面メッシュを張る際に参照する。
    /// 輪郭は <see cref="NoiseAmplitude"/> により Perlin で半径が揺れ、真円でない場合がある。
    /// </summary>
    public readonly struct LakeRegion
    {
        /// <summary>ワールド XZ 上の中心（X = .x, Z = .y）。</summary>
        public Vector2 Center { get; }

        /// <summary>基準半径（ワールドメートル）。ノイズで実効半径がこれを中心に揺れる。</summary>
        public float Radius { get; }

        /// <summary>水面のワールド Y。湖は水面が平らなので 1 値で保持する。</summary>
        public float WaterY { get; }

        /// <summary>
        /// 凍結しているか。true の場合、水面は氷として描画され、
        /// 将来の移動判定で「歩行可能」として扱われる想定。
        /// GroundStateGrid 側は従来どおり Water タグのままなので、
        /// 他の配置フェーズ（木・岩など）は湖を避け続ける。
        /// </summary>
        public bool IsFrozen { get; }

        /// <summary>
        /// 各方向の実効半径に対して、<see cref="GroundState.Water"/> を付ける内側の比率（0〜1）。
        /// </summary>
        public float WaterTagRatio { get; }

        /// <summary>輪郭の Perlin 歪みの強さ。0 で真円（基準半径のみ）。</summary>
        public float NoiseAmplitude { get; }

        /// <summary>ノイズの空間周波数（1/メートル）。</summary>
        public float NoiseFrequency { get; }

        /// <summary>ノイズで膨らんだ外周の上限（掘削・バウンディングに使用）。</summary>
        public float OuterRadius => Radius * (1f + Mathf.Max(0f, NoiseAmplitude));

        /// <summary>互換・概算：基準半径×水タグ比率（円として見た内側半径）。</summary>
        public float WaterTaggedRadius => Radius * WaterTagRatio;

        public LakeRegion(
            Vector2 center,
            float radius,
            float waterY,
            bool isFrozen = false,
            float waterTaggedRadius = -1f,
            float noiseAmplitude = 0f,
            float noiseFrequency = 0.18f)
        {
            Center = center;
            Radius = radius;
            WaterY = waterY;
            IsFrozen = isFrozen;
            float tagAbs = waterTaggedRadius > 0f ? waterTaggedRadius : radius * 0.9f;
            WaterTagRatio = Mathf.Clamp01(tagAbs / Mathf.Max(0.01f, radius));
            NoiseAmplitude = Mathf.Max(0f, noiseAmplitude);
            NoiseFrequency = Mathf.Max(0.001f, noiseFrequency);
        }

        /// <summary>
        /// 中心からのオフセット (dx, dy) における実効半径（ワールドメートル）。
        /// </summary>
        public float EffectiveRadius(float dx, float dy)
        {
            return ComputeEffectiveRadius(Center, Radius, NoiseAmplitude, NoiseFrequency, dx, dy);
        }

        public static float ComputeEffectiveRadius(
            Vector2 center,
            float baseRadius,
            float noiseAmplitude,
            float noiseFrequency,
            float dx,
            float dy)
        {
            if (noiseAmplitude <= 0f) return baseRadius;

            float saltX = center.x * 0.41f + 17.3f;
            float saltY = center.y * 0.59f + 5.9f;
            float nx = dx * noiseFrequency + saltX;
            float ny = dy * noiseFrequency + saltY;
            float n = Mathf.PerlinNoise(nx, ny);
            float perturb = (n - 0.5f) * 2f;
            float r = baseRadius * (1f + noiseAmplitude * perturb);
            return r < 0.01f ? 0.01f : r;
        }

        public bool ContainsCarve(Vector2 worldPos)
        {
            Vector2 d = worldPos - Center;
            float eff = EffectiveRadius(d.x, d.y);
            return d.sqrMagnitude <= eff * eff;
        }

        public bool ContainsWaterTagged(Vector2 worldPos)
        {
            Vector2 d = worldPos - Center;
            float eff = EffectiveRadius(d.x, d.y) * WaterTagRatio;
            return d.sqrMagnitude <= eff * eff;
        }

        /// <summary>水面メッシュの輪郭用。単位方向 u 上の岸までの距離（固定点反復）。</summary>
        public float BoundaryRadiusAlong(Vector2 u)
        {
            if (u.sqrMagnitude < 1e-8f) return Radius;
            u.Normalize();
            if (NoiseAmplitude <= 0f) return Radius;
            float r = Radius;
            for (int k = 0; k < 12; k++)
            {
                float nr = EffectiveRadius(u.x * r, u.y * r);
                if (Mathf.Abs(nr - r) < 1e-3f) break;
                r = nr;
            }
            return r;
        }
    }
}
