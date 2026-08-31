using System;
using UnityEngine;

namespace WarSimulation.Combat.Map
{
    [Serializable]
    public struct FeaturePlacementRadii
    {
        [Min(0f)] public float Rock;
        [Tooltip("幹・枝・根を含む半径。木同士の判定に使用する。"), Min(0f)] public float Tree;
        [Tooltip("葉を含む半径。岩・魔石・橋との判定に使用する。"), Min(0f)] public float TreeCanopy;
        [Tooltip("台座を含む魔石の半径。"), Min(0f)] public float MagicStone;
        [Min(0f)] public float Clearance;

        public float Radius(FeatureType type, FeatureType other)
        {
            switch (type)
            {
                case FeatureType.Rock: return Rock;
                case FeatureType.Tree: return other == FeatureType.Tree ? Tree : Mathf.Max(Tree, TreeCanopy);
                case FeatureType.OwnMainStone:
                case FeatureType.EnemyMainStone: return MagicStone;
                default: return 0f;
            }
        }

    }
}
