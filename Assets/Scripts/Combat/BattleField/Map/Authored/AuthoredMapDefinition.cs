using System.Collections.Generic;
using UnityEngine;

namespace WarSimulation.Combat.Map
{
    /// <summary>
    /// 手作りマップ 1 枚分の配置レシピ。実行時は <see cref="AuthoredMapBuilder"/> で MapData に展開する。
    /// </summary>
    [CreateAssetMenu(menuName = "WarSim/Map/手作りマップ", fileName = "AuthoredMap")]
    public sealed class AuthoredMapDefinition : ScriptableObject
    {
        [Tooltip("グリッドサイズ・橋寸法・既定の川形状などを共有する設定。自動生成 Config を流用してよい。")]
        [SerializeField] private MapGenerationConfig _sharedConfig;

        [SerializeField] private int _buildSeed;

        [SerializeField] private List<AuthoredMountainPlacement> _mountains = new();
        [SerializeField] private List<AuthoredRiverPlacement> _rivers = new();
        [SerializeField] private List<AuthoredLakePlacement> _lakes = new();
        [SerializeField] private List<AuthoredGroundPatchPlacement> _groundPatches = new();
        [SerializeField] private List<AuthoredForestPlacement> _forests = new();
        [SerializeField] private List<AuthoredBridgePlacement> _bridges = new();
        // 散布木・岩は AuthoredMapBuilder が SharedConfig の自動生成ルールで配置する（リストは未使用・互換用）。
        [SerializeField] private List<AuthoredPointFeaturePlacement> _trees = new();
        [SerializeField] private List<AuthoredPointFeaturePlacement> _rocks = new();
        [SerializeField] private List<AuthoredMagicStonePlacement> _magicStones = new();

        public MapGenerationConfig SharedConfig
        {
            get => _sharedConfig;
            set => _sharedConfig = value;
        }

        public int BuildSeed
        {
            get => _buildSeed;
            set => _buildSeed = value;
        }

        public List<AuthoredMountainPlacement> Mountains => _mountains;
        public List<AuthoredRiverPlacement> Rivers => _rivers;
        public List<AuthoredLakePlacement> Lakes => _lakes;
        public List<AuthoredGroundPatchPlacement> GroundPatches => _groundPatches;
        public List<AuthoredForestPlacement> Forests => _forests;
        public List<AuthoredBridgePlacement> Bridges => _bridges;
        public List<AuthoredPointFeaturePlacement> Trees => _trees;
        public List<AuthoredPointFeaturePlacement> Rocks => _rocks;
        public List<AuthoredMagicStonePlacement> MagicStones => _magicStones;
    }
}
