using System;
using UnityEngine;

namespace WarSimulation.Combat.Map
{
    /// <summary>
    /// 大構造フェーズで押す <see cref="HeightStampShape"/> と、そのプリセットの目標個数。
    /// リストの先頭から順に処理されるので、大きい山を先に置きたい場合は上に並べる。
    /// </summary>
    [Serializable]
    public sealed class StructureStampEntry
    {
        [Tooltip("このプリセットを目標として何個押すか。0 の行は無視。")]
        [Min(0)] public int Count = 1;

        public HeightStampShape Shape;
    }
}
