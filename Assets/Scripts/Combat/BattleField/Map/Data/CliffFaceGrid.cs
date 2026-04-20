using UnityEngine;

namespace WarSimulation.Combat.Map
{
    /// <summary>
    /// スタンプの Cliff 扇＋スカート判定（<see cref="HeightStampShape"/> と同一手順）で立てた「崖面」フラグ。
    /// <see cref="HeightMap"/> が保持し、地形グリッドと解像度を共有する。
    /// </summary>
    public sealed class CliffFaceGrid
    {
        private readonly bool[,] _cells;

        public int Width { get; }
        public int Height { get; }
        public float CellSize { get; }

        public CliffFaceGrid(int width, int height, float cellSize)
        {
            Width = width;
            Height = height;
            CellSize = cellSize;
            _cells = new bool[width, height];
        }

        public bool Get(int x, int z) => _cells[x, z];

        /// <summary>いずれかのスタンプが崖面と判定したセルを true にする（OR）。</summary>
        public void MarkCliff(int x, int z) => _cells[x, z] = true;

        public bool SampleAt(Vector3 worldPos)
        {
            int x = Mathf.Clamp(Mathf.FloorToInt(worldPos.x / CellSize), 0, Width - 1);
            int z = Mathf.Clamp(Mathf.FloorToInt(worldPos.z / CellSize), 0, Height - 1);
            return _cells[x, z];
        }
    }
}
