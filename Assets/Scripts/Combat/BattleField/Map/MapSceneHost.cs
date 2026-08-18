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
        [SerializeField] private bool _hasBakedRenderFingerprint;

        public MapConfig Config
        {
            get => _config;
            set => _config = value;
        }

        public MapData LastAppliedMap { get; private set; }
        public int BakedRenderFingerprint => _bakedRenderFingerprint;
        public bool HasBakedRenderFingerprint =>
            _hasBakedRenderFingerprint || _bakedRenderFingerprint != 0;

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
            _hasBakedRenderFingerprint = true;
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
                    $"[{nameof(MapSceneHost)}] Scene 3D does not match the baked map.",
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

        /// <summary>3D生成物を変更せず、侵攻ルート検証に使う保存済みNavMeshだけをロードする。</summary>
        public bool LoadBakedNavMeshForValidation(NavMeshData prebakedNavMesh)
        {
            if (prebakedNavMesh == null) return false;
            CombatNavMeshBuilder navMeshBuilder = GetOrAddComponent<CombatNavMeshBuilder>();
            return navMeshBuilder.Load(prebakedNavMesh);
        }

        /// <summary>シーン3Dが指定したベイク済みマップと一致するかを副作用なしで返す。</summary>
        public bool HasBakedRenderDataFor(MapData map, int fingerprint) =>
            map != null && HasBakedRenderData(map, fingerprint);

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
            if (!HasExpectedChildren("GeneratedRivers", map.Rivers.Count)) return false;
            if (!HasExpectedChildren("GeneratedLakes", map.Lakes.Count)) return false;

            int bridgeCount = 0;
            int featureCount = 0;
            for (int i = 0; i < map.Features.Count; i++)
            {
                FeatureType type = map.Features[i].Type;
                if (type == FeatureType.Bridge) bridgeCount++;
                if (type == FeatureType.Tree || type == FeatureType.Rock ||
                    type == FeatureType.OwnMainStone || type == FeatureType.EnemyMainStone)
                {
                    featureCount++;
                }
            }

            return HasExpectedChildren("GeneratedBridges", bridgeCount) &&
                HasExpectedChildren("GeneratedFeatures", featureCount);
        }

        private bool HasExpectedChildren(string rootName, int expectedCount)
        {
            Transform root = transform.Find(rootName);
            return expectedCount == 0 ? root == null || root.childCount == 0 :
                root != null && root.childCount == expectedCount;
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
