using UnityEngine;

namespace WarSimulation.Combat.Map
{
    /// <summary>
    /// 森のクラスター（木が集まるゾーン）を定義するスタンプ。
    ///   - 実行時に <see cref="MapData.ForestRegions"/> に不整形の領域を登録する
    ///     （RockPhase など後続フェーズがここを避けるのに使う）
    ///   - ゾーン内に <see cref="FeatureType.Tree"/> を <see cref="TreeCount"/> 本散布する
    ///
    /// 輪郭は真円ではなく Perlin ノイズで歪ませる（<see cref="ForestRegion"/> 側で保持）。
    /// GroundPatchStampShape（沼・雪）と同じ方式で、バイオーム間で見た目の整合を取る。
    ///
    /// ゾーン自体は <see cref="GroundStateGrid"/> には書き込まない。木は「オブジェクト」であって
    /// 地面の状態ではないため、地面側は Normal / Swamp / Snow / Water の 4 状態だけで表現する。
    /// </summary>
    [CreateAssetMenu(menuName = "WarSim/Map/Forest Cluster Stamp", fileName = "ForestClusterStamp")]
    public sealed class ForestClusterStampShape : StampShape
    {
        [Tooltip("クラスターの基本半径（ワールドメートル）。Perlin ノイズで ±NoiseAmplitude 倍に揺らぐ。")]
        [SerializeField, Min(0.1f)] private float _radius = 5f;

        [Tooltip("このスタンプで散布する木の本数。")]
        [SerializeField, Min(0)] private int _treeCount = 18;

        [Tooltip("木同士の最小間隔（メートル）。これ未満の距離に既存の木がある候補は棄却する。")]
        [SerializeField, Min(0f)] private float _treeMinDistance = 1.2f;

        [Tooltip("木を置く最大高度（メートル）。HeightMap 値がこれを超えるセルは置かない。0 以下で無効。")]
        [SerializeField] private float _maxHeight = 0f;

        [Tooltip("1 本の木配置で試行する最大回数（棄却サンプリングの上限）。")]
        [SerializeField, Min(1)] private int _maxAttemptsPerTree = 12;

        [Tooltip("輪郭を Perlin ノイズで歪ませる強さ。0 = 真円、0.35 で半径が ±35% 揺れて自然な不整形に。")]
        [SerializeField, Range(0f, 0.6f)] private float _noiseAmplitude = 0.35f;

        [Tooltip("ノイズの空間周波数（1/メートル）。大きいほど細かい凹凸、小さいほどゆったりしたうねり。")]
        [SerializeField, Min(0.001f)] private float _noiseFrequency = 0.22f;

        public float Radius => _radius;
        public int TreeCount => _treeCount;
        public float TreeMinDistance => _treeMinDistance;
        public float MaxHeight => _maxHeight;
        public float NoiseAmplitude => _noiseAmplitude;
        public float NoiseFrequency => _noiseFrequency;

        public override void Apply(MapData map, StampPlacement placement)
        {
            if (map == null) return;
            if (_treeCount <= 0) return;

            // まずクラスター領域をノイズ歪み込みで登録（Rock フェーズや描画がここを避けるため）
            var region = new ForestRegion(placement.Center, _radius, _noiseAmplitude, _noiseFrequency);
            map.AddForestRegion(region);

            float minDistSq = _treeMinDistance * _treeMinDistance;
            bool hasHeightLimit = _maxHeight > 0f;

            // 配置中心ワールド座標から決定的な PRNG 状態を作り、スタンプ毎に安定した散らし方にする
            uint rngState = (uint)Mathf.FloorToInt(placement.Center.x * 73.9f + placement.Center.y * 41.1f + 1013.3f);
            if (rngState == 0u) rngState = 1u;

            // 棄却サンプリング用の外接円（ノイズで膨らむ最大半径）
            float samplingRadius = region.OuterRadius;

            int recordedStart = map.Features.Count;
            for (int i = 0; i < _treeCount; i++)
            {
                bool placed = false;
                for (int attempt = 0; attempt < _maxAttemptsPerTree && !placed; attempt++)
                {
                    Vector2 offset = RandomInsideDisc(ref rngState, samplingRadius);
                    Vector2 pos = placement.Center + offset;

                    // 森の不整形輪郭の内側のみ許可（真円ではなくノイズ歪みの内側）
                    if (!region.Contains(pos)) continue;

                    Vector3 world3 = new(pos.x, 0f, pos.y);

                    // 川・湖の上には木を植えない
                    if (map.GroundStates.SampleAt(world3) == GroundState.Water) continue;

                    if (hasHeightLimit && map.Height.SampleAt(world3) > _maxHeight) continue;

                    bool tooClose = false;
                    for (int j = recordedStart; j < map.Features.Count; j++)
                    {
                        if (map.Features[j].Type != FeatureType.Tree) continue;
                        Vector3 wp = map.Features[j].WorldPosition;
                        float ddx = wp.x - pos.x;
                        float ddz = wp.z - pos.y;
                        if (ddx * ddx + ddz * ddz < minDistSq)
                        {
                            tooClose = true;
                            break;
                        }
                    }
                    if (tooClose) continue;

                    float y = map.Height.SampleAt(world3);
                    map.AddFeature(new PlacedFeature(
                        FeatureType.Tree,
                        new Vector3(pos.x, y, pos.y),
                        Quaternion.identity));
                    placed = true;
                }
            }
        }

        /// <summary>
        /// xorshift32 ベースの軽量 PRNG で半径 r の円内一様分布サンプルを得る。
        /// 外部の IRandom に依存しないため、スタンプ内部だけで決定的に木を散らせる。
        /// </summary>
        private static Vector2 RandomInsideDisc(ref uint state, float r)
        {
            float angle = NextFloat01(ref state) * Mathf.PI * 2f;
            float rr = Mathf.Sqrt(NextFloat01(ref state)) * r;
            return new Vector2(Mathf.Cos(angle) * rr, Mathf.Sin(angle) * rr);
        }

        private static float NextFloat01(ref uint state)
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return (state & 0x00FFFFFFu) / (float)0x01000000;
        }
    }
}
