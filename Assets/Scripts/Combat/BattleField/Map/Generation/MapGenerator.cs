using System.Collections.Generic;
using UnityEngine;

namespace WarSimulation.Combat.Map
{
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
        //   River      : 平地状態でマップ端〜端を蛇行パス化し掘削＋ Water タグ付け
        //   Lake       : 湖のボウル型くぼみ＋ Water タグ付け（山の前に置くので山が湖を避ける）
        //   Structure  : Water セル（川・湖）を避けながら山・丘・盆地を配置
        //   GroundPatch: 沼・雪などの地面状態パッチ（Water は保護）
        //   Forest     : 木のクラスター。ゾーンを登録し PlacedFeature.Tree を散布
        //   TreeScatter: クラスター外に木をマップ全体へ散布（Water・森ゾーン・既存の木を避ける）
        //   Rock       : 岩をマップ全体に散布（Water セル＋森ゾーンを避ける）
        //   Decoration : 魔石配置（Water セル除外）
        //   Bridge     : 川の経路上に橋を複数配置
        private readonly List<IMapGenerationPhase> _phases = new()
        {
            new RiverPhase(),
            new LakePhase(),
            new StructurePhase(),
            new GroundPatchPhase(),
            new ForestPhase(),
            new TreeScatterPhase(),
            new RockPhase(),
            new DecorationPhase(),
            new BridgePhase(),
        };

        public MapGenerationConfig Config
        {
            get => _config;
            set => _config = value;
        }

        public MapData LastGeneratedMap { get; private set; }

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
            IRandom rng = new SystemRandom(seed);

            MapData map = CreateEmptyMap(_config, seed);
            foreach (IMapGenerationPhase phase in _phases)
            {
                phase.Execute(map, rng, _config);
            }
            LastGeneratedMap = map;
            SetCombatMapSystemCurrentMap(map);
            return map;
        }

        public void Render3D(MapData map)
        {
            if (map == null)
            {
                Debug.LogWarning($"[{nameof(MapGenerator)}] Render3D called with null MapData.");
                return;
            }

            TerrainRenderer terrainRenderer = GetOrAddComponent<TerrainRenderer>();
            terrainRenderer.Render(map);

            RiverRenderer riverRenderer = GetOrAddComponent<RiverRenderer>();
            riverRenderer.Render(map);

            LakeRenderer lakeRenderer = GetOrAddComponent<LakeRenderer>();
            lakeRenderer.Render(map);

            BridgeRenderer bridgeRenderer = GetOrAddComponent<BridgeRenderer>();
            bridgeRenderer.Render(map, _config);

            FeatureRenderer featureRenderer = GetOrAddComponent<FeatureRenderer>();
            featureRenderer.Render(map);
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

        private static MapData CreateEmptyMap(MapGenerationConfig config, int seed)
        {
            // HeightMap と GroundStateGrid は同じ解像度・同じセルサイズで持つ。
            // 旧設計では別解像度に出来たが「認知コストが高い」ため 1 本に統一した。
            int resolution = config.HeightMapResolution;
            float cellSize = config.HeightMapCellSize;

            var height = new HeightMap(resolution, resolution, cellSize);
            var grid = new GroundStateGrid(resolution, resolution, cellSize);

            return new MapData(height, grid, seed);
        }
    }
}
