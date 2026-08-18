using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Scripting.APIUpdating;

namespace WarSimulation.Combat.Map
{
    /// <summary>
    /// 戦闘マップのシーン側ホスト。MapData の 3D 反映・NavMesh ベイク・マップ原点を担う。
    /// </summary>
    [MovedFrom(true, "WarSimulation.Combat.Map", "Assembly-CSharp", "MapGenerator")]
    public sealed class MapSceneHost : MonoBehaviour
    {
        [SerializeField] private MapConfig _config;
        [SerializeField] private int _bakedRenderFingerprint;

        public MapConfig Config
        {
            get => _config;
            set => _config = value;
        }

        public MapData LastAppliedMap { get; private set; }

        /// <summary>
        /// 既存の MapData（手作りマップなど）を CurrentMap に載せ、必要なら 3D 描画する。
        /// </summary>
        public bool ApplyMapData(
            MapData map,
            bool render3D = true,
            bool bakeNavMesh = true,
            NavMeshData prebakedNavMesh = null)
        {
            if (map == null)
            {
                Debug.LogWarning($"[{nameof(MapSceneHost)}] ApplyMapData called with null MapData.");
                return false;
            }

            if (!render3D)
            {
                LastAppliedMap = map;
                SetCombatMapSystemCurrentMap(map);
                return true;
            }

            if (!Render3D(map, bakeNavMesh, prebakedNavMesh)) return false;
            LastAppliedMap = map;
            SetCombatMapSystemCurrentMap(map);
            return true;
        }

        public void SetBakedRenderFingerprint(int fingerprint)
        {
            _bakedRenderFingerprint = fingerprint;
        }

        public bool LoadBakedMap(MapData map, NavMeshData prebakedNavMesh, int fingerprint)
        {
            if (map == null || prebakedNavMesh == null)
            {
                Debug.LogError($"[{nameof(MapSceneHost)}] Baked map and NavMeshData are required.", this);
                return false;
            }

            if (!HasBakedRenderData(map, fingerprint))
            {
                Debug.LogError(
                    $"[{nameof(MapSceneHost)}] Generated map render data is missing from the scene.",
                    this);
                return false;
            }

            CombatNavMeshBuilder navMeshBuilder = GetComponent<CombatNavMeshBuilder>();
            if (navMeshBuilder == null)
            {
                Debug.LogError($"[{nameof(MapSceneHost)}] CombatNavMeshBuilder is missing.", this);
                return false;
            }

            if (!navMeshBuilder.Load(prebakedNavMesh)) return false;
            SetCombatMapSystemCurrentMap(map);
            return true;
        }

        public void Render3D(MapData map) => Render3D(map, bakeNavMesh: true, prebakedNavMesh: null);

        /// <returns>NavMesh を用意できたかどうか。ベイクもロードもしない場合は true。</returns>
        public bool Render3D(MapData map, bool bakeNavMesh, NavMeshData prebakedNavMesh = null)
        {
            if (map == null)
            {
                Debug.LogWarning($"[{nameof(MapSceneHost)}] Render3D called with null MapData.");
                return false;
            }

            TerrainRenderer terrainRenderer = GetOrAddComponent<TerrainRenderer>();
            terrainRenderer.Render(map);

            TerrainSkirtRenderer terrainSkirtRenderer = GetOrAddComponent<TerrainSkirtRenderer>();
            terrainSkirtRenderer.Render(map);

            RiverRenderer riverRenderer = GetOrAddComponent<RiverRenderer>();
            riverRenderer.Render(map);

            LakeRenderer lakeRenderer = GetOrAddComponent<LakeRenderer>();
            lakeRenderer.Render(map);

            BridgeRenderer bridgeRenderer = GetOrAddComponent<BridgeRenderer>();
            bridgeRenderer.Render(map, _config);

            FeatureRenderer featureRenderer = GetOrAddComponent<FeatureRenderer>();
            featureRenderer.Render(map);

            global::CombatNavMeshBuilder navMeshBuilder = GetOrAddComponent<global::CombatNavMeshBuilder>();
            if (prebakedNavMesh != null)
            {
                return navMeshBuilder.Load(prebakedNavMesh);
            }

            if (!bakeNavMesh)
            {
                navMeshBuilder.Clear();
                return true;
            }

            return navMeshBuilder.Build(map);
        }

        public void Clear3D()
        {
            GetComponent<CombatNavMeshBuilder>()?.Clear();
            GetComponent<FeatureRenderer>()?.Clear();
            GetComponent<BridgeRenderer>()?.Clear();
            GetComponent<LakeRenderer>()?.Clear();
            GetComponent<RiverRenderer>()?.Clear();
            GetComponent<TerrainSkirtRenderer>()?.Clear();
            GetComponent<TerrainRenderer>()?.Clear();
        }

        private bool HasBakedRenderData(MapData map, int fingerprint)
        {
            if (_bakedRenderFingerprint != fingerprint) return false;
            if (transform.Find("GeneratedTerrain") == null) return false;
            if (map.Rivers.Count > 0 && transform.Find("GeneratedRivers") == null) return false;
            if (map.Lakes.Count > 0 && transform.Find("GeneratedLakes") == null) return false;

            bool hasBridge = false;
            bool hasFeature = false;
            for (int i = 0; i < map.Features.Count; i++)
            {
                FeatureType type = map.Features[i].Type;
                if (type == FeatureType.Bridge) hasBridge = true;
                if (type == FeatureType.Tree || type == FeatureType.Rock ||
                    type == FeatureType.OwnMainStone || type == FeatureType.EnemyMainStone)
                {
                    hasFeature = true;
                }
            }

            if (hasBridge && transform.Find("GeneratedBridges") == null) return false;
            if (hasFeature && transform.Find("GeneratedFeatures") == null) return false;
            return true;
        }

        private static void SetCombatMapSystemCurrentMap(MapData map)
        {
            CombatSceneContext context = CombatSceneContext.Instance;
            if (context != null && context.MapSystem != null)
            {
                context.MapSystem.SetCurrentMap(map);
                return;
            }

            CombatMapSystem mapSystem = FindAnyObjectByType<CombatMapSystem>();
            if (mapSystem != null)
            {
                mapSystem.SetCurrentMap(map);
            }
        }

        private T GetOrAddComponent<T>() where T : Component
        {
            T component = GetComponent<T>();
            if (component != null) return component;
            return gameObject.AddComponent<T>();
        }
    }
}
