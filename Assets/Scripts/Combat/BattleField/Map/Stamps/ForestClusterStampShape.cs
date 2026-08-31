using UnityEngine;

namespace WarSimulation.Combat.Map
{
    /// <summary>
    /// 森のクラスター（木が集まるゾーン）を定義するスタンプ。
    ///   - 実行時に <see cref="MapData.ForestRegions"/> に不整形の領域を登録する
    ///     （RockPhase など後続フェーズがここを避けるのに使う）
    ///   - ゾーン内に <see cref="FeatureType.Tree"/> を <see cref="TreeCount"/> 本散布する
    ///     （<see cref="TreePlacementUtility.IsInsidePlayableBounds"/> で terrain 外は棄却）
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
        [SerializeField, Min(0)] private int _treeCount = 30;

        [Tooltip("木同士の最小間隔（メートル）。これ未満の距離に既存の木がある候補は棄却する。")]
        [SerializeField, Min(0f)] private float _treeMinDistance = 1.0f;

        [Tooltip("木を置く最大高度（メートル）。HeightMap 値がこれを超えるセルは置かない。0 以下で無効。")]
        [SerializeField] private float _maxHeight = 0f;

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
            map.AddForestRegion(CreateRegion(placement));
            PlaceTrees(map, placement);
        }

        internal ForestRegion CreateRegion(StampPlacement placement) =>
            new(placement.Center, _radius, _noiseAmplitude, _noiseFrequency);

        internal void PlaceTrees(MapData map, StampPlacement placement)
        {
            if (map == null) return;
            if (_treeCount <= 0) return;
            var region = CreateRegion(placement);

            uint seed = (uint)Mathf.FloorToInt(placement.Center.x * 73.9f + placement.Center.y * 41.1f + 1013.3f);
            var rng = new SystemRandom(unchecked((int)seed));
            float radius = region.OuterRadius;
            Vector2 min = Vector2.Max(Vector2.zero, placement.Center - Vector2.one * radius);
            Vector2 max = Vector2.Min(map.Height.WorldSize, placement.Center + Vector2.one * radius);
            if (max.x < min.x || max.y < min.y) return;
            var candidates = new PlacementCandidates(map, FeatureType.Tree,
                Rect.MinMaxRect(min.x, min.y, max.x, max.y), _treeMinDistance, rng);
            candidates.KeepWhere(pos =>
                TreePlacementUtility.IsValidTreeSite(map, region, pos, _maxHeight > 0f, _maxHeight));

            int placedCount = 0;
            while (placedCount < _treeCount && candidates.TryTake(rng, out Vector2 pos))
            {
                float y = map.Height.SampleAt(new Vector3(pos.x, 0f, pos.y));
                map.AddFeature(new PlacedFeature(FeatureType.Tree,
                    new Vector3(pos.x, y, pos.y), Quaternion.identity));
                placedCount++;
            }
            if (placedCount < _treeCount)
                Debug.LogWarning($"[ForestCluster] 配置可能な候補を使い切りました: {placedCount}/{_treeCount} 本");
        }

    }
}
