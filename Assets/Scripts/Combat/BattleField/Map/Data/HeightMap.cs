using System;
using UnityEngine;

namespace WarSimulation.Combat.Map
{
    /// <summary>
    /// 地形の高度（Y 座標）を 2D 配列で保持する純粋データクラス。
    /// ワールド座標はマップ左下（XZ 平面の原点）を基準とする。
    /// <see cref="CliffFaces"/> は <see cref="HeightStampShape"/> が崖スカートと判定したセル（高さと同じセル対応）。
    /// </summary>
    public class HeightMap
    {
        private readonly float[,] _values;

        public int Width { get; }
        public int Height { get; }
        public float CellSize { get; }

        /// <summary>
        /// 崖面セル。スタンプ適用時に <see cref="HeightStampShape"/> と同一の幾何判定で書き込まれる。
        /// </summary>
        public CliffFaceGrid CliffFaces { get; }

        public HeightMap(int width, int height, float cellSize)
        {
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
            if (cellSize <= 0f) throw new ArgumentOutOfRangeException(nameof(cellSize));

            Width = width;
            Height = height;
            CellSize = cellSize;
            _values = new float[width, height];
            CliffFaces = new CliffFaceGrid(width, height, cellSize);
        }

        public float GetHeight(int x, int z) => _values[x, z];

        public void SetHeight(int x, int z, float value) => _values[x, z] = value;

        public bool IsInBounds(int x, int z) =>
            x >= 0 && x < Width && z >= 0 && z < Height;

        public Vector2 WorldSize => new Vector2(Width * CellSize, Height * CellSize);

        /// <summary>
        /// ワールド XZ 座標を受け取り、線形補間で高度を返す。
        /// マップ外の座標は端にクランプして評価する。
        /// </summary>
        public float SampleAt(Vector3 worldPos)
        {
            float u = worldPos.x / CellSize;
            float v = worldPos.z / CellSize;

            int x0 = Mathf.Clamp(Mathf.FloorToInt(u), 0, Width - 1);
            int z0 = Mathf.Clamp(Mathf.FloorToInt(v), 0, Height - 1);
            int x1 = Mathf.Min(x0 + 1, Width - 1);
            int z1 = Mathf.Min(z0 + 1, Height - 1);

            float fx = Mathf.Clamp01(u - x0);
            float fz = Mathf.Clamp01(v - z0);

            float h00 = _values[x0, z0];
            float h10 = _values[x1, z0];
            float h01 = _values[x0, z1];
            float h11 = _values[x1, z1];

            float hx0 = Mathf.Lerp(h00, h10, fx);
            float hx1 = Mathf.Lerp(h01, h11, fx);
            return Mathf.Lerp(hx0, hx1, fz);
        }

        /// <summary>
        /// ワールド XZ 座標における地形の勾配を度数で返す。
        /// 中心差分の基線は <see cref="CellSize"/> × <paramref name="sampleSpacingMultiplier"/>。
        /// 既定 1 は 1 セル幅。マルチプライを大きくすると細かい凹凸の影響が減り、見た目より急に出る誤差が抑えられる。
        /// </summary>
        public float SampleSlopeDeg(Vector3 worldPos, float sampleSpacingMultiplier = 1f)
        {
            float eps = CellSize * Mathf.Max(0.25f, sampleSpacingMultiplier);
            float hE = SampleAt(new Vector3(worldPos.x + eps, 0f, worldPos.z));
            float hW = SampleAt(new Vector3(worldPos.x - eps, 0f, worldPos.z));
            float hN = SampleAt(new Vector3(worldPos.x, 0f, worldPos.z + eps));
            float hS = SampleAt(new Vector3(worldPos.x, 0f, worldPos.z - eps));

            float dydx = (hE - hW) / (2f * eps);
            float dydz = (hN - hS) / (2f * eps);
            float gradientMagnitude = Mathf.Sqrt(dydx * dydx + dydz * dydz);
            return Mathf.Atan(gradientMagnitude) * Mathf.Rad2Deg;
        }

        /// <summary>グリッド (x,z) が崖面セルか。高さの値からは分からないので必ずこちらか <see cref="SampleCliffFace"/> を使う。</summary>
        public bool IsCliffFaceCell(int x, int z) => CliffFaces.Get(x, z);

        /// <summary>
        /// ワールド位置が崖面セルに含まれるか。<see cref="CliffFaces"/> と同じ（スタンプと同一手順で付いたフラグ）。
        /// </summary>
        public bool SampleCliffFace(Vector3 worldPos) => CliffFaces.SampleAt(worldPos);
    }
}
