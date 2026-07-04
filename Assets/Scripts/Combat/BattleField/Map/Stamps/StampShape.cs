using UnityEngine;

namespace WarSimulation.Combat.Map
{
    /// <summary>
    /// 全スタンプの基底。ScriptableObject として Assets 上に並べて管理する。
    /// 派生クラスは <see cref="Apply"/> で自身が持つパラメータに従って
    /// MapData を変更する。
    /// </summary>
    public abstract class StampShape : ScriptableObject
    {
        [SerializeField] private string _displayName;

        public string DisplayName => _displayName;

        /// <summary>
        /// 指定された配置でスタンプを 1 回押す。
        /// 引数の map を直接書き換える。
        /// </summary>
        public abstract void Apply(MapData map, StampPlacement placement);
    }
}
