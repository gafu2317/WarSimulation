using System.Collections.Generic;
using UnityEngine;

namespace WarSimulation.Combat.Map
{
    public readonly struct MapGenerationPhaseTiming
    {
        public string Name { get; }
        public long ElapsedMilliseconds { get; }

        public MapGenerationPhaseTiming(string name, long elapsedMilliseconds)
        {
            Name = name;
            ElapsedMilliseconds = elapsedMilliseconds;
        }
    }

    /// <summary>
    /// マップ生成の司令塔。Config を受け取り、フェーズを順に実行して MapData を返す。
    /// MonoBehaviour として Scene に 1 つ置いて使う想定だが、コアロジックである
    /// Generate() は Unity 依存を最小化しており、テストからも直接呼び出せる。
    /// </summary>
    public sealed class MapGenerator : MonoBehaviour
    {
        [SerializeField] private MapGenerationConfig _config;

        [Tooltip("true の場合は毎回ランダムなシードを使う。false なら _seed を使う（再現性あり）。")]
        [SerializeField] private bool _useRandomSeed = true;

        [SerializeField] private int _seed;

        // パイプラインは 3 レイヤ（地形=高低差 / 地面=状態 / オブジェクト=点配置）で積み上げる。
        //   Mountain   : 大山 1 個＋小山 N 個を先に配置し、MapData.Mountains に記録
        //   River      : 山生成後の高さ 0 セルだけを通ってマップ端〜端の川を探索
        //   Lake       : 湖のボウル型くぼみ＋ Water タグ付け（既存の川 Water を避ける）
        //   GroundPatch: 沼・雪などの地面状態パッチ（Water は保護）
        //   Bridge     : 川の経路上に橋を複数配置（後続フェーズが近傍除外に参照）
        //   Forest     : 木のクラスター。ゾーンを登録し PlacedFeature.Tree を散布
        //   TreeScatter: クラスター外に木をマップ全体へ散布（Water・森ゾーン・橋近傍・既存の木を避ける）
        //   Rock       : 岩をマップ全体に散布（Water セル＋森ゾーン＋橋近傍を避ける）
        //   Decoration : 魔石配置（Water セル＋森ゾーン＋橋近傍除外）
        private readonly List<IMapGenerationPhase> _phases = new()
        {
            new MountainPhase(),
            new RiverPhase(),
            new LakePhase(),
            new GroundPatchPhase(),
            new BridgePhase(),
            new ForestPhase(),
            new TreeScatterPhase(),
            new RockPhase(),
            new DecorationPhase(),
        };

        public MapGenerationConfig Config
        {
            get => _config;
            set => _config = value;
        }

        public MapData LastGeneratedMap { get; private set; }
        public IReadOnlyList<MapGenerationPhaseTiming> LastPhaseTimings => _lastPhaseTimings;

        private readonly List<MapGenerationPhaseTiming> _lastPhaseTimings = new();

        /// <summary>
        /// Config に従ってマップを 1 枚生成して返す。
        /// </summary>
        public MapData Generate()
        {
            if (_config == null)
            {
                Debug.LogError($"[{nameof(MapGenerator)}] MapGenerationConfig is not assigned.");
                return null;
            }

            int seed = _useRandomSeed ? unchecked(System.Environment.TickCount ^ GetHashCode()) : _seed;
            return Generate(seed);
        }

        public MapData Generate(int seed)
        {
            if (_config == null)
            {
                Debug.LogError($"[{nameof(MapGenerator)}] MapGenerationConfig is not assigned.");
                return null;
            }

            IRandom rng = new SystemRandom(seed);

            MapData map = MapDataFactory.CreateEmptyMap(_config, seed);
            map.BridgeFeatureExclusionMargin = _config.BridgeFeatureExclusionMargin;
            _lastPhaseTimings.Clear();
            foreach (IMapGenerationPhase phase in _phases)
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                phase.Execute(map, rng, _config);
                sw.Stop();
                _lastPhaseTimings.Add(new MapGenerationPhaseTiming(
                    phase.GetType().Name,
                    sw.ElapsedMilliseconds));
            }
            LastGeneratedMap = map;
            SetCombatMapSystemCurrentMap(map);
            return map;
        }

        /// <summary>
        /// 既存の MapData（手作りマップなど）を CurrentMap に載せ、必要なら 3D 描画する。
        /// </summary>
        public bool ApplyMapData(MapData map, bool render3D = true, bool bakeNavMesh = true)
        {
            if (map == null)
            {
                Debug.LogWarning($"[{nameof(MapGenerator)}] ApplyMapData called with null MapData.");
                return false;
            }

            LastGeneratedMap = map;
            SetCombatMapSystemCurrentMap(map);
            if (!render3D)
                return true;
            return Render3D(map, bakeNavMesh);
        }

        public void Render3D(MapData map) => Render3D(map, bakeNavMesh: true);

        /// <returns>NavMesh をベイクした場合に成功したかどうか。ベイクしない場合は true。</returns>
        public bool Render3D(MapData map, bool bakeNavMesh)
        {
            if (map == null)
            {
                Debug.LogWarning($"[{nameof(MapGenerator)}] Render3D called with null MapData.");
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
