using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace WarSimulation.Combat.Map
{
    public enum AuthoredAssaultRouteSource
    {
        Auto,
        Manual,
    }

    [Serializable]
    public sealed class AuthoredAssaultRoute
    {
        public string RouteId;
        public string DisplayName;
        public AuthoredAssaultRouteSource Source;
        public List<Vector2> Waypoints = new();

        public AuthoredAssaultRoute(
            string routeId,
            string displayName,
            AuthoredAssaultRouteSource source,
            IEnumerable<Vector2> waypoints = null)
        {
            RouteId = routeId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            Source = source;
            Waypoints = waypoints != null ? new List<Vector2>(waypoints) : new List<Vector2>();
        }
    }

    /// <summary>
    /// Editor ベイク済みの侵攻ルート 1 本分（マップローカル座標）。
    /// </summary>
    [Serializable]
    public struct AuthoredBakedAssaultRoute
    {
        public int BridgeFeatureIndex;
        public bool HasBridgeWaypoints;
        public Vector3 EnterLocal;
        public Vector3 ExitLocal;

        public AuthoredBakedAssaultRoute(
            int bridgeFeatureIndex,
            bool hasBridgeWaypoints,
            Vector3 enterLocal,
            Vector3 exitLocal)
        {
            BridgeFeatureIndex = bridgeFeatureIndex;
            HasBridgeWaypoints = hasBridgeWaypoints;
            EnterLocal = enterLocal;
            ExitLocal = exitLocal;
        }
    }

    /// <summary>
    /// 手作りマップ 1 枚分の配置レシピ。実行時は <see cref="AuthoredMapBuilder"/> で MapData に展開する。
    /// </summary>
    [CreateAssetMenu(menuName = "WarSim/Map/手作りマップ", fileName = "AuthoredMap")]
    public sealed class AuthoredMapDefinition : ScriptableObject
    {
        [Tooltip("グリッドサイズ・橋寸法・既定の川形状・木/岩散布などを共有する設定。")]
        [SerializeField] private MapConfig _sharedConfig;

        [SerializeField] private int _buildSeed;

        [Header("Baked Navigation")]
        [SerializeField] private NavMeshData _bakedNavMesh;
        [SerializeField] private int _navMeshBakeFingerprint;
        [SerializeField] private BakedMapData _bakedMapData;
        [SerializeField, HideInInspector] private List<AuthoredBakedAssaultRoute> _bakedAllyAssaultRoutes = new();
        [SerializeField, HideInInspector] private List<AuthoredBakedAssaultRoute> _bakedEnemyAssaultRoutes = new();
        [SerializeField, HideInInspector] private int _assaultRouteBakeFingerprint;
        [SerializeField, HideInInspector] private bool _hasBakedAssaultRoutes;
        [SerializeField, HideInInspector] private bool _legacyAssaultRoutesMigrated;

        [Header("Assault Routes")]
        [SerializeField] private List<AuthoredAssaultRoute> _assaultRoutes = new();

        [Header("Baked Preview")]
        [SerializeField] private Texture2D _bakedPreview;
        [SerializeField] private int _previewBakeFingerprint;

        [SerializeField] private List<AuthoredMountainPlacement> _mountains = new();
        [SerializeField] private List<AuthoredRiverPlacement> _rivers = new();
        [SerializeField] private List<AuthoredLakePlacement> _lakes = new();
        [SerializeField] private List<AuthoredGroundPatchPlacement> _groundPatches = new();
        [SerializeField] private List<AuthoredForestPlacement> _forests = new();
        [SerializeField] private List<AuthoredBridgePlacement> _bridges = new();
        // 散布木・岩は AuthoredMapBuilder が SharedConfig の散布ルールで配置する（リストは未使用・互換用）。
        [SerializeField] private List<AuthoredPointFeaturePlacement> _trees = new();
        [SerializeField] private List<AuthoredPointFeaturePlacement> _rocks = new();
        [SerializeField] private List<AuthoredMagicStonePlacement> _magicStones = new();

        public MapConfig SharedConfig
        {
            get => _sharedConfig;
            set => _sharedConfig = value;
        }

        public int BuildSeed
        {
            get => _buildSeed;
            set => _buildSeed = value;
        }

        public NavMeshData BakedNavMesh => _bakedNavMesh;
        public int NavMeshBakeFingerprint => _navMeshBakeFingerprint;
        public BakedMapData BakedMapData => _bakedMapData;
        public IReadOnlyList<AuthoredBakedAssaultRoute> BakedAllyAssaultRoutes => _bakedAllyAssaultRoutes;
        public IReadOnlyList<AuthoredBakedAssaultRoute> BakedEnemyAssaultRoutes => _bakedEnemyAssaultRoutes;
        public int AssaultRouteBakeFingerprint => _assaultRouteBakeFingerprint;
        public Texture2D BakedPreview => _bakedPreview;
        public int PreviewBakeFingerprint => _previewBakeFingerprint;

        public List<AuthoredMountainPlacement> Mountains => _mountains;
        public List<AuthoredRiverPlacement> Rivers => _rivers;
        public List<AuthoredLakePlacement> Lakes => _lakes;
        public List<AuthoredGroundPatchPlacement> GroundPatches => _groundPatches;
        public List<AuthoredForestPlacement> Forests => _forests;
        public List<AuthoredBridgePlacement> Bridges => _bridges;
        public List<AuthoredPointFeaturePlacement> Trees => _trees;
        public List<AuthoredPointFeaturePlacement> Rocks => _rocks;
        public List<AuthoredMagicStonePlacement> MagicStones => _magicStones;
        public List<AuthoredAssaultRoute> AssaultRoutes => _assaultRoutes;

        public bool HasValidBakedNavMesh =>
            _bakedNavMesh != null && _navMeshBakeFingerprint == ComputeGeometryFingerprint();

        public bool HasValidBakedMapData =>
            _bakedMapData != null && _bakedMapData.IsValidFor(ComputeGeometryFingerprint());

        public bool HasValidBakedAssaultRoutes =>
            (_bakedMapData != null && _bakedMapData.HasValidAssaultRoutes(ComputeAssaultRouteFingerprint())) ||
            HasValidLegacyBakedAssaultRoutes;

        public bool HasValidLegacyBakedAssaultRoutes =>
            (_assaultRoutes == null || _assaultRoutes.Count == 0) &&
            _hasBakedAssaultRoutes && _bakedAllyAssaultRoutes != null && _bakedAllyAssaultRoutes.Count > 0 &&
            _assaultRouteBakeFingerprint == ComputeGeometryFingerprint();

        public bool HasBakedAssaultRoutesData =>
            (_bakedMapData != null && _bakedMapData.HasAssaultRoutesData) ||
            ((_assaultRoutes == null || _assaultRoutes.Count == 0) && _hasBakedAssaultRoutes);

        public bool HasValidBakedPreview =>
            _bakedPreview != null && _previewBakeFingerprint ==
                ((_assaultRoutes != null && _assaultRoutes.Count > 0)
                    ? ComputeAssaultRouteFingerprint()
                    : ComputeGeometryFingerprint());

        /// <summary>マップ内容が変わると変わる fingerprint。ベイク鮮度判定用（Editor/Runtime で同一・決定的）。</summary>
        public int ComputeGeometryFingerprint()
        {
            unchecked
            {
                int hash = _buildSeed;
                hash = MixConfig(hash, _sharedConfig);
                hash = MixMountains(hash, _mountains);
                hash = MixRivers(hash, _rivers);
                hash = MixLakes(hash, _lakes);
                hash = MixGroundPatches(hash, _groundPatches);
                hash = MixForests(hash, _forests);
                hash = MixBridges(hash, _bridges);
                hash = MixMagicStones(hash, _magicStones);
                return hash;
            }
        }

        public int ComputeAssaultRouteFingerprint()
        {
            unchecked
            {
                int hash = ComputeGeometryFingerprint();
                hash = Mix(hash, _assaultRoutes != null ? _assaultRoutes.Count : 0);
                if (_assaultRoutes == null) return hash;
                for (int i = 0; i < _assaultRoutes.Count; i++)
                {
                    AuthoredAssaultRoute route = _assaultRoutes[i];
                    if (route == null) continue;
                    hash = Mix(hash, StableStringHash(route.RouteId));
                    hash = Mix(hash, StableStringHash(route.DisplayName));
                    hash = Mix(hash, (int)route.Source);
                    hash = Mix(hash, route.Waypoints != null ? route.Waypoints.Count : 0);
                    if (route.Waypoints == null) continue;
                    for (int p = 0; p < route.Waypoints.Count; p++)
                        hash = Mix(hash, route.Waypoints[p]);
                }

                return hash;
            }
        }

        public int ComputeBakeFingerprint() => ComputeGeometryFingerprint();

        public bool MigrateLegacyAssaultRoutes()
        {
            if (_legacyAssaultRoutesMigrated) return false;
            _assaultRoutes ??= new List<AuthoredAssaultRoute>();
            if (_assaultRoutes.Count == 0 && _hasBakedAssaultRoutes && _bakedAllyAssaultRoutes != null)
            {
                for (int i = 0; i < _bakedAllyAssaultRoutes.Count; i++)
                {
                    AuthoredBakedAssaultRoute legacy = _bakedAllyAssaultRoutes[i];
                    bool direct = !legacy.HasBridgeWaypoints;
                    var waypoints = new List<Vector2>();
                    if (!direct)
                    {
                        waypoints.Add(new Vector2(legacy.EnterLocal.x, legacy.EnterLocal.z));
                        waypoints.Add(new Vector2(legacy.ExitLocal.x, legacy.ExitLocal.z));
                    }

                    int bridgeIndex = FindAuthoredBridgeIndex(legacy.BridgeFeatureIndex);
                    string id = direct ? "auto:direct" : $"auto:bridge:{bridgeIndex}";
                    _assaultRoutes.Add(new AuthoredAssaultRoute(
                        id,
                        direct ? "直進" : $"橋ルート {bridgeIndex + 1}",
                        AuthoredAssaultRouteSource.Auto,
                        waypoints));
                }
            }

            _legacyAssaultRoutesMigrated = true;
            return true;
        }

        private int FindAuthoredBridgeIndex(int legacyBridgeFeatureIndex)
        {
            if (_bridges == null) return legacyBridgeFeatureIndex;
            int featureIndex = -1;
            for (int i = 0; i < _bridges.Count; i++)
            {
                if (_bridges[i] == null) continue;
                featureIndex++;
                if (featureIndex == legacyBridgeFeatureIndex) return i;
            }
            return legacyBridgeFeatureIndex;
        }

        private static int MixConfig(int hash, MapConfig config)
        {
            if (config == null) return Mix(hash, 0);
            hash = Mix(hash, StableStringHash(config.name));
            hash = Mix(hash, config.WorldSize);
            hash = Mix(hash, config.HeightMapResolution);
            hash = Mix(hash, config.BaseHeight);
            hash = Mix(hash, StableStringHash(config.RiverShape != null ? config.RiverShape.name : null));
            hash = MixRiverShape(hash, config.RiverShape);
            hash = Mix(hash, config.FlatRiverMeanderAmplitude);
            hash = Mix(hash, config.FlatRiverMeanderFrequency);
            hash = Mix(hash, config.BridgeLengthExtraMargin);
            hash = Mix(hash, config.BridgeWidth);
            hash = Mix(hash, config.BridgeThickness);
            hash = Mix(hash, config.BridgeHeightAboveWater);
            hash = Mix(hash, config.BridgeFeatureExclusionMargin);
            hash = Mix(hash, config.ScatterTreeCount);
            hash = Mix(hash, config.ScatterTreeMinDistance);
            hash = Mix(hash, config.ScatterTreePlacementMargin);
            hash = Mix(hash, config.RockCount);
            hash = Mix(hash, config.RockMinDistance);
            hash = Mix(hash, config.RockPlacementMargin);
            hash = Mix(hash, config.RockTopHeightExclusionRatio);
            return hash;
        }

        private static int MixMountains(int hash, List<AuthoredMountainPlacement> list)
        {
            hash = Mix(hash, list != null ? list.Count : 0);
            if (list == null) return hash;
            for (int i = 0; i < list.Count; i++)
            {
                AuthoredMountainPlacement p = list[i];
                if (p == null) continue;
                hash = Mix(hash, StableStringHash(p.Shape != null ? p.Shape.name : null));
                hash = MixHeightStamp(hash, p.Shape);
                hash = Mix(hash, (int)p.Kind);
                hash = Mix(hash, p.Center);
                hash = Mix(hash, p.RotationDeg);
                hash = Mix(hash, p.Scale);
            }

            return hash;
        }

        private static int MixRivers(int hash, List<AuthoredRiverPlacement> list)
        {
            hash = Mix(hash, list != null ? list.Count : 0);
            if (list == null) return hash;
            for (int i = 0; i < list.Count; i++)
            {
                AuthoredRiverPlacement river = list[i];
                if (river == null) continue;
                hash = Mix(hash, StableStringHash(river.Shape != null ? river.Shape.name : null));
                hash = MixRiverShape(hash, river.Shape);
                if (river.ControlPoints == null) continue;
                hash = Mix(hash, river.ControlPoints.Count);
                for (int p = 0; p < river.ControlPoints.Count; p++)
                    hash = Mix(hash, river.ControlPoints[p]);
            }

            return hash;
        }

        private static int MixLakes(int hash, List<AuthoredLakePlacement> list)
        {
            hash = Mix(hash, list != null ? list.Count : 0);
            if (list == null) return hash;
            for (int i = 0; i < list.Count; i++)
            {
                AuthoredLakePlacement p = list[i];
                if (p == null) continue;
                hash = Mix(hash, StableStringHash(p.Shape != null ? p.Shape.name : null));
                hash = MixLakeStamp(hash, p.Shape);
                hash = Mix(hash, p.Center);
                hash = Mix(hash, p.RotationDeg);
                hash = Mix(hash, p.Scale);
                hash = Mix(hash, p.IsFrozen ? 1 : 0);
            }

            return hash;
        }

        private static int MixGroundPatches(int hash, List<AuthoredGroundPatchPlacement> list)
        {
            hash = Mix(hash, list != null ? list.Count : 0);
            if (list == null) return hash;
            for (int i = 0; i < list.Count; i++)
            {
                AuthoredGroundPatchPlacement p = list[i];
                if (p == null) continue;
                hash = Mix(hash, StableStringHash(p.Shape != null ? p.Shape.name : null));
                hash = MixGroundPatchStamp(hash, p.Shape);
                hash = Mix(hash, p.Center);
                hash = Mix(hash, p.RotationDeg);
                hash = Mix(hash, p.Scale);
            }

            return hash;
        }

        private static int MixForests(int hash, List<AuthoredForestPlacement> list)
        {
            hash = Mix(hash, list != null ? list.Count : 0);
            if (list == null) return hash;
            for (int i = 0; i < list.Count; i++)
            {
                AuthoredForestPlacement p = list[i];
                if (p == null) continue;
                hash = Mix(hash, StableStringHash(p.Shape != null ? p.Shape.name : null));
                hash = MixForestStamp(hash, p.Shape);
                hash = Mix(hash, p.Center);
                hash = Mix(hash, p.RotationDeg);
                hash = Mix(hash, p.Scale);
            }

            return hash;
        }

        private static int MixBridges(int hash, List<AuthoredBridgePlacement> list)
        {
            hash = Mix(hash, list != null ? list.Count : 0);
            if (list == null) return hash;
            for (int i = 0; i < list.Count; i++)
            {
                AuthoredBridgePlacement p = list[i];
                if (p == null) continue;
                hash = Mix(hash, p.Center);
                hash = Mix(hash, p.RotationDeg);
                hash = Mix(hash, p.Scale.x);
                hash = Mix(hash, p.Scale.y);
                hash = Mix(hash, p.Scale.z);
            }

            return hash;
        }

        private static int MixMagicStones(int hash, List<AuthoredMagicStonePlacement> list)
        {
            hash = Mix(hash, list != null ? list.Count : 0);
            if (list == null) return hash;
            for (int i = 0; i < list.Count; i++)
            {
                AuthoredMagicStonePlacement p = list[i];
                if (p == null) continue;
                hash = Mix(hash, (int)p.Type);
                hash = Mix(hash, p.Center);
            }

            return hash;
        }

        private static int MixHeightStamp(int hash, HeightStampShape shape)
        {
            if (shape == null) return hash;
            hash = Mix(hash, (int)shape.Kind);
            hash = Mix(hash, shape.Radius);
            hash = Mix(hash, shape.PeakDelta);
            hash = Mix(hash, shape.RidgeLength);
            hash = Mix(hash, shape.FlatTopRatio);
            hash = Mix(hash, shape.NoiseAmplitude);
            hash = Mix(hash, shape.NoiseFrequency);
            hash = Mix(hash, shape.CliffArcDeg);
            hash = Mix(hash, shape.CliffDirectionDeg);
            hash = Mix(hash, shape.CliffSkirtRatio);
            hash = Mix(hash, shape.CliffCutOffsetRatio);
            hash = Mix(hash, shape.CliffBlendDeg);
            hash = Mix(hash, (int)shape.Blend);
            return hash;
        }

        private static int MixRiverShape(int hash, RiverShape shape)
        {
            if (shape == null) return hash;
            hash = Mix(hash, shape.WidthMeters);
            hash = Mix(hash, shape.DepthMeters);
            hash = Mix(hash, shape.WaterTagRatio);
            return hash;
        }

        private static int MixLakeStamp(int hash, LakeStampShape shape)
        {
            if (shape == null) return hash;
            hash = Mix(hash, shape.Radius);
            hash = Mix(hash, shape.DepthMeters);
            hash = Mix(hash, shape.WaterSurfaceRatio);
            hash = Mix(hash, shape.NoiseAmplitude);
            hash = Mix(hash, shape.NoiseFrequency);
            return hash;
        }

        private static int MixGroundPatchStamp(int hash, GroundPatchStampShape shape)
        {
            if (shape == null) return hash;
            hash = Mix(hash, (int)shape.State);
            hash = Mix(hash, shape.Radius);
            hash = Mix(hash, shape.OverrideExistingState ? 1 : 0);
            hash = Mix(hash, shape.MaxHeight);
            hash = Mix(hash, shape.NoiseAmplitude);
            hash = Mix(hash, shape.NoiseFrequency);
            return hash;
        }

        private static int MixForestStamp(int hash, ForestClusterStampShape shape)
        {
            if (shape == null) return hash;
            hash = Mix(hash, shape.Radius);
            hash = Mix(hash, shape.TreeCount);
            hash = Mix(hash, shape.TreeMinDistance);
            hash = Mix(hash, shape.MaxHeight);
            hash = Mix(hash, shape.MaxAttemptsPerTree);
            hash = Mix(hash, shape.NoiseAmplitude);
            hash = Mix(hash, shape.NoiseFrequency);
            return hash;
        }

        private static int Mix(int hash, int value) => unchecked(hash * 31 + value);

        private static int Mix(int hash, float value)
        {
            return Mix(hash, System.BitConverter.SingleToInt32Bits(value));
        }

        private static int Mix(int hash, Vector2 value)
        {
            hash = Mix(hash, value.x);
            return Mix(hash, value.y);
        }

        private static int StableStringHash(string value)
        {
            if (string.IsNullOrEmpty(value)) return 0;
            unchecked
            {
                int hash = 23;
                for (int i = 0; i < value.Length; i++)
                    hash = hash * 31 + value[i];
                return hash;
            }
        }

        public void SetBakedNavMesh(NavMeshData navMeshData, int fingerprint)
        {
            _bakedNavMesh = navMeshData;
            _navMeshBakeFingerprint = fingerprint;
        }

        public void SetBakedMapData(BakedMapData bakedMapData)
        {
            _bakedMapData = bakedMapData;
        }

        public void SetBakedAssaultRoutes(
            List<AuthoredBakedAssaultRoute> allyRoutes,
            List<AuthoredBakedAssaultRoute> enemyRoutes,
            int fingerprint)
        {
            _bakedAllyAssaultRoutes = allyRoutes ?? new List<AuthoredBakedAssaultRoute>();
            _bakedEnemyAssaultRoutes = enemyRoutes ?? new List<AuthoredBakedAssaultRoute>();
            _assaultRouteBakeFingerprint = fingerprint;
            _hasBakedAssaultRoutes = true;
        }

        public void SetBakedPreview(Texture2D preview, int fingerprint)
        {
            _bakedPreview = preview;
            _previewBakeFingerprint = fingerprint;
        }
    }
}
