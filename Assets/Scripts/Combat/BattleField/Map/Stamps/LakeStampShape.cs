using UnityEngine;

namespace WarSimulation.Combat.Map
{
    /// <summary>
    /// 湖スタンプ：指定位置に円形のボウル型くぼみを掘り、GroundStateGrid に Water タグを付け、
    /// <see cref="MapData.Lakes"/> に <see cref="LakeRegion"/> を登録する。
    ///
    /// 掘削プロファイル：
    ///   中心で DepthMeters だけ深く、半径で 0 に向かう 2 次プロファイル。
    ///   合成は Min なので、元地形が既にそれより低ければ触らない。
    ///
    /// 輪郭は <see cref="_noiseAmplitude"/> により Perlin で半径が揺れ、
    /// <see cref="GroundPatchStampShape"/> と同様に真円からずらせる（0 で従来どおりの円）。
    ///
    /// 水面 Y の決め方：
    ///   「スタンプ適用前の中心高度」を起点に、DepthMeters * WaterSurfaceRatio だけ上を水面とする。
    ///   湖の水面は平らなので、全セルで同じ Y を使う。
    /// </summary>
    [CreateAssetMenu(menuName = "WarSim/Map/Lake Stamp", fileName = "LakeStamp")]
    public sealed class LakeStampShape : StampShape
    {
        [Tooltip("湖の半径（ワールドメートル）。")]
        [SerializeField, Min(0.1f)] private float _radius = 5f;

        [Tooltip("湖の最大深さ（ワールドメートル）。中心でこの値だけ地面を下げる。")]
        [SerializeField, Min(0.01f)] private float _depthMeters = 1.5f;

        [Tooltip("水面の高さを DepthMeters に対する比率で指定（0.85 で 85% の高さ＝岸近くまで水が来る）。")]
        [SerializeField, Range(0f, 1f)] private float _waterSurfaceRatio = 0.85f;

        [Tooltip("GroundStateGrid に Water タグを書き込む範囲を半径に対する比率で指定。岸は Water タグ化しない方が自然。")]
        [SerializeField, Range(0f, 1f)] private float _waterTagRatio = 0.9f;

        [Tooltip("輪郭を Perlin ノイズで歪ませる強さ。0 = 真円（従来どおり）、0.3 = 半径が ±30% 揺れる。")]
        [SerializeField, Range(0f, 0.6f)] private float _noiseAmplitude = 0f;

        [Tooltip("ノイズの空間周波数（1/メートル）。大きいほど細かい凹凸。")]
        [SerializeField, Min(0.001f)] private float _noiseFrequency = 0.16f;

        public float Radius => _radius;
        public float OuterRadius => _radius * (1f + Mathf.Max(0f, _noiseAmplitude));
        public float DepthMeters => _depthMeters;
        public float WaterSurfaceRatio => _waterSurfaceRatio;
        public float NoiseAmplitude => _noiseAmplitude;
        public float NoiseFrequency => _noiseFrequency;

        public override void Apply(MapData map, StampPlacement placement)
        {
            if (map == null) return;

            HeightMap h = map.Height;
            GroundStateGrid g = map.GroundStates;
            float cs = h.CellSize;

            int cxCell = Mathf.Clamp(Mathf.FloorToInt(placement.Center.x / cs), 0, h.Width - 1);
            int cyCell = Mathf.Clamp(Mathf.FloorToInt(placement.Center.y / cs), 0, h.Height - 1);
            float centerOriginalH = h.GetHeight(cxCell, cyCell);

            float waterY = (centerOriginalH - _depthMeters) + _waterSurfaceRatio * _depthMeters;

            CarveHeightMap(h, placement.Center, centerOriginalH, cs);
            TagWater(g, placement.Center);

            map.AddLake(new LakeRegion(
                placement.Center,
                _radius,
                waterY,
                isFrozen: false,
                waterTaggedRadius: _radius * _waterTagRatio,
                noiseAmplitude: _noiseAmplitude,
                noiseFrequency: _noiseFrequency));
        }

        private void CarveHeightMap(HeightMap h, Vector2 worldCenter, float centerOriginalH, float cs)
        {
            float noiseExpand = 1f + _noiseAmplitude;
            int cellRadius = Mathf.Max(1, Mathf.CeilToInt(_radius * noiseExpand / cs));
            int cxCell = Mathf.FloorToInt(worldCenter.x / cs);
            int cyCell = Mathf.FloorToInt(worldCenter.y / cs);

            bool useNoise = _noiseAmplitude > 0f;
            for (int dy = -cellRadius; dy <= cellRadius; dy++)
            {
                for (int dx = -cellRadius; dx <= cellRadius; dx++)
                {
                    int x = cxCell + dx;
                    int y = cyCell + dy;
                    if (!h.IsInBounds(x, y)) continue;

                    float wx = (x + 0.5f) * cs - worldCenter.x;
                    float wy = (y + 0.5f) * cs - worldCenter.y;
                    float distSq = wx * wx + wy * wy;
                    float eff = useNoise
                        ? LakeRegion.ComputeEffectiveRadius(
                            worldCenter, _radius, _noiseAmplitude, _noiseFrequency, wx, wy)
                        : _radius;
                    float effSq = eff * eff;
                    if (distSq > effSq) continue;

                    float t = Mathf.Sqrt(distSq) / eff;
                    // 中心で深さ DepthMeters、端で 0。 Min 合成なので既存が更に低いなら触らない。
                    float carved = centerOriginalH - _depthMeters * (1f - t * t);
                    float current = h.GetHeight(x, y);
                    if (carved < current) h.SetHeight(x, y, carved);
                }
            }
        }

        private void TagWater(GroundStateGrid g, Vector2 worldCenter)
        {
            float gCell = g.CellSize;
            float noiseExpand = 1f + _noiseAmplitude;
            float boundR = _radius * noiseExpand * _waterTagRatio;
            int cxCell = Mathf.FloorToInt(worldCenter.x / gCell);
            int cyCell = Mathf.FloorToInt(worldCenter.y / gCell);
            int gR = Mathf.Max(0, Mathf.CeilToInt(boundR / gCell));
            bool useNoise = _noiseAmplitude > 0f;

            for (int dy = -gR; dy <= gR; dy++)
            {
                for (int dx = -gR; dx <= gR; dx++)
                {
                    int x = cxCell + dx;
                    int y = cyCell + dy;
                    if (!g.IsInBounds(x, y)) continue;

                    float cxw = (x + 0.5f) * gCell - worldCenter.x;
                    float cyw = (y + 0.5f) * gCell - worldCenter.y;
                    float distSq = cxw * cxw + cyw * cyw;
                    float eff = useNoise
                        ? LakeRegion.ComputeEffectiveRadius(
                            worldCenter, _radius, _noiseAmplitude, _noiseFrequency, cxw, cyw)
                        : _radius;
                    float tagR = eff * _waterTagRatio;
                    if (distSq > tagR * tagR) continue;

                    g.SetCell(x, y, GroundState.Water);
                }
            }
        }
    }
}
